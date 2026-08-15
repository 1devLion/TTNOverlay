using TTNOverlay.Native;
using TTNOverlay.Overlay.Controls;
using TTNOverlay.Services;
using Vortice.Direct2D1;
using Vortice.DirectWrite;
using Vortice.Mathematics;
using Rect = Vortice.Mathematics.Rect;

namespace TTNOverlay.Overlay;

/// <summary>
/// SettingsRenderWindow partial: the Alerts section (event flash color, per-event colors, and event GIFs).
/// </summary>
internal sealed partial class SettingsRenderWindow
{

    private bool _enableVisualFlash;
    private bool _disableAlertCooldown;
    private string _flashColorHex = "#FFD700";
    private byte _flashAlpha = 0xFF;
    private bool _enableIrcEventGif;
    private string _ircEventGifPath = "";
    private bool _ircEventGifAdvancedMode;
    private Dictionary<string, string> _ircEventGifPaths = new();
    private bool _eventBoxColorAdvancedMode;
    private Dictionary<string, string> _eventBoxColorModes = new();
    private Dictionary<string, string> _eventBoxColors = new();

    private bool _originalEnableVisualFlash;
    private bool _originalDisableAlertCooldown;
    private string _originalFlashColorHex = "";
    private byte _originalFlashAlpha;
    private bool _originalEnableIrcEventGif;
    private string _originalIrcEventGifPath = "";
    private bool _originalIrcEventGifAdvancedMode;
    private Dictionary<string, string> _originalIrcEventGifPaths = new();
    private bool _originalEventBoxColorAdvancedMode;
    private Dictionary<string, string> _originalEventBoxColorModes = new();
    private Dictionary<string, string> _originalEventBoxColors = new();

    private readonly Dropdown _eventColorModeDropdown = new();

    private string? _eventColorModeDropdownKey;

    private ScrollState _alertsScroll;

    private Rect _flashColorSwatchRect;
    private Rect _pickFlashColorButtonRect;
    private Rect _testFlashButtonRect;
    private Rect _ircGifBrowseButtonRect;
    private Rect _ircGifClearButtonRect;
    private Rect _resetAllGifsButtonRect;
    private Rect _resetAllColorsButtonRect;
    private readonly Dictionary<string, Rect> _alertsGifBrowseButtonRects = new();
    private readonly Dictionary<string, Rect> _alertsGifClearButtonRects = new();
    private readonly Dictionary<string, Rect> _alertsColorModeButtonRects = new();
    private readonly Dictionary<string, Rect> _alertsColorChooseButtonRects = new();

    private readonly Dictionary<string, Rect> _alertsGifPreviewButtonRects = new();
    private readonly Dictionary<string, ID2D1Bitmap?> _gifThumbnailCache = new();
    private readonly HashSet<string> _gifThumbnailLoadInFlight = new();
    private readonly Dictionary<string, D2DBitmapLoader.DecodedImage> _pendingGifThumbnails = new();
    private const int GifThumbnailPixelSize = 64;
    private const float GifPreviewSize = 34f;

    public event Action<string, byte>? TestFlashRequested;

    private void InitAlerts()
    {
        _enableVisualFlash = Settings.EnableVisualFlash;
        _disableAlertCooldown = Settings.DisableAlertCooldown;
        _flashColorHex = Settings.AlertFlashColor;
        _flashAlpha = Settings.AlertFlashAlpha;
        _enableIrcEventGif = Settings.EnableIrcEventGif;
        _ircEventGifPath = Settings.IrcEventGifPath ?? "";
        _ircEventGifAdvancedMode = Settings.IrcEventGifAdvancedMode;
        _ircEventGifPaths = new Dictionary<string, string>(Settings.IrcEventGifPaths);
        _eventBoxColorAdvancedMode = Settings.EventBoxColorAdvancedMode;
        _eventBoxColorModes = new Dictionary<string, string>(Settings.EventBoxColorModes);
        _eventBoxColors = new Dictionary<string, string>(Settings.EventBoxColors);

        _originalEnableVisualFlash = _enableVisualFlash;
        _originalDisableAlertCooldown = _disableAlertCooldown;
        _originalFlashColorHex = _flashColorHex;
        _originalFlashAlpha = _flashAlpha;
        _originalEnableIrcEventGif = _enableIrcEventGif;
        _originalIrcEventGifPath = _ircEventGifPath;
        _originalIrcEventGifAdvancedMode = _ircEventGifAdvancedMode;
        _originalIrcEventGifPaths = new Dictionary<string, string>(_ircEventGifPaths);
        _originalEventBoxColorAdvancedMode = _eventBoxColorAdvancedMode;
        _originalEventBoxColorModes = new Dictionary<string, string>(_eventBoxColorModes);
        _originalEventBoxColors = new Dictionary<string, string>(_eventBoxColors);
    }

