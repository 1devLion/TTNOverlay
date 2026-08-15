using System.Numerics;
using TTNOverlay.Native;
using TTNOverlay.Services;
using Vortice.Direct2D1;
using Vortice.DirectWrite;
using Vortice.Mathematics;
using Rect = Vortice.Mathematics.Rect;

namespace TTNOverlay.Overlay;

/// <summary>
/// Native "what's new" dialog with a single close button, shown once after an update is applied.
/// </summary>
internal sealed class ReleaseNotesDialogWindow : OverlayWindowBase
{
    private const int FixedWidth = 520;
    private const float Padding = 20f;
    private const float TitleHeight = 22f;
    private const float TitleGap = 10f;
    private const float MessageGap = 20f;
    private const float ButtonHeight = 28f;
    private const float ButtonWidth = 100f;
    private const float MaxMessageHeight = 520f;

    protected override int ResizeGripSize => 0;
    protected override int TitleBarHeight => 0;
    protected override bool QuitApplicationOnDestroy => false;

    private readonly string _title;
    private readonly string _plainText;
    private readonly List<MarkdownLite.BoldSpan> _boldSpans;
    private readonly string _closeText;
    private readonly IntPtr _ownerHwnd;

    private int _computedHeight;
    protected override int MinimumClientWidth => FixedWidth;
    protected override int MinimumClientHeight => _computedHeight;

    private Rect _closeButtonRect;
    private float _hoverMouseX = -1f, _hoverMouseY = -1f;
    private float _messageHeight;
    private float _fullMessageHeight;

    private ScrollState _scroll;
    private const float ScrollStepPx = 40f;

    private ID2D1SolidColorBrush? _textBrush;
    private ID2D1SolidColorBrush? _secondaryBrush;
    private ID2D1SolidColorBrush? _windowBackgroundBrush;
    private ID2D1SolidColorBrush? _fieldBackgroundBrush;
    private ID2D1SolidColorBrush? _fieldBorderBrush;
    private ID2D1SolidColorBrush? _hoverShadowBrush;
    private bool? _lastKnownIsDark;
    private IDWriteTextFormat? _titleFormat;
    private IDWriteTextFormat? _messageFormat;
    private IDWriteTextFormat? _buttonFormat;

    protected override void OnMouseWheel(int delta, int clientX, int clientY)
    {
        float deltaPx = (delta / 120f) * ScrollStepPx;
        _scroll.ApplyWheel(deltaPx, invert: true);
        RequestRender();
    }

    public ReleaseNotesDialogWindow(string title, string message, string closeText, IntPtr ownerHwnd)
        : base("TTNOverlayReleaseNotesWndClass")
    {
        _title = title;
        (_plainText, _boldSpans) = MarkdownLite.Parse(message);
        _closeText = closeText;
        _ownerHwnd = ownerHwnd;
    }

    public static void Show(IntPtr ownerHwnd, Action<Action> postToOwnerUiThread, string title, string message, string closeText)
    {
        var wnd = new ReleaseNotesDialogWindow(title, message, closeText, ownerHwnd);
        wnd.Destroyed += () => postToOwnerUiThread(wnd.Dispose);
        wnd.Create(title, 100, 100, FixedWidth, 180);
    }

    protected override void OnCreated()
    {
        Win32.EnableWindow(_ownerHwnd, false);

        using var probeFormat = DWriteFactory.CreateTextFormat("Segoe UI", FontWeight.Normal, Vortice.DirectWrite.FontStyle.Normal, 13f);
        probeFormat.WordWrapping = Vortice.DirectWrite.WordWrapping.Wrap;
        float contentWidth = FixedWidth - Padding * 2f;
        using (var probe = DWriteFactory.CreateTextLayout(_plainText, probeFormat, contentWidth, float.MaxValue))
            _fullMessageHeight = probe.Metrics.Height;
        _messageHeight = Math.Min(_fullMessageHeight, MaxMessageHeight);

        int preferredHeight = (int)(Padding + TitleHeight + TitleGap + _messageHeight + MessageGap + ButtonHeight + Padding);

        Win32.GetSizeFittingScreen(_ownerHwnd, FixedWidth, preferredHeight, FixedWidth, 160, out int finalWidth, out int finalHeight, margin: 60);
        _computedHeight = finalHeight;

        Resize(finalWidth, finalHeight);

        if (Win32.TryGetCenteredPosition(_ownerHwnd, finalWidth, finalHeight, out int x, out int y))
            Win32.SetWindowPos(Hwnd, IntPtr.Zero, x, y, 0, 0, Win32.SWP_NOSIZE | Win32.SWP_NOZORDER | Win32.SWP_NOACTIVATE);

    }
    protected override void OnClientMouseMove(int x, int y)
    {
        _hoverMouseX = x;
        _hoverMouseY = y;
        RequestRender();
    }

