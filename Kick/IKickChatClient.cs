using TTNOverlay.Models;

namespace TTNOverlay.Kick;

/// <summary>
/// Abstraction over the Kick chat client (connect/disconnect and incoming message notifications).
/// Same shape as ITwitchIrcClient on purpose, so it plugs into ChatRenderWindow the same way.
/// </summary>
public interface IKickChatClient : IAsyncDisposable
{
    event Action<ChatMessage>? MessageReceived;
    event Action<string>? Connected;
    event Action<string>? Disconnected;
    event Action<Exception>? Error;

    Task ConnectAsync(string channelSlug);

    /// <summary>
    /// Fetches the current viewer count for the connected channel (null if offline, unresolved,
    /// or not connected yet).
    /// </summary>
    Task<int?> GetViewerCountAsync();
}