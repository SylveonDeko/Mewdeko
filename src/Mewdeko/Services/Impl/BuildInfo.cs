using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using Mewdeko.Common.Attributes.ASPNET;

namespace Mewdeko.Services.Impl;

/// <summary>
///     Which commit this build came from and when. The Docker image gets both stamped in by CI as
///     environment variables; a build from a working tree falls back to the assembly attribute and then to
///     asking git, and reports null when none of those know.
/// </summary>
public static class BuildInfo
{
    private static readonly Lazy<string?> gitSha = new(ResolveGitSha);
    private static readonly Lazy<DateTime?> buildDate = new(ResolveBuildDate);

    /// <summary>
    ///     The full commit hash, or null when it cannot be determined.
    /// </summary>
    public static string? GitSha
    {
        get
        {
            return gitSha.Value;
        }
    }

    /// <summary>
    ///     The first seven characters of <see cref="GitSha" />, or null.
    /// </summary>
    public static string? ShortSha
    {
        get
        {
            return GitSha is { Length: >= 7 } sha ? sha[..7] : GitSha;
        }
    }

    /// <summary>
    ///     When the image was built, or null outside CI built images.
    /// </summary>
    public static DateTime? BuildDate
    {
        get
        {
            return buildDate.Value;
        }
    }

    private static string? ResolveGitSha()
    {
        var fromEnv = Environment.GetEnvironmentVariable("MEWDEKO_GIT_SHA");
        if (!string.IsNullOrWhiteSpace(fromEnv) && fromEnv != "unknown")
            return fromEnv.Trim();

        var attribute = Assembly.GetExecutingAssembly().GetCustomAttribute<GitHashAttribute>();
        if (!string.IsNullOrWhiteSpace(attribute?.Hash))
            return attribute.Hash.Trim();

        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "rev-parse HEAD",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            });
            if (process == null)
                return null;

            var output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit(5000);
            return process.ExitCode == 0 && output.Length >= 7 ? output : null;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            return null;
        }
    }

    private static DateTime? ResolveBuildDate()
    {
        var fromEnv = Environment.GetEnvironmentVariable("MEWDEKO_BUILD_DATE");
        if (string.IsNullOrWhiteSpace(fromEnv) || fromEnv == "unknown")
            return null;

        return DateTimeOffset.TryParse(fromEnv, null, System.Globalization.DateTimeStyles.AssumeUniversal,
            out var parsed)
            ? parsed.UtcDateTime
            : null;
    }
}
