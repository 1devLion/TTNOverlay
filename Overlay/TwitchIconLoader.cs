using System;
using System.Drawing;
using System.Reflection;
using TTNOverlay.Services;

namespace TTNOverlay.Overlay;

/// <summary>
/// Loads the Twitch glyph used on every "Log in with Twitch" / "Log out" button (see
/// TwitchLoginButtonStyle). Same load-once-decode-once shape as AppIconLoader: the raw decoded
/// pixels are cached here and reused by all three call sites, but each of them still has to turn
/// those pixels into its own ID2D1Bitmap, since a Direct2D bitmap is bound to whichever render
/// target created it and ChatRenderWindow / SettingsRenderWindow are separate native windows.
/// </summary>
internal static class TwitchIconLoader
{
    private const int RequestedSize = 32;
    private const string ResourceName = "TTNOverlay.Resources.twitch.ico";

    private static D2DBitmapLoader.DecodedImage? _cached;
    private static bool _loadAttempted;

    public static D2DBitmapLoader.DecodedImage? GetDecodedIcon()
    {
        if (_cached is not null || _loadAttempted)
            return _cached;
        _loadAttempted = true;

        try
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName);
            if (stream is null)
            {
                DebugLog.Write($"TwitchIconLoader: embedded resource not found ({ResourceName})");
                return null;
            }

            using var icon = new Icon(stream, RequestedSize, RequestedSize);
            using var bitmap = icon.ToBitmap();

            _cached = D2DBitmapLoader.Decode(bitmap);
            DebugLog.Write($"TwitchIconLoader: decode OK {_cached.Value.Width}x{_cached.Value.Height}");
        }
        catch (Exception ex)
        {
            DebugLog.WriteException("TwitchIconLoader.GetDecodedIcon", ex);
        }

        return _cached;
    }
}