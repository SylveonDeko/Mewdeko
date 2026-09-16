using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using Mewdeko.Modules.OwnerOnly.Common;

namespace Mewdeko.Modules.OwnerOnly.Services;

/// <summary>
///     Exposes the log files pm2 keeps for the processes on this host, so bot owners can read them from the
///     dashboard without a shell. The process list comes from <c>pm2 jlist</c>; the log contents are read
///     straight from the files pm2 reports, never by piping through <c>pm2 logs</c>, so a busy log cannot stall
///     the request and the byte offsets in each chunk let the dashboard follow a file live.
/// </summary>
public partial class Pm2LogService : INService
{
    /// <summary>
    ///     Upper bound on how many lines a single tail request returns.
    /// </summary>
    public const int MaxLines = 2000;

    /// <summary>
    ///     Upper bound on how many bytes a single chunk reads, so a request over a multi gigabyte log stays cheap.
    /// </summary>
    private const int ChunkByteCap = 1024 * 1024;

    private static readonly TimeSpan ProcessListTtl = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan Pm2Timeout = TimeSpan.FromSeconds(10);

    private readonly SemaphoreSlim listLock = new(1, 1);
    private readonly ILogger<Pm2LogService> logger;
    private DateTime cachedAt = DateTime.MinValue;
    private Pm2ProcessList? cachedList;

    /// <summary>
    ///     Initializes a new instance of the <see cref="Pm2LogService" /> class.
    /// </summary>
    /// <param name="logger">The logger instance for structured logging.</param>
    public Pm2LogService(ILogger<Pm2LogService> logger)
    {
        this.logger = logger;
    }

    /// <summary>
    ///     The directory pm2 keeps its state in, honouring <c>PM2_HOME</c> the same way pm2 itself does.
    /// </summary>
    private static string Pm2Home
    {
        get
        {
            var configured = Environment.GetEnvironmentVariable("PM2_HOME");
            if (!string.IsNullOrWhiteSpace(configured))
                return configured;

            var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(profile, ".pm2");
        }
    }

    /// <summary>
    ///     Whether a pm2 daemon appears to be running for the current user. Checked before shelling out because
    ///     <c>pm2 jlist</c> would otherwise spawn a daemon as a side effect.
    /// </summary>
    private static bool DaemonPresent
    {
        get
        {
            var home = Pm2Home;
            return File.Exists(Path.Combine(home, "rpc.sock")) || File.Exists(Path.Combine(home, "pm2.pid"));
        }
    }

    /// <summary>
    ///     Returns the pm2 processes on this host, cached briefly so a dashboard polling the list does not fork
    ///     pm2 on every tick.
    /// </summary>
    public async Task<Pm2ProcessList> GetProcessesAsync()
    {
        await listLock.WaitAsync();
        try
        {
            if (cachedList != null && DateTime.UtcNow - cachedAt < ProcessListTtl)
                return cachedList;

            var list = await QueryDaemonAsync() ?? ScanLogDirectory();
            foreach (var process in list.Processes)
            {
                process.OutLogBytes = FileSize(process.OutLogPath);
                process.ErrorLogBytes = FileSize(process.ErrorLogPath);
                process.IsSelf = IsCurrentProcess(process);
            }

            list.Processes = list.Processes.OrderBy(p => p.PmId).ToList();
            cachedList = list;
            cachedAt = DateTime.UtcNow;
            return list;
        }
        finally
        {
            listLock.Release();
        }
    }

    /// <summary>
    ///     Finds a process by its pm2 id.
    /// </summary>
    /// <param name="pmId">The pm2 id.</param>
    /// <returns>The process, or null when pm2 does not know it.</returns>
    public async Task<Pm2ProcessInfo?> FindProcessAsync(int pmId)
    {
        var list = await GetProcessesAsync();
        return list.Processes.FirstOrDefault(p => p.PmId == pmId);
    }

    /// <summary>
    ///     Resolves which file backs a stream for a process.
    /// </summary>
    /// <param name="process">The process.</param>
    /// <param name="stream">The stream to read.</param>
    /// <returns>The file path, or null when pm2 reported no file for that stream.</returns>
    public static string? LogPath(Pm2ProcessInfo process, Pm2LogStream stream)
    {
        var path = stream == Pm2LogStream.Error ? process.ErrorLogPath : process.OutLogPath;
        return string.IsNullOrWhiteSpace(path) ? null : path;
    }

