using System.Text.Json;
using TTNOverlay.Models;
using TTNOverlay.Services;

namespace TTNOverlay.Streamlabs;

/// <summary>
/// Maps raw Streamlabs socket events (donations, subs, follows, etc.) into ChatMessage entries for display.
///
/// All wording lives in the "SlMsg_*" keys in Services/Localization/&lt;Lang&gt;/StreamlabsMessages.cs
/// (see Strings.cs for how languages/fallback work). This class only picks which key(s) apply and
/// fills in the placeholders. Add a language by adding a StreamlabsMessages.cs there, not here.
/// </summary>
internal static class StreamlabsEventMapper
{
    internal static IEnumerable<ChatMessage> MapToMessages(JsonElement data)
    {
        var lang = LocalizationService.Instance.CurrentLanguage;

        if (data.ValueKind != JsonValueKind.Object)
            yield break;
        if (
            !data.TryGetProperty("type", out var typeEl)
            || typeEl.ValueKind != JsonValueKind.String
        )
            yield break;

        var type = (typeEl.GetString() ?? "").ToLowerInvariant();
        var normalizedType = EventTypeIds.NormalizeStreamlabsRawType(type);

        if (
            !data.TryGetProperty("message", out var messages)
            || messages.ValueKind != JsonValueKind.Array
        )
            yield break;

        foreach (var item in messages.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
                continue;

            var name =
                GetFirstString(
                    item,
                    "display_name",
                    "name",
                    "username",
                    "displayName",
                    "redeemer_display_name",
                    "from"
                ) ?? "???";
            var gifterDisplay = GetFirstString(item, "gifter_display_name", "gifter") ?? "";
            var comment = GetFirstString(item, "message", "comment");
            var months = GetFirstInt(item, "months");
            var amountInt = GetFirstInt(item, "amount");
            var amountStr = GetAmountString(item);

            // Power-ups don't necessarily report "amount". Streamlabs' own widget template for this
            // event type uses {powerUpName}/{bitsSpent} tokens (see ReplacePlaceholders), so read those
            // directly too. bitsSpent falls back to "amount" when Streamlabs doesn't send a dedicated
            // bits field, and also backfills amountStr so the built-in (non-custom-template) Power-up
            // text still shows a bits count.
            var powerUpName = GetFirstString(item, "powerUpName", "power_up_name", "powerupName");
            var bitsSpentInt = GetFirstInt(item, "bitsSpent", "bits_spent") ?? amountInt;
            var bitsSpentStr = bitsSpentInt?.ToString();
            if (type == "powerup" && string.IsNullOrEmpty(amountStr))
                amountStr = bitsSpentStr;

            // Merch's own widget template uses {product} (see ReplacePlaceholders). Streamlabs sends
            // the product name flat as "product" on the message item, not under unsavedSettings.
            var product = GetFirstString(item, "product");

            var streakMonths = GetFirstInt(item, "streak_months");
            var subPlanRaw = GetFirstString(item, "sub_plan");
            var subPlanName = subPlanRaw is null
                ? null
                : SubEventVariationResolver.GetSubPlanName(subPlanRaw);

            var subType =
                (type == "subscription" || type == "submysterygift" || type == "resub")
                    ? GetFirstString(item, "sub_type")?.ToLowerInvariant() ?? ""
                    : "";

            if (type == "resub")
                subType = "resub";

            var condition = GetFirstString(item, "condition");
            var variation =
                condition is null ? null : GetVariationSettings(item, condition, subPlanRaw);

            var usedTierVariation = false;
            if (variation is null && subType is "subgift" or "submysterygift")
                variation = SubEventVariationResolver.ResolveCachedSubVariation(
                    subType,
                    IsAnonymous(item),
                    subPlanRaw
                );
            else if (variation is null && subType is "" or "resub")
            {
                variation = SubEventVariationResolver.ResolveTierSubVariation(subPlanRaw);
                usedTierVariation = variation is not null;
            }

            var customFormat =
                variation?.Format ?? GetFlatSetting(item, "message_template", type, normalizedType);

            var customImageUrl = ImageUrlHelper.Normalize(
                variation?.ImageUrl ?? GetFlatSetting(item, "image_href", type, normalizedType)
            );

            var isGiftSubType = subType is "subgift" or "submysterygift" || usedTierVariation;
            if (
                string.IsNullOrWhiteSpace(customFormat) && string.IsNullOrWhiteSpace(customImageUrl)
            )
            {
                if (!isGiftSubType)
                {
                    var cachedConfig = SubEventVariationResolver.TryGetAlertConfig(normalizedType);
                    if (cachedConfig is not null)
                    {
                        customFormat = cachedConfig.Value.Format;
                        customImageUrl = cachedConfig.Value.ImageUrl;
                    }
                }
            }
            else if (!isGiftSubType)
            {
                SubEventVariationResolver.SetAlertConfig(
                    normalizedType,
                    customFormat,
                    customImageUrl
                );
            }

            string displayName = name;
            string eventType = "sl_" + normalizedType;
            string eventText;

            var viewersCount = GetFirstInt(item, "viewers", "raiders");

            if (type == "subscription" || type == "submysterygift" || type == "resub")
            {
                switch (subType)
                {
                    case "subgift":
                        displayName = name;
                        if (IsAnonymous(item))
                        {
                            eventType = "sl_anonsubgift";
                            eventText = Strings.Get("SlMsg_AnonSubgift", lang);
                        }
                        else
                        {
                            eventType = "sl_subgift";
                            eventText = string.IsNullOrEmpty(gifterDisplay)
                                ? Strings.Get("SlMsg_Subgift", lang)
                                : string.Format(Strings.Get("SlMsg_SubgiftFrom", lang), gifterDisplay);
                        }
                        if (months is > 1)
                            eventText += string.Format(Strings.Get("SlMsg_SubgiftMonths", lang), months);
                        eventText += BuildSubSuffix(streakMonths, subPlanName, lang);
                        break;

                    case "submysterygift":
                        var count = amountInt ?? 1;
                        if (IsAnonymous(item))
                        {
                            eventType = "sl_anonmysterygift";
                            displayName = "";
                            eventText = Strings.GetPlural("SlMsg_AnonMysteryGift", count, lang, count);
                        }
                        else
                        {
                            eventType = "sl_submysterygift";
                            displayName = string.IsNullOrEmpty(gifterDisplay)
                                ? Strings.Get("SlMsg_Someone", lang)
                                : gifterDisplay;
                            eventText = Strings.GetPlural("SlMsg_MysteryGift", count, lang, count);
                        }
                        eventText += BuildSubSuffix(null, subPlanName, lang);
                        break;

                    case "resub":

                        displayName = name;
                        eventType = "sl_subscription";
                        eventText = months is > 1
                            ? string.Format(Strings.Get("SlMsg_ResubMonths", lang), months)
                            : Strings.Get("SlMsg_Resub", lang);
                        eventText += BuildSubSuffix(streakMonths, subPlanName, lang);
                        break;

                    default:

                        displayName = name;
                        eventType = "sl_subscription";
                        eventText = Strings.Get("SlMsg_Subscribed", lang);
                        eventText += BuildSubSuffix(streakMonths, subPlanName, lang);
                        break;
                }
            }
            else
            {

                eventText = BuildEventText(
                    normalizedType,
                    amountStr,
                    comment,
                    months,
                    viewersCount,
                    lang
                );
            }

            if (!string.IsNullOrWhiteSpace(customFormat))
            {
                eventText = ReplacePlaceholders(
                    customFormat,
                    lang,
                    gifter: gifterDisplay,
                    name: name,
                    amount: amountStr,
                    months: months?.ToString(),
                    streakMonths: streakMonths?.ToString(),
                    subPlan: subPlanName,
                    viewers: viewersCount?.ToString(),
                    powerUpName: powerUpName,
                    bitsSpent: bitsSpentStr,
                    product: product
                );
            }

            if (!string.IsNullOrWhiteSpace(comment))
                eventText += $"\n\"{comment}\"";

            var (eventPlatform, eventKind) = EventTypeIds.Classify(eventType);

            yield return new ChatMessage
            {
                DisplayName = displayName,
                IsSystem = true,
                Color = ChatColors.StreamlabsEvent,
                EventType = eventType,
                Platform = eventPlatform,
                EventKind = eventKind,
                Text = eventText,
                StreakMonths = streakMonths,
                SubPlanName = subPlanName,
                EventImageUrl = customImageUrl,
            };
        }
    }

