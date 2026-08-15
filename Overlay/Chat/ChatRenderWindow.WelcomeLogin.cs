using TTNOverlay.Services;
using Vortice.Direct2D1;
using Vortice.DirectWrite;
using Vortice.Mathematics;

namespace TTNOverlay.Overlay;

/// <summary>
/// ChatRenderWindow partial: the "Log in with Twitch" button rendered inside the first-run welcome
/// guide message. Hides itself once the user is logged in.
/// </summary>
internal sealed partial class ChatRenderWindow
{
    private const float WelcomeButtonGapY = 6f;

    private Rect? _welcomeLoginButtonRect;
    private bool _welcomeLoginButtonHovered;

    private ID2D1SolidColorBrush? _twitchButtonPrimaryFillBrush;
    private ID2D1SolidColorBrush? _twitchButtonPrimaryFillHoverBrush;
    private ID2D1SolidColorBrush? _twitchButtonPrimaryTextBrush;
    private ID2D1SolidColorBrush? _twitchButtonSecondaryFillBrush;
    private ID2D1SolidColorBrush? _twitchButtonSecondaryFillHoverBrush;
    private ID2D1SolidColorBrush? _twitchButtonSecondaryBorderBrush;
    private ID2D1SolidColorBrush? _twitchButtonSecondaryTextBrush;
    private IDWriteTextFormat? _twitchButtonFormat;

    private ID2D1Bitmap? _twitchButtonIconWhiteBitmap;
    private bool _twitchButtonIconWhiteLoadAttempted;
    private ID2D1Bitmap? _twitchButtonIconDarkBitmap;
    private bool _twitchButtonIconDarkLoadAttempted;

    private ID2D1SolidColorBrush GetOrCreateTwitchButtonPrimaryFillBrush(ID2D1DCRenderTarget target, bool hovered) =>
        hovered
            ? _twitchButtonPrimaryFillHoverBrush ??= target.CreateSolidColorBrush(TwitchLoginButtonStyle.PrimaryFillHover)
            : _twitchButtonPrimaryFillBrush ??= target.CreateSolidColorBrush(TwitchLoginButtonStyle.PrimaryFill);

    private ID2D1SolidColorBrush GetOrCreateTwitchButtonTextBrush(ID2D1DCRenderTarget target) =>
        _twitchButtonPrimaryTextBrush ??= target.CreateSolidColorBrush(TwitchLoginButtonStyle.PrimaryText);

    private ID2D1SolidColorBrush GetOrCreateTwitchButtonSecondaryFillBrush(ID2D1DCRenderTarget target, bool hovered) =>
        hovered
            ? _twitchButtonSecondaryFillHoverBrush ??= target.CreateSolidColorBrush(TwitchLoginButtonStyle.SecondaryFillHover(ThemeService.IsDark))
            : _twitchButtonSecondaryFillBrush ??= target.CreateSolidColorBrush(TwitchLoginButtonStyle.SecondaryFill(ThemeService.IsDark));

    private ID2D1SolidColorBrush GetOrCreateTwitchButtonSecondaryBorderBrush(ID2D1DCRenderTarget target) =>
        _twitchButtonSecondaryBorderBrush ??= target.CreateSolidColorBrush(TwitchLoginButtonStyle.SecondaryBorder(ThemeService.IsDark));

    private ID2D1SolidColorBrush GetOrCreateTwitchButtonSecondaryTextBrush(ID2D1DCRenderTarget target) =>
        _twitchButtonSecondaryTextBrush ??= target.CreateSolidColorBrush(TwitchLoginButtonStyle.SecondaryText(ThemeService.IsDark));

    private IDWriteTextFormat GetOrCreateTwitchButtonFormat() =>
        _twitchButtonFormat ??= TwitchLoginButtonStyle.CreateFormat(DWriteFactory, (float)(_settings.FontSize * 0.9));

