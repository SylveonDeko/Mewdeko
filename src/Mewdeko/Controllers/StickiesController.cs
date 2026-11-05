using Mewdeko.Controllers.Common.UtilityExtras;
using Mewdeko.Modules.Utility.Common;
using Mewdeko.Modules.Utility.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.Controllers;

/// <summary>
///     Reads and changes a guild's sticky messages and sticky templates for the dashboard.
/// </summary>
[ApiController]
[Route("botapi/[controller]/{guildId}")]
[Authorize("ApiKeyPolicy")]
public class StickiesController(
    DiscordShardedClient client,
    StickyService stickies,
    MessageRepeaterService repeaters,
    IDashboardAuditContext auditContext) : Controller
{
    /// <summary>
    ///     Lists the stickies in a guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    [HttpGet]
    public IActionResult GetStickies(ulong guildId)
    {
        return Ok(stickies.GetStickies(guildId).Select(ToDto));
    }

    /// <summary>
    ///     Sticks a message to a channel, replacing the channel's sticky if it has one, and sets when it returns.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="request">The sticky.</param>
    [HttpPut]
    public async Task<IActionResult> SetSticky(ulong guildId, [FromBody] StickyDto request)
    {
        var problem = Validate(guildId, request.ChannelId);
        if (problem != null)
            return problem;

        if (string.IsNullOrWhiteSpace(request.Content))
            return BadRequest("Nothing to stick");

        if (request.Messages is < 1 or > 1000 || request.Seconds is < 5 or > 21600)
            return BadRequest("Messages must be 1 to 1000 and seconds 5 to 21600");

        var before = stickies.GetSticky(guildId, request.ChannelId);
        if (before != null)
            auditContext.RecordBefore(ToDto(before));

        var result = await stickies.StickAsync(guildId, request.ChannelId, request.Content);
        if (result == StickyResult.Failed)
            return BadRequest("The sticky could not be created");

        await stickies.TuneAsync(guildId, request.ChannelId, request.Messages, TimeSpan.FromSeconds(request.Seconds));

        var after = ToDto(stickies.GetSticky(guildId, request.ChannelId)!);
        auditContext.RecordAfter(after);
        return Ok(after);
    }

    /// <summary>
    ///     Removes a channel's sticky.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="channelId">The channel ID.</param>
    [HttpDelete("{channelId}")]
    public async Task<IActionResult> DeleteSticky(ulong guildId, ulong channelId)
    {
        if (!stickies.Ready)
            return StatusCode(503, "Stickies are still loading");

        var before = stickies.GetSticky(guildId, channelId);
        if (before == null)
            return NotFound("Channel has no sticky");

        auditContext.RecordBefore(ToDto(before));
        await stickies.UnstickAsync(guildId, channelId);
        return Ok();
    }

    /// <summary>
    ///     Removes every sticky in a guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>How many stickies were removed.</returns>
    [HttpDelete]
    public async Task<IActionResult> ClearStickies(ulong guildId)
    {
        if (!stickies.Ready)
            return StatusCode(503, "Stickies are still loading");

        return Ok(await stickies.ClearAsync(guildId));
    }

    /// <summary>
    ///     Lists a guild's sticky templates.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    [HttpGet("templates")]
    public async Task<IActionResult> GetTemplates(ulong guildId)
    {
        return Ok((await stickies.GetTemplatesAsync(guildId))
            .Select(t => new StickyTemplateDto
            {
                Name = t.Name, Content = t.Content
            }));
    }

    /// <summary>
    ///     Saves a sticky template, replacing the content of one with the same name.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="request">The template.</param>
    [HttpPut("templates")]
    public async Task<IActionResult> SaveTemplate(ulong guildId, [FromBody] StickyTemplateDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Content))
            return BadRequest("A template needs a name and content");

        var existing = await stickies.GetTemplateAsync(guildId, request.Name);
        if (existing != null)
        {
            auditContext.RecordBefore(new StickyTemplateDto
            {
                Name = existing.Name, Content = existing.Content
            });
        }

        await stickies.SaveTemplateAsync(guildId, request.Name.Trim(), request.Content);
        auditContext.RecordAfter(request);
        return Ok(request);
    }

    /// <summary>
    ///     Deletes a sticky template.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="name">The template name.</param>
    [HttpDelete("templates/{name}")]
    public async Task<IActionResult> DeleteTemplate(ulong guildId, string name)
    {
        var existing = await stickies.GetTemplateAsync(guildId, name);
        if (existing == null)
            return NotFound("Template not found");

        auditContext.RecordBefore(new StickyTemplateDto
        {
            Name = existing.Name, Content = existing.Content
        });
        await stickies.DeleteTemplateAsync(guildId, name);
        return Ok();
    }

    /// <summary>
    ///     Sticks a saved template to a channel.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="name">The template name.</param>
    /// <param name="request">The channel.</param>
    [HttpPost("templates/{name}/use")]
    public async Task<IActionResult> UseTemplate(ulong guildId, string name, [FromBody] UseStickyTemplateRequest request)
    {
        var problem = Validate(guildId, request.ChannelId);
        if (problem != null)
            return problem;

        var template = await stickies.GetTemplateAsync(guildId, name);
        if (template == null)
            return NotFound("Template not found");

        var result = await stickies.StickAsync(guildId, request.ChannelId, template.Content);
        return result == StickyResult.Failed ? BadRequest("The sticky could not be created") : Ok();
    }

    private IActionResult? Validate(ulong guildId, ulong channelId)
    {
        if (!stickies.Ready || !repeaters.RepeaterReady)
            return StatusCode(503, "Stickies are still loading");

        var guild = client.GetGuild(guildId);
        if (guild == null)
            return NotFound("Guild not found");

        return guild.GetTextChannel(channelId) == null ? BadRequest("Channel not found") : null;
    }

    private static StickyDto ToDto(RepeatRunner runner)
    {
        TimeSpan.TryParse(runner.Repeater.ActivityTimeWindow, out var window);
        return new StickyDto
        {
            ChannelId = runner.Repeater.ChannelId,
            Content = runner.Repeater.Message ?? "",
            Messages = runner.Repeater.ActivityThreshold,
            Seconds = (int)(window == TimeSpan.Zero ? StickyDefaults.Window : window).TotalSeconds
        };
    }
}
