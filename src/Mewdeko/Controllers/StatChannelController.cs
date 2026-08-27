using Mewdeko.Modules.StatChannels.Common;
using Mewdeko.Modules.StatChannels.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.Controllers;

/// <summary>
///     API controller for managing stat channels.
/// </summary>
[ApiController]
[Route("botapi/[controller]/{guildId}")]
[Authorize("ApiKeyPolicy")]
public class StatChannelController(
    StatChannelService statChannelService,
    DiscordShardedClient client,
    IDashboardAuditContext auditContext) : Controller
{
    /// <summary>
    ///     Gets all stat channels for a guild.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetStatChannels(ulong guildId)
    {
        var channels = await statChannelService.GetStatChannelsAsync(guildId);
        var guild = client.GetGuild(guildId);

        var results = new List<object>();
        foreach (var sc in channels)
        {
            var voiceChannel = guild?.GetVoiceChannel(sc.ChannelId);
            results.Add(new
            {
                sc.Id,
                sc.ChannelId,
                channelName = voiceChannel?.Name ?? "Unknown",
                statType = sc.StatType,
                typeName = ((StatChannelType)sc.StatType).ToString(),
                sc.Template,
                sc.RoleId,
                roleName = sc.RoleId.HasValue ? guild?.GetRole(sc.RoleId.Value)?.Name : null,
                sc.CountdownDate,
                sc.GoalTarget,
                sc.DisplayStyle,
                styleName = ((StatChannelDisplayStyle)sc.DisplayStyle).ToString(),
                sc.StyleOptions,
                sc.UpdateMechanism,
                mechanismName = ((StatChannelUpdateMechanism)sc.UpdateMechanism).ToString(),
                sc.UpdateIntervalMinutes,
                sc.TargetId,
                sc.TargetName,
                sc.LastUpdateAt,
                currentValue = guild != null ? await statChannelService.ResolveStatValueAsync(sc, guild) : null,
                sc.DateAdded
            });
        }

        return Ok(results);
    }

    /// <summary>
    ///     Gets the catalogue of stat types, display styles and update mechanisms so the dashboard can render pickers
    ///     and worked examples without duplicating the bot's metadata.
    /// </summary>
    [HttpGet("metadata")]
    public IActionResult GetMetadata(ulong guildId)
    {
        var sampleOptions = new StatChannelStyleOptions();

        return Ok(new
        {
            commonPlaceholders = StatChannelDefinitions.CommonPlaceholders,
            statTypes = StatChannelDefinitions.All.Select(d => new
            {
                type = (int)d.Type,
                name = d.Name,
                category = d.Category,
                description = d.Description,
                defaultTemplate = d.DefaultTemplate,
                placeholders = d.Placeholders,
                valueKind = (int)d.ValueKind,
                valueKindName = d.ValueKind.ToString(),
                requirement = (int)d.Requirement,
                requirementName = d.Requirement.ToString(),
                example = d.Example,
                recommendedStyle = (int)d.RecommendedStyle,
                realtime = d.Realtime
            }),
            displayStyles = Enum.GetValues<StatChannelDisplayStyle>().Select(s => new
            {
                style = (int)s, name = s.ToString(), example = StatChannelFormatter.Format(1234, s, sampleOptions, 2000)
            }),
            mechanisms = Enum.GetValues<StatChannelUpdateMechanism>().Select(m => new
            {
                mechanism = (int)m,
                name = m.ToString(),
                minimumIntervalMinutes = m == StatChannelUpdateMechanism.Rename
                    ? StatChannelService.MinimumRenameIntervalMinutes
                    : StatChannelService.MinimumIntervalMinutes
            })
        });
    }

    /// <summary>
    ///     Gets the guild wide stat channel defaults.
    /// </summary>
    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings(ulong guildId)
    {
        var settings = await statChannelService.GetSettingsAsync(guildId);
        return Ok(new
        {
            settings.DefaultMechanism, settings.DefaultIntervalMinutes, settings.DefaultDisplayStyle
        });
    }

    /// <summary>
    ///     Updates the guild wide stat channel defaults.
    /// </summary>
    [HttpPut("settings")]
    public async Task<IActionResult> UpdateSettings(ulong guildId, [FromBody] StatChannelSettingsRequest request)
    {
        auditContext.RecordBefore(await statChannelService.GetSettingsAsync(guildId));

        var settings = await statChannelService.UpdateSettingsAsync(guildId,
            request.DefaultMechanism.HasValue
                ? (StatChannelUpdateMechanism)request.DefaultMechanism.Value
                : null,
            request.DefaultIntervalMinutes,
            request.DefaultDisplayStyle.HasValue
                ? (StatChannelDisplayStyle)request.DefaultDisplayStyle.Value
                : null);

        auditContext.RecordAfter(settings);
        return Ok(new
        {
            settings.DefaultMechanism, settings.DefaultIntervalMinutes, settings.DefaultDisplayStyle
        });
    }

    /// <summary>
    ///     Renders a template without saving it, so the dashboard can show a live preview.
    /// </summary>
    [HttpPost("preview")]
    public async Task<IActionResult> Preview(ulong guildId, [FromBody] StatChannelPreviewRequest request)
    {
        var rendered = await statChannelService.PreviewAsync(guildId, (StatChannelType)request.StatType,
            request.Template ?? StatChannelDefinitions.DefaultTemplate((StatChannelType)request.StatType),
            ToOptions(request));

        if (rendered == null)
            return NotFound("Guild not found");

        return Ok(new
        {
            rendered
        });
    }

    /// <summary>
    ///     Adds a stat channel. If channelId is 0, creates a new voice channel.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> AddStatChannel(ulong guildId, [FromBody] AddStatChannelRequest request)
    {
        var guild = client.GetGuild(guildId);
        if (guild == null)
            return NotFound("Guild not found");

        var statType = (StatChannelType)request.StatType;
        var template = request.Template ?? StatChannelDefinitions.DefaultTemplate(statType);
        var options = ToOptions(request);

        try
        {
            if (request.ChannelId == 0)
            {
                var (sc, vc) = await statChannelService.CreateStatChannelAsync(
                    guild, statType, template, request.CategoryId, request.RoleId,
                    request.CountdownDate, request.GoalTarget ?? 0, options);

                return Ok(new
                {
                    sc.Id,
                    channelId = vc.Id,
                    sc.StatType,
                    sc.Template,
                    sc.DisplayStyle,
                    sc.UpdateMechanism,
                    sc.UpdateIntervalMinutes,
                    channelName = vc.Name
                });
            }

            var existingSc = await statChannelService.AddStatChannelAsync(
                guildId, request.ChannelId, statType, template, request.RoleId,
                request.CountdownDate, request.GoalTarget ?? 0, options);

            return Ok(new
            {
                existingSc.Id,
                existingSc.ChannelId,
                existingSc.StatType,
                existingSc.Template,
                existingSc.DisplayStyle,
                existingSc.UpdateMechanism,
                existingSc.UpdateIntervalMinutes
            });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }
    }

    /// <summary>
    ///     Updates a stat channel. Only the fields present on the request are changed.
    /// </summary>
    [HttpPut("{channelId}")]
    public async Task<IActionResult> UpdateStatChannel(ulong guildId, ulong channelId,
        [FromBody] UpdateStatChannelRequest request)
    {
        auditContext.RecordBefore(
            (await statChannelService.GetStatChannelsAsync(guildId)).FirstOrDefault(c => c.ChannelId == channelId));

        var options = ToOptions(request);
        if (request.StatType.HasValue)
            options.StatType = (StatChannelType)request.StatType.Value;

        var sc = await statChannelService.UpdateStatChannelConfigAsync(guildId, channelId, options);
        if (sc == null)
            return NotFound("Stat channel not found");

        auditContext.RecordAfter(sc);
        return Ok(new
        {
            sc.Id,
            sc.ChannelId,
            sc.StatType,
            sc.Template,
            sc.DisplayStyle,
            sc.StyleOptions,
            sc.UpdateMechanism,
            sc.UpdateIntervalMinutes
        });
    }

    /// <summary>
    ///     Removes a stat channel.
    /// </summary>
    [HttpDelete("{channelId}")]
    public async Task<IActionResult> RemoveStatChannel(ulong guildId, ulong channelId)
    {
        auditContext.RecordBefore(
            (await statChannelService.GetStatChannelsAsync(guildId)).FirstOrDefault(c => c.ChannelId == channelId));
        var removed = await statChannelService.RemoveStatChannelAsync(guildId, channelId);
        if (!removed)
            return NotFound("Stat channel not found");

        return Ok(new
        {
            success = true
        });
    }

    private static StatChannelOptions ToOptions(StatChannelRequestBase request)
    {
        return new StatChannelOptions
        {
            Template = request.Template,
            DisplayStyle = request.DisplayStyle.HasValue
                ? (StatChannelDisplayStyle)request.DisplayStyle.Value
                : null,
            StyleOptions = request.StyleOptions,
            Mechanism = request.UpdateMechanism.HasValue
                ? (StatChannelUpdateMechanism)request.UpdateMechanism.Value
                : null,
            UpdateIntervalMinutes = request.UpdateIntervalMinutes,
            RoleId = request.RoleId,
            CountdownDate = request.CountdownDate,
            GoalTarget = request.GoalTarget,
            TargetId = request.TargetId,
            TargetName = request.TargetName
        };
    }
}

