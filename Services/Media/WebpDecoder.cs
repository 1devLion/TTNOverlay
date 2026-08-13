using System.IO;
using System.Runtime.InteropServices;

namespace TTNOverlay.Overlay;

/// <summary>
/// P/Invoke wrapper around libwebp used to decode static and animated WebP images.
/// </summary>
internal static class WebpDecoder
{
    [DllImport("libwebp", CallingConvention = CallingConvention.Cdecl)]
    private static extern int WebPGetInfo(byte[] data, nuint dataSize, out int width, out int height);

    [DllImport("libwebp", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr WebPDecodeBGRAInto(
        byte[] data,
        nuint dataSize,
        byte[] outputBuffer,
        nuint outputBufferSize,
        int outputStride
    );

    public static RawBgra? TryDecode(byte[] webpBytes, int targetSize = 0)
    {
        try
        {
            var (pixels, width, height) = DecodeBgra(webpBytes);
            if (pixels is null)
                return null;

            return new RawBgra(pixels, width, height, width * 4).DownscaleIfNeeded(targetSize);
        }
        catch (DllNotFoundException)
        {
            return null;
        }
        catch
        {
            return null;
        }
    }

    private static (byte[]? pixels, int width, int height) DecodeBgra(byte[] data)
    {
        if (WebPGetInfo(data, (nuint)data.Length, out int width, out int height) == 0)
            return (null, 0, 0);

        int stride = width * 4;
        var pixels = new byte[stride * height];

        var result = WebPDecodeBGRAInto(data, (nuint)data.Length, pixels, (nuint)pixels.Length, stride);
        return result == IntPtr.Zero ? (null, 0, 0) : (pixels, width, height);
    }

    private readonly record struct Rect(int X, int Y, int Width, int Height);

    public static List<RawAnimatedFrame>? TryDecodeAnimated(byte[] data, int targetSize = 0)
    {
        try
        {
            if (!TryParseExtendedHeader(data, out int canvasWidth, out int canvasHeight, out int offset))
                return null;

            var canvas = new byte[canvasWidth * canvasHeight * 4];
            var frames = new List<RawAnimatedFrame>();
            Rect? previousRect = null;
            bool disposePreviousToTransparent = false;

            const int maxFrames = 240;

            while (frames.Count < maxFrames && offset + 8 <= data.Length)
            {
                if (ReadFourCc(data, offset) != "ANMF")
                    break;

                uint chunkSize = ReadUInt32LE(data, offset + 4);
                int payloadStart = offset + 8;
                int frameDataEnd = payloadStart + (int)chunkSize;
                if (frameDataEnd > data.Length)
                    break;

                int frameX = (int)ReadUInt24LE(data, payloadStart) * 2;
                int frameY = (int)ReadUInt24LE(data, payloadStart + 3) * 2;
                int frameWidth = (int)ReadUInt24LE(data, payloadStart + 6) + 1;
                int frameHeight = (int)ReadUInt24LE(data, payloadStart + 9) + 1;
                int durationMs = (int)ReadUInt24LE(data, payloadStart + 12);
                byte flags = data[payloadStart + 15];
                bool blendAlpha = (flags & 0x02) == 0;
                bool disposeToTransparent = (flags & 0x01) != 0;

                if (previousRect is { } prevRect && disposePreviousToTransparent)
                    ClearRect(canvas, canvasWidth, canvasHeight, prevRect);

                var frameDataStart = payloadStart + 16;
                var (framePixels, decodedW, decodedH) = DecodeAnmfFrame(
                    data,
                    frameDataStart,
                    frameDataEnd,
                    frameWidth,
                    frameHeight
                );

                if (framePixels is not null)
                {
                    CompositeFrame(
                        canvas,
                        canvasWidth,
                        canvasHeight,
                        framePixels,
                        frameX,
                        frameY,
                        decodedW,
                        decodedH,
                        blendAlpha
                    );

                    var snapshot = BuildFrameSnapshot(canvas, canvasWidth, canvasHeight, targetSize);
                    frames.Add(new RawAnimatedFrame(snapshot, Math.Max(durationMs, 20)));
                }

                previousRect = new Rect(frameX, frameY, frameWidth, frameHeight);
                disposePreviousToTransparent = disposeToTransparent;

                offset = frameDataEnd + (int)(chunkSize % 2);
            }

            return frames.Count >= 2 ? frames : null;
        }
        catch (DllNotFoundException)
        {
            return null;
        }
        catch
        {
            return null;
        }
    }

    private static RawBgra BuildFrameSnapshot(byte[] canvas, int canvasWidth, int canvasHeight, int targetSize)
    {
        var snapshot = (byte[])canvas.Clone();
        return new RawBgra(snapshot, canvasWidth, canvasHeight, canvasWidth * 4).DownscaleIfNeeded(targetSize);
    }

    private static (byte[]? pixels, int width, int height) DecodeAnmfFrame(
        byte[] data,
        int start,
        int end,
        int frameWidth,
        int frameHeight
    )
    {
        int pos = start;
        int alphStart = -1, alphLen = 0;
        string? bitstreamFourCc = null;
        int bsStart = -1, bsLen = 0;

        while (pos + 8 <= end)
        {
            string fourCc = ReadFourCc(data, pos);
            int size = (int)ReadUInt32LE(data, pos + 4);
            int payloadStart = pos + 8;
            if (payloadStart + size > end)
                break;

            if (fourCc == "ALPH")
            {
                alphStart = payloadStart;
                alphLen = size;
            }
            else if (fourCc == "VP8 " || fourCc == "VP8L")
            {
                bitstreamFourCc = fourCc;
                bsStart = payloadStart;
                bsLen = size;
                break;
            }

            pos = payloadStart + size + (size % 2);
        }

        if (bitstreamFourCc is null)
            return (null, 0, 0);

        byte[] container = alphStart < 0
            ? BuildSimpleContainer(data, bitstreamFourCc, bsStart, bsLen)
            : BuildExtendedContainerWithAlpha(
                data,
                alphStart,
                alphLen,
                bitstreamFourCc,
                bsStart,
                bsLen,
                frameWidth,
                frameHeight
            );

        return DecodeBgra(container);
    }

    private static void CompositeFrame(
        byte[] canvas,
        int canvasWidth,
        int canvasHeight,
        byte[] frame,
        int frameX,
        int frameY,
        int frameWidth,
        int frameHeight,
        bool blendAlpha
    )
    {
        for (int y = 0; y < frameHeight; y++)
        {
            int cy = frameY + y;
            if (cy < 0 || cy >= canvasHeight)
                continue;

            for (int x = 0; x < frameWidth; x++)
            {
                int cx = frameX + x;
                if (cx < 0 || cx >= canvasWidth)
                    continue;

                int fi = (y * frameWidth + x) * 4;
                int ci = (cy * canvasWidth + cx) * 4;

                byte srcB = frame[fi], srcG = frame[fi + 1], srcR = frame[fi + 2], srcA = frame[fi + 3];

                if (!blendAlpha || srcA == 255)
                {
                    canvas[ci] = srcB;
                    canvas[ci + 1] = srcG;
                    canvas[ci + 2] = srcR;
                    canvas[ci + 3] = srcA;
                    continue;
                }
                if (srcA == 0)
                    continue;

                byte dstB = canvas[ci], dstG = canvas[ci + 1], dstR = canvas[ci + 2], dstA = canvas[ci + 3];
                int outA = srcA + dstA * (255 - srcA) / 255;
                if (outA == 0)
                {
                    canvas[ci] = canvas[ci + 1] = canvas[ci + 2] = canvas[ci + 3] = 0;
                    continue;
                }

                canvas[ci] = (byte)((srcB * srcA + dstB * dstA * (255 - srcA) / 255) / outA);
                canvas[ci + 1] = (byte)((srcG * srcA + dstG * dstA * (255 - srcA) / 255) / outA);
                canvas[ci + 2] = (byte)((srcR * srcA + dstR * dstA * (255 - srcA) / 255) / outA);
                canvas[ci + 3] = (byte)outA;
            }
        }
    }

    private static void ClearRect(byte[] canvas, int canvasWidth, int canvasHeight, Rect rect)
    {
        int x0 = Math.Max(rect.X, 0);
        int x1 = Math.Min(rect.X + rect.Width, canvasWidth);
        int y0 = Math.Max(rect.Y, 0);
        int y1 = Math.Min(rect.Y + rect.Height, canvasHeight);
        if (x1 <= x0 || y1 <= y0)
            return;

        int rowBytes = (x1 - x0) * 4;
        for (int y = y0; y < y1; y++)
            Array.Clear(canvas, (y * canvasWidth + x0) * 4, rowBytes);
    }

    private static bool TryParseExtendedHeader(byte[] data, out int canvasWidth, out int canvasHeight, out int firstAnmfOffset)
    {
        canvasWidth = canvasHeight = firstAnmfOffset = 0;
        if (data.Length < 30)
            return false;
        if (ReadFourCc(data, 0) != "RIFF" || ReadFourCc(data, 8) != "WEBP")
            return false;
        if (ReadFourCc(data, 12) != "VP8X")
            return false;

        uint vp8xSize = ReadUInt32LE(data, 16);
        int payload = 20;
        byte flags = data[payload];
        if ((flags & 0x02) == 0)
            return false;

        canvasWidth = (int)ReadUInt24LE(data, payload + 4) + 1;
        canvasHeight = (int)ReadUInt24LE(data, payload + 7) + 1;

        int pos = payload + (int)vp8xSize + (int)(vp8xSize % 2);

        if (pos + 8 <= data.Length && ReadFourCc(data, pos) == "ICCP")
        {
            uint sz = ReadUInt32LE(data, pos + 4);
            pos += 8 + (int)sz + (int)(sz % 2);
        }

        if (pos + 8 > data.Length || ReadFourCc(data, pos) != "ANIM")
            return false;

        uint animSize = ReadUInt32LE(data, pos + 4);
        pos += 8 + (int)animSize + (int)(animSize % 2);

        firstAnmfOffset = pos;
        return true;
    }

    private static byte[] BuildSimpleContainer(byte[] src, string fourCc, int start, int len)
    {
        using var ms = new MemoryStream();
        WriteRiffPlaceholder(ms);
        WriteChunk(ms, fourCc, src, start, len);
        return FinalizeRiff(ms);
    }

    private static byte[] BuildExtendedContainerWithAlpha(
        byte[] src,
        int alphStart,
        int alphLen,
        string bitstreamFourCc,
        int bsStart,
        int bsLen,
        int width,
        int height
    )
    {
        using var ms = new MemoryStream();
        WriteRiffPlaceholder(ms);

        WriteFourCcMs(ms, "VP8X");
        WriteUInt32LEMs(ms, 10);
        ms.WriteByte(0x10);
        ms.Write(new byte[3], 0, 3);
        WriteUInt24LEMs(ms, (uint)(width - 1));
        WriteUInt24LEMs(ms, (uint)(height - 1));

        WriteChunk(ms, "ALPH", src, alphStart, alphLen);
        WriteChunk(ms, bitstreamFourCc, src, bsStart, bsLen);

        return FinalizeRiff(ms);
    }

    private static void WriteRiffPlaceholder(MemoryStream ms) =>
        ms.Write(
            new byte[] { (byte)'R', (byte)'I', (byte)'F', (byte)'F', 0, 0, 0, 0, (byte)'W', (byte)'E', (byte)'B', (byte)'P' },
            0,
            12
        );

    private static byte[] FinalizeRiff(MemoryStream ms)
    {
        var buf = ms.ToArray();
        uint fileSize = (uint)(buf.Length - 8);
        buf[4] = (byte)fileSize;
        buf[5] = (byte)(fileSize >> 8);
        buf[6] = (byte)(fileSize >> 16);
        buf[7] = (byte)(fileSize >> 24);
        return buf;
    }

    private static void WriteChunk(MemoryStream ms, string fourCc, byte[] src, int start, int len)
    {
        WriteFourCcMs(ms, fourCc);
        WriteUInt32LEMs(ms, (uint)len);
        ms.Write(src, start, len);
        if (len % 2 != 0)
            ms.WriteByte(0);
    }

    private static void WriteFourCcMs(MemoryStream ms, string fourCc)
    {
        foreach (char c in fourCc)
            ms.WriteByte((byte)c);
    }

    private static void WriteUInt32LEMs(MemoryStream ms, uint value)
    {
        ms.WriteByte((byte)value);
        ms.WriteByte((byte)(value >> 8));
        ms.WriteByte((byte)(value >> 16));
        ms.WriteByte((byte)(value >> 24));
    }

    private static void WriteUInt24LEMs(MemoryStream ms, uint value)
    {
        ms.WriteByte((byte)value);
        ms.WriteByte((byte)(value >> 8));
        ms.WriteByte((byte)(value >> 16));
    }

    private static string ReadFourCc(byte[] data, int offset) =>
        $"{(char)data[offset]}{(char)data[offset + 1]}{(char)data[offset + 2]}{(char)data[offset + 3]}";

    private static uint ReadUInt32LE(byte[] data, int offset) =>
        (uint)(data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16) | (data[offset + 3] << 24));

    private static uint ReadUInt24LE(byte[] data, int offset) =>
        (uint)(data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16));
}

