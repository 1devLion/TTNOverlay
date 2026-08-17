using TTNOverlay.Native;
using TTNOverlay.Overlay.Controls;
using TTNOverlay.Services;
using Vortice.Direct2D1;
using Rect = Vortice.Mathematics.Rect;

namespace TTNOverlay.Overlay;

/// <summary>
/// Partial implementation of SettingsRenderWindow for the General section.
/// Handles channel, font size, message limits, appearance, and chat source settings.
/// </summary>
internal sealed partial class SettingsRenderWindow
{
    private readonly Dropdown _themeDropdown = new();
    private readonly Dropdown _languageDropdown = new();
    private readonly Dropdown _chatSourceDropdown = new();
    private readonly TextBox _channelBox = new();
    private readonly TextBox _kickChannelBox = new();
    private readonly TextBox _fontSizeBox = new() { MaxLength = 4 };
    private readonly TextBox _timeoutBox = new() { MaxLength = 7 };
    private readonly TextBox _maxMessagesBox = new() { MaxLength = 5 };
    private string _chatSourceMode = "Twitch";
    private bool _multichatUseSameChannel;
    private bool _multichatTwitchEnabled;
    private bool _multichatKickEnabled;
    private bool _clickThrough;
    private bool _debugMode;
    private bool _thirdPartyEmotes;
    private bool _eventsPanel;
    private bool _moderationPanel;
    private bool _highQualityMedia;

    private string _originalTheme = "";
    private string _originalLanguage = "";
    private string _originalChannel = "";
    private string _originalKickChannel = "";
    private string _originalChatSourceMode = "Twitch";
    private bool _originalMultichatUseSameChannel;
    private bool _originalMultichatTwitchEnabled;
    private bool _originalMultichatKickEnabled;
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
    private Rect _chatSourceFieldRect;
    private Rect _channelFieldRect;
    private Rect _kickChannelFieldRect;
    private Rect _fontSizeFieldRect;
    private Rect _timeoutFieldRect;
    private Rect _maxMessagesFieldRect;
    private readonly List<(Rect Bounds, string Field)> _checkboxRects = new();

    private ScrollState _generalScroll;

