using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Modules.Utility.Common;
using Mewdeko.Services.Impl;

namespace Mewdeko.Modules.Utility.Services;

/// <summary>
///     Runs members' code in throwaway containers and keeps the number of runs bounded.
/// </summary>
public sealed class CodeRunService : INService, IReadyExecutor, IDisposable
{
    /// <summary>
    ///     The most runs going at once across the bot.
    /// </summary>
    public const int MaxConcurrentRuns = 4;

    private static readonly TimeSpan Cooldown = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromMinutes(3);
    private static readonly TimeSpan StaleAge = TimeSpan.FromSeconds(60);
    private readonly CancellationTokenSource stopping = new();
    private readonly ConcurrentDictionary<ulong, DateTime> lastRuns = new();
    private readonly ConcurrentDictionary<ulong, byte> running = new();
    private readonly SemaphoreSlim slots = new(MaxConcurrentRuns, MaxConcurrentRuns);
    private readonly BotCredentials creds;
    private readonly CodeRunDockerClient docker;
    private readonly ILogger<CodeRunService> logger;
    private readonly Lazy<CodeRunLanguages?> languages;

    /// <summary>
    ///     Initializes a new instance of the <see cref="CodeRunService" /> class.
    /// </summary>
    /// <param name="creds">The bot credentials holding the run settings.</param>
    /// <param name="docker">The Docker client for the run host.</param>
    /// <param name="logger">The logger instance for structured logging.</param>
    public CodeRunService(BotCredentials creds, CodeRunDockerClient docker, ILogger<CodeRunService> logger)
    {
        this.creds = creds;
        this.docker = docker;
        this.logger = logger;
        languages = new Lazy<CodeRunLanguages?>(LoadLanguages);
    }

    /// <summary>
    ///     Gets whether the feature is switched on and a Docker host can be reached.
    /// </summary>
    public bool Available
    {
        get
        {
            return creds.CodeRunEnabled && docker.Available && languages.Value != null;
        }
    }

    /// <summary>
    ///     Gets whether images can be pulled, which does not need the feature to be switched on yet.
    /// </summary>
    public bool CanPullImages
    {
        get
        {
            return docker.Available && languages.Value != null;
        }
    }

