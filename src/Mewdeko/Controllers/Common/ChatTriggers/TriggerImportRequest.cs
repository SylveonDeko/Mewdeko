namespace Mewdeko.Controllers.Common.ChatTriggers;

/// <summary>
///     Request model for importing chat triggers from an exported file.
/// </summary>
public class TriggerImportRequest
{
    /// <summary>
    ///     The exported trigger file contents.
    /// </summary>
    public string? Text { get; set; }

    /// <summary>
    ///     The member the import runs as, whose role permissions limit which roles the triggers may hand out.
    /// </summary>
    public ulong UserId { get; set; }
}
