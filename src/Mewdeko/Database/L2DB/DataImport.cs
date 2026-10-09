#nullable enable

using LinqToDB.Mapping;

namespace DataModel;

/// <summary>
///     A record of data imported from another bot, holding what it replaced so it can be undone.
/// </summary>
[Table("DataImports")]
public class DataImport
{
    /// <summary>
    ///     Gets or sets the database row identifier.
    /// </summary>
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    /// <summary>
    ///     Gets or sets the Discord guild ID the data was imported into.
    /// </summary>
    [Column("GuildId")]
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Gets or sets the Discord user ID of whoever ran the import.
    /// </summary>
    [Column("UserId")]
    public ulong UserId { get; set; }

    /// <summary>
    ///     Gets or sets where the data came from, as a <see cref="Mewdeko.Modules.Import.Common.ImportSource" />.
    /// </summary>
    [Column("Source")]
    public int Source { get; set; }

    /// <summary>
    ///     Gets or sets what the data was for, as a <see cref="Mewdeko.Modules.Import.Common.ImportKind" />.
    /// </summary>
    [Column("Kind")]
    public int Kind { get; set; }

    /// <summary>
    ///     Gets or sets how many members were written.
    /// </summary>
    [Column("MemberCount")]
    public int MemberCount { get; set; }

    /// <summary>
    ///     Gets or sets how many role rewards were written.
    /// </summary>
    [Column("RoleRewardCount")]
    public int RoleRewardCount { get; set; }

    /// <summary>
    ///     Gets or sets the JSON snapshot of everything the import replaced. Emptied once the undo window closes.
    /// </summary>
    [Column("Snapshot")]
    public string Snapshot { get; set; } = "";

    /// <summary>
    ///     Gets or sets when the import was undone.
    /// </summary>
    [Column("UndoneAt")]
    public DateTime? UndoneAt { get; set; }

    /// <summary>
    ///     Gets or sets when the import ran.
    /// </summary>
    [Column("DateAdded")]
    public DateTime DateAdded { get; set; }
}