    private static string BuildEventText(
        string type,
        string? amount,
        string? comment,
        int? months,
        int? viewers,
        AppLanguage lang
    )
    {
        var head = type switch
        {
            "donation" => string.IsNullOrEmpty(amount)
                ? Strings.Get("SlMsg_DonationPlain", lang)
                : string.Format(Strings.Get("SlMsg_Donation", lang), amount),
            "follow" => Strings.Get("SlMsg_Follow", lang),
            "host" => viewers is { } v
                ? string.Format(Strings.Get("SlMsg_HostViewers", lang), v)
                : Strings.Get("SlMsg_Host", lang),
            "raid" => viewers is { } r
                ? string.Format(Strings.Get("SlMsg_RaidViewers", lang), r)
                : Strings.Get("SlMsg_Raid", lang),
            "merch" => Strings.Get("SlMsg_Merch", lang),
            "subscription" => months is > 1
                ? string.Format(Strings.Get("SlMsg_SubscriptionMonths", lang), months)
                : Strings.Get("SlMsg_Subscribed", lang),
            "bits" => string.IsNullOrEmpty(amount)
                ? Strings.Get("SlMsg_Bits", lang)
                : string.Format(Strings.Get("SlMsg_BitsAmount", lang), amount),
            "powerup" => string.IsNullOrEmpty(amount)
                ? Strings.Get("SlMsg_Powerup", lang)
                : string.Format(Strings.Get("SlMsg_PowerupAmount", lang), amount),
            _ => type,
        };

        return string.IsNullOrWhiteSpace(comment) ? head : $"{head}\n\"{comment}\"";
    }