    private void RevertAlerts()
    {
        Settings.EnableVisualFlash = _originalEnableVisualFlash;
        Settings.DisableAlertCooldown = _originalDisableAlertCooldown;
        Settings.AlertFlashColor = _originalFlashColorHex;
        Settings.AlertFlashAlpha = _originalFlashAlpha;
        Settings.EnableIrcEventGif = _originalEnableIrcEventGif;
        Settings.IrcEventGifPath = _originalIrcEventGifPath;
        Settings.IrcEventGifAdvancedMode = _originalIrcEventGifAdvancedMode;
        Settings.IrcEventGifPaths = new Dictionary<string, string>(_originalIrcEventGifPaths);
        Settings.EventBoxColorAdvancedMode = _originalEventBoxColorAdvancedMode;
        Settings.EventBoxColorModes = new Dictionary<string, string>(_originalEventBoxColorModes);
        Settings.EventBoxColors = new Dictionary<string, string>(_originalEventBoxColors);
    }

    private void DrawAlertsSection(ID2D1DCRenderTarget target, float x, float width, float winHeight)
    {
        float viewportTop = TitleBarHeight;
        float viewportHeight = System.Math.Max(0f, winHeight - FooterHeight - viewportTop);

        float totalHeight = MeasureAlertsContentHeight();
        _alertsScroll.RecomputeOverflow(totalHeight, viewportHeight);

        _checkboxRects.Clear();
        _alertsGifBrowseButtonRects.Clear();
        _alertsGifClearButtonRects.Clear();
        _alertsGifPreviewButtonRects.Clear();
        _alertsColorModeButtonRects.Clear();
        _alertsColorChooseButtonRects.Clear();

        if (_pendingGifThumbnails.Count > 0)
        {
            foreach (var (path, decoded) in _pendingGifThumbnails)
            {
                try { _gifThumbnailCache[path] = D2DBitmapLoader.CreateBitmap(target, decoded, path); }
                catch { _gifThumbnailCache[path] = null; }
            }
            _pendingGifThumbnails.Clear();
        }

        target.PushAxisAlignedClip(new Rect(x, viewportTop, width, viewportHeight), AntialiasMode.PerPrimitive);
        DrawAlertsContent(target, x, width, viewportTop + Padding - _alertsScroll.Offset);
        target.PopAxisAlignedClip();
    }

    private float MeasureAlertsContentHeight()
    {
        float h = Padding;
        h += 32f;
        h += CheckboxSize + FieldGap;
        h += CheckboxSize + FieldGap;
        h += 18f + LabelGap + 24f + FieldGap;
        h += CheckboxSize + FieldGap;
        h += 18f + LabelGap + FieldHeight + FieldGap;
        h += CheckboxSize + FieldGap;
        if (_ircEventGifAdvancedMode)
            h += IrcGifEventTypes.Length * GifListRowHeight + FooterButtonHeight + FieldGap;
        h += 8f + 1f + 16f;
        h += 18f + 4f + 28f + 16f;
        h += CheckboxSize + FieldGap;
        if (_eventBoxColorAdvancedMode)
            h += AllEventTypesForColor.Length * ColorListRowHeight + FooterButtonHeight + FieldGap;
        return h;
    }

