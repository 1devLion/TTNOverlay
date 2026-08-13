namespace TTNOverlay.Services;

/// <summary>
/// Entry point for looking up UI text by key and <see cref="AppLanguage"/>.
///
/// The actual text lives one level down, split by language: Services/Localization/En/ (EnStrings)
/// and Services/Localization/Es/ (EsStrings). Each of those is in turn split by UI area (Common.cs,
/// SettingsAlerts.cs, EventTypes.cs, etc.), so no single file grows without bound either as more UI
/// text is added within a language, or as more languages are added.
///
/// To add a new language:
///   1. Add it to the AppLanguage enum (AppLanguage.cs).
///   2. Create a new folder here, e.g. Services/Localization/Fr/, with an FrStrings.cs facade and one
///      FrStrings.&lt;Section&gt;.cs per UI area -- copy the shape of the En/ or Es/ folder.
///   3. Register FrStrings.Map in Tables below.
/// None of the existing En/*.cs or Es/*.cs files need to change. Sections you haven't translated yet
/// simply fall back to English (see Get below) until you fill them in.
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
    /// Two plural buckets ("one" vs. "other") are enough to cover every language this app
    /// currently ships (see GetPluralCategory). If a language needing a richer CLDR plural rule
    /// (Polish, Arabic, ...) is ever added, extend this enum and GetPluralCategory together --
    /// existing "_One"/"_Other" keys keep working unchanged.
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
    /// language tables (see EventMessages.cs / StreamlabsMessages.cs for examples), picks between
    /// them based on <paramref name="count"/>, and formats the chosen template with
    /// <paramref name="formatArgs"/> (so pass count again in there if the template uses it -- the
    /// two are separate because some templates need the count plus other values, e.g. "{0} gifts
    /// from {1}"). Reuses Get for the lookup, so a language missing one of the two suffixed keys
    /// falls back to English for just that count, not the whole key. Languages that never need
    /// "_One" (日本語, 简体中文) can skip writing it entirely.
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
    /// Debug-time sanity check: any key present in English but missing from another language's
    /// table gets logged once at startup (via DebugLog), so an incomplete translation is caught
    /// early instead of silently falling back to English forever without anyone noticing.
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
