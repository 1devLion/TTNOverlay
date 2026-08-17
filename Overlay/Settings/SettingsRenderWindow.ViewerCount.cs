using TTNOverlay.Native;
using TTNOverlay.Overlay.Controls;
using TTNOverlay.Services;
using Vortice.Direct2D1;
using Vortice.DirectWrite;
using Vortice.Mathematics;
using Rect = Vortice.Mathematics.Rect;

namespace TTNOverlay.Overlay;

/// <summary>
/// SettingsRenderWindow partial: the Viewer Count section. Split out of the Twitch API section
/// (SettingsRenderWindow.TwitchApi.cs) because the counter isn't Twitch-only -- it already covers
/// Kick and will cover YouTube, so it doesn't belong under a Twitch-branded settings screen.
/// </summary>
internal sealed partial class SettingsRenderWindow
{
    private bool _showViewerCount;
    private bool _originalShowViewerCount;

    private readonly Dropdown _viewerCountModeDropdown = new();
    private string _viewerCountDisplayMode = "Sum";
    private string _originalViewerCountDisplayMode = "Sum";
    private Rect _viewerCountModeFieldRect;

    private bool _viewerCountIncludeTwitch;
    private bool _viewerCountIncludeKick;
    private bool _viewerCountIncludeYouTube;
    private bool _originalViewerCountIncludeTwitch;
    private bool _originalViewerCountIncludeKick;
    private bool _originalViewerCountIncludeYouTube;

    private readonly TextBox _viewerCountSizeBox = new() { MaxLength = 4 };
    private string _viewerCountColorHex = "";
    private byte _viewerCountColorAlpha = 0xAA;
    private string _originalViewerCountColorHex = "";
    private byte _originalViewerCountColorAlpha;
    private double _originalViewerCountSize;
    private Rect _viewerCountColorSwatchRect;
    private Rect _pickViewerCountColorButtonRect;
    private Rect _resetViewerCountColorButtonRect;
    private Rect _viewerCountSizeFieldRect;

    private string _viewerCountTextColorHex = "";
    private string _originalViewerCountTextColorHex = "";
    private Rect _viewerCountTextColorSwatchRect;
    private Rect _pickViewerCountTextColorButtonRect;
    private Rect _resetViewerCountTextColorButtonRect;

    private void InitViewerCount()
    {
        _showViewerCount = Settings.ShowViewerCount;
        _viewerCountColorHex = Settings.ViewerCountBackgroundColor;
        _viewerCountColorAlpha = Settings.ViewerCountBackgroundAlpha;
        _viewerCountTextColorHex = Settings.ViewerCountTextColor;
        _viewerCountSizeBox.Text = Settings.ViewerCountSize.ToString(System.Globalization.CultureInfo.InvariantCulture);
        _viewerCountDisplayMode = Settings.ViewerCountDisplayMode;
        _viewerCountIncludeTwitch = Settings.ViewerCountIncludeTwitch;
        _viewerCountIncludeKick = Settings.ViewerCountIncludeKick;
        _viewerCountIncludeYouTube = Settings.ViewerCountIncludeYouTube;

        _originalShowViewerCount = _showViewerCount;
        _originalViewerCountColorHex = _viewerCountColorHex;
        _originalViewerCountColorAlpha = _viewerCountColorAlpha;
        _originalViewerCountTextColorHex = _viewerCountTextColorHex;
        _originalViewerCountSize = Settings.ViewerCountSize;
        _originalViewerCountDisplayMode = _viewerCountDisplayMode;
        _originalViewerCountIncludeTwitch = _viewerCountIncludeTwitch;
        _originalViewerCountIncludeKick = _viewerCountIncludeKick;
        _originalViewerCountIncludeYouTube = _viewerCountIncludeYouTube;
    }