    private void DrawAlertsContent(ID2D1DCRenderTarget target, float x, float width, float y)
    {
        using (var header = DWriteFactory.CreateTextLayout(LocalizationService.T("Settings_Alerts_Header"), _headerFormat!, width, 24f))
            target.DrawTextLayout(new System.Numerics.Vector2(x, y), header, _textBrush!);
        y += 32f;

        y = DrawCheckboxField(target, x, width, y, "Settings_Alerts_VisualFlash", _enableVisualFlash, "EnableVisualFlash");
        y = DrawCheckboxField(target, x, width, y, "Settings_Alerts_NoCooldown", _disableAlertCooldown, "DisableAlertCooldown");
        y = DrawFlashColorRow(target, x, width, y);

        y = DrawCheckboxField(target, x, width, y, "Settings_Alerts_ShowIrcGif", _enableIrcEventGif, "EnableIrcEventGif");
        y = DrawIrcGifGenericRow(target, x, width, y);

        y = DrawCheckboxField(target, x, width, y, "Settings_Alerts_GifAdvancedMode", _ircEventGifAdvancedMode, "IrcEventGifAdvancedMode");
        if (_ircEventGifAdvancedMode)
        {
            float listHeight = IrcGifEventTypes.Length * GifListRowHeight;
            target.DrawRectangle(new Rect(x, y, width, listHeight), _fieldBorderBrush!, 1f);
            foreach (var (key, locKey) in IrcGifEventTypes)
                y = DrawGifListItemRow(target, x, width, y, key, LocalizationService.T(locKey));

            _resetAllGifsButtonRect = MeasureButtonRect(LocalizationService.T("Settings_Alerts_ResetAllGifs"), x, y, FooterButtonHeight, minWidth: 160f);
            DrawFooterButton(target, _resetAllGifsButtonRect, LocalizationService.T("Settings_Alerts_ResetAllGifs"), primary: false);
            y += FooterButtonHeight + FieldGap;
        }

        y += 8f;
        target.DrawLine(new System.Numerics.Vector2(x, y), new System.Numerics.Vector2(x + width, y), _fieldBorderBrush!, 1f);
        y += 16f;

        using (var boxHeader = DWriteFactory.CreateTextLayout(LocalizationService.T("Settings_Alerts_BoxColorHeader"), _labelFormat!, width, 18f))
            target.DrawTextLayout(new System.Numerics.Vector2(x, y), boxHeader, _textBrush!);
        y += 18f + 4f;

        using (var explanation = DWriteFactory.CreateTextLayout(LocalizationService.T("Settings_Alerts_ColorModeExplanation"), _labelFormat!, width, 28f))
            target.DrawTextLayout(new System.Numerics.Vector2(x, y), explanation, _secondaryBrush!);
        y += 28f + 16f;

        y = DrawCheckboxField(target, x, width, y, "Settings_Alerts_CustomizeBoxColor", _eventBoxColorAdvancedMode, "EventBoxColorAdvancedMode");
        if (_eventBoxColorAdvancedMode)
        {
            float listHeight2 = AllEventTypesForColor.Length * ColorListRowHeight;
            target.DrawRectangle(new Rect(x, y, width, listHeight2), _fieldBorderBrush!, 1f);
            foreach (var (key, locKey, defaultHex) in AllEventTypesForColor)
                y = DrawColorListItemRow(target, x, width, y, key, LocalizationService.T(locKey), defaultHex);

            _resetAllColorsButtonRect = MeasureButtonRect(LocalizationService.T("Settings_Alerts_ResetAllColors"), x, y, FooterButtonHeight, minWidth: 160f);
            DrawFooterButton(target, _resetAllColorsButtonRect, LocalizationService.T("Settings_Alerts_ResetAllColors"), primary: false);
            y += FooterButtonHeight + FieldGap;
        }
    }

    private float DrawFlashColorRow(ID2D1DCRenderTarget target, float x, float width, float y)
    {
        using (var label = DWriteFactory.CreateTextLayout(LocalizationService.T("Settings_Alerts_FlashColor"), _labelFormat!, width, 18f))
            target.DrawTextLayout(new System.Numerics.Vector2(x, y), label, _secondaryBrush!);
        y += 18f + LabelGap;

        bool enabled = _enableVisualFlash;
        _flashColorSwatchRect = new Rect(x, y, 24f, 24f);
        if (ColorPickerWindow.TryParseHex(_flashColorHex, out var fr, out var fg, out var fb))
        {
            using var swatchBrush = target.CreateSolidColorBrush(new Color4(fr / 255f, fg / 255f, fb / 255f, enabled ? _flashAlpha / 255f : 0.3f));
            target.FillRectangle(_flashColorSwatchRect, swatchBrush);
        }
        target.DrawRectangle(_flashColorSwatchRect, _fieldBorderBrush!, 1f);

        _pickFlashColorButtonRect = new Rect(x + 24f + 8f, y, 90f, 24f);
        DrawFooterButton(target, _pickFlashColorButtonRect, LocalizationService.T("Settings_Alerts_PickColor"), primary: false, enabled: enabled);

        _testFlashButtonRect = new Rect(_pickFlashColorButtonRect.Right + 8f, y, 70f, 24f);
        DrawFooterButton(target, _testFlashButtonRect, LocalizationService.T("Settings_Alerts_Test"), primary: false, enabled: enabled);

        return y + 24f + FieldGap;
    }

