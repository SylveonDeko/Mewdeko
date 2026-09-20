using System.Buffers.Binary;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using Mewdeko.Modules.OwnerOnly.Common;

namespace Mewdeko.Modules.OwnerOnly.Services;

/// <summary>
///     Talks to the Docker daemon on the host this instance runs on, so bot owners can see and drive the
///     containers there from the dashboard. Container level work goes straight to the Engine API over the
///     daemon's socket, which needs no CLI. Project level work (pull, up) shells out to <c>docker compose</c>
///     because the compose model lives in the CLI, and runs as a background job the dashboard polls, since a
///     pull can take minutes.
/// </summary>
public sealed class DockerService : INService, IDisposable
{
    /// <summary>
    ///     Upper bound on how many log lines one request returns.
    /// </summary>
    public const int MaxTail = 2000;

    private const int MaxJobs = 50;
    private const int MaxJobOutputLines = 4000;
    private static readonly TimeSpan ApiTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan OverviewTtl = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan ComposeTimeout = TimeSpan.FromMinutes(20);

    private readonly ConcurrentDictionary<string, DockerJob> jobs = new();
    private readonly ConcurrentDictionary<string, bool> ttyCache = new();
    private readonly ILogger<DockerService> logger;
    private readonly SemaphoreSlim overviewLock = new(1, 1);
    private readonly Lazy<(HttpClient? Client, string? Endpoint, string? Problem)> transport;
    private DockerOverview? cachedOverview;
    private DateTime cachedOverviewAt = DateTime.MinValue;
    private bool? composeAvailable;
    private string? selfContainerId;
    private bool selfResolved;

