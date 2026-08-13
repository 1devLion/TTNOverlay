using System.Numerics;
using TTNOverlay.Native;
using TTNOverlay.Overlay.Controls;
using TTNOverlay.Services;
using Vortice.Direct2D1;
using Vortice.DirectWrite;
using Vortice.Mathematics;
using Rect = Vortice.Mathematics.Rect;

namespace TTNOverlay.Overlay;

/// <summary>
/// Native color picker dialog window (hue/saturation/value + alpha) used by the settings and moderation UI.
/// </summary>
internal sealed class ColorPickerWindow : OverlayWindowBase
{
    internal const int WindowWidth = 320;
    internal const int WindowHeight = 500;

    protected override int ResizeGripSize => 0;
    protected override int MinimumClientWidth => WindowWidth;
    protected override int MinimumClientHeight => WindowHeight;
    protected override int TitleBarHeight => 34;
    protected override bool QuitApplicationOnDestroy => false;

    private const float Padding = 20f;
    private const float SliderLabelWidth = 20f;
    private const float SliderValueWidth = 34f;
    private const float SliderRowHeight = 26f;
    private const float SliderRowGap = 8f;
    private const float PreviewHeight = 48f;
    private const float PresetSize = 26f;
    private const float PresetGap = 6f;
    private const float FooterButtonWidth = 90f;
    private const float FooterButtonHeight = 28f;
    private const float FieldHeight = 26f;
    private const int CaretBlinkIntervalMs = 530;

    private static readonly (string Hex, Color4 Color)[] Presets =
    {
        ("#9147FF", new Color4(0x91 / 255f, 0x47 / 255f, 1f, 1f)),
        ("#FFD700", new Color4(1f, 0xD7 / 255f, 0f, 1f)),
        ("#FF7A00", new Color4(1f, 0x7A / 255f, 0f, 1f)),
        ("#00B24F", new Color4(0f, 0xB2 / 255f, 0x4F / 255f, 1f)),
        ("#0082FF", new Color4(0f, 0x82 / 255f, 1f, 1f)),
        ("#E91E63", new Color4(0xE9 / 255f, 0x1E / 255f, 0x63 / 255f, 1f)),
    };

    public Action<(string Hex, byte Alpha)?>? ResultReady;

    private readonly Slider _rSlider = new() { Minimum = 0, Maximum = 255 };
    private readonly Slider _gSlider = new() { Minimum = 0, Maximum = 255 };
    private readonly Slider _bSlider = new() { Minimum = 0, Maximum = 255 };
    private readonly Slider _opacitySlider = new() { Minimum = 0, Maximum = 100 };
    private readonly TextBox _hexBox = new() { MaxLength = 7 };

    private bool _confirmed;

    private Rect _previewRect;
    private Rect _rSliderRect, _gSliderRect, _bSliderRect, _opacitySliderRect;
    private Rect _hexFieldRect;
    private readonly List<Rect> _presetRects = new();
    private Rect _cancelButtonRect, _acceptButtonRect;

    private float _hoverMouseX = -1f, _hoverMouseY = -1f;
    private TextBox? _focusedTextBox;
    private System.Threading.Timer? _caretBlinkTimer;

    private ID2D1SolidColorBrush? _textBrush;
    private ID2D1SolidColorBrush? _secondaryBrush;
    private ID2D1SolidColorBrush? _windowBackgroundBrush;
    private ID2D1SolidColorBrush? _fieldBackgroundBrush;
    private ID2D1SolidColorBrush? _fieldBorderBrush;
    private ID2D1SolidColorBrush? _hoverShadowBrush;
    private ID2D1SolidColorBrush? _caretBrush;
    private ID2D1SolidColorBrush? _selectionBrush;
    private ID2D1SolidColorBrush? _previewBrush;
    private bool? _lastKnownIsDark;
    private IDWriteTextFormat? _titleFormat;
    private IDWriteTextFormat? _labelFormat;
    private IDWriteTextFormat? _fieldFormat;
    private IDWriteTextFormat? _buttonFormat;

    private readonly IntPtr _ownerHwnd;

    private ColorPickerWindow(string initialHex, byte initialAlpha, IntPtr ownerHwnd) : base("TTNOverlayColorPickerWndClass")
    {
        _ownerHwnd = ownerHwnd;
        _opacitySlider.SetValue((float)Math.Round(initialAlpha / 255.0 * 100));
        SetFromHex(initialHex);
    }

