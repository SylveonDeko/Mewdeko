namespace Mewdeko.Controllers.Common.Tickets;

/// <summary>
///     Shared ticket-opening settings for panel buttons and select menu options.
/// </summary>
public abstract class TicketComponentRequestBase
{
    /// <summary>
    ///     Optional emoji for the component.
    /// </summary>
    public string? Emoji { get; set; }

    /// <summary>
    ///     Optional JSON for ticket opening message.
    /// </summary>
    public string? OpenMessageJson { get; set; }

    /// <summary>
    ///     Optional JSON for ticket creation modal.
    /// </summary>
    public string? ModalJson { get; set; }

    /// <summary>
    ///     Format for ticket channel names.
    /// </summary>
    public string? ChannelFormat { get; set; }

    /// <summary>
    ///     Optional category for ticket channels.
    /// </summary>
    public ulong? CategoryId { get; set; }

    /// <summary>
    ///     Optional category for archived tickets.
    /// </summary>
    public ulong? ArchiveCategoryId { get; set; }

    /// <summary>
    ///     List of support role IDs.
    /// </summary>
    public List<ulong>? SupportRoles { get; set; }

    /// <summary>
    ///     List of viewer role IDs.
    /// </summary>
    public List<ulong>? ViewerRoles { get; set; }

    /// <summary>
    ///     Optional auto-close duration.
    /// </summary>
    public TimeSpan? AutoCloseTime { get; set; }

    /// <summary>
    ///     Optional required response time.
    /// </summary>
    public TimeSpan? RequiredResponseTime { get; set; }

    /// <summary>
    ///     List of allowed priority IDs.
    /// </summary>
    public List<string>? AllowedPriorities { get; set; }

    /// <summary>
    ///     Optional default priority.
    /// </summary>
    public string? DefaultPriority { get; set; }
}

/// <summary>
///     Shared settings for creating ticket panel components.
/// </summary>
public abstract class AddTicketComponentRequestBase : TicketComponentRequestBase
{
    /// <summary>
    ///     Component label.
    /// </summary>
    public string Label { get; set; } = null!;

    /// <summary>
    ///     Maximum active tickets per user.
    /// </summary>
    public int MaxActiveTickets { get; set; } = 1;
}

/// <summary>
///     Shared settings for updating ticket panel components.
/// </summary>
public abstract class UpdateTicketComponentRequestBase : TicketComponentRequestBase
{
    /// <summary>
    ///     Updated component label.
    /// </summary>
    public string? Label { get; set; }

    /// <summary>
    ///     Updated maximum active tickets.
    /// </summary>
    public int? MaxActiveTickets { get; set; }

    /// <summary>
    ///     Updated save transcript setting.
    /// </summary>
    public bool? SaveTranscript { get; set; }

    /// <summary>
    ///     Updated delete on close setting.
    /// </summary>
    public bool? DeleteOnClose { get; set; }

    /// <summary>
    ///     Updated lock on close setting.
    /// </summary>
    public bool? LockOnClose { get; set; }

    /// <summary>
    ///     Updated rename on close setting.
    /// </summary>
    public bool? RenameOnClose { get; set; }

    /// <summary>
    ///     Updated remove creator on close setting.
    /// </summary>
    public bool? RemoveCreatorOnClose { get; set; }

    /// <summary>
    ///     Updated delete delay.
    /// </summary>
    public TimeSpan? DeleteDelay { get; set; }

    /// <summary>
    ///     Updated lock on archive setting.
    /// </summary>
    public bool? LockOnArchive { get; set; }

    /// <summary>
    ///     Updated rename on archive setting.
    /// </summary>
    public bool? RenameOnArchive { get; set; }

    /// <summary>
    ///     Updated remove creator on archive setting.
    /// </summary>
    public bool? RemoveCreatorOnArchive { get; set; }

    /// <summary>
    ///     Updated auto archive on close setting.
    /// </summary>
    public bool? AutoArchiveOnClose { get; set; }
}