using TTNOverlay.Native;
using TTNOverlay.Overlay.Controls;
using TTNOverlay.Services;
using Vortice.Direct2D1;
using Vortice.DirectWrite;
using Vortice.Mathematics;
using Rect = Vortice.Mathematics.Rect;

namespace TTNOverlay.Overlay;

/// <summary>
/// SettingsRenderWindow partial: the Hotkeys section, including hotkey capture input.
/// </summary>
internal sealed partial class SettingsRenderWindow
{

    private bool _enableGlobalHotkeys;
    private uint _eventsHotkeyModifiers;
    private uint _eventsHotkeyKey;
    private uint _moderationHotkeyModifiers;
    private uint _moderationHotkeyKey;
    private uint _bordersHotkeyModifiers;
    private uint _bordersHotkeyKey;

    private bool _originalEnableGlobalHotkeys;
    private uint _originalEventsHotkeyModifiers;
    private uint _originalEventsHotkeyKey;
    private uint _originalModerationHotkeyModifiers;
    private uint _originalModerationHotkeyKey;
    private uint _originalBordersHotkeyModifiers;
    private uint _originalBordersHotkeyKey;

    private string? _capturingHotkeyField;
    private string? _hotkeyErrorField;
    private string? _hotkeyErrorMessage;
    private Rect _eventsHotkeyRect;
    private Rect _moderationHotkeyRect;
    private Rect _bordersHotkeyRect;

    private void InitHotkeys()
    {
        _enableGlobalHotkeys = Settings.EnableGlobalHotkeys;
        _eventsHotkeyModifiers = Settings.EventsHotkeyModifiers;
        _eventsHotkeyKey = Settings.EventsHotkeyKey;
        _moderationHotkeyModifiers = Settings.ModerationHotkeyModifiers;
        _moderationHotkeyKey = Settings.ModerationHotkeyKey;
        _bordersHotkeyModifiers = Settings.BordersHotkeyModifiers;
        _bordersHotkeyKey = Settings.BordersHotkeyKey;

        _originalEnableGlobalHotkeys = _enableGlobalHotkeys;
        _originalEventsHotkeyModifiers = _eventsHotkeyModifiers;
        _originalEventsHotkeyKey = _eventsHotkeyKey;
        _originalModerationHotkeyModifiers = _moderationHotkeyModifiers;
        _originalModerationHotkeyKey = _moderationHotkeyKey;
        _originalBordersHotkeyModifiers = _bordersHotkeyModifiers;
        _originalBordersHotkeyKey = _bordersHotkeyKey;
    }

    private void RevertHotkeys()
    {
        Settings.EnableGlobalHotkeys = _originalEnableGlobalHotkeys;
        Settings.EventsHotkeyModifiers = _originalEventsHotkeyModifiers;
        Settings.EventsHotkeyKey = _originalEventsHotkeyKey;
        Settings.ModerationHotkeyModifiers = _originalModerationHotkeyModifiers;
        Settings.ModerationHotkeyKey = _originalModerationHotkeyKey;
        Settings.BordersHotkeyModifiers = _originalBordersHotkeyModifiers;
        Settings.BordersHotkeyKey = _originalBordersHotkeyKey;
    }

    private void DrawHotkeysSection(ID2D1DCRenderTarget target, float x, float width)
    {
        float y = TitleBarHeight + Padding;

        using (var header = DWriteFactory.CreateTextLayout(LocalizationService.T("Settings_Section_Hotkeys"), _headerFormat!, width, 24f))
            target.DrawTextLayout(new System.Numerics.Vector2(x, y), header, _textBrush!);
        y += 32f;

        _checkboxRects.Clear();
        y = DrawCheckboxField(target, x, width, y, "Settings_Hotkeys_Enable", _enableGlobalHotkeys, "EnableGlobalHotkeys");
        y += FieldGap;

        y = DrawHotkeyCaptureField(target, x, width, y, "Settings_Hotkeys_ToggleEvents", _eventsHotkeyModifiers, _eventsHotkeyKey, "Events", out _eventsHotkeyRect);
        y = DrawHotkeyCaptureField(target, x, width, y, "Settings_Hotkeys_OpenModeration", _moderationHotkeyModifiers, _moderationHotkeyKey, "Moderation", out _moderationHotkeyRect);
        y = DrawHotkeyCaptureField(target, x, width, y, "Settings_Hotkeys_ToggleBorders", _bordersHotkeyModifiers, _bordersHotkeyKey, "Borders", out _bordersHotkeyRect);

        y += LabelGap;
        using var info = DWriteFactory.CreateTextLayout(LocalizationService.T("Settings_Hotkeys_Info"), _labelFormat!, width, 40f);
        target.DrawTextLayout(new System.Numerics.Vector2(x, y), info, _secondaryBrush!);
    }

