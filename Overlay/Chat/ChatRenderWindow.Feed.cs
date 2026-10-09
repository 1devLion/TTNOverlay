using TTNOverlay.Kick;
using TTNOverlay.Models;
using TTNOverlay.Services;
using TTNOverlay.Twitch;

namespace TTNOverlay.Overlay;

/// <summary>
/// ChatRenderWindow partial: owns the Twitch IRC and Kick connection lifecycles and forwards incoming
/// chat messages. Which source(s) get connected is decided by Settings.ChatSourceMode ("Twitch", "Kick",
/// or "Multichat"), the same three modes SettingsRenderWindow.General.cs already exposes in the UI.
/// </summary>
internal sealed partial class ChatRenderWindow
{
    private readonly ITwitchIrcClient _irc = new TwitchIrcClient();
    private readonly IKickChatClient _kick = new KickChatClient();

    // Auto-reconnect state for each source. Chat is push-based (a persistent WebSocket), unlike
    // the viewer count widget's polling timer (ChatRenderWindow.ViewerCount.cs), which recovers
    // from a dropped connection "for free" because it keeps re-firing every 60s regardless of
    // whether the previous tick succeeded. A WebSocket has no equivalent built-in retry: once
    // ReceiveLoopAsync exits (network drop, router hiccup, etc.) nothing ever calls ConnectAsync
    // again on its own, so the chat stayed dark forever even after the internet came back. These
    // timers give both sources the same self-healing behavior the viewer count already had.
    private const int ReconnectInitialDelaySeconds = 5;
    private const int ReconnectMaxDelaySeconds = 60;

    private string _twitchChannel = "";
    private System.Threading.Timer? _twitchReconnectTimer;
    private int _twitchReconnectDelaySeconds = ReconnectInitialDelaySeconds;

    private string _kickChannelSlug = "";
    private System.Threading.Timer? _kickReconnectTimer;
    private int _kickReconnectDelaySeconds = ReconnectInitialDelaySeconds;

    // Whether this session actually wired up each source. Set by ConnectFeed based on
    // ChatSourceMode, read by DisconnectFeed/ReconnectFeedAsync so they only unwire what was
    // actually wired (Multichat can have either source active on its own).
    private bool _twitchActive;
    private bool _kickActive;

    private string _connectionStatusText = string.Empty;
    private string? _twitchStatusKey;
    private string? _twitchStatusArg;
    private string? _kickStatusKey;
    private string? _kickStatusArg;

    private void SetTwitchStatus(string localizationKey, string? arg = null)
    {
        _twitchStatusKey = localizationKey;
        _twitchStatusArg = arg;
        RebuildConnectionStatusText();
    }

    private void SetKickStatus(string localizationKey, string? arg = null)
    {
        _kickStatusKey = localizationKey;
        _kickStatusArg = arg;
        RebuildConnectionStatusText();
    }

    // Convenience for the single-source paths (today: welcome guide / "no channel configured"),
    // where only one status line ever applies. Sets it on whichever source(s) are active.
    private void SetConnectionStatus(string localizationKey, string? arg = null)
    {
        if (_kickActive && !_twitchActive)
            SetKickStatus(localizationKey, arg);
        else
            SetTwitchStatus(localizationKey, arg);
    }

    private void RebuildConnectionStatusText()
    {
        string? Format(string? key, string? arg) =>
            key is null
                ? null
                : arg is null
                    ? LocalizationService.T(key)
                    : string.Format(LocalizationService.T(key), arg);

        var twitchText = _twitchActive ? Format(_twitchStatusKey, _twitchStatusArg) : null;
        var kickText = _kickActive ? Format(_kickStatusKey, _kickStatusArg) : null;

        // Both sources active (Multichat with Twitch+Kick both enabled): show both, prefixed so
        // it's clear which status belongs to which platform instead of one overwriting the other.
        _connectionStatusText = (twitchText, kickText) switch
        {
            (not null, not null) => $"Twitch: {twitchText}  |  Kick: {kickText}",
            (not null, null) => twitchText,
            (null, not null) => kickText,
            _ => _connectionStatusText,
        };
    }

