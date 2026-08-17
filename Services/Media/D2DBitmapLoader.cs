using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Vortice.Direct2D1;
using Vortice.DXGI;
using Vortice.Mathematics;

namespace TTNOverlay.Services;

/// <summary>
/// Downloads images and decodes them into Direct2D bitmaps for rendering.
/// </summary>
internal static class D2DBitmapLoader
{
    private static readonly HttpClient Http = SharedHttpClient.Instance;

    /// <summary>
    /// Represents a decoded image with premultiplied BGRA pixel data.
    /// </summary>
    public readonly record struct DecodedImage(
        byte[] PremultipliedBgra,
        int Width,
        int Height,
        int Stride
    );

    /// <summary>
    /// Downloads an image from the specified URL and decodes it into a <see cref="DecodedImage"/>.
    /// </summary>
    /// <param name="url">The image URL.</param>
    /// <param name="targetSize">Optional target size for resizing.</param>
    /// <returns>A decoded image, or null if the download or decode fails.</returns>
    public static async Task<DecodedImage?> DownloadAndDecodeAsync(string url, int targetSize = 0)
    {
        try
        {
            DebugLog.Write($"D2DBitmapLoader: GET {url}");
            byte[] bytes = await Http.GetByteArrayAsync(url);
            DebugLog.Write($"D2DBitmapLoader: download OK ({bytes.Length} bytes). {url}");

            var webp = WebpDecoder.TryDecode(bytes, targetSize);
            if (webp is not null)
            {
                var decodedWebp = Decode(webp.Value);
                DebugLog.Write(
                    $"D2DBitmapLoader: decode OK (WebP) {decodedWebp.Width}x{decodedWebp.Height} stride={decodedWebp.Stride}. {url}"
                );
                return decodedWebp;
            }

            using var stream = new MemoryStream(bytes);
            using var gdiBitmap = new Bitmap(stream);

            if (targetSize > 0 && (gdiBitmap.Width > targetSize || gdiBitmap.Height > targetSize))
            {
                using var resized = new Bitmap(gdiBitmap, targetSize, targetSize);
                var decodedResized = Decode(resized);
                DebugLog.Write(
                    $"D2DBitmapLoader: decode OK (GDI+, resized to {targetSize}) {decodedResized.Width}x{decodedResized.Height} stride={decodedResized.Stride}. {url}"
                );
                return decodedResized;
            }

            var decoded = Decode(gdiBitmap);
            DebugLog.Write(
                $"D2DBitmapLoader: decode OK {decoded.Width}x{decoded.Height} stride={decoded.Stride}. {url}"
            );
            return decoded;
        }
        catch (Exception ex)
        {
            DebugLog.Write(
                $"D2DBitmapLoader: FAILED download/decode ({ex.GetType().Name}: {ex.Message}). {url}"
            );
            return null;
        }
    }

    /// <summary>
    /// Decodes a GDI+ <see cref="Bitmap"/> into a <see cref="DecodedImage"/> with premultiplied alpha.
    /// </summary>
    /// <param name="gdiBitmap">The source bitmap.</param>
    /// <returns>A decoded image.</returns>
    public static DecodedImage Decode(Bitmap gdiBitmap)
    {
        int width = gdiBitmap.Width;
        int height = gdiBitmap.Height;
        var fullRect = new Rectangle(0, 0, width, height);

        var locked = gdiBitmap.LockBits(
            fullRect,
            ImageLockMode.ReadOnly,
            PixelFormat.Format32bppArgb
        );
        try
        {
            int stride = locked.Stride;
            var pixels = new byte[stride * height];
            Marshal.Copy(locked.Scan0, pixels, 0, pixels.Length);
            PremultiplyInPlace(pixels);
            return new DecodedImage(pixels, width, height, stride);
        }
        finally
        {
            gdiBitmap.UnlockBits(locked);
        }
    }

    /// <summary>
    /// Decodes raw BGRA pixel data into a <see cref="DecodedImage"/> with premultiplied alpha.
    /// </summary>
    /// <param name="raw">The raw BGRA data.</param>
    /// <returns>A decoded image.</returns>
    public static DecodedImage Decode(RawBgra raw)
    {
        var pixels = raw.Pixels;
        PremultiplyInPlace(pixels);
        return new DecodedImage(pixels, raw.Width, raw.Height, raw.Stride);
    }

    /// <summary>
    /// Creates a Direct2D bitmap from a decoded image.
    /// </summary>
    /// <param name="target">The D2D render target.</param>
    /// <param name="image">The decoded image data.</param>
    /// <param name="key">An optional cache key for logging.</param>
    /// <returns>A Direct2D bitmap.</returns>
    public static ID2D1Bitmap CreateBitmap(ID2D1DCRenderTarget target, DecodedImage image, string key = "")
    {
        var props = new BitmapProperties(
            new Vortice.DCommon.PixelFormat(
                Format.B8G8R8A8_UNorm,
                Vortice.DCommon.AlphaMode.Premultiplied
            )
        );

        var handle = GCHandle.Alloc(image.PremultipliedBgra, GCHandleType.Pinned);
        try
        {
            var bitmap = target.CreateBitmap(
                new SizeI(image.Width, image.Height),
                handle.AddrOfPinnedObject(),
                (uint)image.Stride,
                props
            );
            DebugLog.Write($"D2DBitmapLoader: CreateBitmap OK key={key} {image.Width}x{image.Height}");
            return bitmap;
        }
        catch (Exception ex)
        {
            DebugLog.WriteException("D2DBitmapLoader.CreateBitmap", ex);
            throw;
        }
        finally
        {
            handle.Free();
        }
    }

    /// <summary>
    /// Converts BGRA pixel data to premultiplied alpha format in place.
    /// </summary>
    /// <param name="bgra">The BGRA pixel array.</param>
    private static void PremultiplyInPlace(byte[] bgra)
    {
        for (int i = 0; i < bgra.Length; i += 4)
        {
            byte a = bgra[i + 3];
            if (a == 255)
                continue;
            bgra[i] = (byte)(bgra[i] * a / 255);
            bgra[i + 1] = (byte)(bgra[i + 1] * a / 255);
            bgra[i + 2] = (byte)(bgra[i + 2] * a / 255);
        }
    }
}