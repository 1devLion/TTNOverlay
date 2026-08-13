using System.ComponentModel;
using System.Runtime.InteropServices;

namespace TTNOverlay.Overlay;

/// <summary>
/// Registers and unregisters a single global hotkey for a window and raises an event when it's pressed.
/// </summary>
public sealed class HotkeyManager : IDisposable
{
    public const uint VK_F7 = 0x76;
    public const uint VK_F8 = 0x77;
    public const uint VK_F9 = 0x78;

    private readonly IntPtr _hwnd;
    private readonly int _hotkeyId;
    private uint _modifiers;
    private uint _vk;
    private bool _enabled;
    private bool _registered;

    public event Action? Pressed;

    public HotkeyManager(IntPtr hwnd, int hotkeyId, uint modifiers, uint vk, bool enabled = true)
    {
        _hwnd = hwnd;
        _hotkeyId = hotkeyId;
        _modifiers = modifiers;
        _vk = vk;
        _enabled = enabled;

        if (_enabled && _vk != 0)
        {
            _registered = Win32.RegisterHotKey(_hwnd, _hotkeyId, _modifiers, _vk);
            if (!_registered)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "No se pudo registrar el HotKey.");
        }
    }

    public bool HandleHotkeyMessage(IntPtr wParam)
    {
        if (wParam.ToInt32() != _hotkeyId)
            return false;

        Pressed?.Invoke();
        return true;
    }

    public void Rebind(uint modifiers, uint vk, bool enabled)
    {
        if (_registered)
        {
            Win32.UnregisterHotKey(_hwnd, _hotkeyId);
            _registered = false;
        }

        _modifiers = modifiers;
        _vk = vk;
        _enabled = enabled;

        if (_enabled && _vk != 0)
            _registered = Win32.RegisterHotKey(_hwnd, _hotkeyId, _modifiers, _vk);
    }

    public void SetEnabled(bool enabled)
    {
        if (enabled == _enabled)
            return;

        _enabled = enabled;

        if (enabled)
            _registered = Win32.RegisterHotKey(_hwnd, _hotkeyId, _modifiers, _vk);
        else if (_registered)
        {
            Win32.UnregisterHotKey(_hwnd, _hotkeyId);
            _registered = false;
        }
    }

    public void Dispose()
    {
        if (_registered)
        {
            Win32.UnregisterHotKey(_hwnd, _hotkeyId);
            _registered = false;
        }
    }
}