    /// <summary>
    /// Resolves which source(s) to connect and with which channel/slug, from Settings.ChatSourceMode.
    /// Same three-mode logic SettingsRenderWindow.General.cs already uses to decide which channel
    /// box(es) to show (see DrawChatSourceFields there).
    /// </summary>
    private (
        string TwitchChannel,
        string KickChannel,
        bool ConnectTwitch,
        bool ConnectKick
    ) ResolveFeedTargets()
    {
        switch (_settings.ChatSourceMode)
        {
            case "Kick":
                return ("", _settings.KickChannel, false, !string.IsNullOrWhiteSpace(_settings.KickChannel));

            case "Multichat":
                var kickChannel = _settings.MultichatUseSameChannel
                    ? _settings.Channel
                    : _settings.KickChannel;
                return (
                    _settings.Channel,
                    kickChannel,
                    _settings.MultichatTwitchEnabled && !string.IsNullOrWhiteSpace(_settings.Channel),
                    _settings.MultichatKickEnabled && !string.IsNullOrWhiteSpace(kickChannel)
                );

            default: // "Twitch"
                return (_settings.Channel, "", !string.IsNullOrWhiteSpace(_settings.Channel), false);
        }
    }

    private void ConnectFeed()
    {
        var (twitchChannel, kickChannel, connectTwitch, connectKick) = ResolveFeedTargets();

        if (!connectTwitch && !connectKick)
        {
            DebugLog.Write("ConnectFeed: no channel configured. Showing welcome guide");
            SetConnectionStatus("MainWindow_FirstTime");
            SeedWelcomeGuide();
            return;
        }

        if (connectTwitch)
            ConnectTwitch(twitchChannel);

        if (connectKick)
            ConnectKick(kickChannel);

        ConnectStreamlabsIfConfigured();
        ConnectEventSubIfEligible();
    }

    // ----------------------------- Twitch -----------------------------------

    private void ConnectTwitch(string channel)
    {
        _twitchChannel = channel;
        _twitchActive = true;
        SetTwitchStatus("MainWindow_Connecting", channel);

        _irc.MessageReceived += OnIrcMessageReceived;
        _irc.Connected += OnIrcConnected;
        _irc.Disconnected += OnIrcDisconnected;
        _irc.Error += OnIrcError;
        _irc.MessageDeleted += OnIrcMessageDeleted;
        _irc.UserPurged += OnIrcUserPurged;
        _irc.ChatCleared += OnIrcChatCleared;
        _irc.RoomStateChanged += OnIrcRoomState;

        _ = LoadBadgeMapAsync(channel);
        _ = LoadThirdPartyEmotesAsync(channel);
        _ = TryConnectIrcAsync(channel);
    }

    private void OnIrcConnected(string channel) =>
        PostToUiThread(() =>
        {
            _twitchReconnectDelaySeconds = ReconnectInitialDelaySeconds;
            SetTwitchStatus("MainWindow_ChannelConnected", channel);
            DebugLog.Write($"ConnectFeed: connected to Twitch #{channel}");
            RequestRender();
        });

    private void OnIrcDisconnected(string reason) =>
        PostToUiThread(() =>
        {
            SetTwitchStatus("MainWindow_Disconnected", reason);
            DebugLog.Write($"ConnectFeed: Twitch disconnected ({reason})");
            RequestRender();
            ScheduleTwitchReconnect();
        });

    private void OnIrcError(Exception ex) =>
        PostToUiThread(() =>
        {
            SetTwitchStatus("MainWindow_ErrorLabel", ex.Message);
            DebugLog.WriteException("ConnectFeed._irc.Error", ex);
            RequestRender();
            ScheduleTwitchReconnect();
        });

    /// <summary>
    /// Schedules a single retry attempt after a backoff delay (5s, 10s, 20s, ... capped at 60s,
    /// reset to 5s on the next successful connect). No-ops if the feed was deliberately torn down
    /// (_twitchActive false) or a retry is already pending, so overlapping Disconnected/Error
    /// events for the same drop don't stack multiple timers.
    /// </summary>
    private void ScheduleTwitchReconnect()
    {
        if (!_twitchActive || _twitchReconnectTimer is not null)
            return;

        int delaySeconds = _twitchReconnectDelaySeconds;
        _twitchReconnectDelaySeconds = Math.Min(_twitchReconnectDelaySeconds * 2, ReconnectMaxDelaySeconds);

        DebugLog.Write($"ScheduleTwitchReconnect: retrying in {delaySeconds}s");
        _twitchReconnectTimer = new System.Threading.Timer(
            _ => PostToUiThread(() =>
            {
                _twitchReconnectTimer?.Dispose();
                _twitchReconnectTimer = null;

                if (!_twitchActive)
                    return;

                SetTwitchStatus("MainWindow_Connecting", _twitchChannel);
                RequestRender();
                _ = TryConnectIrcAsync(_twitchChannel);
            }),
            null,
            TimeSpan.FromSeconds(delaySeconds),
            System.Threading.Timeout.InfiniteTimeSpan
        );
    }

