using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using TTNOverlay.Generated;
using TTNOverlay.Services;

namespace TTNOverlay.Twitch;

/// <summary>
/// Handles Twitch OAuth: opens the browser login flow and exchanges/validates access tokens.
/// </summary>
public static partial class TwitchAuthService
{
    private const string RedirectUri = "http://localhost:3939/";
    public const string ClientId = "moip38rvs0bu0lw6sigyuz3we231k1";
    private const string WorkerBaseUrl = "https://ttnoverlay-auth.ttnoverlay.workers.dev";
    private const string Scopes =
        "moderator:read:chatters moderator:manage:banned_users moderator:read:banned_users "
        + "moderator:manage:warnings moderator:read:chat_settings moderator:manage:chat_settings";
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
        var state = Guid.NewGuid().ToString("N");

        using var listener = new HttpListener();
        listener.Prefixes.Add(RedirectUri);

        try
        {
            listener.Start();
        }
        catch (Exception ex)
        {
            DebugLog.WriteException("TwitchAuthService.LoginAsync (listener.Start)", ex);
            return null;
        }

        var authorizeUrl =
            "https://id.twitch.tv/oauth2/authorize"
            + $"?client_id={Uri.EscapeDataString(ClientId)}"
            + $"&redirect_uri={Uri.EscapeDataString(RedirectUri)}"
            + "&response_type=code"
            + $"&scope={Uri.EscapeDataString(Scopes)}"
            + $"&state={state}";

        try
        {
            Process.Start(new ProcessStartInfo(authorizeUrl) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            DebugLog.WriteException("TwitchAuthService.LoginAsync (Process.Start)", ex);
            listener.Stop();
            return null;
        }

        HttpListenerContext context;
        try
        {

            using var registration = cancellationToken.Register(() =>
            {
                try { listener.Stop(); } catch {  }
            });

            var getContextTask = listener.GetContextAsync();
            var timeoutTask = Task.Delay(TimeSpan.FromMinutes(3), CancellationToken.None);
            var completed = await Task.WhenAny(getContextTask, timeoutTask);
            if (completed == timeoutTask)
            {
                DebugLog.Write(
                    "TwitchAuthService.LoginAsync: Timeout waiting for browser redirect"
                );
                listener.Stop();
                return null;
            }

            context = await getContextTask;
        }
        catch (Exception ex)
        {
            if (cancellationToken.IsCancellationRequested)
                DebugLog.Write("TwitchAuthService.LoginAsync: cancelled (window closed or user retry)");
            else
                DebugLog.WriteException("TwitchAuthService.LoginAsync (GetContextAsync)", ex);
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
            DebugLog.Write(
                $"TwitchAuthService.LoginAsync: login cancelled or invalid (error={error})"
            );
            return null;
        }

        try
        {
            var exchangeBody = new ExchangeCodeRequest { Code = code };
            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"{WorkerBaseUrl}/twitch/exchange"
            )
            {
                Content = JsonContent.Create(exchangeBody, TwitchAuthJsonContext.Default.ExchangeCodeRequest),
            };
            request.Headers.Add("x-ttn-app-key", AppKeyProvider.Key);
            var response = await Http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                DebugLog.Write(
                    $"TwitchAuthService.LoginAsync: failed to redeem code, status {response.StatusCode}"
                );
                return null;
            }

            var payload = await response.Content.ReadFromJsonAsync(
                TwitchAuthJsonContext.Default.TokenResponse
            );
            if (payload is null)
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
            DebugLog.WriteException("TwitchAuthService.LoginAsync (token exchange)", ex);
            return null;
        }
    }

    public static async Task<AuthResult?> RefreshAsync(string refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            return null;

        try
        {
            var refreshBody = new RefreshTokenRequest { RefreshToken = refreshToken };
            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"{WorkerBaseUrl}/twitch/refresh"
            )
            {
                Content = JsonContent.Create(refreshBody, TwitchAuthJsonContext.Default.RefreshTokenRequest),
            };
            request.Headers.Add("x-ttn-app-key", AppKeyProvider.Key);
            var response = await Http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                DebugLog.Write(
                    $"TwitchAuthService.RefreshAsync: Refreshing failure, status {response.StatusCode}"
                );
                return null;
            }

            var payload = await response.Content.ReadFromJsonAsync(
                TwitchAuthJsonContext.Default.TokenResponse
            );
            if (payload is null)
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
            DebugLog.WriteException("TwitchAuthService.RefreshAsync", ex);
            return null;
        }
    }

    private static async Task<(string? Login, string? UserId)> GetUserInfoAsync(string accessToken)
    {
        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                "https://api.twitch.tv/helix/users"
            );
            request.Headers.Add("Client-Id", ClientId);
            request.Headers.Add("Authorization", $"Bearer {accessToken}");

            var response = await Http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                return (null, null);

            var payload = await response.Content.ReadFromJsonAsync(
                TwitchAuthJsonContext.Default.UsersResponse
            );
            var user = payload?.Data?.FirstOrDefault();
            return (user?.Login, user?.Id);
        }
        catch (Exception ex)
        {
            DebugLog.WriteException("TwitchAuthService.GetUserInfoAsync", ex);
            return (null, null);
        }
    }

    private static async Task RespondToBrowserAsync(HttpListenerContext context, bool success)
    {
        try
        {
            var html = success
                ? "<html><body style='font-family:sans-serif;text-align:center;margin-top:80px'><h2>Listo, ya pod\u00e9s cerrar esta pesta\u00f1a.</h2></body></html>"
                : "<html><body style='font-family:sans-serif;text-align:center;margin-top:80px'><h2>No se pudo iniciar sesi\u00f3n. Pod\u00e9s cerrar esta pesta\u00f1a.</h2></body></html>";
            var buffer = Encoding.UTF8.GetBytes(html);
            context.Response.ContentType = "text/html; charset=utf-8";
            context.Response.ContentLength64 = buffer.Length;
            await context.Response.OutputStream.WriteAsync(buffer);
            context.Response.OutputStream.Close();
        }
        catch (Exception ex)
        {
            DebugLog.WriteException("TwitchAuthService.RespondToBrowserAsync", ex);
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

    internal class UsersResponse
    {
        [JsonPropertyName("data")]
        public List<UserData>? Data { get; set; }
    }

    internal class UserData
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("login")]
        public string Login { get; set; } = "";
    }

    internal class ExchangeCodeRequest
    {
        [JsonPropertyName("code")]
        public string Code { get; set; } = "";
    }

    internal class RefreshTokenRequest
    {
        [JsonPropertyName("refresh_token")]
        public string RefreshToken { get; set; } = "";
    }

    [JsonSerializable(typeof(TokenResponse))]
    [JsonSerializable(typeof(UsersResponse))]
    [JsonSerializable(typeof(ExchangeCodeRequest))]
    [JsonSerializable(typeof(RefreshTokenRequest))]
    internal partial class TwitchAuthJsonContext : JsonSerializerContext { }
}