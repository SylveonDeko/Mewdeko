namespace Mewdeko.Controllers.Common.Logging;

/// <summary>
///     Request model for setting the prefixes skipped by message logs
/// </summary>
public class SetIgnorePrefixesRequest
{
    /// <summary>
    ///     Prefixes to skip. An empty list clears them
    /// </summary>
    public List<string> Prefixes { get; set; } = new();
}
