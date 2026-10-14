using TTNOverlay.Kick;
using TTNOverlay.Services;
using Vortice.Direct2D1;
using Vortice.DirectWrite;
using Rect = Vortice.Mathematics.Rect;

namespace TTNOverlay.Overlay;

/// <summary>
/// SettingsRenderWindow partial: the Kick API section (Kick login/logout). Mirrors the login half of the Twitch API
/// section; the session lives in Settings.KickRefreshToken/KickLogin/KickUserId.
/// </summary>
internal sealed partial class SettingsRenderWindow
{
    private KickSessionService? _kickSession;

    private string? _kickLoginTransientStatus;
    private bool _kickLoginBusy;
    private CancellationTokenSource? _kickLoginCts;
    private Rect _kickLoginActionRect;

    private IDWriteTextFormat? _kickLoginButtonFormat;
    private ID2D1SolidColorBrush? _kickLoginPrimaryFillBrush;
    private ID2D1SolidColorBrush? _kickLoginPrimaryFillHoverBrush;
    private ID2D1SolidColorBrush? _kickLoginPrimaryTextBrush;
    private ID2D1SolidColorBrush? _kickLoginSecondaryFillBrush;
    private ID2D1SolidColorBrush? _kickLoginSecondaryFillHoverBrush;
    private ID2D1SolidColorBrush? _kickLoginSecondaryBorderBrush;
    private ID2D1SolidColorBrush? _kickLoginSecondaryTextBrush;
    private ID2D1Bitmap? _kickLoginIconBitmap;
    private bool _kickLoginIconLoadAttempted;

    private ID2D1Bitmap? GetOrCreateKickLoginIconBitmap(ID2D1DCRenderTarget target)
    {
        if (_kickLoginIconBitmap is not null || _kickLoginIconLoadAttempted)
            return _kickLoginIconBitmap;
        _kickLoginIconLoadAttempted = true;

        // Same colored logo the title bar uses; it looks fine in both themes, so there is no dark/white variant.
        var decoded = PlatformBadgeIconLoader.GetDecodedIcon("platform/kick");
        if (decoded is null)
            return null;

        try { _kickLoginIconBitmap = D2DBitmapLoader.CreateBitmap(target, decoded.Value, "KickLoginIcon"); }
        catch { }
        return _kickLoginIconBitmap;
    }

    private void InitKickApi()
    {
        _kickSession = new KickSessionService(Settings);
        KickSessionService.SessionChanged += OnKickSessionChanged;
    }

    /// <summary>
    /// Another window (the chat window, after a scope check) logged in or out of Kick. This window edits a copy of the
    /// settings, so copy the session over.
    /// </summary>
    private void OnKickSessionChanged(KickSessionService source, string refreshToken, string login, string userId)
    {
        if (ReferenceEquals(source, _kickSession))
            return;

        PostToUiThread(() =>
        {
            Settings.KickRefreshToken = refreshToken;
            Settings.KickLogin = login;
            Settings.KickUserId = userId;
            _kickSession?.DropCachedToken();
            RequestRender();
        });
    }

    private void DrawKickApiSection(ID2D1DCRenderTarget target, float x, float width)
    {
        float y = TitleBarHeight + Padding;

        using (var header = DWriteFactory.CreateTextLayout(LocalizationService.T("Settings_Section_KickApi"), _headerFormat!, width, 24f))
            target.DrawTextLayout(new System.Numerics.Vector2(x, y), header, _textBrush!);
        y += 28f;

        using (var info = DWriteFactory.CreateTextLayout(LocalizationService.T("Settings_KickApi_LoginInfo"), _labelFormat!, width, 32f))
            target.DrawTextLayout(new System.Numerics.Vector2(x, y), info, _secondaryBrush!);
        y += 40f;

        bool loggedIn = _kickSession!.IsLoggedIn;
        string statusText = _kickLoginTransientStatus ?? (
            loggedIn
                ? string.Format(LocalizationService.T("Settings_KickApi_Connected"), _kickSession.Login)
                : LocalizationService.T("Settings_KickApi_NotLoggedIn"));
        using (var status = DWriteFactory.CreateTextLayout(statusText, _labelFormat!, width, 18f))
            target.DrawTextLayout(new System.Numerics.Vector2(x, y), status, _secondaryBrush!);
        y += 18f + LabelGap + 4f;

        DrawKickLoginButton(target, x, y, loggedIn);
    }