    private float DrawIrcGifGenericRow(ID2D1DCRenderTarget target, float x, float width, float y)
    {
        using (var label = DWriteFactory.CreateTextLayout(LocalizationService.T("Settings_Alerts_IrcGifPath"), _labelFormat!, width, 18f))
            target.DrawTextLayout(new System.Numerics.Vector2(x, y), label, _secondaryBrush!);
        y += 18f + LabelGap;

        bool enabled = _enableIrcEventGif && !_ircEventGifAdvancedMode;

        _ircGifClearButtonRect = new Rect(x + width - 28f, y, 28f, FieldHeight);
        _ircGifBrowseButtonRect = new Rect(_ircGifClearButtonRect.Left - 8f - 32f, y, 32f, FieldHeight);
        float pathWidth = _ircGifBrowseButtonRect.Left - x - FieldGap;
        var pathRect = new Rect(x, y, System.Math.Max(1f, pathWidth), FieldHeight);

        target.FillRectangle(pathRect, _fieldBackgroundBrush!);
        target.DrawRectangle(pathRect, _fieldBorderBrush!, 1f);
        target.PushAxisAlignedClip(pathRect, AntialiasMode.PerPrimitive);
        string displayPath = string.IsNullOrEmpty(_ircEventGifPath) ? "" : System.IO.Path.GetFileName(_ircEventGifPath);
        using (var pathLayout = DWriteFactory.CreateTextLayout(displayPath, _fieldFormat!, pathRect.Width - 16f, pathRect.Height))
            target.DrawTextLayout(new System.Numerics.Vector2(pathRect.Left + 8f, pathRect.Top), pathLayout, enabled ? _secondaryBrush! : _fieldBorderBrush!);
        target.PopAxisAlignedClip();

        DrawFooterButton(target, _ircGifBrowseButtonRect, "...", primary: false, enabled: enabled);
        DrawFooterButton(target, _ircGifClearButtonRect, "\u2715", primary: false, enabled: enabled && !string.IsNullOrWhiteSpace(_ircEventGifPath));

        return y + FieldHeight + FieldGap;
    }

    private const float GifListRowHeight = 42f;

