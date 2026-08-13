using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using TTNOverlay.Services;

namespace TTNOverlay.Twitch;

/// <summary>
/// HelixClient partial: reads and updates a channel's chat settings (slow mode, follower-only, etc.).
/// </summary>
public partial class HelixClient
{
    public class ChatSettings
    {
        public bool EmoteMode { get; set; }
        public bool FollowerMode { get; set; }
        public int? FollowerModeDurationMinutes { get; set; }
        public bool SlowMode { get; set; }
        public int? SlowModeWaitSeconds { get; set; }
        public bool SubscriberMode { get; set; }
        public bool UniqueChatMode { get; set; }
    }

    public async Task<ChatSettings?> GetChatSettingsAsync(
        string broadcasterId,
        string moderatorId,
        string userAccessToken
    )
    {
        try
        {
            var url =
                $"https://api.twitch.tv/helix/chat/settings?broadcaster_id={Uri.EscapeDataString(broadcasterId)}"
                + $"&moderator_id={Uri.EscapeDataString(moderatorId)}";

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("Client-Id", _clientId);
            request.Headers.Add("Authorization", $"Bearer {userAccessToken}");

            var response = await Http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                DebugLog.Write(
                    $"Helix: failed to request chat settings, status {response.StatusCode}"
                );
                return null;
            }

            var payload = await response.Content.ReadFromJsonAsync(
                HelixJsonContext.Default.ChatSettingsResponse
            );
            var data = payload?.Data?.FirstOrDefault();
            if (data is null)
                return null;

            return new ChatSettings
            {
                EmoteMode = data.EmoteMode,
                FollowerMode = data.FollowerMode,
                FollowerModeDurationMinutes = data.FollowerModeDuration,
                SlowMode = data.SlowMode,
                SlowModeWaitSeconds = data.SlowModeWaitTime,
                SubscriberMode = data.SubscriberMode,
                UniqueChatMode = data.UniqueChatMode,
            };
        }
        catch (Exception ex)
        {
            DebugLog.WriteException("HelixClient.GetChatSettingsAsync", ex);
            return null;
        }
    }

    public async Task<bool> UpdateChatSettingsAsync(
        string broadcasterId,
        string moderatorId,
        string userAccessToken,
        ChatSettings settings
    )
    {
        try
        {
            var url =
                $"https://api.twitch.tv/helix/chat/settings?broadcaster_id={Uri.EscapeDataString(broadcasterId)}"
                + $"&moderator_id={Uri.EscapeDataString(moderatorId)}";

            using var request = new HttpRequestMessage(HttpMethod.Patch, url);
            request.Headers.Add("Client-Id", _clientId);
            request.Headers.Add("Authorization", $"Bearer {userAccessToken}");
            request.Content = JsonContent.Create(
                new ChatSettingsUpdateRequest
                {
                    EmoteMode = settings.EmoteMode,
                    FollowerMode = settings.FollowerMode,
                    FollowerModeDuration = settings.FollowerModeDurationMinutes ?? 0,
                    SlowMode = settings.SlowMode,
                    SlowModeWaitTime = settings.SlowModeWaitSeconds ?? 30,
                    SubscriberMode = settings.SubscriberMode,
                    UniqueChatMode = settings.UniqueChatMode,
                },
                HelixJsonContext.Default.ChatSettingsUpdateRequest
            );

            var response = await Http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                DebugLog.Write(
                    $"Helix: failed to update chat settings, status {response.StatusCode}: {body}"
                );
            }

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            DebugLog.WriteException("HelixClient.UpdateChatSettingsAsync", ex);
            return false;
        }
    }

    internal class ChatSettingsUpdateRequest
    {
        [JsonPropertyName("emote_mode")]
        public bool EmoteMode { get; set; }

        [JsonPropertyName("follower_mode")]
        public bool FollowerMode { get; set; }

        [JsonPropertyName("follower_mode_duration")]
        public int FollowerModeDuration { get; set; }

        [JsonPropertyName("slow_mode")]
        public bool SlowMode { get; set; }

        [JsonPropertyName("slow_mode_wait_time")]
        public int SlowModeWaitTime { get; set; }

        [JsonPropertyName("subscriber_mode")]
        public bool SubscriberMode { get; set; }

        [JsonPropertyName("unique_chat_mode")]
        public bool UniqueChatMode { get; set; }
    }

    internal class ChatSettingsResponse
    {
        [JsonPropertyName("data")]
        public List<ChatSettingsData>? Data { get; set; }
    }

    internal class ChatSettingsData
    {
        [JsonPropertyName("emote_mode")]
        public bool EmoteMode { get; set; }

        [JsonPropertyName("follower_mode")]
        public bool FollowerMode { get; set; }

        [JsonPropertyName("follower_mode_duration")]
        public int? FollowerModeDuration { get; set; }

        [JsonPropertyName("slow_mode")]
        public bool SlowMode { get; set; }

        [JsonPropertyName("slow_mode_wait_time")]
        public int? SlowModeWaitTime { get; set; }

        [JsonPropertyName("subscriber_mode")]
        public bool SubscriberMode { get; set; }

        [JsonPropertyName("unique_chat_mode")]
        public bool UniqueChatMode { get; set; }
    }
}

