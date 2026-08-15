using System;
using System.Drawing;
using System.Reflection;
using TTNOverlay.Services;

namespace TTNOverlay.Overlay;

/// <summary>
/// Loads and caches the Twitch glyph icon (White and Dark variants) used on the
/// "Log in with Twitch" / "Log out" buttons.
/// </summary>
internal static class TwitchIconLoader
{
    public enum Variant
    {
        /// <summary>The original white glyph.</summary>
        White,
        /// <summary>Dark variant of the glyph.</summary>
        Dark,
    }

    private const int RequestedSize = 32;

    private static readonly D2DBitmapLoader.DecodedImage?[] _cached = new D2DBitmapLoader.DecodedImage?[2];
    private static readonly bool[] _loadAttempted = new bool[2];

    private static string ResourceNameFor(Variant variant) => variant switch
    {
        Variant.White => "TTNOverlay.Resources.twitch_white.ico",
        Variant.Dark => "TTNOverlay.Resources.twitch_dark.ico",
        _ => throw new ArgumentOutOfRangeException(nameof(variant)),
    };

    /// <summary>Returns the White or Dark glyph variant for the given theme.</summary>
    public static D2DBitmapLoader.DecodedImage? GetDecodedIcon(bool isDarkTheme) =>
        GetDecodedIcon(isDarkTheme ? Variant.White : Variant.Dark);

    public static D2DBitmapLoader.DecodedImage? GetDecodedIcon(Variant variant)
    {
        int i = (int)variant;
        if (_cached[i] is not null || _loadAttempted[i])
            return _cached[i];
        _loadAttempted[i] = true;

        string resourceName = ResourceNameFor(variant);
        try
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
            if (stream is null)
            {
                DebugLog.Write($"TwitchIconLoader: embedded resource not found ({resourceName})");
                return null;
            }

            using var icon = new Icon(stream, RequestedSize, RequestedSize);
            using var bitmap = icon.ToBitmap();

            var decoded = D2DBitmapLoader.Decode(bitmap);
            _cached[i] = decoded;
            DebugLog.Write($"TwitchIconLoader: decode OK ({variant}) {decoded.Width}x{decoded.Height}");
        }
        catch (Exception ex)
        {
            DebugLog.WriteException($"TwitchIconLoader.GetDecodedIcon({variant})", ex);
        }

        return _cached[i];
    }
}