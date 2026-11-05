namespace Mewdeko.Controllers.Common.ChatTriggers;

/// <summary>
///     Request model for importing a chat trigger from a share code.
/// </summary>
public class ShareImportRequest
{
    /// <summary>
    ///     The share code to import.
    /// </summary>
    public string? Code { get; set; }

    /// <summary>
    ///     The member the import runs as, whose role permissions limit which roles the trigger may hand out.
    /// </summary>
    public ulong UserId { get; set; }
}
