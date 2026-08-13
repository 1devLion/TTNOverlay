using TTNOverlay.Models;

namespace TTNOverlay.Twitch;

/// <summary>
/// Abstraction over the Twitch IRC chat client (connect/disconnect and incoming message/event notifications).
/// </summary>
public interface ITwitchIrcClient : IAsyncDisposable
{
    event Action<ChatMessage>? MessageReceived;
    event Action<string>? Connected;
    event Action<string>? Disconnected;
    event Action<Exception>? Error;

    Task ConnectAsync(string channel);
}