    private void DrawKickLoginButton(ID2D1DCRenderTarget target, float x, float y, bool loggedIn)
    {
        _kickLoginButtonFormat ??= TwitchLoginButtonStyle.CreateFormat(DWriteFactory, 13f);
        string actionLabel = LocalizationService.T(loggedIn ? "Common_Logout" : "Common_LoginWithKick");

        using var labelLayout = DWriteFactory.CreateTextLayout(
            actionLabel,
            _kickLoginButtonFormat,
            float.MaxValue,
            KickLoginButtonStyle.Height
        );
        _kickLoginActionRect = KickLoginButtonStyle.Measure(labelLayout, x, y);
        bool hovered = Contains(_kickLoginActionRect, _hoverMouseX, _hoverMouseY);

        if (!loggedIn)
        {
            bool dark = ThemeService.IsDark;
            _kickLoginPrimaryFillBrush ??= target.CreateSolidColorBrush(KickLoginButtonStyle.PrimaryFill(dark));
            _kickLoginPrimaryTextBrush ??= target.CreateSolidColorBrush(KickLoginButtonStyle.PrimaryText(dark));
            _kickLoginSecondaryBorderBrush ??= target.CreateSolidColorBrush(TwitchLoginButtonStyle.SecondaryBorder(dark));
            // The colors follow the theme, so they are set every frame instead of being baked into the brushes.
            _kickLoginPrimaryFillBrush.Color = hovered ? KickLoginButtonStyle.PrimaryFillHover(dark) : KickLoginButtonStyle.PrimaryFill(dark);
            _kickLoginPrimaryTextBrush.Color = KickLoginButtonStyle.PrimaryText(dark);
            KickLoginButtonStyle.DrawPrimary(
                target,
                _kickLoginActionRect,
                labelLayout,
                _kickLoginPrimaryFillBrush,
                _kickLoginSecondaryBorderBrush,
                _kickLoginPrimaryTextBrush,
                GetOrCreateKickLoginIconBitmap(target)
            );
            return;
        }

        _kickLoginSecondaryFillBrush ??= target.CreateSolidColorBrush(TwitchLoginButtonStyle.SecondaryFill(ThemeService.IsDark));
        _kickLoginSecondaryFillHoverBrush ??= target.CreateSolidColorBrush(TwitchLoginButtonStyle.SecondaryFillHover(ThemeService.IsDark));
        _kickLoginSecondaryBorderBrush ??= target.CreateSolidColorBrush(TwitchLoginButtonStyle.SecondaryBorder(ThemeService.IsDark));
        _kickLoginSecondaryTextBrush ??= target.CreateSolidColorBrush(TwitchLoginButtonStyle.SecondaryText(ThemeService.IsDark));
        KickLoginButtonStyle.DrawSecondary(
            target,
            _kickLoginActionRect,
            labelLayout,
            hovered ? _kickLoginSecondaryFillHoverBrush : _kickLoginSecondaryFillBrush,
            _kickLoginSecondaryBorderBrush,
            _kickLoginSecondaryTextBrush,
            GetOrCreateKickLoginIconBitmap(target)
        );
    }

    private void DisposeKickLoginButtonResources()
    {
        _kickLoginButtonFormat?.Dispose();
        _kickLoginButtonFormat = null;
        _kickLoginPrimaryFillBrush?.Dispose();
        _kickLoginPrimaryFillBrush = null;
        _kickLoginPrimaryFillHoverBrush?.Dispose();
        _kickLoginPrimaryFillHoverBrush = null;
        _kickLoginPrimaryTextBrush?.Dispose();
        _kickLoginPrimaryTextBrush = null;
        _kickLoginSecondaryFillBrush?.Dispose();
        _kickLoginSecondaryFillBrush = null;
        _kickLoginSecondaryFillHoverBrush?.Dispose();
        _kickLoginSecondaryFillHoverBrush = null;
        _kickLoginSecondaryBorderBrush?.Dispose();
        _kickLoginSecondaryBorderBrush = null;
        _kickLoginSecondaryTextBrush?.Dispose();
        _kickLoginSecondaryTextBrush = null;
        _kickLoginIconBitmap?.Dispose();
        _kickLoginIconBitmap = null;
        _kickLoginIconLoadAttempted = false;
    }

    private void DisposeKickApi()
    {
        KickSessionService.SessionChanged -= OnKickSessionChanged;
        _kickLoginCts?.Cancel();
        _kickLoginCts?.Dispose();
        _kickLoginCts = null;
    }

    private async void HandleKickLoginAction()
    {
        if (_kickSession is null)
            return;

        if (_kickSession.IsLoggedIn)
        {
            _kickSession.Logout();
            RequestRender();
            return;
        }

        if (_kickLoginBusy)
        {
            _kickLoginCts?.Cancel();
            _kickLoginCts?.Dispose();
        }

        var cts = new CancellationTokenSource();
        _kickLoginCts = cts;

        _kickLoginBusy = true;
        _kickLoginTransientStatus = LocalizationService.T("Settings_KickApi_OpeningBrowser");
        RequestRender();

        var ok = await _kickSession.LoginAsync(cts.Token);

        PostToUiThread(() =>
        {
            if (_kickLoginCts != cts)
                return;

            _kickLoginBusy = false;
            _kickLoginTransientStatus = ok ? null : LocalizationService.T("Settings_KickApi_LoginFailed");
            RequestRender();
        });
    }

    private void HandleKickApiSectionClick(int clientX, int clientY)
    {
        if (Contains(_kickLoginActionRect, clientX, clientY))
            HandleKickLoginAction();
    }
}
