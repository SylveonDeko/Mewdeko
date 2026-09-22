namespace Mewdeko.Modules.OwnerOnly.Common;

/// <summary>
///     Whether the bot can reach a Docker daemon, and if not, why.
/// </summary>
public enum DockerAvailability
{
    /// <summary>
    ///     The daemon answered and containers can be listed.
    /// </summary>
    Available,

    /// <summary>
    ///     No socket or DOCKER_HOST could be found on this host.
    /// </summary>
    NotConfigured,

    /// <summary>
    ///     The socket exists but the daemon refused or did not answer, most often a permissions problem.
    /// </summary>
    Unreachable
}

/// <summary>
///     What the Docker daemon on this host looks like, with every container it knows about.
/// </summary>
public sealed class DockerOverview
{
    /// <summary>
    ///     Whether the daemon could be reached.
    /// </summary>
    public DockerAvailability Availability { get; set; }

    /// <summary>
    ///     A human readable explanation when the daemon is not available.
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    ///     Where the bot connected, such as unix:///var/run/docker.sock.
    /// </summary>
    public string? Endpoint { get; set; }

    /// <summary>
    ///     The daemon's reported version.
    /// </summary>
    public string? ServerVersion { get; set; }

    /// <summary>
    ///     The daemon's operating system string.
    /// </summary>
    public string? OperatingSystem { get; set; }

    /// <summary>
    ///     The daemon host's architecture.
    /// </summary>
    public string? Architecture { get; set; }

    /// <summary>
    ///     How many containers are running.
    /// </summary>
    public int Running { get; set; }

    /// <summary>
    ///     How many containers exist but are stopped.
    /// </summary>
    public int Stopped { get; set; }

    /// <summary>
    ///     How many images the daemon holds.
    /// </summary>
    public int Images { get; set; }

    /// <summary>
    ///     Whether the docker CLI with the compose plugin is available for project wide operations.
    /// </summary>
    public bool ComposeAvailable { get; set; }

    /// <summary>
    ///     Every container, running or not.
    /// </summary>
    public List<DockerContainerInfo> Containers { get; set; } = [];

    /// <summary>
    ///     The compose projects the containers belong to.
    /// </summary>
    public List<DockerComposeProject> Projects { get; set; } = [];
}

/// <summary>
///     A compose project as reconstructed from container labels.
/// </summary>
public sealed class DockerComposeProject
{
    /// <summary>
    ///     The project name.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    ///     The directory the project was brought up from.
    /// </summary>
    public string? WorkingDir { get; set; }

    /// <summary>
    ///     The compose files that define the project, as recorded on its containers.
    /// </summary>
    public List<string> ConfigFiles { get; set; } = [];

    /// <summary>
    ///     How many of the project's containers are running.
    /// </summary>
    public int Running { get; set; }

    /// <summary>
    ///     How many containers the project has in total.
    /// </summary>
    public int Total { get; set; }

    /// <summary>
    ///     Whether the compose files still exist where the labels say, so up and pull can be run.
    /// </summary>
    public bool Operable { get; set; }
}

/// <summary>
///     One container as the daemon lists it.
/// </summary>
public sealed class DockerContainerInfo
{
    /// <summary>
    ///     The full container id.
    /// </summary>
    public string Id { get; set; } = "";

    /// <summary>
    ///     The container name without the leading slash.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    ///     The image the container was created from.
    /// </summary>
    public string Image { get; set; } = "";

    /// <summary>
    ///     The daemon's state word: running, exited, paused, restarting, created, dead.
    /// </summary>
    public string State { get; set; } = "";

    /// <summary>
    ///     The daemon's human readable status, such as "Up 4 days (healthy)".
    /// </summary>
    public string Status { get; set; } = "";

    /// <summary>
    ///     The healthcheck verdict parsed out of the status, or null when the container has no healthcheck.
    /// </summary>
    public string? Health { get; set; }

    /// <summary>
    ///     When the container was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    ///     The compose project the container belongs to, from its labels.
    /// </summary>
    public string? ComposeProject { get; set; }

