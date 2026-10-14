using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using TTNOverlay.Services;

namespace TTNOverlay.Kick;

/// <summary>
/// Per-message moderation on Kick through the official public API (api.kick.com/public/v1): delete a message,
/// time out or ban its author. Mirrors what ModerationService does on Twitch so ModerationActionRunner can drive both.
/// The token comes from <see cref="KickSessionService"/>.
/// </summary>
/// <remarks>
/// Kick has no "warn" action in its public API, so <see cref="WarnAsync"/> always returns false (the UI never offers
/// it for Kick rows). Needs the scopes moderation:chat_message:manage (delete) and moderation:ban (timeout/ban);
/// both are in KickAuthService.RequiredScopes.
/// </remarks>
public sealed partial class KickModerationService : IChatModerationActions
{
    private const string ApiBase = "https://api.kick.com/public/v1";

    /// <summary>Kick timeouts are expressed in whole minutes: 1 minute up to 7 days.</summary>
    private const int MinTimeoutMinutes = 1;

    private const int MaxTimeoutMinutes = 10080;

    private static readonly HttpClient Http = SharedHttpClient.Instance;

    private readonly KickSessionService _session;

    // slug -> broadcaster user id. A channel's id never changes, so it is resolved once.
    private readonly ConcurrentDictionary<string, long> _broadcasterIds = new(StringComparer.OrdinalIgnoreCase);

    public KickModerationService(KickSessionService session) => _session = session;

    public bool IsLoggedIn => _session.IsLoggedIn;

    /// <summary>Kick has no warnings.</summary>
    public Task<bool> WarnAsync(string channelLogin, string targetUserId, string reason) => Task.FromResult(false);

    public async Task<ModerationDeleteResult> DeleteMessageAsync(string channelLogin, string messageId)
    {
        try
        {
            var token = await _session.GetAccessTokenAsync();
            if (token is null)
                return ModerationDeleteResult.MissingPermission;

            using var request = new HttpRequestMessage(
                HttpMethod.Delete,
                $"{ApiBase}/chat/{Uri.EscapeDataString(messageId)}"
            );
            request.Headers.Add("Authorization", $"Bearer {token}");

            using var response = await Http.SendAsync(request);
            if (response.IsSuccessStatusCode)
                return ModerationDeleteResult.Ok;

            DebugLog.Write($"KickModerationService.DeleteMessageAsync: status {(int)response.StatusCode}");
            return response.StatusCode switch
            {
                // Token rejected or without moderation:chat_message:manage: logging in again fixes it.
                HttpStatusCode.Unauthorized => ModerationDeleteResult.MissingPermission,
                // Message already gone, too old, or from someone who can't be moderated: nothing to fix on our side.
                HttpStatusCode.BadRequest or HttpStatusCode.NotFound => ModerationDeleteResult.Rejected,
                _ => ModerationDeleteResult.Failed,
            };
        }
        catch (Exception ex)
        {
            DebugLog.WriteException("KickModerationService.DeleteMessageAsync", ex);
            return ModerationDeleteResult.Failed;
        }
    }

    public Task<bool> TimeoutAsync(string channelLogin, string targetUserId, int durationSeconds, string? reason = null)
    {
        int minutes = Math.Clamp((int)Math.Ceiling(durationSeconds / 60.0), MinTimeoutMinutes, MaxTimeoutMinutes);
        return BanCoreAsync(channelLogin, targetUserId, minutes, reason);
    }

    public Task<bool> BanAsync(string channelLogin, string targetUserId, string? reason = null) =>
        BanCoreAsync(channelLogin, targetUserId, durationMinutes: null, reason);

    /// <summary>Lifts a ban or a timeout (DELETE /moderation/bans).</summary>
    public async Task<bool> UnbanAsync(string channelSlug, string targetUserId)
    {
        try
        {
            if (!long.TryParse(targetUserId, out var userId))
                return false;

            var token = await _session.GetAccessTokenAsync();
            if (token is null)
                return false;

            var broadcasterId = await ResolveBroadcasterIdAsync(channelSlug, token);
            if (broadcasterId is null)
                return false;

            var body = new BanRequest { BroadcasterUserId = broadcasterId.Value, UserId = userId };
            using var request = new HttpRequestMessage(HttpMethod.Delete, $"{ApiBase}/moderation/bans")
            {
                Content = JsonContent.Create(body, KickModerationJsonContext.Default.BanRequest),
            };
            request.Headers.Add("Authorization", $"Bearer {token}");

            using var response = await Http.SendAsync(request);
            if (response.IsSuccessStatusCode)
                return true;

            DebugLog.Write($"KickModerationService.UnbanAsync: status {(int)response.StatusCode}");
            return false;
        }
        catch (Exception ex)
        {
            DebugLog.WriteException("KickModerationService.UnbanAsync", ex);
            return false;
        }
    }

