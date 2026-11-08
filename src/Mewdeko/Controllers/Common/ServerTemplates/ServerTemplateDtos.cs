namespace Mewdeko.Controllers.Common.ServerTemplates;

/// <summary>
///     A user's saved server template, without its data.
/// </summary>
public class ServerTemplateDto
{
    /// <summary>
    ///     The template name.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    ///     The compressed size in bytes.
    /// </summary>
    public int SizeBytes { get; set; }

    /// <summary>
    ///     How many roles the template holds.
    /// </summary>
    public int RoleCount { get; set; }

    /// <summary>
    ///     How many channels and categories the template holds.
    /// </summary>
    public int ChannelCount { get; set; }

    /// <summary>
    ///     When the template was saved.
    /// </summary>
    public DateTime CreatedAt { get; set; }
}

/// <summary>
///     The starter templates and a user's saved templates.
/// </summary>
public class ServerTemplateListResponse
{
    /// <summary>
    ///     The names of the starter templates.
    /// </summary>
    public List<string> BuiltIn { get; set; } = [];

    /// <summary>
    ///     The user's saved templates.
    /// </summary>
    public List<ServerTemplateDto> Templates { get; set; } = [];
}

/// <summary>
///     Request to save a server as a template.
/// </summary>
public class SaveServerTemplateRequest
{
    /// <summary>
    ///     The template name.
    /// </summary>
    public string Name { get; set; } = "";
}