    public static void Show(IntPtr ownerHwnd, Action<Action> postToOwnerUiThread, string initialHex, byte initialAlpha, Action<(string Hex, byte Alpha)?> callback)
    {
        int x = 100, y = 100;
        if (Win32.GetWindowRect(ownerHwnd, out var ownerRect))
        {
            x = ownerRect.Left + (ownerRect.Right - ownerRect.Left - WindowWidth) / 2;
            y = ownerRect.Top + (ownerRect.Bottom - ownerRect.Top - WindowHeight) / 2;
        }

        var wnd = new ColorPickerWindow(initialHex, initialAlpha, ownerHwnd);
        wnd.ResultReady += callback;

        wnd.Destroyed += () => postToOwnerUiThread(wnd.Dispose);
        wnd.Create(Strings.Get("WindowTitle_SetColor", LocalizationService.Instance.CurrentLanguage), x, y, WindowWidth, WindowHeight);
    }

    public static bool TryParseHex(string hex, out byte r, out byte g, out byte b)
    {
        r = g = b = 0;
        var text = hex?.Trim().TrimStart('#') ?? "";
        if (text.Length != 6)
            return false;
        try
        {
            r = Convert.ToByte(text.Substring(0, 2), 16);
            g = Convert.ToByte(text.Substring(2, 2), 16);
            b = Convert.ToByte(text.Substring(4, 2), 16);
            return true;
        }
        catch
        {
            return false;
        }
    }

    protected override void OnCreated()
    {
        Win32.EnableWindow(_ownerHwnd, false);
        _caretBlinkTimer = new System.Threading.Timer(_ => PostToUiThread(TickCaretBlink), null, CaretBlinkIntervalMs, CaretBlinkIntervalMs);

        _rSlider.ValueChanged += _ => SyncHexFromSliders();
        _gSlider.ValueChanged += _ => SyncHexFromSliders();
        _bSlider.ValueChanged += _ => SyncHexFromSliders();
        _opacitySlider.ValueChanged += _ => RequestRender();
    }

    private void TickCaretBlink()
    {
        if (_focusedTextBox is { } tb && tb.TickBlink())
            RequestRender();
    }

    private void SetFromHex(string hex)
    {
        if (!TryParseHex(hex, out var r, out var g, out var b))
            return;
        _rSlider.SetValue(r);
        _gSlider.SetValue(g);
        _bSlider.SetValue(b);
        SyncHexFromSliders();
    }

    private void SyncHexFromSliders()
    {
        _hexBox.Text = $"#{(byte)_rSlider.Value:X2}{(byte)_gSlider.Value:X2}{(byte)_bSlider.Value:X2}";
    }

    private byte CurrentAlpha() => (byte)Math.Round(_opacitySlider.Value / 100.0 * 255);

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
        _caretBrush ??= target.CreateSolidColorBrush(_textBrush!.Color);
        _hoverShadowBrush ??= target.CreateSolidColorBrush(new Color4(0f, 0f, 0f, 1f));
        _selectionBrush ??= target.CreateSolidColorBrush(new Color4(0.30f, 0.62f, 0.98f, 0.35f));
        _titleFormat ??= DWriteFactory.CreateTextFormat("Segoe UI", FontWeight.Bold, Vortice.DirectWrite.FontStyle.Normal, 16f);
        _labelFormat ??= DWriteFactory.CreateTextFormat("Segoe UI", FontWeight.Normal, Vortice.DirectWrite.FontStyle.Normal, 13f);
        _fieldFormat ??= DWriteFactory.CreateTextFormat("Segoe UI", FontWeight.Normal, Vortice.DirectWrite.FontStyle.Normal, 13f);
        _fieldFormat.ParagraphAlignment = ParagraphAlignment.Center;
        _fieldFormat.WordWrapping = Vortice.DirectWrite.WordWrapping.NoWrap;
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
        DrawTitleBar(target, width);

        float x = Padding;
        float contentWidth = width - Padding * 2f;
        float y = TitleBarHeight + Padding;

