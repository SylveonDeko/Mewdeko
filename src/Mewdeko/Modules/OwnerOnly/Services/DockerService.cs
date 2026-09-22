using System.Buffers.Binary;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using Mewdeko.Modules.OwnerOnly.Common;
using Mewdeko.Services.Impl;

namespace Mewdeko.Modules.OwnerOnly.Services;

/// <summary>
///     Talks to the Docker daemon on the host this instance runs on, so bot owners can see and drive the
///     containers there from the dashboard. Everything goes through the Engine API over the daemon's socket,
///     which needs no CLI in the bot. Project level work (pull, up, update) is run by a short lived helper
///     container from the official docker CLI image, so an update that recreates the bot's own container does
///     not kill the process performing it.
/// </summary>
public sealed class DockerService : INService, IDisposable
{
    /// <summary>
    ///     Upper bound on how many log lines one request returns.
    /// </summary>
    public const int MaxTail = 2000;

    /// <summary>
    ///     Label every helper container carries, so jobs can be listed without tracking them in memory.
    /// </summary>
    public const string JobLabel = "mewdeko.compose-job";

    private const string HelperImage = "docker:cli";
    private const int JobsToKeep = 20;
    private const int JobOutputLines = 4000;
    private static readonly TimeSpan ApiTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan PullTimeout = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan OverviewTtl = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan PublishedTtl = TimeSpan.FromMinutes(5);
    private static readonly DateTime ProcessStartedAt = DateTime.UtcNow;

    private readonly Dictionary<string, bool> ttyCache = new(StringComparer.Ordinal);
    private readonly IHttpClientFactory factory;
    private readonly ILogger<DockerService> logger;
    private readonly SemaphoreSlim overviewLock = new(1, 1);
    private readonly SemaphoreSlim publishedLock = new(1, 1);
    private readonly Lazy<(HttpClient? Client, string? Endpoint, string? SocketPath, string? Problem)> transport;
    private DockerOverview? cachedOverview;
    private DateTime cachedOverviewAt = DateTime.MinValue;
    private DockerPublishedImage? cachedPublished;
    private string? selfContainerId;
    private bool selfResolved;

    /// <summary>
    ///     Initializes a new instance of the <see cref="DockerService" /> class.
    /// </summary>
    /// <param name="factory">Used for the registry lookup, which goes over plain HTTPS rather than the socket.</param>
    /// <param name="logger">The logger instance for structured logging.</param>
    public DockerService(IHttpClientFactory factory, ILogger<DockerService> logger)
    {
        this.factory = factory;
        this.logger = logger;
        transport = new Lazy<(HttpClient?, string?, string?, string?)>(CreateTransport);
    }

    /// <summary>
    ///     The Docker Hub repository the bot's image is published to. Overridable for forks with
    ///     <c>MEWDEKO_IMAGE_REPO</c>.
    /// </summary>
    private static string ImageRepository
    {
        get
        {
            var configured = Environment.GetEnvironmentVariable("MEWDEKO_IMAGE_REPO");
            return string.IsNullOrWhiteSpace(configured) ? "sylveondeko/mewdeko" : configured.Trim();
        }
    }

    /// <summary>
    ///     The moving tag CI pushes on every commit to main. Overridable with <c>MEWDEKO_IMAGE_TAG</c>.
    /// </summary>
    private static string ImageTag
    {
        get
        {
            var configured = Environment.GetEnvironmentVariable("MEWDEKO_IMAGE_TAG");
            return string.IsNullOrWhiteSpace(configured) ? "nightly" : configured.Trim();
        }
    }

