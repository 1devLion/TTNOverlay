using TTNOverlay.Native;
using TTNOverlay.Overlay.Controls;
using TTNOverlay.Services;
using Vortice.Direct2D1;
using Vortice.DirectWrite;
using Vortice.Mathematics;
using Rect = Vortice.Mathematics.Rect;

namespace TTNOverlay.Overlay;

/// <summary>
/// SettingsRenderWindow partial: the Streamlabs section (socket token entry and validation).
/// </summary>
internal sealed partial class SettingsRenderWindow
{

    private readonly TextBox _streamlabsSocketTokenBox = new() { IsPassword = true };
    private readonly TextBox _streamlabsWidgetTokenBox = new() { IsPassword = true };
    private readonly Dropdown _eventAlertSourceDropdown = new();
    private bool _enableStreamlabsEvents;
    private string _eventAlertSource = "Both";
    private bool _originalEnableStreamlabsEvents;
    private string _originalStreamlabsSocketToken = "";
    private string _originalStreamlabsWidgetToken = "";
    private string _originalEventAlertSource = "";
    private Rect _streamlabsSocketTokenFieldRect;
    private Rect _streamlabsWidgetTokenFieldRect;
    private Rect _eventAlertSourceFieldRect;

    private Rect _streamlabsSocketTokenRevealRect;
    private Rect _streamlabsWidgetTokenRevealRect;

    private void InitStreamlabs()
    {
        _streamlabsSocketTokenBox.Text = Settings.StreamlabsSocketToken ?? "";
        _streamlabsWidgetTokenBox.Text = Settings.StreamlabsWidgetToken ?? "";
        _enableStreamlabsEvents = Settings.EnableStreamlabsEvents;

        _eventAlertSource = Settings.EventAlertSource is "Both" or "IrcOnly" or "StreamlabsOnly"
            ? Settings.EventAlertSource
            : "Both";

        _originalEnableStreamlabsEvents = _enableStreamlabsEvents;
        _originalStreamlabsSocketToken = _streamlabsSocketTokenBox.Text;
        _originalStreamlabsWidgetToken = _streamlabsWidgetTokenBox.Text;
        _originalEventAlertSource = _eventAlertSource;
    }

    private void RevertStreamlabs()
    {
        Settings.EnableStreamlabsEvents = _originalEnableStreamlabsEvents;
        Settings.StreamlabsSocketToken = _originalStreamlabsSocketToken;
        Settings.StreamlabsWidgetToken = _originalStreamlabsWidgetToken;
        Settings.EventAlertSource = _originalEventAlertSource;
    }

    private void DrawStreamlabsSection(ID2D1DCRenderTarget target, float x, float width)
    {
        float y = TitleBarHeight + Padding;

        using (var header = DWriteFactory.CreateTextLayout(LocalizationService.T("Settings_Streamlabs_Header"), _headerFormat!, width, 24f))
            target.DrawTextLayout(new System.Numerics.Vector2(x, y), header, _textBrush!);
        y += 28f;

        using (var info = DWriteFactory.CreateTextLayout(LocalizationService.T("Settings_Streamlabs_Info"), _labelFormat!, width, 32f))
            target.DrawTextLayout(new System.Numerics.Vector2(x, y), info, _secondaryBrush!);
        y += 40f;

        _checkboxRects.Clear();
        y = DrawCheckboxField(target, x, width, y, "Settings_Streamlabs_Enable", _enableStreamlabsEvents, "EnableStreamlabsEvents");
        y += FieldGap;

        y = DrawTextField(target, x, width, y, "Settings_Streamlabs_SocketToken", _streamlabsSocketTokenBox, out _streamlabsSocketTokenFieldRect, out _streamlabsSocketTokenRevealRect, passwordReveal: true, enabled: _enableStreamlabsEvents);
        y = DrawTextField(target, x, width, y, "Settings_Streamlabs_WidgetToken", _streamlabsWidgetTokenBox, out _streamlabsWidgetTokenFieldRect, out _streamlabsWidgetTokenRevealRect, infoKey: "Settings_Streamlabs_WidgetTokenInfo", passwordReveal: true, enabled: _enableStreamlabsEvents);

        y = DrawDropdownField(target, x, width, ref y, "Settings_Streamlabs_SourceLabel", _eventAlertSourceDropdown, EventAlertSourceLabel(_eventAlertSource), out _eventAlertSourceFieldRect);

        using var sourceInfo = DWriteFactory.CreateTextLayout(LocalizationService.T("Settings_Streamlabs_SourceInfo"), _labelFormat!, width, 28f);
        target.DrawTextLayout(new System.Numerics.Vector2(x, y), sourceInfo, _secondaryBrush!);
    }

    private static string EventAlertSourceLabel(string value) => value switch
    {
        "IrcOnly" => LocalizationService.T("Settings_Streamlabs_SourceIrcOnly"),
        "StreamlabsOnly" => LocalizationService.T("Settings_Streamlabs_SourceStreamlabsOnly"),
        _ => LocalizationService.T("Settings_Streamlabs_SourceBoth"),
    };
    private void OpenEventAlertSourceDropdown()
    {
        Win32.GetClientRect(Hwnd, out var client);
        _eventAlertSourceDropdown.Open(_eventAlertSourceFieldRect.Left, _eventAlertSourceFieldRect.Bottom, client.Right - client.Left, client.Bottom - client.Top,
            new List<Dropdown.Item>
            {
                new() { Label = LocalizationService.T("Settings_Streamlabs_SourceBoth"), OnSelect = () => { _eventAlertSource = "Both"; Settings.EventAlertSource = "Both"; } },
                new() { Label = LocalizationService.T("Settings_Streamlabs_SourceIrcOnly"), OnSelect = () => { _eventAlertSource = "IrcOnly"; Settings.EventAlertSource = "IrcOnly"; } },
                new() { Label = LocalizationService.T("Settings_Streamlabs_SourceStreamlabsOnly"), OnSelect = () => { _eventAlertSource = "StreamlabsOnly"; Settings.EventAlertSource = "StreamlabsOnly"; } },
            },
            _fieldFormat!);
        RequestRender();
    }

    private static string NormalizeStreamlabsToken(string input)
    {
        var text = input?.Trim() ?? string.Empty;
        if (text.Length == 0)
            return text;

        if (!text.Contains("://") && !text.Contains('/'))
            return text;

        text = text.TrimEnd('/');
        var slashIdx = text.LastIndexOf('/');
        return slashIdx >= 0 && slashIdx < text.Length - 1 ? text[(slashIdx + 1)..] : text;
    }
    private void HandleStreamlabsSectionClick(int clientX, int clientY)
    {
        foreach (var (bounds, field) in _checkboxRects)
        {
            if (Contains(bounds, clientX, clientY))
            {
                ToggleCheckbox(field);
                RequestRender();
                return;
            }
        }

        if (Contains(_eventAlertSourceFieldRect, clientX, clientY))
        {
            OpenEventAlertSourceDropdown();
        }
    }
}

