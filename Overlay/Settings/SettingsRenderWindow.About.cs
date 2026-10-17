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
    private Rect _checkUpdatesButtonRect;
    private Rect _updateNowButtonRect;
    private Rect _debugLogGoToButtonRect;

    private enum UpdateUiState
    {
        Idle,
        Checking,
        UpToDate,
        Available,
        Failed,
        NotInstalled,
    }

    private UpdateUiState _updateState = UpdateUiState.Idle;
    private string _availableVersion = "";

    private const float AboutUpdateLineHeight = 26f;
    private const float AboutLogGoToWidth = 90f;
    private const float AboutLogRowMaxWidth = 480f;

    /// <summary>"Update now" was pressed: the overlay window runs the update prompt.</summary>
    public event Action? UpdateRequested;

    /// <summary>The user chose "Restart now" after changing debug mode: the overlay restarts once the settings are saved.</summary>
    public bool RestartRequested { get; private set; }
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
            32f + 8f + AboutUpdateLineHeight + 18f +
            20f +
            (22f + 6f) * 2 +
            6f +
            licenseTextHeight +
            20f + CheckboxSize + FieldGap + 6f + debugWarningHeight +
            12f + 18f + LabelGap + FieldHeight;

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

        // Support and Check for updates, side by side, with the result of the check under them.
        const float aboutButtonWidth = 170f;
        const float aboutButtonGap = 12f;
        float buttonsX = x + (width - (aboutButtonWidth * 2f + aboutButtonGap)) / 2f;
        _supportButtonRect = new Rect(buttonsX, y, aboutButtonWidth, 32f);
        _checkUpdatesButtonRect = new Rect(buttonsX + aboutButtonWidth + aboutButtonGap, y, aboutButtonWidth, 32f);
        DrawFooterButton(target, _supportButtonRect, LocalizationService.T("Settings_About_SupportUs"), primary: false);
        DrawFooterButton(
            target,
            _checkUpdatesButtonRect,
            LocalizationService.T(_updateState == UpdateUiState.Checking ? "Settings_About_Checking" : "Settings_About_CheckUpdates"),
            primary: false,
            enabled: _updateState != UpdateUiState.Checking
        );
        y += 32f + 8f;

        DrawAboutUpdateStatus(target, x, width, y);
        y += AboutUpdateLineHeight + 18f;

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
        y += debugWarningHeight + 12f;

        DrawAboutDebugLogRow(target, x, width, y);
    }

    /// <summary>The result of the last update check, centered; with an "Update now" button when there is a new version.</summary>
    private void DrawAboutUpdateStatus(ID2D1DCRenderTarget target, float x, float width, float y)
    {
        _updateNowButtonRect = default;

        string? text = _updateState switch
        {
            UpdateUiState.UpToDate => LocalizationService.T("Settings_About_UpToDate"),
            UpdateUiState.Available => string.Format(LocalizationService.T("Settings_About_UpdateAvailable"), _availableVersion),
            UpdateUiState.Failed => LocalizationService.T("Settings_About_UpdateFailed"),
            UpdateUiState.NotInstalled => LocalizationService.T("Settings_About_UpdateNotInstalled"),
            _ => null,
        };
        if (text is null)
            return;

        bool problem = _updateState is UpdateUiState.Failed;
        var brush = problem ? (_warningBrush ??= target.CreateSolidColorBrush(new Color4(0.85f, 0.55f, 0.15f, 1f))) : _secondaryBrush!;
        float textWidth = MeasureAboutText(text, _aboutSmallCenterFormat!);
        textWidth = System.Math.Min(textWidth, width);

        const float nowGap = 10f;
        Rect nowRect = default;
        float totalWidth = textWidth;
        if (_updateState == UpdateUiState.Available)
        {
            nowRect = MeasureButtonRect(LocalizationService.T("Settings_About_UpdateNow"), 0f, 0f, AboutUpdateLineHeight, 90f);
            totalWidth += nowGap + nowRect.Width;
        }

        float startX = x + (width - totalWidth) / 2f;
        using (var layout = DWriteFactory.CreateTextLayout(text, _aboutSmallCenterFormat!, textWidth + 4f, AboutUpdateLineHeight))
        {
            layout.TextAlignment = Vortice.DirectWrite.TextAlignment.Leading;
            target.DrawTextLayout(new System.Numerics.Vector2(startX, y + (AboutUpdateLineHeight - (float)layout.Metrics.Height) / 2f), layout, brush);
        }

        if (_updateState == UpdateUiState.Available)
        {
            _updateNowButtonRect = new Rect(startX + textWidth + nowGap, y, nowRect.Width, AboutUpdateLineHeight);
            DrawFooterButton(target, _updateNowButtonRect, LocalizationService.T("Settings_About_UpdateNow"), primary: true);
        }
    }

    /// <summary>Where the debug log is, with a button that opens Explorer there.</summary>
    private void DrawAboutDebugLogRow(ID2D1DCRenderTarget target, float x, float width, float y)
    {
        float rowWidth = System.Math.Min(width, AboutLogRowMaxWidth);
        float rowX = x + (width - rowWidth) / 2f;

        using (var label = DWriteFactory.CreateTextLayout(LocalizationService.T("Settings_About_DebugLogLocation"), _labelFormat!, rowWidth, 18f))
            target.DrawTextLayout(new System.Numerics.Vector2(rowX, y), label, _secondaryBrush!);
        y += 18f + LabelGap;

        _debugLogGoToButtonRect = new Rect(rowX + rowWidth - AboutLogGoToWidth, y, AboutLogGoToWidth, FieldHeight);
        var pathRect = new Rect(rowX, y, System.Math.Max(1f, rowWidth - AboutLogGoToWidth - FieldGap), FieldHeight);

        target.FillRectangle(pathRect, _fieldBackgroundBrush!);
        target.DrawRectangle(pathRect, _fieldBorderBrush!, 1f);

        // Long paths: keep the end visible (the file name is the part that matters).
        string path = DebugLog.FilePath;
        float available = pathRect.Width - 16f;
        target.PushAxisAlignedClip(pathRect, AntialiasMode.PerPrimitive);
        using (var pathLayout = DWriteFactory.CreateTextLayout(path, _fieldFormat!, 4000f, pathRect.Height))
        {
            float textWidth = (float)pathLayout.Metrics.WidthIncludingTrailingWhitespace;
            float shift = System.Math.Max(0f, textWidth - available);
            target.DrawTextLayout(new System.Numerics.Vector2(pathRect.Left + 8f - shift, pathRect.Top), pathLayout, _secondaryBrush!);
        }
        target.PopAxisAlignedClip();

        DrawFooterButton(target, _debugLogGoToButtonRect, LocalizationService.T("Settings_About_GoTo"), primary: false);
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

        if (Contains(_checkUpdatesButtonRect, clientX, clientY))
        {
            if (_updateState != UpdateUiState.Checking)
                StartUpdateCheck();
            return;
        }

        if (_updateState == UpdateUiState.Available && Contains(_updateNowButtonRect, clientX, clientY))
        {
            UpdateRequested?.Invoke();
            return;
        }

        if (Contains(_debugLogGoToButtonRect, clientX, clientY))
        {
            OpenDebugLogLocation();
            return;
        }

        foreach (var (bounds, field) in _checkboxRects)
        {
            if (Contains(bounds, clientX, clientY))
            {
                ToggleCheckbox(field);
                RequestRender();

                // Debug mode is read once at startup: offer to restart (not when it went back to how it was).
                if (field == "DebugMode" && _debugMode != _originalDebugMode)
                    PromptRestartForDebugMode();
                return;
            }
        }
    }

    private void StartUpdateCheck()
    {
        _updateState = UpdateUiState.Checking;
        RequestRender();

        _ = Task.Run(async () =>
        {
            var result = await UpdateService.CheckForUpdateAsync();
            PostToUiThread(() =>
            {
                _availableVersion = result.Version;
                _updateState = result.State switch
                {
                    UpdateService.UpdateCheckState.Available => UpdateUiState.Available,
                    UpdateService.UpdateCheckState.UpToDate => UpdateUiState.UpToDate,
                    UpdateService.UpdateCheckState.NotInstalled => UpdateUiState.NotInstalled,
                    _ => UpdateUiState.Failed,
                };
                RequestRender();
            });
        });
    }

    private void PromptRestartForDebugMode()
    {
        ConfirmDialogWindow.Show(
            Hwnd,
            action => PostToUiThread(action),
            LocalizationService.T("Settings_Restart_Title"),
            LocalizationService.T("Settings_Restart_Message"),
            LocalizationService.T("Settings_Restart_Now"),
            restartNow =>
            {
                if (!restartNow)
                    return; // "Later": the choice stays in the settings and applies the next time the app starts.

                RestartRequested = true;
                CloseKeepingChanges();
            },
            destructive: false,
            cancelText: LocalizationService.T("Settings_Restart_Later")
        );
    }

    /// <summary>Opens Explorer on the debug log (selecting it), or on its folder when there is no log yet.</summary>
    private static void OpenDebugLogLocation()
    {
        try
        {
            string path = DebugLog.FilePath;
            if (System.IO.File.Exists(path))
            {
                System.Diagnostics.Process.Start(
                    new System.Diagnostics.ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true }
                );
                return;
            }

            string? folder = System.IO.Path.GetDirectoryName(path);
            if (folder is null)
                return;
            System.IO.Directory.CreateDirectory(folder);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(folder) { UseShellExecute = true });
        }
        catch
        {
            // Nothing to show the user: Explorer just does not open.
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