namespace TTNOverlay.Services;

/// <summary>
/// Provides methods to read and decode local image files, including static images and animated GIF/WebP.
/// </summary>
internal static class LocalImageLoader
{
    /// <summary>
    /// Reads all bytes from a file at the specified path.
    /// </summary>
    /// <param name="path">The file path.</param>
    /// <returns>The file bytes, or null if an error occurs.</returns>
    public static async Task<byte[]?> ReadBytesAsync(string path)
    {
        try
        {
            return await File.ReadAllBytesAsync(path);
        }
        catch (Exception ex)
        {
            DebugLog.Write($"LocalImageLoader: no se pudo leer {path}. {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Attempts to decode an animated image (GIF or WebP) into a list of frames.
    /// Returns null if the file is not animated or has fewer than 2 frames.
    /// </summary>
    /// <param name="bytes">The image byte data.</param>
    /// <param name="targetSize">Optional target size for resizing frames.</param>
    /// <returns>A list of animated frames, or null.</returns>
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
            DebugLog.Write($"LocalImageLoader: decode animado falló. {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Attempts to decode a static image (JPG, PNG, GIF, or WebP) into a <see cref="D2DBitmapLoader.DecodedImage"/>.
    /// </summary>
    /// <param name="bytes">The image byte data.</param>
    /// <param name="targetSize">Optional target size for resizing the image.</param>
    /// <returns>A decoded image, or null if decoding fails.</returns>
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
            DebugLog.Write($"LocalImageLoader: decode estático falló. {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }
}