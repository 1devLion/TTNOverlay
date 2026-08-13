namespace TTNOverlay.Overlay;

/// <summary>
/// Minimal GIF decoder that extracts raw BGRA frames for the animated image pipeline.
/// </summary>
internal static class GifDecoder
{
    private const int BytesPerPixel = 4;

    private readonly record struct Rect(int X, int Y, int Width, int Height)
    {
        public static readonly Rect Empty = new(0, 0, 0, 0);
    }

    public static List<RawAnimatedFrame>? TryDecode(byte[] data, int targetSize = 0, int maxFrames = int.MaxValue)
    {
        try
        {
            return DecodeCore(data, targetSize, maxFrames);
        }
        catch
        {
            return null;
        }
    }

    private static List<RawAnimatedFrame>? DecodeCore(byte[] data, int targetSize, int maxFrames)
    {
        int pos = 0;
        if (!TryReadHeader(data, ref pos))
            return null;

        var (canvasWidth, canvasHeight, globalColorTable) = ReadLogicalScreenDescriptor(data, ref pos);
        if (canvasWidth <= 0 || canvasHeight <= 0)
            return null;

        int canvasStride = canvasWidth * BytesPerPixel;
        var canvas = new byte[canvasHeight * canvasStride];

        byte[]? restoreBuffer = null;
        var restoreRect = Rect.Empty;
        int pendingDisposal = 0;
        var pendingRect = Rect.Empty;

        int pendingDelayMs = 100;
        int pendingDisposalMethod = 0;
        bool pendingTransparentFlag = false;
        byte pendingTransparentIndex = 0;

        var frames = new List<RawAnimatedFrame>();

        while (pos < data.Length && frames.Count < maxFrames)
        {
            byte marker = data[pos++];

            if (marker == 0x3B)
                break;

            if (marker == 0x21)
            {
                if (pos >= data.Length)
                    break;
                byte label = data[pos++];

                if (label == 0xF9)
                {
                    if (pos + 6 > data.Length)
                        break;
                    byte blockSize = data[pos++];
                    byte packed = data[pos];
                    pendingDisposalMethod = (packed >> 2) & 0x07;
                    pendingTransparentFlag = (packed & 0x01) != 0;
                    int delayCentis = ReadUInt16LE(data, pos + 1);
                    pendingDelayMs = Math.Max(delayCentis * 10, 20);
                    pendingTransparentIndex = data[pos + 3];
                    pos += blockSize;
                    pos = SkipSubBlocks(data, pos);
                }
                else
                {

                    pos = SkipSubBlocks(data, pos);
                }
                continue;
            }

            if (marker == 0x2C)
            {
                if (pos + 9 > data.Length)
                    break;
                int left = ReadUInt16LE(data, pos);
                int top = ReadUInt16LE(data, pos + 2);
                int imgWidth = ReadUInt16LE(data, pos + 4);
                int imgHeight = ReadUInt16LE(data, pos + 6);
                byte packed = data[pos + 8];
                pos += 9;

                bool hasLocalColorTable = (packed & 0x80) != 0;
                bool interlaced = (packed & 0x40) != 0;
                int lctSizeBits = packed & 0x07;

                byte[] colorTable = globalColorTable;
                if (hasLocalColorTable)
                {
                    int lctEntries = 2 << lctSizeBits;
                    colorTable = ReadColorTable(data, ref pos, lctEntries);
                }

                if (pos >= data.Length)
                    break;
                byte minCodeSize = data[pos++];
                var lzwData = ReadSubBlocks(data, ref pos);

                if (imgWidth <= 0 || imgHeight <= 0 || minCodeSize is < 2 or > 11)
                {

                    pendingDelayMs = 100;
                    pendingDisposalMethod = 0;
                    pendingTransparentFlag = false;
                    pendingTransparentIndex = 0;
                    continue;
                }

                var indices = DecodeLzw(lzwData, minCodeSize, imgWidth * imgHeight);
                if (interlaced)
                    indices = Deinterlace(indices, imgWidth, imgHeight);

                var frameBgra = IndicesToBgra(indices, colorTable, pendingTransparentFlag, pendingTransparentIndex);
                var frameRect = ClipToCanvas(new Rect(left, top, imgWidth, imgHeight), canvasWidth, canvasHeight);

                ApplyDisposal(canvas, canvasStride, pendingDisposal, pendingRect, restoreBuffer, restoreRect);

                restoreBuffer = pendingDisposalMethod == 3
                    ? CopyRegion(canvas, canvasStride, frameRect, out restoreRect)
                    : null;

                CompositeOnto(canvas, canvasStride, canvasWidth, canvasHeight, frameBgra, imgWidth * BytesPerPixel, frameRect, left, top);

                var snapshot = (byte[])canvas.Clone();
                var resized = new RawBgra(snapshot, canvasWidth, canvasHeight, canvasStride).DownscaleIfNeeded(targetSize);
                frames.Add(new RawAnimatedFrame(resized, pendingDelayMs));

                pendingDisposal = pendingDisposalMethod;
                pendingRect = frameRect;

                pendingDelayMs = 100;
                pendingDisposalMethod = 0;
                pendingTransparentFlag = false;
                pendingTransparentIndex = 0;
                continue;
            }

            break;
        }

        return frames.Count >= 2 ? frames : null;
    }

