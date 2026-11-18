using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using Mewdeko.Modules.OwnerOnly.Services;
using Mewdeko.Modules.Utility.Common;
using Mewdeko.Services.Impl;

namespace Mewdeko.Modules.Utility.Services;

/// <summary>
///     Where code run containers are created.
/// </summary>
public enum CodeRunEndpointSource
{
    /// <summary>
    ///     This instance's own Docker connection.
    /// </summary>
    BotConnection,

    /// <summary>
    ///     The host named by the code run credentials.
    /// </summary>
    Configured,

    /// <summary>
    ///     There is nowhere to run code.
    /// </summary>
    Unavailable
}

/// <summary>
///     Where code runs go and why, when nowhere.
/// </summary>
/// <param name="Source">The chosen source.</param>
/// <param name="Problem">Why no endpoint can be used, or null.</param>
public sealed record CodeRunEndpointChoice(CodeRunEndpointSource Source, string? Problem);

/// <summary>
///     Raised when the Docker daemon refuses a request or answers unexpectedly.
/// </summary>
/// <param name="message">What the daemon said.</param>
public sealed class CodeRunDockerException(string message) : Exception(message);

/// <summary>
///     The Docker Engine API calls the run command needs. Every call goes to the host chosen by the code run
///     credentials, never to a different one, and the bot's own Docker features are not touched.
/// </summary>
public sealed partial class CodeRunDockerClient : INService, IDisposable
{
    private static readonly TimeSpan ApiTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan PullTimeout = TimeSpan.FromMinutes(10);
    private readonly Lazy<(HttpClient? Client, bool Owned, string? Problem)> connection;
    private readonly BotCredentials creds;
    private readonly ILogger<CodeRunDockerClient> logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="CodeRunDockerClient" /> class.
    /// </summary>
    /// <param name="creds">The bot credentials holding the code run host settings.</param>
    /// <param name="docker">The bot's own Docker service, used only when no code run host is set.</param>
    /// <param name="logger">The logger instance for structured logging.</param>
    public CodeRunDockerClient(BotCredentials creds, DockerService docker, ILogger<CodeRunDockerClient> logger)
    {
        this.creds = creds;
        this.logger = logger;
        connection = new Lazy<(HttpClient?, bool, string?)>(() => Connect(docker));
    }

    /// <summary>
    ///     Gets whether there is a Docker host code can run on.
    /// </summary>
    public bool Available
    {
        get
        {
            return connection.Value.Client != null;
        }
    }

