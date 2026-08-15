using System.Numerics;
using Vortice.Direct2D1;
using Vortice.DirectWrite;
using Vortice.Mathematics;

namespace TTNOverlay.Overlay;

/// <summary>
/// Visual definition (sizing, colors, measure/draw logic) for every "Log in with Twitch" /
/// "Log out" button in the app.
/// </summary>
internal static class TwitchLoginButtonStyle
{
    public const float Height = 30f;
    public const float PaddingX = 14f;
    public const float BorderThickness = 1f;

    public const float IconSize = 20f;
    public const float IconTextGap = 8f;

    public static Color4 IconBackgroundColor => new(0x85 / 255f, 0x57 / 255f, 0xDE / 255f, 1f);

    /// <summary>Twitch-brand fill for the "Log in with Twitch" (primary) state.</summary>
    public static Color4 PrimaryFill => IconBackgroundColor;
    public static Color4 PrimaryFillHover => new(0x9D / 255f, 0x6F / 255f, 0xF6 / 255f, 1f);
    public static Color4 PrimaryText => new(1f, 1f, 1f, 1f);

    /// <summary>Neutral outline for the "Log out" (secondary) state.</summary> 
    public static Color4 SecondaryFill(bool isDark) => isDark ? new Color4(1f, 1f, 1f, 0.08f) : new Color4(0f, 0f, 0f, 0.05f);
    public static Color4 SecondaryFillHover(bool isDark) => isDark ? new Color4(1f, 1f, 1f, 0.14f) : new Color4(0f, 0f, 0f, 0.09f);
    public static Color4 SecondaryBorder(bool isDark) => isDark ? new Color4(1f, 1f, 1f, 0.35f) : new Color4(0f, 0f, 0f, 0.30f);
    public static Color4 SecondaryText(bool isDark) => isDark ? new Color4(1f, 1f, 1f, 1f) : new Color4(0.125f, 0.125f, 0.125f, 1f);

    public static IDWriteTextFormat CreateFormat(IDWriteFactory dwriteFactory, float fontSize)
    {
        var format = dwriteFactory.CreateTextFormat(
            "Segoe UI",
            FontWeight.SemiBold,
            Vortice.DirectWrite.FontStyle.Normal,
            fontSize
        );
        format.TextAlignment = TextAlignment.Leading;
        format.ParagraphAlignment = ParagraphAlignment.Near;
        format.WordWrapping = WordWrapping.NoWrap;
        return format;
    }

    public static Rect Measure(IDWriteTextLayout labelLayout, float x, float y) =>
        new(
            x,
            y,
            PaddingX + IconSize + IconTextGap + (float)labelLayout.Metrics.WidthIncludingTrailingWhitespace + PaddingX,
            Height
        );

    /// <summary>Draws the filled (primary, "Log in with Twitch") variant.</summary>
    public static void DrawPrimary(
        ID2D1DCRenderTarget target,
        Rect rect,
        IDWriteTextLayout labelLayout,
        ID2D1Brush fillBrush,
        ID2D1Brush textBrush,
        ID2D1Bitmap? icon
    )
    {
        target.FillRectangle(rect, fillBrush);
        DrawIconAndLabel(target, rect, labelLayout, textBrush, icon);
    }

    /// <summary>Draws the outlined (secondary, "Log out") variant.</summary>
    public static void DrawSecondary(
        ID2D1DCRenderTarget target,
        Rect rect,
        IDWriteTextLayout labelLayout,
        ID2D1Brush fillBrush,
        ID2D1Brush borderBrush,
        ID2D1Brush textBrush,
        ID2D1Bitmap? icon
    )
    {
        target.FillRectangle(rect, fillBrush);
        target.DrawRectangle(rect, borderBrush, BorderThickness);
        DrawIconAndLabel(target, rect, labelLayout, textBrush, icon);
    }

    private static void DrawIconAndLabel(
        ID2D1DCRenderTarget target,
        Rect rect,
        IDWriteTextLayout labelLayout,
        ID2D1Brush textBrush,
        ID2D1Bitmap? icon
    )
    {
        if (icon is not null)
        {
            var iconRect = new Rect(rect.Left + PaddingX, rect.Top + (Height - IconSize) / 2f, IconSize, IconSize);
            target.DrawBitmap(
                icon,
                iconRect,
                1f,
                BitmapInterpolationMode.Linear,
                new Rect(0, 0, icon.Size.Width, icon.Size.Height)
            );
        }

        float labelX = rect.Left + PaddingX + (icon is not null ? IconSize + IconTextGap : 0f);

        float labelY = rect.Top + (Height - labelLayout.Metrics.Height) / 2f;

        target.DrawTextLayout(
            new Vector2(labelX, labelY),
            labelLayout,
            textBrush
        );
    }
}