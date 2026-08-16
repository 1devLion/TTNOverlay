using System.Net.Http.Json;
using System.Text.Json.Serialization;
using TTNOverlay.Services;

namespace TTNOverlay.Twitch;

/// <summary>
/// HelixClient partial: current viewer count lookup for a channel.
/// </summary>
public partial class HelixClient
{
    public async Task<int?> GetViewerCountAsync(string channelLogin, string userAccessToken)
    {
        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"https://api.twitch.tv/helix/streams?user_login={Uri.EscapeDataString(channelLogin)}"
            );
            request.Headers.Add("Client-Id", _clientId);
            request.Headers.Add("Authorization", $"Bearer {userAccessToken}");

            var response = await Http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                DebugLog.Write($"Helix: failed to request streams, status {response.StatusCode}");
                return null;
            }

            var payload = await response.Content.ReadFromJsonAsync(
                HelixJsonContext.Default.StreamsResponse
            );
            var stream = payload?.Data?.FirstOrDefault();
            return stream?.ViewerCount;
        }
        catch (Exception ex)
        {
            DebugLog.WriteException("HelixClient.GetViewerCountAsync", ex);
            return null;
        }
    }

    internal class StreamsResponse
    {
        [JsonPropertyName("data")]
        public List<StreamData>? Data { get; set; }
    }

    internal class StreamData
    {
        [JsonPropertyName("viewer_count")]
        public int ViewerCount { get; set; }
    }
}
