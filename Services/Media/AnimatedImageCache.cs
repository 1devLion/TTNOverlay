using System.Net.Http;
using TTNOverlay.Services;

namespace TTNOverlay.Overlay;

/// <summary>
/// Downloads and caches decoded animated image frames (GIF/WebP/APNG) by URL, bounded by total memory usage.
/// </summary>
internal static class AnimatedImageCache
{

    private const int MaxCachedBytes = 8 * 1024 * 1024;
    private static readonly HttpClient Http = SharedHttpClient.Instance;
    private static readonly LruCache<string, List<RawAnimatedFrame>?> _cache =
        new(MaxCachedBytes, weigher: FrameBytes);

    private static int FrameBytes(List<RawAnimatedFrame>? frames)
    {
        if (frames is null || frames.Count == 0)
            return 1;
        long total = 0;
        foreach (var f in frames)
            total += (long)f.Image.Width * f.Image.Height * 4;
        return total > int.MaxValue ? int.MaxValue : (int)total;
    }

    public static Task<List<RawAnimatedFrame>?> GetFramesAsync(string cacheKey, string animatedUrl, int targetSize)
    {
        return _cache.GetOrAdd(cacheKey, async _ =>
        {
            try
            {
                var bytes = await Http.GetByteArrayAsync(animatedUrl);
                bool isWebp = bytes.Length > 12 &&
                              bytes[0] == (byte)'R' && bytes[1] == (byte)'I' &&
                              bytes[2] == (byte)'F' && bytes[3] == (byte)'F' &&
                              bytes[8] == (byte)'W' && bytes[9] == (byte)'E' &&
                              bytes[10] == (byte)'B' && bytes[11] == (byte)'P';
                return isWebp ? WebpDecoder.TryDecodeAnimated(bytes, targetSize)
                              : GifDecoder.TryDecode(bytes, targetSize);
            }
            catch
            {
                return null;
            }
        });
    }

    public static void Clear() => _cache.Clear();
}

