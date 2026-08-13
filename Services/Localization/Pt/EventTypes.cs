namespace TTNOverlay.Services;

internal static partial class PtStrings
{
    private static readonly Dictionary<string, string> EventTypesEntries = new()
    {
        ["EventType_Sub"] = "Inscrição (IRC)",
        ["EventType_Resub"] = "Reinscrição (IRC)",
        ["EventType_Subgift"] = "Inscrição presenteada (IRC)",
        ["EventType_AnonSubgift"] = "Inscrição presenteada anônima (IRC)",
        ["EventType_MysteryGift"] = "Inscrição misteriosa (IRC)",
        ["EventType_AnonMysteryGift"] = "Inscrição misteriosa anônima (IRC)",
        ["EventType_PrimeUpgrade"] = "Prime → pago",
        ["EventType_GiftUpgrade"] = "Continua inscrição presenteada",
        ["EventType_AnonGiftUpgrade"] = "Continua inscrição presenteada anônima",
        ["EventType_Raid"] = "Raid (IRC)",
        ["EventType_Ritual"] = "Novo usuário",
        ["EventType_BitsBadge"] = "Emblema de bits",
        ["EventType_Announcement"] = "Anúncio",
        ["EventType_SlDonation"] = "Doação (Streamlabs)",
        ["EventType_SlFollow"] = "Seguir (Streamlabs)",
        ["EventType_SlHost"] = "Host (Streamlabs)",
        ["EventType_SlMerch"] = "Merch (Streamlabs)",
        ["EventType_SlSubscription"] = "Inscrição (Streamlabs)",
        ["EventType_SlBits"] = "Bits (Streamlabs)",
        ["EventType_SlPowerup"] = "Power-Up (Streamlabs)",
        ["EventType_SlRaid"] = "Raid (Streamlabs)",
        ["EventType_SlSubgift"] = "Inscrição presenteada (Streamlabs)",
        ["EventType_SlAnonSubgift"] = "Inscrição presenteada anônima (Streamlabs)",
        ["EventType_SlMysteryGift"] = "Inscrição misteriosa (Streamlabs)",
        ["EventType_SlAnonMysteryGift"] = "Inscrição misteriosa anônima (Streamlabs)",
        ["EventType_Short_NewChatter"] = "Novo usuário (ritual)",
        ["EventType_Short_PrimeUpgrade"] = "Atualização Prime → pago",
        ["EventType_Short_GiftUpgrade"] = "Atualização de inscrição presenteada",
        ["EventType_Short_AnonGiftUpgrade"] = "Atualização de inscrição presenteada (anônimo)",
        ["EventType_Short_GiftedSub"] = "Inscrição presenteada",
        ["EventType_Short_AnonGiftedSub"] = "Inscrição presenteada (anônimo)",
        ["EventType_Short_MysterySub"] = "Inscrição misteriosa",
        ["EventType_Short_AnonMysterySub"] = "Inscrição misteriosa (anônimo)",
    };
}