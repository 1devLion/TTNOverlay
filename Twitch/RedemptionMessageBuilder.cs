using TTNOverlay.Models;
using TTNOverlay.Services;

namespace TTNOverlay.Twitch;

/// <summary>
/// Turns a <see cref="ChannelPointsRedemption"/> into the localized text and the <see cref="ChatMessage"/>
/// shown in the chat list and in the events panel. All wording lives in the "EventMsg_Redemption*" and
/// "Redemption_Auto_*" keys in Services/Localization/&lt;Lang&gt;/EventMessages.cs.
/// </summary>
internal static class RedemptionMessageBuilder
{
    public static string BuildHeadline(ChannelPointsRedemption r, AppLanguage lang)
    {
        var title = r.RewardTitle;
        if (string.IsNullOrWhiteSpace(title) && !string.IsNullOrEmpty(r.AutoRewardType))
            title = GetAutoRewardName(r.AutoRewardType, lang);

        if (string.IsNullOrWhiteSpace(title))
            return Strings.Get("EventMsg_RedemptionGeneric", lang);

        if (r.Cost is int cost && cost > 0)
            return Strings.GetPlural("EventMsg_Redemption", cost, lang, title, cost);

        return string.Format(Strings.Get("EventMsg_RedemptionNoCost", lang), title);
    }

    /// <summary>Localized name of a built-in reward; falls back to a humanized version of the raw type.</summary>
    public static string GetAutoRewardName(string type, AppLanguage lang)
    {
        var key = "Redemption_Auto_" + type;
        var name = Strings.Get(key, lang);
        return name == key ? type.Replace('_', ' ') : name;
    }

    /// <summary>
    /// Builds the message for one of the two places a redemption is shown.
    /// Chat list: isSystem=false (DrawMessage then routes it to the event banner) and includeUserInput=false,
    /// because the viewer's own text already appears as a normal chat message (with its emotes).
    /// Events panel: isSystem=true (same as every other event) and includeUserInput=true.
    /// </summary>
    public static ChatMessage ToMessage(
        ChannelPointsRedemption r,
        AppLanguage lang,
        bool includeUserInput,
        bool isSystem
    )
    {
        var text = BuildHeadline(r, lang);
        if (includeUserInput && !string.IsNullOrWhiteSpace(r.UserInput))
            text += $"\n\"{r.UserInput}\"";

        var eventId = EventTypeIds.Twitch.ChannelPointsRedemption;
        var (platform, kind) = EventTypeIds.Classify(eventId);

        return new ChatMessage
        {
            Username = r.UserLogin,
            DisplayName = string.IsNullOrEmpty(r.DisplayName) ? r.UserLogin : r.DisplayName,
            IsSystem = isSystem,
            Color = ChatColors.ChannelPointsRedemption,
            Text = text,
            EventType = eventId,
            Platform = platform,
            EventKind = kind,
        };
    }
}