        _previewRect = new Rect(x, y, contentWidth, PreviewHeight);
        target.DrawRectangle(_previewRect, _fieldBorderBrush!, 1f);
        var previewColor = new Color4((byte)_rSlider.Value / 255f, (byte)_gSlider.Value / 255f, (byte)_bSlider.Value / 255f, CurrentAlpha() / 255f);
        _previewBrush ??= target.CreateSolidColorBrush(previewColor);
        _previewBrush.Color = previewColor;
        target.FillRectangle(new Rect(_previewRect.Left + 1f, _previewRect.Top + 1f, _previewRect.Width - 2f, _previewRect.Height - 2f), _previewBrush);
        y += PreviewHeight + 14f;

        y = DrawSliderRow(target, x, contentWidth, y, "R", _rSlider, out _rSliderRect, v => { SyncHexFromSliders(); });
        y = DrawSliderRow(target, x, contentWidth, y, "G", _gSlider, out _gSliderRect, v => { SyncHexFromSliders(); });
        y = DrawSliderRow(target, x, contentWidth, y, "B", _bSlider, out _bSliderRect, v => { SyncHexFromSliders(); });
        y += 8f;
        y = DrawSliderRow(target, x, contentWidth, y, LocalizationService.T("ColorPicker_Opacity"), _opacitySlider, out _opacitySliderRect, null, valueSuffix: "%", labelWidth: 60f);
        y += 6f;

        using (var hexLabel = DWriteFactory.CreateTextLayout(LocalizationService.T("ColorPicker_Hex"), _labelFormat!, contentWidth, 18f))
            target.DrawTextLayout(new Vector2(x, y), hexLabel, _textBrush!);
        y += 18f + 4f;
        DrawHexField(target, x, y, out _hexFieldRect);
        y += FieldHeight + 14f;

        using (var presetLabel = DWriteFactory.CreateTextLayout(LocalizationService.T("ColorPicker_Presets"), _labelFormat!, contentWidth, 14f))
            target.DrawTextLayout(new Vector2(x, y), presetLabel, _secondaryBrush!);
        y += 16f;
        _presetRects.Clear();
        float presetX = x;
        for (int i = 0; i < Presets.Length; i++)
        {
            var rect = new Rect(presetX, y, PresetSize, PresetSize);
            _presetRects.Add(rect);
            using var presetBrush = target.CreateSolidColorBrush(Presets[i].Color);
            target.FillRectangle(rect, presetBrush);
            presetX += PresetSize + PresetGap;
        }
        y += PresetSize + 20f;

