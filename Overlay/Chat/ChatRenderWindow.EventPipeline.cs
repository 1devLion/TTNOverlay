using TTNOverlay.Models;
using TTNOverlay.Services;
using TTNOverlay.Streamlabs;
using Vortice.Mathematics;

namespace TTNOverlay.Overlay;

/// <summary>
/// ChatRenderWindow partial: merges and deduplicates events arriving from the Twitch IRC and Streamlabs feeds.
/// </summary>
internal sealed partial class ChatRenderWindow
{
    private static readonly TimeSpan StreamlabsDedupWindow = TimeSpan.FromSeconds(4);

    private readonly Dictionary<string, List<PendingIrcEvent>> _pendingIrcEventsByFamily = new();
    private readonly Dictionary<string, DateTime> _recentStreamlabsEventByFamily = new();

    private IStreamlabsSocketClient? _streamlabs;

    private sealed class PendingIrcEvent
    {
        public required ChatMessage Message { get; init; }
        public System.Threading.Timer Timer { get; set; } = null!;
    }

    private void ConnectStreamlabsIfConfigured()
    {

        if (_settings.EnableStreamlabsEvents && !string.IsNullOrWhiteSpace(_settings.StreamlabsWidgetToken))
            _ = SeedStreamlabsWidgetConfigAsync();
        else
            SubEventVariationResolver.Clear();

        if (
            !_settings.EnableStreamlabsEvents
            || string.IsNullOrWhiteSpace(_settings.StreamlabsSocketToken)
            || _settings.EventAlertSource == "IrcOnly"
        )
        {
            DebugLog.Write("ConnectStreamlabsIfConfigured: no configurado o EventAlertSource=IrcOnly. No conecta socket");
            return;
        }

        var client = new StreamlabsSocketClient();
        client.MessageReceived += OnStreamlabsMessageReceived;
        client.Connected += () => DebugLog.Write("ConnectStreamlabsIfConfigured: conectado");
        client.Disconnected += reason =>
            DebugLog.Write($"ConnectStreamlabsIfConfigured: desconectado ({reason})");
        client.Error += ex =>
            DebugLog.WriteException("ConnectStreamlabsIfConfigured._streamlabs.Error", ex);

        _streamlabs = client;
        _ = TryConnectStreamlabsAsync(client);
    }

    private void ReconnectStreamlabs()
    {
        if (string.IsNullOrWhiteSpace(_settings.Channel))
            return;

        DisconnectStreamlabs();
        ConnectStreamlabsIfConfigured();
    }

    private async Task SeedStreamlabsWidgetConfigAsync()
    {
        if (string.IsNullOrWhiteSpace(_settings.StreamlabsWidgetToken))
            return;

        try
        {
            await SubEventVariationResolver.FetchAndSeedWidgetConfigAsync(_settings.StreamlabsWidgetToken);
        }
        catch (Exception ex)
        {
            DebugLog.WriteException("SeedStreamlabsWidgetConfigAsync", ex);

        }
    }

    private async Task TryConnectStreamlabsAsync(IStreamlabsSocketClient client)
    {
        try
        {
            await SubEventVariationResolver.FetchAndSeedWidgetConfigAsync(_settings.StreamlabsWidgetToken);
            await client.ConnectAsync(_settings.StreamlabsSocketToken);
        }
        catch (Exception ex)
        {
            DebugLog.WriteException("TryConnectStreamlabsAsync", ex);
        }
    }

    private void OnStreamlabsMessageReceived(ChatMessage msg)
    {
        PostToUiThread(() =>
        {
            if (msg.IsSystem && msg.EventType is not null)
                ProcessIncomingEvent(msg, isFromStreamlabs: true);
            else
                DebugLog.Write("OnStreamlabsMessageReceived: message without EventType, ignored (unexpected)");
        });
    }

    private void DisconnectStreamlabs()
    {
        var client = _streamlabs;
        if (client is null)
            return;

        _streamlabs = null;
        client.MessageReceived -= OnStreamlabsMessageReceived;
        _ = client.DisposeAsync().AsTask();
    }

    private void ProcessIncomingEvent(ChatMessage msg, bool isFromStreamlabs)
    {
        var eventType = msg.EventType!;
        var family = EventFamily(msg.EventKind);

        if (_settings.EventAlertSource == "IrcOnly")
        {
            if (!isFromStreamlabs)
                ShowEventBanner(msg);
            return;
        }

        if (_settings.EventAlertSource == "StreamlabsOnly")
        {
            if (isFromStreamlabs)
                ShowEventBanner(msg);
            return;
        }

        if (isFromStreamlabs)
        {
            ShowEventBanner(msg);

            if (family is not null)
            {
                _recentStreamlabsEventByFamily[family] = DateTime.UtcNow;

                if (
                    _pendingIrcEventsByFamily.TryGetValue(family, out var pending)
                    && pending.Count > 0
                )
                {
                    var cancelled = pending[0];
                    pending.RemoveAt(0);
                    cancelled.Timer.Dispose();
                }
            }
            return;
        }

        if (family is null || !_settings.EnableStreamlabsEvents)
        {
            ShowEventBanner(msg);
            return;
        }

        if (
            _recentStreamlabsEventByFamily.TryGetValue(family, out var lastSl)
            && DateTime.UtcNow - lastSl < StreamlabsDedupWindow
        )
        {
            DebugLog.Write(
                $"ProcessIncomingEvent: IRC event '{eventType}' discarded, the Streamlabs equivalent already arrived"
            );
            return;
        }

        var pendingEvent = new PendingIrcEvent { Message = msg };
        pendingEvent.Timer = new System.Threading.Timer(
            _ => PostToUiThread(() => ResolvePendingIrcEvent(family, pendingEvent)),
            null,
            StreamlabsDedupWindow,
            System.Threading.Timeout.InfiniteTimeSpan
        );

        if (!_pendingIrcEventsByFamily.TryGetValue(family, out var queue))
        {
            queue = new List<PendingIrcEvent>();
            _pendingIrcEventsByFamily[family] = queue;
        }
        queue.Add(pendingEvent);
    }

