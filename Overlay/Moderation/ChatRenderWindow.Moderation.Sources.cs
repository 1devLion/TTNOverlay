using TTNOverlay.Kick;
using TTNOverlay.Models;
using TTNOverlay.Services;
using TTNOverlay.Twitch;
using Rect = Vortice.Mathematics.Rect;

namespace TTNOverlay.Overlay;

/// <summary>
/// ChatRenderWindow partial: the moderation panel's platform mode ("Multichat", "Kick" or "Twitch") and the extra,
/// moderation-only chat connections that make it independent of the overlay's chat source setting.
/// </summary>
/// <remarks>
/// The overlay shows whatever Settings.ChatSourceMode says. The panel can manage a platform the overlay is not
/// showing (for example Kick while the overlay is Twitch-only), and for that it needs that platform's messages with
/// their ids. So while the panel is open, a platform that is wanted by the panel but not displayed gets its own
/// connection that only feeds the Messages tab log; nothing is added to the chat list. Those connections close when
/// the panel closes, so they cost nothing the rest of the time.
/// </remarks>
internal sealed partial class ChatRenderWindow
{
    internal const string ModeMultichat = "Multichat";
    internal const string ModeKick = "Kick";
    internal const string ModeTwitch = "Twitch";

    private const int ModerationSourceRetrySeconds = 10;

    private ITwitchIrcClient? _modIrc;
    private IKickChatClient? _modKick;
    private string _modKickChannel = "";
    private System.Threading.Timer? _modIrcRetryTimer;
    private System.Threading.Timer? _modKickRetryTimer;

    private readonly List<(Rect Bounds, string Mode)> _moderationModeRects = new();

    private string ModerationMode =>
        _settings.ModerationPlatformMode is ModeKick or ModeTwitch ? _settings.ModerationPlatformMode : ModeMultichat;

    private bool ModerationShowsTwitch => ModerationMode != ModeKick;

    private bool ModerationShowsKick => ModerationMode != ModeTwitch;

    private bool ModerationShowsPlatform(Platform platform) =>
        platform == Platform.Kick ? ModerationShowsKick : ModerationShowsTwitch;

    /// <summary>The Kick channel the panel acts on: the displayed one, or the configured/own channel otherwise.</summary>
    private string KickModerationChannel
    {
        get
        {
            if (_kickActive && !string.IsNullOrWhiteSpace(_kickChannelSlug))
                return _kickChannelSlug;

            string configured =
                _settings.ChatSourceMode == ModeMultichat && _settings.MultichatUseSameChannel
                    ? _settings.Channel
                    : _settings.KickChannel;
            if (!string.IsNullOrWhiteSpace(configured))
                return configured.Trim();

            // Nothing configured: a streamer moderating their own channel uses their Kick login as the slug.
            return _settings.KickLogin?.Trim() ?? "";
        }
    }

    private void SetModerationMode(string mode)
    {
        if (string.Equals(ModerationMode, mode, StringComparison.Ordinal))
            return;

        _settings.ModerationPlatformMode = mode;
        SettingsService.Save(_settings);

        // AutoMod is a Twitch feature.
        if (!ModerationShowsTwitch && _moderationTab == ModerationTab.AutoMod)
            _moderationTab = ModerationTab.Messages;

        _moderationMessagesLastNewest = null;
        _moderationMessagesScroll = default;
        EnsureModerationSources();
        RequestRender();
    }

    private void OpenModerationModeDropdown(Rect anchor)
    {
        var items = new List<ModerationDropdownItem>();
        foreach (var mode in new[] { ModeMultichat, ModeKick, ModeTwitch })
        {
            string selected = mode;
            items.Add(
                new ModerationDropdownItem
                {
                    Label = (selected == ModerationMode ? "\u2713  " : "     ") + selected,
                    OnSelect = () => SetModerationMode(selected),
                }
            );
        }
        OpenModerationDropdown(anchor, items);
    }

    private void HandleModerationModeClick(int clientX, int clientY, out bool handled)
    {
        handled = false;
        foreach (var (bounds, mode) in _moderationModeRects)
        {
            if (!Contains(bounds, clientX, clientY))
                continue;

            handled = true;
            OpenModerationModeDropdown(bounds);
            return;
        }
    }

    /// <summary>
    /// Opens or closes the moderation-only connections to match the panel's mode and logins. Safe to call any time
    /// (panel open/close, mode change, login/logout, feed reconnect).
    /// </summary>
    private void EnsureModerationSources()
    {
        bool panelOpen = _showingModeration && _settings.EnableModerationPanel;

        bool wantTwitch =
            panelOpen
            && ModerationShowsTwitch
            && !_twitchActive
            && !string.IsNullOrWhiteSpace(_settings.ModeratorRefreshToken)
            && !string.IsNullOrWhiteSpace(ModerationTwitchChannel);

        string kickChannel = KickModerationChannel;
        bool wantKick =
            panelOpen
            && ModerationShowsKick
            && !_kickActive
            && !string.IsNullOrWhiteSpace(_settings.KickRefreshToken)
            && !string.IsNullOrWhiteSpace(kickChannel);

        if (wantTwitch && _modIrc is null)
            StartModerationTwitch(ModerationTwitchChannel);
        else if (!wantTwitch && _modIrc is not null)
            StopModerationTwitch();

        if (wantKick && (_modKick is null || !string.Equals(_modKickChannel, kickChannel, StringComparison.OrdinalIgnoreCase)))
        {
            StopModerationKick();
            StartModerationKick(kickChannel);
        }
        else if (!wantKick && _modKick is not null)
            StopModerationKick();
    }

    private string ModerationTwitchChannel =>
        !string.IsNullOrWhiteSpace(_settings.Channel) ? _settings.Channel.Trim() : _settings.ModeratorLogin?.Trim() ?? "";

