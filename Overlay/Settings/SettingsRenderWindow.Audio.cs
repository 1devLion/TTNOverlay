using TTNOverlay.Native;
using TTNOverlay.Overlay.Controls;
using TTNOverlay.Services;
using Vortice.Direct2D1;
using Rect = Vortice.Mathematics.Rect;

namespace TTNOverlay.Overlay;

/// <summary>
/// SettingsRenderWindow partial: the Audio section (output device, message/event sound presets and volume).
/// </summary>
internal sealed partial class SettingsRenderWindow
{

    private readonly Dropdown _audioDeviceDropdown = new();
    private readonly Dropdown _messageSoundPresetDropdown = new();
    private readonly Dropdown _eventSoundPresetDropdown = new();
    private readonly Slider _messageVolumeSlider = new();
    private readonly Slider _eventVolumeSlider = new();

    private IReadOnlyList<(int Id, string Name)> _audioOutputDevices = Array.Empty<(int, string)>();
    private List<(string Name, string FullPath)> _soundPresets = new();

    private bool _enableMessageAlert;
    private string _messageSoundPath = "";
    private bool _enableEventAlert;
    private string _eventSoundPath = "";
    private int _alertOutputDeviceId = -1;

    private bool _originalEnableMessageAlert;
    private string _originalMessageSoundPath = "";
    private bool _originalEnableEventAlert;
    private string _originalEventSoundPath = "";
    private int _originalAlertOutputDeviceId;
    private float _originalMessageAlertVolume;
    private float _originalEventAlertVolume;

    private Rect _audioDeviceFieldRect;
    private Rect _messageSoundPresetFieldRect;
    private Rect _messageSoundBrowseButtonRect;
    private Rect _messageSoundTestButtonRect;
    private Rect _messageVolumeSliderRect;
    private Rect _eventSoundPresetFieldRect;
    private Rect _eventSoundBrowseButtonRect;
    private Rect _eventSoundTestButtonRect;
    private Rect _eventVolumeSliderRect;

    private void InitAudio()
    {

        _audioOutputDevices = AlertService.GetOutputDevices();
        _soundPresets = SoundHelper.GetAvailableSounds();

        _alertOutputDeviceId = Settings.AlertOutputDeviceId;

        AlertService.SetOutputDevice(_alertOutputDeviceId);
        _enableMessageAlert = Settings.EnableMessageAlert;
        _messageSoundPath = Settings.MessageSoundPath ?? "";
        _enableEventAlert = Settings.EnableEventAlert;
        _eventSoundPath = Settings.EventSoundPath ?? "";

        _messageVolumeSlider.SetValue(Settings.MessageAlertVolume);
        _eventVolumeSlider.SetValue(Settings.EventAlertVolume);
        _messageVolumeSlider.ValueChanged += v =>
        {
            Settings.MessageAlertVolume = v;
            AlertService.SetVolume("message", v);
        };
        _eventVolumeSlider.ValueChanged += v =>
        {
            Settings.EventAlertVolume = v;
            AlertService.SetVolume("event", v);
        };

        _originalAlertOutputDeviceId = _alertOutputDeviceId;
        _originalEnableMessageAlert = _enableMessageAlert;
        _originalMessageSoundPath = _messageSoundPath;
        _originalEnableEventAlert = _enableEventAlert;
        _originalEventSoundPath = _eventSoundPath;
        _originalMessageAlertVolume = _messageVolumeSlider.Value;
        _originalEventAlertVolume = _eventVolumeSlider.Value;
    }

    private void RevertAudio()
    {
        Settings.AlertOutputDeviceId = _originalAlertOutputDeviceId;
        AlertService.SetOutputDevice(_originalAlertOutputDeviceId);
        Settings.EnableMessageAlert = _originalEnableMessageAlert;
        Settings.MessageSoundPath = _originalMessageSoundPath;
        Settings.EnableEventAlert = _originalEnableEventAlert;
        Settings.EventSoundPath = _originalEventSoundPath;
        Settings.MessageAlertVolume = _originalMessageAlertVolume;
        Settings.EventAlertVolume = _originalEventAlertVolume;
        AlertService.SetVolume("message", _originalMessageAlertVolume);
        AlertService.SetVolume("event", _originalEventAlertVolume);
    }