    private async Task TryConnectIrcAsync(string channel)
    {
        try
        {
            DebugLog.Write($"TryConnectIrcAsync: connecting to '{channel}'...");
            await _irc.ConnectAsync(channel);
        }
        catch (Exception ex)
        {
            DebugLog.WriteException("TryConnectIrcAsync", ex);
            PostToUiThread(() =>
            {
                SetTwitchStatus("MainWindow_ErrorLabel", ex.Message);
                RequestRender();
                ScheduleTwitchReconnect();
            });
        }
    }

    // ----------------------------- Kick -----------------------------------

    private void ConnectKick(string channelSlug)
    {
        _kickChannelSlug = channelSlug;
        _kickActive = true;
        SetKickStatus("MainWindow_Connecting", channelSlug);

        _kick.MessageReceived += OnKickMessageReceived;
        _kick.Connected += OnKickConnected;
        _kick.Disconnected += OnKickDisconnected;
        _kick.Error += OnKickError;

        _ = TryConnectKickAsync(channelSlug);
    }

    private void OnKickConnected(string channelSlug) =>
        PostToUiThread(() =>
        {
            _kickReconnectDelaySeconds = ReconnectInitialDelaySeconds;
            SetKickStatus("MainWindow_ChannelConnected", channelSlug);
            DebugLog.Write($"ConnectFeed: connected to Kick '{channelSlug}'");
            RequestRender();
        });

    private void OnKickDisconnected(string reason) =>
        PostToUiThread(() =>
        {
            SetKickStatus("MainWindow_Disconnected", reason);
            DebugLog.Write($"ConnectFeed: Kick disconnected ({reason})");
            RequestRender();
            ScheduleKickReconnect();
        });

    private void OnKickError(Exception ex) =>
        PostToUiThread(() =>
        {
            SetKickStatus("MainWindow_ErrorLabel", ex.Message);
            DebugLog.WriteException("ConnectFeed._kick.Error", ex);
            RequestRender();
            ScheduleKickReconnect();
        });

    /// <summary>Kick counterpart of ScheduleTwitchReconnect — same backoff/guard behavior.</summary>
    private void ScheduleKickReconnect()
    {
        if (!_kickActive || _kickReconnectTimer is not null)
            return;

        int delaySeconds = _kickReconnectDelaySeconds;
        _kickReconnectDelaySeconds = Math.Min(_kickReconnectDelaySeconds * 2, ReconnectMaxDelaySeconds);

        DebugLog.Write($"ScheduleKickReconnect: retrying in {delaySeconds}s");
        _kickReconnectTimer = new System.Threading.Timer(
            _ => PostToUiThread(() =>
            {
                _kickReconnectTimer?.Dispose();
                _kickReconnectTimer = null;

                if (!_kickActive)
                    return;

                SetKickStatus("MainWindow_Connecting", _kickChannelSlug);
                RequestRender();
                _ = TryConnectKickAsync(_kickChannelSlug);
            }),
            null,
            TimeSpan.FromSeconds(delaySeconds),
            System.Threading.Timeout.InfiniteTimeSpan
        );
    }

    private async Task TryConnectKickAsync(string channelSlug)
    {
        try
        {
            DebugLog.Write($"TryConnectKickAsync: connecting to '{channelSlug}'...");
            await _kick.ConnectAsync(channelSlug);
        }
        catch (Exception ex)
        {
            DebugLog.WriteException("TryConnectKickAsync", ex);
            PostToUiThread(() =>
            {
                SetKickStatus("MainWindow_ErrorLabel", ex.Message);
                RequestRender();
                ScheduleKickReconnect();
            });
        }
    }

    private void OnKickMessageReceived(ChatMessage msg)
    {
        AddPlatformBadgeIfMultichat(msg, Platform.Kick);
        OnChatMessageReceived(msg);
    }

    // ---------- Shared incoming-message handling (both sources funnel through here) -----------

