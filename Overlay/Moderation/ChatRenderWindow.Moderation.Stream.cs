using TTNOverlay.Overlay.Controls;
using TTNOverlay.Kick;
using TTNOverlay.Models;
using TTNOverlay.Native;
using TTNOverlay.Services;
using Rect = Vortice.Mathematics.Rect;

namespace TTNOverlay.Overlay;

/// <summary>
/// ChatRenderWindow partial: the parts of the moderation panel that go beyond moderating. The Kick account block of the
/// "Stream" tab (login / logout, so Kick has the same button Twitch has) and the message box of the main chat, which
/// sends a chat message as the logged-in moderator to Twitch or Kick.
/// </summary>
internal sealed partial class ChatRenderWindow
{
    // ------------------------------------------------------------------ Kick account (Stream tab)

    private Rect? _moderationKickActionRect;
    private bool _moderationKickButtonHovered;
    private bool _moderationKickLoginBusy;

    private async void HandleKickPanelLoginAction()
    {
        _kickSession ??= new KickSessionService(_settings);

        if (_kickSession.IsLoggedIn)
        {
            _kickSession.Logout();
            OnKickSessionChangedLocally();
            return;
        }

        if (_moderationKickLoginBusy)
            return;

        _moderationKickLoginBusy = true;
        RequestRender();

        bool ok = false;
        try
        {
            ok = await _kickSession.LoginAsync();
        }
        catch (Exception ex)
        {
            DebugLog.WriteException("ChatRenderWindow.HandleKickPanelLoginAction", ex);
        }

        PostToUiThread(() =>
        {
            _moderationKickLoginBusy = false;
            if (ok)
                OnKickSessionChangedLocally();
            RequestRender();
        });
    }

    /// <summary>
    /// The session service raises SessionChanged for the other windows but this window ignores its own event (see
    /// OnKickSessionChanged), so the same clean-up is done here after a login/logout made from the panel itself.
    /// </summary>
    private void OnKickSessionChangedLocally()
    {
        if (string.IsNullOrWhiteSpace(_settings.KickRefreshToken))
        {
            ClearModerationLog(Platform.Kick);
            _kickSanctions.Clear();
            if (_moderationSendPlatform == Platform.Kick)
                _moderationSendPlatform = Platform.Twitch;
            _moderationSendToBoth = false;
        }
        else
        {
            _ = RefreshKickGrantedScopesAsync();
        }

        EnsureModerationSources();
        RequestRender();
    }

    // ------------------------------------------------------------------ writing in chat (message box of the main chat)

    private const int ModerationInputMaxLength = 500; // Twitch and Kick both cap a chat message at 500 characters
    private const float ModerationInputRowHeight = 32f;

    private readonly Controls.TextBox _moderationInput = new() { MaxLength = ModerationInputMaxLength };
    private Rect? _moderationInputFieldRect;
    private Rect? _moderationSendRect;
    private Rect? _moderationTargetRect;
    private Platform _moderationSendPlatform = Platform.Twitch;

    /// <summary>"Multichat" picked as the destination: the message goes to Twitch and Kick at once.</summary>
    private bool _moderationSendToBoth;

    private bool _moderationSending;
    private System.Threading.Timer? _moderationInputBlinkTimer;

    /// <summary>Where a typed message goes: the only logged-in platform of the current mode, or the one picked in Multichat.</summary>
    private Platform ModerationSendPlatform
    {
        get
        {
            bool twitch = ModerationShowsTwitch && TwitchModerationReady;
            bool kick = ModerationShowsKick && KickModerationReady;
            if (twitch && !kick)
                return Platform.Twitch;
            if (kick && !twitch)
                return Platform.Kick;
            return _moderationSendPlatform;
        }
    }

    /// <summary>True when the message goes to both chats: Multichat is the picked destination and both accounts are logged in.</summary>
    private bool ModerationSendsToBoth =>
        _moderationSendToBoth
        && ModerationShowsTwitch
        && TwitchModerationReady
        && ModerationShowsKick
        && KickModerationReady;

