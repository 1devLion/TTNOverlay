using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using TTNOverlay.Models;
using TTNOverlay.Services;

namespace TTNOverlay.Streamlabs;

/// <summary>
/// WebSocket client for the Streamlabs Socket API; receives donation/sub/follow events and raises them as ChatMessages.
/// </summary>
public class StreamlabsSocketClient : IStreamlabsSocketClient
{
    private const string WsUrlBase =
        "wss://sockets.streamlabs.com/socket.io/?EIO=3&transport=websocket&token=";

    private ClientWebSocket? _socket;
    private CancellationTokenSource? _cts;
    private int _pingIntervalMs = 25000;
    private bool _pingLoopStarted;

    public event Action<ChatMessage>? MessageReceived;
    public event Action? Connected;
    public event Action<string>? Disconnected;
    public event Action<Exception>? Error;

    public async Task ConnectAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new ArgumentException("Falta el Socket API Token de Streamlabs");

        _cts = new CancellationTokenSource();
        _socket = new ClientWebSocket();

        _socket.Options.SetRequestHeader(
            "User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36"
        );
        _socket.Options.SetRequestHeader("Origin", "https://streamlabs.com");
        _socket.Options.SetRequestHeader("Accept-Language", "es-ES,es;q=0.9,en;q=0.8");
        _socket.Options.SetRequestHeader("Cache-Control", "no-cache");
        _socket.Options.SetRequestHeader("Pragma", "no-cache");

        var uri = new Uri(WsUrlBase + Uri.EscapeDataString(token.Trim()));
        DebugLog.Write("Streamlabs: conectando...");
        await _socket.ConnectAsync(uri, _cts.Token);
        DebugLog.Write($"Streamlabs: WebSocket conectado, estado: {_socket.State}");

        _ = ReceiveLoopAsync(_cts.Token);
    }

    private async Task SendAsync(string text)
    {
        if (_socket is not { State: WebSocketState.Open })
            return;
        var bytes = Encoding.UTF8.GetBytes(text);
        await _socket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
    }

    private async Task ReceiveLoopAsync(CancellationToken token)
    {
        var buffer = new byte[16384];
        var sb = new StringBuilder();

        try
        {
            while (_socket is { State: WebSocketState.Open } && !token.IsCancellationRequested)
            {
                var result = await _socket.ReceiveAsync(buffer, token);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    var reason =
                        $"code={_socket.CloseStatus} desc=\"{_socket.CloseStatusDescription}\"";
                    DebugLog.Write($"Streamlabs: socket cerrado por el servidor ({reason})");
                    Disconnected?.Invoke(reason);
                    break;
                }

                sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                if (!result.EndOfMessage)
                    continue;

                var packet = sb.ToString();
                sb.Clear();
                await HandleEngineIoPacketAsync(packet);
            }
        }
        catch (Exception ex) when (!token.IsCancellationRequested)
        {
            DebugLog.WriteException("StreamlabsSocketClient.ReceiveLoopAsync", ex);
            Error?.Invoke(ex);
        }
    }

    private async Task HandleEngineIoPacketAsync(string packet)
    {
        if (packet.Length == 0)
            return;

        var preview = packet.Length > 4000 ? packet[..4000] + "..." : packet;
        DebugLog.Write($"Streamlabs RECV: {preview}");

        var engineType = packet[0];
        var rest = packet[1..];

        switch (engineType)
        {
            case '0':
            {
                try
                {
                    using var doc = JsonDocument.Parse(rest);

                    if (doc.RootElement.TryGetProperty("pingInterval", out var ping))
                    {
                        _pingIntervalMs = ping.GetInt32();
                        DebugLog.Write($"Streamlabs: pingInterval={_pingIntervalMs}ms");
                    }

                    StartPingLoop();
                }
                catch (Exception ex)
                {
                    DebugLog.WriteException("Streamlabs Handshake", ex);
                }

                break;
            }
            case '2':
                DebugLog.Write("Streamlabs RECV: 2 (ping)");
                await SendAsync("3");
                DebugLog.Write("Streamlabs SEND: 3 (pong)");
                break;
            case '4':
                HandleSocketIoPacket(rest);
                break;
        }
    }

    private void HandleSocketIoPacket(string payload)
    {
        if (payload.Length == 0)
            return;

        var socketIoType = payload[0];
        var rest = payload[1..];

        switch (socketIoType)
        {
            case '0':
                DebugLog.Write("Streamlabs: namespace conectado, escuchando eventos");
                Connected?.Invoke();
                break;
            case '2':
                HandleEventArray(rest);
                break;
            case '4':
                DebugLog.Write($"Streamlabs: error de socket.io: {rest}");
                break;
        }
    }

    private void HandleEventArray(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var arr = doc.RootElement;
            if (arr.ValueKind != JsonValueKind.Array || arr.GetArrayLength() < 2)
                return;

            if (arr[0].GetString() != "event")
                return;

            foreach (var msg in StreamlabsEventMapper.MapToMessages(arr[1]))
                MessageReceived?.Invoke(msg);
        }
        catch (Exception ex)
        {

            DebugLog.WriteException("StreamlabsSocketClient.HandleEventArray", ex);
        }
    }

    public async ValueTask DisposeAsync()
    {
        _cts?.Cancel();
        if (_socket is { State: WebSocketState.Open })
        {
            try
            {
                _cts?.Cancel();
                _pingLoopStarted = false;
                await _socket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "bye",
                    CancellationToken.None
                );
            }
            catch
            {

            }
        }
        _socket?.Dispose();
    }

    private void StartPingLoop()
    {
        if (_pingLoopStarted)
            return;

        _pingLoopStarted = true;

        _ = Task.Run(async () =>
        {
            while (
                _cts is { IsCancellationRequested: false }
                && _socket is { State: WebSocketState.Open }
            )
            {
                try
                {

                    var delay = Math.Max(1000, _pingIntervalMs - 1000);

                    await Task.Delay(delay, _cts.Token);

                    if (_socket?.State != WebSocketState.Open)
                        break;

                    DebugLog.Write("Streamlabs SEND: 2 (ping)");
                    await SendAsync("2");
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    DebugLog.WriteException("Streamlabs PingLoop", ex);
                }
            }

            _pingLoopStarted = false;
        });
    }

}

