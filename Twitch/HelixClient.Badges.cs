using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using TTNOverlay.Services;

namespace TTNOverlay.Twitch;

/// <summary>
/// HelixClient partial: fetches global and channel chat badge sets.
/// </summary>
public partial class HelixClient
{
    public async Task<Dictionary<string, string>?> GetBadgeMapAsync(string channelLogin, string userAccessToken)
    {
        try
        {
            var map = new Dictionary<string, string>();

            var global = await FetchBadgeSetsAsync(
                "https://api.twitch.tv/helix/chat/badges/global",
                userAccessToken
            );
            if (global != null)
                foreach (var kv in global)
                    map[kv.Key] = kv.Value;

            var broadcasterId = await GetBroadcasterIdAsync(channelLogin, userAccessToken);
            if (broadcasterId != null)
            {
                var channel = await FetchBadgeSetsAsync(
                    $"https://api.twitch.tv/helix/chat/badges?broadcaster_id={broadcasterId}",
                    userAccessToken
                );

                if (channel != null)
                    foreach (var kv in channel)
                        map[kv.Key] = kv.Value;
            }

            return map;
        }
        catch (Exception ex)
        {
            DebugLog.WriteException("HelixClient.GetBadgeMapAsync", ex);
            return null;
        }
    }

    private async Task<Dictionary<string, string>?> FetchBadgeSetsAsync(string url, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("Client-Id", _clientId);
        request.Headers.Add("Authorization", $"Bearer {token}");

        var response = await Http.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            DebugLog.Write($"Helix: fallo al pedir badges ({url}), status {response.StatusCode}");
            return null;
        }

        var payload = await response.Content.ReadFromJsonAsync(
            HelixJsonContext.Default.BadgeSetsResponse
        );
        if (payload?.Data is null)
            return null;

        var map = new Dictionary<string, string>();
        foreach (var set in payload.Data)
        foreach (var version in set.Versions)
            if (!string.IsNullOrEmpty(version.ImageUrl2x))
                map[$"{set.SetId}/{version.Id}"] = version.ImageUrl2x;

        return map;
    }

    private async Task<string?> GetBroadcasterIdAsync(string login, string token)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"https://api.twitch.tv/helix/users?login={Uri.EscapeDataString(login)}"
        );
        request.Headers.Add("Client-Id", _clientId);
        request.Headers.Add("Authorization", $"Bearer {token}");

        var response = await Http.SendAsync(request);
        if (!response.IsSuccessStatusCode)
            return null;

        var payload = await response.Content.ReadFromJsonAsync(
            HelixJsonContext.Default.UsersResponse
        );
        return payload?.Data?.FirstOrDefault()?.Id;
    }

    internal class BadgeSetsResponse
    {
        [JsonPropertyName("data")]
        public List<BadgeSetData>? Data { get; set; }
    }

    internal class BadgeSetData
    {
        [JsonPropertyName("set_id")]
        public string SetId { get; set; } = "";

        [JsonPropertyName("versions")]
        public List<BadgeVersionData> Versions { get; set; } = new();
    }

    internal class BadgeVersionData
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("image_url_2x")]
        public string ImageUrl2x { get; set; } = "";
    }
}
