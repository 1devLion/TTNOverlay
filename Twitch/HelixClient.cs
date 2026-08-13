using System.Net.Http;
using TTNOverlay.Services;

namespace TTNOverlay.Twitch;

/// <summary>
/// Twitch Helix API client core: shared HTTP client and client ID. All calls run as the logged-in
/// user (see TwitchAuthService/ModerationService) instead of an app-level access token.
/// </summary>
public partial class HelixClient : IHelixClient
{
    private static readonly HttpClient Http = SharedHttpClient.Instance;

    private readonly string _clientId;

    public HelixClient(string clientId)
    {
        _clientId = clientId;
    }

    public bool HasCredentials => !string.IsNullOrWhiteSpace(_clientId);
}