    private void RevertViewerCount()
    {
        Settings.ShowViewerCount = _originalShowViewerCount;
        Settings.ViewerCountBackgroundColor = _originalViewerCountColorHex;
        Settings.ViewerCountBackgroundAlpha = _originalViewerCountColorAlpha;
        Settings.ViewerCountTextColor = _originalViewerCountTextColorHex;
        Settings.ViewerCountSize = _originalViewerCountSize;
        Settings.ViewerCountDisplayMode = _originalViewerCountDisplayMode;
        Settings.ViewerCountIncludeTwitch = _originalViewerCountIncludeTwitch;
        Settings.ViewerCountIncludeKick = _originalViewerCountIncludeKick;
        Settings.ViewerCountIncludeYouTube = _originalViewerCountIncludeYouTube;
    }

    private void DrawViewerCountSection(ID2D1DCRenderTarget target, float x, float width)
    {
        float y = TitleBarHeight + Padding;

        // Header height is measured instead of hard-coded: verbose languages (e.g. Spanish "Contador
        // de espectadores") can wrap to more than one line, and a fixed 24px box would let the second
        // line spill down into the row below it.
        using (var header = DWriteFactory.CreateTextLayout(LocalizationService.T("Settings_Section_ViewerCount"), _headerFormat!, width, 1000f))
        {
            target.DrawTextLayout(new System.Numerics.Vector2(x, y), header, _textBrush!);
            y += header.Metrics.Height + LabelGap + 6f;
        }

        // The mode dropdown's box also needs to fit its own label text ("Mostrar espectadores como"
        // in Spanish is longer than the English source string), so it's sized from the actual
        // measured text instead of a fixed pixel width.
        _viewerCountModeDropdown.Width = ComputeViewerCountModeDropdownWidth(width);

        _checkboxRects.Clear();
        y = DrawCheckboxField(target, x, width, y, "Settings_TwitchApi_ShowViewerCount", _showViewerCount, "ShowViewerCount");
        y = DrawDropdownField(target, x, width, ref y, "Settings_TwitchApi_ViewerCountMode", _viewerCountModeDropdown, ViewerCountModeLabel(_viewerCountDisplayMode), out _viewerCountModeFieldRect);

        // The per-platform checkboxes only make sense in "Personalizado" mode. Showing them while
        // "Total" is selected is misleading (they'd look like they still apply), so they're only
        // drawn -- and therefore only clickable, since _checkboxRects stays empty otherwise -- when
        // the custom/per-platform mode is active.
        if (_viewerCountDisplayMode == "PerPlatform")
        {
            y = DrawCheckboxField(target, x, width, y, "Settings_TwitchApi_ViewerCountIncludeTwitch", _viewerCountIncludeTwitch, "ViewerCountIncludeTwitch");
            y = DrawCheckboxField(target, x, width, y, "Settings_TwitchApi_ViewerCountIncludeKick", _viewerCountIncludeKick, "ViewerCountIncludeKick");
            y = DrawCheckboxField(target, x, width, y, "Settings_TwitchApi_ViewerCountIncludeYouTube", _viewerCountIncludeYouTube, "ViewerCountIncludeYouTube");
        }

        y = DrawViewerCountColorRow(target, x, width, y);
        y = DrawViewerCountTextColorRow(target, x, width, y);
        y = DrawTextField(target, x, width, y, "Settings_TwitchApi_ViewerCountSize", _viewerCountSizeBox, out _viewerCountSizeFieldRect, out _, enabled: _showViewerCount, fieldWidth: 120f);
    }

    /// <summary>
    /// Sizes the mode dropdown (both its closed box and its open item list, which share
    /// <see cref="Dropdown.Width"/>) so the widest of the current-language option labels fits without
    /// wrapping or spilling past the box's right edge. Clamped to the available section width so it
    /// never pushes past the settings window itself.
    /// </summary>
    private float ComputeViewerCountModeDropdownWidth(float maxWidth)
    {
        float sumLabelWidth = MeasureTextWidth(LocalizationService.T("Settings_ViewerCountMode_Sum"));
        float perPlatformLabelWidth = MeasureTextWidth(LocalizationService.T("Settings_ViewerCountMode_PerPlatform"));
        float widestLabel = System.Math.Max(sumLabelWidth, perPlatformLabelWidth);

        // Closed box needs room for left padding + text + gap + dropdown arrow; the open item list
        // needs left/right padding around the text. Use the larger of the two requirements.
        float closedBoxNeeded = 8f + widestLabel + DropdownArrowGap + DropdownArrowWidth + 8f;
        float openItemNeeded = widestLabel + 24f;
        float needed = System.Math.Max(closedBoxNeeded, openItemNeeded);

        return System.Math.Clamp(needed, 200f, maxWidth);
    }