    private void ResolvePendingIrcEvent(string family, PendingIrcEvent pendingEvent)
    {
        pendingEvent.Timer.Dispose();
        if (_pendingIrcEventsByFamily.TryGetValue(family, out var list))
            list.Remove(pendingEvent);
        ShowEventBanner(pendingEvent.Message);
    }

    private void ShowEventBanner(ChatMessage msg)
    {
        if (!_settings.EnableEventsPanel)
            return;

        AddDashboardEvent(msg);
        ShowEventsTemporarily();
        TriggerAlert("event");
    }

    // Groups by canonical EventType instead of enumerating every Twitch/Streamlabs raw-id pair by hand.
    // This is what makes the IRC/Streamlabs dedup logic above automatically cross-platform: whenever a
    // future platform (Kick, YouTube, ...) is classified into EventType.Sub/Resub/.../Raid, its events
    // dedup against Twitch's and Streamlabs' the same way, with no change needed here.
    private static string? EventFamily(EventType eventKind) =>
        eventKind switch
        {
            EventType.Sub
            or EventType.Resub
            or EventType.SubGift
            or EventType.AnonSubGift
            or EventType.MysteryGiftSub
            or EventType.AnonMysteryGiftSub
            or EventType.PrimeUpgrade
            or EventType.GiftUpgrade
            or EventType.AnonGiftUpgrade => "sub",
            EventType.Raid => "raid",
            _ => null,
        };

    private void TriggerAlert(string key)
    {
        if (!AlertService.ShouldTrigger(key))
            return;

        AlertService.PlaySound(key);
        if (_settings.EnableVisualFlash)
            FlashVisualAlert();
    }

    private DateTime? _flashStartUtc;
    private System.Threading.Timer? _flashTimer;
    private static readonly TimeSpan FlashDuration = TimeSpan.FromMilliseconds(400);

    private void FlashVisualAlert()
    {
        _flashStartUtc = DateTime.UtcNow;
        _flashTimer ??= new System.Threading.Timer(_ => PostToUiThread(TickFlash), null, 0, 16);
    }

    private Color4? _testFlashColor;

    private void TestFlashAlert(string hexColor, byte alpha)
    {
        _testFlashColor = ParseFlashColor(hexColor, alpha);
        _flashStartUtc = DateTime.UtcNow;
        _flashTimer ??= new System.Threading.Timer(_ => PostToUiThread(TickFlash), null, 0, 16);
    }

    private void TickFlash()
    {
        if (_flashStartUtc is null)
            return;

        if (DateTime.UtcNow - _flashStartUtc.Value >= FlashDuration)
        {
            _flashStartUtc = null;
            _testFlashColor = null;
            _flashTimer?.Dispose();
            _flashTimer = null;
        }

        RequestRender();
    }

    private float CurrentFlashOpacity()
    {
        if (_flashStartUtc is not { } start)
            return 0f;

        float t = (float)((DateTime.UtcNow - start).TotalMilliseconds / FlashDuration.TotalMilliseconds);
        if (t >= 1f)
            return 0f;

        float eased = 1f - t;
        return eased * eased;
    }

    private static Color4 ParseFlashColor(string hex, byte alpha)
    {
        try
        {
            if (hex.StartsWith('#'))
                hex = hex[1..];

            int offset = hex.Length == 8 ? 2 : 0;
            byte r = Convert.ToByte(hex[offset..(offset + 2)], 16);
            byte g = Convert.ToByte(hex[(offset + 2)..(offset + 4)], 16);
            byte b = Convert.ToByte(hex[(offset + 4)..(offset + 6)], 16);
            return new Color4(r / 255f, g / 255f, b / 255f, alpha / 255f);
        }
        catch
        {
            return new Color4(0xFF / 255f, 0xD7 / 255f, 0x00 / 255f, alpha / 255f);
        }
    }

    private void DisconnectFlash()
    {
        _flashTimer?.Dispose();
        _flashTimer = null;
    }

    private void SetupAlerts()
    {
        AlertService.SetOutputDevice(_settings.AlertOutputDeviceId);
        AlertService.SetVolume("message", _settings.MessageAlertVolume);
        AlertService.SetVolume("event", _settings.EventAlertVolume);
        AlertService.SetCooldownEnabled(!_settings.DisableAlertCooldown);
        AlertService.PrepareAlert("message", _settings.EnableMessageAlert ? _settings.MessageSoundPath : null);
        AlertService.PrepareAlert("event", _settings.EnableEventAlert ? _settings.EventSoundPath : null);
    }
}