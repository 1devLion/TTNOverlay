using TTNOverlay.Native;
using TTNOverlay.Overlay.Controls;
using TTNOverlay.Services;
using Vortice.Direct2D1;
using Vortice.DirectWrite;
using Vortice.Mathematics;
using Rect = Vortice.Mathematics.Rect;

namespace TTNOverlay.Overlay;

/// <summary>
/// SettingsRenderWindow partial: the About section (version info and links).
/// </summary>
internal sealed partial class SettingsRenderWindow
{

    private const string SupportUrl = "https://ko-fi.com/1devlion/donate";

    private const float AboutIconSize = 96f;
    private const float AboutSeparatorWidth = 280f;
    private const float AboutLicenseTextMaxWidth = 360f;

    private Rect _supportButtonRect;

    private ID2D1Bitmap? _aboutIconBitmap;
    private bool _aboutIconLoadAttempted;

    private Rect _debugModeCheckboxRect;
    private ID2D1Bitmap? GetOrCreateAboutIconBitmap(ID2D1DCRenderTarget target)
    {
        if (_aboutIconBitmap is not null || _aboutIconLoadAttempted)
            return _aboutIconBitmap;
        _aboutIconLoadAttempted = true;

        var decoded = AppIconLoader.GetDecodedIcon();
        if (decoded is null)
            return null;

        try
        {
            _aboutIconBitmap = D2DBitmapLoader.CreateBitmap(target, decoded.Value, "AppIcon");
        }
        catch
        {

        }
        return _aboutIconBitmap;
    }

    private IDWriteTextFormat? _aboutTitleFormat;
    private IDWriteTextFormat? _aboutCenterFormat;
    private IDWriteTextFormat? _aboutBoldCenterFormat;
    private IDWriteTextFormat? _aboutSmallCenterFormat;
    private ID2D1SolidColorBrush? _warningBrush;

    private static string AppVersionText
    {
        get
        {
            var v = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            return v is null ? "?" : $"{v.Major}.{v.Minor}.{v.Build}";
        }
    }