    /// <summary>
    ///     Gets why no Docker host can be used, or null.
    /// </summary>
    public string? Problem
    {
        get
        {
            return connection.Value.Problem;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (connection.IsValueCreated && connection.Value.Owned)
            connection.Value.Client?.Dispose();
    }

    /// <summary>
    ///     Decides which Docker host code runs use.
    /// </summary>
    /// <param name="configuredHost">The code run host credential.</param>
    /// <param name="botConnectionPresent">Whether the bot has its own Docker connection.</param>
    /// <param name="tls">The TLS settings for the configured host.</param>
    public static CodeRunEndpointChoice Choose(string configuredHost, bool botConnectionPresent, DockerTlsSettings tls)
    {
        if (!string.IsNullOrWhiteSpace(configuredHost))
        {
            var endpoint = DockerTransport.ParseEndpoint(configuredHost, tls.IsConfigured);
            return endpoint.Problem == null
                ? new CodeRunEndpointChoice(CodeRunEndpointSource.Configured, null)
                : new CodeRunEndpointChoice(CodeRunEndpointSource.Unavailable, endpoint.Problem);
        }

        return botConnectionPresent
            ? new CodeRunEndpointChoice(CodeRunEndpointSource.BotConnection, null)
            : new CodeRunEndpointChoice(CodeRunEndpointSource.Unavailable, "This instance has no Docker connection");
    }

    /// <summary>
    ///     Checks whether an image is on the host.
    /// </summary>
    /// <param name="image">The configured image.</param>
    public async Task<bool> ImageExistsAsync(string image)
    {
        using var timeout = new CancellationTokenSource(ApiTimeout);
        using var response = await SendAsync(HttpMethod.Get, $"images/{image}/json", null,
            HttpCompletionOption.ResponseContentRead, timeout.Token);
        return response.IsSuccessStatusCode;
    }

    /// <summary>
    ///     Pulls an image and waits for the pull to finish.
    /// </summary>
    /// <param name="image">The configured image.</param>
    /// <returns>Whether the image is now on the host, with the daemon's message when it is not.</returns>
    public async Task<(bool Success, string? Error)> PullImageAsync(string image)
    {
        var colon = image.LastIndexOf(':');
        var repo = image[..colon];
        var tag = image[(colon + 1)..];
        using var timeout = new CancellationTokenSource(PullTimeout);
        using var response = await SendAsync(HttpMethod.Post,
            $"images/create?fromImage={Uri.EscapeDataString(repo)}&tag={Uri.EscapeDataString(tag)}", null,
            HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        if (!response.IsSuccessStatusCode)
            return (false, await ReadErrorAsync(response));

        await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
        using var reader = new StreamReader(stream);
        string? error = null;
        while (await reader.ReadLineAsync(timeout.Token) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            try
            {
                using var doc = JsonDocument.Parse(line);
                if (doc.RootElement.TryGetProperty("error", out var message))
                    error = message.GetString();
            }
            catch (JsonException)
            {
            }
        }

        return (error == null, error);
    }

    /// <summary>
    ///     Creates a container for a run without starting it.
    /// </summary>
    /// <param name="name">The generated container name.</param>
    /// <param name="request">The container request.</param>
    /// <returns>The container id.</returns>
    public async Task<string> CreateAsync(string name, CodeRunContainerRequest request)
    {
        using var timeout = new CancellationTokenSource(ApiTimeout);
        using var response = await SendAsync(HttpMethod.Post,
            $"containers/create?name={Uri.EscapeDataString(name)}", request, HttpCompletionOption.ResponseContentRead,
            timeout.Token);
        if (!response.IsSuccessStatusCode)
            throw new CodeRunDockerException(await ReadErrorAsync(response));

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(timeout.Token));
        var id = doc.RootElement.GetProperty("Id").GetString() ?? "";
        return ValidId(id);
    }

    /// <summary>
    ///     Starts a container.
    /// </summary>
    /// <param name="id">The container id.</param>
    public async Task StartAsync(string id)
    {
        using var timeout = new CancellationTokenSource(ApiTimeout);
        using var response = await SendAsync(HttpMethod.Post, $"containers/{ValidId(id)}/start", null,
            HttpCompletionOption.ResponseContentRead, timeout.Token);
        if (!response.IsSuccessStatusCode)
            throw new CodeRunDockerException(await ReadErrorAsync(response));
    }

    /// <summary>
    ///     Waits for a container to stop, giving up at the deadline.
    /// </summary>
    /// <param name="id">The container id.</param>
    /// <param name="deadline">How long to wait.</param>
    /// <returns>The exit code, or null when the deadline passed first.</returns>
    public async Task<long?> WaitAsync(string id, TimeSpan deadline)
    {
        using var timeout = new CancellationTokenSource(deadline);
        try
        {
            using var response = await SendAsync(HttpMethod.Post,
                $"containers/{ValidId(id)}/wait?condition=not-running", null, HttpCompletionOption.ResponseContentRead,
                timeout.Token);
            if (!response.IsSuccessStatusCode)
                throw new CodeRunDockerException(await ReadErrorAsync(response));

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(timeout.Token));
            return doc.RootElement.GetProperty("StatusCode").GetInt64();
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            return null;
        }
    }

