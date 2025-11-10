using System.Text.Json.Serialization;

namespace Mewdeko.Modules.CoprMonitoring.Common;

/// <summary>
///     Represents a Fedora Messaging message envelope.
/// </summary>
public class FedoraMessage
{
    /// <summary>
    ///     Gets or sets the message topic.
    /// </summary>
    [JsonPropertyName("topic")]
    public string Topic { get; set; } = null!;

    /// <summary>
    ///     Gets or sets the message body containing COPR build data.
    /// </summary>
    [JsonPropertyName("body")]
    public CoprBuildMessage Body { get; set; } = null!;
}

/// <summary>
///     Represents the body of a COPR build message from Fedora Messaging.
/// </summary>
public class CoprBuildMessage
{
    /// <summary>
    ///     Gets or sets the build ID.
    /// </summary>
    [JsonPropertyName("build")]
    public int Build { get; set; }

    /// <summary>
    ///     Gets or sets the chroot name (e.g., "fedora-42-x86_64").
    /// </summary>
    [JsonPropertyName("chroot")]
    public string Chroot { get; set; } = null!;

    /// <summary>
    ///     Gets or sets the build status.
    /// </summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = null!;

    /// <summary>
    ///     Gets or sets the COPR project owner.
    /// </summary>
    [JsonPropertyName("owner")]
    public string Owner { get; set; } = null!;

    /// <summary>
    ///     Gets or sets the COPR project name.
    /// </summary>
    [JsonPropertyName("project")]
    public string Project { get; set; } = null!;

    /// <summary>
    ///     Gets or sets the package name.
    /// </summary>
    [JsonPropertyName("pkg")]
    public string Package { get; set; } = null!;

    /// <summary>
    ///     Gets or sets the package version.
    /// </summary>
    [JsonPropertyName("version")]
    public string? Version { get; set; }

    /// <summary>
    ///     Gets or sets the user who submitted the build.
    /// </summary>
    [JsonPropertyName("user")]
    public string? User { get; set; }

    /// <summary>
    ///     Gets or sets the build submission timestamp.
    /// </summary>
    [JsonPropertyName("submitter")]
    public string? Submitter { get; set; }
}

/// <summary>
///     Represents the status of a COPR build.
/// </summary>
public enum CoprBuildStatus
{
    /// <summary>
    ///     Package sources are being imported into Copr DistGit.
    /// </summary>
    Importing = 0,

    /// <summary>
    ///     Build is waiting in queue for a backend worker.
    /// </summary>
    Pending = 1,

    /// <summary>
    ///     Backend worker is trying to acquire a builder machine.
    /// </summary>
    Starting = 2,

    /// <summary>
    ///     Build in progress.
    /// </summary>
    Running = 3,

    /// <summary>
    ///     Successfully built.
    /// </summary>
    Succeeded = 4,

    /// <summary>
    ///     Build failed.
    /// </summary>
    Failed = 5,

    /// <summary>
    ///     The build has been cancelled manually.
    /// </summary>
    Canceled = 6,

    /// <summary>
    ///     This package was skipped.
    /// </summary>
    Skipped = 7,

    /// <summary>
    ///     Build has been forked from another build.
    /// </summary>
    Forked = 8,

    /// <summary>
    ///     Task is waiting for something else to finish.
    /// </summary>
    Waiting = 9
}

/// <summary>
///     Extension methods for COPR-related functionality.
/// </summary>
public static class CoprExtensions
{
    /// <summary>
    ///     Parses a string status to the corresponding enum value.
    /// </summary>
    /// <param name="status">The status string from COPR.</param>
    /// <returns>The parsed CoprBuildStatus enum value.</returns>
    public static CoprBuildStatus ParseStatus(string status)
    {
        return status.ToLowerInvariant() switch
        {
            "importing" => CoprBuildStatus.Importing,
            "pending" => CoprBuildStatus.Pending,
            "starting" => CoprBuildStatus.Starting,
            "running" => CoprBuildStatus.Running,
            "succeeded" => CoprBuildStatus.Succeeded,
            "failed" => CoprBuildStatus.Failed,
            "canceled" => CoprBuildStatus.Canceled,
            "cancelled" => CoprBuildStatus.Canceled,
            "skipped" => CoprBuildStatus.Skipped,
            "forked" => CoprBuildStatus.Forked,
            "waiting" => CoprBuildStatus.Waiting,
            _ => CoprBuildStatus.Pending
        };
    }

    /// <summary>
    ///     Gets a user-friendly display name for the build status.
    /// </summary>
    /// <param name="status">The build status.</param>
    /// <returns>A display-friendly status string.</returns>
    public static string ToDisplayString(this CoprBuildStatus status)
    {
        return status switch
        {
            CoprBuildStatus.Importing => "Importing",
            CoprBuildStatus.Pending => "Pending",
            CoprBuildStatus.Starting => "Starting",
            CoprBuildStatus.Running => "Running",
            CoprBuildStatus.Succeeded => "Succeeded",
            CoprBuildStatus.Failed => "Failed",
            CoprBuildStatus.Canceled => "Canceled",
            CoprBuildStatus.Skipped => "Skipped",
            CoprBuildStatus.Forked => "Forked",
            CoprBuildStatus.Waiting => "Waiting",
            _ => "Unknown"
        };
    }
}