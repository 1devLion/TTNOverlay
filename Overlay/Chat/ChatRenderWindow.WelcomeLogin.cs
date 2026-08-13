using TTNOverlay.Services;
using Vortice.Direct2D1;
using Vortice.DirectWrite;
using Vortice.Mathematics;

namespace TTNOverlay.Overlay;

/// <summary>
/// ChatRenderWindow partial: the "Log in with Twitch" button rendered inside the first-run welcome
/// guide message (see SeedWelcomeGuide in ChatRenderWindow.MessageList.cs), so a new user can connect
/// their account without having to open Settings first. Hides itself once the user is logged in.
/// Shares its ID2D1 brushes/format with the moderation panel's login button (see
/// ChatRenderWindow.Moderation.Render.cs) since both live on the same render target; the visual
/// definition itself (colors, sizing, draw logic) lives once in TwitchLoginButtonStyle, also shared
/// with Settings -> Twitch API's login button.
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

    private ID2D1Bitmap? _twitchButtonIconBitmap;
    private bool _twitchButtonIconLoadAttempted;

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

    private ID2D1Bitmap? GetOrCreateTwitchButtonIconBitmap(ID2D1DCRenderTarget target)
    {
        if (_twitchButtonIconBitmap is not null || _twitchButtonIconLoadAttempted)
            return _twitchButtonIconBitmap;
        _twitchButtonIconLoadAttempted = true;

        var decoded = TwitchIconLoader.GetDecodedIcon();
        if (decoded is null)
            return null;

        try
        {
            _twitchButtonIconBitmap = D2DBitmapLoader.CreateBitmap(target, decoded.Value, "TwitchIcon");
        }
        catch
        {

        }
        return _twitchButtonIconBitmap;
    }

    /// <summary>
    /// Draws (or, when draw is false, just measures) the login button right below a system message's
    /// text and returns the new content bottom -- same draw/measure-in-one contract as DrawMessage's
    /// other helpers, so it composes cleanly with GetOrMeasureHeight's cached-height pass.
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
                GetOrCreateTwitchButtonIconBitmap(target)
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
        _twitchButtonIconBitmap?.Dispose();
        _twitchButtonIconBitmap = null;
        _twitchButtonIconLoadAttempted = false;
    }
}