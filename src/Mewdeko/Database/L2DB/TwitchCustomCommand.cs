#nullable enable

using LinqToDB.Mapping;

namespace DataModel;

/// <summary>
///     Represents a guild-scoped custom Twitch chat command.
/// </summary>
[Table("TwitchCustomCommands")]
public class TwitchCustomCommand
{
    /// <summary>
    ///     Gets or sets the database row identifier.
    /// </summary>
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    /// <summary>
    ///     Gets or sets the Discord guild ID that owns this command.
    /// </summary>
    [Column("GuildId")]
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Gets or sets the command name without the Twitch command prefix.
    /// </summary>
    [Column("Name")]
    public string Name { get; set; } = "";

    /// <summary>
    ///     Gets or sets the response sent when the command is invoked.
    /// </summary>
    [Column("Response")]
    public string Response { get; set; } = "";

    /// <summary>
    ///     Gets or sets the minimum Twitch permission level required to run the command.
    /// </summary>
    [Column("PermissionLevel")]
    public int PermissionLevel { get; set; }

    /// <summary>
    ///     Gets or sets the per-command cooldown in seconds.
    /// </summary>
    [Column("CooldownSeconds")]
    public int CooldownSeconds { get; set; }

    /// <summary>
    ///     Gets or sets whether the command is currently enabled.
    /// </summary>
    [Column("Enabled")]
    public bool Enabled { get; set; } = true;

    /// <summary>
    ///     Gets or sets the number of successful command invocations.
    /// </summary>
    [Column("UseCount")]
    public int UseCount { get; set; }

    /// <summary>
    ///     Gets or sets when the command was last successfully used.
    /// </summary>
    [Column("LastUsedAt")]
    public DateTime? LastUsedAt { get; set; }

    /// <summary>
    ///     Gets or sets when the command was created.
    /// </summary>
    [Column("DateAdded")]
    public DateTime? DateAdded { get; set; }

    /// <summary>
    ///     Gets or sets when the command was last edited.
    /// </summary>
    [Column("LastUpdatedAt")]
    public DateTime? LastUpdatedAt { get; set; }
}