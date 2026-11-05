#nullable enable

using LinqToDB.Mapping;

namespace DataModel;

/// <summary>
///     Represents a saved sticky message preset a guild can reuse.
/// </summary>
[Table("StickyTemplates")]
public class StickyTemplate
{
    /// <summary>
    ///     Gets or sets the database row identifier.
    /// </summary>
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    /// <summary>
    ///     Gets or sets the guild ID the template belongs to.
    /// </summary>
    [Column("GuildId")]
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Gets or sets the template name.
    /// </summary>
    [Column("Name")]
    public string Name { get; set; } = "";

    /// <summary>
    ///     Gets or sets the message text or embed json the template posts.
    /// </summary>
    [Column("Content")]
    public string Content { get; set; } = "";

    /// <summary>
    ///     Gets or sets when the template was saved.
    /// </summary>
    [Column("DateAdded", SkipOnInsert = true, SkipOnUpdate = true)]
    public DateTime DateAdded { get; set; }
}
