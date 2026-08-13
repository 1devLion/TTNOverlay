using TTNOverlay.Native;
using TTNOverlay.Overlay.Controls;
using TTNOverlay.Services;
using Vortice.Direct2D1;
using Vortice.DirectWrite;
using Vortice.Mathematics;
using Rect = Vortice.Mathematics.Rect;

namespace TTNOverlay.Overlay;

/// <summary>
/// SettingsRenderWindow partial: the General section (channel, font size, message limits, appearance).
/// </summary>
internal sealed partial class SettingsRenderWindow
{

    private readonly Dropdown _themeDropdown = new();
    private readonly Dropdown _languageDropdown = new();
    private readonly TextBox _channelBox = new();
    private readonly TextBox _fontSizeBox = new() { MaxLength = 4 };
    private readonly TextBox _timeoutBox = new() { MaxLength = 7 };
    private readonly TextBox _maxMessagesBox = new() { MaxLength = 5 };
    private bool _clickThrough;
    private bool _debugMode;
    private bool _thirdPartyEmotes;
    private bool _eventsPanel;
    private bool _moderationPanel;
    private bool _highQualityMedia;

    private string _originalTheme = "";
    private string _originalLanguage = "";
    private string _originalChannel = "";
    private double _originalFontSize;
    private int _originalTimeoutSeconds;
    private int _originalMaxMessages;
    private bool _originalClickThrough;
    private bool _originalDebugMode;
    private bool _originalThirdPartyEmotes;
    private bool _originalEventsPanel;
    private bool _originalModerationPanel;
    private bool _originalHighQualityMedia;
    private Rect _themeFieldRect;
    private Rect _languageFieldRect;
    private Rect _channelFieldRect;
    private Rect _fontSizeFieldRect;
    private Rect _timeoutFieldRect;
    private Rect _maxMessagesFieldRect;
    private readonly List<(Rect Bounds, string Field)> _checkboxRects = new();

    private void InitGeneral()
    {

        _channelBox.Text = Settings.Channel ?? "";
        _fontSizeBox.Text = Settings.FontSize.ToString();
        _timeoutBox.Text = Settings.MessageTimeoutSeconds.ToString();
        _maxMessagesBox.Text = Settings.MaxMessages.ToString();
        _clickThrough = Settings.ClickThrough;
        _debugMode = Settings.EnableDebugMode;
        _thirdPartyEmotes = Settings.EnableThirdPartyEmotes;
        _eventsPanel = Settings.EnableEventsPanel;
        _moderationPanel = Settings.EnableModerationPanel;
        _highQualityMedia = Settings.HighQualityMedia;

        _originalTheme = Settings.Theme;
        _originalLanguage = Settings.Language;
        _originalChannel = Settings.Channel ?? "";
        _originalFontSize = Settings.FontSize;
        _originalTimeoutSeconds = Settings.MessageTimeoutSeconds;
        _originalMaxMessages = Settings.MaxMessages;
        _originalClickThrough = _clickThrough;
        _originalDebugMode = _debugMode;
        _originalThirdPartyEmotes = _thirdPartyEmotes;
        _originalEventsPanel = _eventsPanel;
        _originalModerationPanel = _moderationPanel;
        _originalHighQualityMedia = _highQualityMedia;
    }

    private void RevertGeneral()
    {
        Settings.Theme = _originalTheme;
        Settings.Language = _originalLanguage;

        LocalizationService.Instance.SetLanguage(AppLanguageExtensions.FromSettingsLabel(_originalLanguage));
        Settings.Channel = _originalChannel;
        Settings.FontSize = _originalFontSize;
        Settings.MessageTimeoutSeconds = _originalTimeoutSeconds;
        Settings.MaxMessages = _originalMaxMessages;
        Settings.ClickThrough = _originalClickThrough;
        Settings.EnableDebugMode = _originalDebugMode;
        Settings.EnableThirdPartyEmotes = _originalThirdPartyEmotes;
        Settings.EnableEventsPanel = _originalEventsPanel;
        Settings.EnableModerationPanel = _originalModerationPanel;
        Settings.HighQualityMedia = _originalHighQualityMedia;
    }