    private float DrawViewerCountColorRow(ID2D1DCRenderTarget target, float x, float width, float y)
    {
        using (var label = DWriteFactory.CreateTextLayout(LocalizationService.T("Settings_TwitchApi_ViewerCountBackground"), _labelFormat!, width, 18f))
            target.DrawTextLayout(new System.Numerics.Vector2(x, y), label, _secondaryBrush!);
        y += 18f + LabelGap;

        bool enabled = _showViewerCount;
        bool hasCustomColor = !string.IsNullOrWhiteSpace(_viewerCountColorHex);
        string swatchHex = hasCustomColor ? _viewerCountColorHex : (ThemeService.IsDark ? "#000000" : "#F2F2F2");

        _viewerCountColorSwatchRect = new Rect(x, y, 24f, 24f);
        if (ColorPickerWindow.TryParseHex(swatchHex, out var cr, out var cg, out var cb))
        {
            using var swatchBrush = target.CreateSolidColorBrush(new Color4(cr / 255f, cg / 255f, cb / 255f, enabled ? _viewerCountColorAlpha / 255f : 0.3f));
            target.FillRectangle(_viewerCountColorSwatchRect, swatchBrush);
        }
        target.DrawRectangle(_viewerCountColorSwatchRect, _fieldBorderBrush!, 1f);

        string pickLabel = LocalizationService.T("Settings_Alerts_PickColor");
        string resetLabel = LocalizationService.T("Settings_TwitchApi_ResetViewerCountColor");
        float rowBottom = LayoutColorRowButtons(pickLabel, resetLabel, x, y, 24f, out _pickViewerCountColorButtonRect, out _resetViewerCountColorButtonRect);
        DrawFooterButton(target, _pickViewerCountColorButtonRect, pickLabel, primary: false, enabled: enabled);
        DrawFooterButton(target, _resetViewerCountColorButtonRect, resetLabel, primary: false, enabled: enabled && hasCustomColor);

        return rowBottom + FieldGap;
    }

    private float DrawViewerCountTextColorRow(ID2D1DCRenderTarget target, float x, float width, float y)
    {
        using (var label = DWriteFactory.CreateTextLayout(LocalizationService.T("Settings_TwitchApi_ViewerCountTextColor"), _labelFormat!, width, 18f))
            target.DrawTextLayout(new System.Numerics.Vector2(x, y), label, _secondaryBrush!);
        y += 18f + LabelGap;

        bool enabled = _showViewerCount;
        bool hasCustomColor = !string.IsNullOrWhiteSpace(_viewerCountTextColorHex);
        string swatchHex = hasCustomColor ? _viewerCountTextColorHex : (ThemeService.IsDark ? "#FFFFFF" : "#000000");

        _viewerCountTextColorSwatchRect = new Rect(x, y, 24f, 24f);
        if (ColorPickerWindow.TryParseHex(swatchHex, out var cr, out var cg, out var cb))
        {
            using var swatchBrush = target.CreateSolidColorBrush(new Color4(cr / 255f, cg / 255f, cb / 255f, enabled ? 1f : 0.3f));
            target.FillRectangle(_viewerCountTextColorSwatchRect, swatchBrush);
        }
        target.DrawRectangle(_viewerCountTextColorSwatchRect, _fieldBorderBrush!, 1f);

        string pickLabel = LocalizationService.T("Settings_Alerts_PickColor");
        string resetLabel = LocalizationService.T("Settings_TwitchApi_ResetViewerCountColor");
        float rowBottom = LayoutColorRowButtons(pickLabel, resetLabel, x, y, 24f, out _pickViewerCountTextColorButtonRect, out _resetViewerCountTextColorButtonRect);
        DrawFooterButton(target, _pickViewerCountTextColorButtonRect, pickLabel, primary: false, enabled: enabled);
        DrawFooterButton(target, _resetViewerCountTextColorButtonRect, resetLabel, primary: false, enabled: enabled && hasCustomColor);

        return rowBottom + FieldGap;
    }

