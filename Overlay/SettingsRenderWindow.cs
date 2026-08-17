using TTNOverlay.Native;
using TTNOverlay.Overlay.Controls;
using TTNOverlay.Services;
using Vortice.Direct2D1;
using Vortice.DirectWrite;
using Vortice.Mathematics;
using Rect = Vortice.Mathematics.Rect;

namespace TTNOverlay.Overlay;

/// <summary>
/// Settings window (native, layered): core fields, section switching, and window lifecycle. Section content lives in the SettingsRenderWindow.*.cs partials in the Settings/ folder.
/// </summary>
internal sealed partial class SettingsRenderWindow : OverlayWindowBase
{

    /// <summary>Ideal size on a large-enough screen. Actual creation size is clamped to fit the monitor's work area. See Win32.GetSizeFittingScreen.</summary>
    internal const int PreferredWidth = 1024;
    internal const int PreferredHeight = 768;

    /// <summary>Absolute floor: below this the sidebar/content layout stops making sense.</summary>
    internal const int MinWindowWidth = 760;
    internal const int MinWindowHeight = 540;

    protected override int ResizeGripSize => 0;
    protected override int MinimumClientWidth => MinWindowWidth;
    protected override int MinimumClientHeight => MinWindowHeight;

    private const float Padding = 16f;
    private const float SidebarWidth = 170f;
    private const float SidebarRowHeight = 40f;
    private const float FieldGap = 10f;
    private const float LabelGap = 2f;
    private const float FieldHeight = 30f;
    private const float CheckboxSize = 16f;
    private const float CheckboxLabelGap = 8f;
    private const int CaretBlinkIntervalMs = 530;

    private const float CloseButtonWidth = 40f;

    private const float FooterHeight = 44f;
    private const float FooterButtonWidth = 84f;
    private const float FooterButtonHeight = 28f;
    private const float FooterButtonGap = 10f;

    private static readonly string[] SectionLabelKeys =
    {
        "Settings_Section_General",
        "Settings_Section_Hotkeys",
        "Settings_Section_TwitchApi",
        "Settings_Section_Streamlabs",
        "Settings_Section_Alerts",
        "Settings_Section_Audio",
        "Settings_Section_ViewerCount",
        "Settings_Section_About",
    };

    /// <summary>Sidebar index of the Viewer Count section. Kept second-to-last, directly above About.</summary>
    private const int ViewerCountSectionIndex = 6;

    /// <summary>Sidebar index of the About section. Always the last row. Keep this in sync with
    /// SectionLabelKeys above if any other section is ever appended.</summary>
    private const int AboutSectionIndex = 7;

    public AppSettings Settings { get; }

    private int _selectedSection;

    private bool? _lastKnownIsDark;

    private readonly List<Rect> _sidebarRowRects = new();

    private Rect _saveButtonRect;
    private Rect _cancelButtonRect;

    private float _hoverMouseX = -1f;
    private float _hoverMouseY = -1f;

    private TextBox? _focusedTextBox;
    private System.Threading.Timer? _caretBlinkTimer;

    private ID2D1SolidColorBrush? _textBrush;
    private ID2D1SolidColorBrush? _windowBackgroundBrush;
    private ID2D1SolidColorBrush? _secondaryBrush;
    private ID2D1SolidColorBrush? _fieldBackgroundBrush;
    private ID2D1SolidColorBrush? _fieldBorderBrush;
    private ID2D1SolidColorBrush? _sidebarSelectedBrush;
    private ID2D1SolidColorBrush? _checkboxBrush;
    private ID2D1SolidColorBrush? _caretBrush;

    private ID2D1SolidColorBrush? _selectionBrush;
    private ID2D1SolidColorBrush? _hoverShadowBrush;

    private ID2D1SolidColorBrush? _scrollbarTrackBrush;
    private ID2D1SolidColorBrush? _scrollbarThumbBrush;

    private ID2D1SolidColorBrush? _windowBackgroundBrushInverse;
    private IDWriteTextFormat? _headerFormat;
    private IDWriteTextFormat? _labelFormat;
    private IDWriteTextFormat? _fieldFormat;
    private IDWriteTextFormat? _buttonFormat;
    private IDWriteTextFormat? _titleBarFormat;
    private IDWriteTextFormat? _sidebarFormat;

    protected override bool QuitApplicationOnDestroy => true;

    protected override int TitleBarHeight => 34;

    protected override bool IsInDraggableTitleBarArea(int clientX, int clientY)
    {
        Win32.GetClientRect(Hwnd, out var client);
        float width = client.Right - client.Left;
        return !Contains(GetCloseButtonRect(width), clientX, clientY);
    }

    private Rect GetCloseButtonRect(float width) =>
        new(width - CloseButtonWidth, 0f, CloseButtonWidth, TitleBarHeight);

    public SettingsRenderWindow(AppSettings settings) : base("TTNOverlaySettingsWndClass")
    {
        Settings = settings;
        _moderation = new ModerationService(settings);
    }

    protected override void OnCreated()
    {
        _themeDropdown.Width = 160f;
        _languageDropdown.Width = 160f;
        // 160f (shared with Theme/Language) was too narrow once "Multichat (Twitch + Kick)" existed
        // It fits in English but overflows the box/dropdown-item text in longer languages (Russian,
        // Portuguese, Japanese full-width text). 260f matches the other dropdowns here that also carry
        // longer entries (_eventAlertSourceDropdown, _audioDeviceDropdown).
        _chatSourceDropdown.Width = 260f;
        _eventAlertSourceDropdown.Width = 260f;
        _audioDeviceDropdown.Width = 260f;
        _messageSoundPresetDropdown.Width = 220f;
        _eventSoundPresetDropdown.Width = 220f;

        InitGeneral();
        InitHotkeys();
        InitTwitchApi();
        InitViewerCount();
        InitStreamlabs();
        InitAudio();
        InitAlerts();

        _caretBlinkTimer = new System.Threading.Timer(_ => PostToUiThread(TickCaretBlink), null, CaretBlinkIntervalMs, CaretBlinkIntervalMs);
    }

