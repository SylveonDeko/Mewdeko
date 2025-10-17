namespace Mewdeko.Controllers.Common.Tickets;

/// <summary>
///     Request model for editing a ticket note
/// </summary>
public class EditNoteRequest
{
    /// <summary>
    ///     The new content of the note
    /// </summary>
    public string Content { get; set; } = null!;

    /// <summary>
    ///     The ID of the user editing the note
    /// </summary>
    public ulong AuthorId { get; set; }
}