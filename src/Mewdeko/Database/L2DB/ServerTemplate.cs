#nullable enable

using LinqToDB.Mapping;

namespace DataModel;

/// <summary>
///     Represents a named copy of a server's roles, categories and channels that a user can apply to a server.
/// </summary>
[Table("ServerTemplates")]
public class ServerTemplate
{
    /// <summary>
    ///     Gets or sets the database row identifier.
    /// </summary>
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    /// <summary>
    ///     Gets or sets the ID of the user who owns the template.
    /// </summary>
    [Column("UserId")]
    public ulong UserId { get; set; }

    /// <summary>
    ///     Gets or sets the template name.
    /// </summary>
    [Column("Name")]
    public string Name { get; set; } = "";

    /// <summary>
    ///     Gets or sets the gzip compressed JSON of the template.
    /// </summary>
    [Column("Data")]
    public byte[] Data { get; set; } = [];

    /// <summary>
    ///     Gets or sets the compressed size in bytes.
    /// </summary>
    [Column("SizeBytes")]
    public int SizeBytes { get; set; }

    /// <summary>
    ///     Gets or sets how many roles the template holds.
    /// </summary>
    [Column("RoleCount")]
    public int RoleCount { get; set; }

    /// <summary>
    ///     Gets or sets how many channels and categories the template holds.
    /// </summary>
    [Column("ChannelCount")]
    public int ChannelCount { get; set; }

    /// <summary>
    ///     Gets or sets when the template was saved.
    /// </summary>
    [Column("DateAdded")]
    public DateTime DateAdded { get; set; }
}