    private void TickCaretBlink()
    {
        if (_focusedTextBox is { } tb && tb.TickBlink())
            RequestRender();
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
        _fieldBackgroundBrush ??= target.CreateSolidColorBrush(ThemeService.FieldBackground);
        _fieldBorderBrush ??= target.CreateSolidColorBrush(ThemeService.FieldBorder);
        _sidebarSelectedBrush ??= target.CreateSolidColorBrush(ThemeService.SubtleHoverFill);
        _checkboxBrush ??= target.CreateSolidColorBrush(new Color4(0.30f, 0.62f, 0.98f, 1f));
        _windowBackgroundBrush ??= target.CreateSolidColorBrush(ThemeService.WindowBackground);
        _caretBrush ??= target.CreateSolidColorBrush(_textBrush!.Color);
        _selectionBrush ??= target.CreateSolidColorBrush(new Color4(0.30f, 0.62f, 0.98f, 0.35f));

        _hoverShadowBrush ??= target.CreateSolidColorBrush(new Color4(0f, 0f, 0f, 1f));
        _scrollbarTrackBrush ??= target.CreateSolidColorBrush(ThemeService.ScrollbarTrack);
        _scrollbarThumbBrush ??= target.CreateSolidColorBrush(ThemeService.ScrollbarThumb);
        _headerFormat ??= DWriteFactory.CreateTextFormat("Segoe UI", FontWeight.Bold, Vortice.DirectWrite.FontStyle.Normal, 18f);
        _labelFormat ??= DWriteFactory.CreateTextFormat("Segoe UI", FontWeight.Normal, Vortice.DirectWrite.FontStyle.Normal, 15f);
        _fieldFormat ??= DWriteFactory.CreateTextFormat("Segoe UI", FontWeight.Normal, Vortice.DirectWrite.FontStyle.Normal, 16f);
        _fieldFormat.ParagraphAlignment = ParagraphAlignment.Center;

        _fieldFormat.WordWrapping = Vortice.DirectWrite.WordWrapping.NoWrap;
        if (_buttonFormat is null)
        {
            _buttonFormat = DWriteFactory.CreateTextFormat("Segoe UI", FontWeight.Normal, Vortice.DirectWrite.FontStyle.Normal, 13f);
            _buttonFormat.TextAlignment = Vortice.DirectWrite.TextAlignment.Center;
            _buttonFormat.ParagraphAlignment = ParagraphAlignment.Center;
            _buttonFormat.WordWrapping = Vortice.DirectWrite.WordWrapping.NoWrap;
        }

        _titleBarFormat ??= DWriteFactory.CreateTextFormat("Segoe UI", FontWeight.SemiBold, Vortice.DirectWrite.FontStyle.Normal, 18f);
        _sidebarFormat ??= DWriteFactory.CreateTextFormat("Segoe UI", FontWeight.Normal, Vortice.DirectWrite.FontStyle.Normal, 17f);

        Win32.GetClientRect(Hwnd, out var client);
        float width = client.Right - client.Left;
        float height = client.Bottom - client.Top;
        if (width <= 0 || height <= 0)
            return;

        target.FillRectangle(new Rect(0f, 0f, width, height), _windowBackgroundBrush);

        DrawTitleBar(target, width);
        DrawSidebar(target, height);

        float contentX = SidebarWidth + Padding;
        float contentWidth = width - contentX - Padding;
        if (contentWidth <= 0)
            return;

        if (_selectedSection == 0)
            DrawGeneralSection(target, contentX, contentWidth, height);
        else if (_selectedSection == 1)
            DrawHotkeysSection(target, contentX, contentWidth);
        else if (_selectedSection == 2)
            DrawTwitchApiSection(target, contentX, contentWidth);
        else if (_selectedSection == 3)
            DrawStreamlabsSection(target, contentX, contentWidth);
        else if (_selectedSection == 4)
            DrawAlertsSection(target, contentX, contentWidth, height);
        else if (_selectedSection == 5)
            DrawAudioSection(target, contentX, contentWidth);
        else if (_selectedSection == ViewerCountSectionIndex)
            DrawViewerCountSection(target, contentX, contentWidth);
        else if (_selectedSection == AboutSectionIndex)
            DrawAboutSection(target, contentX, contentWidth, height);

        DrawFooter(target, width, height);

        _themeDropdown.Draw(target, DWriteFactory, _textBrush);
        _languageDropdown.Draw(target, DWriteFactory, _textBrush);
        _chatSourceDropdown.Draw(target, DWriteFactory, _textBrush);
        _eventAlertSourceDropdown.Draw(target, DWriteFactory, _textBrush);
        _audioDeviceDropdown.Draw(target, DWriteFactory, _textBrush);
        _messageSoundPresetDropdown.Draw(target, DWriteFactory, _textBrush);
        _eventSoundPresetDropdown.Draw(target, DWriteFactory, _textBrush);
        _eventColorModeDropdown.Draw(target, DWriteFactory, _textBrush);
        _viewerCountModeDropdown.Draw(target, DWriteFactory, _textBrush);
    }