    private static string BuildSubSuffix(int? streakMonths, string? subPlanName, AppLanguage lang)
    {
        var parts = new List<string>();
        if (streakMonths is > 1)
            parts.Add(string.Format(Strings.Get("SlMsg_StreakSuffix", lang), streakMonths));
        if (!string.IsNullOrEmpty(subPlanName))
            parts.Add(subPlanName);
        return parts.Count == 0 ? "" : $" · {string.Join(" · ", parts)}";
    }

    private static string? GetFirstString(JsonElement obj, params string[] names)
    {
        foreach (var name in names)
        {
            if (obj.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String)
                return v.GetString();
        }
        return null;
    }

    private static string? GetAmountString(JsonElement item)
    {
        var formatted = GetFirstString(item, "formatted_amount", "formattedAmount");
        if (!string.IsNullOrEmpty(formatted))
            return formatted;

        if (!item.TryGetProperty("amount", out var amountEl))
            return null;

        string? raw = amountEl.ValueKind switch
        {
            JsonValueKind.Number => amountEl.GetRawText(),
            JsonValueKind.String => amountEl.GetString(),
            _ => null,
        };
        if (raw is null)
            return null;

        var currency = GetFirstString(item, "currency");
        return currency is null ? raw : $"{raw} {currency}";
    }

