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

    /// <summary>CLEARMSG: a single message was deleted (argument: the deleted message's id).</summary>
    event Action<string>? MessageDeleted;

    /// <summary>CLEARCHAT with a target: a user's messages were purged (arguments: user id, timeout seconds or null for a ban).</summary>
    event Action<string, int?>? UserPurged;

    /// <summary>CLEARCHAT without a target: the whole chat was cleared.</summary>
    event Action? ChatCleared;

    /// <summary>ROOMSTATE: slow / subscribers-only / emote-only / followers-only / unique-chat changed (or the full state on join).</summary>
    event Action<IrcRoomStateUpdate>? RoomStateChanged;

    Task ConnectAsync(string channel);
}