    // Regular chat lines don't go through the general UI-action queue one by one. They wait in a small bounded
    // queue and are consumed in time-boxed batches (DrainIncomingChat). Under load the oldest waiting lines are
    // dropped, which is what the overlay would have done anyway: only the newest MaxMessages are ever on screen.
    private const int MaxIncomingChatQueue = 400;
    private const int ChatDrainBudgetMs = 4;
    private static readonly long ChatDrainBudgetTicks = System.Diagnostics.Stopwatch.Frequency * ChatDrainBudgetMs / 1000;

    private readonly System.Collections.Concurrent.ConcurrentQueue<ChatMessage> _incomingChat = new();
    private int _incomingChatCount;
    private int _chatDrainQueued;
    private int _droppedChatMessageCount;

    private void OnIrcMessageReceived(ChatMessage msg)
    {
        AddPlatformBadgeIfMultichat(msg, Platform.Twitch);
        OnChatMessageReceived(msg);
    }

    /// <summary>
    /// Tags a message with its source platform (Twitch/Kick logo) so it's identifiable at a glance
    /// when both feeds are mixed together in one list. Only relevant while both sources are
    /// simultaneously active (Multichat with Twitch+Kick both enabled) — with a single source
    /// there's nothing to disambiguate, so we skip it there rather than add noise to every message.
    /// Inserted at index 0 so it's always the leftmost badge, in a consistent position regardless of
    /// how many role/sub badges the message also carries.
    /// </summary>
    private void AddPlatformBadgeIfMultichat(ChatMessage msg, Platform platform)
    {
        if (!_twitchActive || !_kickActive)
            return;

        msg.Badges.Insert(
            0,
            new Badge
            {
                Name = "platform",
                Version = platform == Platform.Twitch ? "twitch" : "kick",
                LocalIcon = platform == Platform.Twitch ? "platform/twitch" : "platform/kick",
            }
        );
    }

    private void OnChatMessageReceived(ChatMessage msg)
    {
        bool isEvent = msg.IsSystem && msg.EventType is not null;

        // Events, system lines and persistent lines are rare and must never be dropped: they keep the plain path.
        if (isEvent || msg.IsSystem || msg.IsPersistent)
        {
            PostToUiThread(() =>
            {
                if (isEvent)
                {
                    ProcessIncomingEvent(msg, isFromStreamlabs: false);
                    return;
                }

                ProcessIncomingChatMessage(msg);
                RequestRender();
            });
            return;
        }

        if (System.Threading.Volatile.Read(ref _incomingChatCount) >= MaxIncomingChatQueue
            && _incomingChat.TryDequeue(out _))
        {
            System.Threading.Interlocked.Decrement(ref _incomingChatCount);
            int dropped = System.Threading.Interlocked.Increment(ref _droppedChatMessageCount);
            if (dropped % 500 == 1)
                DebugLog.Write(
                    $"OnChatMessageReceived: more than {MaxIncomingChatQueue} chat lines waiting, "
                        + $"dropping the oldest ({dropped} dropped so far)"
                );
        }

        _incomingChat.Enqueue(msg);
        System.Threading.Interlocked.Increment(ref _incomingChatCount);
        QueueChatDrain();
    }

    private void QueueChatDrain()
    {
        if (System.Threading.Interlocked.CompareExchange(ref _chatDrainQueued, 1, 0) == 0)
            PostToUiThread(DrainIncomingChat);
    }

    /// <summary>Everything a regular chat line triggers once it reaches the UI thread.</summary>
    private void ProcessIncomingChatMessage(ChatMessage msg)
    {
        TryShowIrcRedemptionFallback(msg);
        LogMessageForModeration(msg);
        AugmentWithThirdPartyEmotes(msg);
        AddMessage(msg);
        if (!msg.IsSystem)
            TriggerAlert("message");
    }