    private float DrawHotkeyCaptureField(ID2D1DCRenderTarget target, float x, float width, float y, string labelKey, uint modifiers, uint vk, string fieldId, out Rect fieldRect)
    {
        bool disabled = !_enableGlobalHotkeys;
        bool capturing = _capturingHotkeyField == fieldId;

        using (var label = DWriteFactory.CreateTextLayout(LocalizationService.T(labelKey), _labelFormat!, width, 18f))
            target.DrawTextLayout(new System.Numerics.Vector2(x, y), label, _secondaryBrush!);
        y += 18f + LabelGap;

        fieldRect = new Rect(x, y, width, FieldHeight);
        if (!disabled)
            DrawHoverShadow(target, fieldRect);
        target.FillRectangle(fieldRect, _fieldBackgroundBrush!);
        target.DrawRectangle(fieldRect, capturing ? _checkboxBrush! : _fieldBorderBrush!, capturing ? 1.5f : 1f);

        string display = _hotkeyErrorField == fieldId && _hotkeyErrorMessage is not null
            ? _hotkeyErrorMessage
            : FormatHotkeyDisplay(modifiers, vk);

        using (var valueLayout = DWriteFactory.CreateTextLayout(display, _fieldFormat!, fieldRect.Width - 12f, fieldRect.Height))
            target.DrawTextLayout(new System.Numerics.Vector2(fieldRect.Left + 8f, fieldRect.Top), valueLayout, disabled ? _secondaryBrush! : _textBrush!);

        return y + FieldHeight + FieldGap;
    }

    private static string FormatHotkeyDisplay(uint modifiers, uint vk)
    {
        if (vk == 0)
            return LocalizationService.T("Settings_Hotkey_Unassigned");

        var parts = new List<string>();
        if ((modifiers & HotkeyModifiers.Control) != 0)
            parts.Add("Ctrl");
        if ((modifiers & HotkeyModifiers.Shift) != 0)
            parts.Add("Shift");
        if ((modifiers & HotkeyModifiers.Alt) != 0)
            parts.Add("Alt");
        if ((modifiers & HotkeyModifiers.Win) != 0)
            parts.Add("Win");
        parts.Add(GetKeyDisplayName(vk));
        return string.Join(" + ", parts);
    }

    private static string GetKeyDisplayName(uint vk)
    {
        if (VirtualKeyNames.TryGetValue(vk, out var name))
            return name;
        if (vk >= 0x30 && vk <= 0x39)
            return ((char)vk).ToString();
        if (vk >= 0x41 && vk <= 0x5A)
            return ((char)vk).ToString();

        var keyboardState = new byte[256];
        uint scanCode = Win32.MapVirtualKey(vk, Win32.MAPVK_VK_TO_VSC);
        var buffer = new System.Text.StringBuilder(8);
        int result = Win32.ToUnicodeEx(vk, scanCode, keyboardState, buffer, buffer.Capacity, 0, Win32.GetKeyboardLayout(0));
        if (result > 0)
        {
            char ch = char.ToUpperInvariant(buffer[0]);
            if (!char.IsControl(ch))
                return ch.ToString();
        }

        return $"VK 0x{vk:X2}";
    }

    private static readonly Dictionary<uint, string> VirtualKeyNames = new()
    {
        [0x08] = "Backspace",
        [0x09] = "Tab",
        [0x0D] = "Enter",
        [0x1B] = "Escape",
        [0x20] = "Space",
        [0x21] = "Page Up",
        [0x22] = "Page Down",
        [0x23] = "End",
        [0x24] = "Home",
        [0x25] = "Left",
        [0x26] = "Up",
        [0x27] = "Right",
        [0x28] = "Down",
        [0x2D] = "Insert",
        [0x2E] = "Delete",
        [0x60] = "Numpad 0",
        [0x61] = "Numpad 1",
        [0x62] = "Numpad 2",
        [0x63] = "Numpad 3",
        [0x64] = "Numpad 4",
        [0x65] = "Numpad 5",
        [0x66] = "Numpad 6",
        [0x67] = "Numpad 7",
        [0x68] = "Numpad 8",
        [0x69] = "Numpad 9",
        [0x6A] = "Numpad *",
        [0x6B] = "Numpad +",
        [0x6D] = "Numpad -",
        [0x6E] = "Numpad .",
        [0x6F] = "Numpad /",
        [0x70] = "F1",
        [0x71] = "F2",
        [0x72] = "F3",
        [0x73] = "F4",
        [0x74] = "F5",
        [0x75] = "F6",
        [0x76] = "F7",
        [0x77] = "F8",
        [0x78] = "F9",
        [0x79] = "F10",
        [0x7A] = "F11",
        [0x7B] = "F12",
        [0x7C] = "F13",
        [0x7D] = "F14",
        [0x7E] = "F15",
        [0x7F] = "F16",
        [0x80] = "F17",
        [0x81] = "F18",
        [0x82] = "F19",
        [0x83] = "F20",
        [0x84] = "F21",
        [0x85] = "F22",
        [0x86] = "F23",
        [0x87] = "F24",
        [0x90] = "Num Lock",
        [0x91] = "Scroll Lock",
    };
    private void HandleHotkeysSectionClick(int clientX, int clientY)
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

