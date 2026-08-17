using System.Text.Json;
using System.Text.Json.Serialization;

namespace TTNOverlay.Services;

/// <summary>
/// Application settings model (AppSettings) plus JSON load/save persistence.
/// </summary>
public class AppSettings
{
    public string Channel { get; set; } = "";

    /// <summary>"Twitch", "Kick", or "Multichat". Individual modes use Channel (Twitch) or
    /// KickChannel (Kick) as-is, same as before this setting existed. Multichat additionally uses
    /// MultichatTwitchEnabled/MultichatKickEnabled per-source, and MultichatUseSameChannel to decide
    /// whether Channel alone feeds every enabled source or each source keeps its own channel box.</summary>
    public string ChatSourceMode { get; set; } = "Twitch";

    public string KickChannel { get; set; } = "";

    public bool MultichatUseSameChannel { get; set; } = false;

    public bool MultichatTwitchEnabled { get; set; } = true;

    public bool MultichatKickEnabled { get; set; } = true;

    public double FontSize { get; set; } = 20;
    public int MessageTimeoutSeconds { get; set; } = 10;
    public int MaxMessages { get; set; } = 50;
    public bool ClickThrough { get; set; } = true;
    public string AlertFlashColor { get; set; } = "#66FFD700";
    public byte AlertFlashAlpha { get; set; } = 0x66;

    public bool EnableGlobalHotkeys { get; set; } = true;

    public uint EventsHotkeyModifiers { get; set; } = 0x0002 | 0x0004;
    public uint EventsHotkeyKey { get; set; } = 0x77;

    public uint ModerationHotkeyModifiers { get; set; } = 0x0002 | 0x0004;
    public uint ModerationHotkeyKey { get; set; } = 0x78;

    public uint BordersHotkeyModifiers { get; set; } = 0x0002 | 0x0004;
    public uint BordersHotkeyKey { get; set; } = 0x76;

    public double WindowLeft { get; set; } = 100;
    public double WindowTop { get; set; } = 100;
    public double WindowWidth { get; set; } = 420;
    public double WindowHeight { get; set; } = 600;
    public double ModerationWindowWidth { get; set; } = 420;
    public double ModerationWindowHeight { get; set; } = 520;
    public bool EnableMessageAlert { get; set; } = false;
    public string MessageSoundPath { get; set; } = "";
    public bool EnableEventAlert { get; set; } = false;
    public string EventSoundPath { get; set; } = "";
    public bool EnableVisualFlash { get; set; } = true;

    public Dictionary<string, string> EventBoxColorModes { get; set; } = new();

    public Dictionary<string, string> EventBoxColors { get; set; } = new();

    public bool EventBoxColorAdvancedMode { get; set; } = false;

    public string IrcEventGifPath { get; set; } = "";

    public Dictionary<string, string> IrcEventGifPaths { get; set; } = new();

    public bool IrcEventGifAdvancedMode { get; set; } = false;

    public bool ShowBadges { get; set; } = true;
    public bool EnableThirdPartyEmotes { get; set; } = true;

    public bool EnableEventsPanel { get; set; } = true;
    public bool EnableModerationPanel { get; set; } = true;

    public bool EnableIrcEventGif { get; set; } = true;

    public bool HighQualityMedia { get; set; } = false;

    public int AlertOutputDeviceId { get; set; } = -1;

    public float MessageAlertVolume { get; set; } = 1f;

    public float EventAlertVolume { get; set; } = 1f;

    public bool DisableAlertCooldown { get; set; } = false;

    public bool ShowViewerCount { get; set; } = true;
    public string ViewerCountBackgroundColor { get; set; } = "";
    public byte ViewerCountBackgroundAlpha { get; set; } = 0xAA;
    public string ViewerCountTextColor { get; set; } = "";
    public double ViewerCountSize { get; set; } = 13;

    public string StreamlabsSocketToken { get; set; } = "";
    public bool EnableStreamlabsEvents { get; set; } = false;
    public bool EnableTwitchApi { get; set; } = true;

    public string EventAlertSource { get; set; } = "Both";

    public string StreamlabsWidgetToken { get; set; } = "";

    public string Theme { get; set; } = "Dark";

    public string ModeratorRefreshToken { get; set; } = "";
    public string ModeratorLogin { get; set; } = "";
    public string ModeratorUserId { get; set; } = "";

    public bool EnableDebugMode { get; set; } = false;

    public string Language { get; set; } = "English";

    public AppSettings Clone()
    {
        var json = JsonSerializer.Serialize(this, SettingsJsonContext.Default.AppSettings);
        return JsonSerializer.Deserialize(json, SettingsJsonContext.Default.AppSettings)
            ?? new AppSettings();
    }
}

public static class SettingsService
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "TTNOverlay",
        "settings.json"
    );

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                var settings = JsonSerializer.Deserialize(json, SettingsJsonContext.Default.AppSettings);
                if (settings != null)
                    return settings;
            }
        }
        catch
        {

        }
        return new AppSettings();
    }

    public static void Save(AppSettings settings)
    {
        try
        {
            var dir = Path.GetDirectoryName(FilePath)!;
            Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(settings, SettingsJsonContext.Default.AppSettings);
            File.WriteAllText(FilePath, json);
        }
        catch
        {

        }
    }
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(AppSettings))]
internal partial class SettingsJsonContext : JsonSerializerContext { }