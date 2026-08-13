using System.Numerics;
using TTNOverlay.Native;
using TTNOverlay.Services;
using Vortice.Direct2D1;
using Vortice.DirectWrite;
using Vortice.Mathematics;
using Rect = Vortice.Mathematics.Rect;

namespace TTNOverlay.Overlay;

/// <summary>
/// Native yes/no confirmation dialog window used before destructive moderation actions.
/// </summary>
internal sealed class ConfirmDialogWindow : OverlayWindowBase
{
    private const int FixedWidth = 320;
    private const float Padding = 20f;
    private const float TitleHeight = 22f;
    private const float TitleGap = 10f;
    private const float MessageGap = 20f;
    private const float ButtonHeight = 28f;
    private const float ButtonWidth = 90f;

    protected override int ResizeGripSize => 0;
    protected override int TitleBarHeight => 0;
    protected override bool QuitApplicationOnDestroy => false;

    private readonly string _title;
    private readonly string _message;
    private readonly string _confirmText;
    private readonly IntPtr _ownerHwnd;
    private bool _confirmed;

    private int _computedHeight;
    protected override int MinimumClientWidth => FixedWidth;
    protected override int MinimumClientHeight => _computedHeight;

    public Action<bool>? ResultReady;

    private Rect _cancelButtonRect, _confirmButtonRect;
    private float _hoverMouseX = -1f, _hoverMouseY = -1f;
    private float _messageHeight;

    private ID2D1SolidColorBrush? _textBrush;
    private ID2D1SolidColorBrush? _secondaryBrush;
    private ID2D1SolidColorBrush? _windowBackgroundBrush;
    private ID2D1SolidColorBrush? _fieldBackgroundBrush;
    private ID2D1SolidColorBrush? _fieldBorderBrush;
    private ID2D1SolidColorBrush? _hoverShadowBrush;

    private ID2D1SolidColorBrush? _dangerBrush;
    private ID2D1SolidColorBrush? _dangerTextBrush;
    private bool? _lastKnownIsDark;
    private IDWriteTextFormat? _titleFormat;
    private IDWriteTextFormat? _messageFormat;
    private IDWriteTextFormat? _buttonFormat;

    public ConfirmDialogWindow(string title, string message, string? confirmText, IntPtr ownerHwnd) : base("TTNOverlayConfirmDialogWndClass")
    {
        _title = title;
        _message = message;
        _confirmText = confirmText ?? LocalizationService.T("Common_Confirm");
        _ownerHwnd = ownerHwnd;
    }

    public static void Show(
        IntPtr ownerHwnd,
        Action<Action> postToOwnerUiThread,
        string title,
        string message,
        string? confirmText,
        Action<bool> callback)
    {
        int x = 100, y = 100;
        if (Win32.GetWindowRect(ownerHwnd, out var ownerRect))
        {
            x = ownerRect.Left + ((ownerRect.Right - ownerRect.Left) - FixedWidth) / 2;
            y = ownerRect.Top + ((ownerRect.Bottom - ownerRect.Top) - 180) / 2;
        }

        var wnd = new ConfirmDialogWindow(title, message, confirmText, ownerHwnd);
        wnd.ResultReady += callback;
        wnd.Destroyed += () => postToOwnerUiThread(wnd.Dispose);
        wnd.Create(Strings.Get("WindowTitle_Confirm", LocalizationService.Instance.CurrentLanguage), x, y, FixedWidth, 180);
    }

    private void PositionOverOwner()
    {
        if (_ownerHwnd == IntPtr.Zero)
            return;

        if (!Win32.GetWindowRect(_ownerHwnd, out var ownerRect))
            return;

        if (!Win32.GetWindowRect(Hwnd, out var dialogRect))
            return;

        int width = dialogRect.Right - dialogRect.Left;
        int height = dialogRect.Bottom - dialogRect.Top;
        int left = ownerRect.Left + ((ownerRect.Right - ownerRect.Left) - width) / 2;
        int top = ownerRect.Top + ((ownerRect.Bottom - ownerRect.Top) - height) / 2;

        Win32.SetWindowPos(
            Hwnd,
            IntPtr.Zero,
            left,
            top,
            0,
            0,
            Win32.SWP_NOSIZE | Win32.SWP_NOZORDER | Win32.SWP_NOACTIVATE
        );
    }

