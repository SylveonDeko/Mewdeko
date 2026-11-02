#nullable enable

using LinqToDB.Mapping;

namespace DataModel;

/// <summary>
///     Represents a guild's link fixing settings, which reply to social media links with a copy that goes through an
///     embed fixing service.
/// </summary>
[Table("LinkFixSettings")]
public class LinkFixSetting
{
    /// <summary>
    ///     Gets or sets the database row identifier.
    /// </summary>
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    /// <summary>
    ///     Gets or sets the guild ID these settings belong to.
    /// </summary>
    [Column("GuildId")]
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Gets or sets whether links are fixed in this guild.
    /// </summary>
    [Column("Enabled")]
    public bool Enabled { get; set; }

    /// <summary>
    ///     Gets or sets the comma separated keys of the sites whose links are left alone.
    /// </summary>
    [Column("DisabledPlatforms")]
    public string DisabledPlatforms { get; set; } = "";

    /// <summary>
    ///     Gets or sets the fixing hosts the guild chose, as comma separated key=host pairs.
    /// </summary>
    [Column("Hosts")]
    public string Hosts { get; set; } = "";

    /// <summary>
    ///     Gets or sets when the row was created.
    /// </summary>
    [Column("DateAdded")]
    public DateTime DateAdded { get; set; }
}