    private void DrawTitleBar(ID2D1DCRenderTarget target, float width)
    {
        target.DrawLine(
            new System.Numerics.Vector2(0f, TitleBarHeight),
            new System.Numerics.Vector2(width, TitleBarHeight),
            _fieldBorderBrush!
        );

        using (var title = DWriteFactory.CreateTextLayout(LocalizationService.T("Settings_WindowTitle"), _titleBarFormat!, width - CloseButtonWidth - Padding, TitleBarHeight))
            target.DrawTextLayout(new System.Numerics.Vector2(Padding, (TitleBarHeight - 18f) / 2f), title, _textBrush!);

        var closeRect = GetCloseButtonRect(width);
        using var closeLayout = DWriteFactory.CreateTextLayout("\u2715", _buttonFormat!, closeRect.Width, closeRect.Height);
        target.DrawTextLayout(new System.Numerics.Vector2(closeRect.Left, closeRect.Top), closeLayout, _secondaryBrush!);
    }

    private void DrawFooter(ID2D1DCRenderTarget target, float width, float height)
    {
        float footerTop = height - FooterHeight;
        target.DrawLine(
            new System.Numerics.Vector2(SidebarWidth, footerTop),
            new System.Numerics.Vector2(width, footerTop),
            _fieldBorderBrush!
        );

        float buttonY = footerTop + (FooterHeight - FooterButtonHeight) / 2f;
        _saveButtonRect = new Rect(width - Padding - FooterButtonWidth, buttonY, FooterButtonWidth, FooterButtonHeight);
        _cancelButtonRect = new Rect(_saveButtonRect.Left - FooterButtonGap - FooterButtonWidth, buttonY, FooterButtonWidth, FooterButtonHeight);

        DrawFooterButton(target, _cancelButtonRect, LocalizationService.T("Common_Cancel"), primary: false);
        DrawFooterButton(target, _saveButtonRect, LocalizationService.T("Common_Save"), primary: true);
    }

    private void DrawFooterButton(ID2D1DCRenderTarget target, Rect rect, string label, bool primary, bool enabled = true)
    {

        if (enabled)
            DrawHoverShadow(target, rect);
        target.FillRectangle(rect, !enabled ? _fieldBackgroundBrush! : primary ? _checkboxBrush! : _fieldBackgroundBrush!);
        if (!primary || !enabled)
            target.DrawRectangle(rect, _fieldBorderBrush!, 1f);

        using var layout = DWriteFactory.CreateTextLayout(label, _buttonFormat!, rect.Width, rect.Height);

        var textBrush = !enabled ? _secondaryBrush! : primary ? _windowBackgroundBrushInverse ??= target.CreateSolidColorBrush(new Color4(1f, 1f, 1f, 1f)) : _textBrush!;

        target.PushAxisAlignedClip(rect, AntialiasMode.PerPrimitive);
        target.DrawTextLayout(new System.Numerics.Vector2(rect.Left, rect.Top), layout, textBrush);
        target.PopAxisAlignedClip();
    }

    /// <summary>
    /// Sizes a button rect to fit its label.
    /// </summary>
    private Rect MeasureButtonRect(string label, float x, float y, float height, float minWidth, float horizontalPadding = 16f)
    {
        const float measureLayoutWidth = 4000f;
        using var layout = DWriteFactory.CreateTextLayout(label, _buttonFormat!, measureLayoutWidth, height);
        float textWidth = (float)layout.Metrics.WidthIncludingTrailingWhitespace;
        float measuredWidth = textWidth * 1.3f + horizontalPadding * 2f + 6f;
        return new Rect(x, y, System.Math.Max(minWidth, measuredWidth), height);
    }


    private void DrawSidebar(ID2D1DCRenderTarget target, float height)
    {
        _sidebarRowRects.Clear();
        float y = TitleBarHeight + Padding;
        for (int i = 0; i < SectionLabelKeys.Length; i++)
        {
            // Row height is measured per-label instead of using the fixed SidebarRowHeight: verbose
            // languages (e.g. Spanish "Contador de espectadores") can wrap to two lines in the narrow
            // sidebar column, and a fixed 40px row let the second line spill below the highlight/hover
            // rect into the next row. Rows that fit on one line keep the original 40px height exactly.
            using var layout = DWriteFactory.CreateTextLayout(LocalizationService.T(SectionLabelKeys[i]), _sidebarFormat!, SidebarWidth - Padding, 1000f);
            float rowHeight = System.Math.Max(SidebarRowHeight, layout.Metrics.Height + LabelGap * 2f);

            var rowRect = new Rect(0f, y, SidebarWidth, rowHeight);
            _sidebarRowRects.Add(rowRect);

            if (i == _selectedSection)
                target.FillRectangle(rowRect, _sidebarSelectedBrush!);

            target.DrawTextLayout(new System.Numerics.Vector2(Padding, y + (rowHeight - layout.Metrics.Height) / 2f), layout, i == _selectedSection ? _textBrush! : _secondaryBrush!);

            y += rowHeight;
        }

        target.DrawLine(
            new System.Numerics.Vector2(SidebarWidth, TitleBarHeight),
            new System.Numerics.Vector2(SidebarWidth, height),
            _fieldBorderBrush!
        );
    }

    private const float DropdownArrowWidth = 24f;
    private const float DropdownArrowGap = 6f;

