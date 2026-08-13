namespace TTNOverlay.Services;

internal static partial class ZhStrings
{
    private static readonly Dictionary<string, string> EventMessagesEntries = new()
    {
        ["EventMsg_PlanPrime"] = "Prime",
        ["EventMsg_PlanTier1"] = "1级",
        ["EventMsg_PlanTier2"] = "2级",
        ["EventMsg_PlanTier3"] = "3级",
        ["EventMsg_Sub"] = "以 {0} 订阅了频道。",
        ["EventMsg_ResubHead"] = "以 {0} 续订了频道。",
        ["EventMsg_Subgift"] = "向 {1} 赠送了一份 {0} 订阅。",
        ["EventMsg_SubgiftMonths"] = "（赠送 {0} 个月）",
        ["EventMsg_AnonSubgift"] = "一位匿名观众向 {1} 赠送了一份 {0} 订阅。",
        ["EventMsg_MysteryGiftTotal"] = "（在本频道累计赠送 {0} 份）",
        ["EventMsg_MysteryGiftSponsor"] = "，由 {0} 赞助",
        ["EventMsg_RitualNewChatter"] = "第一次在聊天区发言。",
        ["EventMsg_RitualOther"] = "参与了一次聊天事件。",
        ["EventMsg_BitsBadge"] = "获得了 {0} 比特徽章。",
        ["EventMsg_PrimeUpgrade"] = "已从 Prime 升级为付费订阅（{0}）。",
        ["EventMsg_GiftUpgrade"] = "继续付费维持由 {0} 赠送的订阅。",
        ["EventMsg_AnonGiftUpgrade"] = "继续付费维持由匿名观众赠送的订阅。",
        ["EventMsg_UnknownSponsor"] = "某个活动",
        ["EventMsg_ViewerMilestone"] = "达成了一个里程碑：{0}（{1}）。",
        ["EventMsg_ResubStreak_Other"] = "连续 {0} 个月！",
        ["EventMsg_ResubTotal_Other"] = "累计订阅 {0} 个月。",
        ["EventMsg_MysteryGift_Other"] = "向频道赠送了 {0} 份神秘订阅",
        ["EventMsg_AnonMysteryGift_Other"] = "一位匿名观众向频道赠送了 {0} 份神秘订阅",
        ["EventMsg_Raid_Other"] = "带着 {0} 名观众发起了突袭。",
        ["EventMsg_WatchStreak_Other"] = "达成了连续 {0} 场直播的观看连击！",
        ["EventMsg_BonusGift_Other"] = "获得了由 {1} 赞助的 {0} 份额外订阅！",
    };
}
