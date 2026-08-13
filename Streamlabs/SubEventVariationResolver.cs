using System.Net.Http;
using System.Text.Json;
using TTNOverlay.Services;

namespace TTNOverlay.Streamlabs;

/// <summary>
/// Resolves the specific sub-event text variation (new sub, resub, gifted, etc.) for a Streamlabs event payload.
/// </summary>
public static class SubEventVariationResolver
{
    private static readonly HttpClient Http = SharedHttpClient.Instance;

    private static readonly Dictionary<
        string,
        (string? Format, string? ImageUrl)
    > _lastKnownAlertConfig = new();

    private static readonly List<(
        string Condition,
        string? ConditionData,
        string? Format,
        string? ImageUrl
    )> _lastKnownSubVariations = new();

    public static async Task FetchAndSeedWidgetConfigAsync(string widgetToken)
    {
        if (string.IsNullOrWhiteSpace(widgetToken))
            return;

        try
        {
            var url =
                $"https://streamlabs.com/api/v5/widget/config?token={Uri.EscapeDataString(widgetToken.Trim())}&widget=alert_box";
            var json = await Http.GetStringAsync(url);

            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("settings", out var settings))
            {
                DebugLog.Write("Streamlabs: /widget/config no trajo \"settings\"");
                return;
            }

            SeedOne(settings, "follow", "follow");
            SeedOne(settings, "donation", "donation");
            SeedOne(settings, "twitchcharitydonation", "twitchcharitydonation");
            SeedOne(settings, "host", "host");
            SeedOne(settings, "raid", "raid");
            SeedOne(settings, "merch", "merch");
            SeedOne(settings, "bits", "bits");
            SeedOne(settings, "subscription", "sub");
            SeedOne(settings, "powerup", "power_up");
            SeedSubVariations(settings);

            DebugLog.Write(
                $"Streamlabs: config del Alert Box cargada ({_lastKnownAlertConfig.Count} tipos, "
                    + $"{_lastKnownSubVariations.Count} variaciones de sub)"
            );
        }
        catch (Exception ex)
        {
            DebugLog.WriteException("SubEventVariationResolver.FetchAndSeedWidgetConfigAsync", ex);

        }
    }

    private static void SeedOne(JsonElement settings, string normalizedType, string configPrefix)
    {
        var format = GetTopLevelString(settings, $"{configPrefix}_message_template");
        var imageUrl = ImageUrlHelper.Normalize(GetTopLevelString(settings, $"{configPrefix}_image_href"));

        if (!string.IsNullOrWhiteSpace(format) || !string.IsNullOrWhiteSpace(imageUrl))
            _lastKnownAlertConfig[normalizedType] = (format, imageUrl);
    }

    private static string? GetTopLevelString(JsonElement root, string key) =>
        root.TryGetProperty(key, out var el) && el.ValueKind == JsonValueKind.String
            ? el.GetString()
            : null;

    private static void SeedSubVariations(JsonElement settings)
    {
        if (
            !settings.TryGetProperty("sub_variations", out var variations)
            || variations.ValueKind != JsonValueKind.Array
        )
            return;

        _lastKnownSubVariations.Clear();

        foreach (var v in variations.EnumerateArray())
        {
            if (
                !v.TryGetProperty("condition", out var condEl)
                || condEl.ValueKind != JsonValueKind.String
            )
                continue;
            var cond = condEl.GetString();
            if (string.IsNullOrEmpty(cond))
                continue;

            string? condData =
                v.TryGetProperty("conditionData", out var condDataEl)
                && condDataEl.ValueKind == JsonValueKind.String
                    ? condDataEl.GetString()
                    : null;

            string? format = null;
            string? imageUrl = null;
            if (v.TryGetProperty("settings", out var settingsObj))
            {
                if (
                    settingsObj.TryGetProperty("text", out var textObj)
                    && textObj.TryGetProperty("format", out var formatEl)
                    && formatEl.ValueKind == JsonValueKind.String
                )
                    format = formatEl.GetString();

                if (
                    settingsObj.TryGetProperty("image", out var imageObj)
                    && imageObj.TryGetProperty("href", out var hrefEl)
                    && hrefEl.ValueKind == JsonValueKind.String
                )
                    imageUrl = ImageUrlHelper.Normalize(hrefEl.GetString());
            }

            _lastKnownSubVariations.Add((cond, condData, format, imageUrl));
        }
    }

    public static (string? Format, string? ImageUrl)? TryGetAlertConfig(string normalizedType) =>
        _lastKnownAlertConfig.TryGetValue(normalizedType, out var cached) ? cached : null;

    public static void SetAlertConfig(string normalizedType, string? format, string? imageUrl) =>
        _lastKnownAlertConfig[normalizedType] = (format, imageUrl);

    public static (string? Format, string? ImageUrl)? GetVariationByCondition(string condition)
    {
        var match = _lastKnownSubVariations.FirstOrDefault(v => v.Condition == condition);
        return match.Condition is null ? null : (match.Format, match.ImageUrl);
    }

    public static (string? Format, string? ImageUrl)? ResolveCachedSubVariation(
        string subType,
        bool isAnonymous,
        string? subPlanRaw = null
    )
    {
        if (_lastKnownSubVariations.Count == 0)
            return null;

        static bool IsMysteryCondition(string c) =>
            c.Contains("MYSTERY", StringComparison.OrdinalIgnoreCase)
            || c.Contains("MASS", StringComparison.OrdinalIgnoreCase);
        static bool IsGiftCondition(string c) =>
            c.Contains("GIFT", StringComparison.OrdinalIgnoreCase);
        static bool IsAnonCondition(string c) =>
            c.Contains("ANON", StringComparison.OrdinalIgnoreCase);

        var candidates = (
            subType == "submysterygift"
                ? _lastKnownSubVariations.Where(v =>
                    IsGiftCondition(v.Condition)
                    && IsMysteryCondition(v.Condition)
                    && IsAnonCondition(v.Condition) == isAnonymous
                )
                : _lastKnownSubVariations.Where(v =>
                    IsGiftCondition(v.Condition)
                    && !IsMysteryCondition(v.Condition)
                    && IsAnonCondition(v.Condition) == isAnonymous
                )
        ).ToList();

        if (!string.IsNullOrEmpty(subPlanRaw))
        {
            var tierMatch = candidates.FirstOrDefault(v =>
                MatchesTierOrPrime(v.Condition, v.ConditionData, subPlanRaw)
            );
            if (tierMatch.Condition is not null)
                return (tierMatch.Format, tierMatch.ImageUrl);
        }

        var match = candidates.FirstOrDefault();
        return match.Condition is null ? null : (match.Format, match.ImageUrl);
    }

    public static (string? Format, string? ImageUrl)? ResolveTierSubVariation(string? subPlanRaw)
    {
        if (_lastKnownSubVariations.Count == 0 || string.IsNullOrEmpty(subPlanRaw))
            return null;

        static bool IsGiftCondition(string c) =>
            c.Contains("GIFT", StringComparison.OrdinalIgnoreCase);

        var match = _lastKnownSubVariations.FirstOrDefault(v =>
            !IsGiftCondition(v.Condition)
            && MatchesTierOrPrime(v.Condition, v.ConditionData, subPlanRaw)
        );

        return match.Condition is null ? null : (match.Format, match.ImageUrl);
    }

    private static bool MatchesTierOrPrime(
        string condition,
        string? conditionData,
        string subPlanRaw
    )
    {
        if (subPlanRaw == "prime")
            return condition.Contains("PRIME", StringComparison.OrdinalIgnoreCase);

        var tierDigit = subPlanRaw.Length > 0 ? subPlanRaw[0].ToString() : null;
        return condition.Contains("TIER", StringComparison.OrdinalIgnoreCase)
            && conditionData == tierDigit;
    }

    public static string? GetSubPlanName(string? subPlan) =>
        subPlan switch
        {
            "1000" => "1",
            "2000" => "2",
            "3000" => "3",
            "prime" => "Prime",
            _ => subPlan,
        };

    internal static void SeedSubVariationsForTests(
        IEnumerable<(string Condition, string? ConditionData, string? Format, string? ImageUrl)> variations
    )
    {
        _lastKnownSubVariations.Clear();
        _lastKnownSubVariations.AddRange(variations);
    }

    public static void Clear()
    {
        _lastKnownAlertConfig.Clear();
        _lastKnownSubVariations.Clear();
    }
}

