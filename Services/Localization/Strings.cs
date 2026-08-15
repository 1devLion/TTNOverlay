namespace TTNOverlay.Services;

/// <summary>
/// Entry point for looking up UI text by key and <see cref="AppLanguage"/>. The actual text lives
/// one level down, split by language: Services/Localization/En/ (EnStrings), Services/Localization/Es/
/// (EsStrings), etc.
/// </summary>
internal static class Strings
{
    public static string Get(string key, AppLanguage lang)
    {
        if (Tables.TryGetValue(lang, out var table) && table.TryGetValue(key, out var value))
            return value;

        if (lang != AppLanguage.English && EnStrings.Map.TryGetValue(key, out var fallback))
            return fallback;

        return key;
    }

    /// <summary>
    /// Plural bucket ("one" vs. "other") for a language/count combination.
    /// </summary>
    private enum PluralCategory
    {
        One,
        Other,
    }

    private static PluralCategory GetPluralCategory(AppLanguage lang, int count) =>
        lang switch
        {
            AppLanguage.French => count == 0 || count == 1 ? PluralCategory.One : PluralCategory.Other,
            AppLanguage.日本語 => PluralCategory.Other,
            AppLanguage.简体中文 => PluralCategory.Other,
            _ => count == 1 ? PluralCategory.One : PluralCategory.Other,
        };

    /// <summary>
    /// Looks up a count-dependent string. Expects "{key}_One" and "{key}_Other" entries in the
    /// language tables, picks between them based on <paramref name="count"/>, and formats the
    /// chosen template with <paramref name="formatArgs"/>.
    /// </summary>
    public static string GetPlural(string key, int count, AppLanguage lang, params object[] formatArgs)
    {
        var suffix = GetPluralCategory(lang, count) == PluralCategory.One ? "_One" : "_Other";
        var template = Get(key + suffix, lang);
        return string.Format(template, formatArgs);
    }

    private static readonly Dictionary<AppLanguage, Dictionary<string, string>> Tables = new()
    {
        [AppLanguage.English] = EnStrings.Map,
        [AppLanguage.Deutsch] = DeStrings.Map,
        [AppLanguage.French] = FrStrings.Map,
        [AppLanguage.日本語] = JaStrings.Map,
        [AppLanguage.Portuguese] = PtStrings.Map,
        [AppLanguage.Spanish] = EsStrings.Map,
        [AppLanguage.简体中文] = ZhStrings.Map,
        [AppLanguage.Русский] = RuStrings.Map,


    };

    static Strings()
    {
        WarnAboutMissingTranslations();
    }

    /// <summary>
    /// Logs any key present in English but missing from another language's table.
    /// </summary>
    private static void WarnAboutMissingTranslations()
    {
        foreach (var (lang, table) in Tables)
        {
            if (lang == AppLanguage.English)
                continue;

            List<string>? missing = null;
            foreach (var key in EnStrings.Map.Keys)
            {
                if (!table.ContainsKey(key))
                    (missing ??= new List<string>()).Add(key);
            }

            if (missing is { Count: > 0 })
                DebugLog.Write($"Strings: {lang} is missing {missing.Count} key(s): {string.Join(", ", missing)}");
        }
    }
}
