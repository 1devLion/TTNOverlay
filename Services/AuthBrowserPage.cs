using System.Net;

namespace TTNOverlay.Services;

/// <summary>
/// The small page shown in the browser after the OAuth redirect (Twitch and Kick), in the app's current language.
/// </summary>
internal static class AuthBrowserPage
{
    public static string Build(bool success)
    {
        string message = WebUtility.HtmlEncode(LocalizationService.T(success ? "Auth_BrowserSuccess" : "Auth_BrowserFailed"));
        return "<html><head><meta charset='utf-8'></head>"
            + "<body style='font-family:sans-serif;text-align:center;margin-top:80px'>"
            + $"<h2>{message}</h2></body></html>";
    }
}