    protected override void OnClientLButtonUp(int x, int y)
    {
        if (_closeButtonRect.Contains(x, y))
            Win32.PostMessage(Hwnd, Win32.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
    }

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
        _fieldBackgroundBrush ??= target.CreateSolidColorBrush(ThemeService.FieldBackground);
        _fieldBorderBrush ??= target.CreateSolidColorBrush(ThemeService.FieldBorder);
        _hoverShadowBrush ??= target.CreateSolidColorBrush(new Color4(1f, 1f, 1f, 0.06f));
        _titleFormat ??= DWriteFactory.CreateTextFormat("Segoe UI", FontWeight.Bold, Vortice.DirectWrite.FontStyle.Normal, 16f);
        _messageFormat ??= DWriteFactory.CreateTextFormat("Segoe UI", FontWeight.Normal, Vortice.DirectWrite.FontStyle.Normal, 13f);
        _buttonFormat ??= DWriteFactory.CreateTextFormat("Segoe UI", FontWeight.SemiBold, Vortice.DirectWrite.FontStyle.Normal, 13f);

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


        float visibleMessageHeight = _messageHeight;
        _scroll.RecomputeOverflow(_fullMessageHeight, visibleMessageHeight);

        target.PushAxisAlignedClip(new Rect(x, y, contentWidth, visibleMessageHeight), AntialiasMode.Aliased);

        using (var message = DWriteFactory.CreateTextLayout(_plainText, _messageFormat!, contentWidth, _messageHeight))
        {
            foreach (var span in _boldSpans)
            {
                var range = new Vortice.DirectWrite.TextRange((uint)span.Start, (uint)span.Length);
                message.SetFontWeight(FontWeight.Bold, range);
                if (span.IsHeader)
                    message.SetFontSize(15f, range);
            }
            target.DrawTextLayout(new Vector2(x, y - _scroll.Offset), message, _secondaryBrush!);
        }
        target.PopAxisAlignedClip();

        if (_scroll.Overflow > 0.5f)
        {
            using var hintFormat = DWriteFactory.CreateTextFormat("Segoe UI", FontWeight.Normal, Vortice.DirectWrite.FontStyle.Normal, 11f);
            using var hint = DWriteFactory.CreateTextLayout("▼ " + LocalizationService.T("Update_ScrollHint"), hintFormat, contentWidth, 16f);
            target.DrawTextLayout(new Vector2(x, y + visibleMessageHeight - 16f), hint, _secondaryBrush!);
        }
        y += _messageHeight + MessageGap;

        _closeButtonRect = new Rect(x + contentWidth - ButtonWidth, y, ButtonWidth, ButtonHeight);
        bool hoveringClose = _closeButtonRect.Contains(_hoverMouseX, _hoverMouseY);
        target.FillRectangle(_closeButtonRect, hoveringClose ? _hoverShadowBrush! : _fieldBackgroundBrush!);
        target.DrawRectangle(_closeButtonRect, _fieldBorderBrush!, 1f);
        using (var closeLabel = DWriteFactory.CreateTextLayout(_closeText, _buttonFormat!, ButtonWidth, ButtonHeight))
        {
            closeLabel.TextAlignment = Vortice.DirectWrite.TextAlignment.Center;
            closeLabel.ParagraphAlignment = ParagraphAlignment.Center;
            target.DrawTextLayout(new Vector2(_closeButtonRect.X, _closeButtonRect.Y), closeLabel, _textBrush!);
        }
    }

    private void DisposeThemedBrushes()
    {
        _textBrush?.Dispose(); _textBrush = null;
        _secondaryBrush?.Dispose(); _secondaryBrush = null;
        _windowBackgroundBrush?.Dispose(); _windowBackgroundBrush = null;
        _fieldBackgroundBrush?.Dispose(); _fieldBackgroundBrush = null;
        _fieldBorderBrush?.Dispose(); _fieldBorderBrush = null;
        _hoverShadowBrush?.Dispose(); _hoverShadowBrush = null;
    }

    protected override void OnDestroyed()
    {
        Win32.EnableWindow(_ownerHwnd, true);
        DisposeThemedBrushes();
        _titleFormat?.Dispose();
        _messageFormat?.Dispose();
        _buttonFormat?.Dispose();
    }
}