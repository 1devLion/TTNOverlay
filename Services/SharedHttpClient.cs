using System.Net.Http;

namespace TTNOverlay.Services;

/// <summary>
/// Process-wide shared HttpClient instance to avoid socket exhaustion from creating one per request.
/// </summary>
public static class SharedHttpClient
{
    public static readonly HttpClient Instance = new();
}
