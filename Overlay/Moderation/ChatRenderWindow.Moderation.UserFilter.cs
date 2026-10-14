using TTNOverlay.Overlay.Controls;
using TTNOverlay.Services;
using Rect = Vortice.Mathematics.Rect;

namespace TTNOverlay.Overlay;

/// <summary>
/// ChatRenderWindow partial: the Messages tab works on specific users. A search box filters the log by nick (login or
/// display name) and the Links chip keeps only the messages that carry a link; with nothing in either, the tab asks
/// for a filter instead of repeating the main chat. Clicking a user's name in the main chat opens the tab with that
/// nick already in the search box.
/// </summary>
internal sealed partial class ChatRenderWindow
{
    private readonly TextBox _moderationFilterInput = new();
    private Rect? _moderationFilterFieldRect;
    private Rect? _moderationFilterClearRect;
    private Rect? _moderationLinksChipRect;
    private bool _moderationLinksOnly;

    /// <summary>The filter the list was last built with, to start at the newest message when it changes.</summary>
    private string _moderationFilterApplied = "0|";

    /// <summary>What is typed in the search box, without the @ people usually add.</summary>
    private string ModerationFilterNick => _moderationFilterInput.Text.Trim().TrimStart('@').Trim();

    private bool EntryMatchesModerationFilter(ModerationLogEntry entry, string nick)
    {
        if (nick.Length > 0)
        {
            bool nickMatches =
                entry.Login.Contains(nick, StringComparison.OrdinalIgnoreCase)
                || entry.DisplayName.Contains(nick, StringComparison.OrdinalIgnoreCase);
            if (!nickMatches)
                return false;
        }

        return !_moderationLinksOnly || ChatLinks.HasLink(entry);
    }

    /// <summary>Clicks on the search box and its clear button. True when the click was on one of them.</summary>
    private bool HandleModerationFilterClick(int clientX, int clientY)
    {
        if (!_showingModeration || _moderationTab != ModerationTab.Messages)
            return false;

        if (_moderationFilterClearRect is { } clearRect && Contains(clearRect, clientX, clientY))
        {
            _moderationFilterInput.Text = "";
            RequestRender();
            return true;
        }

        if (_moderationFilterFieldRect is { } field && Contains(field, clientX, clientY))
        {
            FocusModerationBox(_moderationFilterInput, field, clientX);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Whether a click on a name in the main chat can open that user's messages: the panel is on and some account is
    /// logged in as moderator (that is what fills the log), and the chat can be clicked.
    /// </summary>
    private bool UserHistoryClickAvailable =>
        !_showingModeration
        && !_showingEvents
        && !_bordersHidden
        && !_clickThroughEnabled
        && _settings.EnableModerationPanel
        && (!string.IsNullOrWhiteSpace(_settings.ModeratorRefreshToken) || !string.IsNullOrWhiteSpace(_settings.KickRefreshToken));

    /// <summary>Opens the panel on the Messages tab filtered by this user.</summary>
    private void OpenModerationForUser(string login)
    {
        _moderationFilterInput.Text = login;
        _moderationLinksOnly = false;
        _moderationMessagesScroll = default;
        _moderationMessagesLastNewest = null;
        CloseModerationDropdown();
        _moderationTab = ModerationTab.Messages;

        if (_showingModeration)
            RequestRender();
        else
            ToggleModerationPanel();
    }
}