    private void DrawAboutSection(ID2D1DCRenderTarget target, float x, float width, float windowHeight)
    {
        _checkboxRects.Clear();
        _aboutTitleFormat ??= DWriteFactory.CreateTextFormat("Segoe UI", FontWeight.Bold, Vortice.DirectWrite.FontStyle.Normal, 26f);
        _aboutTitleFormat.TextAlignment = Vortice.DirectWrite.TextAlignment.Center;

        _aboutCenterFormat ??= DWriteFactory.CreateTextFormat("Segoe UI", FontWeight.Normal, Vortice.DirectWrite.FontStyle.Normal, 16f);
        _aboutCenterFormat.TextAlignment = Vortice.DirectWrite.TextAlignment.Center;

        _aboutBoldCenterFormat ??= DWriteFactory.CreateTextFormat("Segoe UI", FontWeight.Bold, Vortice.DirectWrite.FontStyle.Normal, 16f);
        _aboutBoldCenterFormat.TextAlignment = Vortice.DirectWrite.TextAlignment.Center;

        _aboutSmallCenterFormat ??= DWriteFactory.CreateTextFormat("Segoe UI", FontWeight.Normal, Vortice.DirectWrite.FontStyle.Normal, 13f);
        _aboutSmallCenterFormat.TextAlignment = Vortice.DirectWrite.TextAlignment.Center;
        _aboutSmallCenterFormat.WordWrapping = Vortice.DirectWrite.WordWrapping.Wrap;

        _windowBackgroundBrushInverse ??= target.CreateSolidColorBrush(new Color4(1f, 1f, 1f, 1f));

        float licenseTextWidth = System.Math.Min(width, AboutLicenseTextMaxWidth);
        float licenseTextHeight;
        using (var probe = DWriteFactory.CreateTextLayout(LocalizationService.T("Settings_About_LicenseText"), _aboutSmallCenterFormat!, licenseTextWidth, 200f))
            licenseTextHeight = probe.Metrics.Height;

        float debugWarningWidth = System.Math.Min(width, AboutLicenseTextMaxWidth);
        float debugWarningHeight;
        using (var probe = DWriteFactory.CreateTextLayout(LocalizationService.T("Settings_About_DebugModeWarning"), _aboutSmallCenterFormat!, debugWarningWidth, 200f))
            debugWarningHeight = probe.Metrics.Height;

        float contentHeight =
            AboutIconSize + 16f +
            32f + 4f +
            20f + 24f +
            32f + 24f +
            20f +
            (22f + 6f) * 2 +
            6f +
            licenseTextHeight +
            20f + CheckboxSize + FieldGap + 6f + debugWarningHeight;

        float availableTop = TitleBarHeight;
        float availableBottom = windowHeight - FooterHeight;
        float y = availableTop + System.Math.Max(Padding, (availableBottom - availableTop - contentHeight) / 2f);

        var iconRect = new Rect(x + (width - AboutIconSize) / 2f, y, AboutIconSize, AboutIconSize);
        var iconBitmap = GetOrCreateAboutIconBitmap(target);
        if (iconBitmap is not null)
        {
            target.DrawBitmap(
                bitmap: iconBitmap,
                destinationRectangle: iconRect,
                opacity: 1f,
                interpolationMode: BitmapInterpolationMode.Linear,
                sourceRectangle: new Rect(0, 0, iconBitmap.Size.Width, iconBitmap.Size.Height)
            );
        }
        else
        {

            target.FillRectangle(iconRect, _checkboxBrush!);
            using var iconText = DWriteFactory.CreateTextLayout("TTN", _aboutTitleFormat!, AboutIconSize, AboutIconSize);
            target.DrawTextLayout(new System.Numerics.Vector2(iconRect.Left, iconRect.Top + (AboutIconSize - 22f) / 2f), iconText, _windowBackgroundBrushInverse!);
        }
        y += AboutIconSize + 16f;

        using (var title = DWriteFactory.CreateTextLayout("TTN Overlay", _aboutTitleFormat!, width, 36f))
            target.DrawTextLayout(new System.Numerics.Vector2(x, y), title, _textBrush!);
        y += 36f + 4f;

        string versionText = string.Format(
            LocalizationService.T("Settings_About_VersionFormat"),
            AppVersionText,
            System.Environment.Is64BitProcess ? 64 : 32);
        using (var version = DWriteFactory.CreateTextLayout(versionText, _aboutCenterFormat!, width, 22f))
            target.DrawTextLayout(new System.Numerics.Vector2(x, y), version, _secondaryBrush!);
        y += 22f + 24f;

        const float supportButtonWidth = 170f;
        _supportButtonRect = new Rect(x + (width - supportButtonWidth) / 2f, y, supportButtonWidth, 32f);
        DrawFooterButton(target, _supportButtonRect, LocalizationService.T("Settings_About_SupportUs"), primary: false);
        y += 32f + 24f;

        float sepX = x + (width - AboutSeparatorWidth) / 2f;
        target.DrawLine(new System.Numerics.Vector2(sepX, y), new System.Numerics.Vector2(sepX + AboutSeparatorWidth, y), _fieldBorderBrush!, 1f);
        y += 20f;

        DrawAboutCenteredLine(target, x, width, ref y, LocalizationService.T("Settings_About_Author"), "1devLion");
        DrawAboutCenteredLine(target, x, width, ref y, LocalizationService.T("Settings_About_License"), "MIT");
        y += 2f;

        float licenseTextX = x + (width - licenseTextWidth) / 2f;
        using (var licenseText = DWriteFactory.CreateTextLayout(LocalizationService.T("Settings_About_LicenseText"), _aboutSmallCenterFormat!, licenseTextWidth, 90f))
            target.DrawTextLayout(new System.Numerics.Vector2(licenseTextX, y), licenseText, _secondaryBrush!);
        y += licenseTextHeight + 20f;

        float debugLabelWidth = MeasureAboutText(LocalizationService.T("Settings_About_DebugMode"), _labelFormat!);
        float debugFieldWidth = CheckboxSize + CheckboxLabelGap + debugLabelWidth;
        float checkboxRowX = x + (width - debugFieldWidth) / 2f;
        y = DrawCheckboxField(target, checkboxRowX, debugFieldWidth, y, "Settings_About_DebugMode", _debugMode, "DebugMode");
        _debugModeCheckboxRect = _checkboxRects[^1].Bounds;

        float warningX = x + (width - debugWarningWidth) / 2f;
        using (var warningText = DWriteFactory.CreateTextLayout(LocalizationService.T("Settings_About_DebugModeWarning"), _aboutSmallCenterFormat!, debugWarningWidth, debugWarningHeight + 4f))
            target.DrawTextLayout(new System.Numerics.Vector2(warningX, y), warningText, _warningBrush ??= target.CreateSolidColorBrush(new Color4(0.85f, 0.55f, 0.15f, 1f)));
    }

    private void DrawAboutCenteredLine(ID2D1DCRenderTarget target, float x, float width, ref float y, string label, string value)
    {
        float labelWidth = MeasureAboutText(label, _aboutCenterFormat!);
        float valueWidth = MeasureAboutText(value, _aboutBoldCenterFormat!);
        float startX = x + (width - labelWidth - valueWidth) / 2f;

        using (var labelLayout = DWriteFactory.CreateTextLayout(label, _aboutCenterFormat!, labelWidth + 4f, 20f))
            target.DrawTextLayout(new System.Numerics.Vector2(startX, y), labelLayout, _secondaryBrush!);

        using (var valueLayout = DWriteFactory.CreateTextLayout(value, _aboutBoldCenterFormat!, valueWidth + 4f, 20f))
            target.DrawTextLayout(new System.Numerics.Vector2(startX + labelWidth, y), valueLayout, _textBrush!);

        y += 20f + 4f;
    }

    private float MeasureAboutText(string text, IDWriteTextFormat format)
    {
        using var layout = DWriteFactory.CreateTextLayout(text, format, 2000f, 20f);
        return layout.Metrics.WidthIncludingTrailingWhitespace;
    }
    private void HandleAboutSectionClick(int clientX, int clientY)
    {
        if (Contains(_supportButtonRect, clientX, clientY))
        {
            OpenSupportLink();
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

    private static void OpenSupportLink()
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(SupportUrl) { UseShellExecute = true });
        }
        catch
        {

        }
    }
}