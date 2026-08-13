namespace TTNOverlay.Overlay;

/// <summary>
/// Creates and manages the Windows system tray icon and its context menu.
/// </summary>
public sealed class TrayIcon : IDisposable
{
    private const int IconId = 1;
    public const uint WM_TRAYICON = Win32.WM_APP + 1;

    private const uint MF_STRING = 0x0;
    private const uint MF_SEPARATOR = 0x800;
    private const uint TPM_RIGHTBUTTON = 0x0002;
    private const uint TPM_RETURNCMD = 0x0100;

    private const int CmdToggleBorders = 1;
    private const int CmdSettings = 2;
    private const int CmdModerationPanel = 3;
    private const int CmdExit = 4;

    private const int NIM_ADD = 0x0;
    private const int NIM_DELETE = 0x2;
    private const int NIF_MESSAGE = 0x1;
    private const int NIF_ICON = 0x2;
    private const int NIF_TIP = 0x4;

    private static readonly IntPtr IDI_APPLICATION = new(32512);

    private readonly IntPtr _hwnd;
    private IntPtr _hIcon = IntPtr.Zero;
    private bool _iconIsOwned;
    private bool _added;

    public event Action? ToggleBordersRequested;
    public event Action? OpenSettingsRequested;
    public event Action? OpenModerationPanelRequested;
    public event Action? ExitRequested;

    public TrayIcon(IntPtr hwnd)
    {
        _hwnd = hwnd;

        _hIcon = LoadAppIcon(out _iconIsOwned);

        var data = new Win32.NOTIFYICONDATA
        {
            cbSize = System.Runtime.InteropServices.Marshal.SizeOf<Win32.NOTIFYICONDATA>(),
            hWnd = _hwnd,
            uID = IconId,
            uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
            uCallbackMessage = (int)WM_TRAYICON,
            hIcon = _hIcon,
            szTip = "TTN Overlay",
        };
        _added = Win32.Shell_NotifyIcon(NIM_ADD, ref data);
    }

    private static IntPtr LoadAppIcon(out bool iconIsOwned)
    {
        try
        {
            var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
            if (!string.IsNullOrEmpty(exePath))
            {
                var large = new IntPtr[1];
                var small = new IntPtr[1];
                int count = Win32.ExtractIconEx(exePath, 0, large, small, 1);
                if (count > 0)
                {
                    if (small[0] != IntPtr.Zero)
                    {
                        if (large[0] != IntPtr.Zero && large[0] != small[0])
                            Win32.DestroyIcon(large[0]);
                        iconIsOwned = true;
                        return small[0];
                    }
                    if (large[0] != IntPtr.Zero)
                    {
                        iconIsOwned = true;
                        return large[0];
                    }
                }
            }
        }
        catch
        {

        }

        iconIsOwned = false;
        return Win32.LoadIcon(IntPtr.Zero, IDI_APPLICATION);
    }

    public bool HandleTrayMessage(IntPtr wParam, IntPtr lParam)
    {
        if (wParam.ToInt32() != IconId)
            return false;

        int mouseMsg = unchecked((int)lParam.ToInt64());

        if (mouseMsg == (int)Win32.WM_RBUTTONUP || mouseMsg == (int)Win32.WM_CONTEXTMENU)
        {
            ShowContextMenu();
            return true;
        }

        if (mouseMsg == (int)Win32.WM_LBUTTONDBLCLK)
        {
            OpenSettingsRequested?.Invoke();
            return true;
        }

        return false;
    }

    private void ShowContextMenu()
    {
        Win32.GetCursorPos(out var cursor);

        var hMenu = Win32.CreatePopupMenu();
        if (hMenu == IntPtr.Zero)
            return;

        try
        {
            Win32.AppendMenu(
                hMenu,
                MF_STRING,
                (IntPtr)CmdToggleBorders,
                Services.LocalizationService.T("Tray_ToggleBorders")
            );
            Win32.AppendMenu(hMenu, MF_STRING, (IntPtr)CmdSettings, Services.LocalizationService.T("Tray_Settings"));
            Win32.AppendMenu(
                hMenu,
                MF_STRING,
                (IntPtr)CmdModerationPanel,
                Services.LocalizationService.T("Tray_ModerationPanel")
            );
            Win32.AppendMenu(hMenu, MF_SEPARATOR, IntPtr.Zero, null);
            Win32.AppendMenu(hMenu, MF_STRING, (IntPtr)CmdExit, Services.LocalizationService.T("Tray_Exit"));

            Win32.SetForegroundWindow(_hwnd);

            int selected = Win32.TrackPopupMenuEx(
                hMenu,
                TPM_RIGHTBUTTON | TPM_RETURNCMD,
                cursor.X,
                cursor.Y,
                _hwnd,
                IntPtr.Zero
            );

            Win32.PostMessage(_hwnd, Win32.WM_NULL, IntPtr.Zero, IntPtr.Zero);

            switch (selected)
            {
                case CmdToggleBorders:
                    ToggleBordersRequested?.Invoke();
                    break;
                case CmdSettings:
                    OpenSettingsRequested?.Invoke();
                    break;
                case CmdModerationPanel:
                    OpenModerationPanelRequested?.Invoke();
                    break;
                case CmdExit:
                    ExitRequested?.Invoke();
                    break;
            }
        }
        finally
        {
            Win32.DestroyMenu(hMenu);
        }
    }

    public void Dispose()
    {
        if (_added)
        {
            var data = new Win32.NOTIFYICONDATA
            {
                cbSize = System.Runtime.InteropServices.Marshal.SizeOf<Win32.NOTIFYICONDATA>(),
                hWnd = _hwnd,
                uID = IconId,
            };
            Win32.Shell_NotifyIcon(NIM_DELETE, ref data);
            _added = false;
        }

        if (_hIcon != IntPtr.Zero)
        {
            if (_iconIsOwned)
                Win32.DestroyIcon(_hIcon);
            _hIcon = IntPtr.Zero;
        }
    }
}

