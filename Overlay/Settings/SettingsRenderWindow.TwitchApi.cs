using TTNOverlay.Native;
using TTNOverlay.Overlay.Controls;
using TTNOverlay.Services;
using Vortice.Direct2D1;
using Vortice.DirectWrite;
using Vortice.Mathematics;
using Rect = Vortice.Mathematics.Rect;

namespace TTNOverlay.Overlay;

/// <summary>
/// SettingsRenderWindow partial: the Twitch API section (client credentials and moderator login).
/// </summary>
internal sealed partial class SettingsRenderWindow
{

    private readonly IModerationService _moderation;
    private bool _enableTwitchApi;
    private bool _showViewerCount;
    private bool _showBadges;
    private bool _originalEnableTwitchApi;
    private bool _originalShowViewerCount;
    private bool _originalShowBadges;

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

    private string? _twitchLoginTransientStatus;
    private bool _twitchLoginBusy;

    private CancellationTokenSource? _twitchLoginCts;
    private Rect _twitchLoginActionRect;

    private void InitTwitchApi()
    {
        _enableTwitchApi = Settings.EnableTwitchApi;
        _showViewerCount = Settings.ShowViewerCount;
        _showBadges = Settings.ShowBadges;
        _viewerCountColorHex = Settings.ViewerCountBackgroundColor;
        _viewerCountColorAlpha = Settings.ViewerCountBackgroundAlpha;
        _viewerCountSizeBox.Text = Settings.ViewerCountSize.ToString(System.Globalization.CultureInfo.InvariantCulture);

        _originalEnableTwitchApi = _enableTwitchApi;
        _originalShowViewerCount = _showViewerCount;
        _originalShowBadges = _showBadges;
        _originalViewerCountColorHex = _viewerCountColorHex;
        _originalViewerCountColorAlpha = _viewerCountColorAlpha;
        _originalViewerCountSize = Settings.ViewerCountSize;
    }

    private void RevertTwitchApi()
    {
        Settings.EnableTwitchApi = _originalEnableTwitchApi;
        Settings.ShowViewerCount = _originalShowViewerCount;
        Settings.ShowBadges = _originalShowBadges;
        Settings.ViewerCountBackgroundColor = _originalViewerCountColorHex;
        Settings.ViewerCountBackgroundAlpha = _originalViewerCountColorAlpha;
        Settings.ViewerCountSize = _originalViewerCountSize;
    }

