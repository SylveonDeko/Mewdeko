using System.Text.Json.Serialization;

namespace Mewdeko.Modules.Utility.Common;

/// <summary>
///     One language the run command can execute, as read from the language data file.
/// </summary>
public sealed class CodeRunLanguage
{
    /// <summary>
    ///     Gets or sets the canonical name members see.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    ///     Gets or sets the other names that select this language.
    /// </summary>
    public List<string> Aliases { get; set; } = [];

    /// <summary>
    ///     Gets or sets the Docker image with a pinned tag.
    /// </summary>
    public string Image { get; set; } = "";

    /// <summary>
    ///     Gets or sets the file the code is written to.
    /// </summary>
    public string FileName { get; set; } = "";

    /// <summary>
    ///     Gets or sets the command that builds the code, or null for interpreted languages.
    /// </summary>
    public string? Compile { get; set; }

    /// <summary>
    ///     Gets or sets the command that runs the code.
    /// </summary>
    public string Run { get; set; } = "";

    /// <summary>
    ///     Gets or sets extra environment variables the toolchain needs.
    /// </summary>
    public Dictionary<string, string> Env { get; set; } = new();

    /// <summary>
    ///     Gets the wall clock limit: 10 seconds for interpreted languages, 20 for compile plus run.
    /// </summary>
    [JsonIgnore]
    public int DeadlineSeconds
    {
        get
        {
            return string.IsNullOrWhiteSpace(Compile) ? 10 : 20;
        }
    }
}

/// <summary>
///     Settings that vary per run and per host.
/// </summary>
/// <param name="UserId">The member who asked for the run.</param>
/// <param name="Runtime">The sandbox runtime, or empty for Docker's default.</param>
public sealed record CodeRunOptions(ulong UserId, string Runtime);

/// <summary>
///     The Docker container create request for one run.
/// </summary>
public sealed class CodeRunContainerRequest
{
    /// <summary>
    ///     Gets or sets the image.
    /// </summary>
    public string Image { get; set; } = "";

    /// <summary>
    ///     Gets or sets the entrypoint.
    /// </summary>
    public List<string> Entrypoint { get; set; } = [];

    /// <summary>
    ///     Gets or sets the command.
    /// </summary>
    public List<string> Cmd { get; set; } = [];

    /// <summary>
    ///     Gets or sets the environment as NAME=value entries.
    /// </summary>
    public List<string> Env { get; set; } = [];

    /// <summary>
    ///     Gets or sets the user and group the process runs as.
    /// </summary>
    public string User { get; set; } = "";

    /// <summary>
    ///     Gets or sets the working directory.
    /// </summary>
    public string WorkingDir { get; set; } = "";

    /// <summary>
    ///     Gets or sets whether networking is disabled.
    /// </summary>
    public bool NetworkDisabled { get; set; }

    /// <summary>
    ///     Gets or sets whether stdin is attached.
    /// </summary>
    public bool AttachStdin { get; set; }

    /// <summary>
    ///     Gets or sets whether stdin stays open.
    /// </summary>
    public bool OpenStdin { get; set; }

    /// <summary>
    ///     Gets or sets whether a terminal is allocated.
    /// </summary>
    public bool Tty { get; set; }

    /// <summary>
    ///     Gets or sets the labels.
    /// </summary>
    public Dictionary<string, string> Labels { get; set; } = new();

    /// <summary>
    ///     Gets or sets the host level restrictions.
    /// </summary>
    public CodeRunHostConfig HostConfig { get; set; } = new();
}

/// <summary>
///     The host level part of a run's container request.
/// </summary>
public sealed class CodeRunHostConfig
{
    /// <summary>
    ///     Gets or sets the network mode.
    /// </summary>
    public string NetworkMode { get; set; } = "";

    /// <summary>
    ///     Gets or sets whether the root filesystem is read only.
    /// </summary>
    public bool ReadonlyRootfs { get; set; }

    /// <summary>
    ///     Gets or sets the capabilities to drop.
    /// </summary>
    public List<string> CapDrop { get; set; } = [];

    /// <summary>
    ///     Gets or sets the security options.
    /// </summary>
    public List<string> SecurityOpt { get; set; } = [];