/// <summary>
///     Fields shared by every stat channel write request.
/// </summary>
public abstract class StatChannelRequestBase
{
    /// <summary>
    ///     The display template.
    /// </summary>
    public string? Template { get; set; }

    /// <summary>
    ///     The role ID for role member counts.
    /// </summary>
    public ulong? RoleId { get; set; }

    /// <summary>
    ///     The countdown target date.
    /// </summary>
    public DateTime? CountdownDate { get; set; }

    /// <summary>
    ///     The member goal target.
    /// </summary>
    public int? GoalTarget { get; set; }

    /// <summary>
    ///     The display style applied to the resolved number.
    /// </summary>
    public int? DisplayStyle { get; set; }

    /// <summary>
    ///     Tuning for the chosen display style.
    /// </summary>
    public StatChannelStyleOptions? StyleOptions { get; set; }

    /// <summary>
    ///     How updates are pushed to Discord: rename, recreate, or auto.
    /// </summary>
    public int? UpdateMechanism { get; set; }

    /// <summary>
    ///     How often the channel refreshes, in minutes.
    /// </summary>
    public int? UpdateIntervalMinutes { get; set; }

    /// <summary>
    ///     A snowflake or row target such as a counting channel or Minecraft server.
    /// </summary>
    public ulong? TargetId { get; set; }

