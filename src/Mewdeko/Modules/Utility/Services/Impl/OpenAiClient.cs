using System.ClientModel;
using System.Threading;
using DataModel;
using OpenAI.Chat;

namespace Mewdeko.Modules.Utility.Services.Impl;

/// <summary>
///     OpenAI API client implementation using the official OpenAI SDK.
/// </summary>
public class OpenAiClient : IAiClient
{
    /// <summary>
    ///     Gets the AI provider type for this client.
    /// </summary>
    public AiService.AiProvider Provider
    {
        get
        {
            return AiService.AiProvider.OpenAi;
        }
    }

    /// <summary>
    ///     Streams a response from the OpenAI model.
    /// </summary>
    /// <param name="messages">The conversation history.</param>
    /// <param name="model">The model identifier to use.</param>
    /// <param name="apiKey">The API key for authentication.</param>
    /// <param name="cancellationToken">Optional token to cancel the operation.</param>
    /// <returns>A stream containing the AI response.</returns>
    public Task<IAsyncEnumerable<string>> StreamResponseAsync(IEnumerable<AiMessage> messages, string model,
        string apiKey, CancellationToken cancellationToken = default)
    {
        var client = new ChatClient(model, apiKey);

        var chatMessages = messages.Select<AiMessage, ChatMessage>(m => m.Role switch
        {
            "user" => new UserChatMessage(m.Content),
            "assistant" => new AssistantChatMessage(m.Content),
            "system" => new SystemChatMessage(m.Content),
            _ => throw new ArgumentException($"Unknown role: {m.Role}")
        }).ToList();

        var completionUpdates = client.CompleteChatStreamingAsync(chatMessages, cancellationToken: cancellationToken);

        return Task.FromResult(TransformUpdates(completionUpdates));

        static async IAsyncEnumerable<string> TransformUpdates(
            AsyncCollectionResult<StreamingChatCompletionUpdate> updates)
        {
            await foreach (var update in updates)
            {
                yield return update.ContentUpdate.FirstOrDefault()?.Text ?? "";
            }
        }
    }
}