    private static int? GetFirstInt(JsonElement obj, params string[] names)
    {
        foreach (var name in names)
        {
            if (!obj.TryGetProperty(name, out var v))
                continue;
            if (v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var n))
                return n;
            if (v.ValueKind == JsonValueKind.String && int.TryParse(v.GetString(), out var n2))
                return n2;
        }
        return null;
    }

    private static bool IsAnonymous(JsonElement item)
    {

        if (
            item.TryGetProperty("is_anonymous", out var isAnon)
            && isAnon.ValueKind == JsonValueKind.True
        )
            return true;
        if (item.TryGetProperty("anonymous", out var anon) && anon.ValueKind == JsonValueKind.True)
            return true;

        if (
            item.TryGetProperty("condition", out var conditionEl)
            && conditionEl.ValueKind == JsonValueKind.String
        )
        {
            var cond = conditionEl.GetString() ?? "";
            if (cond.Contains("ANON", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        if (item.TryGetProperty("gifter", out var gifter))
        {
            if (gifter.ValueKind == JsonValueKind.Null)
                return true;
            if (gifter.ValueKind == JsonValueKind.String)
            {
                var gifterStr = gifter.GetString() ?? "";
                if (
                    string.IsNullOrEmpty(gifterStr)
                    || gifterStr.Equals("anonymous", StringComparison.OrdinalIgnoreCase)
                )
                    return true;
            }
        }
        if (item.TryGetProperty("gifter_display_name", out var gifterDisplay))
        {
            if (gifterDisplay.ValueKind == JsonValueKind.Null)
                return true;
            if (gifterDisplay.ValueKind == JsonValueKind.String)
            {
                var display = gifterDisplay.GetString() ?? "";
                if (
                    string.IsNullOrEmpty(display)
                    || display.Equals("anonymous", StringComparison.OrdinalIgnoreCase)
                )
                    return true;
            }
        }
        return false;
    }

    private static (string? Format, string? ImageUrl)? GetVariationSettings(
        JsonElement item,
        string condition,
        string? subPlanRaw = null
    )
    {
        if (
            !item.TryGetProperty("unsavedSettings", out var unsaved)
            || !unsaved.TryGetProperty("sub_variations", out var variations)
            || variations.ValueKind != JsonValueKind.Array
        )
            return null;

        var requiresTierMatch = condition.Contains("TIER", StringComparison.OrdinalIgnoreCase)
            && !condition.Contains("GIFT", StringComparison.OrdinalIgnoreCase);
        string? tierDigit =
            requiresTierMatch && !string.IsNullOrEmpty(subPlanRaw) ? subPlanRaw[0].ToString() : null;

        foreach (var var in variations.EnumerateArray())
        {
            if (!var.TryGetProperty("condition", out var condEl) || condEl.GetString() != condition)
                continue;

            if (requiresTierMatch)
            {
                var condData =
                    var.TryGetProperty("conditionData", out var condDataEl)
                    && condDataEl.ValueKind == JsonValueKind.String
                        ? condDataEl.GetString()
                        : null;
                if (condData != tierDigit)
                    continue;
            }

            if (!var.TryGetProperty("settings", out var settingsObj))
                continue;

            string? format = null;
            if (
                settingsObj.TryGetProperty("text", out var textObj)
                && textObj.TryGetProperty("format", out var formatEl)
                && formatEl.ValueKind == JsonValueKind.String
            )
            {
                format = formatEl.GetString();
            }

            string? imageUrl = null;
            if (
                settingsObj.TryGetProperty("image", out var imageObj)
                && imageObj.TryGetProperty("href", out var hrefEl)
                && hrefEl.ValueKind == JsonValueKind.String
            )
            {
                imageUrl = hrefEl.GetString();
            }

            return (format, imageUrl);
        }
        return null;
    }

    private static string? GetFlatSetting(
        JsonElement item,
        string suffix,
        params string[] typeCandidates
    )
    {
        if (
            !item.TryGetProperty("unsavedSettings", out var unsaved)
            || unsaved.ValueKind != JsonValueKind.Object
        )
            return null;

        foreach (var prefix in typeCandidates)
        {
            var key = $"{prefix}_{suffix}";
            if (unsaved.TryGetProperty(key, out var el) && el.ValueKind == JsonValueKind.String)
            {
                var val = el.GetString();
                if (!string.IsNullOrWhiteSpace(val))
                    return val;
            }
        }
        return null;
    }

    private static string ReplacePlaceholders(
        string template,
        AppLanguage lang,
        string? gifter,
        string? name,
        string? amount,
        string? months,
        string? streakMonths = null,
        string? subPlan = null,
        string? viewers = null,
        string? powerUpName = null,
        string? bitsSpent = null,
        string? product = null
    )
    {
        var result = template
            .Replace("{gifter}", gifter ?? Strings.Get("SlMsg_Someone", lang))
            .Replace("{name}", name ?? Strings.Get("SlMsg_SomeoneLower", lang))
            .Replace("{amount}", amount ?? "?")
            .Replace("{months}", months ?? "?")
            .Replace("{streak_months}", streakMonths ?? "?")
            .Replace("{streakMonths}", streakMonths ?? "?")
            .Replace("{sub_plan}", subPlan ?? "?")
            .Replace("{subPlan}", subPlan ?? "?")

            .Replace("{count}", viewers ?? "?")
            .Replace("{viewers}", viewers ?? "?")
            .Replace("{raiders}", viewers ?? "?")

            .Replace("{powerUpName}", powerUpName ?? "?")
            .Replace("{power_up_name}", powerUpName ?? "?")
            .Replace("{bitsSpent}", bitsSpent ?? "?")
            .Replace("{bits_spent}", bitsSpent ?? "?")

            .Replace("{product}", product ?? "?");

        return result;
    }
}