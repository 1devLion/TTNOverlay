namespace TTNOverlay.Native;

/// <summary>
/// Win32 hotkey modifier flag constants (Alt/Control/Shift/Win) for RegisterHotKey.
/// </summary>
public static class HotkeyModifiers
{
    public const uint Alt = 0x0001;
    public const uint Control = 0x0002;
    public const uint Shift = 0x0004;
    public const uint Win = 0x0008;
}