    private float DrawGifListItemRow(ID2D1DCRenderTarget target, float x, float width, float y, string key, string displayName)
    {
        float rowTop = y + 4f;
        const float rowContentHeight = 34f;

        _ircEventGifPaths.TryGetValue(key, out var path);
        path ??= "";

        var clearRect = new Rect(x + width - 24f, rowTop, 24f, rowContentHeight);
        var browseRect = new Rect(clearRect.Left - 6f - 28f, rowTop, 28f, rowContentHeight);
        var nameRect = new Rect(x, rowTop, 320f, rowContentHeight);
        var previewRect = new Rect(nameRect.Right + 8f, rowTop + (rowContentHeight - GifPreviewSize) / 2f, GifPreviewSize, GifPreviewSize);
        var pathRect = new Rect(previewRect.Right + 8f, rowTop, System.Math.Max(1f, browseRect.Left - previewRect.Right - 8f - FieldGap), rowContentHeight);

        target.PushAxisAlignedClip(nameRect, AntialiasMode.PerPrimitive);
        using (var nameLayout = DWriteFactory.CreateTextLayout(displayName, _labelFormat!, nameRect.Width, nameRect.Height))
            target.DrawTextLayout(new System.Numerics.Vector2(nameRect.Left, nameRect.Top + 2f), nameLayout, _secondaryBrush!);
        target.PopAxisAlignedClip();

        target.FillRectangle(previewRect, _fieldBackgroundBrush!);
        target.DrawRectangle(previewRect, _fieldBorderBrush!, 1f);
        if (!string.IsNullOrWhiteSpace(path))
        {
            var thumb = GetOrLoadGifThumbnail(path);
            if (thumb is not null)
            {
                target.PushAxisAlignedClip(previewRect, AntialiasMode.PerPrimitive);
                target.DrawBitmap(thumb, previewRect, 1f, BitmapInterpolationMode.Linear, new Rect(0, 0, thumb.Size.Width, thumb.Size.Height));
                target.PopAxisAlignedClip();
            }
            DrawGifPreviewEyeOverlay(target, previewRect);
            _alertsGifPreviewButtonRects[key] = previewRect;
        }

        target.FillRectangle(pathRect, _fieldBackgroundBrush!);
        target.DrawRectangle(pathRect, _fieldBorderBrush!, 1f);
        target.PushAxisAlignedClip(pathRect, AntialiasMode.PerPrimitive);
        string displayPath = string.IsNullOrEmpty(path) ? "" : System.IO.Path.GetFileName(path);
        using (var pathLayout = DWriteFactory.CreateTextLayout(displayPath, _fieldFormat!, pathRect.Width - 8f, pathRect.Height))
            target.DrawTextLayout(new System.Numerics.Vector2(pathRect.Left + 4f, pathRect.Top), pathLayout, _secondaryBrush!);
        target.PopAxisAlignedClip();

        DrawFooterButton(target, browseRect, "...", primary: false);
        DrawFooterButton(target, clearRect, "\u2715", primary: false, enabled: !string.IsNullOrWhiteSpace(path));

        _alertsGifBrowseButtonRects[key] = browseRect;
        _alertsGifClearButtonRects[key] = clearRect;

        return y + GifListRowHeight;
    }

    private void DrawGifPreviewEyeOverlay(ID2D1DCRenderTarget target, Rect bounds)
    {
        using var dimBrush = target.CreateSolidColorBrush(new Color4(0f, 0f, 0f, 0.4f));
        target.FillRectangle(bounds, dimBrush);

        float cx = bounds.Left + bounds.Width / 2f;
        float cy = bounds.Top + bounds.Height / 2f;
        using var eyeBrush = target.CreateSolidColorBrush(new Color4(1f, 1f, 1f, 1f));
        target.DrawEllipse(new Ellipse(new System.Numerics.Vector2(cx, cy), 9f, 5.5f), eyeBrush, 1.4f);
        target.FillEllipse(new Ellipse(new System.Numerics.Vector2(cx, cy), 2.6f, 2.6f), eyeBrush);
    }

    private ID2D1Bitmap? GetOrLoadGifThumbnail(string path)
    {
        if (_gifThumbnailCache.TryGetValue(path, out var cached))
            return cached;

        if (_gifThumbnailLoadInFlight.Add(path))
            _ = LoadGifThumbnailAsync(path);

        return null;
    }

    private async Task LoadGifThumbnailAsync(string path)
    {
        var bytes = await LocalImageLoader.ReadBytesAsync(path);
        D2DBitmapLoader.DecodedImage? decoded = null;
        if (bytes is not null)
        {
            var animated = LocalImageLoader.TryDecodeAnimated(bytes, GifThumbnailPixelSize);
            decoded = animated is { Count: > 0 }
                ? D2DBitmapLoader.Decode(animated[0].Image)
                : LocalImageLoader.TryDecodeStatic(bytes, GifThumbnailPixelSize);
        }

        PostToUiThread(() =>
        {
            _gifThumbnailLoadInFlight.Remove(path);
            if (decoded is null)
            {
                _gifThumbnailCache[path] = null;
                RequestRender();
                return;
            }
            _pendingGifThumbnails[path] = decoded.Value;
            RequestRender();
        });
    }

    private void DisposeGifThumbnailCache()
    {
        foreach (var bmp in _gifThumbnailCache.Values)
            bmp?.Dispose();
        _gifThumbnailCache.Clear();
        _gifThumbnailLoadInFlight.Clear();
        _pendingGifThumbnails.Clear();
    }

    private void OpenGifPreviewWindow(string path) => GifPreviewWindow.Show(Hwnd, PostToUiThread, path);

    private const float ColorListRowHeight = 36f;

