using System.Net.Http.Json;
using System.Text.Json.Serialization;
using TTNOverlay.Services;

namespace TTNOverlay.Twitch;

/// <summary>
/// HelixClient partial: moderation endpoints (chatters, bans/timeouts, unban/untimeout, warnings).
/// </summary>
public partial class HelixClient
{
    public async Task<List<(string Id, string Login)>?> GetChattersAsync(
        string broadcasterId,
        string moderatorId,
        string userAccessToken
    )
    {
        var result = new List<(string, string)>();
        string? cursor = null;

        try
        {
            do
            {
                var url =
                    $"https://api.twitch.tv/helix/chat/chatters?broadcaster_id={Uri.EscapeDataString(broadcasterId)}"
                    + $"&moderator_id={Uri.EscapeDataString(moderatorId)}&first=1000"
                    + (cursor is null ? "" : $"&after={Uri.EscapeDataString(cursor)}");

                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("Client-Id", _clientId);
                request.Headers.Add("Authorization", $"Bearer {userAccessToken}");

                var response = await Http.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    DebugLog.Write($"Helix: failed to request chatters, status {response.StatusCode}");
                    return result.Count > 0 ? result : null;
                }

                var payload = await response.Content.ReadFromJsonAsync(
                    HelixJsonContext.Default.ChattersResponse
                );
                if (payload?.Data != null)
                    foreach (var c in payload.Data)
                        result.Add((c.UserId, c.UserLogin));

                cursor = string.IsNullOrEmpty(payload?.Pagination?.Cursor)
                    ? null
                    : payload!.Pagination!.Cursor;
            } while (cursor != null);

            return result;
        }
        catch (Exception ex)
        {
            DebugLog.WriteException("HelixClient.GetChattersAsync", ex);
            return result.Count > 0 ? result : null;
        }
    }

    public async Task<List<(
        string Id,
        string Login,
        DateTime? ExpiresAt,
        string Reason
    )>?> GetBannedUsersAsync(string broadcasterId, string userAccessToken)
    {
        var result = new List<(string, string, DateTime?, string)>();
        string? cursor = null;

        try
        {
            do
            {
                var url =
                    $"https://api.twitch.tv/helix/moderation/banned?broadcaster_id={Uri.EscapeDataString(broadcasterId)}"
                    + "&first=100"
                    + (cursor is null ? "" : $"&after={Uri.EscapeDataString(cursor)}");

                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("Client-Id", _clientId);
                request.Headers.Add("Authorization", $"Bearer {userAccessToken}");

                var response = await Http.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    DebugLog.Write(
                        $"Helix: failed to request banned users, status {response.StatusCode}"
                    );
                    return result.Count > 0 ? result : null;
                }

                var payload = await response.Content.ReadFromJsonAsync(
                    HelixJsonContext.Default.BannedUsersResponse
                );
                if (payload?.Data != null)
                    foreach (var b in payload.Data)
                        result.Add((b.UserId, b.UserLogin, b.ExpiresAt, b.Reason ?? ""));

                cursor = string.IsNullOrEmpty(payload?.Pagination?.Cursor)
                    ? null
                    : payload!.Pagination!.Cursor;
            } while (cursor != null);

            return result;
        }
        catch (Exception ex)
        {
            DebugLog.WriteException("HelixClient.GetBannedUsersAsync", ex);
            return result.Count > 0 ? result : null;
        }
    }

    public async Task<AutoModDecisionResult> ManageHeldAutoModMessageAsync(
        string moderatorId,
        string userAccessToken,
        string messageId,
        bool allow
    )
    {
        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                "https://api.twitch.tv/helix/moderation/automod/message"
            );
            request.Headers.Add("Client-Id", _clientId);
            request.Headers.Add("Authorization", $"Bearer {userAccessToken}");
            request.Content = JsonContent.Create(
                new ManageHeldAutoModRequest
                {
                    // Here "user_id" is the moderator doing the review, not the author of the message.
                    UserId = moderatorId,
                    MessageId = messageId,
                    Action = allow ? "ALLOW" : "DENY",
                },
                HelixJsonContext.Default.ManageHeldAutoModRequest
            );

            using var response = await Http.SendAsync(request);
            if (response.IsSuccessStatusCode)
                return AutoModDecisionResult.Ok;

            var body = await response.Content.ReadAsStringAsync();
            DebugLog.Write(
                $"Helix: {(allow ? "allowing" : "denying")} held AutoMod message {messageId} failed, status {response.StatusCode}: {body}"
            );

            return MapAutoModDecisionStatus(response.StatusCode);
        }
        catch (Exception ex)
        {
            DebugLog.WriteException("HelixClient.ManageHeldAutoModMessageAsync", ex);
            return AutoModDecisionResult.Failed;
        }
    }

    /// <summary>401 means the token lacks moderator:manage:automod; 400/404 mean the message isn't in the queue anymore.</summary>
    internal static AutoModDecisionResult MapAutoModDecisionStatus(System.Net.HttpStatusCode status) =>
        status switch
        {
            System.Net.HttpStatusCode.Unauthorized => AutoModDecisionResult.MissingPermission,
            System.Net.HttpStatusCode.BadRequest or System.Net.HttpStatusCode.NotFound =>
                AutoModDecisionResult.AlreadyResolved,
            _ => AutoModDecisionResult.Failed,
        };

    internal class ManageHeldAutoModRequest
    {
        [JsonPropertyName("user_id")]
        public string UserId { get; set; } = "";

        [JsonPropertyName("msg_id")]
        public string MessageId { get; set; } = "";

        [JsonPropertyName("action")]
        public string Action { get; set; } = "";
    }

    public async Task<bool> WarnUserAsync(
        string broadcasterId,
        string moderatorId,
        string userAccessToken,
        string targetUserId,
        string reason
    )
    {
        try
        {
            var url =
                $"https://api.twitch.tv/helix/moderation/warnings?broadcaster_id={Uri.EscapeDataString(broadcasterId)}"
                + $"&moderator_id={Uri.EscapeDataString(moderatorId)}";

            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("Client-Id", _clientId);
            request.Headers.Add("Authorization", $"Bearer {userAccessToken}");
            request.Content = JsonContent.Create(
                new WarnUserRequest
                {
                    Data = new WarnUserData { UserId = targetUserId, Reason = reason },
                },
                HelixJsonContext.Default.WarnUserRequest
            );

            var response = await Http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                DebugLog.Write(
                    $"Helix: failure to warn {targetUserId}, status {response.StatusCode}: {body}"
                );
            }

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            DebugLog.WriteException("HelixClient.WarnUserAsync", ex);
            return false;
        }
    }

    public async Task<bool> BanUserAsync(
        string broadcasterId,
        string moderatorId,
        string userAccessToken,
        string targetUserId,
        int? durationSeconds = null,
        string? reason = null
    )
    {
        try
        {
            var url =
                $"https://api.twitch.tv/helix/moderation/bans?broadcaster_id={Uri.EscapeDataString(broadcasterId)}"
                + $"&moderator_id={Uri.EscapeDataString(moderatorId)}";

            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("Client-Id", _clientId);
            request.Headers.Add("Authorization", $"Bearer {userAccessToken}");
            request.Content = JsonContent.Create(
                new BanUserRequest
                {
                    Data = new BanUserData
                    {
                        UserId = targetUserId,
                        Duration = durationSeconds,
                        Reason = reason,
                    },
                },
                HelixJsonContext.Default.BanUserRequest
            );

            var response = await Http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                DebugLog.Write(
                    $"Helix: failure to ban/mute {targetUserId}, status {response.StatusCode}: {body}"
                );
            }

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            DebugLog.WriteException("HelixClient.BanUserAsync", ex);
            return false;
        }
    }

    public async Task<bool> UnbanUserAsync(
        string broadcasterId,
        string moderatorId,
        string userAccessToken,
        string targetUserId
    )
    {
        try
        {
            var url =
                $"https://api.twitch.tv/helix/moderation/bans?broadcaster_id={Uri.EscapeDataString(broadcasterId)}"
                + $"&moderator_id={Uri.EscapeDataString(moderatorId)}"
                + $"&user_id={Uri.EscapeDataString(targetUserId)}";

            using var request = new HttpRequestMessage(HttpMethod.Delete, url);
            request.Headers.Add("Client-Id", _clientId);
            request.Headers.Add("Authorization", $"Bearer {userAccessToken}");

            var response = await Http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                DebugLog.Write(
                    $"Helix: unbanning failed {targetUserId}, status {response.StatusCode}"
                );

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            DebugLog.WriteException("HelixClient.UnbanUserAsync", ex);
            return false;
        }
    }

    public async Task<ModerationDeleteResult> DeleteChatMessagesAsync(
        string broadcasterId,
        string moderatorId,
        string userAccessToken,
        string? messageId = null
    )
    {
        try
        {
            // Without message_id, Twitch removes every message in the chat room.
            var url =
                $"https://api.twitch.tv/helix/moderation/chat?broadcaster_id={Uri.EscapeDataString(broadcasterId)}"
                + $"&moderator_id={Uri.EscapeDataString(moderatorId)}"
                + (messageId is null ? "" : $"&message_id={Uri.EscapeDataString(messageId)}");

            using var request = new HttpRequestMessage(HttpMethod.Delete, url);
            request.Headers.Add("Client-Id", _clientId);
            request.Headers.Add("Authorization", $"Bearer {userAccessToken}");

            using var response = await Http.SendAsync(request);
            if (response.IsSuccessStatusCode)
                return ModerationDeleteResult.Ok;

            var body = await response.Content.ReadAsStringAsync();
            DebugLog.Write(
                $"Helix: deleting chat message(s) failed (message_id={messageId ?? "<all>"}), status {response.StatusCode}: {body}"
            );

            return MapDeleteStatus(response.StatusCode, body);
        }
        catch (Exception ex)
        {
            DebugLog.WriteException("HelixClient.DeleteChatMessagesAsync", ex);
            return ModerationDeleteResult.Failed;
        }
    }

    /// <summary>
    /// Maps a Delete Chat Messages failure to something the UI can act on. 401 normally means the token lacks the
    /// moderator:manage:chat_messages scope, but Twitch also answers 401 "incorrect user authorization" when the
    /// target message belongs to someone of equal or higher rank (broadcaster, another moderator), which is a
    /// per-message refusal, not a login problem. 403 means the account isn't a moderator of that channel.
    /// </summary>
    internal static ModerationDeleteResult MapDeleteStatus(System.Net.HttpStatusCode status, string body) =>
        status switch
        {
            System.Net.HttpStatusCode.Unauthorized
                when body.Contains("incorrect user authorization", StringComparison.OrdinalIgnoreCase) =>
                ModerationDeleteResult.Rejected,
            System.Net.HttpStatusCode.Unauthorized => ModerationDeleteResult.MissingPermission,
            System.Net.HttpStatusCode.BadRequest or System.Net.HttpStatusCode.NotFound =>
                ModerationDeleteResult.Rejected,
            _ => ModerationDeleteResult.Failed,
        };

    internal class WarnUserRequest
    {
        [JsonPropertyName("data")]
        public WarnUserData Data { get; set; } = new();
    }

    internal class WarnUserData
    {
        [JsonPropertyName("user_id")]
        public string UserId { get; set; } = "";

        [JsonPropertyName("reason")]
        public string Reason { get; set; } = "";
    }

    internal class BanUserRequest
    {
        [JsonPropertyName("data")]
        public BanUserData Data { get; set; } = new();
    }

    internal class BanUserData
    {
        [JsonPropertyName("user_id")]
        public string UserId { get; set; } = "";

        [JsonPropertyName("duration")]
        public int? Duration { get; set; }

        [JsonPropertyName("reason")]
        public string? Reason { get; set; }
    }

    internal class ChattersResponse
    {
        [JsonPropertyName("data")]
        public List<ChatterData>? Data { get; set; }

        [JsonPropertyName("pagination")]
        public PaginationData? Pagination { get; set; }
    }

    internal class ChatterData
    {
        [JsonPropertyName("user_id")]
        public string UserId { get; set; } = "";

        [JsonPropertyName("user_login")]
        public string UserLogin { get; set; } = "";
    }

    internal class BannedUsersResponse
    {
        [JsonPropertyName("data")]
        public List<BannedUserData>? Data { get; set; }

        [JsonPropertyName("pagination")]
        public PaginationData? Pagination { get; set; }
    }

    internal class BannedUserData
    {
        [JsonPropertyName("user_id")]
        public string UserId { get; set; } = "";

        [JsonPropertyName("user_login")]
        public string UserLogin { get; set; } = "";

        [JsonPropertyName("expires_at")]
        public string? ExpiresAtRaw { get; set; }

        [JsonPropertyName("reason")]
        public string? Reason { get; set; }

        public DateTime? ExpiresAt =>
            !string.IsNullOrEmpty(ExpiresAtRaw) && DateTime.TryParse(ExpiresAtRaw, out var dt)
                ? dt.ToUniversalTime()
                : null;
    }

    internal class PaginationData
    {
        [JsonPropertyName("cursor")]
        public string? Cursor { get; set; }
    }

}

