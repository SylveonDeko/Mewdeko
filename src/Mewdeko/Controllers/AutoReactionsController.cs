using DataModel;
using Mewdeko.Controllers.Common.AutoReactions;
using Mewdeko.Modules.Utility.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.Controllers;

/// <summary>
///     Reads and changes a guild's auto reaction rules for the dashboard.
/// </summary>
[ApiController]
[Route("botapi/[controller]/{guildId}")]
[Authorize("ApiKeyPolicy")]
public class AutoReactionsController(
    AutoReactionService service,
    DiscordShardedClient client,
    IDashboardAuditContext auditContext) : Controller
{
    /// <summary>
    ///     Gets the rules and the limits.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    [HttpGet]
    public async Task<IActionResult> List(ulong guildId) => Ok(new AutoReactionListResponse
    {
        Rules = (await service.GetRulesAsync(guildId)).Select(ToDto).ToList(),
        MaxEmojis = AutoReactionService.MaxEmojis,
        MaxWords = AutoReactionService.MaxWords,
        MaxWordLength = AutoReactionService.MaxWordLength,
        ManagerRoleId = await service.GetManagerRoleAsync(guildId)
    });

    /// <summary>
    ///     Sets the order of the rules. Rules that are not listed go last.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="request">The rule IDs, first to last.</param>
    [HttpPut("order")]
    public async Task<IActionResult> Reorder(ulong guildId, [FromBody] AutoReactionOrderRequest request)
    {
        auditContext.RecordBefore((await service.GetRulesAsync(guildId)).Select(x => x.Id).ToList());
        await service.ReorderAsync(guildId, request.RuleIds);
        var after = (await service.GetRulesAsync(guildId)).Select(ToDto).ToList();
        auditContext.RecordAfter(after.Select(x => x.Id).ToList());
        return Ok(after);
    }

    /// <summary>
    ///     Gets the role whose members can manage auto reactions without Manage Server.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    [HttpGet("manager-role")]
    public async Task<IActionResult> GetManagerRole(ulong guildId) =>
        Ok(new AutoReactionManagerRequest
        {
            RoleId = await service.GetManagerRoleAsync(guildId)
        });

    /// <summary>
    ///     Sets or clears the role whose members can manage auto reactions without Manage Server.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="request">The role, or null to clear it.</param>
    [HttpPut("manager-role")]
    public async Task<IActionResult> SetManagerRole(ulong guildId, [FromBody] AutoReactionManagerRequest request)
    {
        var guild = client.GetGuild(guildId);
        if (guild is null)
            return NotFound("Guild not found");
        if (request.RoleId is { } roleId && guild.GetRole(roleId) is null)
            return BadRequest("Role not found");

        auditContext.RecordBefore(new AutoReactionManagerRequest
        {
            RoleId = await service.GetManagerRoleAsync(guildId)
        });
        await service.SetManagerRoleAsync(guildId, request.RoleId);
        auditContext.RecordAfter(request);
        return Ok(request);
    }

    /// <summary>
    ///     Gets one rule.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="id">The rule ID.</param>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(ulong guildId, int id)
    {
        var rule = await service.GetRuleAsync(guildId, id);
        return rule is null ? NotFound("Rule not found") : Ok(ToDto(rule));
    }

    /// <summary>
    ///     Creates a rule.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="request">The rule.</param>
    [HttpPost]
    public async Task<IActionResult> Create(ulong guildId, [FromBody] AutoReactionDto request)
    {
        var invalid = Check(guildId, request, out var emojis, out var skip, out var required);
        if (invalid is not null)
            return invalid;

        var rule = new AutoReaction();
        Apply(rule, request, emojis, skip, required);
        var (error, created) = await service.AddAsync(guildId, rule);
        if (created is null)
            return BadRequest(error.ToString());

        var after = ToDto(created);
        auditContext.RecordAfter(after);
        return Ok(after);
    }

    /// <summary>
    ///     Saves a rule. Every field is written.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="id">The rule ID.</param>
    /// <param name="request">The rule.</param>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(ulong guildId, int id, [FromBody] AutoReactionDto request)
    {
        var existing = await service.GetRuleAsync(guildId, id);
        if (existing is null)
            return NotFound("Rule not found");

        var invalid = Check(guildId, request, out var emojis, out var skip, out var required);
        if (invalid is not null)
            return invalid;

        auditContext.RecordBefore(ToDto(existing));
        var (error, rule) = await service.UpdateAsync(guildId, id, x => Apply(x, request, emojis, skip, required));
        if (error != AutoReactionError.None || rule is null)
            return BadRequest(error.ToString());

        var after = ToDto(rule);
        auditContext.RecordAfter(after);
        return Ok(after);
    }

    /// <summary>
    ///     Removes a rule.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="id">The rule ID.</param>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Remove(ulong guildId, int id)
    {
        var existing = await service.GetRuleAsync(guildId, id);
        if (existing is null)
            return NotFound("Rule not found");

        auditContext.RecordBefore(ToDto(existing));
        await service.RemoveAsync(guildId, id);
        return Ok();
    }

    private IActionResult? Check(ulong guildId, AutoReactionDto request, out string emojis, out string skip,
        out string required)
    {
        emojis = skip = required = "";
        var guild = client.GetGuild(guildId);
        if (guild is null)
            return NotFound("Guild not found");

        if (guild.GetChannel(request.ChannelId) is not (ITextChannel or IForumChannel))
            return BadRequest(AutoReactionError.InvalidChannel.ToString());

        var bad = AutoReactionService.TryParseEmojis(guild, string.Join(' ', request.Emojis), out emojis);
        if (bad is not null)
            return BadRequest($"Invalid emoji: {bad}");

        var skipWords = AutoReactionService.JoinWords(request.SkipWords);
        var requiredWords = AutoReactionService.JoinWords(request.RequiredWords);
        if (skipWords is null || requiredWords is null)
            return BadRequest(AutoReactionError.InvalidWords.ToString());

        skip = skipWords;
        required = requiredWords;
        return null;
    }

    private static void Apply(AutoReaction rule, AutoReactionDto request, string emojis, string skip, string required)
    {
        rule.ChannelId = request.ChannelId;
        rule.Emojis = emojis;
        rule.MessageTypes = request.MessageTypes;
        rule.IgnoreBots = request.IgnoreBots;
        rule.IgnoreWebhooks = request.IgnoreWebhooks;
        rule.IgnoreReplies = request.IgnoreReplies;
        rule.AllowedRoleIds = AutoReactionService.JoinIds(request.AllowedRoleIds);
        rule.IgnoredRoleIds = AutoReactionService.JoinIds(request.IgnoredRoleIds);
        rule.IgnoredUserIds = AutoReactionService.JoinIds(request.IgnoredUserIds);
        rule.SkipWords = skip;
        rule.RequiredWords = required;
        rule.DeleteNonMatching = request.DeleteNonMatching;
        rule.Enabled = request.Enabled;
    }

    private static AutoReactionDto ToDto(AutoReaction rule) => new()
    {
        Id = rule.Id,
        ChannelId = rule.ChannelId,
        Emojis = rule.Emojis.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList(),
        MessageTypes = rule.MessageTypes,
        IgnoreBots = rule.IgnoreBots,
        IgnoreWebhooks = rule.IgnoreWebhooks,
        IgnoreReplies = rule.IgnoreReplies,
        AllowedRoleIds = AutoReactionService.ParseIds(rule.AllowedRoleIds),
        IgnoredRoleIds = AutoReactionService.ParseIds(rule.IgnoredRoleIds),
        IgnoredUserIds = AutoReactionService.ParseIds(rule.IgnoredUserIds),
        SkipWords = AutoReactionService.ParseWords(rule.SkipWords),
        RequiredWords = AutoReactionService.ParseWords(rule.RequiredWords),
        Priority = rule.Priority,
        DeleteNonMatching = rule.DeleteNonMatching,
        Enabled = rule.Enabled
    };
}