    protected override void OnCreated()
    {
        Win32.EnableWindow(_ownerHwnd, false);

        using var probeFormat = DWriteFactory.CreateTextFormat("Segoe UI", FontWeight.Normal, Vortice.DirectWrite.FontStyle.Normal, 13f);
        probeFormat.WordWrapping = Vortice.DirectWrite.WordWrapping.Wrap;
        float messageWidth = FixedWidth - Padding * 2f;
        using (var probe = DWriteFactory.CreateTextLayout(_message, probeFormat, messageWidth, 500f))
            _messageHeight = probe.Metrics.Height;

        _computedHeight = (int)(Padding + TitleHeight + TitleGap + _messageHeight + MessageGap + ButtonHeight + Padding);
        Resize(FixedWidth, _computedHeight);
        PositionOverOwner();
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
        _hoverShadowBrush ??= target.CreateSolidColorBrush(new Color4(0f, 0f, 0f, 1f));
        _dangerBrush ??= target.CreateSolidColorBrush(new Color4(0xC0 / 255f, 0x39 / 255f, 0x2B / 255f, 1f));
        _dangerTextBrush ??= target.CreateSolidColorBrush(new Color4(1f, 1f, 1f, 1f));
        _titleFormat ??= DWriteFactory.CreateTextFormat("Segoe UI", FontWeight.Bold, Vortice.DirectWrite.FontStyle.Normal, 16f);
        if (_messageFormat is null)
        {
            _messageFormat = DWriteFactory.CreateTextFormat("Segoe UI", FontWeight.Normal, Vortice.DirectWrite.FontStyle.Normal, 13f);
            _messageFormat.WordWrapping = Vortice.DirectWrite.WordWrapping.Wrap;
        }
        if (_buttonFormat is null)
        {
            _buttonFormat = DWriteFactory.CreateTextFormat("Segoe UI", FontWeight.Normal, Vortice.DirectWrite.FontStyle.Normal, 13f);
            _buttonFormat.TextAlignment = Vortice.DirectWrite.TextAlignment.Center;
            _buttonFormat.ParagraphAlignment = ParagraphAlignment.Center;
        }

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

        using (var message = DWriteFactory.CreateTextLayout(_message, _messageFormat!, contentWidth, _messageHeight))
            target.DrawTextLayout(new Vector2(x, y), message, _secondaryBrush!);
        y += _messageHeight + MessageGap;

        _confirmButtonRect = new Rect(x + contentWidth - ButtonWidth, y, ButtonWidth, ButtonHeight);
        _cancelButtonRect = new Rect(_confirmButtonRect.Left - 10f - ButtonWidth, y, ButtonWidth, ButtonHeight);

        DrawHoverShadow(target, _cancelButtonRect);
        target.FillRectangle(_cancelButtonRect, _fieldBackgroundBrush!);
        target.DrawRectangle(_cancelButtonRect, _fieldBorderBrush!, 1f);
        using (var cancelLabel = DWriteFactory.CreateTextLayout(LocalizationService.T("Common_Cancel"), _buttonFormat!, ButtonWidth, ButtonHeight))
            target.DrawTextLayout(new Vector2(_cancelButtonRect.Left, _cancelButtonRect.Top), cancelLabel, _textBrush!);

        DrawHoverShadow(target, _confirmButtonRect);
        target.FillRectangle(_confirmButtonRect, _dangerBrush!);
        using (var confirmLabel = DWriteFactory.CreateTextLayout(_confirmText, _buttonFormat!, ButtonWidth, ButtonHeight))
            target.DrawTextLayout(new Vector2(_confirmButtonRect.Left, _confirmButtonRect.Top), confirmLabel, _dangerTextBrush!);
    }

    private void DrawHoverShadow(ID2D1DCRenderTarget target, Rect rect)
    {
        if (!Contains(rect, _hoverMouseX, _hoverMouseY))
            return;
        Span<float> offsets = stackalloc float[] { 3f, 2f, 1f };
        Span<float> opacities = stackalloc float[] { 0.05f, 0.09f, 0.14f };
        for (int i = 0; i < offsets.Length; i++)
        {
            var shadowRect = new Rect(rect.Left + offsets[i], rect.Top + offsets[i], rect.Width, rect.Height);
            _hoverShadowBrush!.Opacity = opacities[i];
            target.FillRectangle(shadowRect, _hoverShadowBrush);
        }
        _hoverShadowBrush!.Opacity = 1f;
    }

    protected override void OnClientLButtonUp(int clientX, int clientY)
    {
        if (Contains(_cancelButtonRect, clientX, clientY))
        {
            _confirmed = false;
            Win32.PostMessage(Hwnd, Win32.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
            return;
        }
        if (Contains(_confirmButtonRect, clientX, clientY))
        {
            _confirmed = true;
            Win32.PostMessage(Hwnd, Win32.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
        }
    }

    protected override void OnClientMouseMove(int clientX, int clientY)
    {
        _hoverMouseX = clientX;
        _hoverMouseY = clientY;
        RequestRender();
    }

    protected override void OnClientMouseLeave()
    {
        _hoverMouseX = -1f;
        _hoverMouseY = -1f;
        RequestRender();
    }

    private void DisposeThemedBrushes()
    {
        _textBrush?.Dispose(); _textBrush = null;
        _secondaryBrush?.Dispose(); _secondaryBrush = null;
        _windowBackgroundBrush?.Dispose(); _windowBackgroundBrush = null;
        _fieldBackgroundBrush?.Dispose(); _fieldBackgroundBrush = null;
        _fieldBorderBrush?.Dispose(); _fieldBorderBrush = null;
    }

    private static bool Contains(Rect rect, float x, float y) =>
        x >= rect.Left && x <= rect.Right && y >= rect.Top && y <= rect.Bottom;

    protected override void OnDestroyed()
    {
        Win32.EnableWindow(_ownerHwnd, true);

        DisposeThemedBrushes();
        _hoverShadowBrush?.Dispose();
        _dangerBrush?.Dispose();
        _dangerTextBrush?.Dispose();
        _titleFormat?.Dispose();
        _messageFormat?.Dispose();
        _buttonFormat?.Dispose();

        ResultReady?.Invoke(_confirmed);
    }
}