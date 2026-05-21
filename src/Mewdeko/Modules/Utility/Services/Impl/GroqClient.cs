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
///     Implements Groq AI functionality using direct API calls.
/// </summary>
public class GroqClient : IAiClient
{
    // Define Groq model context windows from the documentation
    private static readonly Dictionary<string, int> ModelContextLimits = new()
    {
        {
            "mixtral-8x7b-32768", 32768
        },
        {
            "llama3-70b-8192", 8192
        },
        {
            "llama3-8b-8192", 8192
        },
        {
            "llama-3.1-70b-versatile", 32768
        },
        {
            "llama-3.1-8b-instant", 131072
        },
        {
            "default", 4096
        } // Default fallback limit
    };

    private readonly IHttpClientFactory httpClientFactory;

    /// <summary>
    ///     Initializes a new instance of the <see cref="GroqClient" /> class.
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    public GroqClient(IHttpClientFactory httpClientFactory)
    {
        this.httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
    }

    /// <summary>
    ///     Gets the AI provider type for this client.
    /// </summary>
    public AiService.AiProvider Provider => AiService.AiProvider.Groq;

    /// <summary>
    ///     Streams a response from the Groq AI model.
    /// </summary>
    /// <param name="messages">The conversation history.</param>
    /// <param name="model">The model identifier to use.</param>
    /// <param name="apiKey">The API key for authentication.</param>
    /// <param name="cancellationToken">Optional token to cancel the operation.</param>
    /// <returns>A stream containing the raw JSON responses from the Groq API.</returns>
    public async Task<IAsyncEnumerable<string>> StreamResponseAsync(IEnumerable<AiMessage> messages, string model,
        string apiKey, CancellationToken cancellationToken = default)
    {
        try
        {
            var httpClient = httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

            // Set a lower max_tokens value for Groq to ensure responses aren't too large
            // Note: max_tokens is currently not used in the request but kept for future implementation
            // const int maxTokens = 512; // This will help prevent Discord embed limit issues

            // Prepare the request body
            var requestBody = new
            {
                model,
                messages = messages.Select(m => new
                {
                    role = m.Role, content = m.Content
                }).ToArray(),
                stream = true
            };

            var jsonRequest = JsonSerializer.Serialize(requestBody);
            Log.Information(
                $"Groq request using model: {model}, message count: {messages.Count()}, request size: {Encoding.UTF8.GetByteCount(jsonRequest)} bytes");

            var content = new StringContent(
                jsonRequest,
                Encoding.UTF8,
                "application/json");

            // Create and send the request
            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.groq.com/openai/v1/chat/completions")
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
                Log.Error($"Groq API error: {response.StatusCode} - {errorResponse}");
                throw new HttpRequestException($"Groq API error: {response.StatusCode} - {errorResponse}");
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

                    while (!cancellationToken.IsCancellationRequested)
                    {
                        var line = await reader.ReadLineAsync(cancellationToken);
                        if (line is null)
                            break;
                        if (string.IsNullOrEmpty(line))
                            continue;

                        if (line.StartsWith("data: "))
                        {
                            var data = line["data: ".Length..];

                            // The stream ends with "data: [DONE]"
                            if (data == "[DONE]")
                                break;

                            // Write the raw JSON to the channel
                            await channel.Writer.WriteAsync(data, cancellationToken);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error processing Groq stream");
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
            Log.Error(ex, "An error occurred while streaming messages from Groq.");
            throw;
        }
    }
}