using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using TTNOverlay.Services;

namespace TTNOverlay.Twitch;

/// <summary>
/// The transport half of Twitch EventSub over WebSocket, shared by every EventSub client in the app (channel points
/// redemptions, AutoMod). Subclasses only say which subscriptions to create and what to do with each notification.
/// No third-party dependencies: System.Net.WebSockets + System.Text.Json.
///
/// Flow: connect -> session_welcome (session id) -> POST /helix/eventsub/subscriptions with that id (must happen
/// within 10 s or Twitch closes the socket) -> notifications. A keepalive message arrives at least every
/// keepalive_timeout_seconds (30 s here); silence longer than that means the link is dead. On session_reconnect
/// the new socket is opened before the old one is closed and subscriptions carry over (no resubscribe).
/// Every subclass owns its own WebSocket: Twitch ties subscriptions to the token that created them, and the
/// clients use different accounts/scopes.
/// </summary>
public abstract class EventSubSessionClient : IAsyncDisposable
{
    private const string WsUrl = "wss://eventsub.wss.twitch.tv/ws?keepalive_timeout_seconds=30";
    private const string SubscriptionsUrl = "https://api.twitch.tv/helix/eventsub/subscriptions";

    private const int InitialReceiveTimeoutSeconds = 30;
    private const int KeepaliveSlackSeconds = 15;
    private const int MaxRecentMessageIds = 64;

    private static readonly HttpClient Http = SharedHttpClient.Instance;

    public event Action<EventSubStatus>? StatusChanged;

    private CancellationTokenSource? _cts;
    private Task? _runTask;
    private bool _sessionWasSubscribed;

    // Twitch may deliver the same message on the old and new socket around a session_reconnect.
    private readonly Queue<string> _recentMessageIds = new();
    private readonly HashSet<string> _recentMessageIdSet = new();

    /// <summary>Prefix for this client's lines in the debug log.</summary>
    protected abstract string LogPrefix { get; }

    /// <summary>
    /// Creates this client's subscriptions for the freshly opened session. Called once per session (not after a
    /// session_reconnect, which carries them over). <paramref name="accessToken"/> is already refreshed.
    /// </summary>
    protected abstract Task<SubscribeOutcome> SubscribeAsync(
        string sessionId,
        string accessToken,
        CancellationToken ct
    );

    /// <summary>A notification arrived. Runs on the receive thread; marshal to the UI thread before touching UI state.</summary>
    protected abstract void OnNotification(string? subscriptionType, JsonElement eventData);

    protected enum SubscribeOutcome
    {
        Ok,

        /// <summary>401/403: the token is invalid or lacks the scope (or the account lacks the required role).</summary>
        NeedsRelogin,

        /// <summary>400/404: Twitch doesn't accept this request (for example an unsupported subscription version).</summary>
        Rejected,

        Failed,
    }

    private enum SessionOutcome
    {
        Disconnected,
        NeedsRelogin,
    }

    /// <summary>
    /// Connects and keeps the session alive (reconnects with backoff) until disposed. The token provider is
    /// called on every (re)subscription so an expired access token is refreshed by the caller.
    /// </summary>
    protected void StartSession(Func<Task<string?>> accessTokenProvider)
    {
        StopSession();

        var cts = new CancellationTokenSource();
        _cts = cts;
        _runTask = Task.Run(() => RunAsync(accessTokenProvider, cts.Token));
    }

    private void StopSession()
    {
        var cts = _cts;
        var run = _runTask;
        _cts = null;
        _runTask = null;
        if (cts is null)
            return;

        try
        {
            cts.Cancel();
        }
        catch { }

        _ = (run ?? Task.CompletedTask).ContinueWith(_ => cts.Dispose(), TaskScheduler.Default);
    }

    public ValueTask DisposeAsync()
    {
        StopSession();
        return ValueTask.CompletedTask;
    }

    private async Task RunAsync(Func<Task<string?>> tokenProvider, CancellationToken ct)
    {
        int backoffSeconds = 5;

        while (!ct.IsCancellationRequested)
        {
            var outcome = SessionOutcome.Disconnected;
            _sessionWasSubscribed = false;

            try
            {
                RaiseStatus(EventSubStatus.Connecting);
                outcome = await RunSessionAsync(tokenProvider, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                DebugLog.WriteException($"{LogPrefix}.RunAsync", ex);
            }

            if (ct.IsCancellationRequested)
                return;

            if (outcome == SessionOutcome.NeedsRelogin)
            {
                RaiseStatus(EventSubStatus.NeedsRelogin);
                return;
            }

            RaiseStatus(EventSubStatus.Disconnected);

            if (_sessionWasSubscribed)
                backoffSeconds = 5;

            DebugLog.Write($"{LogPrefix}: retrying in {backoffSeconds}s");
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(backoffSeconds), ct);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            backoffSeconds = Math.Min(backoffSeconds * 2, 60);
        }
    }