    private float DrawDropdownField(ID2D1DCRenderTarget target, float x, float width, ref float y, string labelKey, Dropdown dropdown, string currentValue, out Rect fieldRect)
    {
        using (var label = DWriteFactory.CreateTextLayout(LocalizationService.T(labelKey), _labelFormat!, width, 18f))
            target.DrawTextLayout(new System.Numerics.Vector2(x, y), label, _secondaryBrush!);
        y += 18f + LabelGap;

        fieldRect = new Rect(x, y, System.Math.Min(width, dropdown.Width), FieldHeight);
        DrawHoverShadow(target, fieldRect);
        target.FillRectangle(fieldRect, _fieldBackgroundBrush!);
        target.DrawRectangle(fieldRect, _fieldBorderBrush!, 1f);
        var textClipRect = new Rect(fieldRect.Left + 8f, fieldRect.Top, fieldRect.Width - 12f - DropdownArrowGap - DropdownArrowWidth, fieldRect.Height);
        target.PushAxisAlignedClip(textClipRect, AntialiasMode.PerPrimitive);
        using (var valueLayout = DWriteFactory.CreateTextLayout(currentValue, _fieldFormat!, textClipRect.Width, fieldRect.Height))
            target.DrawTextLayout(new System.Numerics.Vector2(textClipRect.Left, textClipRect.Top), valueLayout, _textBrush!);
        target.PopAxisAlignedClip();

        using (var arrow = DWriteFactory.CreateTextLayout("\u25BE", _fieldFormat!, DropdownArrowWidth, fieldRect.Height))
            target.DrawTextLayout(new System.Numerics.Vector2(fieldRect.Right - DropdownArrowWidth, fieldRect.Top), arrow, _secondaryBrush!);

        return y + FieldHeight + FieldGap;
    }

    private const float RevealButtonWidth = 28f;

    private float DrawTextField(ID2D1DCRenderTarget target, float x, float width, float y, string labelKey, TextBox box, out Rect fieldRect, out Rect revealButtonRect, string? infoKey = null, bool passwordReveal = false, bool enabled = true, string? belowInfoKey = null, float? fieldWidth = null)
    {
        using (var label = DWriteFactory.CreateTextLayout(LocalizationService.T(labelKey), _labelFormat!, width, 18f))
            target.DrawTextLayout(new System.Numerics.Vector2(x, y), label, _secondaryBrush!);
        y += 18f + LabelGap;

        if (infoKey is not null)
        {
            using var info = DWriteFactory.CreateTextLayout(LocalizationService.T(infoKey), _labelFormat!, width, 28f);
            target.DrawTextLayout(new System.Numerics.Vector2(x, y), info, _secondaryBrush!);
            y += 28f;
        }

        float boxWidth = fieldWidth ?? width;
        float textWidth = passwordReveal ? boxWidth - RevealButtonWidth : boxWidth;
        fieldRect = new Rect(x, y, textWidth, FieldHeight);
        var outerRect = new Rect(x, y, boxWidth, FieldHeight);
        revealButtonRect = passwordReveal ? new Rect(x + textWidth, y, RevealButtonWidth, FieldHeight) : default;

        var borderBrush = !enabled ? _fieldBorderBrush! : box == _focusedTextBox ? _checkboxBrush! : _fieldBorderBrush!;
        var borderWidth = enabled && box == _focusedTextBox ? 1.5f : 1f;
        if (enabled)
            DrawHoverShadow(target, outerRect);
        target.FillRectangle(outerRect, _fieldBackgroundBrush!);
        target.DrawRectangle(outerRect, borderBrush, borderWidth);
        if (passwordReveal)
        {
            target.DrawLine(
                new System.Numerics.Vector2(fieldRect.Right, y + 4f),
                new System.Numerics.Vector2(fieldRect.Right, y + FieldHeight - 4f),
                _fieldBorderBrush!,
                1f
            );
            DrawPasswordRevealButton(target, revealButtonRect, box.RevealPassword, enabled);
        }

        string display;

        float scrollOffset = ComputeTextScrollOffset(box, fieldRect, out float innerLeft, out display);
        float caretX = MeasureTextWidth(display.Substring(0, System.Math.Min(box.CaretIndex, display.Length)));

        target.PushAxisAlignedClip(fieldRect, AntialiasMode.PerPrimitive);

        if (enabled && box.HasSelection)
        {
            float selStartX = innerLeft + MeasureTextWidth(display.Substring(0, box.SelectionStart)) - scrollOffset;
            float selEndX = innerLeft + MeasureTextWidth(display.Substring(0, box.SelectionEnd)) - scrollOffset;
            target.FillRectangle(new Rect(selStartX, fieldRect.Top + 3f, selEndX - selStartX, fieldRect.Height - 6f), _selectionBrush!);
        }

        using (var textLayout = DWriteFactory.CreateTextLayout(display, _fieldFormat!, 4000f, fieldRect.Height))
            target.DrawTextLayout(new System.Numerics.Vector2(innerLeft - scrollOffset, fieldRect.Top), textLayout, enabled ? _textBrush! : _secondaryBrush!);

        if (enabled && box == _focusedTextBox && box.CaretVisibleThisFrame && !box.HasSelection)
        {
            float caretDrawX = innerLeft + caretX - scrollOffset;
            target.DrawLine(
                new System.Numerics.Vector2(caretDrawX, fieldRect.Top + 5f),
                new System.Numerics.Vector2(caretDrawX, fieldRect.Bottom - 5f),
                _caretBrush!,
                1.5f
            );
        }
        target.PopAxisAlignedClip();

        y += FieldHeight;

        if (belowInfoKey is not null)
        {
            using var belowInfo = DWriteFactory.CreateTextLayout(LocalizationService.T(belowInfoKey), _labelFormat!, width, 28f);
            target.DrawTextLayout(new System.Numerics.Vector2(x, y + 4f), belowInfo, _secondaryBrush!);
            y += 28f + 4f;
        }

        return y + FieldGap;
    }