    /// <summary>
    ///     Gets the configured languages. Only valid while <see cref="Available" /> or <see cref="CanPullImages" />
    ///     is true.
    /// </summary>
    public CodeRunLanguages Languages
    {
        get
        {
            return languages.Value ?? throw new InvalidOperationException("The language file is not loaded");
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        stopping.Cancel();
        stopping.Dispose();
        slots.Dispose();
    }

    /// <inheritdoc />
    public Task OnReadyAsync()
    {
        if (!Available)
            return Task.CompletedTask;

        _ = Task.Run(CleanupLoopAsync);
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Lists the languages whose image is on the host.
    /// </summary>
    public async Task<List<CodeRunLanguage>> GetInstalledAsync()
    {
        var installed = new List<CodeRunLanguage>();
        if (!Available)
            return installed;

        foreach (var language in Languages.All)
        {
            try
            {
                if (await docker.ImageExistsAsync(language.Image))
                    installed.Add(language);
            }
            catch (Exception ex) when (ex is HttpRequestException or CodeRunDockerException or OperationCanceledException)
            {
                logger.LogWarning(ex, "Could not check the image for {Language}", language.Name);
                break;
            }
        }

        return installed;
    }

    /// <summary>
    ///     Pulls the image of every configured language.
    /// </summary>
    /// <param name="progress">Called after each image with how many are done, how many there are and the failures.</param>
    public async Task<List<(CodeRunLanguage Language, string Error)>> PullAllAsync(
        Func<int, int, Task> progress)
    {
        var failures = new List<(CodeRunLanguage, string)>();
        var images = Languages.All.GroupBy(l => l.Image).Select(g => g.First()).ToList();
        var done = 0;
        foreach (var language in images)
        {
            try
            {
                var (success, error) = await docker.PullImageAsync(language.Image);
                if (!success)
                    failures.Add((language, error ?? "unknown error"));
            }
            catch (Exception ex) when (ex is HttpRequestException or CodeRunDockerException or OperationCanceledException)
            {
                failures.Add((language, ex.Message));
            }

            done++;
            await progress(done, images.Count);
        }

        return failures;
    }

    /// <summary>
    ///     Runs code and returns what it printed.
    /// </summary>
    /// <param name="language">The configured language.</param>
    /// <param name="code">The code, already checked for length.</param>
    /// <param name="userId">The member running it.</param>
    /// <param name="guildId">The server it was run in.</param>
    public async Task<CodeRunResult> RunAsync(CodeRunLanguage language, string code, ulong userId, ulong guildId)
    {
        if (!Available)
            return CodeRunResult.Refused(CodeRunStatus.Unavailable);

        if (lastRuns.TryGetValue(userId, out var last) && DateTime.UtcNow - last < Cooldown)
            return CodeRunResult.Refused(CodeRunStatus.Cooldown);

        if (!running.TryAdd(userId, 0))
            return CodeRunResult.Refused(CodeRunStatus.UserBusy);

        if (!await slots.WaitAsync(0))
        {
            running.TryRemove(userId, out _);
            return CodeRunResult.Refused(CodeRunStatus.Full);
        }

        try
        {
            if (!await docker.ImageExistsAsync(language.Image))
                return CodeRunResult.Refused(CodeRunStatus.NotInstalled);

            lastRuns[userId] = DateTime.UtcNow;
            var result = await ExecuteAsync(language, code, userId);
            logger.LogInformation(
                "Code run by {UserId} in {GuildId}: {Language}, {DurationMs} ms, exit code {ExitCode}, timed out {TimedOut}",
                userId, guildId, language.Name, (long)result.Duration.TotalMilliseconds, result.ExitCode,
                result.TimedOut);
            return result;
        }
        catch (Exception ex) when (ex is HttpRequestException or CodeRunDockerException or IOException
                                       or OperationCanceledException or System.Text.Json.JsonException)
        {
            logger.LogWarning(ex, "Code run for {UserId} failed", userId);
            return CodeRunResult.Refused(CodeRunStatus.Failed);
        }
        finally
        {
            slots.Release();
            running.TryRemove(userId, out _);
        }
    }

    private async Task<CodeRunResult> ExecuteAsync(CodeRunLanguage language, string code, ulong userId)
    {
        var request = CodeRunRequestBuilder.Build(language, code, new CodeRunOptions(userId, creds.CodeRunRuntime));
        var id = await docker.CreateAsync($"mewdeko-coderun-{Guid.NewGuid():N}", request);
        try
        {
            var watch = Stopwatch.StartNew();
            await docker.StartAsync(id);
            var exitCode = await docker.WaitAsync(id, TimeSpan.FromSeconds(language.DeadlineSeconds));
            var timedOut = exitCode == null;
            if (timedOut)
                await docker.KillAsync(id);
            watch.Stop();

            var (data, cut) = await docker.ReadLogsAsync(id);
            return new CodeRunResult(CodeRunStatus.Completed, Encoding.UTF8.GetString(data), cut, exitCode ?? 137,
                watch.Elapsed, timedOut);
        }
        finally
        {
            await RemoveQuietlyAsync(id);
        }
    }

    private async Task RemoveQuietlyAsync(string id)
    {
        try
        {
            await docker.RemoveAsync(id);
        }
        catch (Exception ex) when (ex is HttpRequestException or CodeRunDockerException or OperationCanceledException)
        {
            logger.LogWarning(ex, "Could not remove code run container {Id}", id);
        }
    }

    private CodeRunLanguages? LoadLanguages()
    {
        try
        {
            return CodeRunLanguages.LoadFile(Path.Combine("data", "coderun.json"));
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            logger.LogError(ex, "The code run language file could not be loaded");
            return null;
        }
    }

    private async Task CleanupLoopAsync()
    {
        using var timer = new PeriodicTimer(CleanupInterval);
        try
        {
            do
            {
                await CleanupAsync();
            } while (await timer.WaitForNextTickAsync(stopping.Token));
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task CleanupAsync()
    {
        try
        {
            foreach (var id in await docker.ListStaleAsync(StaleAge))
                await RemoveQuietlyAsync(id);
        }
        catch (Exception ex) when (ex is HttpRequestException or CodeRunDockerException or OperationCanceledException
                                       or System.Text.Json.JsonException)
        {
            logger.LogWarning(ex, "The code run cleanup pass failed");
        }
    }
}
