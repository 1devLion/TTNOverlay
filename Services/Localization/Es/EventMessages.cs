namespace TTNOverlay.Services;

internal static partial class EsStrings
{
    private static readonly Dictionary<string, string> EventMessagesEntries = new()
    {
        ["EventMsg_PlanPrime"] = "Prime",
        ["EventMsg_PlanTier1"] = "Nivel 1",
        ["EventMsg_PlanTier2"] = "Nivel 2",
        ["EventMsg_PlanTier3"] = "Nivel 3",
        ["EventMsg_Sub"] = "Se suscribió con {0}.",
        ["EventMsg_ResubHead"] = "Se resuscribió con {0}.",
        ["EventMsg_Subgift"] = "Le regaló una suscripción {0} a {1}.",
        ["EventMsg_SubgiftMonths"] = " ({0} meses regalados)",
        ["EventMsg_AnonSubgift"] = "Alguien anónimo le regaló una suscripción {0} a {1}.",
        ["EventMsg_MysteryGiftTotal"] = " (lleva {0} regalados en el canal)",
        ["EventMsg_MysteryGiftSponsor"] = ", patrocinado por {0}",
        ["EventMsg_RitualNewChatter"] = "Escribió por primera vez en el chat.",
        ["EventMsg_RitualOther"] = "Participó de un evento del chat.",
        ["EventMsg_BitsBadge"] = "Alcanzó la insignia de {0} bits.",
        ["EventMsg_PrimeUpgrade"] = "Pasó de Prime a una suscripción paga ({0}).",
        ["EventMsg_GiftUpgrade"] = "Continuó pagando la suscripción que le regaló {0}.",
        ["EventMsg_AnonGiftUpgrade"] = "Continuó pagando la suscripción que le regaló alguien anónimo.",
        ["EventMsg_UnknownSponsor"] = "un evento",
        ["EventMsg_ViewerMilestone"] = "Alcanzó un hito: {0} ({1}).",
        ["EventMsg_ResubStreak_One"] = " ¡Racha de {0} mes seguido!",
        ["EventMsg_ResubStreak_Other"] = " ¡Racha de {0} meses seguidos!",
        ["EventMsg_ResubTotal_One"] = "Lleva {0} mes en total.",
        ["EventMsg_ResubTotal_Other"] = "Lleva {0} meses en total.",
        ["EventMsg_MysteryGift_One"] = "Regaló {0} suscripción misteriosa al canal",
        ["EventMsg_MysteryGift_Other"] = "Regaló {0} suscripciones misteriosas al canal",
        ["EventMsg_AnonMysteryGift_One"] = "Un viewer anónimo regaló {0} suscripción misteriosa al canal",
        ["EventMsg_AnonMysteryGift_Other"] = "Un viewer anónimo regaló {0} suscripciones misteriosas al canal",
        ["EventMsg_Raid_One"] = "Llegó en raid con {0} espectador.",
        ["EventMsg_Raid_Other"] = "Llegó en raid con {0} espectadores.",
        ["EventMsg_WatchStreak_One"] = "¡Alcanzó una racha de {0} stream consecutivo!",
        ["EventMsg_WatchStreak_Other"] = "¡Alcanzó una racha de {0} streams consecutivos!",
        ["EventMsg_BonusGift_One"] = "¡Recibió {0} suscripción extra patrocinada por {1}!",
        ["EventMsg_BonusGift_Other"] = "¡Recibió {0} suscripciones extra patrocinadas por {1}!",
    };
}
