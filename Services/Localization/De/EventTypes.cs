namespace TTNOverlay.Services;

internal static partial class DeStrings
{
    private static readonly Dictionary<string, string> EventTypesEntries = new()
    {
        ["EventType_Sub"] = "Abonnement (IRC)",
        ["EventType_Resub"] = "Erneutes Abonnement (IRC)",
        ["EventType_Subgift"] = "Verschenktes Abo (IRC)",
        ["EventType_AnonSubgift"] = "Anonymes verschenktes Abo (IRC)",
        ["EventType_MysteryGift"] = "Mystery-Abo (IRC)",
        ["EventType_AnonMysteryGift"] = "Anonymes Mystery-Abo (IRC)",
        ["EventType_PrimeUpgrade"] = "Prime → bezahlt",
        ["EventType_GiftUpgrade"] = "Fortgesetztes verschenktes Abo",
        ["EventType_AnonGiftUpgrade"] = "Fortgesetztes anonymes verschenktes Abo",
        ["EventType_Raid"] = "Raid (IRC)",
        ["EventType_Ritual"] = "Neuer Chatter",
        ["EventType_BitsBadge"] = "Bits-Abzeichen",
        ["EventType_Announcement"] = "Ankündigung",
        ["EventType_SlDonation"] = "Spende (Streamlabs)",
        ["EventType_SlFollow"] = "Follow (Streamlabs)",
        ["EventType_SlHost"] = "Host (Streamlabs)",
        ["EventType_SlMerch"] = "Merch (Streamlabs)",
        ["EventType_SlSubscription"] = "Abonnement (Streamlabs)",
        ["EventType_SlBits"] = "Bits (Streamlabs)",
        ["EventType_SlPowerup"] = "Power-Up (Streamlabs)",
        ["EventType_SlRaid"] = "Raid (Streamlabs)",
        ["EventType_SlSubgift"] = "Verschenktes Abo (Streamlabs)",
        ["EventType_SlAnonSubgift"] = "Anonymes verschenktes Abo (Streamlabs)",
        ["EventType_SlMysteryGift"] = "Mystery-Abo (Streamlabs)",
        ["EventType_SlAnonMysteryGift"] = "Anonymes Mystery-Abo (Streamlabs)",
        ["EventType_Short_NewChatter"] = "Neuer Chatter (Ritual)",
        ["EventType_Short_PrimeUpgrade"] = "Prime → bezahltes Upgrade",
        ["EventType_Short_GiftUpgrade"] = "Upgrade verschenktes Abo",
        ["EventType_Short_AnonGiftUpgrade"] = "Upgrade verschenktes Abo (anonym)",
        ["EventType_Short_GiftedSub"] = "Verschenktes Abonnement",
        ["EventType_Short_AnonGiftedSub"] = "Verschenktes Abonnement (anonym)",
        ["EventType_Short_MysterySub"] = "Mystery-Abonnement",
        ["EventType_Short_AnonMysterySub"] = "Mystery-Abonnement (anonym)",
    };
}