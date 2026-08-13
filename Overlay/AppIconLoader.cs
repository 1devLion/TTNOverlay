using System;
using System.Drawing;
using System.Reflection;
using TTNOverlay.Services;

namespace TTNOverlay.Overlay;

/// <summary>
/// Loads the application icon at a requested size for use as the window and tray icon.
/// </summary>
internal static class AppIconLoader
{

    private const int RequestedSize = 72;
    private const string ResourceName = "TTNOverlay.Resources.icon.ico";

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
                DebugLog.Write($"AppIconLoader: embedded resource not found ({ResourceName})");
                return null;
            }

            using var icon = new Icon(stream, RequestedSize, RequestedSize);
            using var bitmap = icon.ToBitmap();

            _cached = D2DBitmapLoader.Decode(bitmap);
            DebugLog.Write($"AppIconLoader: decode OK {_cached.Value.Width}x{_cached.Value.Height}");
        }
        catch (Exception ex)
        {
            DebugLog.WriteException("AppIconLoader.GetDecodedIcon", ex);
        }

        return _cached;
    }
}