        if (!_enableGlobalHotkeys)
            return;

        if (Contains(_eventsHotkeyRect, clientX, clientY))
            BeginHotkeyCapture("Events");
        else if (Contains(_moderationHotkeyRect, clientX, clientY))
            BeginHotkeyCapture("Moderation");
        else if (Contains(_bordersHotkeyRect, clientX, clientY))
            BeginHotkeyCapture("Borders");
        else
            _capturingHotkeyField = null;

        RequestRender();
    }
    private void BeginHotkeyCapture(string field)
    {
        _capturingHotkeyField = field;
        _hotkeyErrorField = null;
        _hotkeyErrorMessage = null;
    }

    private void HandleHotkeyCaptureKeyDown(int virtualKeyCode, bool ctrlDown, bool shiftDown)
    {
        if (virtualKeyCode is Win32.VK_SHIFT or Win32.VK_CONTROL or Win32.VK_MENU or Win32.VK_LWIN or Win32.VK_RWIN)
            return;

        string field = _capturingHotkeyField!;

        if (virtualKeyCode == Win32.VK_ESCAPE)
        {
            SetHotkeyValue(field, 0, 0);
            _capturingHotkeyField = null;
            _hotkeyErrorField = null;
            _hotkeyErrorMessage = null;
            RequestRender();
            return;
        }

        uint modifiers = 0;
        if (ctrlDown)
            modifiers |= HotkeyModifiers.Control;
        if (shiftDown)
            modifiers |= HotkeyModifiers.Shift;
        if ((Win32.GetKeyState(Win32.VK_MENU) & 0x8000) != 0)
            modifiers |= HotkeyModifiers.Alt;
        if ((Win32.GetKeyState(Win32.VK_LWIN) & 0x8000) != 0 || (Win32.GetKeyState(Win32.VK_RWIN) & 0x8000) != 0)
            modifiers |= HotkeyModifiers.Win;

        if (modifiers == 0)
        {
            _hotkeyErrorField = field;
            _hotkeyErrorMessage = LocalizationService.T("Settings_Hotkey_NeedsModifier");
            RequestRender();
            return;
        }

        var vk = (uint)virtualKeyCode;
        if (IsHotkeyTakenByOther(field, modifiers, vk))
        {
            _hotkeyErrorField = field;
            _hotkeyErrorMessage = LocalizationService.T("Settings_Hotkey_AlreadyTaken");
            RequestRender();
            return;
        }

        SetHotkeyValue(field, modifiers, vk);
        _capturingHotkeyField = null;
        _hotkeyErrorField = null;
        _hotkeyErrorMessage = null;
        RequestRender();
    }
    private void SetHotkeyValue(string field, uint modifiers, uint vk)
    {
        switch (field)
        {
            case "Events":
                _eventsHotkeyModifiers = modifiers;
                _eventsHotkeyKey = vk;
                Settings.EventsHotkeyModifiers = modifiers;
                Settings.EventsHotkeyKey = vk;
                break;
            case "Moderation":
                _moderationHotkeyModifiers = modifiers;
                _moderationHotkeyKey = vk;
                Settings.ModerationHotkeyModifiers = modifiers;
                Settings.ModerationHotkeyKey = vk;
                break;
            case "Borders":
                _bordersHotkeyModifiers = modifiers;
                _bordersHotkeyKey = vk;
                Settings.BordersHotkeyModifiers = modifiers;
                Settings.BordersHotkeyKey = vk;
                break;
        }
    }
    private bool IsHotkeyTakenByOther(string editingField, uint modifiers, uint vk)
    {
        foreach (var (field, mod, key) in new[]
        {
            ("Events", _eventsHotkeyModifiers, _eventsHotkeyKey),
            ("Moderation", _moderationHotkeyModifiers, _moderationHotkeyKey),
            ("Borders", _bordersHotkeyModifiers, _bordersHotkeyKey),
        })
        {
            if (field != editingField && mod == modifiers && key == vk)
                return true;
        }
        return false;
    }
}
