using TTNOverlay.Services;
using TTNOverlay.Native;

namespace TTNOverlay.Overlay;

/// <summary>
/// ChatRenderWindow partial: tray icon actions and global hotkeys (toggle events/moderation/borders, open settings).
/// </summary>
internal sealed partial class ChatRenderWindow
{
    private const int EventsHotkeyId = 9101;
    private const int ModerationHotkeyId = 9102;
    private const int BordersHotkeyId = 9103;

    private HotkeyManager? _eventsHotkey;
    private HotkeyManager? _moderationHotkey;
    private HotkeyManager? _bordersHotkey;
    private TrayIcon? _trayIcon;
    private bool _bordersHidden;

    protected override int TitleBarHeight => _bordersHidden ? 0 : ExpandedTitleBarHeight;
    protected override int ResizeGripSize => _bordersHidden ? 0 : base.ResizeGripSize;

    private void ConnectTrayAndHotkeys()
    {
        _eventsHotkey = new HotkeyManager(
            Hwnd,
            EventsHotkeyId,
            _settings.EventsHotkeyModifiers,
            _settings.EventsHotkeyKey,
            _settings.EnableGlobalHotkeys
        );
        _eventsHotkey.Pressed += ToggleEventsView;

        _moderationHotkey = new HotkeyManager(
            Hwnd,
            ModerationHotkeyId,
            _settings.ModerationHotkeyModifiers,
            _settings.ModerationHotkeyKey,
            _settings.EnableGlobalHotkeys
        );
        _moderationHotkey.Pressed += ToggleModerationPanel;

        _bordersHotkey = new HotkeyManager(
            Hwnd,
            BordersHotkeyId,
            _settings.BordersHotkeyModifiers,
            _settings.BordersHotkeyKey,
            _settings.EnableGlobalHotkeys
        );
        _bordersHotkey.Pressed += ToggleBorders;

        _trayIcon = new TrayIcon(Hwnd);
        _trayIcon.ToggleBordersRequested += ToggleBorders;
        _trayIcon.OpenSettingsRequested += OpenSettings;
        _trayIcon.OpenModerationPanelRequested += ToggleModerationPanel;
        _trayIcon.ExitRequested += ExitApplication;
    }

    private bool HandleTrayHotkeyMessage(uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == Win32.WM_HOTKEY)
        {
            if (_eventsHotkey?.HandleHotkeyMessage(wParam) == true)
                return true;
            if (_moderationHotkey?.HandleHotkeyMessage(wParam) == true)
                return true;
            if (_bordersHotkey?.HandleHotkeyMessage(wParam) == true)
                return true;
            return false;
        }

        if (msg == TrayIcon.WM_TRAYICON)
            return _trayIcon?.HandleTrayMessage(wParam, lParam) == true;

        return false;
    }

    private void ToggleBorders() => SetBordersHidden(!_bordersHidden);

    private void SetBordersHidden(bool hidden)
    {
        _bordersHidden = hidden;
        SetClickThrough(_bordersHidden && _settings.ClickThrough);
        RequestRender();
    }

    private void OpenSettings() => OpenNativeSettings();

    private bool _settingsWindowOpen;

    private void OpenNativeSettings()
    {
        if (_settingsWindowOpen)
            return;

        _settingsWindowOpen = true;

        _eventsHotkey?.SetEnabled(false);
        _moderationHotkey?.SetEnabled(false);
        _bordersHotkey?.SetEnabled(false);

        var previousChannel = _settings.Channel.Trim().ToLowerInvariant();
        var previousHighQualityMedia = _settings.HighQualityMedia;
        var previousFontSize = _settings.FontSize;

        Win32.GetSizeFittingScreen(
            Hwnd,
            SettingsRenderWindow.PreferredWidth,
            SettingsRenderWindow.PreferredHeight,
            SettingsRenderWindow.MinWindowWidth,
            SettingsRenderWindow.MinWindowHeight,
            out int width,
            out int height);

        int x = 100, y = 100;
        if (!Win32.TryGetCenteredPosition(Hwnd, width, height, out x, out y))
        {
            if (Win32.GetWindowRect(Hwnd, out var overlayRect))
            {
                x = overlayRect.Left + (overlayRect.Right - overlayRect.Left - width) / 2;
                y = overlayRect.Top + (overlayRect.Bottom - overlayRect.Top - height) / 2;
            }
        }

        var title = Strings.Get("WindowTitle_Settings", LocalizationService.Instance.CurrentLanguage);

        var editableSettings = _settings.Clone();

        var settingsThread = new System.Threading.Thread(() => RunSettingsWindow(
            editableSettings,
            title, x, y, width, height,
            previousChannel, previousHighQualityMedia, previousFontSize))
        {
            IsBackground = true,
            Name = "TTNOverlay-Settings",
        };
        settingsThread.SetApartmentState(System.Threading.ApartmentState.STA);
        settingsThread.Start();
    }

    private void RunSettingsWindow(
        AppSettings editableSettings,
        string title, int x, int y, int width, int height,
        string previousChannel, bool previousHighQualityMedia, double previousFontSize)
    {
        using var wnd = new SettingsRenderWindow(editableSettings);

        wnd.TestFlashRequested += (hex, alpha) => PostToUiThread(() => TestFlashAlert(hex, alpha));

        wnd.Destroyed += () =>
        {
            PostToUiThread(() =>
            {
                _settingsWindowOpen = false;

                _settings = editableSettings;

                DisconnectModeration();

                SettingsService.Save(_settings);
                SetupAlerts();
                SetupViewerCountWidget();
                InvalidateSettingsDependentResources();

                RequestRender();

                if (_settings.HighQualityMedia != previousHighQualityMedia || _settings.FontSize != previousFontSize)
                    InvalidateMediaCaches();

                if (_settings.FontSize != previousFontSize || _settings.Channel.Trim().ToLowerInvariant() != previousChannel)
                    InvalidateMessageHeightCache();

                if (!_settings.EnableEventsPanel && _showingEvents)
                    SetView(showEvents: false);

                if (_settings.Channel.Trim().ToLowerInvariant() != previousChannel)
                {
                    ResetForChannelChange();
                    _ = ReconnectFeedAsync();
                }
                else
                {
                    ReconnectStreamlabs();
                }

                _eventsHotkey?.Rebind(
                    _settings.EventsHotkeyModifiers,
                    _settings.EventsHotkeyKey,
                    _settings.EnableGlobalHotkeys
                );
                _moderationHotkey?.Rebind(
                    _settings.ModerationHotkeyModifiers,
                    _settings.ModerationHotkeyKey,
                    _settings.EnableGlobalHotkeys
                );
                _bordersHotkey?.Rebind(
                    _settings.BordersHotkeyModifiers,
                    _settings.BordersHotkeyKey,
                    _settings.EnableGlobalHotkeys
                );
            });
        };

        wnd.Create(title, x, y, width, height);
        wnd.RunMessageLoop();
    }

    private void ExitApplication()
    {
        Win32.PostMessage(Hwnd, Win32.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
    }

    private void DisconnectTrayAndHotkeys()
    {
        _trayIcon?.Dispose();
        _trayIcon = null;
        _eventsHotkey?.Dispose();
        _eventsHotkey = null;
        _moderationHotkey?.Dispose();
        _moderationHotkey = null;
        _bordersHotkey?.Dispose();
        _bordersHotkey = null;
    }
}