    private void DrawGeneralSection(ID2D1DCRenderTarget target, float x, float width)
    {
        float y = TitleBarHeight + Padding;

        using (var header = DWriteFactory.CreateTextLayout(LocalizationService.T("Settings_Section_General"), _headerFormat!, width, 24f))
            target.DrawTextLayout(new System.Numerics.Vector2(x, y), header, _textBrush!);
        y += 32f;

        y = DrawDropdownField(target, x, width, ref y, "Settings_General_Theme", _themeDropdown, Settings.Theme, out _themeFieldRect);
        y = DrawDropdownField(target, x, width, ref y, "Settings_Language", _languageDropdown, Settings.Language, out _languageFieldRect);

        y = DrawTextField(target, x, width, y, "Settings_General_Channel", _channelBox, out _channelFieldRect, out _);
        y = DrawTextField(target, x, width, y, "Settings_General_FontSize", _fontSizeBox, out _fontSizeFieldRect, out _);
        y = DrawTextField(target, x, width, y, "Settings_General_MessageLifetime", _timeoutBox, out _timeoutFieldRect, out _, belowInfoKey: "Settings_General_MessageLifetimeInfo");
        y = DrawTextField(target, x, width, y, "Settings_General_MaxMessages", _maxMessagesBox, out _maxMessagesFieldRect, out _);

        _checkboxRects.Clear();
        y = DrawCheckboxField(target, x, width, y, "Settings_General_ClickThrough", _clickThrough, "ClickThrough");
        y = DrawCheckboxField(target, x, width, y, "Settings_General_DebugMode", _debugMode, "DebugMode");
        y = DrawCheckboxField(target, x, width, y, "Settings_General_ThirdPartyEmotes", _thirdPartyEmotes, "ThirdPartyEmotes");
        y = DrawCheckboxField(target, x, width, y, "Settings_General_EnableEventsPanel", _eventsPanel, "EventsPanel");
        y = DrawCheckboxField(target, x, width, y, "Settings_General_EnableModerationPanel", _moderationPanel, "ModerationPanel");
        DrawCheckboxField(target, x, width, y, "Settings_General_HighQualityMedia", _highQualityMedia, "HighQualityMedia");
    }

    private void OpenThemeDropdown()
    {
        Win32.GetClientRect(Hwnd, out var client);
        _themeDropdown.Open(_themeFieldRect.Left, _themeFieldRect.Bottom, client.Right - client.Left, client.Bottom - client.Top,
            new List<Dropdown.Item>
            {
                new() { Label = "Dark", OnSelect = () => { Settings.Theme = "Dark"; ThemeService.Apply("Dark"); } },
                new() { Label = "Light", OnSelect = () => { Settings.Theme = "Light"; ThemeService.Apply("Light"); } },
            },
            _fieldFormat!);
        RequestRender();
    }

    private void OpenLanguageDropdown()
    {
        Win32.GetClientRect(Hwnd, out var client);
        _languageDropdown.Open(_languageFieldRect.Left, _languageFieldRect.Bottom, client.Right - client.Left, client.Bottom - client.Top,
            new List<Dropdown.Item>
            {

                new() { Label = "English", OnSelect = () => { Settings.Language = "English"; LocalizationService.Instance.SetLanguage(AppLanguage.English); } },
                new() { Label = "Deutsch", OnSelect = () => { Settings.Language = "Deutsch"; LocalizationService.Instance.SetLanguage(AppLanguage.Deutsch); } },
                new() { Label = "French", OnSelect = () => { Settings.Language = "French"; LocalizationService.Instance.SetLanguage(AppLanguage.French); } },
                new() { Label = "日本語", OnSelect = () => { Settings.Language = "日本語"; LocalizationService.Instance.SetLanguage(AppLanguage.日本語); } },
                new() { Label = "Portuguese", OnSelect = () => { Settings.Language = "Portuguese"; LocalizationService.Instance.SetLanguage(AppLanguage.Portuguese); } },
                new() { Label = "Русский", OnSelect = () => { Settings.Language = "Русский"; LocalizationService.Instance.SetLanguage(AppLanguage.Русский); } },
                new() { Label = "Spanish", OnSelect = () => { Settings.Language = "Spanish"; LocalizationService.Instance.SetLanguage(AppLanguage.Spanish); } },
                new() { Label = "简体中文", OnSelect = () => { Settings.Language = "简体中文"; LocalizationService.Instance.SetLanguage(AppLanguage.简体中文); } },

            },
            _fieldFormat!);
        RequestRender();
    }

}