    private void DrawPasswordRevealButton(ID2D1DCRenderTarget target, Rect bounds, bool revealed, bool enabled = true)
    {
        float cx = bounds.Left + bounds.Width / 2f;
        float cy = bounds.Top + bounds.Height / 2f;
        var eyeBrush = !enabled ? _fieldBorderBrush! : revealed ? _checkboxBrush! : _secondaryBrush!;
        target.DrawEllipse(new Ellipse(new System.Numerics.Vector2(cx, cy), 7f, 4.5f), eyeBrush, 1.3f);
        if (enabled && revealed)
            target.FillEllipse(new Ellipse(new System.Numerics.Vector2(cx, cy), 2.2f, 2.2f), eyeBrush);
    }

    private float MeasureTextWidth(string text)
    {
        if (text.Length == 0)
            return 0f;
        using var layout = DWriteFactory.CreateTextLayout(text, _fieldFormat!, 2000f, FieldHeight);
        return layout.Metrics.WidthIncludingTrailingWhitespace;
    }

    private float ComputeTextScrollOffset(TextBox box, Rect fieldRect, out float innerLeft, out string display)
    {
        display = box.IsPassword && !box.RevealPassword ? new string('\u25CF', box.Text.Length) : box.Text;
        innerLeft = fieldRect.Left + 8f;
        float innerWidth = fieldRect.Width - 16f;
        if (box == _focusedTextBox)
        {
            float caretX = MeasureTextWidth(display.Substring(0, System.Math.Min(box.CaretIndex, display.Length)));
            return System.Math.Max(0f, caretX - innerWidth);
        }
        float fullWidth = MeasureTextWidth(display);
        return fullWidth > innerWidth ? fullWidth - innerWidth : 0f;
    }

    private float DrawCheckboxField(ID2D1DCRenderTarget target, float x, float width, float y, string labelKey, bool isChecked, string fieldId)
    {
        var boxRect = new Rect(x, y, CheckboxSize, CheckboxSize);
        var rowRect = new Rect(x, y, width, CheckboxSize);
        _checkboxRects.Add((rowRect, fieldId));

        DrawHoverShadow(target, boxRect);
        target.DrawRectangle(boxRect, _fieldBorderBrush!, 1f);
        if (isChecked)
        {
            var inset = new Rect(x + 3f, y + 3f, CheckboxSize - 6f, CheckboxSize - 6f);
            target.FillRectangle(inset, _checkboxBrush!);
        }

        using (var label = DWriteFactory.CreateTextLayout(LocalizationService.T(labelKey), _labelFormat!, width - CheckboxSize - CheckboxLabelGap, CheckboxSize + 4f))
            target.DrawTextLayout(new System.Numerics.Vector2(x + CheckboxSize + CheckboxLabelGap, y - 2f), label, _textBrush!);

        return y + CheckboxSize + FieldGap;
    }

    /// <summary>
    /// True while any of the 8 dropdowns in this window has its item list expanded.
    /// </summary>
    private bool AnyDropdownOpen() =>
        _themeDropdown.IsOpen || _languageDropdown.IsOpen || _chatSourceDropdown.IsOpen || _eventAlertSourceDropdown.IsOpen
        || _audioDeviceDropdown.IsOpen || _messageSoundPresetDropdown.IsOpen || _eventSoundPresetDropdown.IsOpen
        || _eventColorModeDropdown.IsOpen || _viewerCountModeDropdown.IsOpen;

    protected override void OnClientLButtonDown(int clientX, int clientY)
    {
        if (AnyDropdownOpen())
            return;

        if (_selectedSection == 5)
        {
            if (_enableMessageAlert && Contains(_messageVolumeSliderRect, clientX, clientY))
            {
                _messageVolumeSlider.HandleLButtonDown(_messageVolumeSliderRect, clientX, this);
                RequestRender();
            }
            else if (_enableEventAlert && Contains(_eventVolumeSliderRect, clientX, clientY))
            {
                _eventVolumeSlider.HandleLButtonDown(_eventVolumeSliderRect, clientX, this);
                RequestRender();
            }
            return;
        }

        if (_selectedSection != 0 && _selectedSection != 3 && _selectedSection != ViewerCountSectionIndex)
            return;

        bool shift = (Win32.GetKeyState(Win32.VK_SHIFT) & 0x8000) != 0;

        if (_selectedSection == 0)
        {
            if (Contains(_channelFieldRect, clientX, clientY))
                FocusTextBox(_channelBox, _channelFieldRect, clientX, shift);
            else if (Contains(_kickChannelFieldRect, clientX, clientY))
                FocusTextBox(_kickChannelBox, _kickChannelFieldRect, clientX, shift);
            else if (Contains(_fontSizeFieldRect, clientX, clientY))
                FocusTextBox(_fontSizeBox, _fontSizeFieldRect, clientX, shift);
            else if (Contains(_timeoutFieldRect, clientX, clientY))
                FocusTextBox(_timeoutBox, _timeoutFieldRect, clientX, shift);
            else if (Contains(_maxMessagesFieldRect, clientX, clientY))
                FocusTextBox(_maxMessagesBox, _maxMessagesFieldRect, clientX, shift);
            else
                BlurFocusedTextBox();
        }
        else if (_selectedSection == ViewerCountSectionIndex)
        {
            if (_showViewerCount && Contains(_viewerCountSizeFieldRect, clientX, clientY))
                FocusTextBox(_viewerCountSizeBox, _viewerCountSizeFieldRect, clientX, shift);
            else
                BlurFocusedTextBox();
        }
        else
        {

            if (!_enableStreamlabsEvents)
            {
                BlurFocusedTextBox();
                return;
            }

            if (Contains(_streamlabsSocketTokenRevealRect, clientX, clientY))
            {
                _streamlabsSocketTokenBox.RevealPassword = !_streamlabsSocketTokenBox.RevealPassword;
                RequestRender();
            }
            else if (Contains(_streamlabsWidgetTokenRevealRect, clientX, clientY))
            {
                _streamlabsWidgetTokenBox.RevealPassword = !_streamlabsWidgetTokenBox.RevealPassword;
                RequestRender();
            }
            else if (Contains(_streamlabsSocketTokenFieldRect, clientX, clientY))
                FocusTextBox(_streamlabsSocketTokenBox, _streamlabsSocketTokenFieldRect, clientX, shift);
            else if (Contains(_streamlabsWidgetTokenFieldRect, clientX, clientY))
                FocusTextBox(_streamlabsWidgetTokenBox, _streamlabsWidgetTokenFieldRect, clientX, shift);
            else
                BlurFocusedTextBox();
        }
    }

