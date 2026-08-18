using System.Text.RegularExpressions;
using TTNOverlay.Models;
using TTNOverlay.Native;
using TTNOverlay.Services;
using Vortice.Direct2D1;
using Vortice.DirectWrite;

namespace TTNOverlay.Overlay;

/// <summary>
/// Main overlay window (native, layered): core fields, layout constants, and window lifecycle. Behavior is split across the ChatRenderWindow.*.cs partials in the Chat/, Messages/, Moderation/, Rendering/ and Caching/ folders.
/// </summary>
internal sealed partial class ChatRenderWindow : OverlayWindowBase
{
    private const int ToggleClickThroughHotkeyId = 9002;
    private const uint VK_T = 0x54;

    private const float Padding = 8f;
    private const float MessageSpacing = 10f;
    private const float UsernameToBodySpacing = 2f;
    private const float BadgeSpacing = 3f;
    private const float EmoteSpacing = 3f;
    private const float BodyLineHeightFactor = 1.35f;

    private const float SettingsButtonWidth = 30f;
    private const float BordersButtonWidth = 40f;
    private const float CloseButtonWidth = 40f;

    private const int ExpandedTitleBarHeight = 40;

    private const float MentionPaddingX = 4f;
    private const float MentionCornerRadius = 4f;
    private const float MentionBorderThickness = 1f;

    private readonly MessageBuffer _messages = new();
    private bool _clickThroughEnabled;

    private ScrollState _messagesScroll;
    private ChatMessage? _messagesLastNewestMsg;
    private ScrollState _eventsScroll;
    private ChatMessage? _eventsLastNewestMsg;
    private const float ScrollStepPx = 60f;

    private enum TitleBarButton
    {
        None,
        Settings,
        Borders,
        Close,
    }

    private TitleBarButton _hoveredButton = TitleBarButton.None;
    private float _settingsHoverProgress;
    private float _bordersHoverProgress;
    private float _closeHoverProgress;
    private System.Threading.Timer? _hoverAnimationTimer;
    private DateTime _lastHoverTickUtc;

    private System.Threading.Timer? _expirySweepTimer;

    private ID2D1DCRenderTarget? _target;

    private bool? _lastKnownIsDark;

    private ID2D1SolidColorBrush? _titleBarBrush;

    private ID2D1SolidColorBrush? _moderationBackgroundBrush;

    private ID2D1SolidColorBrush? _titleBarForegroundBrush;

    private IDWriteTextFormat? _titleBarButtonFormat;

    private ID2D1SolidColorBrush? _titleBarHoverBrush;
    private ID2D1SolidColorBrush? _closeHoverBrush;

    // Connection status dots (title bar, Multichat with both sources active). See
    // ChatRenderWindow.TitleBar.cs DrawConnectionDots. One brush per state, shared by both
    // Twitch's and Kick's dot so there's no duplicate GPU resource for the same color.
    private ID2D1SolidColorBrush? _connectionDotConnectedBrush;
    private ID2D1SolidColorBrush? _connectionDotConnectingBrush;
    private ID2D1SolidColorBrush? _connectionDotErrorBrush;

    private IDWriteTextFormat? _titleBarLabelFormat;

    private ID2D1SolidColorBrush? _bodyBrush;
    private ID2D1SolidColorBrush? _systemBrush;
    private ID2D1SolidColorBrush? _resizeGripBrush;

    private ID2D1SolidColorBrush? _hitTestCatcherBrush;

    private ID2D1SolidColorBrush? _flashBrush;

    private ID2D1SolidColorBrush? _mentionBackgroundBrush;
    private ID2D1SolidColorBrush? _mentionBorderBrush;

    private ID2D1SolidColorBrush? _outlineBrush;

    private string? _mentionRegexChannel;
    private Regex? _mentionRegex;

    private IDWriteTextFormat? _usernameFormat;
    private IDWriteTextFormat? _bodyFormat;
    private IDWriteTextFormat? _systemFormat;

    public ChatRenderWindow()
        : base("TTNOverlayChatRenderWndClass") { }

    protected override void OnCreated()
    {
        DebugLog.Write("ChatRenderWindow: OnCreated");
        Win32.RegisterHotKey(
            Hwnd,
            ToggleClickThroughHotkeyId,
            HotkeyModifiers.Control | HotkeyModifiers.Alt,
            VK_T
        );
        SetupAlerts();
        SetupViewerCountWidget();
        ConnectFeed();
        ConnectTrayAndHotkeys();
        EnsureExpirySweepTimerRunning();
        EnsureMediaStatsTimerRunning();

        LocalizationService.Instance.LanguageChanged += OnLanguageChanged;
    }

