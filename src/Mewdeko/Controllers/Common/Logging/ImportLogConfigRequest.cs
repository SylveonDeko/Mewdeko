namespace Mewdeko.Controllers.Common.Logging;

/// <summary>
///     Request model for importing a log configuration
/// </summary>
public class ImportLogConfigRequest
{
    /// <summary>
    ///     The export code or JSON to import
    /// </summary>
    public string Code { get; set; } = "";
}
