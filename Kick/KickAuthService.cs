using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using TTNOverlay.Generated;
using TTNOverlay.Services;

namespace TTNOverlay.Kick;

/// <summary>
/// Handles Kick OAuth 2.1 (authorization code + PKCE). The browser login runs locally; the token exchange and
/// refresh go through the ttnoverlay-auth Worker, which holds KICK_CLIENT_SECRET (Kick requires it even with PKCE).
/// </summary>
public static partial class KickAuthService
{
    private const string RedirectUri = "http://localhost:3939/";
    public const string ClientId = "01M4F1DH3RPJCE3W7GPJ9V562J";
    private const string WorkerBaseUrl = "https://ttnoverlay-auth.ttnoverlay.workers.dev";

    /// <summary>
    /// Every scope the app needs. Same mechanism as Twitch: on startup the stored session is checked against this
    /// list (<see cref="GetGrantedScopesAsync"/>) and, if anything is missing, the user is asked to log in again.
    /// Must be a subset of the scopes ticked in the Kick developer panel.
    /// </summary>
    public static readonly IReadOnlyList<string> RequiredScopes = new[]
    {
        "user:read",
        "channel:read",
        "channel:write",
        "moderation:ban",
        "moderation:chat_message:manage",
        "channel:rewards:read",
        "channel:rewards:write",
        "chat:write",
    };

    private static readonly string Scopes = string.Join(' ', RequiredScopes);

    /// <summary>The scopes the login asks for, as stored in settings (space separated).</summary>
    public static string RequestedScopesText => Scopes;

    /// <summary>The required scopes that were not part of an earlier login's request (i.e. added to the app since).</summary>
    public static IReadOnlyList<string> FindScopesAddedSince(string requestedAtLogin)
    {
        var requested = new HashSet<string>(
            requestedAtLogin.Split(' ', StringSplitOptions.RemoveEmptyEntries),
            StringComparer.OrdinalIgnoreCase
        );
        return RequiredScopes.Where(s => !requested.Contains(s)).ToList();
    }
    private static readonly HttpClient Http = SharedHttpClient.Instance;

    public class AuthResult
    {
        public string AccessToken { get; set; } = "";
        public string RefreshToken { get; set; } = "";
        public int ExpiresIn { get; set; }
        public string Login { get; set; } = "";
        public string UserId { get; set; } = "";
    }