    private void OnLanguageChanged() => PostToUiThread(() =>
    {
        RebuildConnectionStatusText();
        RequestRender();
    });

    private System.Threading.Timer? _mediaStatsTimer;

    private void EnsureMediaStatsTimerRunning()
    {
        _mediaStatsTimer ??= new System.Threading.Timer(
            _ => PostToUiThread(DumpMediaCacheStats),
            null,
            5000,
            30000
        );
    }

    protected override void OnMouseWheel(int delta, int clientX, int clientY)
    {

        CloseModerationDropdown();

        float deltaPx = (delta / 120f) * ScrollStepPx;

        if (_showingModeration)
            _moderationScroll.ApplyWheel(deltaPx, invert: true);
        else if (_showingEvents)
            _eventsScroll.ApplyWheel(deltaPx);
        else
            _messagesScroll.ApplyWheel(deltaPx);

        RequestRender();
    }

    protected override bool OnCustomMessage(uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == Win32.WM_HOTKEY && wParam.ToInt32() == ToggleClickThroughHotkeyId)
        {
            _clickThroughEnabled = !_clickThroughEnabled;
            SetClickThrough(_clickThroughEnabled);
            RequestRender();
            return true;
        }

        return HandleTrayHotkeyMessage(msg, wParam, lParam);
    }

    protected override int MinimumClientWidth =>
        _bordersHidden ? 60 : (int)(SettingsButtonWidth + BordersButtonWidth + CloseButtonWidth) + 40;

    protected override int MinimumClientHeight => TitleBarHeight + ResizeGripSize + 40;

    protected override bool OnClosing()
    {
        Win32.UnregisterHotKey(Hwnd, ToggleClickThroughHotkeyId);
        PersistWindowGeometry();
        return true;
    }

    private void PersistWindowGeometry()
    {
        if (!Win32.GetWindowRect(Hwnd, out var rect))
            return;

        if (_showingModeration)
        {
            _settings.ModerationWindowWidth = rect.Right - rect.Left;
            _settings.ModerationWindowHeight = rect.Bottom - rect.Top;

            if (_normalWidthBeforeModeration.HasValue && _normalHeightBeforeModeration.HasValue)
            {
                _settings.WindowWidth = _normalWidthBeforeModeration.Value;
                _settings.WindowHeight = _normalHeightBeforeModeration.Value;
            }
        }
        else
        {
            _settings.WindowLeft = rect.Left;
            _settings.WindowTop = rect.Top;
            _settings.WindowWidth = rect.Right - rect.Left;
            _settings.WindowHeight = rect.Bottom - rect.Top;
        }
        SettingsService.Save(_settings);
    }

    private void InvalidateSettingsDependentResources()
    {
        _titleBarBrush?.Dispose();
        _titleBarBrush = null;
        _moderationBackgroundBrush?.Dispose();
        _moderationBackgroundBrush = null;
        _dropdownBackgroundBrush?.Dispose();
        _dropdownBackgroundBrush = null;
        _moderationTextBrush?.Dispose();
        _moderationTextBrush = null;
        _moderationPillBrush?.Dispose();
        _moderationPillBrush = null;
        _moderationSecondaryBrush?.Dispose();
        _moderationSecondaryBrush = null;
        _moderationHeaderFormat?.Dispose();
        _moderationHeaderFormat = null;
        _moderationBodyFormat?.Dispose();
        _moderationBodyFormat = null;
        _dropdownBorderBrush?.Dispose();
        _dropdownBorderBrush = null;
        _dropdownHoverBrush?.Dispose();
        _dropdownHoverBrush = null;
        _titleBarForegroundBrush?.Dispose();
        _titleBarForegroundBrush = null;
        _titleBarHoverBrush?.Dispose();
        _titleBarHoverBrush = null;
        _viewerCountBadgeBrush?.Dispose();
        _viewerCountBadgeBrush = null;
        _viewerCountTextBrush?.Dispose();
        _viewerCountTextBrush = null;
        _viewerCountFormat?.Dispose();
        _viewerCountFormat = null;
        _bodyBrush?.Dispose();
        _bodyBrush = null;
        _systemBrush?.Dispose();
        _systemBrush = null;
        _resizeGripBrush?.Dispose();
        _resizeGripBrush = null;
        _mentionBackgroundBrush?.Dispose();
        _mentionBackgroundBrush = null;
        _mentionBorderBrush?.Dispose();
        _mentionBorderBrush = null;
        _outlineBrush?.Dispose();
        _outlineBrush = null;
        _usernameFormat?.Dispose();
        _usernameFormat = null;
        _bodyFormat?.Dispose();
        _bodyFormat = null;
        _systemFormat?.Dispose();
        _systemFormat = null;
        DisposeTwitchButtonResources();

        InvalidateWordLayoutCache();
        InvalidateBodyLayoutCache();
        InvalidateUsernameLayoutCache();

        _eventTextBrush?.Dispose();
        _eventTextBrush = null;
        _eventIconFormat?.Dispose();
        _eventIconFormat = null;
        _eventNameFormat?.Dispose();
        _eventNameFormat = null;
        _eventBodyFormat?.Dispose();
        _eventBodyFormat = null;

    }

    protected override void OnDestroyed()
    {
        LocalizationService.Instance.LanguageChanged -= OnLanguageChanged;
        DisconnectFeed();
        DisconnectTrayAndHotkeys();
        DisconnectDashboard();
        DisconnectFlash();
        DisconnectViewerCount();
        DisconnectModeration();
        _titleBarBrush?.Dispose();
        _moderationBackgroundBrush?.Dispose();
        _dropdownBackgroundBrush?.Dispose();
        _dropdownBorderBrush?.Dispose();
        _dropdownHoverBrush?.Dispose();
        _dropdownItemFormat?.Dispose();
        _titleBarButtonFormat?.Dispose();
        _titleBarLabelFormat?.Dispose();
        _titleBarHoverBrush?.Dispose();
        _closeHoverBrush?.Dispose();
        _connectionDotConnectedBrush?.Dispose();
        _connectionDotConnectingBrush?.Dispose();
        _connectionDotErrorBrush?.Dispose();
        _hoverAnimationTimer?.Dispose();
        _viewerCountBadgeBrush?.Dispose();
        _viewerCountTextBrush?.Dispose();
        _viewerCountFormat?.Dispose();
        _bodyBrush?.Dispose();
        _systemBrush?.Dispose();
        _resizeGripBrush?.Dispose();
        _mentionBackgroundBrush?.Dispose();
        _mentionBorderBrush?.Dispose();
        _outlineBrush?.Dispose();
        DisposeImageCaches();
        _animationTimer?.Dispose();
        _expirySweepTimer?.Dispose();
        _usernameFormat?.Dispose();
        _bodyFormat?.Dispose();
        _systemFormat?.Dispose();
        DisposeTwitchButtonResources();
        InvalidateWordLayoutCache();
        InvalidateBodyLayoutCache();
        InvalidateUsernameLayoutCache();
        _eventTextBrush?.Dispose();
        _eventIconFormat?.Dispose();
        _eventNameFormat?.Dispose();
        _eventBodyFormat?.Dispose();
        _moderationHeaderFormat?.Dispose();
        _moderationBodyFormat?.Dispose();
    }

    protected override void OnDeviceResourcesInvalidated()
    {
        _target = null;

        InvalidateSettingsDependentResources();

        _closeHoverBrush?.Dispose();
        _closeHoverBrush = null;
        _hitTestCatcherBrush?.Dispose();
        _hitTestCatcherBrush = null;
        _flashBrush?.Dispose();
        _flashBrush = null;

        DisposeImageCaches();
    }

    public void ShowConfirmDialog(string title, string message, string? confirmText, Action<bool> callback)
    {
        DebugLog.Write($"ShowConfirmDialog: called from thread {Environment.CurrentManagedThreadId} (UI thread={OverlayWindowBase.IsOnUiThread})");
        PostToUiThread(() =>
        {
            DebugLog.Write("ShowConfirmDialog: within PostToUiThread, by calling ConfirmDialogWindow.Show");
            ConfirmDialogWindow.Show(Hwnd, PostToUiThread, title, message, confirmText, callback);
            DebugLog.Write("ShowConfirmDialog: ConfirmDialogWindow.Show return");
        });
    }
    public UpdateProgressDialogWindow ShowUpdateProgressDialog(string title)
    => UpdateProgressDialogWindow.Show(Hwnd, PostToUiThread, title);
    public void ShowReleaseNotesDialog(string title, string notes)
    => ReleaseNotesDialogWindow.Show(Hwnd, PostToUiThread, title, notes, LocalizationService.T("Update_CloseButton"));
}