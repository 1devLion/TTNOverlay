using System.Numerics;
using TTNOverlay.Native;
using TTNOverlay.Services;
using Vortice.Direct2D1;
using Vortice.DirectWrite;
using Vortice.Mathematics;
using Rect = Vortice.Mathematics.Rect;

namespace TTNOverlay.Overlay;

/// <summary>
/// Native dialog showing update-download progress: bar, percent, MB downloaded/remaining and speed.
/// Has no buttons; the caller closes it via <see cref="Close"/> once the download finishes or fails.
/// </summary>
internal sealed class UpdateProgressDialogWindow : OverlayWindowBase
{
    private const int FixedWidth = 320;
    private const int FixedHeight = 130;
    private const float Padding = 20f;
    private const float TitleHeight = 22f;
    private const float TitleGap = 12f;
    private const float BarHeight = 14f;
    private const float BarGap = 10f;

    protected override int ResizeGripSize => 0;
    protected override int TitleBarHeight => 0;
    protected override bool QuitApplicationOnDestroy => false;
    protected override int MinimumClientWidth => FixedWidth;
    protected override int MinimumClientHeight => FixedHeight;

    private readonly string _title;
    private readonly IntPtr _ownerHwnd;

    private int _percent;
    private long _totalBytes;
    private double _speedBytesPerSecond;
    private long _lastBytes;
    private DateTime _lastSampleUtc = DateTime.UtcNow;

    private ID2D1SolidColorBrush? _textBrush;
    private ID2D1SolidColorBrush? _secondaryBrush;
    private ID2D1SolidColorBrush? _windowBackgroundBrush;
    private ID2D1SolidColorBrush? _fieldBorderBrush;
    private ID2D1SolidColorBrush? _barTrackBrush;
    private ID2D1SolidColorBrush? _barFillBrush;
    private bool? _lastKnownIsDark;
    private IDWriteTextFormat? _titleFormat;
    private IDWriteTextFormat? _detailFormat;

    private UpdateProgressDialogWindow(string title, IntPtr ownerHwnd) : base("TTNOverlayUpdateProgressWndClass")
    {
        _title = title;
        _ownerHwnd = ownerHwnd;
    }

    public static UpdateProgressDialogWindow Show(IntPtr ownerHwnd, Action<Action> postToOwnerUiThread, string title)
    {
        // Centered on screen rather than over the (possibly tiny) overlay rect, same reasoning as
        // ConfirmDialogWindow.
        Win32.TryGetCenteredPosition(ownerHwnd, FixedWidth, FixedHeight, out int x, out int y);

        var wnd = new UpdateProgressDialogWindow(title, ownerHwnd);
        wnd.Destroyed += () => postToOwnerUiThread(wnd.Dispose);
        wnd.Create(title, x, y, FixedWidth, FixedHeight);
        return wnd;
    }

    /// <summary>Call from the download progress callback. Safe to call from any thread.</summary>
    public void ReportProgress(int percent, long totalBytes)
    {
        PostToUiThread(() =>
        {
            var now = DateTime.UtcNow;
            long downloadedBytes = totalBytes <= 0 ? 0 : totalBytes * percent / 100;
            double elapsedSeconds = (now - _lastSampleUtc).TotalSeconds;
            if (elapsedSeconds > 0.15)
            {
                _speedBytesPerSecond = Math.Max(0, downloadedBytes - _lastBytes) / elapsedSeconds;
                _lastBytes = downloadedBytes;
                _lastSampleUtc = now;
            }

            _percent = Math.Clamp(percent, 0, 100);
            _totalBytes = totalBytes;
            RequestRender();
        });
    }

    /// <summary>Closes the dialog (e.g. once the download finished or failed). Safe from any thread.</summary>
    public void Close() => PostToUiThread(() => Win32.PostMessage(Hwnd, Win32.WM_CLOSE, IntPtr.Zero, IntPtr.Zero));

    protected override void OnCreated() => Win32.EnableWindow(_ownerHwnd, false);