    private void OpenViewerCountTextColorPicker()
    {
        string current = string.IsNullOrWhiteSpace(_viewerCountTextColorHex)
            ? (ThemeService.IsDark ? "#FFFFFF" : "#000000")
            : _viewerCountTextColorHex;

        ColorPickerWindow.Show(Hwnd, PostToUiThread, current, 0xFF, result =>
        {
            if (result is null)
                return;
            _viewerCountTextColorHex = result.Value.Hex;
            Settings.ViewerCountTextColor = _viewerCountTextColorHex;
            RequestRender();
        });
    }

    private void ResetViewerCountTextColor()
    {
        _viewerCountTextColorHex = "";
        Settings.ViewerCountTextColor = "";
        RequestRender();
    }

    private void OpenViewerCountColorPicker()
    {
        string current = string.IsNullOrWhiteSpace(_viewerCountColorHex)
            ? (ThemeService.IsDark ? "#000000" : "#F2F2F2")
            : _viewerCountColorHex;

        ColorPickerWindow.Show(Hwnd, PostToUiThread, current, _viewerCountColorAlpha, result =>
        {
            if (result is null)
                return;
            _viewerCountColorHex = result.Value.Hex;
            _viewerCountColorAlpha = result.Value.Alpha;
            Settings.ViewerCountBackgroundColor = _viewerCountColorHex;
            Settings.ViewerCountBackgroundAlpha = _viewerCountColorAlpha;
            RequestRender();
        });
    }

    private void ResetViewerCountColor()
    {
        _viewerCountColorHex = "";
        Settings.ViewerCountBackgroundColor = "";
        RequestRender();
    }

    private void CommitViewerCountSize()
    {
        if (double.TryParse(_viewerCountSizeBox.Text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var size))
            Settings.ViewerCountSize = System.Math.Clamp(size, 8, 32);
    }

    private static string ViewerCountModeLabel(string mode) => mode switch
    {
        "PerPlatform" => LocalizationService.T("Settings_ViewerCountMode_PerPlatform"),
        _ => LocalizationService.T("Settings_ViewerCountMode_Sum"),
    };

    private void OpenViewerCountModeDropdown()
    {
        Win32.GetClientRect(Hwnd, out var client);
        _viewerCountModeDropdown.Open(_viewerCountModeFieldRect.Left, _viewerCountModeFieldRect.Bottom, client.Right - client.Left, client.Bottom - client.Top,
            new List<Dropdown.Item>
            {
                new() { Label = LocalizationService.T("Settings_ViewerCountMode_Sum"), OnSelect = () => SetViewerCountDisplayMode("Sum") },
                new() { Label = LocalizationService.T("Settings_ViewerCountMode_PerPlatform"), OnSelect = () => SetViewerCountDisplayMode("PerPlatform") },
            },
            _fieldFormat!);
        RequestRender();
    }

    private void SetViewerCountDisplayMode(string mode)
    {
        _viewerCountDisplayMode = mode;
        Settings.ViewerCountDisplayMode = mode;
    }

    private void HandleViewerCountSectionClick(int clientX, int clientY)
    {
        if (Contains(_viewerCountModeFieldRect, clientX, clientY))
        {
            OpenViewerCountModeDropdown();
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

        if (_showViewerCount)
        {
            if (Contains(_pickViewerCountColorButtonRect, clientX, clientY)) { OpenViewerCountColorPicker(); return; }
            if (!string.IsNullOrWhiteSpace(_viewerCountColorHex) && Contains(_resetViewerCountColorButtonRect, clientX, clientY)) { ResetViewerCountColor(); return; }
            if (Contains(_pickViewerCountTextColorButtonRect, clientX, clientY)) { OpenViewerCountTextColorPicker(); return; }
            if (!string.IsNullOrWhiteSpace(_viewerCountTextColorHex) && Contains(_resetViewerCountTextColorButtonRect, clientX, clientY)) { ResetViewerCountTextColor(); return; }
        }
    }
}