    /// <summary>
    ///     Initializes a new instance of the <see cref="DockerService" /> class.
    /// </summary>
    /// <param name="logger">The logger instance for structured logging.</param>
    public DockerService(ILogger<DockerService> logger)
    {
        this.logger = logger;
        transport = new Lazy<(HttpClient?, string?, string?)>(CreateTransport);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (transport.IsValueCreated)
            transport.Value.Client?.Dispose();
        overviewLock.Dispose();
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
    ///     Takes a single resource sample for a running container.
    /// </summary>
    /// <param name="container">The container to sample.</param>
    /// <returns>The sample, or null when the daemon could not provide one.</returns>
    public async Task<DockerContainerStats?> GetStatsAsync(DockerContainerInfo container)
    {
        var client = Client;
        if (client == null)
            return null;

        try
        {
            using var response = await client.GetAsync($"containers/{container.Id}/stats?stream=false");
            if (!response.IsSuccessStatusCode)
                return null;

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = doc.RootElement;

            var cpu = root.TryGetProperty("cpu_stats", out var c) ? c : default;
            var preCpu = root.TryGetProperty("precpu_stats", out var pc) ? pc : default;
            var memory = root.TryGetProperty("memory_stats", out var m) ? m : default;

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
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException
                                       or IOException)
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
        var client = Client;
        if (client == null)
            return null;

        tail = Math.Clamp(tail, 1, MaxTail);
        var query = new StringBuilder($"containers/{container.Id}/logs?stdout=1&stderr=1&timestamps=1");
        if (!string.IsNullOrWhiteSpace(since))
            query.Append("&since=").Append(Uri.EscapeDataString(since));
        else
            query.Append("&tail=").Append(tail);

        try
        {
            using var response = await client.GetAsync(query.ToString());
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Docker refused logs for {Id}: {Status}", container.Id, response.StatusCode);
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
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
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
        var client = Client;
        if (client == null)
            return new DockerActionResult
            {
                Success = false, Message = "Docker is not available on this host"
            };

        var path = action switch
        {
            "start" => $"containers/{container.Id}/start",
            "stop" => $"containers/{container.Id}/stop?t=15",
            "restart" => $"containers/{container.Id}/restart?t=15",
            _ => null
        };
        if (path == null)
            return new DockerActionResult
            {
                Success = false, Message = "Unknown action"
            };

        try
        {
            using var response = await client.PostAsync(path, null);
            InvalidateOverview();

            if (response.IsSuccessStatusCode)
                return new DockerActionResult
                {
                    Success = true, Message = $"{action} accepted"
                };

            if ((int)response.StatusCode == 304)
                return new DockerActionResult
                {
                    Success = true, Message = $"Container was already {(action == "stop" ? "stopped" : "running")}"
                };

            var body = await response.Content.ReadAsStringAsync();
            return new DockerActionResult
            {
                Success = false, Message = ExtractDaemonMessage(body) ?? $"Docker answered {(int)response.StatusCode}"
            };
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
        {
            logger.LogWarning(ex, "Docker {Action} failed for {Id}", action, container.Id);
            return new DockerActionResult
            {
                Success = false, Message = ex.Message
            };
        }
    }

    /// <summary>
    ///     Starts a compose operation on a project in the background.
    /// </summary>
    /// <param name="project">The project, as discovered from container labels.</param>
    /// <param name="operation">pull, up, or update (pull followed by up).</param>
    /// <returns>The job to poll, or null when the operation is unknown or the project cannot be operated on.</returns>
    public DockerJob? StartComposeJob(DockerComposeProject project, string operation)
    {
        if (!project.Operable || string.IsNullOrWhiteSpace(project.WorkingDir))
            return null;

        var arguments = new List<string>
        {
            "compose", "--project-name", project.Name, "--project-directory", project.WorkingDir
        };
        foreach (var file in project.ConfigFiles)
        {
            arguments.Add("-f");
            arguments.Add(file);
        }

        List<string[]> steps;
        switch (operation)
        {
            case "pull":
                steps =
                [
                    ["pull"]
                ];
                break;
            case "up":
                steps =
                [
                    ["up", "-d", "--remove-orphans"]
                ];
                break;
            case "update":
                steps =
                [
                    ["pull"], ["up", "-d", "--remove-orphans"]
                ];
                break;
            case "build":
                steps =
                [
                    ["build", "--pull"], ["up", "-d", "--remove-orphans"]
                ];
                break;
            default:
                return null;
        }

        var job = new DockerJob
        {
            Id = Guid.NewGuid().ToString("N")[..12],
            Project = project.Name,
            Operation = operation,
            Command = "docker " + string.Join(' ', arguments) + " " +
                      string.Join(" && docker compose ... ", steps.Select(s => string.Join(' ', s))),
            Status = DockerJobStatus.Running,
            StartedAt = DateTime.UtcNow
        };

        TrimJobs();
        jobs[job.Id] = job;
        _ = Task.Run(() => RunComposeStepsAsync(job, arguments, steps));
        return job;
    }

    /// <summary>
    ///     Looks up a background job.
    /// </summary>
    /// <param name="id">The job id.</param>
    /// <returns>The job, or null when it is unknown or has been trimmed.</returns>
    public DockerJob? FindJob(string id)
    {
        return jobs.TryGetValue(id, out var job) ? job : null;
    }

    /// <summary>
    ///     Lists the jobs still in memory, newest first.
    /// </summary>
    public List<DockerJob> ListJobs()
    {
        return jobs.Values.OrderByDescending(j => j.StartedAt).ToList();
    }

    private HttpClient? Client
    {
        get
        {
            return transport.Value.Client;
        }
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
    private (HttpClient? Client, string? Endpoint, string? Problem) CreateTransport()
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
                    BaseAddress = new Uri(url), Timeout = ApiTimeout
                }, configured, null);
            }

            return (null, configured, $"DOCKER_HOST '{configured}' is not a unix:// or tcp:// address");
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

        return (null, null, "No Docker socket was found. Mount /var/run/docker.sock into the bot or set DOCKER_HOST.");
    }

