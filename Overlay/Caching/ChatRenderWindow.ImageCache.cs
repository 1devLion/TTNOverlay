using System.Numerics;
using TTNOverlay.Services;
using Vortice.Direct2D1;
using Vortice.Mathematics;

namespace TTNOverlay.Overlay;

/// <summary>
/// ChatRenderWindow partial: resolves and caches the Direct2D bitmaps used for badges, emotes, and event images.
/// </summary>
internal sealed partial class ChatRenderWindow
{

    private const long MaxCachedImageBytes = 20L * 1024 * 1024;
    private const int MaxCachedAnimatedFrames = 2000;
    private const int MaxCachedUsernameBrushes = 128;

    private readonly DisposingLruCache<string, ID2D1Bitmap?> _imageCache =
        new((int)MaxCachedImageBytes, weigher: ImageBitmapBytes, onEvict: (_, bmp) => bmp?.Dispose());

    private readonly HashSet<string> _imageLoadInFlight = new();

    private readonly DisposingLruCache<string, List<(ID2D1Bitmap Bitmap, int DelayMs)>?> _animatedImageCache =
        new(MaxCachedAnimatedFrames, weigher: frames => frames?.Count ?? 0, onEvict: (_, frames) => DisposeAnimatedFrames(frames));

    private readonly HashSet<string> _animatedLoadInFlight = new();
    private readonly Dictionary<string, (int Index, DateTime NextDueUtc)> _animationState = new();
    private System.Threading.Timer? _animationTimer;

    private readonly DisposingLruCache<string, ID2D1SolidColorBrush> _usernameBrushCache =
        new(MaxCachedUsernameBrushes, onEvict: (_, brush) => brush.Dispose());

    private static int ImageBitmapBytes(ID2D1Bitmap? bmp) =>
        bmp is null ? 0 : (int)(bmp.Size.Width * bmp.Size.Height * 4);

    /// <summary>Disposes an evicted/removed animated entry's frames. _animationState is intentionally
    /// left alone here: AdvanceAnimations already prunes any entry whose cache lookup misses on its
    /// next tick (at most 33ms later), so cleaning it up here too would be redundant.</summary>
    private static void DisposeAnimatedFrames(List<(ID2D1Bitmap Bitmap, int DelayMs)>? frames)
    {
        if (frames is not null)
            foreach (var f in frames)
                f.Bitmap.Dispose();
    }

    private ID2D1Bitmap? GetOrLoadImageBitmap(string key, string url, int targetSize)
    {
        if (_imageCache.TryGetValue(key, out var cached))
            return cached;

        if (!_imageLoadInFlight.Add(key))
            return null;

        DebugLog.Write($"GetOrLoadImageBitmap: firing new load from {key}. {url}");
        _ = LoadImageAsync(key, url, targetSize);
        return null;
    }

    private async Task LoadImageAsync(string key, string url, int targetSize)
    {
        var decoded = await D2DBitmapLoader.DownloadAndDecodeAsync(url, targetSize);
        DebugLog.Write(
            $"LoadImageAsync: {key} -- decode {(decoded is null ? "FAILED (see D2DBitmapLoader log above)" : "OK")}, _target {(_target is null ? "still null (no OnRender yet)" : "ready")}"
        );

        PostToUiThread(() =>
        {
            _imageLoadInFlight.Remove(key);

            if (decoded is null)
            {
                _imageCache.Set(key, null);
                _loggedWaitingBadges.Remove(key);
                DebugLog.Write($"LoadImageAsync: {key} cached as null (decode failed)");
                RequestRender();
                return;
            }

            if (_target is null)
            {
                DebugLog.Write($"LoadImageAsync: {key}.  _target still null, not caching, will retry");
                return;
            }

            try
            {
                var bitmap = D2DBitmapLoader.CreateBitmap(_target, decoded.Value, key);
                _imageCache.Set(key, bitmap);
                _loggedWaitingBadges.Remove(key);
                DebugLog.Write($"LoadImageAsync: {key} inserted into cache OK");
            }
            catch (Exception ex)
            {
                DebugLog.WriteException($"LoadImageAsync ({key})", ex);
                _imageCache.Set(key, null);
                _loggedWaitingBadges.Remove(key);
            }

            RequestRender();
        });
    }

    /// <summary>
    /// Resolves an embedded-resource badge (Kick role badges/sub_gifter tiers, see
    /// KickBadgeIconLoader) into a cached ID2D1Bitmap. Unlike GetOrLoadImageBitmap, this has no
    /// network round-trip -- the WebP is already in the assembly -- so it decodes and creates the
    /// bitmap synchronously on first use instead of going through the async load-in-flight path.
    /// </summary>
    private ID2D1Bitmap? GetOrCreateLocalBadgeBitmap(ID2D1DCRenderTarget target, string cacheKey, string localIconKey)
    {
        if (_imageCache.TryGetValue(cacheKey, out var cached))
            return cached;

        var decoded = KickBadgeIconLoader.GetDecodedIcon(localIconKey);
        if (decoded is null)
        {
            _imageCache.Set(cacheKey, null);
            return null;
        }

        try
        {
            var bitmap = D2DBitmapLoader.CreateBitmap(target, decoded.Value, cacheKey);
            _imageCache.Set(cacheKey, bitmap);
            return bitmap;
        }
        catch (Exception ex)
        {
            DebugLog.WriteException($"GetOrCreateLocalBadgeBitmap ({cacheKey})", ex);
            _imageCache.Set(cacheKey, null);
            return null;
        }
    }

    private ID2D1SolidColorBrush GetUsernameBrush(ID2D1DCRenderTarget target, string hex)
    {
        if (_usernameBrushCache.TryGetValue(hex, out var cached))
            return cached;

        var brush = target.CreateSolidColorBrush(ParseHexColor(hex));
        _usernameBrushCache.Set(hex, brush);
        return brush;
    }

