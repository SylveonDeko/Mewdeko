namespace Mewdeko.Controllers.Common.Tickets;

/// <summary>
///     Request model for deleting a ticket note
/// </summary>
public class DeleteNoteRequest
{
    /// <summary>
    ///     The ID of the user deleting the note
    /// </summary>
    public ulong AuthorId { get; set; }
}