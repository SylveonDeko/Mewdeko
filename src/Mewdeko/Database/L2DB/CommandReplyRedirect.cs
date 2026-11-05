#nullable enable

using LinqToDB.Mapping;

namespace DataModel;

/// <summary>
///     Represents a rule that sends a command's or module's replies to a set channel instead of where it was used.
/// </summary>
[Table("CommandReplyRedirects")]
public class CommandReplyRedirect
{
    /// <summary>
    ///     Gets or sets the database row identifier.
    /// </summary>
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    /// <summary>
    ///     Gets or sets the guild ID the rule belongs to.
    /// </summary>
    [Column("GuildId")]
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Gets or sets the lowercase command or module name the rule applies to.
    /// </summary>
    [Column("Target")]
    public string Target { get; set; } = "";

    /// <summary>
    ///     Gets or sets whether <see cref="Target" /> is a module name rather than a command name.
    /// </summary>
    [Column("IsModule")]
    public bool IsModule { get; set; }

    /// <summary>
    ///     Gets or sets the channel replies are sent to.
    /// </summary>
    [Column("ChannelId")]
    public ulong ChannelId { get; set; }

    /// <summary>
    ///     Gets or sets whether the message that invoked the command is deleted.
    /// </summary>
    [Column("DeleteInvoker")]
    public bool DeleteInvoker { get; set; }

    /// <summary>
    ///     Gets or sets when the rule was added.
    /// </summary>
    [Column("DateAdded", SkipOnInsert = true, SkipOnUpdate = true)]
    public DateTime DateAdded { get; set; }
}