    private float DrawColorListItemRow(ID2D1DCRenderTarget target, float x, float width, float y, string key, string displayName, string defaultHex)
    {
        float rowTop = y + 4f;
        const float rowContentHeight = 26f;

        string mode = _eventBoxColorModes.TryGetValue(key, out var m) ? m : "Theme";

        var chooseRect = new Rect(x + width - 90f, rowTop, 90f, rowContentHeight);
        var swatchRect = new Rect(chooseRect.Left - 8f - 22f, rowTop + 2f, 22f, 22f);
        var modeRect = new Rect(swatchRect.Left - 8f - 120f, rowTop, 120f, rowContentHeight);
        var nameRect = new Rect(x, rowTop, System.Math.Max(1f, modeRect.Left - x - FieldGap), rowContentHeight);

        using (var nameLayout = DWriteFactory.CreateTextLayout(displayName, _labelFormat!, nameRect.Width, nameRect.Height))
            target.DrawTextLayout(new System.Numerics.Vector2(nameRect.Left, nameRect.Top + 2f), nameLayout, _secondaryBrush!);

        DrawFooterButton(target, modeRect, ColorModeLabel(mode) + " \u25BE", primary: false);

        Color4 swatchColor;
        if (mode == "Custom")
        {
            string customHex = _eventBoxColors.TryGetValue(key, out var customValue) && !string.IsNullOrEmpty(customValue) ? customValue : defaultHex;
            swatchColor = ColorPickerWindow.TryParseHex(customHex, out var cr, out var cg, out var cb)
                ? new Color4(cr / 255f, cg / 255f, cb / 255f, 1f)
                : new Color4(0f, 0f, 0f, 1f);
        }
        else if (mode == "Original")
        {
            swatchColor = ColorPickerWindow.TryParseHex(defaultHex, out var cr, out var cg, out var cb)
                ? new Color4(cr / 255f, cg / 255f, cb / 255f, 1f)
                : new Color4(0f, 0f, 0f, 1f);
        }
        else
        {
            swatchColor = ThemeService.IsDark
                ? new Color4(0f, 0f, 0f, 1f)
                : new Color4(0xE8 / 255f, 0xE8 / 255f, 0xE8 / 255f, 1f);
        }

        using (var swatchBrush = target.CreateSolidColorBrush(swatchColor))
            target.FillRectangle(swatchRect, swatchBrush);
        target.DrawRectangle(swatchRect, _fieldBorderBrush!, 1f);

        DrawFooterButton(target, chooseRect, LocalizationService.T("Settings_Alerts_ChooseEllipsis"), primary: false, enabled: mode == "Custom");

        _alertsColorModeButtonRects[key] = modeRect;
        _alertsColorChooseButtonRects[key] = chooseRect;

        return y + ColorListRowHeight;
    }

    private static string ColorModeLabel(string mode) => mode switch
    {
        "Original" => LocalizationService.T("Settings_Alerts_ColorMode_Original"),
        "Custom" => LocalizationService.T("Settings_Alerts_ColorMode_Custom"),
        _ => LocalizationService.T("Settings_Alerts_ColorMode_Theme"),
    };

    private static readonly (string Key, string LocKey, string DefaultHex)[] AllEventTypesForColor =
    {
        ("sub", "EventType_Sub", "#9B4DCA"),
        ("resub", "EventType_Resub", "#6D28D9"),
        ("subgift", "EventType_Subgift", "#C026D3"),
        ("anonsubgift", "EventType_AnonSubgift", "#86198F"),
        ("submysterygift", "EventType_MysteryGift", "#DB2777"),
        ("anonsubmysterygift", "EventType_AnonMysteryGift", "#9D174D"),
        ("primepaidupgrade", "EventType_PrimeUpgrade", "#4F46E5"),
        ("giftpaidupgrade", "EventType_GiftUpgrade", "#7C3AED"),
        ("anongiftpaidupgrade", "EventType_AnonGiftUpgrade", "#5B21B6"),
        ("raid", "EventType_Raid", "#FF7A00"),
        ("ritual", "EventType_Ritual", "#009E9E"),
        ("bitsbadgetier", "EventType_BitsBadge", "#0090FF"),
        ("announcement", "EventType_Announcement", "#1F69FF"),
        ("sl_donation", "EventType_SlDonation", "#1FA05C"),
        ("sl_follow", "EventType_SlFollow", "#9B4DCA"),
        ("sl_host", "EventType_SlHost", "#0082FF"),
        ("sl_merch", "EventType_SlMerch", "#FF7A00"),
        ("sl_subscription", "EventType_SlSubscription", "#9B4DCA"),
        ("sl_bits", "EventType_SlBits", "#0090FF"),
        ("sl_powerup", "EventType_SlPowerup", "#0090FF"),
        ("sl_raid", "EventType_SlRaid", "#FF7A00"),
        ("sl_subgift", "EventType_SlSubgift", "#9B4DCA"),
        ("sl_anonsubgift", "EventType_SlAnonSubgift", "#9B4DCA"),
        ("sl_submysterygift", "EventType_SlMysteryGift", "#9B4DCA"),
        ("sl_anonmysterygift", "EventType_SlAnonMysteryGift", "#9B4DCA"),
    };

