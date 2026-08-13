using System.Text.Json.Serialization;

namespace TTNOverlay.Twitch;

/// <summary>
/// System.Text.Json source-generation context for the HelixClient's request/response DTOs.
/// </summary>
public partial class HelixClient
{
    [JsonSerializable(typeof(StreamsResponse))]
    [JsonSerializable(typeof(ChatSettingsResponse))]
    [JsonSerializable(typeof(ChatSettingsUpdateRequest))]
    [JsonSerializable(typeof(BadgeSetsResponse))]
    [JsonSerializable(typeof(UsersResponse))]
    [JsonSerializable(typeof(ChattersResponse))]
    [JsonSerializable(typeof(BannedUsersResponse))]
    [JsonSerializable(typeof(WarnUserRequest))]
    [JsonSerializable(typeof(BanUserRequest))]
    internal partial class HelixJsonContext : JsonSerializerContext { }
}

