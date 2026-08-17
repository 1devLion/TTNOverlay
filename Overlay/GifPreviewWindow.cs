using TTNOverlay.Native;
using TTNOverlay.Services;
using Vortice.Direct2D1;
using Rect = Vortice.Mathematics.Rect;

namespace TTNOverlay.Overlay;

/// <summary>
/// A native window that displays a local image or animated GIF file, scaled to fit with aspect preservation.
/// </summary>
internal sealed class GifPreviewWindow : OverlayWindowBase
{
    protected override int ResizeGripSize => 16;
    protected override int TitleBarHeight => 0;
    protected override bool QuitApplicationOnDestroy => false;

    private readonly string _path;
    private readonly IntPtr _ownerHwnd;

    private List<RawAnimatedFrame>? _pendingAnimatedFrames;
    private D2DBitmapLoader.DecodedImage? _pendingStaticImage;
    private List<(ID2D1Bitmap Bitmap, int DelayMs)>? _frameBitmaps;
    private ID2D1Bitmap? _staticBitmap;
    private int _frameIndex;
    private DateTime _nextFrameDueUtc;
    private System.Threading.Timer? _animationTimer;

    private ID2D1SolidColorBrush? _backgroundBrush;
    private bool? _lastKnownIsDark;

    private static int _instanceCounter;

    private static GifPreviewWindow? _current;

    public GifPreviewWindow(string path, IntPtr ownerHwnd)
        : base($"TTNOverlayGifPreviewWndClass_{System.Threading.Interlocked.Increment(ref _instanceCounter)}")
    {
        _path = path;
        _ownerHwnd = ownerHwnd;
        DebugLog.Write($"GifPreviewWindow: ctor path={path}");
    }

    private const int DefaultSize = 260;

    /// <summary>
    /// Creates and shows a GIF preview window for the specified image file.
    /// </summary>
    /// <param name="ownerHwnd">The owner window handle.</param>
    /// <param name="postToOwnerUiThread">A delegate to post actions to the owner's UI thread.</param>
    /// <param name="path">The path to the image file.</param>
    public static void Show(IntPtr ownerHwnd, Action<Action> postToOwnerUiThread, string path)
    {
        DebugLog.Write($"GifPreviewWindow.Show: path={path}");

        if (_current is { } previous)
        {
            DebugLog.Write("GifPreviewWindow.Show: closing previous preview before opening new one");
            _current = null;
            Win32.DestroyWindow(previous.Hwnd);
        }

        int x = 150, y = 150;
        if (Win32.GetWindowRect(ownerHwnd, out var ownerRect))
        {
            x = ownerRect.Left + ((ownerRect.Right - ownerRect.Left) - DefaultSize) / 2;
            y = ownerRect.Top + ((ownerRect.Bottom - ownerRect.Top) - DefaultSize) / 2;
        }

        var wnd = new GifPreviewWindow(path, ownerHwnd);
        _current = wnd;
        wnd.Destroyed += wnd.NotifyClosedIfCurrent;
        wnd.Destroyed += () => { DebugLog.Write("GifPreviewWindow: Destroyed event"); postToOwnerUiThread(wnd.Dispose); };
        wnd.Create(System.IO.Path.GetFileName(path), x, y, DefaultSize, DefaultSize);
        DebugLog.Write("GifPreviewWindow.Show: Create() returned OK");
    }

    private void NotifyClosedIfCurrent()
    {
        if (_current == this)
            _current = null;
    }