    private static readonly (string Key, string LocKey)[] IrcGifEventTypes =
    {
        ("sub", "EventType_Sub"),
        ("resub", "EventType_Resub"),
        ("raid", "EventType_Raid"),
        ("ritual", "EventType_Short_NewChatter"),
        ("bitsbadgetier", "EventType_BitsBadge"),
        ("announcement", "EventType_Announcement"),
        ("primepaidupgrade", "EventType_Short_PrimeUpgrade"),
        ("giftpaidupgrade", "EventType_Short_GiftUpgrade"),
        ("anongiftpaidupgrade", "EventType_Short_AnonGiftUpgrade"),
        ("subgift", "EventType_Short_GiftedSub"),
        ("anonsubgift", "EventType_Short_AnonGiftedSub"),
        ("submysterygift", "EventType_Short_MysterySub"),
        ("anonsubmysterygift", "EventType_Short_AnonMysterySub"),
    };

    private void HandleAlertsSectionClick(int clientX, int clientY)
    {
        foreach (var (bounds, field) in _checkboxRects)
        {
            if (Contains(bounds, clientX, clientY))
            {
                ToggleCheckbox(field);
                RequestRender();
                return;
            }
        }

        if (_enableVisualFlash)
        {
            if (Contains(_pickFlashColorButtonRect, clientX, clientY)) { OpenFlashColorPicker(); return; }
            if (Contains(_testFlashButtonRect, clientX, clientY)) { TestFlashColor(); return; }
        }

        if (_enableIrcEventGif && !_ircEventGifAdvancedMode)
        {
            if (Contains(_ircGifBrowseButtonRect, clientX, clientY)) { BrowseIrcEventGif(null); return; }
            if (!string.IsNullOrWhiteSpace(_ircEventGifPath) && Contains(_ircGifClearButtonRect, clientX, clientY))
            {
                _ircEventGifPath = "";
                Settings.IrcEventGifPath = "";
                RequestRender();
                return;
            }
        }

        if (_ircEventGifAdvancedMode)
        {
            foreach (var (key, bounds) in _alertsGifBrowseButtonRects)
                if (Contains(bounds, clientX, clientY)) { BrowseIrcEventGif(key); return; }
            foreach (var (key, bounds) in _alertsGifClearButtonRects)
                if (Contains(bounds, clientX, clientY)) { ClearAdvancedGif(key); return; }
            foreach (var (key, bounds) in _alertsGifPreviewButtonRects)
            {
                if (!Contains(bounds, clientX, clientY))
                    continue;
                if (_ircEventGifPaths.TryGetValue(key, out var previewPath) && !string.IsNullOrWhiteSpace(previewPath))
                    OpenGifPreviewWindow(previewPath);
                return;
            }
            if (Contains(_resetAllGifsButtonRect, clientX, clientY)) { ResetAllGifs(); return; }
        }

        if (_eventBoxColorAdvancedMode)
        {
            foreach (var (key, bounds) in _alertsColorModeButtonRects)
                if (Contains(bounds, clientX, clientY)) { OpenEventColorModeDropdown(key); return; }
            foreach (var (key, bounds) in _alertsColorChooseButtonRects)
            {
                if (!Contains(bounds, clientX, clientY))
                    continue;
                string mode = _eventBoxColorModes.TryGetValue(key, out var m) ? m : "Theme";
                if (mode == "Custom")
                    OpenEventBoxColorPicker(key);
                return;
            }
            if (Contains(_resetAllColorsButtonRect, clientX, clientY)) { ResetAllColors(); return; }
        }
    }