    protected override void OnRender(ID2D1DCRenderTarget target)
    {
        if (_lastKnownIsDark != ThemeService.IsDark)
        {
            _lastKnownIsDark = ThemeService.IsDark;
            DisposeThemedBrushes();
        }

        _textBrush ??= target.CreateSolidColorBrush(ThemeService.WindowText);
        _secondaryBrush ??= target.CreateSolidColorBrush(ThemeService.WindowTextSecondary);
        _windowBackgroundBrush ??= target.CreateSolidColorBrush(ThemeService.WindowBackground);
        _fieldBorderBrush ??= target.CreateSolidColorBrush(ThemeService.FieldBorder);
        _barTrackBrush ??= target.CreateSolidColorBrush(ThemeService.FieldBackground);
        _barFillBrush ??= target.CreateSolidColorBrush(new Color4(0x2E / 255f, 0x8B / 255f, 0x57 / 255f, 1f));
        _titleFormat ??= DWriteFactory.CreateTextFormat("Segoe UI", FontWeight.Bold, Vortice.DirectWrite.FontStyle.Normal, 16f);
        _detailFormat ??= DWriteFactory.CreateTextFormat("Segoe UI", FontWeight.Normal, Vortice.DirectWrite.FontStyle.Normal, 13f);

        Win32.GetClientRect(Hwnd, out var client);
        float width = client.Right - client.Left;
        float height = client.Bottom - client.Top;
        if (width <= 0 || height <= 0)
            return;

        target.FillRectangle(new Rect(0f, 0f, width, height), _windowBackgroundBrush);
        target.DrawRectangle(new Rect(0f, 0f, width, height), _fieldBorderBrush!, 1f);

        float x = Padding;
        float contentWidth = width - Padding * 2f;
        float y = Padding;

        using (var title = DWriteFactory.CreateTextLayout(_title, _titleFormat!, contentWidth, TitleHeight))
            target.DrawTextLayout(new Vector2(x, y), title, _textBrush!);
        y += TitleHeight + TitleGap;

        var trackRect = new Rect(x, y, contentWidth, BarHeight);
        target.FillRectangle(trackRect, _barTrackBrush!);
        target.DrawRectangle(trackRect, _fieldBorderBrush!, 1f);
        float fillWidth = contentWidth * (_percent / 100f);
        if (fillWidth > 0.5f)
            target.FillRectangle(new Rect(x, y, fillWidth, BarHeight), _barFillBrush!);
        y += BarHeight + BarGap;

        double downloadedMb = _totalBytes <= 0 ? 0 : _totalBytes * _percent / 100.0 / 1024.0 / 1024.0;
        double totalMb = _totalBytes / 1024.0 / 1024.0;
        double remainingMb = Math.Max(0, totalMb - downloadedMb);
        double speedMb = _speedBytesPerSecond / 1024.0 / 1024.0;

        string sizeLine = _totalBytes > 0
            ? $"{_percent}%  \u2022  {downloadedMb:F1} MB / {totalMb:F1} MB"
            : $"{_percent}%";
        using (var sizeLayout = DWriteFactory.CreateTextLayout(sizeLine, _detailFormat!, contentWidth, 18f))
            target.DrawTextLayout(new Vector2(x, y), sizeLayout, _textBrush!);
        y += 18f;

        string speedLine = _totalBytes > 0
            ? $"{speedMb:F1} MB/s  \u2022  {LocalizationService.T("Update_RemainingLabel")} {remainingMb:F1} MB"
            : "";
        using (var speedLayout = DWriteFactory.CreateTextLayout(speedLine, _detailFormat!, contentWidth, 18f))
            target.DrawTextLayout(new Vector2(x, y), speedLayout, _secondaryBrush!);
    }

    private void DisposeThemedBrushes()
    {
        _textBrush?.Dispose(); _textBrush = null;
        _secondaryBrush?.Dispose(); _secondaryBrush = null;
        _windowBackgroundBrush?.Dispose(); _windowBackgroundBrush = null;
        _fieldBorderBrush?.Dispose(); _fieldBorderBrush = null;
        _barTrackBrush?.Dispose(); _barTrackBrush = null;
        _barFillBrush?.Dispose(); _barFillBrush = null;
    }

    protected override void OnDestroyed()
    {
        Win32.EnableWindow(_ownerHwnd, true);
        DisposeThemedBrushes();
        _titleFormat?.Dispose();
        _detailFormat?.Dispose();
    }
}