    /// <summary>
    ///     The compose service name, from its labels.
    /// </summary>
    public string? ComposeService { get; set; }

    /// <summary>
    ///     Published ports in "host:container/proto" form.
    /// </summary>
    public List<string> Ports { get; set; } = [];

    /// <summary>
    ///     Whether this is the container the answering bot instance runs in.
    /// </summary>
    public bool IsSelf { get; set; }

    /// <summary>
    ///     Whether the container was started with a TTY, which changes how its log stream is framed.
    /// </summary>
    public bool Tty { get; set; }
}

/// <summary>
///     A one shot resource sample for a running container.
/// </summary>
public sealed class DockerContainerStats
{
    /// <summary>
    ///     The container id the sample is for.
    /// </summary>
    public string Id { get; set; } = "";

    /// <summary>
    ///     Percentage of the host's total CPU the container used between the two samples.
    /// </summary>
    public double CpuPercent { get; set; }

    /// <summary>
    ///     Memory in use, in bytes, with the page cache excluded where the daemon reports it.
    /// </summary>
    public long MemoryBytes { get; set; }

    /// <summary>
    ///     The memory limit in bytes, which is the host's total when the container is unlimited.
    /// </summary>
    public long MemoryLimitBytes { get; set; }

    /// <summary>
    ///     Bytes received over all of the container's interfaces since it started.
    /// </summary>
    public long NetworkRxBytes { get; set; }

    /// <summary>
    ///     Bytes sent over all of the container's interfaces since it started.
    /// </summary>
    public long NetworkTxBytes { get; set; }

    /// <summary>
    ///     How many processes the container is running.
    /// </summary>
    public long Pids { get; set; }

    /// <summary>
    ///     When the sample was taken.
    /// </summary>
    public DateTime SampledAt { get; set; }
}

/// <summary>
///     One line of a container's log with the timestamp the daemon stamped it with.
/// </summary>
public sealed class DockerLogLine
{
    /// <summary>
    ///     The daemon's timestamp for the line, in RFC 3339 form with nanoseconds, echoed back as received so
    ///     it can be passed to the next request unchanged.
    /// </summary>
    public string Timestamp { get; set; } = "";

    /// <summary>
    ///     Whether the line came from stderr rather than stdout.
    /// </summary>
    public bool IsError { get; set; }

    /// <summary>
    ///     The line itself, with the trailing newline removed and ANSI colour codes left in place.
    /// </summary>
    public string Text { get; set; } = "";
}

/// <summary>
///     A run of log lines plus the cursor needed to ask for what comes after them.
/// </summary>
public sealed class DockerLogChunk
{
    /// <summary>
    ///     The container the lines came from.
    /// </summary>
    public string ContainerId { get; set; } = "";

    /// <summary>
    ///     The lines, oldest first.
    /// </summary>
    public List<DockerLogLine> Lines { get; set; } = [];

    /// <summary>
    ///     The timestamp of the last line, to pass as since on the next request. Null when nothing was returned.
    /// </summary>
    public string? Cursor { get; set; }
}

/// <summary>
///     What happened when the daemon was asked to change a container's state.
/// </summary>
public sealed class DockerActionResult
{
    /// <summary>
    ///     Whether the daemon accepted the request.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    ///     The daemon's message when it did not, or a note when it did.
    /// </summary>
    public string? Message { get; set; }
}

/// <summary>
///     The newest image published for the bot, as the registry reports it.
/// </summary>
public sealed class DockerPublishedImage
{
    /// <summary>
    ///     The repository that was queried, such as sylveondeko/mewdeko.
    /// </summary>
    public string Repository { get; set; } = "";

    /// <summary>
    ///     The moving tag that was resolved, normally nightly.
    /// </summary>
    public string Tag { get; set; } = "nightly";

    /// <summary>
    ///     The commit the tag currently points at, when the registry also carries a sha tag for the same digest.
    /// </summary>
    public string? GitSha { get; set; }

    /// <summary>
    ///     The manifest digest the tag points at.
    /// </summary>
    public string? Digest { get; set; }