    private void OpenFlashColorPicker()
    {
        ColorPickerWindow.Show(Hwnd, PostToUiThread, _flashColorHex, _flashAlpha, result =>
        {
            if (result is null)
                return;
            _flashColorHex = result.Value.Hex;
            _flashAlpha = result.Value.Alpha;
            Settings.AlertFlashColor = _flashColorHex;
            Settings.AlertFlashAlpha = _flashAlpha;
            RequestRender();
        });
    }

    private void TestFlashColor() => TestFlashRequested?.Invoke(_flashColorHex, _flashAlpha);

    private void BrowseIrcEventGif(string? advancedKey)
    {
        var path = FileDialog.PickImageFile(Hwnd);
        if (path is null)
            return;

        if (advancedKey is null)
        {
            _ircEventGifPath = path;
            Settings.IrcEventGifPath = path;
        }
        else
        {
            _ircEventGifPaths[advancedKey] = path;
            Settings.IrcEventGifPaths = new Dictionary<string, string>(_ircEventGifPaths);
        }
        RequestRender();
    }

    private void ClearAdvancedGif(string key)
    {
        _ircEventGifPaths[key] = "";
        Settings.IrcEventGifPaths = new Dictionary<string, string>(_ircEventGifPaths);
        RequestRender();
    }

    private void ResetAllGifs()
    {
        foreach (var key in _ircEventGifPaths.Keys.ToList())
            _ircEventGifPaths[key] = "";
        Settings.IrcEventGifPaths = new Dictionary<string, string>(_ircEventGifPaths);
        RequestRender();
    }

    private void OpenEventColorModeDropdown(string key)
    {
        if (!_alertsColorModeButtonRects.TryGetValue(key, out var anchorRect))
            return;
        Win32.GetClientRect(Hwnd, out var client);
        _eventColorModeDropdownKey = key;
        var items = new List<Dropdown.Item>
        {
            new() { Label = LocalizationService.T("Settings_Alerts_ColorMode_Theme"), OnSelect = () => SetEventColorMode(key, "Theme") },
            new() { Label = LocalizationService.T("Settings_Alerts_ColorMode_Original"), OnSelect = () => SetEventColorMode(key, "Original") },
            new() { Label = LocalizationService.T("Settings_Alerts_ColorMode_Custom"), OnSelect = () => SetEventColorMode(key, "Custom") },
        };
        _eventColorModeDropdown.Open(anchorRect.Left, anchorRect.Bottom, client.Right - client.Left, client.Bottom - client.Top, items, _fieldFormat!);
        RequestRender();
    }

    private void SetEventColorMode(string key, string mode)
    {
        _eventBoxColorModes[key] = mode;
        Settings.EventBoxColorModes = new Dictionary<string, string>(_eventBoxColorModes);
        RequestRender();
    }

    private void OpenEventBoxColorPicker(string key)
    {
        string defaultHex = AllEventTypesForColor.First(t => t.Key == key).DefaultHex;
        string current = _eventBoxColors.TryGetValue(key, out var existing) && !string.IsNullOrEmpty(existing) ? existing : defaultHex;

        ColorPickerWindow.Show(Hwnd, PostToUiThread, current, 0xFF, result =>
        {
            if (result is null)
                return;
            _eventBoxColors[key] = result.Value.Hex;
            _eventBoxColorModes[key] = "Custom";
            Settings.EventBoxColors = new Dictionary<string, string>(_eventBoxColors);
            Settings.EventBoxColorModes = new Dictionary<string, string>(_eventBoxColorModes);
            RequestRender();
        });
    }

    private void ResetAllColors()
    {
        _eventBoxColorModes.Clear();
        _eventBoxColors.Clear();
        Settings.EventBoxColorModes = new Dictionary<string, string>();
        Settings.EventBoxColors = new Dictionary<string, string>();
        RequestRender();
    }

    protected override void OnMouseWheel(int delta, int clientX, int clientY)
    {
        if (_selectedSection != 4 || _alertsScroll.Overflow <= 0f)
            return;
        const float stepPerNotch = 48f;
        _alertsScroll.ApplyWheel(delta / 120f * stepPerNotch, invert: true);
        RequestRender();
    }

}