    private HttpClient? Client
    {
        get
        {
            return transport.Value.Client;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (transport.IsValueCreated)
            transport.Value.Client?.Dispose();
        overviewLock.Dispose();
        publishedLock.Dispose();
    }

    /// <summary>
    ///     Returns the daemon summary and container list, cached briefly so a polling dashboard does not hammer
    ///     the socket.
    /// </summary>
    public async Task<DockerOverview> GetOverviewAsync()
    {
        await overviewLock.WaitAsync();
        try
        {
            if (cachedOverview != null && DateTime.UtcNow - cachedOverviewAt < OverviewTtl)
                return cachedOverview;

            var overview = await BuildOverviewAsync();
            cachedOverview = overview;
            cachedOverviewAt = DateTime.UtcNow;
            return overview;
        }
        finally
        {
            overviewLock.Release();
        }
    }

    /// <summary>
    ///     Finds a container by full or abbreviated id, or by name.
    /// </summary>
    /// <param name="idOrName">The id prefix or name.</param>
    /// <returns>The container, or null when the daemon does not know it.</returns>
    public async Task<DockerContainerInfo?> FindContainerAsync(string idOrName)
    {
        var overview = await GetOverviewAsync();
        return overview.Containers.FirstOrDefault(c =>
            c.Id.StartsWith(idOrName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(c.Name, idOrName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    ///     Finds a compose project by name.
    /// </summary>
    /// <param name="name">The project name.</param>
    /// <returns>The project, or null when no container carries that project label.</returns>
    public async Task<DockerComposeProject?> FindProjectAsync(string name)
    {
        var overview = await GetOverviewAsync();
        return overview.Projects.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.Ordinal));
    }

    /// <summary>
    ///     Describes the running build, the container it lives in, and whether the registry has a newer image.
    /// </summary>
    public async Task<DockerSelfInfo> GetSelfAsync()
    {
        var overview = await GetOverviewAsync();
        var container = overview.Containers.FirstOrDefault(c => c.IsSelf);
        var project = container?.ComposeProject != null
            ? overview.Projects.FirstOrDefault(p => p.Name == container.ComposeProject)
            : null;
        var published = await GetPublishedAsync();

        var info = new DockerSelfInfo
        {
            BotVersion = StatsService.BotVersion,
            GitSha = BuildInfo.GitSha,
            BuildDate = BuildInfo.BuildDate,
            StartedAt = ProcessStartedAt,
            Container = container,
            Project = project,
            Published = published
        };

        if (published?.GitSha != null && info.GitSha != null)
            info.UpdateAvailable = !info.GitSha.StartsWith(published.GitSha, StringComparison.OrdinalIgnoreCase);

        if (overview.Availability != DockerAvailability.Available)
            info.UpdateBlockedReason = overview.Message ?? "Docker is not available on this host";
        else if (container == null)
            info.UpdateBlockedReason = "This instance is not running in a container";
        else if (project == null || string.IsNullOrEmpty(container.ComposeService))
            info.UpdateBlockedReason = "This instance's container was not started by docker compose";
        else if (!project.Operable)
            info.UpdateBlockedReason = "The compose files for this instance are not visible from inside it";
        else
            info.CanUpdate = true;

        return info;
    }

    /// <summary>
    ///     Asks Docker Hub what the moving tag currently points at, cached for a few minutes.
    /// </summary>
    /// <param name="force">Skip the cache.</param>
    public async Task<DockerPublishedImage?> GetPublishedAsync(bool force = false)
    {
        await publishedLock.WaitAsync();
        try
        {
            if (!force && cachedPublished != null && DateTime.UtcNow - cachedPublished.CheckedAt < PublishedTtl)
                return cachedPublished;

            cachedPublished = await QueryRegistryAsync();
            return cachedPublished;
        }
        finally
        {
            publishedLock.Release();
        }
    }

    /// <summary>
    ///     Takes a single resource sample for a running container.
    /// </summary>
    /// <param name="container">The container to sample.</param>
    /// <returns>The sample, or null when the daemon could not provide one.</returns>
    public async Task<DockerContainerStats?> GetStatsAsync(DockerContainerInfo container)
    {
        try
        {
            using var response = await GetAsync($"containers/{container.Id}/stats?stream=false");
            if (response == null || !response.IsSuccessStatusCode)
                return null;

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = doc.RootElement;

            var cpu = ReadObject(root, "cpu_stats");
            var preCpu = ReadObject(root, "precpu_stats");
            var memory = ReadObject(root, "memory_stats");

            var cpuTotal = ReadLong(ReadObject(cpu, "cpu_usage"), "total_usage") ?? 0;
            var preCpuTotal = ReadLong(ReadObject(preCpu, "cpu_usage"), "total_usage") ?? 0;
            var systemTotal = ReadLong(cpu, "system_cpu_usage") ?? 0;
            var preSystemTotal = ReadLong(preCpu, "system_cpu_usage") ?? 0;
            var onlineCpus = ReadLong(cpu, "online_cpus") ?? 0;
            var cpuUsage = ReadObject(cpu, "cpu_usage");
            if (onlineCpus == 0 && cpuUsage.ValueKind == JsonValueKind.Object &&
                cpuUsage.TryGetProperty("percpu_usage", out var perCpu) && perCpu.ValueKind == JsonValueKind.Array)
                onlineCpus = perCpu.GetArrayLength();

            var cpuDelta = cpuTotal - preCpuTotal;
            var systemDelta = systemTotal - preSystemTotal;
            var cpuPercent = cpuDelta > 0 && systemDelta > 0
                ? (double)cpuDelta / systemDelta * Math.Max(1, onlineCpus) * 100.0
                : 0.0;

            var usage = ReadLong(memory, "usage") ?? 0;
            var memoryDetail = ReadObject(memory, "stats");
            var cache = ReadLong(memoryDetail, "inactive_file")
                        ?? ReadLong(memoryDetail, "total_inactive_file")
                        ?? ReadLong(memoryDetail, "cache")
                        ?? 0;

            long rx = 0, tx = 0;
            if (root.TryGetProperty("networks", out var networks) && networks.ValueKind == JsonValueKind.Object)
            {
                foreach (var iface in networks.EnumerateObject())
                {
                    rx += ReadLong(iface.Value, "rx_bytes") ?? 0;
                    tx += ReadLong(iface.Value, "tx_bytes") ?? 0;
                }
            }

            return new DockerContainerStats
            {
                Id = container.Id,
                CpuPercent = Math.Round(cpuPercent, 2),
                MemoryBytes = Math.Max(0, usage - cache),
                MemoryLimitBytes = ReadLong(memory, "limit") ?? 0,
                NetworkRxBytes = rx,
                NetworkTxBytes = tx,
                Pids = ReadLong(ReadObject(root, "pids_stats"), "current") ?? 0,
                SampledAt = DateTime.UtcNow
            };
        }
        catch (Exception ex) when (IsTransportError(ex))
        {
            logger.LogWarning(ex, "Could not sample stats for container {Id}", container.Id);
            return null;
        }
    }

    /// <summary>
    ///     Reads a container's recent log lines, or the lines written since a cursor.
    /// </summary>
    /// <param name="container">The container.</param>
    /// <param name="tail">How many lines to return when no cursor is given, clamped to <see cref="MaxTail" />.</param>
    /// <param name="since">The cursor from a previous chunk, or null for a fresh tail.</param>
    /// <returns>The chunk, or null when the daemon refused.</returns>
    public async Task<DockerLogChunk?> GetLogsAsync(DockerContainerInfo container, int tail, string? since)
    {
        tail = Math.Clamp(tail, 1, MaxTail);
        var query = new StringBuilder($"containers/{container.Id}/logs?stdout=1&stderr=1&timestamps=1");
        if (!string.IsNullOrWhiteSpace(since))
            query.Append("&since=").Append(Uri.EscapeDataString(since));
        else
            query.Append("&tail=").Append(tail);

        try
        {
            using var response = await GetAsync(query.ToString());
            if (response == null || !response.IsSuccessStatusCode)
            {
                logger.LogWarning("Docker refused logs for {Id}: {Status}", container.Id, response?.StatusCode);
                return null;
            }

            var payload = await response.Content.ReadAsByteArrayAsync();
            var lines = container.Tty ? ParseTtyLog(payload) : ParseMultiplexedLog(payload);

            // The daemon's since is inclusive, so the line the cursor points at comes back again.
            if (!string.IsNullOrWhiteSpace(since))
                lines.RemoveAll(l => string.CompareOrdinal(l.Timestamp, since) <= 0);

            if (lines.Count > MaxTail)
                lines.RemoveRange(0, lines.Count - MaxTail);

            return new DockerLogChunk
            {
                ContainerId = container.Id,
                Lines = lines,
                Cursor = lines.Count > 0 ? lines[^1].Timestamp : since
            };
        }
        catch (Exception ex) when (IsTransportError(ex))
        {
            logger.LogWarning(ex, "Could not read logs for container {Id}", container.Id);
            return null;
        }
    }

    /// <summary>
    ///     Asks the daemon to start, stop or restart a container.
    /// </summary>
    /// <param name="container">The container.</param>
    /// <param name="action">start, stop or restart.</param>
    /// <returns>Whether the daemon accepted, with its message when it did not.</returns>
    public async Task<DockerActionResult> RunActionAsync(DockerContainerInfo container, string action)
    {
        var path = action switch
        {
            "start" => $"containers/{container.Id}/start",
            "stop" => $"containers/{container.Id}/stop?t=15",
            "restart" => $"containers/{container.Id}/restart?t=15",
            _ => null
        };
        if (path == null)
            return Failure("Unknown action");

        try
        {
            using var response = await PostAsync(path, null, TimeSpan.FromSeconds(60));
            InvalidateOverview();
            if (response == null)
                return Failure("Docker is not available on this host");

            if (response.IsSuccessStatusCode)
                return new DockerActionResult
                {
                    Success = true, Message = $"{action} accepted"
                };

            if (response.StatusCode == HttpStatusCode.NotModified)
                return new DockerActionResult
                {
                    Success = true, Message = $"Container was already {(action == "stop" ? "stopped" : "running")}"
                };

            var body = await response.Content.ReadAsStringAsync();
            return Failure(ExtractDaemonMessage(body) ?? $"Docker answered {(int)response.StatusCode}");
        }
        catch (Exception ex) when (IsTransportError(ex))
        {
            logger.LogWarning(ex, "Docker {Action} failed for {Id}", action, container.Id);
            return Failure(ex.Message);
        }
    }

    /// <summary>
    ///     Starts a compose operation on a project in a helper container and returns the job to poll.
    /// </summary>
    /// <param name="project">The project, as discovered from container labels.</param>
    /// <param name="operation">pull (fetch newer images and rebuild, restart nothing), up, or update (both).</param>
    /// <param name="services">The services to limit the operation to, or empty for the whole project.</param>
    /// <returns>The job, or a failure explaining why it could not be started.</returns>
    public async Task<(DockerJob? Job, string? Error)> StartComposeJobAsync(DockerComposeProject project,
        string operation, IReadOnlyList<string> services)
    {
        if (!project.Operable || string.IsNullOrWhiteSpace(project.WorkingDir))
            return (null, "The project's compose files are not visible from the bot, so it cannot be operated on");

        var socket = transport.Value.SocketPath;
        if (socket == null)
            return (null, "Compose jobs need the daemon's unix socket; DOCKER_HOST over tcp is not supported for them");

        var compose = new StringBuilder("docker compose --project-name ").Append(Quote(project.Name))
            .Append(" --project-directory ").Append(Quote(project.WorkingDir));
        foreach (var file in project.ConfigFiles)
            compose.Append(" -f ").Append(Quote(file));
        var scope = services.Count > 0 ? " " + string.Join(' ', services.Select(Quote)) : "";

        var steps = operation switch
        {
            "pull" =>
            [
                $"{compose} pull --ignore-buildable{scope}", $"{compose} build --pull{scope}"
            ],
            "up" =>
            [
                $"{compose} up -d --remove-orphans{scope}"
            ],
            "update" =>
            [
                $"{compose} pull --ignore-buildable{scope}", $"{compose} build --pull{scope}",
                $"{compose} up -d --remove-orphans{scope}"
            ],
            _ => (string[]?)null
        };
        if (steps == null)
            return (null, "operation must be pull, up or update");

        var script = new StringBuilder("set -e\n");
        foreach (var step in steps)
            script.Append("echo '$ ").Append(step.Replace("'", "'\\''")).Append("'\n").Append(step).Append('\n');
        script.Append("echo 'done'\n");

        var binds = new List<string>
        {
            $"{socket}:/var/run/docker.sock", $"{project.WorkingDir}:{project.WorkingDir}:ro"
        };
        foreach (var dir in project.ConfigFiles.Select(Path.GetDirectoryName).OfType<string>().Distinct())
        {
            if (!dir.StartsWith(project.WorkingDir, StringComparison.Ordinal))
                binds.Add($"{dir}:{dir}:ro");
        }

        try
        {
            if (!await EnsureHelperImageAsync())
                return (null, $"The {HelperImage} image could not be pulled");

            var name = $"mewdeko-compose-{operation}-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
            var body = new
            {
                Image = HelperImage,
                Cmd = new[]
                {
                    "sh", "-c", script.ToString()
                },
                WorkingDir = project.WorkingDir,
                Labels = new Dictionary<string, string>
                {
                    [JobLabel] = "1",
                    [$"{JobLabel}.project"] = project.Name,
                    [$"{JobLabel}.operation"] = operation,
                    [$"{JobLabel}.services"] = string.Join(',', services),
                    [$"{JobLabel}.command"] = string.Join(" && ", steps)
                },
                HostConfig = new
                {
                    Binds = binds, AutoRemove = false
                }
            };

            using var create = await PostAsync($"containers/create?name={name}", body, ApiTimeout);
            if (create == null || !create.IsSuccessStatusCode)
            {
                var text = create == null ? "" : await create.Content.ReadAsStringAsync();
                return (null, ExtractDaemonMessage(text) ?? "The helper container could not be created");
            }

            string id;
            using (var doc = JsonDocument.Parse(await create.Content.ReadAsStringAsync()))
                id = ReadString(doc.RootElement, "Id") ?? "";

            using var start = await PostAsync($"containers/{id}/start", null, ApiTimeout);
            if (start == null || !start.IsSuccessStatusCode)
            {
                var text = start == null ? "" : await start.Content.ReadAsStringAsync();
                return (null, ExtractDaemonMessage(text) ?? "The helper container could not be started");
            }

            InvalidateOverview();
            _ = Task.Run(TrimJobsAsync);
            return (await FindJobAsync(id), null);
        }
        catch (Exception ex) when (IsTransportError(ex))
        {
            logger.LogWarning(ex, "Compose {Operation} on {Project} could not be started", operation, project.Name);
            return (null, ex.Message);
        }
    }

    /// <summary>
    ///     Lists the compose jobs whose helper containers still exist, newest first, without their output.
    /// </summary>
    public async Task<List<DockerJob>> ListJobsAsync()
    {
        var jobs = new List<DockerJob>();
        try
        {
            var filters = Uri.EscapeDataString($"{{\"label\":[\"{JobLabel}=1\"]}}");
            using var response = await GetAsync($"containers/json?all=1&filters={filters}");
            if (response == null || !response.IsSuccessStatusCode)
                return jobs;

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            foreach (var element in doc.RootElement.EnumerateArray())
                jobs.Add(JobFromListing(element));
        }
        catch (Exception ex) when (IsTransportError(ex))
        {
            logger.LogDebug(ex, "Could not list compose jobs");
        }

        return jobs.OrderByDescending(j => j.StartedAt).ToList();
    }

    /// <summary>
    ///     Returns a job with its exit code and the output it has produced so far.
    /// </summary>
    /// <param name="id">The helper container id or prefix.</param>
    public async Task<DockerJob?> FindJobAsync(string id)
    {
        var job = (await ListJobsAsync()).FirstOrDefault(j => j.Id.StartsWith(id, StringComparison.OrdinalIgnoreCase));
        if (job == null)
            return null;

        try
        {
            using var inspect = await GetAsync($"containers/{job.Id}/json");
            if (inspect != null && inspect.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(await inspect.Content.ReadAsStringAsync());
                var state = ReadObject(doc.RootElement, "State");
                var running = state.ValueKind == JsonValueKind.Object &&
                              state.TryGetProperty("Running", out var r) && r.ValueKind == JsonValueKind.True;
                var exit = ReadLong(state, "ExitCode");
                var finished = ReadString(state, "FinishedAt");
                if (!running)
                {
                    job.ExitCode = (int?)exit;
                    job.Status = exit == 0 ? DockerJobStatus.Succeeded : DockerJobStatus.Failed;
                    if (DateTimeOffset.TryParse(finished, out var finishedAt) && finishedAt.Year > 1)
                        job.FinishedAt = finishedAt.UtcDateTime;
                }
            }

            var chunk = await GetLogsAsync(new DockerContainerInfo
            {
                Id = job.Id, Tty = false
            }, JobOutputLines, null);
            if (chunk != null)
                job.Output = chunk.Lines.Select(l => l.Text).ToList();
        }
        catch (Exception ex) when (IsTransportError(ex))
        {
            logger.LogDebug(ex, "Could not read job {Id}", job.Id);
        }

        return job;
    }

    private static DockerJob JobFromListing(JsonElement element)
    {
        var labels = ReadObject(element, "Labels");
        var state = ReadString(element, "State") ?? "";
        var status = ReadString(element, "Status") ?? "";
        var servicesText = ReadString(labels, $"{JobLabel}.services") ?? "";

        var job = new DockerJob
        {
            Id = ReadString(element, "Id") ?? "",
            Name = FirstName(element),
            Project = ReadString(labels, $"{JobLabel}.project") ?? "",
            Operation = ReadString(labels, $"{JobLabel}.operation") ?? "",
            Command = ReadString(labels, $"{JobLabel}.command") ?? "",
            Services = servicesText.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList(),
            StartedAt = DateTimeOffset.FromUnixTimeSeconds(ReadLong(element, "Created") ?? 0).UtcDateTime
        };

        if (state == "running" || state == "created" || state == "restarting")
            job.Status = DockerJobStatus.Running;
        else if (status.StartsWith("Exited (0)", StringComparison.Ordinal))
            job.Status = DockerJobStatus.Succeeded;
        else
            job.Status = DockerJobStatus.Failed;

        return job;
    }

    /// <summary>
    ///     Removes the helper containers of finished jobs beyond the newest few, so they do not pile up.
    /// </summary>
    private async Task TrimJobsAsync()
    {
        try
        {
            var finished = (await ListJobsAsync()).Where(j => j.Status != DockerJobStatus.Running).Skip(JobsToKeep);
            foreach (var job in finished)
            {
                using var response = await DeleteAsync($"containers/{job.Id}");
                if (response != null && !response.IsSuccessStatusCode)
                    logger.LogDebug("Could not remove old job container {Id}: {Status}", job.Id, response.StatusCode);
            }
        }
        catch (Exception ex) when (IsTransportError(ex))
        {
            logger.LogDebug(ex, "Trimming old compose jobs failed");
        }
    }

    private async Task<bool> EnsureHelperImageAsync()
    {
        using var inspect = await GetAsync($"images/{Uri.EscapeDataString(HelperImage)}/json");
        if (inspect != null && inspect.IsSuccessStatusCode)
            return true;

        logger.LogInformation("Pulling {Image} for compose jobs", HelperImage);
        using var pull = await PostAsync("images/create?fromImage=docker&tag=cli", null, PullTimeout,
            HttpCompletionOption.ResponseHeadersRead);
        if (pull == null || !pull.IsSuccessStatusCode)
            return false;

        // The pull streams progress until it is done; draining the body is what waits for completion.
        using var timeout = new CancellationTokenSource(PullTimeout);
        await using var stream = await pull.Content.ReadAsStreamAsync(timeout.Token);
        var buffer = new byte[8192];
        while (await stream.ReadAsync(buffer, timeout.Token) > 0)
        {
        }

        using var check = await GetAsync($"images/{Uri.EscapeDataString(HelperImage)}/json");
        return check != null && check.IsSuccessStatusCode;
    }

    private async Task<DockerPublishedImage> QueryRegistryAsync()
    {
        var result = new DockerPublishedImage
        {
            Repository = ImageRepository, Tag = ImageTag, CheckedAt = DateTime.UtcNow
        };

        try
        {
            using var client = factory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(15);
            var url =
                $"https://hub.docker.com/v2/repositories/{ImageRepository}/tags?page_size=50&ordering=last_updated";
            using var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                result.Error = $"Docker Hub answered {(int)response.StatusCode}";
                return result;
            }

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            if (!doc.RootElement.TryGetProperty("results", out var results) ||
                results.ValueKind != JsonValueKind.Array)
            {
                result.Error = "Docker Hub returned no tag list";
                return result;
            }

            var tags = results.EnumerateArray()
                .Select(t => (Name: ReadString(t, "name") ?? "", Digest: ReadString(t, "digest"),
                    Updated: ReadString(t, "last_updated")))
                .ToList();

            var moving = tags.FirstOrDefault(t => t.Name == ImageTag);
            if (moving.Name.Length == 0)
            {
                result.Error = $"Tag {ImageTag} was not among the newest 50 tags";
                return result;
            }

            result.Digest = moving.Digest;
            if (DateTimeOffset.TryParse(moving.Updated, out var updated))
                result.PublishedAt = updated.UtcDateTime;

            var sha = tags.FirstOrDefault(t =>
                t.Digest != null && t.Digest == moving.Digest && t.Name != ImageTag && LooksLikeSha(t.Name));
            result.GitSha = sha.Name.Length > 0 ? sha.Name : null;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogDebug(ex, "Docker Hub lookup for {Repo} failed", ImageRepository);
            result.Error = ex.Message;
        }

        return result;
    }

    private static bool LooksLikeSha(string name)
    {
        return name.Length is >= 7 and <= 40 && name.All(char.IsAsciiHexDigitLower);
    }

    private void InvalidateOverview()
    {
        cachedOverviewAt = DateTime.MinValue;
    }

    /// <summary>
    ///     Works out where the daemon is and builds a client for it. The result is fixed for the life of the
    ///     process: a daemon that appears later needs a restart to be picked up, which keeps this from probing
    ///     the filesystem on every request.
    /// </summary>
    private (HttpClient? Client, string? Endpoint, string? SocketPath, string? Problem) CreateTransport()
    {
        var configured = Environment.GetEnvironmentVariable("DOCKER_HOST");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            if (configured.StartsWith("unix://", StringComparison.OrdinalIgnoreCase))
                return BuildUnixTransport(configured["unix://".Length..]);

            if (configured.StartsWith("tcp://", StringComparison.OrdinalIgnoreCase))
            {
                var url = "http://" + configured["tcp://".Length..].TrimEnd('/') + "/";
                return (new HttpClient
                {
                    BaseAddress = new Uri(url), Timeout = Timeout.InfiniteTimeSpan
                }, configured, null, null);
            }

            return (null, configured, null, $"DOCKER_HOST '{configured}' is not a unix:// or tcp:// address");
        }

        var candidates = new List<string>
        {
            "/var/run/docker.sock"
        };
        var runtimeDir = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");
        if (!string.IsNullOrWhiteSpace(runtimeDir))
            candidates.Add(Path.Combine(runtimeDir, "docker.sock"));
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(home))
            candidates.Add(Path.Combine(home, ".docker", "run", "docker.sock"));

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
                return BuildUnixTransport(candidate);
        }