    private static (HttpClient, string, string?) BuildUnixTransport(string socketPath)
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
            BaseAddress = new Uri("http://docker/"), Timeout = ApiTimeout
        }, "unix://" + socketPath, null);
    }

    private async Task<DockerOverview> BuildOverviewAsync()
    {
        var (client, endpoint, problem) = transport.Value;
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
            using var version = await client.GetAsync("version");
            if (!version.IsSuccessStatusCode)
            {
                overview.Availability = DockerAvailability.Unreachable;
                overview.Message = $"The daemon answered {(int)version.StatusCode} to a version query";
                return overview;
            }

            using (var doc = JsonDocument.Parse(await version.Content.ReadAsStringAsync()))
            {
                overview.ServerVersion = ReadString(doc.RootElement, "Version");
                overview.OperatingSystem = ReadString(doc.RootElement, "Os");
                overview.Architecture = ReadString(doc.RootElement, "Arch");
            }

            using (var info = await client.GetAsync("info"))
            {
                if (info.IsSuccessStatusCode)
                {
                    using var doc = JsonDocument.Parse(await info.Content.ReadAsStringAsync());
                    overview.Images = (int)(ReadLong(doc.RootElement, "Images") ?? 0);
                    if (string.IsNullOrEmpty(overview.OperatingSystem))
                        overview.OperatingSystem = ReadString(doc.RootElement, "OperatingSystem");
                }
            }

            using var list = await client.GetAsync("containers/json?all=1");
            if (!list.IsSuccessStatusCode)
            {
                overview.Availability = DockerAvailability.Unreachable;
                overview.Message = $"The daemon answered {(int)list.StatusCode} to a container listing";
                return overview;
            }

            using (var doc = JsonDocument.Parse(await list.Content.ReadAsStringAsync()))
            {
                foreach (var element in doc.RootElement.EnumerateArray())
                    overview.Containers.Add(ParseContainer(element));
            }

            var selfId = ResolveSelfContainerId();
            foreach (var container in overview.Containers)
            {
                container.IsSelf = selfId != null && container.Id.StartsWith(selfId, StringComparison.Ordinal);
                if (!ttyCache.TryGetValue(container.Id, out var tty))
                {
                    tty = await ReadTtyAsync(client, container.Id);
                    ttyCache[container.Id] = tty;
                }

                container.Tty = tty;
            }

            foreach (var gone in ttyCache.Keys.Except(overview.Containers.Select(c => c.Id)).ToList())
                ttyCache.TryRemove(gone, out _);

            overview.Running = overview.Containers.Count(c => c.State == "running");
            overview.Stopped = overview.Containers.Count - overview.Running;
            overview.Containers = overview.Containers
                .OrderBy(c => c.ComposeProject ?? "￿")
                .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            overview.Projects = BuildProjects(overview.Containers);
            await FillProjectPathsAsync(overview);
            overview.ComposeAvailable = await ComposeAvailableAsync();
            overview.Availability = DockerAvailability.Available;
            return overview;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException
                                       or IOException or SocketException)
        {
            logger.LogDebug(ex, "Docker daemon at {Endpoint} could not be queried", endpoint);
            overview.Availability = DockerAvailability.Unreachable;
            overview.Message = ex is HttpRequestException { InnerException: SocketException se }
                ? $"Could not connect to the daemon: {se.Message}. Check the bot's user is in the docker group."
                : ex.Message;
            return overview;
        }
    }

    private static async Task<bool> ReadTtyAsync(HttpClient client, string id)
    {
        try
        {
            using var response = await client.GetAsync($"containers/{id}/json");
            if (!response.IsSuccessStatusCode)
                return false;

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return doc.RootElement.TryGetProperty("Config", out var config) &&
                   config.TryGetProperty("Tty", out var tty) && tty.ValueKind == JsonValueKind.True;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException
                                       or IOException)
        {
            return false;
        }
    }

    private static DockerContainerInfo ParseContainer(JsonElement element)
    {
        var labels = element.TryGetProperty("Labels", out var l) && l.ValueKind == JsonValueKind.Object
            ? l
            : default;
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
        var client = Client;
        if (client == null)
            return;

        foreach (var project in overview.Projects)
        {
            var sample = overview.Containers.FirstOrDefault(c => c.ComposeProject == project.Name);
            if (sample == null)
                continue;

            try
            {
                using var response = await client.GetAsync($"containers/{sample.Id}/json");
                if (!response.IsSuccessStatusCode)
                    continue;

                using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                var labels = ReadObject(ReadObject(doc.RootElement, "Config"), "Labels");
                project.WorkingDir = ReadString(labels, "com.docker.compose.project.working_dir");
                var files = ReadString(labels, "com.docker.compose.project.config_files");
                if (!string.IsNullOrWhiteSpace(files))
                    project.ConfigFiles = files.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .ToList();

                project.Operable = !string.IsNullOrWhiteSpace(project.WorkingDir) &&
                                   Directory.Exists(project.WorkingDir) &&
                                   project.ConfigFiles.Count > 0 &&
                                   project.ConfigFiles.All(File.Exists);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException
                                           or IOException)
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
        return value.All(c => char.IsAsciiHexDigit(c));
    }

    private async Task<bool> ComposeAvailableAsync()
    {
        if (composeAvailable.HasValue)
            return composeAvailable.Value;

        try
        {
            var info = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = "compose version",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using var process = Process.Start(info);
            if (process == null)
            {
                composeAvailable = false;
                return false;
            }

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await process.WaitForExitAsync(timeout.Token);
            composeAvailable = process.ExitCode == 0;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException
                                       or OperationCanceledException)
        {
            logger.LogDebug(ex, "docker compose is not available on this host");
            composeAvailable = false;
        }

        return composeAvailable.Value;
    }

    private async Task RunComposeStepsAsync(DockerJob job, List<string> baseArguments, List<string[]> steps)
    {
        try
        {
            foreach (var step in steps)
            {
                AppendOutput(job, $"$ docker {string.Join(' ', baseArguments)} {string.Join(' ', step)}");
                var exit = await RunComposeStepAsync(job, baseArguments, step);
                job.ExitCode = exit;
                if (exit != 0)
                {
                    job.Status = DockerJobStatus.Failed;
                    job.FinishedAt = DateTime.UtcNow;
                    InvalidateOverview();
                    return;
                }
            }

            job.Status = DockerJobStatus.Succeeded;
        }
        catch (Exception ex)
        {
            AppendOutput(job, $"error: {ex.Message}");
            job.Status = DockerJobStatus.Failed;
            logger.LogWarning(ex, "Compose job {Job} for {Project} failed", job.Id, job.Project);
        }
        finally
        {
            job.FinishedAt ??= DateTime.UtcNow;
            InvalidateOverview();
        }
    }

    private async Task<int> RunComposeStepAsync(DockerJob job, List<string> baseArguments, string[] step)
    {
        var info = new ProcessStartInfo
        {
            FileName = "docker",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var argument in baseArguments)
            info.ArgumentList.Add(argument);
        foreach (var argument in step)
            info.ArgumentList.Add(argument);

        using var process = new Process();
        process.StartInfo = info;
        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null) AppendOutput(job, e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null) AppendOutput(job, e.Data);
        };

        if (!process.Start())
        {
            AppendOutput(job, "error: docker could not be started");
            return -1;
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var timeout = new CancellationTokenSource(ComposeTimeout);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            AppendOutput(job, $"error: timed out after {ComposeTimeout.TotalMinutes} minutes, killing docker");
            try
            {
                process.Kill(true);
            }
            catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
            {
            }

            return -1;
        }

        return process.ExitCode;
    }

    private static void AppendOutput(DockerJob job, string line)
    {
        lock (job.Output)
        {
            job.Output.Add(line);
            if (job.Output.Count > MaxJobOutputLines)
                job.Output.RemoveRange(0, job.Output.Count - MaxJobOutputLines);
        }
    }

    private void TrimJobs()
    {
        if (jobs.Count < MaxJobs)
            return;

        foreach (var stale in jobs.Values.Where(j => j.Status != DockerJobStatus.Running)
                     .OrderBy(j => j.StartedAt).Take(jobs.Count - MaxJobs + 1))
            jobs.TryRemove(stale.Id, out _);
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