    /// <summary>
    ///     When the tag was last pushed.
    /// </summary>
    public DateTime? PublishedAt { get; set; }

    /// <summary>
    ///     When the registry was last asked.
    /// </summary>
    public DateTime CheckedAt { get; set; }

    /// <summary>
    ///     Why the registry could not be queried, or null when it answered.
    /// </summary>
    public string? Error { get; set; }
}

/// <summary>
///     What the answering bot instance runs: its build, the container it lives in, and whether a newer image
///     has been published.
/// </summary>
public sealed class DockerSelfInfo
{
    /// <summary>
    ///     The bot's declared version string.
    /// </summary>
    public string BotVersion { get; set; } = "";

    /// <summary>
    ///     The commit the running build came from, or null when unknown.
    /// </summary>
    public string? GitSha { get; set; }

    /// <summary>
    ///     When the running image was built, or null outside CI built images.
    /// </summary>
    public DateTime? BuildDate { get; set; }

    /// <summary>
    ///     When this process started.
    /// </summary>
    public DateTime StartedAt { get; set; }

    /// <summary>
    ///     The container this instance runs in, or null when it runs directly on the host.
    /// </summary>
    public DockerContainerInfo? Container { get; set; }

    /// <summary>
    ///     The compose project the container belongs to, when it was brought up by compose.
    /// </summary>
    public DockerComposeProject? Project { get; set; }

    /// <summary>
    ///     The newest published image, or null when the registry could not be reached.
    /// </summary>
    public DockerPublishedImage? Published { get; set; }

    /// <summary>
    ///     True when the published commit differs from the running one, false when they match, null when either
    ///     side is unknown.
    /// </summary>
    public bool? UpdateAvailable { get; set; }

    /// <summary>
    ///     Whether this instance can be updated from the dashboard: it runs in a compose managed container
    ///     whose project files are visible to the bot.
    /// </summary>
    public bool CanUpdate { get; set; }

    /// <summary>
    ///     Why <see cref="CanUpdate" /> is false, for the dashboard to show.
    /// </summary>
    public string? UpdateBlockedReason { get; set; }
}

/// <summary>
///     Where a long running compose operation is up to.
/// </summary>
public enum DockerJobStatus
{
    /// <summary>
    ///     Still running.
    /// </summary>
    Running,

    /// <summary>
    ///     Finished with a zero exit code.
    /// </summary>
    Succeeded,

    /// <summary>
    ///     Finished with a non zero exit code or could not be started.
    /// </summary>
    Failed
}

/// <summary>
///     A compose operation run in a helper container, with the output it has produced so far. Running it in
///     its own container rather than inside the bot means it survives the bot's own container being
///     recreated by the very update it is performing.
/// </summary>
public sealed class DockerJob
{
    /// <summary>
    ///     The helper container's id, which is also the job id to poll with.
    /// </summary>
    public string Id { get; set; } = "";

    /// <summary>
    ///     The helper container's name.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    ///     The compose services the job was limited to, empty for the whole project.
    /// </summary>
    public List<string> Services { get; set; } = [];

    /// <summary>
    ///     The compose project the job operates on.
    /// </summary>
    public string Project { get; set; } = "";

    /// <summary>
    ///     What the job does, such as "pull" or "up".
    /// </summary>
    public string Operation { get; set; } = "";

    /// <summary>
    ///     The command line that was run.
    /// </summary>
    public string Command { get; set; } = "";

    /// <summary>
    ///     Where the job is up to.
    /// </summary>
    public DockerJobStatus Status { get; set; }

    /// <summary>
    ///     When the job started.
    /// </summary>
    public DateTime StartedAt { get; set; }

    /// <summary>
    ///     When the job finished, or null while it runs.
    /// </summary>
    public DateTime? FinishedAt { get; set; }

    /// <summary>
    ///     The process exit code once finished.
    /// </summary>
    public int? ExitCode { get; set; }

    /// <summary>
    ///     Combined stdout and stderr, oldest first.
    /// </summary>
    public List<string> Output { get; set; } = [];
}
