using TTNOverlay.Models;
using TTNOverlay.Services;
using TTNOverlay.Twitch;

namespace TTNOverlay.Overlay;

/// <summary>
/// ChatRenderWindow partial: owns the Twitch IRC connection lifecycle and forwards incoming chat messages.
/// </summary>
internal sealed partial class ChatRenderWindow
{
    private readonly ITwitchIrcClient _irc = new TwitchIrcClient();

    private string _connectionStatusText = string.Empty;

    private void ConnectFeed()
    {
        if (string.IsNullOrWhiteSpace(_settings.Channel))
        {
            DebugLog.Write("ConnectFeed: no channel configured -- showing welcome guide");
            _connectionStatusText = LocalizationService.T("MainWindow_FirstTime");
            SeedWelcomeGuide();
            return;
        }

        _connectionStatusText = string.Format(
            LocalizationService.T("MainWindow_Connecting"),
            _settings.Channel
        );

        _irc.MessageReceived += OnIrcMessageReceived;
        _irc.Connected += OnIrcConnected;
        _irc.Disconnected += OnIrcDisconnected;
        _irc.Error += OnIrcError;

        _ = LoadBadgeMapAsync(_settings.Channel);
        _ = LoadThirdPartyEmotesAsync(_settings.Channel);
        _ = TryConnectIrcAsync();

        ConnectStreamlabsIfConfigured();
    }

    private void OnIrcConnected(string channel) =>
        PostToUiThread(() =>
        {
            _connectionStatusText = string.Format(
                LocalizationService.T("MainWindow_ChannelConnected"),
                channel
            );
            DebugLog.Write($"ConnectFeed: conectado a #{channel}");
            RequestRender();
        });

    private void OnIrcDisconnected(string reason) =>
        PostToUiThread(() =>
        {
            _connectionStatusText = string.Format(
                LocalizationService.T("MainWindow_Disconnected"),
                reason
            );
            DebugLog.Write($"ConnectFeed: desconectado ({reason})");
            RequestRender();
        });

    private void OnIrcError(Exception ex) =>
        PostToUiThread(() =>
        {
            _connectionStatusText = string.Format(
                LocalizationService.T("MainWindow_ErrorLabel"),
                ex.Message
            );
            DebugLog.WriteException("ConnectFeed._irc.Error", ex);
            RequestRender();
        });

    private async Task TryConnectIrcAsync()
    {
        try
        {
            DebugLog.Write($"TryConnectIrcAsync: conectando a '{_settings.Channel}'...");
            await _irc.ConnectAsync(_settings.Channel);
        }
        catch (Exception ex)
        {

            DebugLog.WriteException("TryConnectIrcAsync", ex);
            PostToUiThread(() =>
            {
                _connectionStatusText = string.Format(
                    LocalizationService.T("MainWindow_ErrorLabel"),
                    ex.Message
                );
                RequestRender();
            });
        }
    }

    private const int MaxPendingChatBacklog = 2000;

    private int _droppedChatMessageCount;

    private void OnIrcMessageReceived(ChatMessage msg)
    {
        bool isEvent = msg.IsSystem && msg.EventType is not null;

        if (!isEvent && PendingUiActionCount > MaxPendingChatBacklog)
        {
            _droppedChatMessageCount++;
            if (_droppedChatMessageCount % 500 == 1)
                DebugLog.Write(
                    $"OnIrcMessageReceived: backlog de UI > {MaxPendingChatBacklog}, "
                        + $"descartando mensajes de chat ({_droppedChatMessageCount} descartados hasta ahora)"
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
        _irc.MessageReceived -= OnIrcMessageReceived;
        _irc.Connected -= OnIrcConnected;
        _irc.Disconnected -= OnIrcDisconnected;
        _irc.Error -= OnIrcError;
        _ = _irc.DisposeAsync().AsTask();
        DisconnectStreamlabs();
    }

    private void ResetForChannelChange()
    {
        _messages.Clear();

        _badgeUrls = null;
        _thirdPartyEmotes = null;

        var staleBadgeKeys = new List<string>();
        foreach (var key in _imageCache.Keys)
            if (key.StartsWith("badge:"))
                staleBadgeKeys.Add(key);
        foreach (var key in staleBadgeKeys)
        {
            if (_imageCache[key] is { } bmp)
                _imageCacheBytes -= (long)(bmp.Size.Width * bmp.Size.Height * 4);
            _imageCache[key]?.Dispose();
            _imageCache.Remove(key);
            _imageCacheOrder.Remove(key);
        }
        _imageLoadInFlight.RemoveWhere(k => k.StartsWith("badge:"));

        foreach (var list in _pendingIrcEventsByFamily.Values)
            foreach (var pending in list)
                pending.Timer.Dispose();
        _pendingIrcEventsByFamily.Clear();

        RequestRender();
    }

    private async Task ReconnectFeedAsync()
    {
        _irc.MessageReceived -= OnIrcMessageReceived;
        _irc.Connected -= OnIrcConnected;
        _irc.Disconnected -= OnIrcDisconnected;
        _irc.Error -= OnIrcError;
        DisconnectStreamlabs();
        await _irc.DisposeAsync();
        ConnectFeed();
    }
}