    private void FocusTextBox(TextBox box, Rect fieldRect, int clientX, bool shift)
    {

        if (_focusedTextBox is not null && _focusedTextBox != box)
            _focusedTextBox.Blur();

        float scrollOffset = ComputeTextScrollOffset(box, fieldRect, out float innerLeft, out _);
        _focusedTextBox = box;

        box.HandleClick(DWriteFactory, _fieldFormat!, new Rect(innerLeft - scrollOffset, 0, 10000f, FieldHeight), clientX, shift, this);
        RequestRender();
    }

    private Rect GetFocusedTextBoxFieldRect()
    {
        if (_focusedTextBox == _channelBox) return _channelFieldRect;
        if (_focusedTextBox == _kickChannelBox) return _kickChannelFieldRect;
        if (_focusedTextBox == _fontSizeBox) return _fontSizeFieldRect;
        if (_focusedTextBox == _timeoutBox) return _timeoutFieldRect;
        if (_focusedTextBox == _maxMessagesBox) return _maxMessagesFieldRect;
        if (_focusedTextBox == _viewerCountSizeBox) return _viewerCountSizeFieldRect;
        if (_focusedTextBox == _streamlabsSocketTokenBox) return _streamlabsSocketTokenFieldRect;
        if (_focusedTextBox == _streamlabsWidgetTokenBox) return _streamlabsWidgetTokenFieldRect;
        return default;
    }

