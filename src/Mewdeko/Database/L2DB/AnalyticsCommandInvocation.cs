using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel;

/// <summary>
///     One command or interaction execution.
/// </summary>
[Table("AnalyticsCommandInvocation")]
public class AnalyticsCommandInvocation
{
    /// <summary>
    ///     Auto-generated primary key.
    /// </summary>
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public long Id { get; set; }

    /// <summary>
    ///     When the command finished, UTC.
    /// </summary>
    [Column("At")]
    public DateTime At { get; set; }

    /// <summary>
    ///     The bot instance that ran it.
    /// </summary>
    [Column("Bot")]
    public string Bot { get; set; } = string.Empty;

    /// <summary>
    ///     The shard the guild lives on.
    /// </summary>
    [Column("Shard")]
    public int? Shard { get; set; }

    /// <summary>
    ///     The guild, or null for direct messages and opted out guilds.
    /// </summary>
    [Column("GuildId")]
    public ulong? GuildId { get; set; }

    /// <summary>
    ///     Member count of the guild at the time.
    /// </summary>
    [Column("GuildSize")]
    public int? GuildSize { get; set; }

    /// <summary>
    ///     How the command was invoked: text, slash, user_ctx, msg_ctx, button, select, modal, autocomplete or trigger.
    /// </summary>
    [Column("Kind")]
    public string Kind { get; set; } = string.Empty;

    /// <summary>
    ///     The module the command belongs to.
    /// </summary>
    [Column("Module")]
    public string? Module { get; set; }

    /// <summary>
    ///     The command name.
    /// </summary>
    [Column("Command")]
    public string Command { get; set; } = string.Empty;

    /// <summary>
    ///     Whether it succeeded.
    /// </summary>
    [Column("Ok")]
    public bool Ok { get; set; }

    /// <summary>
    ///     The error class when it failed.
    /// </summary>
    [Column("ErrorClass")]
    public string? ErrorClass { get; set; }

    /// <summary>
    ///     The error message when it failed, trimmed to 200 characters.
    /// </summary>
    [Column("ErrorMessage")]
    public string? ErrorMessage { get; set; }

    /// <summary>
    ///     Execution time in milliseconds.
    /// </summary>
    [Column("DurationMs")]
    public int DurationMs { get; set; }

    /// <summary>
    ///     Time from interaction creation to the first acknowledgement, when known.
    /// </summary>
    [Column("AckMs")]
    public int? AckMs { get; set; }

    /// <summary>
    ///     The guild or user locale the command ran under.
    /// </summary>
    [Column("Language")]
    public string? Language { get; set; }
}