    private static bool TryReadHeader(byte[] data, ref int pos)
    {
        if (data.Length < 13)
            return false;
        bool ok = data[0] == 'G' && data[1] == 'I' && data[2] == 'F'
            && data[3] == '8' && (data[4] == '7' || data[4] == '9') && data[5] == 'a';
        pos = 6;
        return ok;
    }

    private static (int Width, int Height, byte[] GlobalColorTable) ReadLogicalScreenDescriptor(byte[] data, ref int pos)
    {
        int width = ReadUInt16LE(data, pos);
        int height = ReadUInt16LE(data, pos + 2);
        byte packed = data[pos + 4];
        pos += 7;

        bool hasGlobalColorTable = (packed & 0x80) != 0;
        int gctSizeBits = packed & 0x07;

        byte[] globalColorTable = Array.Empty<byte>();
        if (hasGlobalColorTable)
        {
            int entries = 2 << gctSizeBits;
            globalColorTable = ReadColorTable(data, ref pos, entries);
        }

        return (width, height, globalColorTable);
    }

    private static byte[] ReadColorTable(byte[] data, ref int pos, int entries)
    {
        int byteCount = entries * 3;
        var table = new byte[byteCount];
        Array.Copy(data, pos, table, 0, Math.Min(byteCount, data.Length - pos));
        pos += byteCount;
        return table;
    }

    private static int SkipSubBlocks(byte[] data, int pos)
    {
        while (pos < data.Length)
        {
            byte len = data[pos++];
            if (len == 0)
                break;
            pos += len;
        }
        return Math.Min(pos, data.Length);
    }

    private static byte[] ReadSubBlocks(byte[] data, ref int pos)
    {
        var result = new List<byte>();
        while (pos < data.Length)
        {
            byte len = data[pos++];
            if (len == 0)
                break;
            int take = Math.Min(len, data.Length - pos);
            for (int i = 0; i < take; i++)
                result.Add(data[pos + i]);
            pos += len;
        }
        pos = Math.Min(pos, data.Length);
        return result.ToArray();
    }

    private static byte[] DecodeLzw(byte[] lzwData, int minCodeSize, int expectedPixelCount)
    {
        int clearCode = 1 << minCodeSize;
        int endCode = clearCode + 1;

        var dict = new List<byte[]>(4096);
        int codeSize = minCodeSize + 1;
        int nextCode = endCode + 1;

        void ResetDict()
        {
            dict.Clear();
            for (int i = 0; i < clearCode; i++)
                dict.Add(new[] { (byte)i });
            dict.Add(Array.Empty<byte>());
            dict.Add(Array.Empty<byte>());
            codeSize = minCodeSize + 1;
            nextCode = endCode + 1;
        }
        ResetDict();

        var output = new byte[expectedPixelCount];
        int outPos = 0;

        int bytePos = 0, bitBuf = 0, bitCount = 0;
        byte[]? prev = null;

        while (outPos < expectedPixelCount)
        {
            while (bitCount < codeSize)
            {
                if (bytePos >= lzwData.Length)
                    goto done;
                bitBuf |= lzwData[bytePos++] << bitCount;
                bitCount += 8;
            }
            int code = bitBuf & ((1 << codeSize) - 1);
            bitBuf >>= codeSize;
            bitCount -= codeSize;

            if (code == clearCode)
            {
                ResetDict();
                prev = null;
                continue;
            }
            if (code == endCode)
                break;

            byte[] entry;
            if (code < dict.Count)
            {
                entry = dict[code];
            }
            else if (code == nextCode && prev is not null)
            {
                entry = Append(prev, prev[0]);
            }
            else
            {
                break;
            }

            int copyLen = Math.Min(entry.Length, expectedPixelCount - outPos);
            Array.Copy(entry, 0, output, outPos, copyLen);
            outPos += copyLen;

            if (prev is not null && nextCode < 4096)
            {
                dict.Add(Append(prev, entry[0]));
                nextCode++;
                if (nextCode == (1 << codeSize) && codeSize < 12)
                    codeSize++;
            }

            prev = entry;

            if (outPos >= expectedPixelCount)
                break;
        }

        done:
        return output;

        static byte[] Append(byte[] a, byte b)
        {
            var r = new byte[a.Length + 1];
            Array.Copy(a, r, a.Length);
            r[^1] = b;
            return r;
        }
    }

