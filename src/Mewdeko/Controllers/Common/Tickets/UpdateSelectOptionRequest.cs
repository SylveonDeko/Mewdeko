namespace Mewdeko.Controllers.Common.Tickets;

/// <summary>
///     Request model for updating select menu option settings
/// </summary>
public class UpdateSelectOptionRequest
{
    /// <summary>
    ///     Updated option label
    /// </summary>
    public string? Label { get; set; }

    /// <summary>
    ///     Updated option description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    ///     Updated option emoji
    /// </summary>
    public string? Emoji { get; set; }

    /// <summary>
    ///     Updated category ID
    /// </summary>
    public ulong? CategoryId { get; set; }

    /// <summary>
    ///     Updated archive category ID
    /// </summary>
    public ulong? ArchiveCategoryId { get; set; }

    /// <summary>
    ///     Updated support roles
    /// </summary>
    public List<ulong>? SupportRoles { get; set; }

    /// <summary>
    ///     Updated viewer roles
    /// </summary>
    public List<ulong>? ViewerRoles { get; set; }

    /// <summary>
    ///     Updated auto-close time
    /// </summary>
    public TimeSpan? AutoCloseTime { get; set; }

    /// <summary>
    ///     Updated required response time
    /// </summary>
    public TimeSpan? RequiredResponseTime { get; set; }

    /// <summary>
    ///     Updated maximum active tickets
    /// </summary>
    public int? MaxActiveTickets { get; set; }

    /// <summary>
    ///     Updated allowed priorities
    /// </summary>
    public List<string>? AllowedPriorities { get; set; }

    /// <summary>
    ///     Updated default priority
    /// </summary>
    public string? DefaultPriority { get; set; }

    /// <summary>
    ///     Updated save transcript setting
    /// </summary>
    public bool? SaveTranscript { get; set; }

    /// <summary>
    ///     Updated delete on close setting
    /// </summary>
    public bool? DeleteOnClose { get; set; }

    /// <summary>
    ///     Updated lock on close setting
    /// </summary>
    public bool? LockOnClose { get; set; }

    /// <summary>
    ///     Updated rename on close setting
    /// </summary>
    public bool? RenameOnClose { get; set; }

    /// <summary>
    ///     Updated remove creator on close setting
    /// </summary>
    public bool? RemoveCreatorOnClose { get; set; }

    /// <summary>
    ///     Updated delete delay
    /// </summary>
    public TimeSpan? DeleteDelay { get; set; }

    /// <summary>
    ///     Updated lock on archive setting
    /// </summary>
    public bool? LockOnArchive { get; set; }

    /// <summary>
    ///     Updated rename on archive setting
    /// </summary>
    public bool? RenameOnArchive { get; set; }

    /// <summary>
    ///     Updated remove creator on archive setting
    /// </summary>
    public bool? RemoveCreatorOnArchive { get; set; }

    /// <summary>
    ///     Updated auto archive on close setting
    /// </summary>
    public bool? AutoArchiveOnClose { get; set; }

    /// <summary>
    ///     Updated modal JSON configuration
    /// </summary>
    public string? ModalJson { get; set; }

    /// <summary>
    ///     Updated open message JSON configuration
    /// </summary>
    public string? OpenMessageJson { get; set; }
}