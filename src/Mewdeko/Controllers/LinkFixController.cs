using Mewdeko.Modules.Utility.Common;
using Mewdeko.Modules.Utility.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.Controllers;

/// <summary>
///     Reads and changes a guild's link fixing settings for the dashboard.
/// </summary>
[ApiController]
[Route("botapi/[controller]/{guildId}")]
[Authorize("ApiKeyPolicy")]
public class LinkFixController : Controller
{
    private readonly LinkFixService linkFix;
    private readonly IDashboardAuditContext auditContext;

    /// <summary>
    ///     Initializes a new instance of the <see cref="LinkFixController" /> class.
    /// </summary>
    /// <param name="linkFix">The link fixing service.</param>
    /// <param name="auditContext">Records before/after state for the dashboard audit log.</param>
    public LinkFixController(LinkFixService linkFix, IDashboardAuditContext auditContext)
    {
        this.linkFix = linkFix;
        this.auditContext = auditContext;
    }

    /// <summary>
    ///     Gets whether link fixing is on, and each site's host and state.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    [HttpGet]
    public async Task<IActionResult> Get(ulong guildId) => Ok(ToResponse(await linkFix.GetConfigAsync(guildId)));

    /// <summary>
    ///     Changes the link fixing settings. Fields left out stay as they are, and a site's host of null or an empty
    ///     string goes back to its default.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="request">The settings to change.</param>
    [HttpPost]
    public async Task<IActionResult> Update(ulong guildId, [FromBody] LinkFixUpdateRequest request)
    {
        var before = await linkFix.GetConfigAsync(guildId);
        auditContext.RecordBefore(ToResponse(before));

        foreach (var change in request.Platforms ?? [])
        {
            if (LinkFixPlatforms.Find(change.Name) is null)
                return BadRequest($"Unknown site {change.Name}");
            if (!string.IsNullOrWhiteSpace(change.Host) && !LinkFixPlatforms.IsValidHost(change.Host.Trim()))
                return BadRequest($"Invalid host for {change.Name}");
        }

        if (request.Enabled is { } enabled && enabled != before.Enabled)
            await linkFix.SetEnabledAsync(guildId, enabled);

        foreach (var change in request.Platforms ?? [])
        {
            var platform = LinkFixPlatforms.Find(change.Name)!;
            if (change.Enabled is { } platformEnabled)
                await linkFix.SetPlatformEnabledAsync(guildId, platform, platformEnabled);

            var host = string.IsNullOrWhiteSpace(change.Host) ? null : change.Host.Trim();
            if (host is null || host.Equals(platform.DefaultHost, StringComparison.OrdinalIgnoreCase))
                await linkFix.SetHostAsync(guildId, platform, null);
            else
                await linkFix.SetHostAsync(guildId, platform, host);
        }

        var after = ToResponse(await linkFix.GetConfigAsync(guildId));
        auditContext.RecordAfter(after);
        return Ok(after);
    }

    private static object ToResponse(LinkFixConfig config) => new
    {
        enabled = config.Enabled,
        platforms = LinkFixPlatforms.All.Select(platform => new
        {
            name = platform.Name,
            displayName = platform.DisplayName,
            defaultHost = platform.DefaultHost,
            host = config.Hosts.GetValueOrDefault(platform.Name, platform.DefaultHost),
            enabled = !config.DisabledPlatforms.Contains(platform.Name)
        })
    };
}

/// <summary>
///     Changes to a guild's link fixing settings.
/// </summary>
public class LinkFixUpdateRequest
{
    /// <summary>
    ///     Whether links are fixed, or null to leave it.
    /// </summary>
    public bool? Enabled { get; set; }

    /// <summary>
    ///     Changes to individual sites.
    /// </summary>
    public List<LinkFixPlatformUpdate>? Platforms { get; set; }
}

/// <summary>
///     A change to one site's link fixing.
/// </summary>
public class LinkFixPlatformUpdate
{
    /// <summary>
    ///     The site key, such as twitter.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    ///     Whether the site's links are fixed, or null to leave it.
    /// </summary>
    public bool? Enabled { get; set; }

    /// <summary>
    ///     The fixing host, or null or empty for the default.
    /// </summary>
    public string? Host { get; set; }
}