    /// <summary>
    /// UI thread. Consumes waiting chat lines for at most ChatDrainBudgetMs. When more lines are waiting than can still
    /// be on screen, the older ones are skipped (their redemption and moderation bookkeeping still runs, but they are
    /// not laid out or added to the list only to be trimmed right away).
    /// </summary>
    private void DrainIncomingChat()
    {
        long deadline = System.Diagnostics.Stopwatch.GetTimestamp() + ChatDrainBudgetTicks;
        int visibleCapacity = Math.Max(1, _settings.MaxMessages);

        int excess = System.Threading.Volatile.Read(ref _incomingChatCount) - visibleCapacity;
        while (excess > 0 && _incomingChat.TryDequeue(out var skipped))
        {
            System.Threading.Interlocked.Decrement(ref _incomingChatCount);
            TryShowIrcRedemptionFallback(skipped);
            LogMessageForModeration(skipped);
            excess--;
        }

        bool any = false;
        while (_incomingChat.TryDequeue(out var msg))
        {
            System.Threading.Interlocked.Decrement(ref _incomingChatCount);
            ProcessIncomingChatMessage(msg);
            any = true;

            if (System.Diagnostics.Stopwatch.GetTimestamp() >= deadline)
                break;
        }

        if (any)
            RequestRender();

        System.Threading.Interlocked.Exchange(ref _chatDrainQueued, 0);
        if (!_incomingChat.IsEmpty)
            QueueChatDrain();
    }

    private void DisconnectFeed()
    {
        // Lines still waiting from the previous connection must not show up under a new channel.
        while (_incomingChat.TryDequeue(out _))
            System.Threading.Interlocked.Decrement(ref _incomingChatCount);

        if (_twitchActive)
        {
            _irc.MessageReceived -= OnIrcMessageReceived;
            _irc.Connected -= OnIrcConnected;
            _irc.Disconnected -= OnIrcDisconnected;
            _irc.Error -= OnIrcError;
            _irc.MessageDeleted -= OnIrcMessageDeleted;
            _irc.UserPurged -= OnIrcUserPurged;
            _irc.ChatCleared -= OnIrcChatCleared;
            _irc.RoomStateChanged -= OnIrcRoomState;
            _ = _irc.DisposeAsync().AsTask();
            _twitchActive = false;
        }
        _twitchReconnectTimer?.Dispose();
        _twitchReconnectTimer = null;
        _twitchReconnectDelaySeconds = ReconnectInitialDelaySeconds;

        if (_kickActive)
        {
            _kick.MessageReceived -= OnKickMessageReceived;
            _kick.Connected -= OnKickConnected;
            _kick.Disconnected -= OnKickDisconnected;
            _kick.Error -= OnKickError;
            _ = _kick.DisposeAsync().AsTask();
            _kickActive = false;
        }
        _kickReconnectTimer?.Dispose();
        _kickReconnectTimer = null;
        _kickReconnectDelaySeconds = ReconnectInitialDelaySeconds;

        DisconnectStreamlabs();
        DisconnectEventSub();
    }

    private void ResetForChannelChange()
    {
        foreach (var msg in _messages)
            RemoveMessageCaches(msg);
        _messages.Clear();
        ClearModerationLog();

        _badgeUrls = null;
        _thirdPartyEmotes = null;

        _imageCache.RemoveWhere(k => k.StartsWith("badge:", StringComparison.Ordinal));
        _imageLoadInFlight.RemoveWhere(k => k.StartsWith("badge:"));

        foreach (var list in _pendingIrcEventsByFamily.Values)
            foreach (var pending in list)
                pending.Timer.Dispose();
        _pendingIrcEventsByFamily.Clear();

        RequestRender();
    }

    private async Task ReconnectFeedAsync()
    {
        if (_twitchActive)
        {
            _irc.MessageReceived -= OnIrcMessageReceived;
            _irc.Connected -= OnIrcConnected;
            _irc.Disconnected -= OnIrcDisconnected;
            _irc.Error -= OnIrcError;
            _irc.MessageDeleted -= OnIrcMessageDeleted;
            _irc.UserPurged -= OnIrcUserPurged;
            _irc.ChatCleared -= OnIrcChatCleared;
            _irc.RoomStateChanged -= OnIrcRoomState;
            await _irc.DisposeAsync();
            _twitchActive = false;
        }
        _twitchReconnectTimer?.Dispose();
        _twitchReconnectTimer = null;
        _twitchReconnectDelaySeconds = ReconnectInitialDelaySeconds;

        if (_kickActive)
        {
            _kick.MessageReceived -= OnKickMessageReceived;
            _kick.Connected -= OnKickConnected;
            _kick.Disconnected -= OnKickDisconnected;
            _kick.Error -= OnKickError;
            await _kick.DisposeAsync();
            _kickActive = false;
        }
        _kickReconnectTimer?.Dispose();
        _kickReconnectTimer = null;
        _kickReconnectDelaySeconds = ReconnectInitialDelaySeconds;

        DisconnectStreamlabs();
        DisconnectEventSub();
        ConnectFeed();
    }
}