using TTNOverlay.Models;

namespace TTNOverlay.Streamlabs;

/// <summary>
/// Abstraction over the Streamlabs Socket API client (connect/disconnect and incoming event notifications).
/// </summary>
public interface IStreamlabsSocketClient : IAsyncDisposable
{
    event Action<ChatMessage>? MessageReceived;
    event Action? Connected;
    event Action<string>? Disconnected;
    event Action<Exception>? Error;

    Task ConnectAsync(string token);
}

