using System.IO;
using System.Threading.Tasks;
using TTNOverlay.Services;

namespace TTNOverlay.Overlay;

/// <summary>
/// Reads and decodes local image/GIF/WebP files (unlike D2DBitmapLoader/AnimatedImageCache,
/// which download from a URL).
/// </summary>
internal static class LocalImageLoader
{
    public static async Task<byte[]?> ReadBytesAsync(string path)
    {
        try
        {
            return await File.ReadAllBytesAsync(path);
        }
        catch (Exception ex)
        {
            DebugLog.Write($"LocalImageLoader: no se pudo leer {path} -- {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    /// <summary>Returns animated frames only if the file has 2+ real frames; otherwise, null.</summary>
    public static List<RawAnimatedFrame>? TryDecodeAnimated(byte[] bytes, int targetSize = 0)
    {
        try
        {
            bool isWebp = bytes.Length > 12
                && bytes[0] == (byte)'R' && bytes[1] == (byte)'I' && bytes[2] == (byte)'F' && bytes[3] == (byte)'F'
                && bytes[8] == (byte)'W' && bytes[9] == (byte)'E' && bytes[10] == (byte)'B' && bytes[11] == (byte)'P';

            return isWebp ? WebpDecoder.TryDecodeAnimated(bytes, targetSize) : GifDecoder.TryDecode(bytes, targetSize);
        }
        catch (Exception ex)
        {
            DebugLog.Write($"LocalImageLoader: decode animado falló -- {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    /// <summary>Static decode of a single frame. Works for single-frame JPG/PNG/GIF or WebP files.</summary>
    public static D2DBitmapLoader.DecodedImage? TryDecodeStatic(byte[] bytes, int targetSize = 0)
    {
        try
        {
            var webp = WebpDecoder.TryDecode(bytes, targetSize);
            if (webp is not null)
                return D2DBitmapLoader.Decode(webp.Value);

            using var stream = new MemoryStream(bytes);
            using var gdiBitmap = new System.Drawing.Bitmap(stream);

            if (targetSize > 0 && (gdiBitmap.Width > targetSize || gdiBitmap.Height > targetSize))
            {
                using var resized = new System.Drawing.Bitmap(gdiBitmap, targetSize, targetSize);
                return D2DBitmapLoader.Decode(resized);
            }

            return D2DBitmapLoader.Decode(gdiBitmap);
        }
        catch (Exception ex)
        {
            DebugLog.Write($"LocalImageLoader: decode estático falló -- {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }
}