    private void OpenModerationSendTargetDropdown(Rect anchor)
    {
        bool both = ModerationSendsToBoth;
        var items = new List<ModerationDropdownItem>();
        foreach (var platform in new[] { Platform.Twitch, Platform.Kick })
        {
            var selected = platform;
            items.Add(
                new ModerationDropdownItem
                {
                    Label = (!both && selected == ModerationSendPlatform ? "\u2713  " : "     ") + selected,
                    OnSelect = () =>
                    {
                        _moderationSendPlatform = selected;
                        _moderationSendToBoth = false;
                        RequestRender();
                    },
                }
            );
        }

        // Multichat: one message, sent to both chats.
        items.Add(
            new ModerationDropdownItem
            {
                Label = (both ? "\u2713  " : "     ") + ModeMultichat,
                OnSelect = () =>
                {
                    _moderationSendToBoth = true;
                    RequestRender();
                },
            }
        );
        OpenModerationDropdown(anchor, items);
    }

    /// <summary>
    /// Click handling for the message box. Returns true when the click was on one of its controls; any other click
    /// takes the keyboard focus out of the box.
    /// </summary>
    private bool HandleModerationInputClick(int clientX, int clientY)
    {
        // The user filter of the Messages tab (it lives in the panel, so only there).
        if (HandleModerationFilterClick(clientX, clientY))
            return true;

        if (_chatInputRowShown)
        {
            if (_moderationTargetRect is { } target && Contains(target, clientX, clientY))
            {
                OpenModerationSendTargetDropdown(target);
                return true;
            }

            if (_moderationSendRect is { } send && Contains(send, clientX, clientY))
            {
                _ = SendModerationMessageAsync();
                return true;
            }

            if (_moderationInputFieldRect is { } field && Contains(field, clientX, clientY))
            {
                FocusModerationBox(_moderationInput, field, clientX);
                return true;
            }
        }

        if (_moderationInput.IsFocused || _moderationFilterInput.IsFocused)
        {
            _moderationInput.Blur();
            _moderationFilterInput.Blur();
            RequestRender();
        }
        return false;
    }

    private void FocusModerationBox(TextBox box, Rect field, int clientX)
    {
        Win32.SetFocus(Hwnd);

        // Only one box has the keyboard at a time.
        if (!ReferenceEquals(box, _moderationInput))
            _moderationInput.Blur();
        if (!ReferenceEquals(box, _moderationFilterInput))
            _moderationFilterInput.Blur();

        float scroll = ModerationInputScroll(box, field, out float innerLeft, out _);
        bool shift = (Win32.GetKeyState(Win32.VK_SHIFT) & 0x8000) != 0;

        // Clicks are handled on button-up here, so the drag the box starts (and the mouse capture it takes) is ended at once.
        box.HandleClick(
            DWriteFactory,
            _moderationBodyFormat!,
            new Rect(innerLeft - scroll, 0, 10000f, field.Height),
            clientX,
            shift,
            this
        );
        box.HandleLButtonUp();
        Win32.ReleaseCapture();

        _moderationInputBlinkTimer ??= new System.Threading.Timer(
            _ => PostToUiThread(TickModerationInputBlink),
            null,
            530,
            530
        );
        RequestRender();
    }

    private void TickModerationInputBlink()
    {
        bool redraw = false;
        if (_moderationInput.IsFocused && _moderationInput.TickBlink())
            redraw = true;
        if (_moderationFilterInput.IsFocused && _moderationFilterInput.TickBlink())
            redraw = true;
        if (redraw)
            RequestRender();
    }

    /// <summary>Horizontal scroll of the text so the caret stays visible; also gives where the text starts.</summary>
    private float ModerationInputScroll(TextBox box, Rect field, out float innerLeft, out string display)
    {
        innerLeft = field.Left + 8f;
        display = box.Text;
        if (!box.IsFocused)
            return 0f;

        float innerWidth = Math.Max(1f, field.Width - 16f);
        float caretX = MeasureModerationInputWidth(display.Substring(0, Math.Min(box.CaretIndex, display.Length)));
        return Math.Max(0f, caretX - innerWidth);
    }

    private float MeasureModerationInputWidth(string text)
    {
        if (text.Length == 0)
            return 0f;
        using var layout = DWriteFactory.CreateTextLayout(text, _moderationBodyFormat!, 4000f, 100f);
        return layout.Metrics.WidthIncludingTrailingWhitespace;
    }