    /// <summary>
    ///     Gets or sets whether the container is privileged.
    /// </summary>
    public bool Privileged { get; set; }

    /// <summary>
    ///     Gets or sets the IPC mode.
    /// </summary>
    public string IpcMode { get; set; } = "";

    /// <summary>
    ///     Gets or sets the process limit.
    /// </summary>
    public long PidsLimit { get; set; }

    /// <summary>
    ///     Gets or sets the memory limit in bytes.
    /// </summary>
    public long Memory { get; set; }

    /// <summary>
    ///     Gets or sets the memory plus swap limit in bytes.
    /// </summary>
    public long MemorySwap { get; set; }

    /// <summary>
    ///     Gets or sets the CPU limit in billionths of a CPU.
    /// </summary>
    public long NanoCpus { get; set; }

    /// <summary>
    ///     Gets or sets the resource limits.
    /// </summary>
    public List<CodeRunUlimit> Ulimits { get; set; } = [];

    /// <summary>
    ///     Gets or sets the tmpfs mounts and their options.
    /// </summary>
    public Dictionary<string, string> Tmpfs { get; set; } = new();

    /// <summary>
    ///     Gets or sets whether Docker removes the container on exit.
    /// </summary>
    public bool AutoRemove { get; set; }

    /// <summary>
    ///     Gets or sets the restart policy.
    /// </summary>
    public CodeRunRestartPolicy RestartPolicy { get; set; } = new();

    /// <summary>
    ///     Gets or sets the log driver settings that bound how much output the daemon keeps.
    /// </summary>
    public CodeRunLogConfig LogConfig { get; set; } = new();

    /// <summary>
    ///     Gets or sets the container runtime, or null for Docker's default.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Runtime { get; set; }
}

/// <summary>
///     One resource limit.
/// </summary>
/// <param name="Name">The limit name.</param>
/// <param name="Soft">The soft value.</param>
/// <param name="Hard">The hard value.</param>
public sealed record CodeRunUlimit(string Name, long Soft, long Hard);

/// <summary>
///     A restart policy.
/// </summary>
public sealed class CodeRunRestartPolicy
{
    /// <summary>
    ///     Gets or sets the policy name.
    /// </summary>
    public string Name { get; set; } = "no";
}

/// <summary>
///     Log driver settings.
/// </summary>
public sealed class CodeRunLogConfig
{
    /// <summary>
    ///     Gets or sets the driver.
    /// </summary>
    public string Type { get; set; } = "json-file";

    /// <summary>
    ///     Gets or sets the driver options.
    /// </summary>
    public Dictionary<string, string> Config { get; set; } = new();
}

/// <summary>
///     How a run ended.
/// </summary>
public enum CodeRunStatus
{
    /// <summary>
    ///     The code ran, whatever its exit code.
    /// </summary>
    Completed,

    /// <summary>
    ///     The feature is off or there is no Docker connection.
    /// </summary>
    Unavailable,

    /// <summary>
    ///     The language's image is not on the host yet.
    /// </summary>
    NotInstalled,

    /// <summary>
    ///     The member already has a run going.
    /// </summary>
    UserBusy,

    /// <summary>
    ///     The bot is already running as many as it allows.
    /// </summary>
    Full,

    /// <summary>
    ///     The member ran code too recently.
    /// </summary>
    Cooldown,

    /// <summary>
    ///     Docker refused or failed.
    /// </summary>
    Failed
}

/// <summary>
///     What a run produced.
/// </summary>
/// <param name="Status">How the run ended.</param>
/// <param name="Output">The output, capped at the read limit.</param>
/// <param name="OutputCut">Whether more output existed than was read.</param>
/// <param name="ExitCode">The exit code.</param>
/// <param name="Duration">How long the run took.</param>
/// <param name="TimedOut">Whether the container was killed at the deadline.</param>
public sealed record CodeRunResult(CodeRunStatus Status, string Output, bool OutputCut, long ExitCode,
    TimeSpan Duration, bool TimedOut)
{
    /// <summary>
    ///     Creates a result that never ran anything.
    /// </summary>
    /// <param name="status">Why nothing ran.</param>
    public static CodeRunResult Refused(CodeRunStatus status)
    {
        return new CodeRunResult(status, "", false, 0, TimeSpan.Zero, false);
    }
}