    private void StopModerationSources()
    {
        StopModerationTwitch();
        StopModerationKick();
    }

    // ---------------------------- Twitch (moderation only) ----------------------------

    private void StartModerationTwitch(string channel)
    {
        var irc = new TwitchIrcClient();
        _modIrc = irc;

        irc.MessageReceived += OnModIrcMessage;
        irc.MessageDeleted += OnIrcMessageDeleted;
        irc.UserPurged += OnIrcUserPurged;
        irc.ChatCleared += OnIrcChatCleared;
        irc.Disconnected += OnModIrcDown;
        irc.Error += OnModIrcError;

        _ = ConnectModIrcAsync(irc, channel);
    }

    private static async Task ConnectModIrcAsync(ITwitchIrcClient irc, string channel)
    {
        try
        {
            await irc.ConnectAsync(channel);
        }
        catch (Exception ex)
        {
            DebugLog.WriteException("Moderation.ConnectModIrcAsync", ex);
        }
    }

    private void OnModIrcMessage(ChatMessage msg) =>
        PostToUiThread(() =>
        {
            msg.SourcePlatform = Platform.Twitch;
            LogMessageForModeration(msg);
            if (ModerationMessagesVisible)
                RequestRender();
        });

    private void OnModIrcDown(string reason) => PostToUiThread(ScheduleModIrcRetry);

    private void OnModIrcError(Exception ex)
    {
        DebugLog.WriteException("Moderation.ModIrc", ex);
        PostToUiThread(ScheduleModIrcRetry);
    }

    private void ScheduleModIrcRetry()
    {
        if (_modIrc is null || _modIrcRetryTimer is not null)
            return;

        _modIrcRetryTimer = new System.Threading.Timer(
            _ => PostToUiThread(() =>
            {
                _modIrcRetryTimer?.Dispose();
                _modIrcRetryTimer = null;
                if (_modIrc is { } irc)
                    _ = ConnectModIrcAsync(irc, ModerationTwitchChannel);
            }),
            null,
            TimeSpan.FromSeconds(ModerationSourceRetrySeconds),
            System.Threading.Timeout.InfiniteTimeSpan
        );
    }

    private void StopModerationTwitch()
    {
        _modIrcRetryTimer?.Dispose();
        _modIrcRetryTimer = null;

        if (_modIrc is not { } irc)
            return;

        _modIrc = null;
        irc.MessageReceived -= OnModIrcMessage;
        irc.MessageDeleted -= OnIrcMessageDeleted;
        irc.UserPurged -= OnIrcUserPurged;
        irc.ChatCleared -= OnIrcChatCleared;
        irc.Disconnected -= OnModIrcDown;
        irc.Error -= OnModIrcError;
        _ = irc.DisposeAsync().AsTask();

        // Those rows only existed because of this connection.
        if (!_twitchActive)
            ClearModerationLog(Platform.Twitch);
    }

    // ----------------------------- Kick (moderation only) -----------------------------

    private void StartModerationKick(string channel)
    {
        var kick = new KickChatClient();
        _modKick = kick;
        _modKickChannel = channel;

        kick.MessageReceived += OnModKickMessage;
        kick.MessageDeleted += OnKickMessageDeleted;
        kick.UserBanned += OnKickUserBanned;
        kick.UserUnbanned += OnKickUserUnbanned;
        kick.ChatCleared += OnKickChatCleared;
        kick.Disconnected += OnModKickDown;
        kick.Error += OnModKickError;

        _ = ConnectModKickAsync(kick, channel);
    }

    private static async Task ConnectModKickAsync(IKickChatClient kick, string channel)
    {
        try
        {
            await kick.ConnectAsync(channel);
        }
        catch (Exception ex)
        {
            DebugLog.WriteException("Moderation.ConnectModKickAsync", ex);
        }
    }

    private void OnModKickMessage(ChatMessage msg) =>
        PostToUiThread(() =>
        {
            msg.SourcePlatform = Platform.Kick;
            LogMessageForModeration(msg);
            if (ModerationMessagesVisible)
                RequestRender();
        });

    private void OnModKickDown(string reason) => PostToUiThread(ScheduleModKickRetry);

    private void OnModKickError(Exception ex)
    {
        DebugLog.WriteException("Moderation.ModKick", ex);
        PostToUiThread(ScheduleModKickRetry);
    }

    private void ScheduleModKickRetry()
    {
        if (_modKick is null || _modKickRetryTimer is not null)
            return;

        _modKickRetryTimer = new System.Threading.Timer(
            _ => PostToUiThread(() =>
            {
                _modKickRetryTimer?.Dispose();
                _modKickRetryTimer = null;
                if (_modKick is { } kick)
                    _ = ConnectModKickAsync(kick, _modKickChannel);
            }),
            null,
            TimeSpan.FromSeconds(ModerationSourceRetrySeconds),
            System.Threading.Timeout.InfiniteTimeSpan
        );
    }

    private void StopModerationKick()
    {
        _modKickRetryTimer?.Dispose();
        _modKickRetryTimer = null;

        if (_modKick is not { } kick)
            return;

        _modKick = null;
        kick.MessageReceived -= OnModKickMessage;
        kick.MessageDeleted -= OnKickMessageDeleted;
        kick.UserBanned -= OnKickUserBanned;
        kick.UserUnbanned -= OnKickUserUnbanned;
        kick.ChatCleared -= OnKickChatCleared;
        kick.Disconnected -= OnModKickDown;
        kick.Error -= OnModKickError;
        _ = kick.DisposeAsync().AsTask();

        if (!_kickActive)
        {
            ClearModerationLog(Platform.Kick);
            _kickSanctions.Clear();
        }
    }
}