    /// <summary>
    ///     Reads the last complete lines of a log file.
    /// </summary>
    /// <param name="path">The log file.</param>
    /// <param name="stream">Which stream the file represents, echoed back in the chunk.</param>
    /// <param name="lines">How many lines to return, clamped to <see cref="MaxLines" />.</param>
    /// <returns>The chunk, or null when the file does not exist.</returns>
    public async Task<Pm2LogChunk?> ReadTailAsync(string path, Pm2LogStream stream, int lines)
    {
        lines = Math.Clamp(lines, 1, MaxLines);

        if (!File.Exists(path))
            return null;

        await using var file = OpenShared(path);
        var length = file.Length;
        var readFrom = Math.Max(0, length - ChunkByteCap);
        var buffer = await ReadRangeAsync(file, readFrom, length);

        var chunk = new Pm2LogChunk
        {
            Stream = stream,
            Path = path,
            FileSize = length,
            Start = length,
            End = length
        };

        var contentEnd = Array.LastIndexOf(buffer, (byte)'\n') + 1;
        if (contentEnd <= 0)
            return chunk;

        var start = 0;
        var seen = 0;
        for (var i = contentEnd - 2; i >= 0; i--)
        {
            if (buffer[i] != (byte)'\n')
                continue;

            seen++;
            if (seen < lines)
                continue;

            start = i + 1;
            break;
        }

        if (start == 0 && readFrom > 0)
        {
            chunk.Truncated = true;
            start = Array.IndexOf(buffer, (byte)'\n') + 1;
        }

        chunk.Start = readFrom + start;
        chunk.End = readFrom + contentEnd;
        chunk.Lines = SplitLines(buffer, start, contentEnd);
        return chunk;
    }

    /// <summary>
    ///     Reads the complete lines appended to a log file after a byte offset, falling back to a fresh tail when
    ///     the file has since been rotated or truncated.
    /// </summary>
    /// <param name="path">The log file.</param>
    /// <param name="stream">Which stream the file represents, echoed back in the chunk.</param>
    /// <param name="offset">The <see cref="Pm2LogChunk.End" /> of the previous chunk.</param>
    /// <param name="tailLines">How many lines to return if the file turns out to have been rotated.</param>
    /// <returns>The chunk, or null when the file does not exist.</returns>
    public async Task<Pm2LogChunk?> ReadAfterAsync(string path, Pm2LogStream stream, long offset, int tailLines)
    {
        if (!File.Exists(path))
            return null;

        await using var file = OpenShared(path);
        var length = file.Length;

        if (offset < 0 || offset > length)
        {
            var tail = await ReadTailAsync(path, stream, tailLines);
            if (tail != null)
                tail.Rotated = true;
            return tail;
        }

        var chunk = new Pm2LogChunk
        {
            Stream = stream,
            Path = path,
            FileSize = length,
            Start = offset,
            End = offset
        };

        if (offset == length)
            return chunk;

        var readFrom = offset;
        if (length - offset > ChunkByteCap)
        {
            readFrom = length - ChunkByteCap;
            chunk.Truncated = true;
        }

        var buffer = await ReadRangeAsync(file, readFrom, length);
        var start = 0;
        if (chunk.Truncated)
            start = Array.IndexOf(buffer, (byte)'\n') + 1;

        var contentEnd = Array.LastIndexOf(buffer, (byte)'\n') + 1;
        if (contentEnd <= start)
        {
            chunk.Start = readFrom + start;
            chunk.End = readFrom + start;
            return chunk;
        }

        chunk.Start = readFrom + start;
        chunk.End = readFrom + contentEnd;
        chunk.Lines = SplitLines(buffer, start, contentEnd);
        return chunk;
    }

    /// <summary>
    ///     Opens a log file for streaming to the caller, sharing it with the process still writing to it.
    /// </summary>
    /// <param name="path">The log file.</param>
    /// <returns>An open read stream, or null when the file does not exist.</returns>
    public static FileStream? OpenForDownload(string path)
    {
        return File.Exists(path) ? OpenShared(path) : null;
    }

