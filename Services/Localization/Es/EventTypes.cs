namespace TTNOverlay.Services;

internal static partial class EsStrings
{
    private static readonly Dictionary<string, string> EventTypesEntries = new()
    {
        ["EventType_Sub"] = "Suscripción (IRC)",
        ["EventType_Resub"] = "Re-suscripción (IRC)",
        ["EventType_Subgift"] = "Sub regalada (IRC)",
        ["EventType_AnonSubgift"] = "Sub regalada anónima (IRC)",
        ["EventType_MysteryGift"] = "Sub misteriosa (IRC)",
        ["EventType_AnonMysteryGift"] = "Sub misteriosa anónima (IRC)",
        ["EventType_PrimeUpgrade"] = "Prime → pago",
        ["EventType_GiftUpgrade"] = "Continúa sub regalada",
        ["EventType_AnonGiftUpgrade"] = "Continúa sub regalada anónima",
        ["EventType_Raid"] = "Raid (IRC)",
        ["EventType_Ritual"] = "Nuevo chatter",
        ["EventType_BitsBadge"] = "Insignia de bits",
        ["EventType_Announcement"] = "Anuncio",
        ["EventType_SlDonation"] = "Donación (Streamlabs)",
        ["EventType_SlFollow"] = "Follow (Streamlabs)",
        ["EventType_SlHost"] = "Host (Streamlabs)",
        ["EventType_SlMerch"] = "Merch (Streamlabs)",
        ["EventType_SlSubscription"] = "Suscripción (Streamlabs)",
        ["EventType_SlBits"] = "Bits (Streamlabs)",
        ["EventType_SlPowerup"] = "Power-Up (Streamlabs)",
        ["EventType_SlRaid"] = "Raid (Streamlabs)",
        ["EventType_SlSubgift"] = "Sub regalada (Streamlabs)",
        ["EventType_SlAnonSubgift"] = "Sub regalada anónima (Streamlabs)",
        ["EventType_SlMysteryGift"] = "Sub misteriosa (Streamlabs)",
        ["EventType_SlAnonMysteryGift"] = "Sub misteriosa anónima (Streamlabs)",
        ["EventType_Short_NewChatter"] = "Nuevo chatter (ritual)",
        ["EventType_Short_PrimeUpgrade"] = "Actualización Prime → pago",
        ["EventType_Short_GiftUpgrade"] = "Actualización de suscripción regalada",
        ["EventType_Short_AnonGiftUpgrade"] = "Actualización de suscripción regalada (anónimo)",
        ["EventType_Short_GiftedSub"] = "Suscripción regalada",
        ["EventType_Short_AnonGiftedSub"] = "Suscripción regalada (anónimo)",
        ["EventType_Short_MysterySub"] = "Suscripción misteriosa",
        ["EventType_Short_AnonMysterySub"] = "Suscripción misteriosa (anónimo)",
    };
}
