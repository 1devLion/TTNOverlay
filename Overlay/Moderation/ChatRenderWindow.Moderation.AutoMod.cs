using TTNOverlay.Models;
using TTNOverlay.Services;
using TTNOverlay.Twitch;
using Rect = Vortice.Mathematics.Rect;

namespace TTNOverlay.Overlay;

/// <summary>
/// ChatRenderWindow partial: the moderation panel's "AutoMod" tab. Messages that AutoMod holds for review never
/// reach IRC, so they are learned from EventSub (automod.message.hold) and kept in a small in-memory queue until a
/// moderator allows or denies them, or someone else does (automod.message.update). Allowing/denying goes through
/// the Helix "Manage Held AutoMod Messages" endpoint. Runs for any moderator of the channel, not only the owner.
/// </summary>
internal sealed partial class ChatRenderWindow
{
    private readonly AutoModQueue _autoModQueue = new();
    private readonly List<(Rect Bounds, AutoModHeldMessage Message, bool Allow)> _moderationAutoModActionRects = new();

    private ITwitchAutoModClient? _autoModClient;
    private CancellationTokenSource? _autoModStartCts;
    private bool _autoModSubscribed;
    private bool _autoModNeedsRelogin;

    private bool ModerationAutoModVisible => _showingModeration && _moderationTab == ModerationTab.AutoMod;

    // ------------------------------------------------------------------ connection

    /// <summary>
    /// (Re)starts the AutoMod EventSub connection if this session can review held messages: the Twitch API and the
    /// moderation panel are enabled, a moderator is logged in, and a Twitch channel is being read. Whether the account
    /// really moderates that channel is up to Twitch: it refuses the subscription otherwise and the tab says so.
    /// </summary>
    private void ConnectAutoModIfEligible()
    {
        DisconnectAutoMod();

        if (
            !_settings.EnableTwitchApi
            || !_settings.EnableModerationPanel
            || string.IsNullOrWhiteSpace(_settings.ModeratorRefreshToken)
            || string.IsNullOrWhiteSpace(_settings.ModeratorUserId)
        )
            return;

        var (twitchChannel, _, connectTwitch, _) = ResolveFeedTargets();
        var channel = twitchChannel.Trim().TrimStart('#');
        if (!connectTwitch || channel.Length == 0)
            return;

        var cts = new CancellationTokenSource();
        _autoModStartCts = cts;
        _ = StartAutoModAsync(channel, _settings.ModeratorLogin, _settings.ModeratorUserId, cts);
    }

    private async Task StartAutoModAsync(
        string channel,
        string moderatorLogin,
        string moderatorUserId,
        CancellationTokenSource cts
    )
    {
        try
        {
            // Own service instance (same settings object, so refreshed tokens land in the same place), like the
            // redemptions connection: the panel's instance is created lazily and discarded when Settings closes.
            var tokens = new ModerationService(_settings);

            // The subscription is about the channel's owner, who is the moderator themselves on their own channel.
            string? broadcasterId = string.Equals(moderatorLogin, channel, StringComparison.OrdinalIgnoreCase)
                ? moderatorUserId
                : await tokens.GetChannelIdAsync(channel);

            if (cts.IsCancellationRequested)
                return;
            if (string.IsNullOrEmpty(broadcasterId))
            {
                DebugLog.Write($"AutoMod: could not resolve the id of channel '{channel}', not connecting");
                return;
            }

            PostToUiThread(() =>
            {
                if (cts.IsCancellationRequested || !ReferenceEquals(_autoModStartCts, cts))
                    return;

                var client = new TwitchAutoModClient();
                client.MessageHeld += OnAutoModMessageHeld;
                client.MessageResolved += OnAutoModMessageResolved;
                client.StatusChanged += OnAutoModStatusChanged;
                _autoModClient = client;
                client.Start(broadcasterId, moderatorUserId, tokens.GetAccessTokenAsync);
            });
        }
        catch (Exception ex)
        {
            DebugLog.WriteException("ChatRenderWindow.StartAutoModAsync", ex);
        }
    }