    private void DrawTwitchApiSection(ID2D1DCRenderTarget target, float x, float width)
    {
        float y = TitleBarHeight + Padding;

        using (var header = DWriteFactory.CreateTextLayout(LocalizationService.T("Settings_Section_TwitchApi"), _headerFormat!, width, 24f))
            target.DrawTextLayout(new System.Numerics.Vector2(x, y), header, _textBrush!);
        y += 28f;

        using (var info = DWriteFactory.CreateTextLayout(LocalizationService.T("Settings_TwitchApi_LoginInfo"), _labelFormat!, width, 32f))
            target.DrawTextLayout(new System.Numerics.Vector2(x, y), info, _secondaryBrush!);
        y += 40f;

        _checkboxRects.Clear();
        y = DrawCheckboxField(target, x, width, y, "Settings_TwitchApi_Enable", _enableTwitchApi, "EnableTwitchApi");
        y = DrawCheckboxField(target, x, width, y, "Settings_TwitchApi_ShowViewerCount", _showViewerCount, "ShowViewerCount");
        y = DrawViewerCountColorRow(target, x, width, y);
        y = DrawTextField(target, x, System.Math.Min(width, 120f), y, "Settings_TwitchApi_ViewerCountSize", _viewerCountSizeBox, out _viewerCountSizeFieldRect, out _, enabled: _showViewerCount);
        y = DrawCheckboxField(target, x, width, y, "Settings_TwitchApi_ShowBadges", _showBadges, "ShowBadges");
        y += FieldGap;

        string statusText = _twitchLoginTransientStatus ?? (
            _moderation.IsLoggedIn
                ? string.Format(LocalizationService.T("Settings_TwitchApi_Connected"), _moderation.ModeratorLogin)
                : LocalizationService.T("Settings_TwitchApi_NotLoggedIn"));
        using (var status = DWriteFactory.CreateTextLayout(statusText, _labelFormat!, width, 18f))
            target.DrawTextLayout(new System.Numerics.Vector2(x, y), status, _secondaryBrush!);
        y += 18f + LabelGap + 4f;

        DrawTwitchLoginButton(target, x, y);
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

        _pickViewerCountColorButtonRect = new Rect(x + 24f + 8f, y, 90f, 24f);
        DrawFooterButton(target, _pickViewerCountColorButtonRect, LocalizationService.T("Settings_Alerts_PickColor"), primary: false, enabled: enabled);

        _resetViewerCountColorButtonRect = new Rect(_pickViewerCountColorButtonRect.Right + 8f, y, 130f, 24f);
        DrawFooterButton(target, _resetViewerCountColorButtonRect, LocalizationService.T("Settings_TwitchApi_ResetViewerCountColor"), primary: false, enabled: enabled && hasCustomColor);

        return y + 24f + FieldGap;
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

    private IDWriteTextFormat? _twitchLoginButtonFormat;
    private ID2D1SolidColorBrush? _twitchLoginPrimaryFillBrush;
    private ID2D1SolidColorBrush? _twitchLoginPrimaryFillHoverBrush;
    private ID2D1SolidColorBrush? _twitchLoginPrimaryTextBrush;
    private ID2D1SolidColorBrush? _twitchLoginSecondaryFillBrush;
    private ID2D1SolidColorBrush? _twitchLoginSecondaryFillHoverBrush;
    private ID2D1SolidColorBrush? _twitchLoginSecondaryBorderBrush;
    private ID2D1SolidColorBrush? _twitchLoginSecondaryTextBrush;

    private ID2D1Bitmap? _twitchLoginIconBitmap;
    private bool _twitchLoginIconLoadAttempted;

    private ID2D1Bitmap? GetOrCreateTwitchLoginIconBitmap(ID2D1DCRenderTarget target)
    {
        if (_twitchLoginIconBitmap is not null || _twitchLoginIconLoadAttempted)
            return _twitchLoginIconBitmap;
        _twitchLoginIconLoadAttempted = true;

        var decoded = TwitchIconLoader.GetDecodedIcon();
        if (decoded is null)
            return null;

        try
        {
            _twitchLoginIconBitmap = D2DBitmapLoader.CreateBitmap(target, decoded.Value, "TwitchIcon");
        }
        catch
        {

        }
        return _twitchLoginIconBitmap;
    }

    private void DrawTwitchLoginButton(ID2D1DCRenderTarget target, float x, float y)
    {
        _twitchLoginButtonFormat ??= TwitchLoginButtonStyle.CreateFormat(DWriteFactory, 13f);
        string actionLabel = LocalizationService.T(_moderation.IsLoggedIn ? "Common_Logout" : "Common_LoginWithTwitch");

        using var actionLabelLayout = DWriteFactory.CreateTextLayout(
            actionLabel,
            _twitchLoginButtonFormat,
            float.MaxValue,
            TwitchLoginButtonStyle.Height
        );
        _twitchLoginActionRect = TwitchLoginButtonStyle.Measure(actionLabelLayout, x, y);
        bool hovered = _enableTwitchApi && Contains(_twitchLoginActionRect, _hoverMouseX, _hoverMouseY);

        if (_enableTwitchApi && !_moderation.IsLoggedIn)
        {
            _twitchLoginPrimaryFillBrush ??= target.CreateSolidColorBrush(TwitchLoginButtonStyle.PrimaryFill);
            _twitchLoginPrimaryFillHoverBrush ??= target.CreateSolidColorBrush(TwitchLoginButtonStyle.PrimaryFillHover);
            _twitchLoginPrimaryTextBrush ??= target.CreateSolidColorBrush(TwitchLoginButtonStyle.PrimaryText);
            TwitchLoginButtonStyle.DrawPrimary(
                target,
                _twitchLoginActionRect,
                actionLabelLayout,
                hovered ? _twitchLoginPrimaryFillHoverBrush : _twitchLoginPrimaryFillBrush,
                _twitchLoginPrimaryTextBrush,
                GetOrCreateTwitchLoginIconBitmap(target)
            );
            return;
        }

        _twitchLoginSecondaryFillBrush ??= target.CreateSolidColorBrush(TwitchLoginButtonStyle.SecondaryFill(ThemeService.IsDark));
        _twitchLoginSecondaryFillHoverBrush ??= target.CreateSolidColorBrush(TwitchLoginButtonStyle.SecondaryFillHover(ThemeService.IsDark));
        _twitchLoginSecondaryBorderBrush ??= target.CreateSolidColorBrush(TwitchLoginButtonStyle.SecondaryBorder(ThemeService.IsDark));
        _twitchLoginSecondaryTextBrush ??= target.CreateSolidColorBrush(TwitchLoginButtonStyle.SecondaryText(ThemeService.IsDark));
        TwitchLoginButtonStyle.DrawSecondary(
            target,
            _twitchLoginActionRect,
            actionLabelLayout,
            hovered ? _twitchLoginSecondaryFillHoverBrush : _twitchLoginSecondaryFillBrush,
            _twitchLoginSecondaryBorderBrush,
            _enableTwitchApi ? _twitchLoginSecondaryTextBrush : _secondaryBrush!,
            GetOrCreateTwitchLoginIconBitmap(target)
        );
    }

    private void DisposeTwitchLoginButtonResources()
    {
        _twitchLoginButtonFormat?.Dispose();
        _twitchLoginButtonFormat = null;
        _twitchLoginPrimaryFillBrush?.Dispose();
        _twitchLoginPrimaryFillBrush = null;
        _twitchLoginPrimaryFillHoverBrush?.Dispose();
        _twitchLoginPrimaryFillHoverBrush = null;
        _twitchLoginPrimaryTextBrush?.Dispose();
        _twitchLoginPrimaryTextBrush = null;
        _twitchLoginSecondaryFillBrush?.Dispose();
        _twitchLoginSecondaryFillBrush = null;
        _twitchLoginSecondaryFillHoverBrush?.Dispose();
        _twitchLoginSecondaryFillHoverBrush = null;
        _twitchLoginSecondaryBorderBrush?.Dispose();
        _twitchLoginSecondaryBorderBrush = null;
        _twitchLoginSecondaryTextBrush?.Dispose();
        _twitchLoginSecondaryTextBrush = null;
        _twitchLoginIconBitmap?.Dispose();
        _twitchLoginIconBitmap = null;
        _twitchLoginIconLoadAttempted = false;
    }

    private async void HandleTwitchLoginAction()
    {
        if (_moderation.IsLoggedIn)
        {
            _moderation.Logout();
            RequestRender();
            return;
        }

        if (_twitchLoginBusy)
        {

            _twitchLoginCts?.Cancel();
            _twitchLoginCts?.Dispose();
        }

        var cts = new CancellationTokenSource();
        _twitchLoginCts = cts;

        _twitchLoginBusy = true;
        _twitchLoginTransientStatus = LocalizationService.T("Settings_TwitchApi_OpeningBrowser");
        RequestRender();

        var ok = await _moderation.LoginAsync(cts.Token);

        PostToUiThread(() =>
        {

            if (_twitchLoginCts != cts)
                return;

            _twitchLoginBusy = false;

            _twitchLoginTransientStatus = ok ? null : LocalizationService.T("Settings_TwitchApi_LoginFailed");
            RequestRender();
        });
    }

    private void HandleTwitchApiSectionClick(int clientX, int clientY)
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

        if (_enableTwitchApi && Contains(_twitchLoginActionRect, clientX, clientY))
        {
            HandleTwitchLoginAction();
            return;
        }

        if (_showViewerCount)
        {
            if (Contains(_pickViewerCountColorButtonRect, clientX, clientY)) { OpenViewerCountColorPicker(); return; }
            if (!string.IsNullOrWhiteSpace(_viewerCountColorHex) && Contains(_resetViewerCountColorButtonRect, clientX, clientY)) { ResetViewerCountColor(); return; }
        }
    }
}