    private async Task<SessionOutcome> RunSessionAsync(Func<Task<string?>> tokenProvider, CancellationToken ct)
    {
        var socket = new ClientWebSocket();
        try
        {
            await socket.ConnectAsync(new Uri(WsUrl), ct);
            DebugLog.Write($"{LogPrefix}: WebSocket connected");

            bool isReconnectedSession = false;
            var receiveTimeout = TimeSpan.FromSeconds(InitialReceiveTimeoutSeconds);

            while (!ct.IsCancellationRequested)
            {
                string? json;
                try
                {
                    json = await ReceiveTextAsync(socket, receiveTimeout, ct);
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    DebugLog.Write($"{LogPrefix}: no keepalive within the timeout, reconnecting");
                    return SessionOutcome.Disconnected;
                }

                if (json is null)
                {
                    DebugLog.Write($"{LogPrefix}: socket closed by the server");
                    return SessionOutcome.Disconnected;
                }

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (!TryGetObject(root, "metadata", out var meta))
                    continue;

                TryGetObject(root, "payload", out var payload);

                if (IsDuplicate(GetString(meta, "message_id")))
                    continue;

                switch (GetString(meta, "message_type"))
                {
                    case "session_welcome":
                    {
                        if (!TryGetObject(payload, "session", out var session))
                            break;

                        var keepalive = GetInt(session, "keepalive_timeout_seconds");
                        if (keepalive is > 0)
                            receiveTimeout = TimeSpan.FromSeconds(keepalive.Value + KeepaliveSlackSeconds);

                        // After session_reconnect the existing subscriptions move over to the new session.
                        if (isReconnectedSession)
                            break;

                        var sessionId = GetString(session, "id");
                        if (string.IsNullOrEmpty(sessionId))
                            return SessionOutcome.Disconnected;

                        var subscribed = await SubscribeAllAsync(sessionId, tokenProvider, ct);
                        if (subscribed == SubscribeOutcome.NeedsRelogin)
                            return SessionOutcome.NeedsRelogin;
                        if (subscribed != SubscribeOutcome.Ok)
                            return SessionOutcome.Disconnected;

                        _sessionWasSubscribed = true;
                        RaiseStatus(EventSubStatus.Subscribed);
                        break;
                    }

                    case "session_keepalive":
                        break;

                    case "notification":
                        HandleNotification(meta, payload);
                        break;

                    case "session_reconnect":
                    {
                        string? reconnectUrl = TryGetObject(payload, "session", out var reconnectSession)
                            ? GetString(reconnectSession, "reconnect_url")
                            : null;
                        if (string.IsNullOrEmpty(reconnectUrl))
                            return SessionOutcome.Disconnected;

                        DebugLog.Write($"{LogPrefix}: session_reconnect, moving to the new socket");
                        var next = new ClientWebSocket();
                        try
                        {
                            await next.ConnectAsync(new Uri(reconnectUrl), ct);
                        }
                        catch
                        {
                            next.Dispose();
                            throw;
                        }

                        var old = socket;
                        socket = next;
                        isReconnectedSession = true;
                        CloseQuietly(old);
                        break;
                    }

                    case "revocation":
                    {
                        string? status = TryGetObject(payload, "subscription", out var revoked)
                            ? GetString(revoked, "status")
                            : null;
                        DebugLog.Write($"{LogPrefix}: subscription revoked ({status})");
                        return status == "authorization_revoked"
                            ? SessionOutcome.NeedsRelogin
                            : SessionOutcome.Disconnected;
                    }
                }
            }

            return SessionOutcome.Disconnected;
        }
        finally
        {
            CloseQuietly(socket);
        }
    }

    private static async Task<string?> ReceiveTextAsync(
        ClientWebSocket socket,
        TimeSpan timeout,
        CancellationToken ct
    )
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(timeout);

        var buffer = new byte[8192];
        using var ms = new MemoryStream();

        while (true)
        {
            var result = await socket.ReceiveAsync(buffer, timeoutCts.Token);
            if (result.MessageType == WebSocketMessageType.Close)
                return null;

            ms.Write(buffer, 0, result.Count);
            if (result.EndOfMessage)
                break;
        }

