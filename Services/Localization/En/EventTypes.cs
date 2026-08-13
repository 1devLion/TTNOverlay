namespace TTNOverlay.Services;

internal static partial class EnStrings
{
    private static readonly Dictionary<string, string> EventTypesEntries = new()
    {
        ["EventType_Sub"] = "Subscription (IRC)",
        ["EventType_Resub"] = "Re-subscription (IRC)",
        ["EventType_Subgift"] = "Gifted sub (IRC)",
        ["EventType_AnonSubgift"] = "Anonymous gifted sub (IRC)",
        ["EventType_MysteryGift"] = "Mystery sub (IRC)",
        ["EventType_AnonMysteryGift"] = "Anonymous mystery sub (IRC)",
        ["EventType_PrimeUpgrade"] = "Prime → paid",
        ["EventType_GiftUpgrade"] = "Continued gifted sub",
        ["EventType_AnonGiftUpgrade"] = "Continued anonymous gifted sub",
        ["EventType_Raid"] = "Raid (IRC)",
        ["EventType_Ritual"] = "New chatter",
        ["EventType_BitsBadge"] = "Bits badge",
        ["EventType_Announcement"] = "Announcement",
        ["EventType_SlDonation"] = "Donation (Streamlabs)",
        ["EventType_SlFollow"] = "Follow (Streamlabs)",
        ["EventType_SlHost"] = "Host (Streamlabs)",
        ["EventType_SlMerch"] = "Merch (Streamlabs)",
        ["EventType_SlSubscription"] = "Subscription (Streamlabs)",
        ["EventType_SlBits"] = "Bits (Streamlabs)",
        ["EventType_SlPowerup"] = "Power-Up (Streamlabs)",
        ["EventType_SlRaid"] = "Raid (Streamlabs)",
        ["EventType_SlSubgift"] = "Gifted sub (Streamlabs)",
        ["EventType_SlAnonSubgift"] = "Anonymous gifted sub (Streamlabs)",
        ["EventType_SlMysteryGift"] = "Mystery sub (Streamlabs)",
        ["EventType_SlAnonMysteryGift"] = "Anonymous mystery sub (Streamlabs)",
        ["EventType_Short_NewChatter"] = "New chatter (ritual)",
        ["EventType_Short_PrimeUpgrade"] = "Prime → paid upgrade",
        ["EventType_Short_GiftUpgrade"] = "Gifted sub upgrade",
        ["EventType_Short_AnonGiftUpgrade"] = "Gifted sub upgrade (anonymous)",
        ["EventType_Short_GiftedSub"] = "Gifted subscription",
        ["EventType_Short_AnonGiftedSub"] = "Gifted subscription (anonymous)",
        ["EventType_Short_MysterySub"] = "Mystery subscription",
        ["EventType_Short_AnonMysterySub"] = "Mystery subscription (anonymous)",
    };
}