    private void DrawAudioSection(ID2D1DCRenderTarget target, float x, float width)
    {
        float y = TitleBarHeight + Padding;

        using (var header = DWriteFactory.CreateTextLayout(LocalizationService.T("Settings_Section_Audio"), _headerFormat!, width, 24f))
            target.DrawTextLayout(new System.Numerics.Vector2(x, y), header, _textBrush!);
        y += 32f;

        y = DrawDropdownField(target, x, width, ref y, "Settings_Audio_OutputDevice", _audioDeviceDropdown, AudioDeviceLabel(_alertOutputDeviceId), out _audioDeviceFieldRect);
        y += FieldGap;

        _checkboxRects.Clear();

        y = DrawCheckboxField(target, x, width, y, "Settings_Alerts_MessageSound", _enableMessageAlert, "EnableMessageAlert");
        y = DrawDropdownField(target, x, width, ref y, "Settings_Alerts_PresetSound", _messageSoundPresetDropdown, SoundPresetLabel(_messageSoundPath), out _messageSoundPresetFieldRect);
        y = DrawSoundPathRow(target, x, width, y, _messageSoundPath, out _messageSoundBrowseButtonRect, out _messageSoundTestButtonRect, _enableMessageAlert);
        y = DrawVolumeSlider(target, x, width, y, "Settings_Audio_MessageVolume", _messageVolumeSlider, _enableMessageAlert, out _messageVolumeSliderRect);
        y += FieldGap;

        y = DrawCheckboxField(target, x, width, y, "Settings_Alerts_EventSound", _enableEventAlert, "EnableEventAlert");
        y = DrawDropdownField(target, x, width, ref y, "Settings_Alerts_PresetSound", _eventSoundPresetDropdown, SoundPresetLabel(_eventSoundPath), out _eventSoundPresetFieldRect);
        y = DrawSoundPathRow(target, x, width, y, _eventSoundPath, out _eventSoundBrowseButtonRect, out _eventSoundTestButtonRect, _enableEventAlert);
        DrawVolumeSlider(target, x, width, y, "Settings_Audio_EventVolume", _eventVolumeSlider, _enableEventAlert, out _eventVolumeSliderRect);
    }

    private const float SoundBrowseButtonWidth = 32f;
    private const float SoundTestButtonWidth = 72f;

    private float DrawSoundPathRow(ID2D1DCRenderTarget target, float x, float width, float y, string path, out Rect browseButtonRect, out Rect testButtonRect, bool enabled)
    {
        using (var label = DWriteFactory.CreateTextLayout(LocalizationService.T("Settings_Alerts_CustomSound"), _labelFormat!, width, 18f))
            target.DrawTextLayout(new System.Numerics.Vector2(x, y), label, _secondaryBrush!);
        y += 18f + LabelGap;

        testButtonRect = new Rect(x + width - SoundTestButtonWidth, y, SoundTestButtonWidth, FieldHeight);
        browseButtonRect = new Rect(testButtonRect.Left - FooterButtonGap - SoundBrowseButtonWidth, y, SoundBrowseButtonWidth, FieldHeight);
        float pathWidth = browseButtonRect.Left - x - FieldGap;
        var pathRect = new Rect(x, y, System.Math.Max(1f, pathWidth), FieldHeight);

        target.FillRectangle(pathRect, _fieldBackgroundBrush!);
        target.DrawRectangle(pathRect, _fieldBorderBrush!, 1f);
        target.PushAxisAlignedClip(pathRect, AntialiasMode.PerPrimitive);
        using (var pathLayout = DWriteFactory.CreateTextLayout(path, _fieldFormat!, pathRect.Width - 16f, pathRect.Height))
            target.DrawTextLayout(new System.Numerics.Vector2(pathRect.Left + 8f, pathRect.Top), pathLayout, enabled ? _secondaryBrush! : _fieldBorderBrush!);
        target.PopAxisAlignedClip();

        DrawFooterButton(target, browseButtonRect, "...", primary: false, enabled: enabled);
        DrawFooterButton(target, testButtonRect, LocalizationService.T("Settings_Alerts_Test"), primary: false, enabled: enabled && !string.IsNullOrWhiteSpace(path));

        return y + FieldHeight + FieldGap;
    }

    private const float SliderTrackHeight = 4f;
    private const float SliderHandleRadius = 7f;

    private float DrawVolumeSlider(ID2D1DCRenderTarget target, float x, float width, float y, string labelKey, Slider slider, bool enabled, out Rect bounds)
    {
        using (var label = DWriteFactory.CreateTextLayout(LocalizationService.T(labelKey) + $"{slider.Value:P0}", _labelFormat!, width, 18f))
            target.DrawTextLayout(new System.Numerics.Vector2(x, y), label, _secondaryBrush!);
        y += 18f + LabelGap;

        bounds = new Rect(x + SliderHandleRadius, y, System.Math.Max(1f, width - SliderHandleRadius * 2f), FieldHeight);

        float trackY = bounds.Top + bounds.Height / 2f - SliderTrackHeight / 2f;
        var track = new Rect(bounds.Left, trackY, bounds.Width, SliderTrackHeight);
        var filled = new Rect(bounds.Left, trackY, bounds.Width * slider.NormalizedPosition, SliderTrackHeight);
        var fillBrush = enabled ? _checkboxBrush! : _fieldBorderBrush!;

        target.FillRectangle(track, _fieldBorderBrush!);
        if (enabled)
            target.FillRectangle(filled, fillBrush);

        float handleCx = bounds.Left + bounds.Width * slider.NormalizedPosition;
        float handleCy = bounds.Top + bounds.Height / 2f;
        target.FillEllipse(new Ellipse(new System.Numerics.Vector2(handleCx, handleCy), SliderHandleRadius, SliderHandleRadius), fillBrush);

        return y + FieldHeight + FieldGap;
    }

