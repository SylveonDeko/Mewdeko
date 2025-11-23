using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using DataModel;
using Serilog;

namespace Mewdeko.Modules.Utility.Services.Impl;

/// <summary>
///     Implements Claude AI functionality using direct API calls to Anthropic's API.
/// </summary>
public class ClaudeClient : IAiClient
{
    private static readonly string[] item = new[]
    {
        "user_query"
    };

    private readonly IHttpClientFactory httpClientFactory;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ClaudeClient" /> class.
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    public ClaudeClient(IHttpClientFactory httpClientFactory)
    {
        this.httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
    }

    /// <summary>
    ///     Gets the AI provider type for this client.
    /// </summary>
    public AiService.AiProvider Provider => AiService.AiProvider.Claude;

    /// <summary>
    ///     Streams a response from the Claude AI model.
    /// </summary>
    /// <param name="messages">The conversation history.</param>
    /// <param name="model">The model identifier to use.</param>
    /// <param name="apiKey">The API key for authentication.</param>
    /// <param name="cancellationToken">Optional token to cancel the operation.</param>
    /// <returns>A stream containing the raw JSON responses from the Claude API.</returns>
    public async Task<IAsyncEnumerable<string>> StreamResponseAsync(IEnumerable<AiMessage> messages, string model,
        string apiKey, CancellationToken cancellationToken = default)
    {
        return await StreamResponseAsync(messages, model, apiKey, false, cancellationToken);
    }

    /// <summary>
    ///     Streams a response from the Claude AI model with optional web search capability.
    /// </summary>
    /// <param name="messages">The conversation history.</param>
    /// <param name="model">The model identifier to use.</param>
    /// <param name="apiKey">The API key for authentication.</param>
    /// <param name="enableWebSearch">Whether to enable web search tool.</param>
    /// <param name="cancellationToken">Optional token to cancel the operation.</param>
    /// <returns>A stream containing the raw JSON responses from the Claude API.</returns>
    public async Task<IAsyncEnumerable<string>> StreamResponseAsync(IEnumerable<AiMessage> messages, string model,
        string apiKey, bool enableWebSearch, CancellationToken cancellationToken = default)
    {
        return await StreamResponseAsync(messages, model, apiKey, enableWebSearch, false, 0, cancellationToken);
    }

    /// <summary>
    ///     Streams a response from the Claude AI model with optional tools.
    /// </summary>
    /// <param name="messages">The conversation history.</param>
    /// <param name="model">The model identifier to use.</param>
    /// <param name="apiKey">The API key for authentication.</param>
    /// <param name="enableWebSearch">Whether to enable web search tool.</param>
    /// <param name="enableUserInfo">Whether to enable user information tool.</param>
    /// <param name="guildId">Guild ID for user information context.</param>
    /// <param name="cancellationToken">Optional token to cancel the operation.</param>
    /// <returns>A stream containing the raw JSON responses from the Claude API.</returns>
    public async Task<IAsyncEnumerable<string>> StreamResponseAsync(IEnumerable<AiMessage> messages, string model,
        string apiKey, bool enableWebSearch, bool enableUserInfo, ulong guildId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var httpClient = httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Add("x-api-key", apiKey);
            httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

            // Extract system message and prepare for API
            var systemMessage = messages.FirstOrDefault(m => m.Role == "system")?.Content;
            var filteredMessages = messages.Where(m => m.Role != "system").ToList();

            // Prepare the request body
            var requestBody = new Dictionary<string, object>
            {
                ["model"] = model,
                ["max_tokens"] = 1024,
                ["messages"] = filteredMessages.Select(m => new
                {
                    role = m.Role, content = m.Content
                }).ToArray(),
                ["stream"] = true
            };

            if (!string.IsNullOrWhiteSpace(systemMessage))
            {
                requestBody["system"] = systemMessage;
            }

            // Add tools if enabled
            var tools = new List<Dictionary<string, object>>();

            if (enableWebSearch)
            {
                tools.Add(new Dictionary<string, object>
                {
                    ["type"] = "web_search_20250305", ["name"] = "web_search", ["max_uses"] = 5
                });
            }

            if (enableUserInfo)
            {
                tools.Add(new Dictionary<string, object>
                {
                    ["name"] = "get_user_info",
                    ["description"] =
                        "Get detailed information about a Discord user in this guild, including XP stats, profile, warnings, and activity",
                    ["input_schema"] = new Dictionary<string, object>
                    {
                        ["type"] = "object",
                        ["properties"] = new Dictionary<string, object>
                        {
                            ["user_query"] = new Dictionary<string, object>
                            {
                                ["type"] = "string",
                                ["description"] = "User ID, username, display name, or @mention to look up"
                            }
                        },
                        ["required"] = item
                    }
                });
            }

            if (tools.Any())
            {
                requestBody["tools"] = tools.ToArray();
            }

            var jsonRequest = JsonSerializer.Serialize(requestBody);
            Log.Information(
                $"Claude request using model: {model}, message count: {filteredMessages.Count}, request size: {Encoding.UTF8.GetByteCount(jsonRequest)} bytes");

            var content = new StringContent(
                jsonRequest,
                Encoding.UTF8,
                "application/json");

            // Create and send the request
            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages")
            {
                Content = content
            };

            var response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorResponse = await response.Content.ReadAsStringAsync(cancellationToken);
                Log.Error($"Claude API error: {response.StatusCode} - {errorResponse}");
                throw new HttpRequestException($"Claude API error: {response.StatusCode} - {errorResponse}");
            }

            var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

            // Create a channel to stream the responses
            var channel = Channel.CreateUnbounded<string>();

            // Process the stream in a separate task
            _ = Task.Run(async () =>
            {
                try
                {
                    using var reader = new StreamReader(stream);
                    string eventType = null;
                    string data = null;

                    while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
                    {
                        var line = await reader.ReadLineAsync(cancellationToken);
                        if (string.IsNullOrEmpty(line))
                        {
                            // Empty line marks the end of an event
                            if (!string.IsNullOrEmpty(data))
                            {
                                // Send complete event data to channel
                                await channel.Writer.WriteAsync(data, cancellationToken);

                                // Log event for debugging
                                Log.Information($"Claude event: {eventType} with data length: {data?.Length ?? 0}");

                                // Reset for next event
                                eventType = null;
                                data = null;
                            }

                            continue;
                        }

                        if (line.StartsWith("event: "))
                        {
                            eventType = line["event: ".Length..];
                        }
                        else if (line.StartsWith("data: "))
                        {
                            data = line["data: ".Length..];
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error processing Claude stream");
                }
                finally
                {
                    channel.Writer.Complete();
                }
            }, cancellationToken);

            // Return the channel reader as an IAsyncEnumerable
            return channel.Reader.ReadAllAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "An error occurred while streaming messages from Claude.");
            throw;
        }
    }
}