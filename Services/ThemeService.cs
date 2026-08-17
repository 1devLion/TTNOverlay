using Vortice.Mathematics;

namespace TTNOverlay.Services;

/// <summary>
/// Tracks the current UI theme (Dark/Light) and exposes the shared dark/light color palette used
/// across every native window (Settings, ColorPicker, ConfirmDialog, dropdowns, title bar, viewer
/// count badge, moderation panel). These used to be duplicated as inline ternaries in each file;
/// centralizing them here means a palette tweak only needs to happen once, and call sites can't
/// silently drift apart from each other via copy-paste.
/// </summary>
public static class ThemeService
{

    public static string Current { get; private set; } = "Dark";

    public static bool IsDark => Current != "Light";

    public static void Apply(string? theme)
    {
        Current = theme == "Light" ? "Light" : "Dark";
    }

    // --- Opaque window chrome: Settings, ColorPicker, ConfirmDialog, moderation panel background ---

    /// <summary>Opaque window background. Used by every native dialog/panel that isn't drawn over the transparent chat surface.</summary>
    public static Color4 WindowBackground => IsDark
        ? new Color4(0x1E / 255f, 0x1E / 255f, 0x1E / 255f, 1f)
        : new Color4(1f, 1f, 1f, 1f);

    /// <summary>Primary body text on an opaque window background.</summary>
    public static Color4 WindowText => IsDark
        ? new Color4(1f, 1f, 1f, 1f)
        : new Color4(0.1f, 0.1f, 0.1f, 1f);

    /// <summary>Secondary/label text on an opaque window background (ColorPicker + Settings).</summary>
    public static Color4 WindowTextSecondary => IsDark
        ? new Color4(0.7f, 0.7f, 0.7f, 1f)
        : new Color4(0.4f, 0.4f, 0.4f, 1f);

    /// <summary>Fill for input fields / swatches on an opaque window background.</summary>
    public static Color4 FieldBackground => IsDark
        ? new Color4(1f, 1f, 1f, 0.06f)
        : new Color4(0f, 0f, 0f, 0.04f);

    /// <summary>Border for input fields, swatches, and dropdown surfaces.</summary>
    public static Color4 FieldBorder => IsDark
        ? new Color4(1f, 1f, 1f, 0.18f)
        : new Color4(0f, 0f, 0f, 0.18f);

    /// <summary>Subtle fill for a selected sidebar item or a hovered dropdown row.</summary>
    public static Color4 SubtleHoverFill => IsDark
        ? new Color4(1f, 1f, 1f, 0.10f)
        : new Color4(0f, 0f, 0f, 0.06f);

    /// <summary>Background of a popped-out dropdown surface (native dropdown, moderation dropdown).</summary>
    public static Color4 DropdownSurfaceBackground => IsDark
        ? new Color4(0x2A / 255f, 0x2A / 255f, 0x2A / 255f, 1f)
        : new Color4(1f, 1f, 1f, 1f);

    // --- Overlay chrome: drawn on/near the transparent chat surface (title bar, viewer count, moderation text) ---

    /// <summary>
    /// Primary text drawn on the overlay chrome (title bar, viewer count badge, moderation panel).
    /// Slightly darker light-mode value than <see cref="WindowText"/> (0.125 vs 0.1) since it sits
    /// over the semi-transparent chat surface rather than an opaque dialog background.
    /// </summary>
    public static Color4 OverlayText => IsDark
        ? new Color4(1f, 1f, 1f, 1f)
        : new Color4(0.125f, 0.125f, 0.125f, 1f);

    /// <summary>Pure white/black. Used where a hover tint or icon needs full contrast rather than the softer text grays above.</summary>
    public static Color4 PureContrastTint => IsDark
             ? new Color4(1f, 1f, 1f, 1f)
             : new Color4(0f, 0f, 0f, 1f);

    /// <summary>Track fill for the thin vertical scrollbar indicator...</summary>
    public static Color4 ScrollbarTrack => IsDark
        ? new Color4(1f, 1f, 1f, 0.05f)
        : new Color4(0f, 0f, 0f, 0.05f);

    /// <summary>Thumb fill for the thin vertical scrollbar indicator...</summary>
    public static Color4 ScrollbarThumb => IsDark
        ? new Color4(1f, 1f, 1f, 0.25f)
        : new Color4(0f, 0f, 0f, 0.25f);
}
