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
    }

    // ----------------------------- Twitch -----------------------------------

    private void ConnectTwitch(string channel)
    {
        _twitchActive = true;
        SetTwitchStatus("MainWindow_Connecting", channel);

        _irc.MessageReceived += OnIrcMessageReceived;
        _irc.Connected += OnIrcConnected;
        _irc.Disconnected += OnIrcDisconnected;
        _irc.Error += OnIrcError;

        _ = LoadBadgeMapAsync(channel);
        _ = LoadThirdPartyEmotesAsync(channel);
        _ = TryConnectIrcAsync(channel);
    }

    private void OnIrcConnected(string channel) =>
        PostToUiThread(() =>
        {
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
        });

    private void OnIrcError(Exception ex) =>
        PostToUiThread(() =>
        {
            SetTwitchStatus("MainWindow_ErrorLabel", ex.Message);
            DebugLog.WriteException("ConnectFeed._irc.Error", ex);
            RequestRender();
        });

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
            });
        }
    }

    // ----------------------------- Kick -----------------------------------

    private void ConnectKick(string channelSlug)
    {
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
        });

    private void OnKickError(Exception ex) =>
        PostToUiThread(() =>
        {
            SetKickStatus("MainWindow_ErrorLabel", ex.Message);
            DebugLog.WriteException("ConnectFeed._kick.Error", ex);
            RequestRender();
        });

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
            });
        }
    }

    private void OnKickMessageReceived(ChatMessage msg) => OnChatMessageReceived(msg);

    // ---------- Shared incoming-message handling (both sources funnel through here) -----------

    private const int MaxPendingChatBacklog = 2000;

    private int _droppedChatMessageCount;

    private void OnIrcMessageReceived(ChatMessage msg) => OnChatMessageReceived(msg);

    private void OnChatMessageReceived(ChatMessage msg)
    {
        bool isEvent = msg.IsSystem && msg.EventType is not null;

        if (!isEvent && PendingUiActionCount > MaxPendingChatBacklog)
        {
            _droppedChatMessageCount++;
            if (_droppedChatMessageCount % 500 == 1)
                DebugLog.Write(
                    $"OnChatMessageReceived: UI backlog > {MaxPendingChatBacklog}, "
                        + $"discarding chat messages ({_droppedChatMessageCount} discarded so far)"
                );
            return;
        }

        PostToUiThread(() =>
        {
            if (isEvent)
            {
                ProcessIncomingEvent(msg, isFromStreamlabs: false);
                return;
            }

            AugmentWithThirdPartyEmotes(msg);
            AddMessage(msg);
            if (!msg.IsSystem)
                TriggerAlert("message");
            RequestRender();
        });
    }

    private void DisconnectFeed()
    {
        if (_twitchActive)
        {
            _irc.MessageReceived -= OnIrcMessageReceived;
            _irc.Connected -= OnIrcConnected;
            _irc.Disconnected -= OnIrcDisconnected;
            _irc.Error -= OnIrcError;
            _ = _irc.DisposeAsync().AsTask();
            _twitchActive = false;
        }

        if (_kickActive)
        {
            _kick.MessageReceived -= OnKickMessageReceived;
            _kick.Connected -= OnKickConnected;
            _kick.Disconnected -= OnKickDisconnected;
            _kick.Error -= OnKickError;
            _ = _kick.DisposeAsync().AsTask();
            _kickActive = false;
        }

        DisconnectStreamlabs();
    }

    private void ResetForChannelChange()
    {
        foreach (var msg in _messages)
            RemoveMessageCaches(msg);
        _messages.Clear();

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
            await _irc.DisposeAsync();
            _twitchActive = false;
        }

        if (_kickActive)
        {
            _kick.MessageReceived -= OnKickMessageReceived;
            _kick.Connected -= OnKickConnected;
            _kick.Disconnected -= OnKickDisconnected;
            _kick.Error -= OnKickError;
            await _kick.DisposeAsync();
            _kickActive = false;
        }

        DisconnectStreamlabs();
        ConnectFeed();
    }
}