    /// <summary>
    ///     Kills a container.
    /// </summary>
    /// <param name="id">The container id.</param>
    public async Task KillAsync(string id)
    {
        using var timeout = new CancellationTokenSource(ApiTimeout);
        using var response = await SendAsync(HttpMethod.Post, $"containers/{ValidId(id)}/kill", null,
            HttpCompletionOption.ResponseContentRead, timeout.Token);
        if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.Conflict)
            throw new CodeRunDockerException(await ReadErrorAsync(response));
    }

    /// <summary>
    ///     Reads a container's output, stopping at the read cap.
    /// </summary>
    /// <param name="id">The container id.</param>
    public async Task<(byte[] Data, bool Cut)> ReadLogsAsync(string id)
    {
        using var timeout = new CancellationTokenSource(ApiTimeout);
        using var response = await SendAsync(HttpMethod.Get, $"containers/{ValidId(id)}/logs?stdout=1&stderr=1",
            null, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        if (!response.IsSuccessStatusCode)
            throw new CodeRunDockerException(await ReadErrorAsync(response));

        await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
        return await CodeRunOutput.ReadDemultiplexedAsync(stream, CodeRunOutput.ReadLimit, timeout.Token);
    }

    /// <summary>
    ///     Force removes a container. A container the daemon does not know counts as removed.
    /// </summary>
    /// <param name="id">The container id.</param>
    public async Task RemoveAsync(string id)
    {
        using var timeout = new CancellationTokenSource(ApiTimeout);
        using var response = await SendAsync(HttpMethod.Delete, $"containers/{ValidId(id)}?force=true&v=true", null,
            HttpCompletionOption.ResponseContentRead, timeout.Token);
        if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.NotFound)
            throw new CodeRunDockerException(await ReadErrorAsync(response));
    }

    /// <summary>
    ///     Lists the ids of run containers older than the given age.
    /// </summary>
    /// <param name="olderThan">The minimum age.</param>
    public async Task<List<string>> ListStaleAsync(TimeSpan olderThan)
    {
        var filters = JsonSerializer.Serialize(new Dictionary<string, string[]>
        {
            ["label"] = [$"{CodeRunRequestBuilder.Label}=1"]
        });
        using var timeout = new CancellationTokenSource(ApiTimeout);
        using var response = await SendAsync(HttpMethod.Get,
            $"containers/json?all=1&filters={Uri.EscapeDataString(filters)}", null,
            HttpCompletionOption.ResponseContentRead, timeout.Token);
        if (!response.IsSuccessStatusCode)
            throw new CodeRunDockerException(await ReadErrorAsync(response));

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(timeout.Token));
        var cutoff = DateTimeOffset.UtcNow - olderThan;
        var stale = new List<string>();
        foreach (var container in doc.RootElement.EnumerateArray())
        {
            var created = DateTimeOffset.FromUnixTimeSeconds(container.GetProperty("Created").GetInt64());
            var id = container.GetProperty("Id").GetString();
            if (created < cutoff && id != null)
                stale.Add(ValidId(id));
        }

        return stale;
    }

    private static string ValidId(string id)
    {
        if (!IdPattern().IsMatch(id))
            throw new CodeRunDockerException("Docker returned an unexpected container id");

        return id;
    }

    private static async Task<string> ReadErrorAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("message", out var message) && message.GetString() is { } text)
                return text;
        }
        catch (JsonException)
        {
        }

        return $"Docker answered {(int)response.StatusCode}";
    }

    private (HttpClient? Client, bool Owned, string? Problem) Connect(DockerService docker)
    {
        var tls = new DockerTlsSettings(creds.CodeRunDockerCertFile, creds.CodeRunDockerCertPassword,
            creds.CodeRunDockerCaFile, creds.CodeRunDockerCertPem, creds.CodeRunDockerKeyPem,
            creds.CodeRunDockerCaPem);
        var botConnection = string.IsNullOrWhiteSpace(creds.CodeRunDockerHost) && docker.ApiClient != null;
        var choice = Choose(creds.CodeRunDockerHost, botConnection, tls);
        switch (choice.Source)
        {
            case CodeRunEndpointSource.BotConnection:
                return (docker.ApiClient, false, null);
            case CodeRunEndpointSource.Configured:
                var (client, _, _, problem) = DockerTransport.Create(creds.CodeRunDockerHost, tls);
                if (problem != null)
                    logger.LogWarning("The code run Docker host cannot be used: {Problem}", problem);
                return (client, true, problem);
            default:
                return (null, false, choice.Problem);
        }
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object? body,
        HttpCompletionOption completion, CancellationToken cancellationToken)
    {
        var client = connection.Value.Client ?? throw new CodeRunDockerException("Docker is not available");
        using var request = new HttpRequestMessage(method, path);
        if (body != null)
            request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        return await client.SendAsync(request, completion, cancellationToken);
    }

    [GeneratedRegex("^[0-9a-f]{12,64}$")]
    private static partial Regex IdPattern();
}
