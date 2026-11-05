using Mewdeko.Controllers.Common.UtilityExtras;
using Mewdeko.Modules.Permissions.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.Controllers;

/// <summary>
///     Reads and changes the rules that send command replies to a set channel for the dashboard.
/// </summary>
[ApiController]
[Route("botapi/[controller]/{guildId}")]
[Authorize("ApiKeyPolicy")]
public class CommandRepliesController(
    DiscordShardedClient client,
    CommandReplyRedirectService redirects,
    IDashboardAuditContext auditContext) : Controller
{
    /// <summary>
    ///     Lists a guild's reply redirect rules.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    [HttpGet]
    public async Task<IActionResult> GetRules(ulong guildId)
    {
        return Ok((await redirects.GetRulesAsync(guildId)).Select(r => new CommandReplyRedirectDto
        {
            Target = r.Target, IsModule = r.IsModule, ChannelId = r.ChannelId, DeleteInvoker = r.DeleteInvoker
        }));
    }

    /// <summary>
    ///     Sends a command's or module's replies to a channel, replacing its current rule.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="request">The rule.</param>
    [HttpPut]
    public async Task<IActionResult> SetRule(ulong guildId, [FromBody] CommandReplyRedirectDto request)
    {
        var guild = client.GetGuild(guildId);
        if (guild == null)
            return NotFound("Guild not found");

        if (guild.GetTextChannel(request.ChannelId) == null)
            return BadRequest("Channel not found");

        var resolved = redirects.ResolveTarget(request.Target);
        if (resolved == null)
            return BadRequest("No such command or module");

        var before = (await redirects.GetRulesAsync(guildId))
            .FirstOrDefault(r => r.Target == resolved.Value.Target && r.IsModule == resolved.Value.IsModule);
        if (before != null)
        {
            auditContext.RecordBefore(new CommandReplyRedirectDto
            {
                Target = before.Target,
                IsModule = before.IsModule,
                ChannelId = before.ChannelId,
                DeleteInvoker = before.DeleteInvoker
            });
        }

        await redirects.SetAsync(guildId, resolved.Value.Target, resolved.Value.IsModule, request.ChannelId,
            request.DeleteInvoker);

        var after = new CommandReplyRedirectDto
        {
            Target = resolved.Value.Target,
            IsModule = resolved.Value.IsModule,
            ChannelId = request.ChannelId,
            DeleteInvoker = request.DeleteInvoker
        };
        auditContext.RecordAfter(after);
        return Ok(after);
    }

    /// <summary>
    ///     Removes a reply redirect rule.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="target">The command or module name.</param>
    /// <param name="isModule">Whether the target is a module.</param>
    [HttpDelete("{target}")]
    public async Task<IActionResult> DeleteRule(ulong guildId, string target, [FromQuery] bool isModule = false)
    {
        var name = target.ToLowerInvariant();
        var before = (await redirects.GetRulesAsync(guildId))
            .FirstOrDefault(r => r.Target == name && r.IsModule == isModule);
        if (before == null)
            return NotFound("No such rule");

        auditContext.RecordBefore(new CommandReplyRedirectDto
        {
            Target = before.Target,
            IsModule = before.IsModule,
            ChannelId = before.ChannelId,
            DeleteInvoker = before.DeleteInvoker
        });
        await redirects.RemoveAsync(guildId, name, isModule);
        return Ok();
    }
}