        _acceptButtonRect = new Rect(x + contentWidth - FooterButtonWidth, y, FooterButtonWidth, FooterButtonHeight);
        _cancelButtonRect = new Rect(_acceptButtonRect.Left - 10f - FooterButtonWidth, y, FooterButtonWidth, FooterButtonHeight);
        DrawButton(target, _cancelButtonRect, LocalizationService.T("Common_Cancel"), primary: false);
        DrawButton(target, _acceptButtonRect, LocalizationService.T("Common_Accept"), primary: true);
    }

    private float DrawSliderRow(ID2D1DCRenderTarget target, float x, float width, float y, string label, Slider slider, out Rect trackRect, Action<float>? onChanged, string valueSuffix = "", float labelWidth = SliderLabelWidth)
    {
        using (var labelLayout = DWriteFactory.CreateTextLayout(label, _labelFormat!, labelWidth, SliderRowHeight))
            target.DrawTextLayout(new Vector2(x, y + (SliderRowHeight - 14f) / 2f), labelLayout, _textBrush!);

        float trackWidth = width - labelWidth - SliderValueWidth - 12f;
        trackRect = new Rect(x + labelWidth, y + (SliderRowHeight - 4f) / 2f, trackWidth, 4f);

        target.FillRectangle(trackRect, _fieldBackgroundBrush!);
        target.DrawRectangle(trackRect, _fieldBorderBrush!, 1f);

        float handleX = trackRect.Left + slider.NormalizedPosition * trackRect.Width;
        var handleRect = new Rect(handleX - 6f, trackRect.Top - 6f + 2f, 12f, 12f);
        target.FillEllipse(new Ellipse(new Vector2(handleX, trackRect.Top + 2f), 6f, 6f), _textBrush!);

        string valueText = valueSuffix == "%" ? $"{slider.Value:0}%" : $"{slider.Value:0}";
        using (var valueLayout = DWriteFactory.CreateTextLayout(valueText, _labelFormat!, SliderValueWidth, SliderRowHeight))
            target.DrawTextLayout(new Vector2(x + labelWidth + trackWidth + 12f, y + (SliderRowHeight - 14f) / 2f), valueLayout, _secondaryBrush!);

        return y + SliderRowHeight + SliderRowGap;
    }

    private void DrawHexField(ID2D1DCRenderTarget target, float x, float y, out Rect fieldRect)
    {
        fieldRect = new Rect(x, y, 100f, FieldHeight);
        DrawHoverShadow(target, fieldRect);
        target.FillRectangle(fieldRect, _fieldBackgroundBrush!);
        var borderBrush = _hexBox == _focusedTextBox ? _fieldBorderBrush! : _fieldBorderBrush!;
        target.DrawRectangle(fieldRect, borderBrush, _hexBox == _focusedTextBox ? 1.5f : 1f);

        target.PushAxisAlignedClip(fieldRect, AntialiasMode.PerPrimitive);
        using (var textLayout = DWriteFactory.CreateTextLayout(_hexBox.Text, _fieldFormat!, fieldRect.Width - 16f, fieldRect.Height))
            target.DrawTextLayout(new Vector2(fieldRect.Left + 8f, fieldRect.Top), textLayout, _textBrush!);

        if (_hexBox == _focusedTextBox && _hexBox.CaretVisibleThisFrame && !_hexBox.HasSelection)
        {
            using var caretLayout = DWriteFactory.CreateTextLayout(_hexBox.Text.Substring(0, Math.Min(_hexBox.CaretIndex, _hexBox.Text.Length)), _fieldFormat!, 2000f, FieldHeight);
            float caretX = fieldRect.Left + 8f + caretLayout.Metrics.WidthIncludingTrailingWhitespace;
            target.DrawLine(new Vector2(caretX, fieldRect.Top + 5f), new Vector2(caretX, fieldRect.Bottom - 5f), _caretBrush!, 1.5f);
        }
        target.PopAxisAlignedClip();
    }

    private void DrawButton(ID2D1DCRenderTarget target, Rect rect, string label, bool primary)
    {
        DrawHoverShadow(target, rect);
        target.FillRectangle(rect, primary ? _textBrush! : _fieldBackgroundBrush!);
        if (!primary)
            target.DrawRectangle(rect, _fieldBorderBrush!, 1f);
        using var layout = DWriteFactory.CreateTextLayout(label, _buttonFormat!, rect.Width, rect.Height);
        target.DrawTextLayout(new Vector2(rect.Left, rect.Top), layout, primary ? _windowBackgroundBrush! : _textBrush!);
    }

    private void DrawTitleBar(ID2D1DCRenderTarget target, float width)
    {
        target.DrawLine(new Vector2(0f, TitleBarHeight), new Vector2(width, TitleBarHeight), _fieldBorderBrush!);
        using var title = DWriteFactory.CreateTextLayout(LocalizationService.T("ColorPicker_Title"), _titleFormat!, width - Padding, TitleBarHeight);
        target.DrawTextLayout(new Vector2(Padding, (TitleBarHeight - 16f) / 2f), title, _textBrush!);
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

    protected override void OnClientLButtonDown(int clientX, int clientY)
    {
        bool shift = (Win32.GetKeyState(Win32.VK_SHIFT) & 0x8000) != 0;

        if (Contains(_rSliderRect, clientX, clientY)) { _rSlider.HandleLButtonDown(_rSliderRect, clientX, this); RequestRender(); return; }
        if (Contains(_gSliderRect, clientX, clientY)) { _gSlider.HandleLButtonDown(_gSliderRect, clientX, this); RequestRender(); return; }
        if (Contains(_bSliderRect, clientX, clientY)) { _bSlider.HandleLButtonDown(_bSliderRect, clientX, this); RequestRender(); return; }
        if (Contains(_opacitySliderRect, clientX, clientY)) { _opacitySlider.HandleLButtonDown(_opacitySliderRect, clientX, this); RequestRender(); return; }

        if (Contains(_hexFieldRect, clientX, clientY))
        {
            if (_focusedTextBox is not null && _focusedTextBox != _hexBox)
                _focusedTextBox.Blur();
            _focusedTextBox = _hexBox;
            _hexBox.HandleClick(DWriteFactory, _fieldFormat!, new Rect(_hexFieldRect.Left + 8f, 0, 10000f, FieldHeight), clientX, shift, this);
            RequestRender();
            return;
        }

        BlurFocusedTextBox();
    }

    protected override void OnClientLButtonUp(int clientX, int clientY)
    {

        bool wasDraggingSlider = _rSlider.HandleLButtonUp() | _gSlider.HandleLButtonUp() | _bSlider.HandleLButtonUp() | _opacitySlider.HandleLButtonUp();
        bool wasDraggingText = _focusedTextBox?.HandleLButtonUp() ?? false;
        if (wasDraggingSlider || wasDraggingText)
            return;

        if (Contains(_cancelButtonRect, clientX, clientY))
        {
            _confirmed = false;
            Win32.PostMessage(Hwnd, Win32.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
            return;
        }
        if (Contains(_acceptButtonRect, clientX, clientY))
        {
            _confirmed = true;
            Win32.PostMessage(Hwnd, Win32.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
            return;
        }
        for (int i = 0; i < _presetRects.Count; i++)
        {
            if (Contains(_presetRects[i], clientX, clientY))
            {
                SetFromHex(Presets[i].Hex);
                RequestRender();
                return;
            }
        }
    }

    protected override void OnClientMouseMove(int clientX, int clientY)
    {
        _hoverMouseX = clientX;
        _hoverMouseY = clientY;
        RequestRender();

        _rSlider.HandleMouseMove(_rSliderRect, clientX);
        _gSlider.HandleMouseMove(_gSliderRect, clientX);
        _bSlider.HandleMouseMove(_bSliderRect, clientX);
        _opacitySlider.HandleMouseMove(_opacitySliderRect, clientX);

        if (_focusedTextBox is null)
            return;
        _focusedTextBox.HandleMouseMoveDrag(DWriteFactory, _fieldFormat!, new Rect(_hexFieldRect.Left + 8f, 0, 10000f, FieldHeight), clientX);
    }

    protected override void OnClientMouseLeave()
    {
        _hoverMouseX = -1f;
        _hoverMouseY = -1f;
        RequestRender();
    }

    private void BlurFocusedTextBox()
    {
        if (_focusedTextBox is null)
            return;
        _focusedTextBox.Blur();
        _focusedTextBox = null;
        RequestRender();
    }

    protected override void OnKeyDown(int virtualKeyCode, bool ctrlDown, bool shiftDown)
    {
        if (virtualKeyCode == Win32.VK_RETURN && _focusedTextBox == _hexBox)
        {
            SetFromHex(_hexBox.Text);
            RequestRender();
            return;
        }
        if (_focusedTextBox is null)
            return;
        _focusedTextBox.HandleKeyDown(virtualKeyCode, ctrlDown, shiftDown);
        RequestRender();
    }

    protected override void OnChar(char c)
    {
        _focusedTextBox?.HandleChar(c);
        RequestRender();
    }

    protected override void OnWindowFocusLost() => BlurFocusedTextBox();

    private void DisposeThemedBrushes()
    {
        _textBrush?.Dispose(); _textBrush = null;
        _secondaryBrush?.Dispose(); _secondaryBrush = null;
        _windowBackgroundBrush?.Dispose(); _windowBackgroundBrush = null;
        _fieldBackgroundBrush?.Dispose(); _fieldBackgroundBrush = null;
        _fieldBorderBrush?.Dispose(); _fieldBorderBrush = null;
        _caretBrush?.Dispose(); _caretBrush = null;
    }

    private static bool Contains(Rect rect, float x, float y) =>
        x >= rect.Left && x <= rect.Right && y >= rect.Top && y <= rect.Bottom;

    protected override void OnDestroyed()
    {
        Win32.EnableWindow(_ownerHwnd, true);

        _caretBlinkTimer?.Dispose();
        _caretBlinkTimer = null;

        DisposeThemedBrushes();
        _hoverShadowBrush?.Dispose();
        _selectionBrush?.Dispose();
        _previewBrush?.Dispose();
        _titleFormat?.Dispose();
        _labelFormat?.Dispose();
        _fieldFormat?.Dispose();
        _buttonFormat?.Dispose();

        var hex = $"#{(byte)_rSlider.Value:X2}{(byte)_gSlider.Value:X2}{(byte)_bSlider.Value:X2}";
        ResultReady?.Invoke(_confirmed ? (hex, CurrentAlpha()) : null);
    }
}