    protected override void OnCreated()
    {
        DebugLog.Write("GifPreviewWindow.OnCreated");
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            DebugLog.Write($"GifPreviewWindow.LoadAsync: reading {_path}");
            var bytes = await LocalImageLoader.ReadBytesAsync(_path);
            if (bytes is null)
            {
                DebugLog.Write("GifPreviewWindow.LoadAsync: ReadBytesAsync returned null, aborting");
                return;
            }
            DebugLog.Write($"GifPreviewWindow.LoadAsync: {bytes.Length} bytes read, decoding animated");

            var animated = LocalImageLoader.TryDecodeAnimated(bytes);
            if (animated is { Count: >= 2 })
            {
                DebugLog.Write($"GifPreviewWindow.LoadAsync: {animated.Count} animated frames decoded");
                PostToUiThread(() => { _pendingAnimatedFrames = animated; RequestRender(); });
                return;
            }

            DebugLog.Write("GifPreviewWindow.LoadAsync: not animated, decoding static");
            var staticImage = LocalImageLoader.TryDecodeStatic(bytes);
            if (staticImage is not null)
            {
                DebugLog.Write($"GifPreviewWindow.LoadAsync: static decoded {staticImage.Value.Width}x{staticImage.Value.Height}");
                PostToUiThread(() => { _pendingStaticImage = staticImage; RequestRender(); });
            }
            else
            {
                DebugLog.Write("GifPreviewWindow.LoadAsync: static decode returned null, nothing to display");
            }
        }
        catch (Exception ex)
        {
            DebugLog.WriteException("GifPreviewWindow.LoadAsync", ex);
        }
    }

    protected override void OnDeviceResourcesInvalidated()
    {
        DebugLog.Write("GifPreviewWindow.OnDeviceResourcesInvalidated: render target recreated - invalidating old bitmaps");
        _backgroundBrush?.Dispose();
        _backgroundBrush = null;

        if (_frameBitmaps is not null)
        {
            foreach (var f in _frameBitmaps)
                f.Bitmap.Dispose();
            _frameBitmaps = null;
        }
        _staticBitmap?.Dispose();
        _staticBitmap = null;

        if (_lastAnimatedFrames is { } frames)
            _pendingAnimatedFrames = frames;
        else if (_lastStaticImage is { } img)
            _pendingStaticImage = img;

        RequestRender();
    }

    private List<RawAnimatedFrame>? _lastAnimatedFrames;
    private D2DBitmapLoader.DecodedImage? _lastStaticImage;

    protected override void OnRender(ID2D1DCRenderTarget target)
    {
        if (_lastKnownIsDark != ThemeService.IsDark)
        {
            _lastKnownIsDark = ThemeService.IsDark;
            _backgroundBrush?.Dispose();
            _backgroundBrush = null;
        }
        _backgroundBrush ??= target.CreateSolidColorBrush(ThemeService.WindowBackground);

        if (_pendingAnimatedFrames is { } animFrames)
        {
            DebugLog.Write($"GifPreviewWindow.OnRender: uploading {animFrames.Count} frames to GPU");
            _pendingAnimatedFrames = null;
            _lastAnimatedFrames = animFrames;
            try
            {
                _frameBitmaps = new List<(ID2D1Bitmap, int)>(animFrames.Count);
                for (int i = 0; i < animFrames.Count; i++)
                {
                    var f = animFrames[i];
                    var bmp = D2DBitmapLoader.CreateBitmap(target, D2DBitmapLoader.Decode(f.Image));
                    _frameBitmaps.Add((bmp, f.DelayMs));
                }
                DebugLog.Write("GifPreviewWindow.OnRender: frames uploaded OK");
            }
            catch (Exception ex)
            {
                DebugLog.WriteException("GifPreviewWindow.OnRender (uploading animated frames)", ex);
                _frameBitmaps = null;
            }

            if (_frameBitmaps is { Count: > 0 })
            {
                _frameIndex = 0;
                _nextFrameDueUtc = DateTime.UtcNow.AddMilliseconds(_frameBitmaps[0].DelayMs);
                EnsureAnimationTimerRunning();
            }
        }
        else if (_pendingStaticImage is { } img)
        {
            DebugLog.Write($"GifPreviewWindow.OnRender: uploading static bitmap {img.Width}x{img.Height}");
            _pendingStaticImage = null;
            _lastStaticImage = img;
            try
            {
                _staticBitmap = D2DBitmapLoader.CreateBitmap(target, img);
                DebugLog.Write("GifPreviewWindow.OnRender: static bitmap uploaded OK");
            }
            catch (Exception ex)
            {
                DebugLog.WriteException("GifPreviewWindow.OnRender (uploading static bitmap)", ex);
                _staticBitmap = null;
            }

        }

        Win32.GetClientRect(Hwnd, out var client);
        float width = client.Right - client.Left;
        float height = client.Bottom - client.Top;
        if (width <= 0 || height <= 0)
            return;

        target.FillRectangle(new Rect(0f, 0f, width, height), _backgroundBrush!);

        var bitmap = _frameBitmaps is { Count: > 0 } ? _frameBitmaps[_frameIndex].Bitmap : _staticBitmap;
        if (bitmap is not null)
        {
            var dest = GetAspectFitRect(bitmap.Size.Width, bitmap.Size.Height, width, height);
            target.DrawBitmap(bitmap, dest, 1f, BitmapInterpolationMode.Linear, new Rect(0, 0, bitmap.Size.Width, bitmap.Size.Height));
        }
    }

    /// <summary>
    /// Calculates the destination rectangle that fits a source image within the available area
    /// while preserving aspect ratio, centered.
    /// </summary>
    private static Rect GetAspectFitRect(float srcWidth, float srcHeight, float availWidth, float availHeight)
    {
        if (srcWidth <= 0 || srcHeight <= 0 || availWidth <= 0 || availHeight <= 0)
            return new Rect(0f, 0f, availWidth, availHeight);

        float scale = System.Math.Min(availWidth / srcWidth, availHeight / srcHeight);
        float w = srcWidth * scale;
        float h = srcHeight * scale;
        float x = (availWidth - w) / 2f;
        float y = (availHeight - h) / 2f;
        return new Rect(x, y, w, h);
    }

    private void EnsureAnimationTimerRunning()
    {
        _animationTimer ??= new System.Threading.Timer(_ => PostToUiThread(AdvanceFrame), null, 33, 33);
    }

    private void AdvanceFrame()
    {
        if (_frameBitmaps is not { Count: > 1 } || DateTime.UtcNow < _nextFrameDueUtc)
            return;
        _frameIndex = (_frameIndex + 1) % _frameBitmaps.Count;
        _nextFrameDueUtc = DateTime.UtcNow.AddMilliseconds(_frameBitmaps[_frameIndex].DelayMs);
        RequestRender();
    }

    protected override void OnClientLButtonUp(int clientX, int clientY) =>
        Win32.PostMessage(Hwnd, Win32.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);

    protected override void OnDestroyed()
    {
        _animationTimer?.Dispose();
        if (_frameBitmaps is not null)
            foreach (var f in _frameBitmaps)
                f.Bitmap.Dispose();
        _staticBitmap?.Dispose();
        _backgroundBrush?.Dispose();
    }
}