    private string AudioDeviceLabel(int deviceId)
    {
        foreach (var (id, name) in _audioOutputDevices)
        {
            if (id == deviceId)
                return name;
        }

        return _audioOutputDevices.Count > 0 ? _audioOutputDevices[0].Name : "";
    }

    private string SoundPresetLabel(string currentPath)
    {
        foreach (var preset in _soundPresets)
        {
            if (preset.FullPath == currentPath)
                return preset.Name;
        }

        return LocalizationService.T("MainWindow_Custom");
    }

    private void OpenAudioDeviceDropdown()
    {
        Win32.GetClientRect(Hwnd, out var client);
        var items = new List<Dropdown.Item>();
        foreach (var (id, name) in _audioOutputDevices)
        {
            items.Add(new Dropdown.Item
            {
                Label = name,
                OnSelect = () =>
                {
                    _alertOutputDeviceId = id;
                    Settings.AlertOutputDeviceId = id;
                    AlertService.SetOutputDevice(id);
                },
            });
        }

        _audioDeviceDropdown.Open(_audioDeviceFieldRect.Left, _audioDeviceFieldRect.Bottom, client.Right - client.Left, client.Bottom - client.Top, items, _fieldFormat!);
        RequestRender();
    }

    private void OpenSoundPresetDropdown(bool isEvent)
    {
        Win32.GetClientRect(Hwnd, out var client);
        var dropdown = isEvent ? _eventSoundPresetDropdown : _messageSoundPresetDropdown;
        var anchorRect = isEvent ? _eventSoundPresetFieldRect : _messageSoundPresetFieldRect;
        var currentPath = isEvent ? _eventSoundPath : _messageSoundPath;

        var items = new List<Dropdown.Item>();
        foreach (var preset in _soundPresets)
        {
            var path = preset.FullPath;
            items.Add(new Dropdown.Item
            {
                Label = preset.Name,
                OnSelect = () =>
                {
                    if (isEvent) { _eventSoundPath = path; Settings.EventSoundPath = path; }
                    else { _messageSoundPath = path; Settings.MessageSoundPath = path; }
                },
            });
        }

        bool isCustomSound = !string.IsNullOrWhiteSpace(currentPath)
            && !_soundPresets.Any(p => p.FullPath == currentPath);
        if (isCustomSound)
            items.Add(new Dropdown.Item { Label = LocalizationService.T("MainWindow_Custom"), OnSelect = () => { } });

        dropdown.Open(anchorRect.Left, anchorRect.Bottom, client.Right - client.Left, client.Bottom - client.Top, items, _fieldFormat!);
        RequestRender();
    }

    private void BrowseSound(bool isEvent)
    {
        var path = FileDialog.PickWavFile(Hwnd);
        if (path is null)
            return;

        if (isEvent)
        {
            _eventSoundPath = path;
            Settings.EventSoundPath = path;
        }
        else
        {
            _messageSoundPath = path;
            Settings.MessageSoundPath = path;
        }

        RequestRender();
    }

    private static void TestSound(string path, float volume)
    {
        if (string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path))
            return;

        AlertService.PrepareAlert("test", path);
        AlertService.SetVolume("test", volume);
        AlertService.PlaySound("test");
    }

    private void HandleAudioSectionClick(int clientX, int clientY)
    {
        if (Contains(_audioDeviceFieldRect, clientX, clientY))
        {
            OpenAudioDeviceDropdown();
            return;
        }

        foreach (var (bounds, field) in _checkboxRects)
        {
            if (Contains(bounds, clientX, clientY))
            {
                ToggleCheckbox(field);
                RequestRender();
                return;
            }
        }

        if (_enableMessageAlert && Contains(_messageSoundPresetFieldRect, clientX, clientY))
        {
            OpenSoundPresetDropdown(isEvent: false);
            return;
        }
        if (_enableEventAlert && Contains(_eventSoundPresetFieldRect, clientX, clientY))
        {
            OpenSoundPresetDropdown(isEvent: true);
            return;
        }
        if (_enableMessageAlert && Contains(_messageSoundBrowseButtonRect, clientX, clientY))
        {
            BrowseSound(isEvent: false);
            return;
        }
        if (_enableEventAlert && Contains(_eventSoundBrowseButtonRect, clientX, clientY))
        {
            BrowseSound(isEvent: true);
            return;
        }
        if (_enableMessageAlert && !string.IsNullOrWhiteSpace(_messageSoundPath) && Contains(_messageSoundTestButtonRect, clientX, clientY))
        {
            TestSound(_messageSoundPath, _messageVolumeSlider.Value);
            return;
        }
        if (_enableEventAlert && !string.IsNullOrWhiteSpace(_eventSoundPath) && Contains(_eventSoundTestButtonRect, clientX, clientY))
        {
            TestSound(_eventSoundPath, _eventVolumeSlider.Value);
        }
    }
}