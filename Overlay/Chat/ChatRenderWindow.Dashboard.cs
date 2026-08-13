using TTNOverlay.Models;

namespace TTNOverlay.Overlay;

/// <summary>
/// ChatRenderWindow partial: the events dashboard view that lists recent Streamlabs/Twitch events separately from chat.
/// </summary>
internal sealed partial class ChatRenderWindow
{
    private const int MaxEventsInDashboard = 30;

    private readonly List<ChatMessage> _dashboardEvents = new();
    private bool _showingEvents;

    private bool _showingModeration;

    private bool _eventsViewPinnedManually;
    private System.Threading.Timer? _autoRevertTimer;

    private void AddDashboardEvent(ChatMessage msg)
    {
        _dashboardEvents.Add(msg);
        while (_dashboardEvents.Count > MaxEventsInDashboard)
            _dashboardEvents.RemoveAt(0);

    }

    private void ToggleEventsView()
    {
        if (!_settings.EnableEventsPanel)
            return;

        StopAutoRevertTimer();
        _eventsViewPinnedManually = !_showingEvents;
        SetView(showEvents: !_showingEvents);
    }

    private void ShowEventsTemporarily()
    {
        SetView(showEvents: true);

        if (_eventsViewPinnedManually)
            return;

        StopAutoRevertTimer();
        _autoRevertTimer = new System.Threading.Timer(
            _ => PostToUiThread(() => SetView(showEvents: false)),
            null,
            TimeSpan.FromSeconds(5),
            System.Threading.Timeout.InfiniteTimeSpan
        );
    }

    private void StopAutoRevertTimer()
    {
        _autoRevertTimer?.Dispose();
        _autoRevertTimer = null;
    }

    private void SetView(bool showEvents, bool showModeration = false)
    {
        if (showModeration && !_showingModeration)
            EnterModerationLayout();
        else if (!showModeration && _showingModeration)
            ExitModerationLayout();

        if (_showingEvents && !showEvents)
        {
            PurgeEventIconCaches();
        }

        _showingEvents = showEvents;
        _showingModeration = showModeration;
        RequestRender();
    }

    private void DisconnectDashboard() => StopAutoRevertTimer();
}