    private async Task SendModerationMessageAsync()
    {
        if (_moderationSending)
            return;

        string text = _moderationInput.Text.Trim();
        if (text.Length == 0)
            return;

        // One destination, or both when Multichat is picked.
        var targets = new List<(Platform Platform, IChatModerationActions Actions, string Channel)>(2);
        var wanted = ModerationSendsToBoth
            ? new[] { Platform.Twitch, Platform.Kick }
            : new[] { ModerationSendPlatform };
        foreach (var wantedPlatform in wanted)
        {
            if (TryGetModerationActions(wantedPlatform, out var wantedActions, out var wantedChannel))
                targets.Add((wantedPlatform, wantedActions, wantedChannel));
        }
        if (targets.Count == 0)
            return;

        _moderationSending = true;
        RequestRender();

        // Both messages go out at the same time, so Multichat takes as long as the slower of the two.
        var results = new ChatSendResult[targets.Count];
        Array.Fill(results, ChatSendResult.Failed);
        await Task.WhenAll(
            targets.Select(async (target, index) =>
            {
                try
                {
                    results[index] = await target.Actions.SendChatMessageAsync(target.Channel, text);
                }
                catch (Exception ex)
                {
                    DebugLog.WriteException("ChatRenderWindow.SendModerationMessageAsync", ex);
                }
            })
        );

        PostToUiThread(() =>
        {
            _moderationSending = false;
            _moderationStatusText = IdleModerationStatus();

            bool anySent = results.Any(r => r == ChatSendResult.Ok);
            bool allSent = results.All(r => r == ChatSendResult.Ok);

            // Keep the text when nothing went out so it can be retried; once it is out in at least one chat, clear it.
            if (anySent)
                _moderationInput.Text = "";

            if (allSent)
            {
                ShowSuccessToast(LocalizationService.T("Moderation_MessageSent"));
            }
            else
            {
                // In Multichat each toast says which chat it is about.
                bool named = targets.Count > 1;
                for (int i = 0; i < targets.Count; i++)
                {
                    string prefix = named ? targets[i].Platform + ": " : "";
                    if (results[i] == ChatSendResult.Ok)
                        ShowSuccessToast(prefix + LocalizationService.T("Moderation_MessageSent"));
                    else
                        ShowErrorToast(
                            prefix
                                + LocalizationService.T(
                                    results[i] == ChatSendResult.MissingPermission
                                        ? "Moderation_SendNeedsRelogin"
                                        : "Moderation_SendFailed"
                                )
                        );
                }
            }
            RequestRender();
        });
    }

    protected override void OnKeyDown(int virtualKeyCode, bool ctrlDown, bool shiftDown)
    {
        if (_moderationFilterInput.IsFocused)
        {
            // The filter applies as you type: Enter and Escape just hand the keyboard back.
            if (virtualKeyCode is Win32.VK_RETURN or Win32.VK_ESCAPE)
            {
                _moderationFilterInput.Blur();
                RequestRender();
                return;
            }

            _moderationFilterInput.HandleKeyDown(virtualKeyCode, ctrlDown, shiftDown);
            RequestRender();
            return;
        }

        if (!_moderationInput.IsFocused)
        {
            base.OnKeyDown(virtualKeyCode, ctrlDown, shiftDown);
            return;
        }

        switch (virtualKeyCode)
        {
            case Win32.VK_RETURN:
                _ = SendModerationMessageAsync();
                return;
            case Win32.VK_ESCAPE:
                _moderationInput.Blur();
                RequestRender();
                return;
        }

        _moderationInput.HandleKeyDown(virtualKeyCode, ctrlDown, shiftDown);
        RequestRender();
    }

    protected override void OnChar(char c)
    {
        if (_moderationFilterInput.IsFocused)
        {
            _moderationFilterInput.HandleChar(c);
            RequestRender();
            return;
        }

        if (!_moderationInput.IsFocused)
        {
            base.OnChar(c);
            return;
        }

        _moderationInput.HandleChar(c);
        RequestRender();
    }

    private void ReleaseModerationInput()
    {
        _moderationInput.Blur();
        _moderationFilterInput.Blur();
        _moderationFilterFieldRect = null;
        _moderationFilterClearRect = null;
        _moderationLinksChipRect = null;
        _moderationInputBlinkTimer?.Dispose();
        _moderationInputBlinkTimer = null;
        _moderationInputFieldRect = null;
        _moderationSendRect = null;
        _moderationTargetRect = null;
    }
}
