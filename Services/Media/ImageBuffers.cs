namespace TTNOverlay.Overlay;

/// <summary>
/// Raw BGRA pixel buffer helpers, including nearest-neighbor downscaling for cached images.
/// </summary>
internal readonly record struct RawBgra(byte[] Pixels, int Width, int Height, int Stride)
{

    public RawBgra DownscaleIfNeeded(int targetSize)
    {
        if (targetSize <= 0 || (Width <= targetSize && Height <= targetSize))
            return this;

        int dstStride = targetSize * 4;
        var dst = new byte[dstStride * targetSize];

        for (int y = 0; y < targetSize; y++)
        {
            int srcY = Math.Min(Height - 1, y * Height / targetSize);
            int srcRowStart = srcY * Width * 4;
            int dstRowStart = y * dstStride;

            for (int x = 0; x < targetSize; x++)
            {
                int srcX = Math.Min(Width - 1, x * Width / targetSize);
                int srcI = srcRowStart + srcX * 4;
                int dstI = dstRowStart + x * 4;

                dst[dstI] = Pixels[srcI];
                dst[dstI + 1] = Pixels[srcI + 1];
                dst[dstI + 2] = Pixels[srcI + 2];
                dst[dstI + 3] = Pixels[srcI + 3];
            }
        }

        return new RawBgra(dst, targetSize, targetSize, dstStride);
    }
}

internal readonly record struct RawAnimatedFrame(RawBgra Image, int DelayMs);

