namespace TTNOverlay.Services;

/// <summary>
/// Tracks the active UI language and raises a change notification when it's switched.
/// </summary>
public sealed class LocalizationService
{
    public static readonly LocalizationService Instance = new();

    public event Action? LanguageChanged;

    public AppLanguage CurrentLanguage { get; private set; } = AppLanguage.English;

    private LocalizationService() { }

    public void SetLanguage(AppLanguage language)
    {
        if (CurrentLanguage == language)
            return;
        CurrentLanguage = language;
        LanguageChanged?.Invoke();
    }

    public string this[string key] => Strings.Get(key, CurrentLanguage);

    public static string T(string key) => Instance[key];
}
