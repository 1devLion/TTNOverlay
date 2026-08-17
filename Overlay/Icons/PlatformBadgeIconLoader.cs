using TTNOverlay.Services;

namespace TTNOverlay.Overlay;

/// <summary>
/// Loads and caches the small platform-origin badges (Twitch/Kick logos) shown on each chat message
/// when Multichat has both sources active, so a viewer can be identified at a glance. Not to be
/// confused with KickBadgeIconLoader, which resolves Kick's role badges (moderator/vip/og/etc.) sent
/// per-user by Kick itself; these two are unrelated icon sets that happen to share the embedded-WebP
/// loading path (see GetOrCreateLocalBadgeBitmap in ImageCache.cs, which dispatches between them).
/// </summary>
internal static class PlatformBadgeIconLoader
{
    /// <summary>
    /// Keys match Badge.LocalIcon as set by ChatRenderWindow.Feed.cs (PlatformBadge helper):
    /// "platform/twitch", "platform/kick", "platform/youtube". youtube.webp isn't bundled as an
    /// embedded resource yet -- until it is, GetDecodedIcon("platform/youtube") just logs a "not
    /// found" and returns null (same as any other missing resource), so this entry is safe to keep
    /// ahead of the asset landing.
    /// </summary>
    private static readonly Dictionary<string, string> ResourceNames = new()
    {
        ["platform/twitch"] = "twitch.webp",
        ["platform/kick"] = "kick.webp",
        ["platform/youtube"] = "youtube.webp",
    };

    private static readonly Dictionary<string, D2DBitmapLoader.DecodedImage?> _cache = new();

    /// <summary>True if this key belongs to the platform-badge set (used by ImageCache to route
    /// between this loader and KickBadgeIconLoader).</summary>
    public static bool HasIcon(string key) => ResourceNames.ContainsKey(key);

    public static D2DBitmapLoader.DecodedImage? GetDecodedIcon(string key)
    {
        if (_cache.TryGetValue(key, out var cached))
            return cached;

        D2DBitmapLoader.DecodedImage? decoded = null;

        if (ResourceNames.TryGetValue(key, out var fileName))
        {
            string resourceName = $"TTNOverlay.PlatformBadges.{fileName}";
            try
            {
                using var stream = System
                    .Reflection.Assembly.GetExecutingAssembly()
                    .GetManifestResourceStream(resourceName);
                if (stream is null)
                {
                    DebugLog.Write($"PlatformBadgeIconLoader: embedded resource not found ({resourceName})");
                }
                else
                {
                    using var ms = new MemoryStream();
                    stream.CopyTo(ms);
                    decoded = LocalImageLoader.TryDecodeStatic(ms.ToArray());
                    if (decoded is null)
                        DebugLog.Write($"PlatformBadgeIconLoader: decode failed for {resourceName}");
                }
            }
            catch (Exception ex)
            {
                DebugLog.WriteException($"PlatformBadgeIconLoader.GetDecodedIcon({key})", ex);
            }
        }

        _cache[key] = decoded;
        return decoded;
    }
}