    /// <summary>
    /// Sends a chat message as the logged-in user (POST /chat, type "user"). Needs the chat:write scope.
    /// </summary>
    public async Task<ChatSendResult> SendChatMessageAsync(string channelSlug, string text)
    {
        try
        {
            var token = await _session.GetAccessTokenAsync();
            if (token is null)
                return ChatSendResult.MissingPermission;

            var broadcasterId = await ResolveBroadcasterIdAsync(channelSlug, token);
            if (broadcasterId is null)
                return ChatSendResult.Failed;

            string json;
            using (var stream = new MemoryStream())
            {
                using (var writer = new System.Text.Json.Utf8JsonWriter(stream))
                {
                    writer.WriteStartObject();
                    writer.WriteNumber("broadcaster_user_id", broadcasterId.Value);
                    writer.WriteString("content", text);
                    writer.WriteString("type", "user");
                    writer.WriteEndObject();
                }
                json = System.Text.Encoding.UTF8.GetString(stream.ToArray());
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, $"{ApiBase}/chat")
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
            };
            request.Headers.Add("Authorization", $"Bearer {token}");

            using var response = await Http.SendAsync(request);
            if (response.IsSuccessStatusCode)
                return ChatSendResult.Ok;

            DebugLog.Write($"KickModerationService.SendChatMessageAsync: status {(int)response.StatusCode}");
            return response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden
                ? ChatSendResult.MissingPermission
                : ChatSendResult.Failed;
        }
        catch (Exception ex)
        {
            DebugLog.WriteException("KickModerationService.SendChatMessageAsync", ex);
            return ChatSendResult.Failed;
        }
    }

    // POST /moderation/bans: with "duration" it is a timeout, without it a permanent ban.
    private async Task<bool> BanCoreAsync(string channelSlug, string targetUserId, int? durationMinutes, string? reason)
    {
        try
        {
            if (!long.TryParse(targetUserId, out var userId))
                return false;

            var token = await _session.GetAccessTokenAsync();
            if (token is null)
                return false;

            var broadcasterId = await ResolveBroadcasterIdAsync(channelSlug, token);
            if (broadcasterId is null)
                return false;

            var body = new BanRequest
            {
                BroadcasterUserId = broadcasterId.Value,
                UserId = userId,
                Duration = durationMinutes,
                Reason = string.IsNullOrWhiteSpace(reason) ? null : reason,
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, $"{ApiBase}/moderation/bans")
            {
                Content = JsonContent.Create(body, KickModerationJsonContext.Default.BanRequest),
            };
            request.Headers.Add("Authorization", $"Bearer {token}");

            using var response = await Http.SendAsync(request);
            if (response.IsSuccessStatusCode)
                return true;

            DebugLog.Write(
                $"KickModerationService.BanCoreAsync: status {(int)response.StatusCode} (duration={durationMinutes?.ToString() ?? "permanent"})"
            );
            return false;
        }
        catch (Exception ex)
        {
            DebugLog.WriteException("KickModerationService.BanCoreAsync", ex);
            return false;
        }
    }

    // GET /channels?slug=...: the ban endpoint wants the broadcaster's numeric user id, not the slug.
    private async Task<long?> ResolveBroadcasterIdAsync(string channelSlug, string token)
    {
        if (_broadcasterIds.TryGetValue(channelSlug, out var cached))
            return cached;

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"{ApiBase}/channels?slug={Uri.EscapeDataString(channelSlug)}"
        );
        request.Headers.Add("Authorization", $"Bearer {token}");

        using var response = await Http.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            DebugLog.Write($"KickModerationService.ResolveBroadcasterIdAsync: status {(int)response.StatusCode}");
            return null;
        }

        var payload = await response.Content.ReadFromJsonAsync(KickModerationJsonContext.Default.ChannelsResponse);
        var id = payload?.Data?.FirstOrDefault()?.BroadcasterUserId;
        if (id is null or <= 0)
        {
            DebugLog.Write($"KickModerationService.ResolveBroadcasterIdAsync: no channel found for '{channelSlug}'");
            return null;
        }

        _broadcasterIds[channelSlug] = id.Value;
        return id.Value;
    }

    internal sealed class BanRequest
    {
        [JsonPropertyName("broadcaster_user_id")]
        public long BroadcasterUserId { get; set; }

        [JsonPropertyName("user_id")]
        public long UserId { get; set; }

        [JsonPropertyName("duration")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? Duration { get; set; }

        [JsonPropertyName("reason")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Reason { get; set; }
    }

    internal sealed class ChannelsResponse
    {
        [JsonPropertyName("data")]
        public List<ChannelData>? Data { get; set; }
    }

    internal sealed class ChannelData
    {
        [JsonPropertyName("broadcaster_user_id")]
        public long BroadcasterUserId { get; set; }
    }

    [JsonSerializable(typeof(BanRequest))]
    [JsonSerializable(typeof(ChannelsResponse))]
    internal sealed partial class KickModerationJsonContext : JsonSerializerContext { }
}