    private static Color4 ParseHexColor(string hex)
    {
        try
        {
            if (hex.StartsWith('#'))
                hex = hex[1..];

            byte r = Convert.ToByte(hex[0..2], 16);
            byte g = Convert.ToByte(hex[2..4], 16);
            byte b = Convert.ToByte(hex[4..6], 16);
            return new Color4(r / 255f, g / 255f, b / 255f, 1f);
        }
        catch
        {
            return new Color4(0.69f, 0.69f, 0.69f, 1f);
        }
    }

    private void InvalidateMediaCaches()
    {
        _imageCache.Clear();
        _imageLoadInFlight.Clear();

        _animatedImageCache.Clear();
        _animatedLoadInFlight.Clear();
        _animationState.Clear();

        RequestRender();
    }

    private void DumpMediaCacheStats()
    {
        long staticBytes = 0;
        int staticCount = 0;
        foreach (var bmp in _imageCache.Values)
        {
            if (bmp is null) continue;
            staticBytes += (long)bmp.Size.Width * (long)bmp.Size.Height * 4;
            staticCount++;
        }

        long animBytes = 0;
        int animFrameCount = 0;
        int animEntryCount = 0;
        foreach (var frames in _animatedImageCache.Values)
        {
            if (frames is null) continue;
            animEntryCount++;
            foreach (var f in frames)
            {
                animBytes += (long)f.Bitmap.Size.Width * (long)f.Bitmap.Size.Height * 4;
                animFrameCount++;
            }
        }

        var ci = System.Globalization.CultureInfo.InvariantCulture;
        DebugLog.Write(
            $"DumpMediaCacheStats: static {staticCount} bitmaps ~{(staticBytes / 1024f / 1024f).ToString("F2", ci)} MB | "
            + $"animated {animEntryCount} emotes / {animFrameCount} frames ~{(animBytes / 1024f / 1024f).ToString("F2", ci)} MB | "
            + $"TOTAL ~{((staticBytes + animBytes) / 1024f / 1024f).ToString("F2", ci)} MB"
        );
    }

    private void DisposeImageCaches()
    {
        _animationTimer?.Dispose();
        _animationTimer = null;

        _usernameBrushCache.Clear();
        _imageCache.Clear();
        _animatedImageCache.Clear();
        _animationState.Clear();
    }

    private void PurgeEventIconCaches()
    {
        const string prefix = "eventicon:";

        _imageCache.RemoveWhere(k => k.StartsWith(prefix, StringComparison.Ordinal));
        _imageLoadInFlight.RemoveWhere(k => k.StartsWith(prefix, StringComparison.Ordinal));

        _animatedImageCache.RemoveWhere(k => k.StartsWith(prefix, StringComparison.Ordinal));
        _animatedLoadInFlight.RemoveWhere(k => k.StartsWith($"anim:{prefix}", StringComparison.Ordinal));
    }

    private void EnsureAnimationTimerRunning()
    {
        _animationTimer ??= new System.Threading.Timer(
            _ => PostToUiThread(AdvanceAnimations),
            null,
            33,
            33
        );
    }

    private void AdvanceAnimations()
    {
        if (_animationState.Count == 0)
            return;
        var now = DateTime.UtcNow;
        bool any = false;
        foreach (var key in _animationState.Keys.ToList())
        {
            if (!_animatedImageCache.TryGetValue(key, out var frames) || frames is null)
            {
                _animationState.Remove(key);
                continue;
            }

            if (now < _animationState[key].NextDueUtc)
                continue;

            int next = (_animationState[key].Index + 1) % frames.Count;
            _animationState[key] = (next, now.AddMilliseconds(frames[next].DelayMs));
            any = true;
        }
        if (any)
            RequestRender();
    }

    private void TryLoadAnimatedImage(
        string key,
        string animatedCacheKey,
        string url,
        float targetSize
    )
    {
        string flightKey = $"anim:{key}";

        if (_animatedImageCache.ContainsKey(key) || !_animatedLoadInFlight.Add(flightKey))
            return;

        _ = LoadAnimatedAsync(key, flightKey, animatedCacheKey, url, targetSize);
    }

    private async Task LoadAnimatedAsync(
        string key,
        string flightKey,
        string animatedCacheKey,
        string url,
        float targetSize
    )
    {
        var frames = await AnimatedImageCache.GetFramesAsync(
            animatedCacheKey,
            url,
            targetSize: (int)targetSize
        );

        PostToUiThread(() =>
        {
            _animatedLoadInFlight.Remove(flightKey);

            if (_target is null)
            {
                DebugLog.Write($"LoadAnimatedAsync: {key}. _target still null, not caching");
                return;
            }

            if (frames is null || frames.Count < 2)
            {
                DebugLog.Write(
                    $"LoadAnimatedAsync: {key} has no animation (frames<2). Staying static, cached as null"
                );
                _animatedImageCache.Set(key, null);
                return;
            }

            try
            {
                var bitmaps = frames
                    .Select(f =>
                        (
                            D2DBitmapLoader.CreateBitmap(
                                _target,
                                D2DBitmapLoader.Decode(f.Image),
                                key
                            ),
                            f.DelayMs
                        )
                    )
                    .ToList();

                _animatedImageCache.Set(key, bitmaps);

                if (_animatedImageCache.ContainsKey(key))
                {
                    _animationState[key] = (0, DateTime.UtcNow.AddMilliseconds(bitmaps[0].DelayMs));
                    EnsureAnimationTimerRunning();
                }

                RequestRender();
            }
            catch (Exception ex)
            {
                DebugLog.WriteException($"LoadAnimatedAsync ({key})", ex);
            }
        });
    }
}