    /// <summary>Stops the connection. The queue is kept: reconnecting for the same channel (Settings closing) must not lose what is still held.</summary>
    private void DisconnectAutoMod()
    {
        try
        {
            _autoModStartCts?.Cancel();
        }
        catch { }
        _autoModStartCts = null;

        var client = _autoModClient;
        _autoModClient = null;
        _autoModSubscribed = false;
        _autoModNeedsRelogin = false;
        if (client is null)
            return;

        client.MessageHeld -= OnAutoModMessageHeld;
        client.MessageResolved -= OnAutoModMessageResolved;
        client.StatusChanged -= OnAutoModStatusChanged;
        _ = client.DisposeAsync().AsTask();
    }

    // ------------------------------------------------------------------ events

    private void OnAutoModMessageHeld(AutoModHeldMessage message) =>
        PostToUiThread(() =>
        {
            bool wasEmpty = _autoModQueue.Count == 0;
            if (!_autoModQueue.Add(message))
                return;

            // One heads-up in the chat when the queue goes from empty to non-empty, so a held message doesn't sit
            // unnoticed while the panel is closed. Not for every message: a raid could flood the chat.
            if (wasEmpty && !ModerationAutoModVisible)
            {
                AddMessage(
                    new ChatMessage
                    {
                        IsSystem = true,
                        Color = ChatColors.SystemGray,
                        Text = string.Format(LocalizationService.T("EventMsg_AutoModHeld"), DisplayNameOf(message)),
                    }
                );
            }

            if (_showingModeration || wasEmpty)
                RequestRender();
        });

    private void OnAutoModMessageResolved(string messageId, AutoModResolution resolution) =>
        PostToUiThread(() =>
        {
            if (_autoModQueue.Remove(messageId) is not null && _showingModeration)
                RequestRender();
        });

    private void OnAutoModStatusChanged(EventSubStatus status) =>
        PostToUiThread(() =>
        {
            _autoModSubscribed = status == EventSubStatus.Subscribed;
            _autoModNeedsRelogin = status == EventSubStatus.NeedsRelogin;
            DebugLog.Write($"AutoMod EventSub status: {status}");
            if (_showingModeration)
                RequestRender();
        });

    // ------------------------------------------------------------------ clicks and actions

    private void HandleAutoModTabClick(int clientX, int clientY)
    {
        foreach (var (bounds, message, allow) in _moderationAutoModActionRects)
        {
            if (Contains(bounds, clientX, clientY))
            {
                _ = ResolveHeldMessageAsync(message, allow);
                return;
            }
        }
    }

    private async Task ResolveHeldMessageAsync(AutoModHeldMessage message, bool allow)
    {
        var moderation = _moderation;
        if (moderation is null || message.IsBusy)
            return;

        message.IsBusy = true;
        var name = DisplayNameOf(message);
        _moderationStatusText = string.Format(LocalizationService.T("Moderation_Working"), name);
        RequestRender();

        var result = AutoModDecisionResult.Failed;
        try
        {
            result = await moderation.ResolveHeldMessageAsync(message.MessageId, allow);
        }
        catch (Exception ex)
        {
            DebugLog.WriteException("ChatRenderWindow.ResolveHeldMessageAsync", ex);
        }

        PostToUiThread(() =>
        {
            message.IsBusy = false;

            switch (result)
            {
                case AutoModDecisionResult.Ok:
                    _autoModQueue.Remove(message.MessageId);
                    _moderationStatusText = string.Format(
                        LocalizationService.T(allow ? "Moderation_AutoModAllowed" : "Moderation_AutoModDenied"),
                        name
                    );
                    break;

                case AutoModDecisionResult.AlreadyResolved:
                    _autoModQueue.Remove(message.MessageId);
                    _moderationStatusText = string.Format(
                        LocalizationService.T("Moderation_AutoModAlreadyResolved"),
                        name
                    );
                    break;

                case AutoModDecisionResult.MissingPermission:
                    _moderationStatusText = LocalizationService.T("Moderation_AutoModNeedsRelogin");
                    break;

                default:
                    _moderationStatusText = string.Format(LocalizationService.T("Moderation_AutoModFailed"), name);
                    break;
            }

            RequestRender();
        });
    }

    private static string DisplayNameOf(AutoModHeldMessage message) =>
        string.IsNullOrEmpty(message.DisplayName) ? message.Login : message.DisplayName;
}