    public static async Task<AuthResult?> LoginAsync(CancellationToken cancellationToken = default)
    {
        var state = Base64Url(RandomNumberGenerator.GetBytes(16));
        var verifier = Base64Url(RandomNumberGenerator.GetBytes(48));
        var challenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

        using var listener = new HttpListener();
        listener.Prefixes.Add(RedirectUri);

        try
        {
            listener.Start();
        }
        catch (Exception ex)
        {
            DebugLog.WriteException("KickAuthService.LoginAsync (listener.Start)", ex);
            return null;
        }

        var authorizeUrl =
            "https://id.kick.com/oauth/authorize"
            + $"?client_id={Uri.EscapeDataString(ClientId)}"
            + $"&redirect_uri={Uri.EscapeDataString(RedirectUri)}"
            + "&response_type=code"
            + $"&scope={Uri.EscapeDataString(Scopes)}"
            + $"&code_challenge={challenge}"
            + "&code_challenge_method=S256"
            + $"&state={state}";

        try
        {
            Process.Start(new ProcessStartInfo(authorizeUrl) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            DebugLog.WriteException("KickAuthService.LoginAsync (Process.Start)", ex);
            listener.Stop();
            return null;
        }

        HttpListenerContext context;
        try
        {
            using var registration = cancellationToken.Register(() =>
            {
                try { listener.Stop(); } catch { }
            });

            var getContextTask = listener.GetContextAsync();
            var timeoutTask = Task.Delay(TimeSpan.FromMinutes(3), CancellationToken.None);
            var completed = await Task.WhenAny(getContextTask, timeoutTask);
            if (completed == timeoutTask)
            {
                DebugLog.Write("KickAuthService.LoginAsync: timeout waiting for browser redirect");
                listener.Stop();
                return null;
            }

            context = await getContextTask;
        }
        catch (Exception ex)
        {
            if (cancellationToken.IsCancellationRequested)
                DebugLog.Write("KickAuthService.LoginAsync: cancelled (window closed or user retry)");
            else
                DebugLog.WriteException("KickAuthService.LoginAsync (GetContextAsync)", ex);
            listener.Stop();
            return null;
        }

        var query = context.Request.QueryString;
        var code = query["code"];
        var returnedState = query["state"];
        var error = query["error"];

        await RespondToBrowserAsync(context, error is null && code != null);
        listener.Stop();

        if (error != null || code is null || returnedState != state)
        {
            DebugLog.Write($"KickAuthService.LoginAsync: login cancelled or invalid (error={error})");
            return null;
        }

        try
        {
            var body = new ExchangeCodeRequest { Code = code, CodeVerifier = verifier, RedirectUri = RedirectUri };
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{WorkerBaseUrl}/kick/exchange")
            {
                Content = JsonContent.Create(body, KickAuthJsonContext.Default.ExchangeCodeRequest),
            };
            request.Headers.Add("x-ttn-app-key", AppKeyProvider.Key);
            var response = await Http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                DebugLog.Write($"KickAuthService.LoginAsync: failed to redeem code, status {response.StatusCode}");
                return null;
            }

            var payload = await response.Content.ReadFromJsonAsync(KickAuthJsonContext.Default.TokenResponse);
            if (payload is null || string.IsNullOrEmpty(payload.AccessToken))
                return null;

            var (login, userId) = await GetUserInfoAsync(payload.AccessToken);
            if (login is null || userId is null)
                return null;

            return new AuthResult
            {
                AccessToken = payload.AccessToken,
                RefreshToken = payload.RefreshToken,
                ExpiresIn = payload.ExpiresIn,
                Login = login,
                UserId = userId,
            };
        }
        catch (Exception ex)
        {
            DebugLog.WriteException("KickAuthService.LoginAsync (token exchange)", ex);
            return null;
        }
    }