    private ID2D1Bitmap? GetOrCreateTwitchButtonIconBitmap(ID2D1DCRenderTarget target, TwitchIconLoader.Variant variant)
    {
        if (variant == TwitchIconLoader.Variant.Dark)
        {
            if (_twitchButtonIconDarkBitmap is not null || _twitchButtonIconDarkLoadAttempted)
                return _twitchButtonIconDarkBitmap;
            _twitchButtonIconDarkLoadAttempted = true;

            var decodedDark = TwitchIconLoader.GetDecodedIcon(TwitchIconLoader.Variant.Dark);
            if (decodedDark is null)
                return null;
            try { _twitchButtonIconDarkBitmap = D2DBitmapLoader.CreateBitmap(target, decodedDark.Value, "TwitchIconDark"); }
            catch { }
            return _twitchButtonIconDarkBitmap;
        }

        if (_twitchButtonIconWhiteBitmap is not null || _twitchButtonIconWhiteLoadAttempted)
            return _twitchButtonIconWhiteBitmap;
        _twitchButtonIconWhiteLoadAttempted = true;

        var decoded = TwitchIconLoader.GetDecodedIcon(TwitchIconLoader.Variant.White);
        if (decoded is null)
            return null;

        try
        {
            _twitchButtonIconWhiteBitmap = D2DBitmapLoader.CreateBitmap(target, decoded.Value, "TwitchIconWhite");
        }
        catch
        {

        }
        return _twitchButtonIconWhiteBitmap;
    }

    /// <summary>
    /// Draws (or, when draw is false, just measures) the login button right below a system message's
    /// text and returns the new content bottom.
    /// </summary>
    private float DrawWelcomeTwitchLoginButton(ID2D1DCRenderTarget target, float x, float y, bool draw)
    {
        float top = y + WelcomeButtonGapY;

        using var labelLayout = DWriteFactory.CreateTextLayout(
            LocalizationService.T("Common_LoginWithTwitch"),
            GetOrCreateTwitchButtonFormat(),
            float.MaxValue,
            TwitchLoginButtonStyle.Height
        );
        var rect = TwitchLoginButtonStyle.Measure(labelLayout, x, top);

        if (draw)
        {
            TwitchLoginButtonStyle.DrawPrimary(
                target,
                rect,
                labelLayout,
                GetOrCreateTwitchButtonPrimaryFillBrush(target, _welcomeLoginButtonHovered),
                GetOrCreateTwitchButtonTextBrush(target),
                GetOrCreateTwitchButtonIconBitmap(target, TwitchIconLoader.Variant.White)
            );
            _welcomeLoginButtonRect = rect;
        }

        return rect.Bottom;
    }

    private void HandleWelcomeLoginButtonClick()
    {
        _moderation ??= new ModerationService(_settings);
        _ = LoginWithTwitchAsync();
    }

    private void DisposeTwitchButtonResources()
    {
        _twitchButtonPrimaryFillBrush?.Dispose();
        _twitchButtonPrimaryFillBrush = null;
        _twitchButtonPrimaryFillHoverBrush?.Dispose();
        _twitchButtonPrimaryFillHoverBrush = null;
        _twitchButtonPrimaryTextBrush?.Dispose();
        _twitchButtonPrimaryTextBrush = null;
        _twitchButtonSecondaryFillBrush?.Dispose();
        _twitchButtonSecondaryFillBrush = null;
        _twitchButtonSecondaryFillHoverBrush?.Dispose();
        _twitchButtonSecondaryFillHoverBrush = null;
        _twitchButtonSecondaryBorderBrush?.Dispose();
        _twitchButtonSecondaryBorderBrush = null;
        _twitchButtonSecondaryTextBrush?.Dispose();
        _twitchButtonSecondaryTextBrush = null;
        _twitchButtonFormat?.Dispose();
        _twitchButtonFormat = null;
        _twitchButtonIconWhiteBitmap?.Dispose();
        _twitchButtonIconWhiteBitmap = null;
        _twitchButtonIconWhiteLoadAttempted = false;
        _twitchButtonIconDarkBitmap?.Dispose();
        _twitchButtonIconDarkBitmap = null;
        _twitchButtonIconDarkLoadAttempted = false;
    }
}