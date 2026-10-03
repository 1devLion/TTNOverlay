using TTNOverlay.Models;
using TTNOverlay.Services;
using TTNOverlay.Twitch;

namespace TTNOverlay.Overlay;

/// <summary>
/// ChatRenderWindow partial: Twitch channel points redemptions. Full detail (reward title, cost, viewer text) comes
/// from EventSub, which needs the broadcaster's own login (channel:read:redemptions). Without it, only redemptions
/// that carry a chat message are visible, through the IRC tags, and without title or cost (see
/// TryShowIrcRedemptionFallback). A redemption is shown in two places: an event banner inline in the chat list, and
/// an entry in the events panel (with the usual alert sound/flash).
/// </summary>
internal sealed partial class ChatRenderWindow
{
    private ITwitchEventSubClient? _eventSub;
    private bool _eventSubSubscribed;
    private bool _redemptionsReloginNoticeShown;

    /// <summary>
    /// (Re)starts the EventSub connection if this session can receive redemptions: the Twitch API is enabled, someone
    /// is logged in, and that account is the owner of the Twitch channel being read. Twitch only allows redemption
    /// subscriptions with the broadcaster's own token, so a moderator login never qualifies.
    /// </summary>
    private void ConnectEventSubIfEligible()
    {
        DisconnectEventSub();

        if (!_settings.EnableTwitchApi || string.IsNullOrWhiteSpace(_settings.ModeratorRefreshToken))
            return;

        var (twitchChannel, _, connectTwitch, _) = ResolveFeedTargets();
        if (!connectTwitch)
            return;

        var channel = twitchChannel.Trim().TrimStart('#');
        var userId = _settings.ModeratorUserId;
        if (
            string.IsNullOrWhiteSpace(userId)
            || !string.Equals(_settings.ModeratorLogin, channel, StringComparison.OrdinalIgnoreCase)
        )
        {
            DebugLog.Write(
                "ConnectEventSubIfEligible: the logged-in account is not the channel's broadcaster, "
                    + "channel points redemptions need the broadcaster's own login"
            );
            return;
        }

        // Its own service instance (same settings object, so refreshed tokens land in the same place): the
        // moderation panel's instance is created lazily and discarded whenever Settings closes.
        var tokens = new ModerationService(_settings);

        var client = new TwitchEventSubClient();
        client.RedemptionReceived += OnRedemptionReceived;
        client.StatusChanged += OnEventSubStatusChanged;
        _eventSub = client;
        client.Start(userId, tokens.GetAccessTokenAsync);
    }

    private void DisconnectEventSub()
    {
        var client = _eventSub;
        if (client is null)
            return;

        _eventSub = null;
        client.RedemptionReceived -= OnRedemptionReceived;
        client.StatusChanged -= OnEventSubStatusChanged;
        _ = client.DisposeAsync().AsTask();
        _eventSubSubscribed = false;
    }

    private void OnRedemptionReceived(ChannelPointsRedemption redemption) =>
        PostToUiThread(() => ShowRedemption(redemption));

    private void OnEventSubStatusChanged(EventSubStatus status) =>
        PostToUiThread(() =>
        {
            _eventSubSubscribed = status == EventSubStatus.Subscribed;
            DebugLog.Write($"EventSub status: {status}");

            if (status == EventSubStatus.Subscribed)
            {
                _redemptionsReloginNoticeShown = false;
            }
            else if (status == EventSubStatus.NeedsRelogin && !_redemptionsReloginNoticeShown)
            {
                _redemptionsReloginNoticeShown = true;
                AddMessage(
                    new ChatMessage
                    {
                        IsSystem = true,
                        Color = ChatColors.SystemGray,
                        Text = LocalizationService.T("EventMsg_RedemptionNeedsLogin"),
                    }
                );
                RequestRender();
            }
        });

    /// <summary>
    /// Shows one redemption. Must run on the UI thread. The chat list gets an event banner without the viewer's text
    /// (their message already shows up as a normal chat message, with emotes); the events panel gets the full entry.
    /// </summary>
    private void ShowRedemption(ChannelPointsRedemption redemption)
    {
        var lang = LocalizationService.Instance.CurrentLanguage;

        AddMessage(
            RedemptionMessageBuilder.ToMessage(redemption, lang, includeUserInput: false, isSystem: false)
        );
        RequestRender();

        ShowEventBanner(
            RedemptionMessageBuilder.ToMessage(redemption, lang, includeUserInput: true, isSystem: true)
        );
    }

    /// <summary>
    /// Without EventSub, the only trace of a redemption is a chat message tagged by Twitch as one. Show the banner
    /// for those (generic wording: IRC doesn't tell us the reward title or cost). While EventSub is subscribed it
    /// reports the same redemption in full, so the IRC tags are ignored to avoid showing it twice.
    /// </summary>
    private void TryShowIrcRedemptionFallback(ChatMessage msg)
    {
        if (_eventSubSubscribed || (msg.RewardId is null && msg.AutoRewardType is null))
            return;

        ShowRedemption(
            new ChannelPointsRedemption
            {
                RewardId = msg.RewardId,
                UserLogin = msg.Username,
                DisplayName = msg.DisplayName,
                AutoRewardType = msg.AutoRewardType,
                UserInput = msg.Text,
            }
        );
    }
}
