namespace Mewdeko.Controllers.Common.Tickets;

/// <summary>
///     Request to turn transcript DMs on or off for every ticket in a server.
/// </summary>
public class SetDmTranscriptRequest
{
    /// <summary>
    ///     Whether the ticket creator gets the transcript in their DMs when the ticket closes.
    /// </summary>
    public bool Enabled { get; set; }
}
