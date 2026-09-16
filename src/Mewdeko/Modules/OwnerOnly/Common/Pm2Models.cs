namespace Mewdeko.Modules.OwnerOnly.Common;

/// <summary>
///     Which of a pm2 process's two log files is being read.
/// </summary>
public enum Pm2LogStream
{
    /// <summary>
    ///     Standard output, where Serilog's console sink writes.
    /// </summary>
    Out,

    /// <summary>
    ///     Standard error, where unhandled crashes and runtime diagnostics land.
    /// </summary>
    Error
}

/// <summary>
///     Where the process list came from, so the dashboard can explain a degraded view.
/// </summary>
public enum Pm2ListSource
{
    /// <summary>
    ///     No pm2 daemon or log directory could be found on this host.
    /// </summary>
    None,

    /// <summary>
    ///     Read live from the pm2 daemon via <c>pm2 jlist</c>.
    /// </summary>
    Daemon,

    /// <summary>
    ///     Reconstructed from the files in pm2's log directory because the daemon could not be queried.
    /// </summary>
    LogDirectory
}

/// <summary>
///     One process known to pm2, with the log file paths the dashboard reads from.
/// </summary>
public sealed class Pm2ProcessInfo
{
    /// <summary>
    ///     pm2's numeric id for the process. Negative for entries reconstructed from the log directory.
    /// </summary>
    public int PmId { get; set; }

    /// <summary>
    ///     The process name as registered with pm2.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    ///     pm2's status string, such as online, stopped or errored. Unknown when reconstructed from files.
    /// </summary>
    public string Status { get; set; } = "unknown";

    /// <summary>
    ///     The operating system pid, when the process is running.
    /// </summary>
    public int? Pid { get; set; }

    /// <summary>
    ///     Percentage of one core the process used at the time of the query.
    /// </summary>
    public double? Cpu { get; set; }

    /// <summary>
    ///     Resident memory in bytes at the time of the query.
    /// </summary>
    public long? MemoryBytes { get; set; }

    /// <summary>
    ///     How many times pm2 has restarted the process.
    /// </summary>
    public int Restarts { get; set; }

    /// <summary>
    ///     When the current incarnation of the process started.
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    ///     pm2's execution mode, fork or cluster.
    /// </summary>
    public string? ExecMode { get; set; }

    /// <summary>
    ///     The script or binary pm2 launched.
    /// </summary>
    public string? Script { get; set; }

    /// <summary>
    ///     Full path of the stdout log file.
    /// </summary>
    public string? OutLogPath { get; set; }

    /// <summary>
    ///     Full path of the stderr log file.
    /// </summary>
    public string? ErrorLogPath { get; set; }

    /// <summary>
    ///     Current size of the stdout log in bytes, or null when the file is missing.
    /// </summary>
    public long? OutLogBytes { get; set; }

    /// <summary>
    ///     Current size of the stderr log in bytes, or null when the file is missing.
    /// </summary>
    public long? ErrorLogBytes { get; set; }

    /// <summary>
    ///     Whether this entry is the bot instance answering the request.
    /// </summary>
    public bool IsSelf { get; set; }
}

/// <summary>
///     The pm2 process list along with how it was obtained.
/// </summary>
public sealed class Pm2ProcessList
{
    /// <summary>
    ///     Where the entries came from.
    /// </summary>
    public Pm2ListSource Source { get; set; }

    /// <summary>
    ///     A human readable note when the list is degraded or empty.
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    ///     The processes found, ordered by pm2 id.
    /// </summary>
    public List<Pm2ProcessInfo> Processes { get; set; } = [];
}

/// <summary>
///     A run of complete lines read out of a log file, with the byte offsets needed to continue from it.
/// </summary>
public sealed class Pm2LogChunk
{
    /// <summary>
    ///     Which log file the chunk came from.
    /// </summary>
    public Pm2LogStream Stream { get; set; }

    /// <summary>
    ///     The file the chunk was read from.
    /// </summary>
    public string Path { get; set; } = "";

    /// <summary>
    ///     Size of the file in bytes when it was read.
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    ///     Byte offset of the first returned line.
    /// </summary>
    public long Start { get; set; }

    /// <summary>
    ///     Byte offset just past the last returned line. Pass this back to receive only what was appended since.
    /// </summary>
    public long End { get; set; }

    /// <summary>
    ///     The lines, oldest first, with line terminators removed and ANSI colour codes left in place.
    /// </summary>
    public List<string> Lines { get; set; } = [];

    /// <summary>
    ///     True when more data existed than the byte cap allowed, so lines before <see cref="Start" /> were skipped.
    /// </summary>
    public bool Truncated { get; set; }

    /// <summary>
    ///     True when the file shrank since the caller's offset, meaning it was rotated or flushed and the chunk
    ///     is a fresh tail rather than a continuation.
    /// </summary>
    public bool Rotated { get; set; }
}