    private void InitGeneral()
    {
        _channelBox.Text = Settings.Channel ?? "";
        _kickChannelBox.Text = Settings.KickChannel ?? "";
        _chatSourceMode = Settings.ChatSourceMode;
        _multichatUseSameChannel = Settings.MultichatUseSameChannel;
        _multichatTwitchEnabled = Settings.MultichatTwitchEnabled;
        _multichatKickEnabled = Settings.MultichatKickEnabled;
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
        _originalKickChannel = Settings.KickChannel ?? "";
        _originalChatSourceMode = _chatSourceMode;
        _originalMultichatUseSameChannel = _multichatUseSameChannel;
        _originalMultichatTwitchEnabled = _multichatTwitchEnabled;
        _originalMultichatKickEnabled = _multichatKickEnabled;
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
        Settings.KickChannel = _originalKickChannel;
        Settings.ChatSourceMode = _originalChatSourceMode;
        Settings.MultichatUseSameChannel = _originalMultichatUseSameChannel;
        Settings.MultichatTwitchEnabled = _originalMultichatTwitchEnabled;
        Settings.MultichatKickEnabled = _originalMultichatKickEnabled;
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

    /// <summary>
    /// Draws the General section with scrolling support.
    /// </summary>
    private void DrawGeneralSection(ID2D1DCRenderTarget target, float x, float width, float winHeight)
    {
        float viewportTop = TitleBarHeight;
        float viewportHeight = System.Math.Max(0f, winHeight - FooterHeight - viewportTop);

        float totalHeight = MeasureGeneralContentHeight();
        _generalScroll.RecomputeOverflow(totalHeight, viewportHeight);

        _checkboxRects.Clear();

        var viewportRect = new Rect(x, viewportTop, width, viewportHeight);
        target.PushAxisAlignedClip(viewportRect, AntialiasMode.PerPrimitive);
        DrawGeneralContent(target, x, width, viewportTop + Padding - _generalScroll.Offset);
        target.PopAxisAlignedClip();

        ScrollbarRenderer.Draw(target, viewportRect, _generalScroll, _scrollbarTrackBrush!, _scrollbarThumbBrush!);
    }

    private void DrawGeneralContent(ID2D1DCRenderTarget target, float x, float width, float y)
    {
        using (var header = DWriteFactory.CreateTextLayout(LocalizationService.T("Settings_Section_General"), _headerFormat!, width, 24f))
            target.DrawTextLayout(new System.Numerics.Vector2(x, y), header, _textBrush!);
        y += 32f;

        y = DrawDropdownField(target, x, width, ref y, "Settings_General_Theme", _themeDropdown, Settings.Theme, out _themeFieldRect);
        y = DrawDropdownField(target, x, width, ref y, "Settings_Language", _languageDropdown, Settings.Language, out _languageFieldRect);
        y = DrawDropdownField(target, x, width, ref y, "Settings_General_ChatSource", _chatSourceDropdown, ChatSourceLabel(_chatSourceMode), out _chatSourceFieldRect);

        y = DrawChatSourceFields(target, x, width, y);

        y = DrawTextField(target, x, width, y, "Settings_General_FontSize", _fontSizeBox, out _fontSizeFieldRect, out _);
        y = DrawTextField(target, x, width, y, "Settings_General_MessageLifetime", _timeoutBox, out _timeoutFieldRect, out _, belowInfoKey: "Settings_General_MessageLifetimeInfo");
        y = DrawTextField(target, x, width, y, "Settings_General_MaxMessages", _maxMessagesBox, out _maxMessagesFieldRect, out _);

        y = DrawCheckboxField(target, x, width, y, "Settings_General_ClickThrough", _clickThrough, "ClickThrough");
        y = DrawCheckboxField(target, x, width, y, "Settings_General_ThirdPartyEmotes", _thirdPartyEmotes, "ThirdPartyEmotes");
        y = DrawCheckboxField(target, x, width, y, "Settings_General_EnableEventsPanel", _eventsPanel, "EventsPanel");
        y = DrawCheckboxField(target, x, width, y, "Settings_General_EnableModerationPanel", _moderationPanel, "ModerationPanel");
        DrawCheckboxField(target, x, width, y, "Settings_General_HighQualityMedia", _highQualityMedia, "HighQualityMedia");
    }

    /// <summary>
    /// Calculates the total height of the General section content.
    /// </summary>
    private float MeasureGeneralContentHeight()
    {
        const float dropdownField = 18f + LabelGap + FieldHeight + FieldGap;
        const float textField = 18f + LabelGap + FieldHeight + FieldGap;
        const float textFieldWithBelowInfo = textField + 28f + 4f;
        const float checkboxField = CheckboxSize + FieldGap;

        float h = Padding;
        h += 32f; // header
        h += dropdownField * 3; // Theme, Language, ChatSource

        h += MeasureChatSourceFieldsHeight();

        h += textField; // FontSize
        h += textFieldWithBelowInfo; // MessageLifetime (+ info line)
        h += textField; // MaxMessages

        h += checkboxField * 5; // ClickThrough, ThirdPartyEmotes, EventsPanel, ModerationPanel, HighQualityMedia

        return h;
    }

    /// <summary>
    /// Calculates the height contributed by the chat source fields.
    /// </summary>
    private float MeasureChatSourceFieldsHeight()
    {
        const float textField = 18f + LabelGap + FieldHeight + FieldGap;
        const float checkboxField = CheckboxSize + FieldGap;

        if (_chatSourceMode == "Kick" || _chatSourceMode != "Multichat")
            return textField; // single channel box (Kick or Twitch)

        if (_multichatUseSameChannel)
            return textField + checkboxField; // shared channel box + "use same channel" checkbox

        // Twitch channel + its enable checkbox, Kick channel + its enable checkbox, "use same channel"
        return textField + checkboxField + textField + checkboxField + checkboxField;
    }

    private static string ChatSourceLabel(string mode) => mode switch
    {
        "Kick" => LocalizationService.T("Settings_ChatSource_Kick"),
        "Multichat" => LocalizationService.T("Settings_ChatSource_Multichat"),
        _ => LocalizationService.T("Settings_ChatSource_Twitch"),
    };

    /// <summary>
    /// Draws channel input fields based on the selected chat source mode.
    /// </summary>
    private float DrawChatSourceFields(ID2D1DCRenderTarget target, float x, float width, float y)
    {
        if (_chatSourceMode == "Kick")
        {
            _channelFieldRect = default;
            return DrawTextField(target, x, width, y, "Settings_General_ChannelKick", _kickChannelBox, out _kickChannelFieldRect, out _);
        }

        if (_chatSourceMode != "Multichat")
        {
            // Twitch (default/original behavior).
            _kickChannelFieldRect = default;
            return DrawTextField(target, x, width, y, "Settings_General_Channel", _channelBox, out _channelFieldRect, out _);
        }

        if (_multichatUseSameChannel)
        {
            y = DrawTextField(target, x, width, y, "Settings_General_ChannelShared", _channelBox, out _channelFieldRect, out _);
            _kickChannelFieldRect = default;
        }
        else
        {
            y = DrawTextField(target, x, width, y, "Settings_General_Channel", _channelBox, out _channelFieldRect, out _);
            y = DrawCheckboxField(target, x, width, y, "Settings_General_MultichatEnableTwitch", _multichatTwitchEnabled, "MultichatTwitchEnabled");

            y = DrawTextField(target, x, width, y, "Settings_General_ChannelKick", _kickChannelBox, out _kickChannelFieldRect, out _);
            y = DrawCheckboxField(target, x, width, y, "Settings_General_MultichatEnableKick", _multichatKickEnabled, "MultichatKickEnabled");
        }

        return DrawCheckboxField(target, x, width, y, "Settings_General_MultichatUseSameChannel", _multichatUseSameChannel, "MultichatUseSameChannel");
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

    private void OpenChatSourceDropdown()
    {
        Win32.GetClientRect(Hwnd, out var client);
        _chatSourceDropdown.Open(_chatSourceFieldRect.Left, _chatSourceFieldRect.Bottom, client.Right - client.Left, client.Bottom - client.Top,
            new List<Dropdown.Item>
            {
                new() { Label = LocalizationService.T("Settings_ChatSource_Twitch"), OnSelect = () => SetChatSourceMode("Twitch") },
                new() { Label = LocalizationService.T("Settings_ChatSource_Kick"), OnSelect = () => SetChatSourceMode("Kick") },
                new() { Label = LocalizationService.T("Settings_ChatSource_Multichat"), OnSelect = () => SetChatSourceMode("Multichat") },
            },
            _fieldFormat!);
        RequestRender();
    }

    private void SetChatSourceMode(string mode)
    {
        _chatSourceMode = mode;
        Settings.ChatSourceMode = mode;
        BlurFocusedTextBox(); // Clear focus when switching modes.
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