        return Encoding.UTF8.GetString(ms.GetBuffer(), 0, (int)ms.Length);
    }

    private static void CloseQuietly(ClientWebSocket socket)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                if (socket.State == WebSocketState.Open)
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                    await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "bye", cts.Token);
                }
            }
            catch { }
            finally
            {
                socket.Dispose();
            }
        });
    }

    private bool IsDuplicate(string? messageId)
    {
        if (string.IsNullOrEmpty(messageId))
            return false;
        if (!_recentMessageIdSet.Add(messageId))
            return true;

        _recentMessageIds.Enqueue(messageId);
        while (_recentMessageIds.Count > MaxRecentMessageIds)
            _recentMessageIdSet.Remove(_recentMessageIds.Dequeue());
        return false;
    }

    private async Task<SubscribeOutcome> SubscribeAllAsync(
        string sessionId,
        Func<Task<string?>> tokenProvider,
        CancellationToken ct
    )
    {
        var token = await tokenProvider();
        if (string.IsNullOrWhiteSpace(token))
        {
            // Could be a transient refresh failure (network); retry with backoff instead of nagging for a login.
            DebugLog.Write($"{LogPrefix}: no access token available");
            return SubscribeOutcome.Failed;
        }

        return await SubscribeAsync(sessionId, token, ct);
    }

    /// <summary>POSTs one subscription for the session. <paramref name="condition"/> holds the type's condition fields.</summary>
    protected async Task<SubscribeOutcome> CreateSubscriptionAsync(
        string type,
        string version,
        IReadOnlyDictionary<string, string> condition,
        string sessionId,
        string accessToken,
        CancellationToken ct
    )
    {
        try
        {
            using var body = new MemoryStream();
            using (var writer = new Utf8JsonWriter(body))
            {
                writer.WriteStartObject();
                writer.WriteString("type", type);
                writer.WriteString("version", version);
                writer.WriteStartObject("condition");
                foreach (var (key, value) in condition)
                    writer.WriteString(key, value);
                writer.WriteEndObject();
                writer.WriteStartObject("transport");
                writer.WriteString("method", "websocket");
                writer.WriteString("session_id", sessionId);
                writer.WriteEndObject();
                writer.WriteEndObject();
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, SubscriptionsUrl)
            {
                Content = new ByteArrayContent(body.ToArray()),
            };
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            request.Headers.Add("Client-Id", TwitchAuthService.ClientId);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            using var response = await Http.SendAsync(request, ct);
            if (response.IsSuccessStatusCode)
            {
                DebugLog.Write($"{LogPrefix}: subscribed to {type} v{version}");
                return SubscribeOutcome.Ok;
            }

            var responseBody = await response.Content.ReadAsStringAsync(ct);
            DebugLog.Write(
                $"{LogPrefix}: subscribing to {type} v{version} failed, status {(int)response.StatusCode}: {responseBody}"
            );

            // 401: token invalid. 403: token lacks the scope (logged in before it existed) or the account lacks the role.
            return response.StatusCode switch
            {
                System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden =>
                    SubscribeOutcome.NeedsRelogin,
                System.Net.HttpStatusCode.BadRequest or System.Net.HttpStatusCode.NotFound => SubscribeOutcome.Rejected,
                _ => SubscribeOutcome.Failed,
            };
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            DebugLog.WriteException($"{LogPrefix}.CreateSubscriptionAsync({type})", ex);
            return SubscribeOutcome.Failed;
        }
    }

    private void HandleNotification(JsonElement meta, JsonElement payload)
    {
        if (!TryGetObject(payload, "event", out var ev))
            return;

        try
        {
            OnNotification(GetString(meta, "subscription_type"), ev);
        }
        catch (Exception ex)
        {
            DebugLog.WriteException($"{LogPrefix}.OnNotification", ex);
        }
    }

    private void RaiseStatus(EventSubStatus status)
    {
        try
        {
            StatusChanged?.Invoke(status);
        }
        catch (Exception ex)
        {
            DebugLog.WriteException($"{LogPrefix}.RaiseStatus", ex);
        }
    }

    protected static bool TryGetObject(JsonElement parent, string name, out JsonElement child)
    {
        child = default;
        if (
            parent.ValueKind == JsonValueKind.Object
            && parent.TryGetProperty(name, out var value)
            && value.ValueKind == JsonValueKind.Object
        )
        {
            child = value;
            return true;
        }
        return false;
    }

    protected static string? GetString(JsonElement obj, string name) =>
        obj.ValueKind == JsonValueKind.Object
        && obj.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    protected static int? GetInt(JsonElement obj, string name)
    {
        if (
            obj.ValueKind == JsonValueKind.Object
            && obj.TryGetProperty(name, out var value)
            && value.ValueKind == JsonValueKind.Number
            && value.TryGetInt32(out var number)
        )
            return number;
        return null;
    }
}