    /// <summary>
    ///     A named target such as a Twitch chat counter.
    /// </summary>
    public string? TargetName { get; set; }
}

/// <summary>
///     Request to add a stat channel.
/// </summary>
public class AddStatChannelRequest : StatChannelRequestBase
{
    /// <summary>
    ///     The voice channel ID. Set to 0 to create a new channel.
    /// </summary>
    public ulong ChannelId { get; set; }

    /// <summary>
    ///     The category ID to create the channel in (when ChannelId is 0).
    /// </summary>
    public ulong? CategoryId { get; set; }

    /// <summary>
    ///     The stat type.
    /// </summary>
    public int StatType { get; set; }
}

/// <summary>
///     Request to update a stat channel. Omitted fields are left unchanged.
/// </summary>
public class UpdateStatChannelRequest : StatChannelRequestBase
{
    /// <summary>
    ///     The new stat type, when changing it.
    /// </summary>
    public int? StatType { get; set; }
}

/// <summary>
///     Request to render a template without saving it.
/// </summary>
public class StatChannelPreviewRequest : StatChannelRequestBase
{
    /// <summary>
    ///     The stat type to render.
    /// </summary>
    public int StatType { get; set; }
}

/// <summary>
///     Request to update the guild wide stat channel defaults.
/// </summary>
public class StatChannelSettingsRequest
{
    /// <summary>
    ///     The default update mechanism for new stat channels.
    /// </summary>
    public int? DefaultMechanism { get; set; }

    /// <summary>
    ///     The default refresh interval in minutes.
    /// </summary>
    public int? DefaultIntervalMinutes { get; set; }

    /// <summary>
    ///     The default display style.
    /// </summary>
    public int? DefaultDisplayStyle { get; set; }
}