    private static FileStream OpenShared(string path)
    {
        return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete,
            81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
    }

    private static async Task<byte[]> ReadRangeAsync(FileStream file, long from, long to)
    {
        var buffer = new byte[Math.Max(0, to - from)];
        if (buffer.Length == 0)
            return buffer;

        file.Seek(from, SeekOrigin.Begin);
        var read = 0;
        while (read < buffer.Length)
        {
            var n = await file.ReadAsync(buffer.AsMemory(read, buffer.Length - read));
            if (n == 0)
                break;
            read += n;
        }

        return read == buffer.Length ? buffer : buffer[..read];
    }

    private static List<string> SplitLines(byte[] buffer, int start, int endExclusive)
    {
        var lines = new List<string>();
        var lineStart = start;
        for (var i = start; i < endExclusive; i++)
        {
            if (buffer[i] != (byte)'\n')
                continue;

            var lineEnd = i;
            if (lineEnd > lineStart && buffer[lineEnd - 1] == (byte)'\r')
                lineEnd--;

            lines.Add(Encoding.UTF8.GetString(buffer, lineStart, lineEnd - lineStart));
            lineStart = i + 1;
        }

        return lines;
    }

    private static long? FileSize(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        try
        {
            var info = new FileInfo(path);
            return info.Exists ? info.Length : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static bool IsCurrentProcess(Pm2ProcessInfo process)
    {
        if (process.Pid == Environment.ProcessId)
            return true;

        var ownId = Environment.GetEnvironmentVariable("pm_id");
        if (int.TryParse(ownId, out var pmId) && process.PmId >= 0 && pmId == process.PmId)
            return true;

        var ownOut = Environment.GetEnvironmentVariable("pm_out_log_path");
        if (string.IsNullOrWhiteSpace(ownOut) || string.IsNullOrWhiteSpace(process.OutLogPath))
            return false;

        try
        {
            return string.Equals(Path.GetFullPath(ownOut), Path.GetFullPath(process.OutLogPath),
                StringComparison.Ordinal);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException)
        {
            return false;
        }
    }

    /// <summary>
    ///     Asks the pm2 daemon for its process table.
    /// </summary>
    /// <returns>The list, or null when pm2 is not installed, not running, or answered with something unparseable.</returns>
    private async Task<Pm2ProcessList?> QueryDaemonAsync()
    {
        if (!DaemonPresent)
            return null;

        string output;
        try
        {
            var info = new ProcessStartInfo
            {
                FileName = "pm2",
                Arguments = "jlist",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            info.Environment["PM2_SILENT"] = "true";

            using var process = Process.Start(info);
            if (process == null)
                return null;

            using var timeout = new CancellationTokenSource(Pm2Timeout);
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();

            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                logger.LogWarning("pm2 jlist did not finish within {Timeout}s, killing it", Pm2Timeout.TotalSeconds);
                try
                {
                    process.Kill(true);
                }
                catch (Exception killEx) when (killEx is InvalidOperationException
                                                   or Win32Exception)
                {
                }

                await Task.WhenAll(stdout, stderr);
                return null;
            }

            output = await stdout;
            var errorText = await stderr;
            if (process.ExitCode != 0)
            {
                logger.LogWarning("pm2 jlist exited with {ExitCode}: {Error}", process.ExitCode, errorText.Trim());
                return null;
            }
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException
                                       or IOException)
        {
            logger.LogDebug(ex, "pm2 is not available on this host");
            return null;
        }

        return ParseJlist(output);
    }

    /// <summary>
    ///     Parses the JSON array printed by <c>pm2 jlist</c>, tolerating the notices pm2 sometimes prints around it.
    /// </summary>
    /// <param name="output">The raw stdout of <c>pm2 jlist</c>.</param>
    /// <returns>The parsed list, or null when no JSON array could be found in the output.</returns>
    public Pm2ProcessList? ParseJlist(string output)
    {
        var start = output.IndexOf("[{", StringComparison.Ordinal);
        if (start < 0)
            start = output.IndexOf("[]", StringComparison.Ordinal);
        var end = output.LastIndexOf(']');
        if (start < 0 || end < start)
        {
            logger.LogWarning("pm2 jlist returned no JSON array");
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(output[start..(end + 1)]);
            var list = new Pm2ProcessList
            {
                Source = Pm2ListSource.Daemon
            };

            foreach (var element in doc.RootElement.EnumerateArray())
            {
                var env = element.TryGetProperty("pm2_env", out var e) ? e : default;
                var monit = element.TryGetProperty("monit", out var m) ? m : default;

                var uptime = ReadLong(env, "pm_uptime");
                var pid = ReadLong(element, "pid");
                list.Processes.Add(new Pm2ProcessInfo
                {
                    PmId = (int)(ReadLong(element, "pm_id") ?? ReadLong(env, "pm_id") ?? -1),
                    Name = ReadString(element, "name") ?? ReadString(env, "name") ?? "unknown",
                    Status = ReadString(env, "status") ?? "unknown",
                    Pid = pid is > 0 ? (int)pid.Value : null,
                    Cpu = ReadDouble(monit, "cpu"),
                    MemoryBytes = ReadLong(monit, "memory"),
                    Restarts = (int)(ReadLong(env, "restart_time") ?? 0),
                    StartedAt = uptime is > 0
                        ? DateTimeOffset.FromUnixTimeMilliseconds(uptime.Value).UtcDateTime
                        : null,
                    ExecMode = ReadString(env, "exec_mode"),
                    Script = ReadString(env, "pm_exec_path"),
                    OutLogPath = ReadString(env, "pm_out_log_path"),
                    ErrorLogPath = ReadString(env, "pm_err_log_path")
                });
            }

            if (list.Processes.Count == 0)
                list.Message = "pm2 is running but has no processes registered";

            return list;
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Could not parse pm2 jlist output");
            return null;
        }
    }

    /// <summary>
    ///     Rebuilds a process list from the file names in pm2's log directory, for hosts where the daemon cannot be
    ///     queried but its files are still readable.
    /// </summary>
    private Pm2ProcessList ScanLogDirectory()
    {
        var logsDir = Path.Combine(Pm2Home, "logs");
        var list = new Pm2ProcessList
        {
            Source = Pm2ListSource.None,
            Message = "pm2 is not running on this host, or this instance was not started by it"
        };

        if (!Directory.Exists(logsDir))
            return list;

        var byName = new Dictionary<string, Pm2ProcessInfo>(StringComparer.Ordinal);
        var nextId = -1;
        try
        {
            foreach (var file in Directory.EnumerateFiles(logsDir, "*.log").OrderBy(f => f, StringComparer.Ordinal))
            {
                var match = LogFileName().Match(Path.GetFileName(file));
                if (!match.Success)
                    continue;

                var name = match.Groups["name"].Value;
                if (!byName.TryGetValue(name, out var process))
                {
                    var idText = match.Groups["id"].Value;
                    process = new Pm2ProcessInfo
                    {
                        Name = name,
                        PmId = idText.Length > 0 && int.TryParse(idText, out var parsed) ? parsed : nextId--
                    };
                    byName[name] = process;
                }

                if (match.Groups["stream"].Value == "error")
                    process.ErrorLogPath = file;
                else
                    process.OutLogPath = file;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Could not enumerate the pm2 log directory {Dir}", logsDir);
            return list;
        }

        if (byName.Count == 0)
            return list;

        list.Source = Pm2ListSource.LogDirectory;
        list.Message = "The pm2 daemon could not be queried, so this list was rebuilt from its log directory";
        list.Processes = byName.Values.ToList();
        return list;
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
            JsonValueKind.String when long.TryParse(value.GetString(), out var parsed) => parsed,
            _ => null
        };
    }

    private static double? ReadDouble(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name, out var value))
            return null;

        return value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var d) ? d : null;
    }

    [GeneratedRegex(@"^(?<name>.+?)-(?<stream>out|error)(?:-(?<id>\d+))?\.log$", RegexOptions.CultureInvariant)]
    private static partial Regex LogFileName();
}