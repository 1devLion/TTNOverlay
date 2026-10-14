using TTNOverlay.Models;

namespace TTNOverlay.Kick;

/// <summary>
/// Abstraction over the Kick chat client (connect/disconnect and incoming message notifications).
/// Same shape as ITwitchIrcClient on purpose, so it plugs into ChatRenderWindow the same way.
/// </summary>
/// <summary>A ban/timeout seen on Kick's chat channel. <see cref="ExpiresAtUtc"/> is null for permanent bans or when Kick didn't say.</summary>
public sealed record KickBanEvent(string UserId, string Username, bool Permanent, DateTime? ExpiresAtUtc);

public interface IKickChatClient : IAsyncDisposable
{
    event Action<ChatMessage>? MessageReceived;
    event Action<string>? Connected;
    event Action<string>? Disconnected;
    event Action<Exception>? Error;

    // Moderation events pushed by Kick on the chat channel. Default (no-op) implementations keep existing
    // implementers, such as test doubles, compiling.

    /// <summary>A message was deleted (argument: the message id).</summary>
    event Action<string>? MessageDeleted { add { } remove { } }

    /// <summary>A user was timed out or banned.</summary>
    event Action<KickBanEvent>? UserBanned { add { } remove { } }

    /// <summary>A user was unbanned or their timeout was lifted (argument: user id).</summary>
    event Action<string>? UserUnbanned { add { } remove { } }

    /// <summary>The whole chat was cleared.</summary>
    event Action? ChatCleared { add { } remove { } }

    Task ConnectAsync(string channelSlug);

    /// <summary>
    /// Fetches the current viewer count for the connected channel (null if offline, unresolved,
    /// or not connected yet).
    /// </summary>
    Task<int?> GetViewerCountAsync();
}