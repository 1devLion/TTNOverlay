using System.Reflection;
using TTNOverlay.Services;

namespace TTNOverlay.Overlay;

/// <summary>
/// Loads and caches Kick's fixed global role badges (moderator/vip/og/founder/broadcaster/verified/
/// staff) and sub_gifter tiers, extracted by hand as WebP (no Kick-hosted image URL exists for these
/// See the remarks on KickChatClient). Shipped as embedded resources under Resources/KickBadges, decoded
/// once per key via the same WebP path LocalImageLoader/D2DBitmapLoader already use for local files.
/// </summary>
internal static class KickBadgeIconLoader
{
    /// <summary>
    /// Maps a Badge.LocalIcon key (set by KickChatClient) to its embedded resource file name.
    /// Keys match the raw "type" string Kick sends for role badges, and "sub_gifter_{tier}" for
    /// gifter tiers. Missing here (e.g. broadcaster, if no file was captured) simply won't resolve.
    /// DrawBadges already tolerates that the same way it tolerates any other unresolved badge.
    /// </summary>
    private static readonly Dictionary<string, string> ResourceNames = new()
    {
        ["moderator"] = "moderator_badge.webp",
        ["vip"] = "vip_badge.webp",
        ["og"] = "og_badge.webp",
        ["founder"] = "founder_badge.webp",
        ["verified"] = "verified_badge.webp",
        ["subscriber"] = "subscriber_badge.webp",
        ["sub_gifter_1"] = "sub_gifter_1_badge.webp",
        ["sub_gifter_5"] = "sub_gifter_5_badge.webp",
        ["sub_gifter_10"] = "sub_gifter_10_badge.webp",
        ["sub_gifter_50"] = "sub_gifter_50_badge.webp",
        ["sub_gifter_200"] = "sub_gifter_200_badge.webp",
    };

    private static readonly Dictionary<string, D2DBitmapLoader.DecodedImage?> _cache = new();

    /// <summary>True if a local WebP is bundled for this key (used by KickChatClient to decide
    /// whether to set Badge.LocalIcon at all).</summary>
    public static bool HasIcon(string key) => ResourceNames.ContainsKey(key);

    public static D2DBitmapLoader.DecodedImage? GetDecodedIcon(string key)
    {
        if (_cache.TryGetValue(key, out var cached))
            return cached;

        D2DBitmapLoader.DecodedImage? decoded = null;

        if (ResourceNames.TryGetValue(key, out var fileName))
        {
            string resourceName = $"TTNOverlay.KickBadges.{fileName}";
            try
            {
                using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
                if (stream is null)
                {
                    DebugLog.Write($"KickBadgeIconLoader: embedded resource not found ({resourceName})");
                }
                else
                {
                    using var ms = new MemoryStream();
                    stream.CopyTo(ms);
                    decoded = LocalImageLoader.TryDecodeStatic(ms.ToArray());
                    if (decoded is null)
                        DebugLog.Write($"KickBadgeIconLoader: decode failed for {resourceName}");
                }
            }
            catch (Exception ex)
            {
                DebugLog.WriteException($"KickBadgeIconLoader.GetDecodedIcon({key})", ex);
            }
        }

        _cache[key] = decoded;
        return decoded;
    }
}