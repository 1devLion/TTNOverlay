namespace TTNOverlay.Twitch;

/// <summary>
/// Builds Twitch CDN URLs for static and animated emote images at a given size.
/// </summary>
public static class EmoteUrlProvider
{
    public static string GetUrl(string emoteId, string size = "2.0", string theme = "dark") =>
        $"https://static-cdn.jtvnw.net/emoticons/v2/{emoteId}/default/{theme}/{size}";

    public static string GetAnimatedUrl(string emoteId, string size = "2.0", string theme = "dark") =>
        $"https://static-cdn.jtvnw.net/emoticons/v2/{emoteId}/animated/{theme}/{size}";

    public static string PickSizeBucket(int targetPx) =>
        targetPx switch
        {
            <= 28 => "1.0",
            <= 56 => "2.0",
            _ => "3.0",
        };
}
