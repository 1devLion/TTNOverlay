namespace TTNOverlay.Services;

/// <summary>
/// Normalizes possibly-relative or protocol-relative image URLs (e.g. from Streamlabs) into absolute HTTPS URLs.
/// </summary>
public static class ImageUrlHelper
{
    private const string StreamlabsBaseUrl = "https://streamlabs.com";

    public static string? Normalize(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;
        if (
            Uri.TryCreate(url, UriKind.Absolute, out var absolute)
            && (absolute.Scheme == Uri.UriSchemeHttp || absolute.Scheme == Uri.UriSchemeHttps)
        )
            return url;
        var combined = url.StartsWith('/') ? StreamlabsBaseUrl + url : $"{StreamlabsBaseUrl}/{url}";
        return combined;
    }
}

