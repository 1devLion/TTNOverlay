using System.Text;
using System.Text.Json;
using TTNOverlay.Services;

namespace TTNOverlay.Twitch;

/// <summary>
/// HelixClient partial: sending a chat message as the logged-in user (POST /helix/chat/messages).
/// Needs the user:write:chat scope on the user's token.
/// </summary>
public partial class HelixClient
{
    public async Task<ChatSendResult> SendChatMessageAsync(
        string broadcasterId,
        string senderId,
        string userAccessToken,
        string message
    )
    {
        try
        {
            string json;
            using (var stream = new MemoryStream())
            {
                using (var writer = new Utf8JsonWriter(stream))
                {
                    writer.WriteStartObject();
                    writer.WriteString("broadcaster_id", broadcasterId);
                    writer.WriteString("sender_id", senderId);
                    writer.WriteString("message", message);
                    writer.WriteEndObject();
                }
                json = Encoding.UTF8.GetString(stream.ToArray());
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.twitch.tv/helix/chat/messages")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };
            request.Headers.Add("Client-Id", _clientId);
            request.Headers.Add("Authorization", $"Bearer {userAccessToken}");

            using var response = await Http.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                // 200 also comes back when Twitch dropped the message (AutoMod, slow mode...): "is_sent": false.
                if (body.Contains("\"is_sent\":true", StringComparison.Ordinal))
                    return ChatSendResult.Ok;

                DebugLog.Write($"Helix: chat message not sent: {body}");
                return ChatSendResult.Failed;
            }

            DebugLog.Write($"Helix: sending chat message failed, status {response.StatusCode}: {body}");
            return response.StatusCode == System.Net.HttpStatusCode.Unauthorized
                ? ChatSendResult.MissingPermission
                : ChatSendResult.Failed;
        }
        catch (Exception ex)
        {
            DebugLog.WriteException("HelixClient.SendChatMessageAsync", ex);
            return ChatSendResult.Failed;
        }
    }
}