    protected override void OnClientMouseMove(int clientX, int clientY)
    {

        _hoverMouseX = clientX;
        _hoverMouseY = clientY;
        RequestRender();

        _messageVolumeSlider.HandleMouseMove(_messageVolumeSliderRect, clientX);
        _eventVolumeSlider.HandleMouseMove(_eventVolumeSliderRect, clientX);

        _themeDropdown.HandleMouseMove(clientX, clientY);
        _languageDropdown.HandleMouseMove(clientX, clientY);
        _chatSourceDropdown.HandleMouseMove(clientX, clientY);
        _eventAlertSourceDropdown.HandleMouseMove(clientX, clientY);
        _audioDeviceDropdown.HandleMouseMove(clientX, clientY);
        _messageSoundPresetDropdown.HandleMouseMove(clientX, clientY);
        _eventSoundPresetDropdown.HandleMouseMove(clientX, clientY);
        _eventColorModeDropdown.HandleMouseMove(clientX, clientY);
        _viewerCountModeDropdown.HandleMouseMove(clientX, clientY);

        if (_focusedTextBox is null)
            return;
        var fieldRect = GetFocusedTextBoxFieldRect();
        float scrollOffset = ComputeTextScrollOffset(_focusedTextBox, fieldRect, out float innerLeft, out _);
        if (_focusedTextBox.HandleMouseMoveDrag(DWriteFactory, _fieldFormat!, new Rect(innerLeft - scrollOffset, 0, 10000f, FieldHeight), clientX))
            RequestRender();
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

    protected override void OnClientLButtonUp(int clientX, int clientY)
    {

        bool wasDraggingText = _focusedTextBox?.HandleLButtonUp() ?? false;
        bool wasDraggingMessageSlider = _messageVolumeSlider.HandleLButtonUp();
        bool wasDraggingEventSlider = _eventVolumeSlider.HandleLButtonUp();
        if (wasDraggingText || wasDraggingMessageSlider || wasDraggingEventSlider)
            return;

        Win32.GetClientRect(Hwnd, out var client);
        float width = client.Right - client.Left;

        if (Contains(GetCloseButtonRect(width), clientX, clientY))
        {
            CloseDiscardingChanges();
            return;
        }
        if (Contains(_cancelButtonRect, clientX, clientY))
        {
            CloseDiscardingChanges();
            return;
        }
        if (Contains(_saveButtonRect, clientX, clientY))
        {
            CloseKeepingChanges();
            return;
        }

        if (_themeDropdown.HandleClick(clientX, clientY) || _languageDropdown.HandleClick(clientX, clientY) || _chatSourceDropdown.HandleClick(clientX, clientY) || _eventAlertSourceDropdown.HandleClick(clientX, clientY)
            || _audioDeviceDropdown.HandleClick(clientX, clientY) || _messageSoundPresetDropdown.HandleClick(clientX, clientY) || _eventSoundPresetDropdown.HandleClick(clientX, clientY)
            || _eventColorModeDropdown.HandleClick(clientX, clientY) || _viewerCountModeDropdown.HandleClick(clientX, clientY))
        {
            RequestRender();
            return;
        }

        for (int i = 0; i < _sidebarRowRects.Count; i++)
        {
            if (Contains(_sidebarRowRects[i], clientX, clientY))
            {
                _selectedSection = i;
                BlurFocusedTextBox();
                _capturingHotkeyField = null;
                RequestRender();
                return;
            }
        }

        if (_selectedSection == 1)
        {
            HandleHotkeysSectionClick(clientX, clientY);
            return;
        }

        if (_selectedSection == 2)
        {
            HandleTwitchApiSectionClick(clientX, clientY);
            return;
        }

        if (_selectedSection == 3)
        {
            HandleStreamlabsSectionClick(clientX, clientY);
            return;
        }

        if (_selectedSection == 4)
        {
            HandleAlertsSectionClick(clientX, clientY);
            return;
        }

        if (_selectedSection == 5)
        {
            HandleAudioSectionClick(clientX, clientY);
            return;
        }

        if (_selectedSection == ViewerCountSectionIndex)
        {
            HandleViewerCountSectionClick(clientX, clientY);
            return;
        }

        if (_selectedSection == AboutSectionIndex)
        {
            HandleAboutSectionClick(clientX, clientY);
            return;
        }

        if (_selectedSection != 0)
            return;

        if (Contains(_themeFieldRect, clientX, clientY))
        {
            OpenThemeDropdown();
            return;
        }
        if (Contains(_languageFieldRect, clientX, clientY))
        {
            OpenLanguageDropdown();
            return;
        }
        if (Contains(_chatSourceFieldRect, clientX, clientY))
        {
            OpenChatSourceDropdown();
            return;
        }

        foreach (var (bounds, field) in _checkboxRects)
        {
            if (Contains(bounds, clientX, clientY))
            {
                ToggleCheckbox(field);
                RequestRender();
                return;
            }
        }
    }

    private void ToggleCheckbox(string fieldId)
    {
        switch (fieldId)
        {
            case "ClickThrough": _clickThrough = !_clickThrough; Settings.ClickThrough = _clickThrough; break;
            case "DebugMode": _debugMode = !_debugMode; Settings.EnableDebugMode = _debugMode; break;
            case "ThirdPartyEmotes": _thirdPartyEmotes = !_thirdPartyEmotes; Settings.EnableThirdPartyEmotes = _thirdPartyEmotes; break;
            case "EventsPanel": _eventsPanel = !_eventsPanel; Settings.EnableEventsPanel = _eventsPanel; break;
            case "ModerationPanel": _moderationPanel = !_moderationPanel; Settings.EnableModerationPanel = _moderationPanel; break;
            case "HighQualityMedia": _highQualityMedia = !_highQualityMedia; Settings.HighQualityMedia = _highQualityMedia; break;
            case "MultichatTwitchEnabled": _multichatTwitchEnabled = !_multichatTwitchEnabled; Settings.MultichatTwitchEnabled = _multichatTwitchEnabled; break;
            case "MultichatKickEnabled": _multichatKickEnabled = !_multichatKickEnabled; Settings.MultichatKickEnabled = _multichatKickEnabled; break;
            case "MultichatUseSameChannel":
                _multichatUseSameChannel = !_multichatUseSameChannel;
                Settings.MultichatUseSameChannel = _multichatUseSameChannel;
                if (_multichatUseSameChannel && _focusedTextBox == _kickChannelBox)
                    BlurFocusedTextBox();
                break;
            case "EnableGlobalHotkeys": _enableGlobalHotkeys = !_enableGlobalHotkeys; Settings.EnableGlobalHotkeys = _enableGlobalHotkeys; break;
            case "EnableTwitchApi": _enableTwitchApi = !_enableTwitchApi; Settings.EnableTwitchApi = _enableTwitchApi; break;
            case "ShowViewerCount": _showViewerCount = !_showViewerCount; Settings.ShowViewerCount = _showViewerCount; break;
            case "ViewerCountIncludeTwitch": _viewerCountIncludeTwitch = !_viewerCountIncludeTwitch; Settings.ViewerCountIncludeTwitch = _viewerCountIncludeTwitch; break;
            case "ViewerCountIncludeKick": _viewerCountIncludeKick = !_viewerCountIncludeKick; Settings.ViewerCountIncludeKick = _viewerCountIncludeKick; break;
            case "ViewerCountIncludeYouTube": _viewerCountIncludeYouTube = !_viewerCountIncludeYouTube; Settings.ViewerCountIncludeYouTube = _viewerCountIncludeYouTube; break;
            case "ShowBadges": _showBadges = !_showBadges; Settings.ShowBadges = _showBadges; break;
            case "EnableStreamlabsEvents":
                _enableStreamlabsEvents = !_enableStreamlabsEvents;
                Settings.EnableStreamlabsEvents = _enableStreamlabsEvents;

                if (!_enableStreamlabsEvents)
                    BlurFocusedTextBox();
                break;
            case "EnableMessageAlert": _enableMessageAlert = !_enableMessageAlert; Settings.EnableMessageAlert = _enableMessageAlert; break;
            case "EnableEventAlert": _enableEventAlert = !_enableEventAlert; Settings.EnableEventAlert = _enableEventAlert; break;
            case "EnableVisualFlash": _enableVisualFlash = !_enableVisualFlash; Settings.EnableVisualFlash = _enableVisualFlash; break;
            case "DisableAlertCooldown": _disableAlertCooldown = !_disableAlertCooldown; Settings.DisableAlertCooldown = _disableAlertCooldown; break;
            case "EnableIrcEventGif": _enableIrcEventGif = !_enableIrcEventGif; Settings.EnableIrcEventGif = _enableIrcEventGif; break;
            case "IrcEventGifAdvancedMode": _ircEventGifAdvancedMode = !_ircEventGifAdvancedMode; Settings.IrcEventGifAdvancedMode = _ircEventGifAdvancedMode; break;
            case "EventBoxColorAdvancedMode": _eventBoxColorAdvancedMode = !_eventBoxColorAdvancedMode; Settings.EventBoxColorAdvancedMode = _eventBoxColorAdvancedMode; break;
        }
    }

    private void RevertToOriginals()
    {
        RevertGeneral();
        RevertHotkeys();
        RevertTwitchApi();
        RevertViewerCount();
        RevertStreamlabs();
        RevertAudio();
        RevertAlerts();
    }

    private void CloseDiscardingChanges()
    {
        RevertToOriginals();
        Win32.PostMessage(Hwnd, Win32.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
    }

    private void CloseKeepingChanges()
    {
        Win32.PostMessage(Hwnd, Win32.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
    }

    protected override void OnKeyDown(int virtualKeyCode, bool ctrlDown, bool shiftDown)
    {
        if (_capturingHotkeyField is not null)
        {
            HandleHotkeyCaptureKeyDown(virtualKeyCode, ctrlDown, shiftDown);
            return;
        }

        if (_focusedTextBox is null)
            return;
        _focusedTextBox.HandleKeyDown(virtualKeyCode, ctrlDown, shiftDown);
        CommitFocusedTextBoxIfNumeric();
        RequestRender();
    }
    protected override void OnChar(char c)
    {
        if (_focusedTextBox is null)
            return;
        _focusedTextBox.HandleChar(c);

        CommitFocusedTextBoxIfNumeric();
        RequestRender();
    }

    protected override void OnWindowFocusLost()
    {

        BlurFocusedTextBox();
        _capturingHotkeyField = null;
    }

    private void CommitFocusedTextBoxIfNumeric()
    {
        if (_focusedTextBox == _channelBox)
            Settings.Channel = _channelBox.Text;
        else if (_focusedTextBox == _kickChannelBox)
            Settings.KickChannel = _kickChannelBox.Text;
        else if (_focusedTextBox == _fontSizeBox && double.TryParse(_fontSizeBox.Text, out var fs))
            Settings.FontSize = fs;
        else if (_focusedTextBox == _timeoutBox && int.TryParse(_timeoutBox.Text, out var to))
            Settings.MessageTimeoutSeconds = to;
        else if (_focusedTextBox == _maxMessagesBox && int.TryParse(_maxMessagesBox.Text, out var mm))
            Settings.MaxMessages = mm;
        else if (_focusedTextBox == _viewerCountSizeBox)
            CommitViewerCountSize();
        else if (_focusedTextBox == _streamlabsSocketTokenBox)
            Settings.StreamlabsSocketToken = NormalizeStreamlabsToken(_streamlabsSocketTokenBox.Text);
        else if (_focusedTextBox == _streamlabsWidgetTokenBox)
            Settings.StreamlabsWidgetToken = NormalizeStreamlabsToken(_streamlabsWidgetTokenBox.Text);
    }

    private void DrawHoverShadow(ID2D1DCRenderTarget target, Rect rect)
    {
        if (!Contains(rect, _hoverMouseX, _hoverMouseY))
            return;

        System.Span<float> offsets = stackalloc float[] { 3f, 2f, 1f };
        System.Span<float> opacities = stackalloc float[] { 0.05f, 0.09f, 0.14f };
        for (int i = 0; i < offsets.Length; i++)
        {
            var shadowRect = new Rect(rect.Left + offsets[i], rect.Top + offsets[i], rect.Width, rect.Height);
            _hoverShadowBrush!.Opacity = opacities[i];
            target.FillRectangle(shadowRect, _hoverShadowBrush);
        }
        _hoverShadowBrush!.Opacity = 1f;
    }

    private static bool Contains(Rect rect, float x, float y) =>
        x >= rect.Left && x <= rect.Right && y >= rect.Top && y <= rect.Bottom;

    protected override void OnDestroyed()
    {

        _twitchLoginCts?.Cancel();
        _twitchLoginCts?.Dispose();
        _twitchLoginCts = null;

        _caretBlinkTimer?.Dispose();
        _caretBlinkTimer = null;
        DisposeThemedBrushes();
        _checkboxBrush?.Dispose();
        _selectionBrush?.Dispose();
        _windowBackgroundBrushInverse?.Dispose();
        _headerFormat?.Dispose();
        _labelFormat?.Dispose();
        _fieldFormat?.Dispose();
        _buttonFormat?.Dispose();
        _titleBarFormat?.Dispose();
        _sidebarFormat?.Dispose();
        _aboutTitleFormat?.Dispose();
        _aboutCenterFormat?.Dispose();
        _aboutBoldCenterFormat?.Dispose();
        _aboutSmallCenterFormat?.Dispose();
        _aboutIconBitmap?.Dispose();
        _hoverShadowBrush?.Dispose();
        _themeDropdown.Dispose();
        _languageDropdown.Dispose();
        _chatSourceDropdown.Dispose();
        _eventAlertSourceDropdown.Dispose();
        _audioDeviceDropdown.Dispose();
        _messageSoundPresetDropdown.Dispose();
        _eventSoundPresetDropdown.Dispose();
        _eventColorModeDropdown.Dispose();
        _viewerCountModeDropdown.Dispose();
        DisposeGifThumbnailCache();
    }

    private void DisposeThemedBrushes()
    {
        _textBrush?.Dispose(); _textBrush = null;
        _secondaryBrush?.Dispose(); _secondaryBrush = null;
        _fieldBackgroundBrush?.Dispose(); _fieldBackgroundBrush = null;
        _fieldBorderBrush?.Dispose(); _fieldBorderBrush = null;
        _sidebarSelectedBrush?.Dispose(); _sidebarSelectedBrush = null;
        _windowBackgroundBrush?.Dispose(); _windowBackgroundBrush = null;
        _caretBrush?.Dispose(); _caretBrush = null;
        _scrollbarTrackBrush?.Dispose(); _scrollbarTrackBrush = null;
        _scrollbarThumbBrush?.Dispose(); _scrollbarThumbBrush = null;
        DisposeTwitchLoginButtonResources();
    }
}