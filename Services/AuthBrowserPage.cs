using System.Net;

namespace TTNOverlay.Services;

/// <summary>
/// The page shown in the browser after the OAuth redirect (Twitch and Kick), in the app's current language. Plain black
/// or white following the system theme, like the app itself.
/// </summary>
internal static class AuthBrowserPage
{
    private const string CheckIcon =
        "<svg class='icon' viewBox='0 0 64 64' fill='none' stroke='currentColor' stroke-width='3' stroke-linecap='square'>"
        + "<circle cx='32' cy='32' r='28'/><path d='M19 33l9 9 17-19'/></svg>";

    private const string CrossIcon =
        "<svg class='icon' viewBox='0 0 64 64' fill='none' stroke='currentColor' stroke-width='3' stroke-linecap='square'>"
        + "<circle cx='32' cy='32' r='28'/><path d='M22 22l20 20M42 22L22 42'/></svg>";

    public static string Build(bool success)
    {
        // Success: a title and the "you can close this tab" line. Failure: its message already says it all.
        string title = WebUtility.HtmlEncode(LocalizationService.T(success ? "Auth_BrowserSuccessTitle" : "Auth_BrowserFailed"));
        string detail = success ? $"<p>{WebUtility.HtmlEncode(LocalizationService.T("Auth_BrowserSuccess"))}</p>" : "";

        return "<!DOCTYPE html><html><head><meta charset='utf-8'>"
            + "<meta name='viewport' content='width=device-width,initial-scale=1'>"
            + "<title>TTNOverlay</title>"
            + "<style>"
            + ":root{color-scheme:light dark;--bg:#ffffff;--card:#ffffff;--fg:#111111;--sub:#595959;--line:#d0d0d0}"
            + "@media (prefers-color-scheme:dark){:root{--bg:#141414;--card:#1c1c1c;--fg:#f2f2f2;--sub:#a3a3a3;--line:#3a3a3a}}"
            + "html,body{height:100%;margin:0}"
            + "body{display:flex;align-items:center;justify-content:center;background:var(--bg);color:var(--fg);"
            + "font-family:'Segoe UI',system-ui,-apple-system,sans-serif}"
            + "main{box-sizing:border-box;width:min(92vw,420px);padding:40px 32px 44px;text-align:center;"
            + "background:var(--card);border:1px solid var(--line)}"
            + ".brand{font-size:16px;text-transform:uppercase;color:var(--sub);margin-bottom:28px;font-weight:bold}"
            + ".icon{display:block;width:64px;height:64px;margin:0 auto 24px}"
            + "h1{font-size:22px;font-weight:600;line-height:1.3;margin:0 0 10px}"
            + "p{margin:0;font-size:15px;line-height:1.5;color:var(--sub)}"
            + "</style></head><body><main>"
            + "<div class='brand'>TTNOverlay</div>"
            + (success ? CheckIcon : CrossIcon)
            + $"<h1>{title}</h1>{detail}"
            + "</main></body></html>";
    }
}