    /// <summary>Kick rotates the refresh token on every refresh, so the caller must store the one returned here.</summary>
    public static async Task<AuthResult?> RefreshAsync(string refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            return null;

        try
        {
            var body = new RefreshTokenRequest { RefreshToken = refreshToken };
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{WorkerBaseUrl}/kick/refresh")
            {
                Content = JsonContent.Create(body, KickAuthJsonContext.Default.RefreshTokenRequest),
            };
            request.Headers.Add("x-ttn-app-key", AppKeyProvider.Key);
            var response = await Http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                DebugLog.Write($"KickAuthService.RefreshAsync: refresh failed, status {response.StatusCode}");
                return null;
            }

            var payload = await response.Content.ReadFromJsonAsync(KickAuthJsonContext.Default.TokenResponse);
            if (payload is null || string.IsNullOrEmpty(payload.AccessToken))
                return null;

            return new AuthResult
            {
                AccessToken = payload.AccessToken,
                RefreshToken = payload.RefreshToken,
                ExpiresIn = payload.ExpiresIn,
            };
        }
        catch (Exception ex)
        {
            DebugLog.WriteException("KickAuthService.RefreshAsync", ex);
            return null;
        }
    }

    /// <summary>
    /// Asks Kick which scopes an access token carries (GET /public/v1/token/introspect). Returns null when that can't
    /// be determined, so callers don't log anyone out over a transient failure.
    /// </summary>
    public static async Task<IReadOnlyList<string>?> GetGrantedScopesAsync(string accessToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.kick.com/public/v1/token/introspect");
            request.Headers.Add("Authorization", $"Bearer {accessToken}");

            var response = await Http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                DebugLog.Write($"KickAuthService.GetGrantedScopesAsync: introspect failed, status {response.StatusCode}");
                return null;
            }

            var payload = await response.Content.ReadFromJsonAsync(KickAuthJsonContext.Default.IntrospectResponse);
            var data = payload?.Data;
            if (data is null || !data.Active)
                return null;

            return (data.Scope ?? "").Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
        }
        catch (Exception ex)
        {
            DebugLog.WriteException("KickAuthService.GetGrantedScopesAsync", ex);
            return null;
        }
    }

    /// <summary>The required scopes that are not in <paramref name="granted"/> (empty when the session is up to date).</summary>
    public static IReadOnlyList<string> FindMissingScopes(IEnumerable<string> granted)
    {
        var set = new HashSet<string>(granted, StringComparer.OrdinalIgnoreCase);
        return RequiredScopes.Where(s => !set.Contains(s)).ToList();
    }

    private static async Task<(string? Login, string? UserId)> GetUserInfoAsync(string accessToken)
    {
        try
        {
            // With no ids in the query, GET /users returns the user the token belongs to.
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.kick.com/public/v1/users");
            request.Headers.Add("Authorization", $"Bearer {accessToken}");

            var response = await Http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                DebugLog.Write($"KickAuthService.GetUserInfoAsync: status {response.StatusCode}");
                return (null, null);
            }

            var payload = await response.Content.ReadFromJsonAsync(KickAuthJsonContext.Default.UsersResponse);
            var user = payload?.Data?.FirstOrDefault();
            if (user is null || string.IsNullOrWhiteSpace(user.Name))
                return (null, null);
            return (user.Name, user.UserId.ToString());
        }
        catch (Exception ex)
        {
            DebugLog.WriteException("KickAuthService.GetUserInfoAsync", ex);
            return (null, null);
        }
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static async Task RespondToBrowserAsync(HttpListenerContext context, bool success)
    {
        try
        {
            var html = AuthBrowserPage.Build(success);
            var buffer = Encoding.UTF8.GetBytes(html);
            context.Response.ContentType = "text/html; charset=utf-8";
            context.Response.ContentLength64 = buffer.Length;
            await context.Response.OutputStream.WriteAsync(buffer);
            context.Response.OutputStream.Close();
        }
        catch (Exception ex)
        {
            DebugLog.WriteException("KickAuthService.RespondToBrowserAsync", ex);
        }
    }

    internal class TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = "";

        [JsonPropertyName("refresh_token")]
        public string RefreshToken { get; set; } = "";

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
    }

    internal class IntrospectResponse
    {
        [JsonPropertyName("data")]
        public IntrospectData? Data { get; set; }
    }

    internal class IntrospectData
    {
        [JsonPropertyName("active")]
        public bool Active { get; set; }

        [JsonPropertyName("scope")]
        public string? Scope { get; set; }
    }

    internal class UsersResponse
    {
        [JsonPropertyName("data")]
        public List<UserData>? Data { get; set; }
    }

    internal class UserData
    {
        [JsonPropertyName("user_id")]
        public long UserId { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = "";
    }

    internal class ExchangeCodeRequest
    {
        [JsonPropertyName("code")]
        public string Code { get; set; } = "";

        [JsonPropertyName("code_verifier")]
        public string CodeVerifier { get; set; } = "";

        [JsonPropertyName("redirect_uri")]
        public string RedirectUri { get; set; } = "";
    }

    internal class RefreshTokenRequest
    {
        [JsonPropertyName("refresh_token")]
        public string RefreshToken { get; set; } = "";
    }

    [JsonSerializable(typeof(TokenResponse))]
    [JsonSerializable(typeof(IntrospectResponse))]
    [JsonSerializable(typeof(UsersResponse))]
    [JsonSerializable(typeof(ExchangeCodeRequest))]
    [JsonSerializable(typeof(RefreshTokenRequest))]
    internal partial class KickAuthJsonContext : JsonSerializerContext { }
}
