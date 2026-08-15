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

    private readonly Dictionary<string, ID2D1Bitmap?> _imageCache = new();
    private readonly HashSet<string> _imageLoadInFlight = new();

    private readonly Dictionary<string, List<(ID2D1Bitmap Bitmap, int DelayMs)>?> _animatedImageCache = new();

    private readonly HashSet<string> _animatedLoadInFlight = new();
    private readonly Dictionary<string, (int Index, DateTime NextDueUtc)> _animationState = new();
    private System.Threading.Timer? _animationTimer;

    private const long MaxCachedImageBytes = 20L * 1024 * 1024;
    private const int MaxCachedAnimatedFrames = 2000;
    private readonly LinkedList<string> _imageCacheOrder = new();
    private readonly LinkedList<string> _animatedImageCacheOrder = new();

    private const int MaxCachedUsernameBrushes = 128;
    private readonly Dictionary<string, ID2D1SolidColorBrush> _usernameBrushCache = new();
    private readonly LinkedList<string> _usernameBrushCacheOrder = new();

    /// <summary>
    /// Running totals kept in sync with _imageCache / _animatedImageCache on every insert and evict.
    /// </summary>
    private long _imageCacheBytes;
    private int _animatedFrameCount;

    private void TouchImageCache(string key)
    {
        _imageCacheOrder.Remove(key);
        _imageCacheOrder.AddLast(key);

        while (_imageCacheBytes > MaxCachedImageBytes && _imageCacheOrder.Count > 0)
        {
            var oldest = _imageCacheOrder.First!.Value;
            _imageCacheOrder.RemoveFirst();
            if (_imageCache.TryGetValue(oldest, out var bmp))
            {
                if (bmp is not null)
                    _imageCacheBytes -= (long)(bmp.Size.Width * bmp.Size.Height * 4);
                bmp?.Dispose();
                _imageCache.Remove(oldest);
                DebugLog.Write($"TouchImageCache: evict {oldest} (cache > {MaxCachedImageBytes / 1024 / 1024}MB)");
            }
        }
    }

    private void TouchAnimatedCache(string key)
    {
        _animatedImageCacheOrder.Remove(key);
        _animatedImageCacheOrder.AddLast(key);

        while (_animatedFrameCount > MaxCachedAnimatedFrames && _animatedImageCacheOrder.Count > 0)
        {
            var oldest = _animatedImageCacheOrder.First!.Value;
            _animatedImageCacheOrder.RemoveFirst();
            if (_animatedImageCache.TryGetValue(oldest, out var frames))
            {
                if (frames is not null)
                {
                    foreach (var f in frames)
                        f.Bitmap.Dispose();
                    _animatedFrameCount -= frames.Count;
                }
                _animatedImageCache.Remove(oldest);
                _animationState.Remove(oldest);
                DebugLog.Write($"TouchAnimatedCache: evict {oldest} ({frames?.Count ?? 0} frames, total > {MaxCachedAnimatedFrames})");
            }
        }
    }

    private ID2D1Bitmap? GetOrLoadImageBitmap(string key, string url, int targetSize)
    {
        if (_imageCache.TryGetValue(key, out var cached))
            return cached;

        if (!_imageLoadInFlight.Add(key))
            return null;

        DebugLog.Write($"GetOrLoadImageBitmap: disparando carga nueva de {key} -- {url}");
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
                _imageCache[key] = null;
                TouchImageCache(key);
                _loggedWaitingBadges.Remove(key);
                DebugLog.Write($"LoadImageAsync: {key} cached as null (decode failed)");
                RequestRender();
                return;
            }

            if (_target is null)
            {
                DebugLog.Write($"LoadImageAsync: {key} -- _target still null, not caching, will retry");
                return;
            }

            try
            {
                var bitmap = D2DBitmapLoader.CreateBitmap(_target, decoded.Value, key);
                _imageCache[key] = bitmap;
                _imageCacheBytes += (long)(bitmap.Size.Width * bitmap.Size.Height * 4);
                TouchImageCache(key);
                _loggedWaitingBadges.Remove(key);
                DebugLog.Write($"LoadImageAsync: {key} inserted into cache OK");
            }
            catch (Exception ex)
            {
                DebugLog.WriteException($"LoadImageAsync ({key})", ex);
                _imageCache[key] = null;
                TouchImageCache(key);
                _loggedWaitingBadges.Remove(key);
            }

            RequestRender();
        });
    }

    private ID2D1SolidColorBrush GetUsernameBrush(ID2D1DCRenderTarget target, string hex)
    {
        if (_usernameBrushCache.TryGetValue(hex, out var cached))
            return cached;

        var brush = target.CreateSolidColorBrush(ParseHexColor(hex));
        _usernameBrushCache[hex] = brush;
        TouchUsernameBrushCache(hex);
        return brush;
    }

    private void TouchUsernameBrushCache(string hex)
    {
        _usernameBrushCacheOrder.Remove(hex);
        _usernameBrushCacheOrder.AddLast(hex);
        while (_usernameBrushCacheOrder.Count > MaxCachedUsernameBrushes)
        {
            var oldest = _usernameBrushCacheOrder.First!.Value;
            _usernameBrushCacheOrder.RemoveFirst();
            if (_usernameBrushCache.TryGetValue(oldest, out var brush))
            {
                brush.Dispose();
                _usernameBrushCache.Remove(oldest);
            }
        }
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
        foreach (var bitmap in _imageCache.Values)
            bitmap?.Dispose();
        _imageCache.Clear();
        _imageCacheOrder.Clear();
        _imageLoadInFlight.Clear();
        _imageCacheBytes = 0;

        foreach (var frames in _animatedImageCache.Values)
            if (frames is not null)
                foreach (var f in frames)
                    f.Bitmap.Dispose();
        _animatedImageCache.Clear();
        _animatedImageCacheOrder.Clear();
        _animatedLoadInFlight.Clear();
        _animationState.Clear();
        _animatedFrameCount = 0;

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

        foreach (var brush in _usernameBrushCache.Values)
            brush.Dispose();
        _usernameBrushCache.Clear();

        foreach (var bitmap in _imageCache.Values)
            bitmap?.Dispose();
        _imageCache.Clear();
        _imageCacheBytes = 0;

        foreach (var frames in _animatedImageCache.Values)
            if (frames is not null)
                foreach (var f in frames)
                    f.Bitmap.Dispose();
        _animatedImageCache.Clear();
        _animationState.Clear();
        _animatedFrameCount = 0;
    }

    private void PurgeEventIconCaches()
    {
        const string prefix = "eventicon:";

        foreach (var key in _imageCache.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).ToList())
        {
            if (_imageCache[key] is { } bmp)
                _imageCacheBytes -= (long)(bmp.Size.Width * bmp.Size.Height * 4);
            _imageCache[key]?.Dispose();
            _imageCache.Remove(key);
            _imageCacheOrder.Remove(key);
        }
        _imageLoadInFlight.RemoveWhere(k => k.StartsWith(prefix, StringComparison.Ordinal));

        foreach (var key in _animatedImageCache.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).ToList())
        {
            if (_animatedImageCache[key] is { } frames)
            {
                foreach (var f in frames)
                    f.Bitmap.Dispose();
                _animatedFrameCount -= frames.Count;
            }
            _animatedImageCache.Remove(key);
            _animatedImageCacheOrder.Remove(key);
            _animationState.Remove(key);
        }
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
                DebugLog.Write($"LoadAnimatedAsync: {key} -- _target still null, not caching");
                return;
            }

            if (frames is null || frames.Count < 2)
            {
                DebugLog.Write(
                    $"LoadAnimatedAsync: {key} has no animation (frames<2) -- staying static, cached as null"
                );
                _animatedImageCache[key] = null;
                TouchAnimatedCache(key);
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

                _animatedImageCache[key] = bitmaps;
                _animatedFrameCount += bitmaps.Count;
                TouchAnimatedCache(key);

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