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
    ILogger<StatChannelController> logger) : Controller
{
    /// <summary>
    ///     Gets all stat channels for a guild.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetStatChannels(ulong guildId)
    {
        var channels = await statChannelService.GetStatChannelsAsync(guildId);
        var guild = client.GetGuild(guildId);

        return Ok(channels.Select(sc =>
        {
            var voiceChannel = guild?.GetVoiceChannel(sc.ChannelId);
            return new
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
                currentValue = guild != null ? statChannelService.ResolveStatValue(sc, guild) : null,
                sc.DateAdded
            };
        }));
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

        try
        {
            if (request.ChannelId == 0)
            {
                var (sc, vc) = await statChannelService.CreateStatChannelAsync(
                    guild, (StatChannelType)request.StatType, request.Template ?? "%count%",
                    request.CategoryId, request.RoleId, request.CountdownDate, request.GoalTarget);

                return Ok(new
                {
                    sc.Id,
                    channelId = vc.Id,
                    sc.StatType,
                    sc.Template,
                    channelName = vc.Name
                });
            }

            var existingSc = await statChannelService.AddStatChannelAsync(
                guildId, request.ChannelId, (StatChannelType)request.StatType,
                request.Template ?? "%count%", request.RoleId, request.CountdownDate, request.GoalTarget);

            return Ok(new
            {
                existingSc.Id, existingSc.ChannelId, existingSc.StatType, existingSc.Template
            });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }
    }

    /// <summary>
    ///     Updates a stat channel's template.
    /// </summary>
    [HttpPut("{channelId}")]
    public async Task<IActionResult> UpdateStatChannel(ulong guildId, ulong channelId,
        [FromBody] UpdateStatChannelRequest request)
    {
        var sc = await statChannelService.UpdateTemplateAsync(guildId, channelId, request.Template);
        if (sc == null)
            return NotFound("Stat channel not found");

        return Ok(new
        {
            sc.Id, sc.ChannelId, sc.StatType, sc.Template
        });
    }

    /// <summary>
    ///     Removes a stat channel.
    /// </summary>
    [HttpDelete("{channelId}")]
    public async Task<IActionResult> RemoveStatChannel(ulong guildId, ulong channelId)
    {
        var removed = await statChannelService.RemoveStatChannelAsync(guildId, channelId);
        if (!removed)
            return NotFound("Stat channel not found");

        return Ok(new
        {
            success = true
        });
    }
}

/// <summary>
///     Request to add a stat channel.
/// </summary>
public class AddStatChannelRequest
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
    ///     The stat type (0-11).
    /// </summary>
    public int StatType { get; set; }

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
    public int GoalTarget { get; set; }
}

/// <summary>
///     Request to update a stat channel template.
/// </summary>
public class UpdateStatChannelRequest
{
    /// <summary>
    ///     The new display template.
    /// </summary>
    public string Template { get; set; } = "";
}