using DataModel;
using Mewdeko.Controllers.Common.Automations;
using Mewdeko.Modules.Automations.Common;
using Mewdeko.Modules.Automations.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.Controllers;

/// <summary>
///     Reads and changes a guild's automations for the dashboard.
/// </summary>
[ApiController]
[Route("botapi/[controller]/{guildId}")]
[Authorize("ApiKeyPolicy")]
public class AutomationsController(
    AutomationService service,
    DiscordShardedClient client,
    IDashboardAuditContext auditContext) : Controller
{
    /// <summary>
    ///     Lists the automations of a guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    [HttpGet]
    public async Task<IActionResult> List(ulong guildId) =>
        Ok((await service.GetAllAsync(guildId)).Select(AutomationDto.From));

    /// <summary>
    ///     Gets one automation.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="id">The automation ID.</param>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(ulong guildId, int id)
    {
        var automation = await service.GetAsync(guildId, id);
        return automation is null ? NotFound() : Ok(AutomationDto.From(automation));
    }

    /// <summary>
    ///     Creates an automation. Everything is checked first, and a 400 lists each problem with its path.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="request">The automation.</param>
    [HttpPost]
    public async Task<IActionResult> Create(ulong guildId, [FromBody] AutomationRequest? request)
    {
        if (request is null)
            return BadRequest();

        var result = await service.CreateAsync(guildId, request.UserId ?? 0, request.ToInput());
        if (result.Automation is null)
            return BadRequest(Errors(result));

        var dto = AutomationDto.From(result.Automation);
        auditContext.RecordAfter(dto);
        return Ok(dto);
    }

    /// <summary>
    ///     Replaces the settings, conditions and actions of an automation.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="id">The automation ID.</param>
    /// <param name="request">The automation.</param>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(ulong guildId, int id, [FromBody] AutomationRequest? request)
    {
        if (request is null)
            return BadRequest();

        var existing = await service.GetAsync(guildId, id);
        if (existing is null)
            return NotFound();

        auditContext.RecordBefore(AutomationDto.From(existing));
        var result = await service.UpdateAsync(guildId, id, request.ToInput());
        if (result.NotFound)
            return NotFound();

        if (result.Automation is null)
            return BadRequest(Errors(result));

        var dto = AutomationDto.From(result.Automation);
        auditContext.RecordAfter(dto);
        return Ok(dto);
    }

    /// <summary>
    ///     Turns an automation on or off.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="id">The automation ID.</param>
    /// <param name="request">Whether it should run.</param>
    [HttpPatch("{id:int}/enabled")]
    public async Task<IActionResult> SetEnabled(ulong guildId, int id, [FromBody] AutomationEnabledRequest? request)
    {
        if (request is null)
            return BadRequest();

        var existing = await service.GetAsync(guildId, id);
        if (existing is null)
            return NotFound();

        auditContext.RecordBefore(AutomationDto.From(existing));
        var automation = await service.SetEnabledAsync(guildId, id, request.Enabled);
        if (automation is null)
            return NotFound();

        var dto = AutomationDto.From(automation);
        auditContext.RecordAfter(dto);
        return Ok(dto);
    }

    /// <summary>
    ///     Deletes an automation and its run log.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="id">The automation ID.</param>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(ulong guildId, int id)
    {
        var existing = await service.GetAsync(guildId, id);
        if (existing is null)
            return NotFound();

        auditContext.RecordBefore(AutomationDto.From(existing));
        return await service.DeleteAsync(guildId, id) ? Ok() : NotFound();
    }

    /// <summary>
    ///     Runs an automation once as the given member, ignoring its conditions and cooldown. The run is marked as a
    ///     test in the run log.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="id">The automation ID.</param>
    /// <param name="request">The member to act as and the channel to run in.</param>
    [HttpPost("{id:int}/test")]
    public async Task<IActionResult> Test(ulong guildId, int id, [FromBody] AutomationTestRequest? request)
    {
        if (request is null || request.UserId == 0)
            return BadRequest();

        var guild = client.GetGuild(guildId);
        if (guild is null || await service.GetAsync(guildId, id) is null)
            return NotFound();

        var member = guild.GetUser(request.UserId);
        if (member is null)
            return BadRequest(new
            {
                Error = "UserNotInGuild"
            });

        IMessageChannel? channel = null;
        if (request.ChannelId is { } channelId &&
            (channel = guild.GetChannel(channelId) as IMessageChannel) is null)
            return BadRequest(new
            {
                Error = "ChannelNotFound"
            });

        var result = await service.RunTestAsync(guild, id, member, channel, null);
        if (result is null)
            return NotFound();

        return Ok(new AutomationTestResultDto
        {
            Success = result.Success,
            FailedActionIndex = result.FailedIndex,
            FailedActionType = result.FailedType,
            Reason = result.Reason
        });
    }

    /// <summary>
    ///     Gets the latest runs of an automation, newest first.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="id">The automation ID.</param>
    /// <param name="limit">The most runs to return, up to 50.</param>
    [HttpGet("{id:int}/runs")]
    public async Task<IActionResult> Runs(ulong guildId, int id, [FromQuery] int limit = AutomationLimits.RunsKept)
    {
        if (await service.GetAsync(guildId, id) is null)
            return NotFound();

        return Ok((await service.GetRunsAsync(guildId, id, limit)).Select(AutomationRunDto.From));
    }

    /// <summary>
    ///     Lists the starter templates and what each needs to be told.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    [HttpGet("templates")]
    public IActionResult Templates(ulong guildId) => Ok(AutomationTemplates.All.Select(x => new
    {
        x.Id,
        x.Name,
        x.Description,
        Parameters = x.Parameters.Select(p => new
        {
            p.Name,
            Kind = p.Kind.ToString(),
            p.Description,
            p.Required
        })
    }));

    /// <summary>
    ///     Creates the automations of a template and runs its one-time setup, such as posting a message with a button.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="templateId">The template ID.</param>
    /// <param name="request">What the template needs to be told.</param>
    [HttpPost("templates/{templateId}")]
    public async Task<IActionResult> CreateFromTemplate(ulong guildId, string templateId,
        [FromBody] AutomationTemplateRequest? request)
    {
        var guild = client.GetGuild(guildId);
        if (guild is null)
            return NotFound();

        request ??= new AutomationTemplateRequest();
        var outcome = await service.CreateFromTemplateAsync(templateId, guild, request.CreatorId ?? 0,
            request.ToValues());

        if (outcome.Error != AutomationTemplateError.None)
            return BadRequest(new
            {
                Error = outcome.Error.ToString(),
                outcome.Detail
            });

        var result = new AutomationTemplateResultDto
        {
            Created = outcome.Created.Select(AutomationDto.From).ToList(),
            SetupError = outcome.SetupError
        };

        foreach (var created in result.Created)
            auditContext.RecordAfter(created);

        return Ok(result);
    }

    /// <summary>
    ///     Describes every trigger, condition and action with its fields, types and descriptions, so an editor can be
    ///     built from it.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    [HttpGet("schema")]
    public IActionResult Schema(ulong guildId) => Ok(new
    {
        Triggers = AutomationSchema.Triggers.Select(x => new
        {
            Type = x.Type.ToString(),
            x.Name,
            x.Description,
            Provides = Flags(x.Provides)
        }),
        Conditions = AutomationSchema.Conditions.Select(Node),
        Actions = AutomationSchema.Actions.Select(Node),
        CooldownScopes = Enum.GetNames<AutomationCooldownScope>(),
        Placeholders = AutomationPlaceholders.All.Select(x => new
        {
            Name = x.Key,
            Description = x.Value
        }),
        Limits = new
        {
            AutomationLimits.MaxConditions,
            AutomationLimits.MaxActions,
            AutomationLimits.MaxNameLength,
            AutomationLimits.MaxListItems,
            AutomationLimits.MaxTextLength,
            AutomationLimits.MaxEmbedLength,
            AutomationLimits.MaxWaitSeconds,
            AutomationLimits.MaxTotalWaitSeconds,
            AutomationLimits.MaxCooldownSeconds,
            AutomationLimits.MaxButtons,
            AutomationLimits.MaxRunsPerMinute,
            AutomationLimits.RunsKept
        }
    });

    private static object Node(AutomationNodeSpec spec) => new
    {
        spec.Type,
        spec.Name,
        spec.Description,
        Requires = Flags(spec.Requires),
        Triggers = AutomationSchema.Triggers.Where(t => (spec.Requires & ~t.Provides) == 0)
            .Select(t => t.Type.ToString()),
        Fields = spec.Fields.Select(f => new
        {
            f.Name,
            Kind = f.Kind.ToString(),
            f.Description,
            f.Required,
            f.Min,
            f.Max,
            f.Options,
            f.Default
        })
    };

    private static IEnumerable<string> Flags(AutomationContext context) =>
        Enum.GetValues<AutomationContext>().Where(x => x != AutomationContext.None && context.HasFlag(x))
            .Select(x => x.ToString());

    private static object Errors(AutomationSaveResult result) => new
    {
        Errors = result.Errors.Select(x => new AutomationErrorDto
        {
            Path = x.Path, Message = x.Message
        })
    };
}
