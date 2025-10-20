using DataModel;
using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace Mewdeko.Database.L2DB;

[Table("PanelButtons")]
public class PanelButton
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    [Column("PanelId")]
    public int PanelId { get; set; }

    [Column("Label", CanBeNull = false)]
    public string Label { get; set; } = null!;

    [Column("Emoji")]
    public string? Emoji { get; set; }

    [Column("CustomId", CanBeNull = false)]
    public string CustomId { get; set; } = null!;

    [Column("Style")]
    public int Style { get; set; }

    [Column("OpenMessageJson")]
    public string? OpenMessageJson { get; set; }

    [Column("ModalJson")]
    public string? ModalJson { get; set; }

    [Column("ChannelNameFormat", CanBeNull = true)]
    public string ChannelNameFormat { get; set; } = null!;

    [Column("CategoryId")]
    public ulong? CategoryId { get; set; }

    [Column("ArchiveCategoryId")]
    public ulong? ArchiveCategoryId { get; set; }

    // Public storage as long[] for database compatibility (must be public for linq2db)
    [Column("SupportRoles")]
    public long[] _supportRoles { get; set; } = [];

    [Column("ViewerRoles")]
    public long[] _viewerRoles { get; set; } = [];

    [Column("AutoCloseTime")]
    public TimeSpan? AutoCloseTime { get; set; }

    [Column("RequiredResponseTime")]
    public TimeSpan? RequiredResponseTime { get; set; }

    [Column("MaxActiveTickets")]
    public int MaxActiveTickets { get; set; }

    [Column("AllowedPriorities", DbType = "text[]")]
    public string[]? AllowedPriorities { get; set; }

    [Column("DefaultPriority")]
    public string? DefaultPriority { get; set; }

    [Column("SaveTranscript")]
    public bool SaveTranscript { get; set; }

    [Column("DeleteOnClose")]
    public bool DeleteOnClose { get; set; } = false;

    [Column("LockOnClose")]
    public bool LockOnClose { get; set; } = true;

    [Column("RenameOnClose")]
    public bool RenameOnClose { get; set; } = true;

    [Column("RemoveCreatorOnClose")]
    public bool RemoveCreatorOnClose { get; set; } = true;

    [Column("DeleteDelay")]
    public TimeSpan DeleteDelay { get; set; } = TimeSpan.FromMinutes(5);

    [Column("LockOnArchive")]
    public bool LockOnArchive { get; set; } = true;

    [Column("RenameOnArchive")]
    public bool RenameOnArchive { get; set; } = true;

    [Column("RemoveCreatorOnArchive")]
    public bool RemoveCreatorOnArchive { get; set; } = false;

    [Column("AutoArchiveOnClose")]
    public bool AutoArchiveOnClose { get; set; } = false;

    /// <summary>
    ///     Support role IDs that can access tickets created with this button
    /// </summary>
    [NotColumn]
    public ulong[] SupportRoles
    {
        get => _supportRoles?.Select(x => (ulong)x).ToArray() ?? [];
        set => _supportRoles = value?.Select(x => (long)x).ToArray() ?? [];
    }

    /// <summary>
    ///     Viewer role IDs that can view tickets created with this button
    /// </summary>
    [NotColumn]
    public ulong[] ViewerRoles
    {
        get => _viewerRoles?.Select(x => (ulong)x).ToArray() ?? [];
        set => _viewerRoles = value?.Select(x => (long)x).ToArray() ?? [];
    }

    #region Associations

    /// <summary>
    ///     FK_PanelButtons_TicketPanels_PanelId - The panel this button belongs to
    /// </summary>
    [Association(CanBeNull = false, ThisKey = nameof(PanelId), OtherKey = nameof(TicketPanel.Id))]
    public TicketPanel Panel { get; set; } = null!;

    /// <summary>
    ///     FK_Tickets_PanelButtons_ButtonId backreference
    /// </summary>
    [Association(ThisKey = nameof(Id), OtherKey = nameof(Ticket.ButtonId))]
    public IEnumerable<Ticket> Tickets { get; set; } = null!;

    #endregion
}