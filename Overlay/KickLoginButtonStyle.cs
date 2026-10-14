using System.Numerics;
using Vortice.Direct2D1;
using Vortice.DirectWrite;
using Vortice.Mathematics;

namespace TTNOverlay.Overlay;

/// <summary>
/// Visual definition for the "Log in with Kick" / "Log out" button. Same layout as TwitchLoginButtonStyle, with the
/// colored Kick logo (Resources/PlatformBadges/kick.webp). The logout state reuses Twitch's neutral outline colors.
/// </summary>
internal static class KickLoginButtonStyle
{
    public const float Height = TwitchLoginButtonStyle.Height;
    public const float PaddingX = TwitchLoginButtonStyle.PaddingX;
    public const float BorderThickness = TwitchLoginButtonStyle.BorderThickness;
    public const float IconSize = TwitchLoginButtonStyle.IconSize;
    public const float IconTextGap = TwitchLoginButtonStyle.IconTextGap;

    /// <summary>
    /// kick.webp has transparent padding around the glyph (Twitch's goes edge to edge), so it is drawn in a larger box
    /// centered on the same IconSize slot to look the same size. Tweak here if it looks too big or small.
    /// </summary>
    public const float IconDrawSize = 22f;

    /// <summary>
    /// The way Kick draws its own button: a gray box in dark mode and a white one in light mode, with the colored logo.
    /// </summary>
    public static Color4 PrimaryFill(bool isDark) => isDark ? new Color4(0.17f, 0.18f, 0.20f, 1f) : new Color4(1f, 1f, 1f, 1f);
    public static Color4 PrimaryFillHover(bool isDark) => isDark ? new Color4(0.24f, 0.25f, 0.27f, 1f) : new Color4(0.93f, 0.93f, 0.94f, 1f);
    public static Color4 PrimaryText(bool isDark) => isDark ? new Color4(1f, 1f, 1f, 1f) : new Color4(0.10f, 0.10f, 0.10f, 1f);

    public static Rect Measure(IDWriteTextLayout labelLayout, float x, float y) =>
        new(
            x,
            y,
            PaddingX + IconSize + IconTextGap + (float)labelLayout.Metrics.WidthIncludingTrailingWhitespace + PaddingX,
            Height
        );

    /// <summary>Box (primary, "Log in with Kick") variant.</summary>
    public static void DrawPrimary(ID2D1DCRenderTarget target, Rect rect, IDWriteTextLayout labelLayout, ID2D1Brush fillBrush, ID2D1Brush borderBrush, ID2D1Brush textBrush, ID2D1Bitmap? icon)
    {
        target.FillRectangle(rect, fillBrush);
        target.DrawRectangle(rect, borderBrush, BorderThickness);
        DrawIconAndLabel(target, rect, labelLayout, textBrush, icon);
    }

    /// <summary>Outlined (secondary, "Log out") variant.</summary>
    public static void DrawSecondary(ID2D1DCRenderTarget target, Rect rect, IDWriteTextLayout labelLayout, ID2D1Brush fillBrush, ID2D1Brush borderBrush, ID2D1Brush textBrush, ID2D1Bitmap? icon)
    {
        target.FillRectangle(rect, fillBrush);
        target.DrawRectangle(rect, borderBrush, BorderThickness);
        DrawIconAndLabel(target, rect, labelLayout, textBrush, icon);
    }

    private static void DrawIconAndLabel(ID2D1DCRenderTarget target, Rect rect, IDWriteTextLayout labelLayout, ID2D1Brush textBrush, ID2D1Bitmap? icon)
    {
        if (icon is not null)
        {
            float slotCenterX = rect.Left + PaddingX + IconSize / 2f;
            float slotCenterY = rect.Top + Height / 2f;
            var iconRect = new Rect(slotCenterX - IconDrawSize / 2f, slotCenterY - IconDrawSize / 2f, IconDrawSize, IconDrawSize);
            target.DrawBitmap(
                icon,
                iconRect,
                1f,
                BitmapInterpolationMode.Linear,
                new Rect(0, 0, icon.Size.Width, icon.Size.Height)
            );
        }

        float labelX = rect.Left + PaddingX + IconSize + IconTextGap;
        float labelY = rect.Top + (Height - labelLayout.Metrics.Height) / 2f;
        target.DrawTextLayout(new Vector2(labelX, labelY), labelLayout, textBrush);
    }
}
