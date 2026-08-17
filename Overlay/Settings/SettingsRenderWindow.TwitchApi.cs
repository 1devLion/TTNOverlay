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
/// Viewer-count fields used to live here too, but that coupled the counter -- which also covers Kick
/// and eventually YouTube -- to a Twitch-only settings screen. They now live in their own section,
/// SettingsRenderWindow.ViewerCount.cs.
/// </summary>
internal sealed partial class SettingsRenderWindow
{

    private readonly IModerationService _moderation;
    private bool _enableTwitchApi;
    private bool _showBadges;
    private bool _originalEnableTwitchApi;
    private bool _originalShowBadges;

    private string? _twitchLoginTransientStatus;
    private bool _twitchLoginBusy;

    private CancellationTokenSource? _twitchLoginCts;
    private Rect _twitchLoginActionRect;

    private void InitTwitchApi()
    {
        _enableTwitchApi = Settings.EnableTwitchApi;
        _showBadges = Settings.ShowBadges;

        _originalEnableTwitchApi = _enableTwitchApi;
        _originalShowBadges = _showBadges;
    }

    private void RevertTwitchApi()
    {
        Settings.EnableTwitchApi = _originalEnableTwitchApi;
        Settings.ShowBadges = _originalShowBadges;
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

    /// <summary>
    /// Lays out the pick/reset button pair used by a color row. Shared with
    /// SettingsRenderWindow.ViewerCount.cs. (MeasureButtonRect itself lives in
    /// SettingsRenderWindow.cs, shared across every section partial.)
    /// </summary>
    private float LayoutColorRowButtons(string pickLabel, string resetLabel, float rowX, float y, float buttonHeight, out Rect pickRect, out Rect resetRect)
    {
        pickRect = MeasureButtonRect(pickLabel, rowX + 24f + 8f, y, buttonHeight, minWidth: 90f);

        float resetY = y + buttonHeight + 6f;
        resetRect = MeasureButtonRect(resetLabel, rowX, resetY, buttonHeight, minWidth: 170f);
        return resetY + buttonHeight;
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
                GetOrCreateTwitchLoginIconBitmap(target, TwitchIconLoader.Variant.White)
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
            GetOrCreateTwitchLoginIconBitmap(target, ThemeService.IsDark ? TwitchIconLoader.Variant.White : TwitchIconLoader.Variant.Dark)
        );
    }

    private IDWriteTextFormat? _twitchLoginButtonFormat;
    private ID2D1SolidColorBrush? _twitchLoginPrimaryFillBrush;
    private ID2D1SolidColorBrush? _twitchLoginPrimaryFillHoverBrush;
    private ID2D1SolidColorBrush? _twitchLoginPrimaryTextBrush;
    private ID2D1SolidColorBrush? _twitchLoginSecondaryFillBrush;
    private ID2D1SolidColorBrush? _twitchLoginSecondaryFillHoverBrush;
    private ID2D1SolidColorBrush? _twitchLoginSecondaryBorderBrush;
    private ID2D1SolidColorBrush? _twitchLoginSecondaryTextBrush;

    private ID2D1Bitmap? _twitchLoginIconWhiteBitmap;
    private bool _twitchLoginIconWhiteLoadAttempted;
    private ID2D1Bitmap? _twitchLoginIconDarkBitmap;
    private bool _twitchLoginIconDarkLoadAttempted;

    private ID2D1Bitmap? GetOrCreateTwitchLoginIconBitmap(ID2D1DCRenderTarget target, TwitchIconLoader.Variant variant)
    {
        if (variant == TwitchIconLoader.Variant.Dark)
        {
            if (_twitchLoginIconDarkBitmap is not null || _twitchLoginIconDarkLoadAttempted)
                return _twitchLoginIconDarkBitmap;
            _twitchLoginIconDarkLoadAttempted = true;

            var decodedDark = TwitchIconLoader.GetDecodedIcon(TwitchIconLoader.Variant.Dark);
            if (decodedDark is null)
                return null;
            try { _twitchLoginIconDarkBitmap = D2DBitmapLoader.CreateBitmap(target, decodedDark.Value, "TwitchIconDark"); }
            catch { }
            return _twitchLoginIconDarkBitmap;
        }

        if (_twitchLoginIconWhiteBitmap is not null || _twitchLoginIconWhiteLoadAttempted)
            return _twitchLoginIconWhiteBitmap;
        _twitchLoginIconWhiteLoadAttempted = true;

        var decoded = TwitchIconLoader.GetDecodedIcon(TwitchIconLoader.Variant.White);
        if (decoded is null)
            return null;

        try
        {
            _twitchLoginIconWhiteBitmap = D2DBitmapLoader.CreateBitmap(target, decoded.Value, "TwitchIconWhite");
        }
        catch
        {

        }
        return _twitchLoginIconWhiteBitmap;
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
        _twitchLoginIconWhiteBitmap?.Dispose();
        _twitchLoginIconWhiteBitmap = null;
        _twitchLoginIconWhiteLoadAttempted = false;
        _twitchLoginIconDarkBitmap?.Dispose();
        _twitchLoginIconDarkBitmap = null;
        _twitchLoginIconDarkLoadAttempted = false;
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
    }
}