    private static byte[] Deinterlace(byte[] indices, int width, int height)
    {
        var result = new byte[indices.Length];
        int srcRow = 0;

        void CopyPass(int startRow, int step)
        {
            for (int row = startRow; row < height; row += step)
            {
                if (srcRow >= height)
                    return;
                Array.Copy(indices, srcRow * width, result, row * width, width);
                srcRow++;
            }
        }

        CopyPass(0, 8);
        CopyPass(4, 8);
        CopyPass(2, 4);
        CopyPass(1, 2);

        return result;
    }

    private static byte[] IndicesToBgra(byte[] indices, byte[] colorTable, bool transparentFlag, byte transparentIndex)
    {
        var bgra = new byte[indices.Length * BytesPerPixel];
        int entries = colorTable.Length / 3;

        for (int i = 0; i < indices.Length; i++)
        {
            byte index = indices[i];
            int bi = i * BytesPerPixel;

            if (transparentFlag && index == transparentIndex)
            {

                continue;
            }

            if (index >= entries)
                continue;

            int ci = index * 3;
            bgra[bi] = colorTable[ci + 2];
            bgra[bi + 1] = colorTable[ci + 1];
            bgra[bi + 2] = colorTable[ci];
            bgra[bi + 3] = 255;
        }

        return bgra;
    }

    private static void ApplyDisposal(
        byte[] canvas,
        int stride,
        int disposal,
        Rect rect,
        byte[]? restoreBuffer,
        Rect restoreRect
    )
    {
        if (rect.Width <= 0 || rect.Height <= 0)
            return;

        switch (disposal)
        {
            case 2:
                ClearRegion(canvas, stride, rect);
                break;
            case 3:
                if (restoreBuffer is not null)
                    RestoreRegion(canvas, stride, restoreRect, restoreBuffer);
                break;
        }
    }

    private static void CompositeOnto(
        byte[] canvas,
        int canvasStride,
        int canvasWidth,
        int canvasHeight,
        byte[] frame,
        int frameStride,
        Rect clippedRect,
        int frameLeft,
        int frameTop
    )
    {
        for (int y = 0; y < clippedRect.Height; y++)
        {
            int canvasY = clippedRect.Y + y;
            int frameY = canvasY - frameTop;
            for (int x = 0; x < clippedRect.Width; x++)
            {
                int canvasX = clippedRect.X + x;
                int frameX = canvasX - frameLeft;

                int fi = frameY * frameStride + frameX * BytesPerPixel;
                byte srcA = frame[fi + 3];
                if (srcA == 0)
                    continue;

                int ci = canvasY * canvasStride + canvasX * BytesPerPixel;
                canvas[ci + 0] = frame[fi + 0];
                canvas[ci + 1] = frame[fi + 1];
                canvas[ci + 2] = frame[fi + 2];
                canvas[ci + 3] = 255;

            }
        }
    }

    private static void ClearRegion(byte[] canvas, int stride, Rect rect)
    {
        for (int y = 0; y < rect.Height; y++)
        {
            int rowStart = (rect.Y + y) * stride + rect.X * BytesPerPixel;
            Array.Clear(canvas, rowStart, rect.Width * BytesPerPixel);
        }
    }

    private static byte[] CopyRegion(byte[] canvas, int stride, Rect rect, out Rect copiedRect)
    {
        copiedRect = rect;
        var buffer = new byte[rect.Height * rect.Width * BytesPerPixel];
        int rowBytes = rect.Width * BytesPerPixel;
        for (int y = 0; y < rect.Height; y++)
        {
            int srcStart = (rect.Y + y) * stride + rect.X * BytesPerPixel;
            Buffer.BlockCopy(canvas, srcStart, buffer, y * rowBytes, rowBytes);
        }
        return buffer;
    }

    private static void RestoreRegion(byte[] canvas, int stride, Rect rect, byte[] buffer)
    {
        int rowBytes = rect.Width * BytesPerPixel;
        for (int y = 0; y < rect.Height; y++)
        {
            int dstStart = (rect.Y + y) * stride + rect.X * BytesPerPixel;
            Buffer.BlockCopy(buffer, y * rowBytes, canvas, dstStart, rowBytes);
        }
    }

    private static Rect ClipToCanvas(Rect rect, int canvasWidth, int canvasHeight)
    {
        int left = Math.Max(0, rect.X);
        int top = Math.Max(0, rect.Y);
        int right = Math.Min(canvasWidth, rect.X + rect.Width);
        int bottom = Math.Min(canvasHeight, rect.Y + rect.Height);
        return new Rect(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top));
    }

    private static int ReadUInt16LE(byte[] data, int offset) =>
        data[offset] | (data[offset + 1] << 8);
}