        return (null, null, null,
            "No Docker socket was found. Mount /var/run/docker.sock into the bot or set DOCKER_HOST.");
    }

    private static (HttpClient, string, string, string?) BuildUnixTransport(string socketPath)
    {
        var handler = new SocketsHttpHandler
        {
            ConnectCallback = async (_, cancellationToken) =>
            {
                var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                try
                {
                    await socket.ConnectAsync(new UnixDomainSocketEndPoint(socketPath), cancellationToken);
                    return new NetworkStream(socket, true);
                }
                catch
                {
                    socket.Dispose();
                    throw;
                }
            }
        };

        return (new HttpClient(handler)
        {
            BaseAddress = new Uri("http://docker/"), Timeout = Timeout.InfiniteTimeSpan
        }, "unix://" + socketPath, socketPath, null);
    }

    private async Task<HttpResponseMessage?> GetAsync(string path)
    {
        var client = Client;
        if (client == null)
            return null;

        using var timeout = new CancellationTokenSource(ApiTimeout);
        return await client.GetAsync(path, timeout.Token);
    }

    private async Task<HttpResponseMessage?> PostAsync(string path, object? body, TimeSpan limit,
        HttpCompletionOption completion = HttpCompletionOption.ResponseContentRead)
    {
        var client = Client;
        if (client == null)
            return null;

        using var timeout = new CancellationTokenSource(limit);
        using var request = new HttpRequestMessage(HttpMethod.Post, path);
        if (body != null)
            request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        return await client.SendAsync(request, completion, timeout.Token);
    }

    private async Task<HttpResponseMessage?> DeleteAsync(string path)
    {
        var client = Client;
        if (client == null)
            return null;

        using var timeout = new CancellationTokenSource(ApiTimeout);
        return await client.DeleteAsync(path, timeout.Token);
    }

    private async Task<DockerOverview> BuildOverviewAsync()
    {
        var (client, endpoint, _, problem) = transport.Value;
        var overview = new DockerOverview
        {
            Endpoint = endpoint
        };

        if (client == null)
        {
            overview.Availability = DockerAvailability.NotConfigured;
            overview.Message = problem;
            return overview;
        }

        try
        {
            using var version = await GetAsync("version");
            if (version == null || !version.IsSuccessStatusCode)
            {
                overview.Availability = DockerAvailability.Unreachable;
                overview.Message = $"The daemon answered {(int?)version?.StatusCode} to a version query";
                return overview;
            }

            using (var doc = JsonDocument.Parse(await version.Content.ReadAsStringAsync()))
            {
                overview.ServerVersion = ReadString(doc.RootElement, "Version");
                overview.OperatingSystem = ReadString(doc.RootElement, "Os");
                overview.Architecture = ReadString(doc.RootElement, "Arch");
            }

            using (var info = await GetAsync("info"))
            {
                if (info != null && info.IsSuccessStatusCode)
                {
                    using var doc = JsonDocument.Parse(await info.Content.ReadAsStringAsync());
                    overview.Images = (int)(ReadLong(doc.RootElement, "Images") ?? 0);
                    if (string.IsNullOrEmpty(overview.OperatingSystem))
                        overview.OperatingSystem = ReadString(doc.RootElement, "OperatingSystem");
                }
            }

            using var list = await GetAsync("containers/json?all=1");
            if (list == null || !list.IsSuccessStatusCode)
            {
                overview.Availability = DockerAvailability.Unreachable;
                overview.Message = $"The daemon answered {(int?)list?.StatusCode} to a container listing";
                return overview;
            }

            using (var doc = JsonDocument.Parse(await list.Content.ReadAsStringAsync()))
            {
                foreach (var element in doc.RootElement.EnumerateArray())
                {
                    var labels = ReadObject(element, "Labels");
                    if (ReadString(labels, JobLabel) == "1")
                        continue;

                    overview.Containers.Add(ParseContainer(element));
                }
            }

            var selfId = ResolveSelfContainerId();
            foreach (var container in overview.Containers)
            {
                container.IsSelf = selfId != null && container.Id.StartsWith(selfId, StringComparison.Ordinal);
                if (!ttyCache.TryGetValue(container.Id, out var tty))
                {
                    tty = await ReadTtyAsync(container.Id);
                    ttyCache[container.Id] = tty;
                }

                container.Tty = tty;
            }

            foreach (var gone in ttyCache.Keys.Except(overview.Containers.Select(c => c.Id)).ToList())
                ttyCache.Remove(gone);

            overview.Running = overview.Containers.Count(c => c.State == "running");
            overview.Stopped = overview.Containers.Count - overview.Running;
            overview.Containers = overview.Containers
                .OrderBy(c => c.ComposeProject ?? "\uffff")
                .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            overview.Projects = BuildProjects(overview.Containers);
            await FillProjectPathsAsync(overview);
            overview.ComposeAvailable = true;
            overview.Availability = DockerAvailability.Available;
            return overview;
        }
        catch (Exception ex) when (IsTransportError(ex) || ex is SocketException)
        {
            logger.LogDebug(ex, "Docker daemon at {Endpoint} could not be queried", endpoint);
            overview.Availability = DockerAvailability.Unreachable;
            overview.Message = ex is HttpRequestException { InnerException: SocketException se }
                ? $"Could not connect to the daemon: {se.Message}. Check the bot's user is in the docker group."
                : ex.Message;
            return overview;
        }
    }

    private async Task<bool> ReadTtyAsync(string id)
    {
        try
        {
            using var response = await GetAsync($"containers/{id}/json");
            if (response == null || !response.IsSuccessStatusCode)
                return false;

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var config = ReadObject(doc.RootElement, "Config");
            return config.ValueKind == JsonValueKind.Object && config.TryGetProperty("Tty", out var tty) &&
                   tty.ValueKind == JsonValueKind.True;
        }
        catch (Exception ex) when (IsTransportError(ex))
        {
            return false;
        }
    }

    private static DockerContainerInfo ParseContainer(JsonElement element)
    {
        var labels = ReadObject(element, "Labels");
        var status = ReadString(element, "Status") ?? "";

        var info = new DockerContainerInfo
        {
            Id = ReadString(element, "Id") ?? "",
            Name = FirstName(element),
            Image = ReadString(element, "Image") ?? "",
            State = ReadString(element, "State") ?? "",
            Status = status,
            Health = ParseHealth(status),
            CreatedAt = DateTimeOffset.FromUnixTimeSeconds(ReadLong(element, "Created") ?? 0).UtcDateTime,
            ComposeProject = ReadString(labels, "com.docker.compose.project"),
            ComposeService = ReadString(labels, "com.docker.compose.service")
        };

        if (element.TryGetProperty("Ports", out var ports) && ports.ValueKind == JsonValueKind.Array)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var port in ports.EnumerateArray())
            {
                var priv = ReadLong(port, "PrivatePort");
                var pub = ReadLong(port, "PublicPort");
                var type = ReadString(port, "Type") ?? "tcp";
                var text = pub.HasValue ? $"{pub}:{priv}/{type}" : $"{priv}/{type}";
                if (seen.Add(text))
                    info.Ports.Add(text);
            }
        }

        return info;
    }

    private static string FirstName(JsonElement element)
    {
        if (!element.TryGetProperty("Names", out var names) || names.ValueKind != JsonValueKind.Array)
            return "";

        foreach (var name in names.EnumerateArray())
        {
            var text = name.GetString();
            if (!string.IsNullOrEmpty(text))
                return text.TrimStart('/');
        }

        return "";
    }

    private static string? ParseHealth(string status)
    {
        var open = status.LastIndexOf('(');
        var close = status.LastIndexOf(')');
        if (open < 0 || close <= open)
            return null;

        var inner = status[(open + 1)..close].Trim().ToLowerInvariant();
        return inner is "healthy" or "unhealthy" or "health: starting" ? inner : null;
    }

    private static List<DockerComposeProject> BuildProjects(List<DockerContainerInfo> containers)
    {
        var byName = new Dictionary<string, DockerComposeProject>(StringComparer.Ordinal);
        foreach (var container in containers)
        {
            if (string.IsNullOrEmpty(container.ComposeProject))
                continue;

            if (!byName.TryGetValue(container.ComposeProject, out var project))
            {
                project = new DockerComposeProject
                {
                    Name = container.ComposeProject
                };
                byName[container.ComposeProject] = project;
            }

            project.Total++;
            if (container.State == "running")
                project.Running++;
        }

        return byName.Values.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    ///     Fills in the working directory and config files for each project from one of its containers, and
    ///     checks those files are visible from this process so compose can be run against them.
    /// </summary>
    private async Task FillProjectPathsAsync(DockerOverview overview)
    {
        foreach (var project in overview.Projects)
        {
            var sample = overview.Containers.FirstOrDefault(c => c.ComposeProject == project.Name);
            if (sample == null)
                continue;

            try
            {
                using var response = await GetAsync($"containers/{sample.Id}/json");
                if (response == null || !response.IsSuccessStatusCode)
                    continue;

                using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                var labels = ReadObject(ReadObject(doc.RootElement, "Config"), "Labels");
                project.WorkingDir = ReadString(labels, "com.docker.compose.project.working_dir");
                var files = ReadString(labels, "com.docker.compose.project.config_files");
                if (!string.IsNullOrWhiteSpace(files))
                    project.ConfigFiles = files
                        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .ToList();

                project.Operable = !string.IsNullOrWhiteSpace(project.WorkingDir) &&
                                   Directory.Exists(project.WorkingDir) &&
                                   project.ConfigFiles.Count > 0 &&
                                   project.ConfigFiles.All(File.Exists);
            }
            catch (Exception ex) when (IsTransportError(ex))
            {
                logger.LogDebug(ex, "Could not inspect {Id} for compose labels", sample.Id);
            }
        }
    }

    /// <summary>
    ///     Reads the id of the container this process runs in, when it runs in one. Docker sets the hostname to
    ///     the short id by default and lists the full id in the cgroup file, so both are tried.
    /// </summary>
    private string? ResolveSelfContainerId()
    {
        if (selfResolved)
            return selfContainerId;

        selfResolved = true;
        if (!File.Exists("/.dockerenv") && !File.Exists("/proc/self/cgroup"))
            return null;

        try
        {
            if (File.Exists("/proc/self/cgroup"))
            {
                foreach (var line in File.ReadLines("/proc/self/cgroup"))
                {
                    var idx = line.IndexOf("docker", StringComparison.Ordinal);
                    if (idx < 0)
                        continue;

                    var tail = line[idx..].Split('/', '-', '.').FirstOrDefault(s => s.Length == 64 && IsHex(s));
                    if (tail != null)
                    {
                        selfContainerId = tail;
                        return selfContainerId;
                    }
                }
            }

            if (File.Exists("/.dockerenv"))
            {
                var hostname = Environment.MachineName;
                if (hostname.Length == 12 && IsHex(hostname))
                    selfContainerId = hostname;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogDebug(ex, "Could not read cgroup information to find own container");
        }

        return selfContainerId;
    }

    private static bool IsHex(string value)
    {
        return value.All(char.IsAsciiHexDigit);
    }

    private static bool IsTransportError(Exception ex)
    {
        return ex is HttpRequestException or TaskCanceledException or OperationCanceledException or JsonException
            or IOException;
    }

    private static DockerActionResult Failure(string message)
    {
        return new DockerActionResult
        {
            Success = false, Message = message
        };
    }

    private static string Quote(string value)
    {
        return "'" + value.Replace("'", "'\\''") + "'";
    }

    /// <summary>
    ///     Splits the frames the daemon writes for a container without a TTY: an eight byte header carrying the
    ///     stream and payload length, then the payload. Partial lines are carried across frames per stream.
    /// </summary>
    public static List<DockerLogLine> ParseMultiplexedLog(byte[] payload)
    {
        var lines = new List<DockerLogLine>();
        var partial = new Dictionary<int, StringBuilder>();
        var offset = 0;

        while (offset + 8 <= payload.Length)
        {
            var stream = payload[offset];
            var length = (int)BinaryPrimitives.ReadUInt32BigEndian(payload.AsSpan(offset + 4, 4));
            offset += 8;
            if (length < 0 || offset + length > payload.Length)
                length = payload.Length - offset;

            var text = Encoding.UTF8.GetString(payload, offset, length);
            offset += length;

            if (!partial.TryGetValue(stream, out var buffer))
            {
                buffer = new StringBuilder();
                partial[stream] = buffer;
            }

            buffer.Append(text);
            FlushLines(buffer, stream == 2, lines);
        }

        foreach (var (stream, buffer) in partial)
        {
            if (buffer.Length > 0)
                lines.Add(ToLine(buffer.ToString(), stream == 2));
        }

        return lines.OrderBy(l => l.Timestamp, StringComparer.Ordinal).ToList();
    }

    /// <summary>
    ///     Splits the raw byte stream of a container started with a TTY, which has no frame headers, into
    ///     lines.
    /// </summary>
    public static List<DockerLogLine> ParseTtyLog(byte[] payload)
    {
        var lines = new List<DockerLogLine>();
        var buffer = new StringBuilder(Encoding.UTF8.GetString(payload));
        FlushLines(buffer, false, lines);
        if (buffer.Length > 0)
            lines.Add(ToLine(buffer.ToString(), false));
        return lines;
    }

    private static void FlushLines(StringBuilder buffer, bool isError, List<DockerLogLine> lines)
    {
        while (true)
        {
            var text = buffer.ToString();
            var newline = text.IndexOf('\n');
            if (newline < 0)
                return;

            lines.Add(ToLine(text[..newline], isError));
            buffer.Remove(0, newline + 1);
        }
    }

    private static DockerLogLine ToLine(string raw, bool isError)
    {
        var text = raw.TrimEnd('\r');
        var space = text.IndexOf(' ');
        if (space > 0 && space < 40 && text[..space].Contains('T') && text[..space].Contains(':'))
            return new DockerLogLine
            {
                Timestamp = text[..space], IsError = isError, Text = text[(space + 1)..]
            };

        return new DockerLogLine
        {
            Timestamp = "", IsError = isError, Text = text
        };
    }

    private static string? ExtractDaemonMessage(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(body);
            return ReadString(doc.RootElement, "message") ?? body.Trim();
        }
        catch (JsonException)
        {
            return body.Trim();
        }
    }

    private static JsonElement ReadObject(JsonElement element, string name)
    {
        return element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) &&
               value.ValueKind == JsonValueKind.Object
            ? value
            : default;
    }

    private static string? ReadString(JsonElement element, string name)
    {
        return element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) &&
               value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static long? ReadLong(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name, out var value))
            return null;

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt64(out var l) => l,
            JsonValueKind.Number when value.TryGetDouble(out var d) => (long)d,
            _ => null
        };
    }
}
