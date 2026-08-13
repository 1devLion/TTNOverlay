using System.Text.Json.Serialization;

namespace TTNOverlay.Twitch;

/// <summary>
/// HelixClient partial: resolves a Twitch login name to a user ID.
/// </summary>
public partial class HelixClient
{
    internal class UsersResponse
    {
        [JsonPropertyName("data")]
        public List<UserData>? Data { get; set; }
    }

    internal class UserData
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";
    }

    public async Task<string?> GetUserIdByLoginAsync(string login, string userAccessToken) =>
        await GetBroadcasterIdAsync(login, userAccessToken);
}
