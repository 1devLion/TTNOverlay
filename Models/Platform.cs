namespace TTNOverlay.Models;

/// <summary>
/// The service an event/message originated from.
///
/// This exists to disambiguate raw event ids that share the same <see cref="EventType"/>: Twitch's
/// "sub" and Streamlabs' "sl_subscription" both classify as <see cref="EventType.Sub"/>, but they stay
/// separate, independently-configurable entries in settings.json on purpose (a user may want a
/// different color/GIF for the Twitch-native sub notice vs. the Streamlabs one for the same sub).
///
/// Kick and YouTube are reserved for when those integrations are added. The intended shape of that
/// change is: add the enum case here, add a raw-id constants block + Parse function in
/// <see cref="EventTypeIds"/>, and register the new ids in the Settings > Alerts lists (Alerts.cs).
/// Everything that switches on <see cref="EventType"/> instead of on raw strings or on Platform
/// directly (icon/color defaults, event "family" grouping/dedup, localized text) does not need to
/// change for a new platform, as long as its events map onto the existing EventType cases.
/// </summary>
public enum Platform
{
    Twitch,
    Streamlabs,
    Kick,
    YouTube,
}