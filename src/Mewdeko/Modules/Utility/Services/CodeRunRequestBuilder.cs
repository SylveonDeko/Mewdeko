using System.Text;
using Mewdeko.Modules.Utility.Common;

namespace Mewdeko.Modules.Utility.Services;

/// <summary>
///     Builds the Docker container request for a run. This is the security boundary of the run command: every
///     restriction is set here and nothing a member types reaches the image, the command line, a path or a URL.
///     The code travels only as base64 in an environment variable.
/// </summary>
public static class CodeRunRequestBuilder
{
    /// <summary>
    ///     The label every run container carries.
    /// </summary>
    public const string Label = "mewdeko.coderun";

    /// <summary>
    ///     The label holding the member who asked for the run.
    /// </summary>
    public const string UserLabel = "mewdeko.coderun.user";

    /// <summary>
    ///     The environment variable the code is passed in.
    /// </summary>
    public const string CodeVariable = "MEWDEKO_CODE";

    /// <summary>
    ///     The user and group the code runs as.
    /// </summary>
    public const string RunUser = "65534:65534";

    /// <summary>
    ///     The writable working directory.
    /// </summary>
    public const string WorkDir = "/work";

    /// <summary>
    ///     The maximum number of processes.
    /// </summary>
    public const long PidsLimit = 64;

    /// <summary>
    ///     The memory limit in bytes.
    /// </summary>
    public const long MemoryBytes = 256L * 1024 * 1024;

    /// <summary>
    ///     The CPU limit in billionths of a CPU.
    /// </summary>
    public const long NanoCpus = 1_000_000_000;

    /// <summary>
    ///     The largest file the code may write, in bytes.
    /// </summary>
    public const long FileSizeBytes = 16L * 1024 * 1024;

    /// <summary>
    ///     The open file limit.
    /// </summary>
    public const long OpenFiles = 256;

    /// <summary>
    ///     The options of both tmpfs mounts.
    /// </summary>
    public const string TmpfsOptions = "rw,exec,nosuid,nodev,size=32m,mode=1777";

    /// <summary>
    ///     Builds the request for one run.
    /// </summary>
    /// <param name="language">The configured language.</param>
    /// <param name="code">The member's code.</param>
    /// <param name="options">The per run options.</param>
    public static CodeRunContainerRequest Build(CodeRunLanguage language, string code, CodeRunOptions options)
    {
        var environment = new Dictionary<string, string>
        {
            ["HOME"] = WorkDir,
            ["TMPDIR"] = "/tmp",
            ["XDG_CACHE_HOME"] = $"{WorkDir}/.cache"
        };
        foreach (var (name, value) in language.Env)
            environment[name] = value;
        environment[CodeVariable] = Convert.ToBase64String(Encoding.UTF8.GetBytes(code));

        return new CodeRunContainerRequest
        {
            Image = language.Image,
            Entrypoint = ["sh", "-c"],
            Cmd = [BuildScript(language)],
            Env = environment.Select(pair => $"{pair.Key}={pair.Value}").ToList(),
            User = RunUser,
            WorkingDir = WorkDir,
            NetworkDisabled = true,
            AttachStdin = false,
            OpenStdin = false,
            Tty = false,
            Labels = new Dictionary<string, string>
            {
                [Label] = "1",
                [UserLabel] = options.UserId.ToString()
            },
            HostConfig = new CodeRunHostConfig
            {
                NetworkMode = "none",
                ReadonlyRootfs = true,
                CapDrop = ["ALL"],
                SecurityOpt = ["no-new-privileges"],
                Privileged = false,
                IpcMode = "none",
                PidsLimit = PidsLimit,
                Memory = MemoryBytes,
                MemorySwap = MemoryBytes,
                NanoCpus = NanoCpus,
                Ulimits =
                [
                    new CodeRunUlimit("nofile", OpenFiles, OpenFiles),
                    new CodeRunUlimit("fsize", FileSizeBytes, FileSizeBytes),
                    new CodeRunUlimit("core", 0, 0)
                ],
                Tmpfs = new Dictionary<string, string>
                {
                    [WorkDir] = TmpfsOptions,
                    ["/tmp"] = TmpfsOptions
                },
                AutoRemove = false,
                RestartPolicy = new CodeRunRestartPolicy
                {
                    Name = "no"
                },
                LogConfig = new CodeRunLogConfig
                {
                    Type = "json-file",
                    Config = new Dictionary<string, string>
                    {
                        ["max-size"] = "1m",
                        ["max-file"] = "1"
                    }
                },
                Runtime = string.IsNullOrWhiteSpace(options.Runtime) ? null : options.Runtime.Trim()
            }
        };
    }

    /// <summary>
    ///     Builds the fixed shell script from the language's configuration. The member's code is never part of it.
    /// </summary>
    /// <param name="language">The configured language.</param>
    public static string BuildScript(CodeRunLanguage language)
    {
        var script = new StringBuilder();
        script.Append("set -e\n");
        script.Append($"printf '%s' \"${CodeVariable}\" | base64 -d > {language.FileName}\n");
        if (!string.IsNullOrWhiteSpace(language.Compile))
            script.Append(language.Compile).Append('\n');
        script.Append(language.Run);
        return script.ToString();
    }
}
