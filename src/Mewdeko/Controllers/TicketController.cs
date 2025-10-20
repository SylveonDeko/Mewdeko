using System.Text.Json;
using DataModel;
using Mewdeko.Controllers.Common.Tickets;
using Mewdeko.Modules.Tickets.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.Controllers;

/// <summary>
///     Controller for managing ticket system functionality
/// </summary>
[ApiController]
[Route("botapi/[controller]/{guildId}")]
[Authorize("ApiKeyPolicy")]
public class TicketController : Controller
{
    private readonly DiscordShardedClient client;
    private readonly ILogger<TicketController> logger;
    private readonly TicketService ticketService;


    /// <summary>
    ///     Initializes a new instance of the TicketController
    /// </summary>
    /// <param name="ticketService">Service for managing ticket operations</param>
    /// <param name="client">Discord client instance</param>
    /// <param name="logger">The logger instance for structured logging.</param>
    public TicketController(TicketService ticketService, DiscordShardedClient client, ILogger<TicketController> logger)
    {
        this.ticketService = ticketService;
        this.client = client;
        this.logger = logger;
    }

    #region Panel Management

    /// <summary>
    ///     Retrieves all ticket panels for a guild
    /// </summary>
    /// <param name="guildId">The ID of the guild to get panels for</param>
    /// <returns>List of ticket panels with resolved channel information</returns>
    [HttpGet("panels")]
    public async Task<IActionResult> GetAllPanels(ulong guildId)
    {
        try
        {
            var panels = await ticketService.GetPanelsAsync(guildId);
            IGuild guild = client.GetGuild(guildId);

            if (guild == null)
                return NotFound("Guild not found");

            var result = await Task.WhenAll(panels.Select(async panel =>
            {
                var channel = await guild.GetTextChannelAsync(panel.ChannelId);
                SmartEmbed.TryParse(panel.EmbedJson, guildId, out var embeds, out var text, out var builder);
                return new
                {
                    panel.Id,
                    panel.MessageId,
                    panel.GuildId,
                    panel.ChannelId,
                    ChannelName = channel?.Name ?? "Deleted Channel",
                    ChannelMention = channel != null ? MentionUtils.MentionChannel(channel.Id) : null,
                    panel.EmbedJson,
                    ButtonCount = panel.PanelButtons?.Count() ?? 0,
                    SelectMenuCount = panel.PanelSelectMenus?.Count() ?? 0
                };
            }));

            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving panels for guild {GuildId}", guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    ///     Creates a new ticket panel in the specified channel
    /// </summary>
    /// <param name="guildId">The ID of the guild to create the panel in</param>
    /// <param name="request">Panel creation request containing channel ID and embed configuration</param>
    /// <returns>The created panel information</returns>
    [HttpPost("panels")]
    public async Task<IActionResult> CreatePanel(ulong guildId, [FromBody] CreatePanelRequest request)
    {
        try
        {
            IGuild guild = client.GetGuild(guildId);
            if (guild == null)
                return NotFound("Guild not found");

            logger.LogInformation(JsonSerializer.Serialize(request));
            var channel = await guild.GetTextChannelAsync(request.ChannelId);
            if (channel == null)
                return NotFound("Channel not found");

            var panel = await ticketService.CreatePanelAsync(
                channel,
                request.EmbedJson ?? "",
                request.Title,
                request.Description,
                request.Color);

            return Ok(new
            {
                panel.Id,
                panel.MessageId,
                panel.GuildId,
                panel.ChannelId,
                ChannelName = channel.Name
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating panel in guild {GuildId}", guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    ///     Deletes a ticket panel
    /// </summary>
    /// <param name="guildId">The ID of the guild containing the panel</param>
    /// <param name="panelId">The message ID of the panel to delete</param>
    /// <param name="force">Whether to force deletion even if there are active tickets</param>
    /// <returns>Success response with deletion details</returns>
    [HttpDelete("panels/{panelId}")]
    public async Task<IActionResult> DeletePanel(ulong guildId, ulong panelId, [FromQuery] bool force = false)
    {
        try
        {
            IGuild guild = client.GetGuild(guildId);
            if (guild == null)
                return NotFound("Guild not found");

            var (success, error, activeTickets, deletedTickets) =
                await ticketService.DeletePanelAsync(panelId, guild, force);

            if (success)
            {
                return Ok(new
                {
                    Success = true,
                    ActiveTicketsCleared = activeTickets?.Count ?? 0,
                    DeletedTicketsCleared = deletedTickets?.Count ?? 0
                });
            }

            return BadRequest(new
            {
                Success = false, Error = error, ActiveTickets = activeTickets, DeletedTickets = deletedTickets
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting panel {PanelId} in guild {GuildId}", panelId, guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    ///     Updates a panel's embed configuration
    /// </summary>
    /// <param name="guildId">The ID of the guild containing the panel</param>
    /// <param name="panelId">The ID of the panel to update</param>
    /// <param name="request">Updated embed configuration</param>
    /// <returns>Success or failure response</returns>
    [HttpPut("panels/{panelId}/embed")]
    public async Task<IActionResult> UpdatePanelEmbed(ulong guildId, ulong panelId,
        [FromBody] UpdateEmbedRequest request)
    {
        try
        {
            IGuild guild = client.GetGuild(guildId);
            if (guild == null)
                return NotFound("Guild not found");

            var success = await ticketService.UpdatePanelEmbedAsync(guild, panelId, request.EmbedJson);

            if (success)
                return Ok();
            return BadRequest("Failed to update panel embed");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating panel {PanelId} embed in guild {GuildId}", panelId, guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    ///     Moves a panel to a different channel
    /// </summary>
    /// <param name="guildId">The ID of the guild containing the panel</param>
    /// <param name="panelId">The message ID of the panel to move</param>
    /// <param name="request">Request containing the target channel ID</param>
    /// <returns>Success or failure response</returns>
    [HttpPut("panels/{panelId}/move")]
    public async Task<IActionResult> MovePanel(ulong guildId, ulong panelId, [FromBody] MovePanelRequest request)
    {
        try
        {
            IGuild guild = client.GetGuild(guildId);
            if (guild == null)
                return NotFound("Guild not found");

            var success = await ticketService.MovePanelAsync(guild, panelId, request.ChannelId);

            if (success)
                return Ok();
            return BadRequest("Failed to move panel");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error moving panel {PanelId} in guild {GuildId}", panelId, guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    ///     Duplicates a panel to another channel
    /// </summary>
    /// <param name="guildId">The ID of the guild containing the panel</param>
    /// <param name="panelId">The message ID of the panel to duplicate</param>
    /// <param name="request">Request containing the target channel ID</param>
    /// <returns>The newly created panel information</returns>
    [HttpPost("panels/{panelId}/duplicate")]
    public async Task<IActionResult> DuplicatePanel(ulong guildId, ulong panelId,
        [FromBody] DuplicatePanelRequest request)
    {
        try
        {
            IGuild guild = client.GetGuild(guildId);
            if (guild == null)
                return NotFound("Guild not found");

            var newPanel = await ticketService.DuplicatePanelAsync(guild, panelId, request.ChannelId);

            if (newPanel != null)
            {
                var channel = await guild.GetTextChannelAsync(newPanel.ChannelId);
                return Ok(new
                {
                    newPanel.Id,
                    newPanel.MessageId,
                    newPanel.GuildId,
                    newPanel.ChannelId,
                    ChannelName = channel?.Name ?? "Unknown"
                });
            }

            return BadRequest("Failed to duplicate panel");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error duplicating panel {PanelId} in guild {GuildId}", panelId, guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    ///     Recreates a deleted panel message
    /// </summary>
    /// <param name="guildId">The ID of the guild containing the panel</param>
    /// <param name="panelId">The message ID of the panel to recreate</param>
    /// <returns>Success response with new message ID</returns>
    [HttpPost("panels/{panelId}/recreate")]
    public async Task<IActionResult> RecreatePanel(ulong guildId, ulong panelId)
    {
        try
        {
            var (success, newMessageId, channelMention, error) =
                await ticketService.RecreatePanelAsync(guildId, panelId);

            if (success)
            {
                return Ok(new
                {
                    Success = true, NewMessageId = newMessageId, ChannelMention = channelMention
                });
            }

            return BadRequest(new
            {
                Success = false, Error = error
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error recreating panel {PanelId} in guild {GuildId}", panelId, guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    ///     Checks the status of a single panel
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="panelId">The message ID of the panel to check</param>
    /// <returns>Panel status information</returns>
    [HttpGet("panels/{panelId}/status")]
    public async Task<IActionResult> CheckSinglePanelStatus(ulong guildId, ulong panelId)
    {
        try
        {
            IGuild guild = client.GetGuild(guildId);
            if (guild == null)
                return NotFound("Guild not found");

            var panel = await ticketService.GetPanelAsync(panelId);
            if (panel == null || panel.GuildId != guildId)
                return NotFound("Panel not found");

            var channel = await guild.GetTextChannelAsync(panel.ChannelId);
            if (channel == null)
            {
                return Ok(new
                {
                    PanelId = panelId, Status = 2
                }); // Channel deleted
            }

            var message = await channel.GetMessageAsync(panel.MessageId);
            if (message == null)
            {
                return Ok(new
                {
                    PanelId = panelId, Status = 1
                }); // Message deleted
            }

            return Ok(new
            {
                PanelId = panelId, Status = 0
            }); // OK
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error checking panel {PanelId} status in guild {GuildId}", panelId, guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    ///     Checks the status of all panels in a guild
    /// </summary>
    /// <param name="guildId">The ID of the guild to check panels for</param>
    /// <returns>List of panel statuses</returns>
    [HttpGet("panels/status")]
    public async Task<IActionResult> CheckPanelStatus(ulong guildId)
    {
        try
        {
            var panelStatuses = await ticketService.CheckPanelStatusAsync(guildId);
            return Ok(panelStatuses);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error checking panel status for guild {GuildId}", guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    ///     Recreates all panels with missing messages
    /// </summary>
    /// <param name="guildId">The ID of the guild to recreate panels for</param>
    /// <returns>Summary of recreation results</returns>
    [HttpPost("panels/recreate-all")]
    public async Task<IActionResult> RecreateAllPanels(ulong guildId)
    {
        try
        {
            var (recreated, failed, errors) = await ticketService.RecreateAllMissingPanelsAsync(guildId);

            return Ok(new
            {
                Recreated = recreated, Failed = failed, Errors = errors
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error recreating all panels for guild {GuildId}", guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    #endregion

    #region Button Management

    /// <summary>
    ///     Gets all buttons for a specific panel
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="panelId">The message ID of the panel</param>
    /// <returns>List of buttons with their configurations</returns>
    [HttpGet("panels/{panelId}/buttons")]
    public async Task<IActionResult> GetPanelButtons(ulong guildId, ulong panelId)
    {
        try
        {
            var buttons = await ticketService.GetPanelButtonsAsync(panelId);
            return Ok(buttons);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting buttons for panel {PanelId} in guild {GuildId}", panelId, guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    ///     Adds a button to a panel
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="panelId">The message ID of the panel</param>
    /// <param name="request">Button configuration request</param>
    /// <returns>The created button information</returns>
    [HttpPost("panels/{panelId}/buttons")]
    public async Task<IActionResult> AddButton(ulong guildId, ulong panelId, [FromBody] AddButtonRequest request)
    {
        try
        {
            var panel = await ticketService.GetPanelAsync(panelId);
            if (panel == null || panel.GuildId != guildId)
                return NotFound("Panel not found");

            var button = await ticketService.AddButtonAsync(
                panel,
                request.Label,
                request.Emoji,
                request.Style,
                request.OpenMessageJson,
                request.ModalJson,
                request.ChannelFormat,
                request.CategoryId,
                request.ArchiveCategoryId,
                request.SupportRoles,
                request.ViewerRoles,
                request.AutoCloseTime,
                request.RequiredResponseTime,
                request.MaxActiveTickets,
                request.AllowedPriorities,
                request.DefaultPriority);

            return Ok(new
            {
                button.Id,
                button.Label,
                button.CustomId,
                button.Style,
                button.Emoji
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error adding button to panel {PanelId} in guild {GuildId}", panelId, guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    ///     Gets detailed information about a specific button
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="buttonId">The ID of the button</param>
    /// <returns>Detailed button configuration</returns>
    [HttpGet("buttons/{buttonId}")]
    public async Task<IActionResult> GetButton(ulong guildId, int buttonId)
    {
        try
        {
            var button = await ticketService.GetButtonAsync(buttonId);
            if (button == null || button.Panel.GuildId != guildId)
                return NotFound("Button not found");

            IGuild guild = client.GetGuild(guildId);
            var category = button.CategoryId.HasValue
                ? await guild.GetCategoryChannelAsync(button.CategoryId.Value)
                : null;
            var archiveCategory = button.ArchiveCategoryId.HasValue
                ? await guild.GetCategoryChannelAsync(button.ArchiveCategoryId.Value)
                : null;

            return Ok(new
            {
                button.Id,
                button.Label,
                button.Style,
                button.Emoji,
                button.CustomId,
                button.ChannelNameFormat,
                button.CategoryId,
                CategoryName = category?.Name,
                button.ArchiveCategoryId,
                ArchiveCategoryName = archiveCategory?.Name,
                button.SupportRoles,
                button.ViewerRoles,
                button.AutoCloseTime,
                button.RequiredResponseTime,
                button.MaxActiveTickets,
                button.AllowedPriorities,
                button.DefaultPriority,
                button.SaveTranscript,
                button.DeleteOnClose,
                button.LockOnClose,
                button.RenameOnClose,
                button.RemoveCreatorOnClose,
                button.DeleteDelay,
                button.LockOnArchive,
                button.RenameOnArchive,
                button.RemoveCreatorOnArchive,
                button.AutoArchiveOnClose,
                button.OpenMessageJson,
                button.ModalJson
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting button {ButtonId} in guild {GuildId}", buttonId, guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    ///     Updates button settings
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="buttonId">The ID of the button to update</param>
    /// <param name="request">Updated button settings</param>
    /// <returns>Success or failure response</returns>
    [HttpPut("buttons/{buttonId}")]
    public async Task<IActionResult> UpdateButton(ulong guildId, int buttonId, [FromBody] UpdateButtonRequest request)
    {
        try
        {
            logger.LogInformation("UpdateButton called - Request deserialized successfully");

            IGuild guild = client.GetGuild(guildId);
            if (guild == null)
                return NotFound("Guild not found");

            var settings = new Dictionary<string, object>();

            if (request.Label != null) settings["label"] = request.Label;
            if (request.Emoji != null) settings["emoji"] = request.Emoji;
            if (request.Style.HasValue) settings["style"] = (int)request.Style.Value;
            if (request.CategoryId.HasValue) settings["categoryId"] = request.CategoryId.Value;
            if (request.ArchiveCategoryId.HasValue) settings["archiveCategoryId"] = request.ArchiveCategoryId.Value;
            if (request.SupportRoles != null) settings["supportRoles"] = request.SupportRoles.ToArray();
            if (request.ViewerRoles != null) settings["viewerRoles"] = request.ViewerRoles.ToArray();
            if (request.AutoCloseTime.HasValue) settings["autoCloseTime"] = request.AutoCloseTime.Value;
            if (request.RequiredResponseTime.HasValue)
                settings["requiredResponseTime"] = request.RequiredResponseTime.Value;
            if (request.MaxActiveTickets.HasValue) settings["maxActiveTickets"] = request.MaxActiveTickets.Value;
            if (request.AllowedPriorities != null) settings["allowedPriorities"] = request.AllowedPriorities.ToArray();
            if (request.DefaultPriority != null) settings["defaultPriority"] = request.DefaultPriority;
            if (request.SaveTranscript.HasValue) settings["saveTranscript"] = request.SaveTranscript.Value;
            if (request.DeleteOnClose.HasValue) settings["deleteOnClose"] = request.DeleteOnClose.Value;
            if (request.LockOnClose.HasValue) settings["lockOnClose"] = request.LockOnClose.Value;
            if (request.RenameOnClose.HasValue) settings["renameOnClose"] = request.RenameOnClose.Value;
            if (request.RemoveCreatorOnClose.HasValue)
                settings["removeCreatorOnClose"] = request.RemoveCreatorOnClose.Value;
            if (request.DeleteDelay.HasValue) settings["deleteDelay"] = request.DeleteDelay.Value;
            if (request.LockOnArchive.HasValue) settings["lockOnArchive"] = request.LockOnArchive.Value;
            if (request.RenameOnArchive.HasValue) settings["renameOnArchive"] = request.RenameOnArchive.Value;
            if (request.RemoveCreatorOnArchive.HasValue)
                settings["removeCreatorOnArchive"] = request.RemoveCreatorOnArchive.Value;
            if (request.AutoArchiveOnClose.HasValue) settings["autoArchiveOnClose"] = request.AutoArchiveOnClose.Value;
            if (request.ModalJson != null) settings["modalJson"] = request.ModalJson;
            if (request.OpenMessageJson != null) settings["openMessageJson"] = request.OpenMessageJson;

            if (!settings.Any())
                return BadRequest("No settings provided to update");

            var success = await ticketService.UpdateButtonSettingsAsync(guild, buttonId, settings);

            if (success)
                return Ok();
            return BadRequest("Failed to update button settings");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating button {ButtonId} in guild {GuildId}", buttonId, guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    ///     Reorders buttons on a panel
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="panelId">The message ID of the panel</param>
    /// <param name="request">Request containing the new button order</param>
    /// <returns>Success or failure response</returns>
    [HttpPut("panels/{panelId}/buttons/reorder")]
    public async Task<IActionResult> ReorderPanelButtons(ulong guildId, ulong panelId,
        [FromBody] ReorderButtonsRequest request)
    {
        try
        {
            IGuild guild = client.GetGuild(guildId);
            if (guild == null)
                return NotFound("Guild not found");

            var success = await ticketService.ReorderPanelButtonsAsync(guild, panelId, request.ButtonOrder);

            if (success)
                return Ok();
            return BadRequest("Failed to reorder buttons");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error reordering buttons for panel {PanelId} in guild {GuildId}", panelId, guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    ///     Deletes a button from a panel
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="buttonId">The ID of the button to delete</param>
    /// <returns>Success or failure response</returns>
    [HttpDelete("buttons/{buttonId}")]
    public async Task<IActionResult> DeleteButton(ulong guildId, int buttonId)
    {
        try
        {
            var button = await ticketService.GetButtonAsync(buttonId);
            if (button == null || button.Panel.GuildId != guildId)
                return NotFound("Button not found");

            IGuild guild = client.GetGuild(guildId);
            var success = await ticketService.DeleteButtonAsync(guild, buttonId);

            if (success)
                return Ok();
            return BadRequest("Failed to delete button");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting button {ButtonId} in guild {GuildId}", buttonId, guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    #endregion

    #region Select Menu Management

    /// <summary>
    ///     Gets all select menus for a specific panel
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="panelId">The message ID of the panel</param>
    /// <returns>List of select menus with their options</returns>
    [HttpGet("panels/{panelId}/selectmenus")]
    public async Task<IActionResult> GetPanelSelectMenus(ulong guildId, ulong panelId)
    {
        try
        {
            var selectMenus = await ticketService.GetPanelSelectMenusAsync(panelId);
            return Ok(selectMenus);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting select menus for panel {PanelId} in guild {GuildId}", panelId, guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    ///     Adds a select menu to a panel
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="panelId">The message ID of the panel</param>
    /// <param name="request">Select menu configuration request</param>
    /// <returns>The created select menu information</returns>
    [HttpPost("panels/{panelId}/selectmenus")]
    public async Task<IActionResult> AddSelectMenu(ulong guildId, ulong panelId,
        [FromBody] AddSelectMenuRequest request)
    {
        try
        {
            var panel = await ticketService.GetPanelAsync(panelId);
            if (panel == null || panel.GuildId != guildId)
                return NotFound("Panel not found");

            var menu = await ticketService.AddSelectMenuAsync(
                panel,
                request.Placeholder,
                request.FirstOptionLabel,
                request.FirstOptionDescription,
                request.FirstOptionEmoji);

            return Ok(new
            {
                menu.Id, menu.CustomId, menu.Placeholder
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error adding select menu to panel {PanelId} in guild {GuildId}", panelId, guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    ///     Updates a select menu's placeholder
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="menuId">The ID of the select menu</param>
    /// <param name="request">Request containing the new placeholder</param>
    /// <returns>Success or failure response</returns>
    [HttpPut("selectmenus/{menuId}/placeholder")]
    public async Task<IActionResult> UpdateSelectMenuPlaceholder(ulong guildId, string menuId,
        [FromBody] UpdatePlaceholderRequest request)
    {
        try
        {
            var menu = await ticketService.GetSelectMenuAsync(menuId);
            if (menu == null || menu.Panel.GuildId != guildId)
                return NotFound("Select menu not found");

            await ticketService.UpdateSelectMenuAsync(menu, m => m.Placeholder = request.Placeholder);

            return Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating select menu {MenuId} placeholder in guild {GuildId}", menuId, guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    ///     Adds an option to a select menu
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="menuId">The ID of the select menu</param>
    /// <param name="request">Option configuration request</param>
    /// <returns>The created option information</returns>
    [HttpPost("selectmenus/{menuId}/options")]
    public async Task<IActionResult> AddSelectOption(ulong guildId, string menuId,
        [FromBody] AddSelectOptionRequest request)
    {
        try
        {
            var menu = await ticketService.GetSelectMenuAsync(menuId);
            if (menu == null || menu.Panel.GuildId != guildId)
                return NotFound("Select menu not found");

            var option = await ticketService.AddSelectOptionAsync(
                menu,
                request.Label,
                $"option_{Guid.NewGuid():N}",
                request.Description,
                request.Emoji,
                request.OpenMessageJson,
                request.ModalJson,
                request.ChannelFormat,
                request.CategoryId,
                request.ArchiveCategoryId,
                request.SupportRoles,
                request.ViewerRoles,
                request.AutoCloseTime,
                request.RequiredResponseTime,
                request.MaxActiveTickets,
                request.AllowedPriorities,
                request.DefaultPriority);

            return Ok(new
            {
                option.Id,
                option.Label,
                option.Value,
                option.Description,
                option.Emoji
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error adding option to select menu {MenuId} in guild {GuildId}", menuId, guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    ///     Deletes a select menu from a panel
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="menuId">The ID of the select menu to delete</param>
    /// <returns>Success or failure response</returns>
    [HttpDelete("selectmenus/{menuId}")]
    public async Task<IActionResult> DeleteSelectMenu(ulong guildId, int menuId)
    {
        try
        {
            IGuild guild = client.GetGuild(guildId);
            if (guild == null)
                return NotFound("Guild not found");

            var success = await ticketService.DeleteSelectMenuAsync(guild, menuId);

            if (success)
                return Ok();
            return BadRequest("Failed to delete select menu");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting select menu {MenuId} in guild {GuildId}", menuId, guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    ///     Gets detailed information about a specific select menu option
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="optionId">The ID of the option</param>
    /// <returns>Detailed option configuration</returns>
    [HttpGet("selectmenus/options/{optionId}")]
    public async Task<IActionResult> GetSelectOption(ulong guildId, int optionId)
    {
        try
        {
            var option = await ticketService.GetSelectOptionAsync(optionId);
            if (option == null || option.SelectMenu.Panel.GuildId != guildId)
                return NotFound("Select menu option not found");

            IGuild guild = client.GetGuild(guildId);
            var category = option.CategoryId.HasValue
                ? await guild.GetCategoryChannelAsync(option.CategoryId.Value)
                : null;
            var archiveCategory = option.ArchiveCategoryId.HasValue
                ? await guild.GetCategoryChannelAsync(option.ArchiveCategoryId.Value)
                : null;

            return Ok(new
            {
                option.Id,
                option.Label,
                option.Description,
                option.Emoji,
                option.Value,
                option.ChannelNameFormat,
                option.CategoryId,
                CategoryName = category?.Name,
                option.ArchiveCategoryId,
                ArchiveCategoryName = archiveCategory?.Name,
                option.SupportRoles,
                option.ViewerRoles,
                option.AutoCloseTime,
                option.RequiredResponseTime,
                option.MaxActiveTickets,
                option.AllowedPriorities,
                option.DefaultPriority,
                option.SaveTranscript,
                option.DeleteOnClose,
                option.LockOnClose,
                option.RenameOnClose,
                option.RemoveCreatorOnClose,
                option.DeleteDelay,
                option.LockOnArchive,
                option.RenameOnArchive,
                option.RemoveCreatorOnArchive,
                option.AutoArchiveOnClose,
                option.OpenMessageJson,
                option.ModalJson
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting select option {OptionId} in guild {GuildId}", optionId, guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    ///     Updates select menu option settings
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="optionId">The ID of the option to update</param>
    /// <param name="request">Updated option settings</param>
    /// <returns>Success or failure response</returns>
    [HttpPut("selectmenus/options/{optionId}")]
    public async Task<IActionResult> UpdateSelectOption(ulong guildId, int optionId,
        [FromBody] UpdateSelectOptionRequest request)
    {
        try
        {
            IGuild guild = client.GetGuild(guildId);
            if (guild == null)
                return NotFound("Guild not found");

            var settings = new Dictionary<string, object>();

            if (request.Label != null) settings["label"] = request.Label;
            if (request.Description != null) settings["description"] = request.Description;
            if (request.Emoji != null) settings["emoji"] = request.Emoji;
            if (request.CategoryId.HasValue) settings["categoryId"] = request.CategoryId.Value;
            if (request.ArchiveCategoryId.HasValue) settings["archiveCategoryId"] = request.ArchiveCategoryId.Value;
            if (request.SupportRoles != null) settings["supportRoles"] = request.SupportRoles.ToArray();
            if (request.ViewerRoles != null) settings["viewerRoles"] = request.ViewerRoles.ToArray();
            if (request.AutoCloseTime.HasValue) settings["autoCloseTime"] = request.AutoCloseTime.Value;
            if (request.RequiredResponseTime.HasValue)
                settings["requiredResponseTime"] = request.RequiredResponseTime.Value;
            if (request.MaxActiveTickets.HasValue) settings["maxActiveTickets"] = request.MaxActiveTickets.Value;
            if (request.AllowedPriorities != null) settings["allowedPriorities"] = request.AllowedPriorities.ToArray();
            if (request.DefaultPriority != null) settings["defaultPriority"] = request.DefaultPriority;
            if (request.SaveTranscript.HasValue) settings["saveTranscript"] = request.SaveTranscript.Value;
            if (request.DeleteOnClose.HasValue) settings["deleteOnClose"] = request.DeleteOnClose.Value;
            if (request.LockOnClose.HasValue) settings["lockOnClose"] = request.LockOnClose.Value;
            if (request.RenameOnClose.HasValue) settings["renameOnClose"] = request.RenameOnClose.Value;
            if (request.RemoveCreatorOnClose.HasValue)
                settings["removeCreatorOnClose"] = request.RemoveCreatorOnClose.Value;
            if (request.DeleteDelay.HasValue) settings["deleteDelay"] = request.DeleteDelay.Value;
            if (request.LockOnArchive.HasValue) settings["lockOnArchive"] = request.LockOnArchive.Value;
            if (request.RenameOnArchive.HasValue) settings["renameOnArchive"] = request.RenameOnArchive.Value;
            if (request.RemoveCreatorOnArchive.HasValue)
                settings["removeCreatorOnArchive"] = request.RemoveCreatorOnArchive.Value;
            if (request.AutoArchiveOnClose.HasValue) settings["autoArchiveOnClose"] = request.AutoArchiveOnClose.Value;
            if (request.ModalJson != null) settings["modalJson"] = request.ModalJson;
            if (request.OpenMessageJson != null) settings["openMessageJson"] = request.OpenMessageJson;

            if (!settings.Any())
                return BadRequest("No settings provided to update");

            var success = await ticketService.UpdateSelectOptionSettingsAsync(guild, optionId, settings);

            if (success)
                return Ok();
            return BadRequest("Failed to update select option settings");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating select option {OptionId} in guild {GuildId}", optionId, guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    ///     Deletes an option from a select menu
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="optionId">The ID of the option to delete</param>
    /// <returns>Success or failure response</returns>
    [HttpDelete("selectmenus/options/{optionId}")]
    public async Task<IActionResult> DeleteSelectOption(ulong guildId, int optionId)
    {
        try
        {
            IGuild guild = client.GetGuild(guildId);
            if (guild == null)
                return NotFound("Guild not found");

            var success = await ticketService.DeleteSelectOptionAsync(guild, optionId);

            if (success)
                return Ok();
            return BadRequest("Failed to delete select option");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting select option {OptionId} in guild {GuildId}", optionId, guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    #endregion

    #region Ticket Management

    /// <summary>
    ///     Gets a ticket by its ID
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="ticketId">The ID of the ticket</param>
    /// <returns>Detailed ticket information</returns>
    [HttpGet("tickets/{ticketId}")]
    public async Task<IActionResult> GetTicket(ulong guildId, int ticketId)
    {
        try
        {
            var ticket = await ticketService.GetTicketAsync(ticketId);
            if (ticket == null || ticket.GuildId != guildId)
                return NotFound("Ticket not found");

            IGuild guild = client.GetGuild(guildId);
            var channel = await guild.GetTextChannelAsync(ticket.ChannelId);
            var creator = await guild.GetUserAsync(ticket.CreatorId);
            var claimedBy = ticket.ClaimedBy.HasValue ? await guild.GetUserAsync(ticket.ClaimedBy.Value) : null;

            return Ok(new
            {
                ticket.Id,
                ticket.GuildId,
                ticket.ChannelId,
                ChannelName = channel?.Name ?? "Deleted Channel",
                ticket.CreatorId,
                CreatorName = creator?.Username ?? "Unknown User",
                ticket.ClaimedBy,
                ClaimedByName = claimedBy?.Username,
                ticket.ButtonId,
                ticket.SelectOptionId,
                ticket.Priority,
                ticket.Tags,
                ticket.CreatedAt,
                ticket.ClosedAt,
                ticket.LastActivityAt,
                ticket.IsArchived,
                ticket.IsDeleted,
                ticket.TranscriptUrl,
                ticket.CaseId,
                ticket.ModalResponses
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting ticket {TicketId} in guild {GuildId}", ticketId, guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    ///     Gets a ticket by its channel ID
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="channelId">The ID of the ticket channel</param>
    /// <returns>Detailed ticket information</returns>
    [HttpGet("tickets/by-channel/{channelId}")]
    public async Task<IActionResult> GetTicketByChannel(ulong guildId, ulong channelId)
    {
        try
        {
            var ticket = await ticketService.GetTicketAsync(channelId);
            if (ticket == null || ticket.GuildId != guildId)
                return NotFound("Ticket not found");

            IGuild guild = client.GetGuild(guildId);
            var channel = await guild.GetTextChannelAsync(ticket.ChannelId);
            var creator = await guild.GetUserAsync(ticket.CreatorId);
            var claimedBy = ticket.ClaimedBy.HasValue ? await guild.GetUserAsync(ticket.ClaimedBy.Value) : null;

            return Ok(new
            {
                ticket.Id,
                ticket.GuildId,
                ticket.ChannelId,
                ChannelName = channel?.Name ?? "Deleted Channel",
                ticket.CreatorId,
                CreatorName = creator?.Username ?? "Unknown User",
                ticket.ClaimedBy,
                ClaimedByName = claimedBy?.Username,
                ticket.ButtonId,
                ticket.SelectOptionId,
                ticket.Priority,
                ticket.Tags,
                ticket.CreatedAt,
                ticket.ClosedAt,
                ticket.LastActivityAt,
                ticket.IsArchived,
                ticket.IsDeleted,
                ticket.TranscriptUrl,
                ticket.CaseId,
                ticket.ModalResponses
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting ticket by channel {ChannelId} in guild {GuildId}", channelId, guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    ///     Gets all tickets for a guild
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="includeArchived">Whether to include archived tickets</param>
    /// <param name="includeClosed">Whether to include closed tickets</param>
    /// <param name="includeDeleted">Whether to include soft-deleted tickets</param>
    /// <returns>List of tickets matching the criteria</returns>
    [HttpGet("tickets")]
    public async Task<IActionResult> GetGuildTickets(ulong guildId, [FromQuery] bool includeArchived = true,
        [FromQuery] bool includeClosed = true, [FromQuery] bool includeDeleted = false)
    {
        try
        {
            var tickets =
                await ticketService.GetGuildTicketsAsync(guildId, includeArchived, includeClosed, includeDeleted);
            IGuild guild = client.GetGuild(guildId);
            if (guild == null)
                return NotFound("Guild not found");

            var result = await Task.WhenAll(tickets.Select(async ticket =>
            {
                var channel = await guild.GetTextChannelAsync(ticket.ChannelId);
                var creator = await guild.GetUserAsync(ticket.CreatorId);
                var claimedBy = ticket.ClaimedBy.HasValue ? await guild.GetUserAsync(ticket.ClaimedBy.Value) : null;
                var buttonLabel = ticket.Button?.Label;
                var optionLabel = ticket.SelectOption?.Label;

                return new
                {
                    ticket.Id,
                    ticket.GuildId,
                    ticket.ChannelId,
                    ChannelName = channel?.Name ?? "Deleted Channel",
                    ticket.CreatorId,
                    CreatorName = creator?.Username ?? "Unknown User",
                    ticket.ClaimedBy,
                    ClaimedByName = claimedBy?.Username,
                    ticket.ButtonId,
                    ButtonLabel = buttonLabel,
                    ticket.SelectOptionId,
                    OptionLabel = optionLabel,
                    ticket.Priority,
                    ticket.Tags,
                    ticket.CreatedAt,
                    ticket.ClosedAt,
                    ticket.LastActivityAt,
                    ticket.IsArchived,
                    ticket.IsDeleted,
                    ticket.TranscriptUrl,
                    ticket.CaseId
                };
            }));

            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting all tickets for guild {GuildId}", guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    ///     Claims a ticket for a staff member
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="channelId">The ID of the ticket channel</param>
    /// <param name="request">Request containing the staff member ID</param>
    /// <returns>Success or failure response</returns>
    [HttpPost("tickets/by-channel/{channelId}/claim")]
    public async Task<IActionResult> ClaimTicket(ulong guildId, ulong channelId, [FromBody] ClaimTicketRequest request)
    {
        try
        {
            IGuild guild = client.GetGuild(guildId);
            if (guild == null)
                return NotFound("Guild not found");

            var staff = await guild.GetUserAsync(request.StaffId);
            if (staff == null)
                return NotFound("Staff member not found");

            var success = await ticketService.ClaimTicket(guild, channelId, staff);

            if (success)
                return Ok();
            return BadRequest("Failed to claim ticket");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error claiming ticket in channel {ChannelId} in guild {GuildId}", channelId, guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    ///     Unclaims a ticket
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="channelId">The ID of the ticket channel</param>
    /// <param name="request">Request containing the staff member ID</param>
    /// <returns>Success or failure response</returns>
    [HttpPost("tickets/by-channel/{channelId}/unclaim")]
    public async Task<IActionResult> UnclaimTicket(ulong guildId, ulong channelId,
        [FromBody] UnclaimTicketRequest request)
    {
        try
        {
            IGuild guild = client.GetGuild(guildId);
            if (guild == null)
                return NotFound("Guild not found");

            var staff = await guild.GetUserAsync(request.StaffId);
            if (staff == null)
                return NotFound("Staff member not found");

            var success = await ticketService.UnclaimTicket(guild, channelId, staff);

            if (success)
                return Ok();
            return BadRequest("Failed to unclaim ticket");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error unclaiming ticket in channel {ChannelId} in guild {GuildId}", channelId,
                guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    ///     Closes a ticket
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="channelId">The ID of the ticket channel</param>
    /// <param name="forceArchive">Whether to force archive the ticket</param>
    /// <returns>Success or failure response</returns>
    [HttpPost("tickets/by-channel/{channelId}/close")]
    public async Task<IActionResult> CloseTicket(ulong guildId, ulong channelId, [FromQuery] bool forceArchive = false)
    {
        try
        {
            IGuild guild = client.GetGuild(guildId);
            if (guild == null)
                return NotFound("Guild not found");

            var success = await ticketService.CloseTicket(guild, channelId, forceArchive);

            if (success)
                return Ok();
            return BadRequest("Failed to close ticket");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error closing ticket in channel {ChannelId} in guild {GuildId}", channelId, guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    ///     Archives a ticket
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="ticketId">The ID of the ticket to archive</param>
    /// <returns>Success or failure response</returns>
    [HttpPost("tickets/{ticketId}/archive")]
    public async Task<IActionResult> ArchiveTicket(ulong guildId, int ticketId)
    {
        try
        {
            var ticket = await ticketService.GetTicketAsync(ticketId);
            if (ticket == null || ticket.GuildId != guildId)
                return NotFound("Ticket not found");

            await ticketService.ArchiveTicketAsync(ticket);
            return Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error archiving ticket {TicketId} in guild {GuildId}", ticketId, guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    ///     Sets the priority of a ticket
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="channelId">The ID of the ticket channel</param>
    /// <param name="request">Request containing priority and staff information</param>
    /// <returns>Success or failure response</returns>
    [HttpPost("tickets/by-channel/{channelId}/priority")]
    public async Task<IActionResult> SetTicketPriority(ulong guildId, ulong channelId,
        [FromBody] SetPriorityRequest request)
    {
        try
        {
            IGuild guild = client.GetGuild(guildId);
            if (guild == null)
                return NotFound("Guild not found");

            var staff = await guild.GetUserAsync(request.StaffId);
            if (staff == null)
                return NotFound("Staff member not found");

            var success = await ticketService.SetTicketPriority(guild, channelId, request.PriorityId, staff);

            if (success)
                return Ok();
            return BadRequest("Failed to set ticket priority");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error setting priority for ticket in channel {ChannelId} in guild {GuildId}",
                channelId,
                guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    ///     Adds tags to a ticket
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="channelId">The ID of the ticket channel</param>
    /// <param name="request">Request containing tag IDs and staff information</param>
    /// <returns>Success or failure response</returns>
    [HttpPost("tickets/by-channel/{channelId}/tags")]
    public async Task<IActionResult> AddTicketTags(ulong guildId, ulong channelId, [FromBody] AddTagsRequest request)
    {
        try
        {
            IGuild guild = client.GetGuild(guildId);
            if (guild == null)
                return NotFound("Guild not found");

            var staff = await guild.GetUserAsync(request.StaffId);
            if (staff == null)
                return NotFound("Staff member not found");

            var success = await ticketService.AddTicketTags(guild, channelId, request.TagIds, staff);

            if (success)
                return Ok();
            return BadRequest("Failed to add tags to ticket");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error adding tags to ticket in channel {ChannelId} in guild {GuildId}", channelId,
                guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    ///     Removes tags from a ticket
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="channelId">The ID of the ticket channel</param>
    /// <param name="request">Request containing tag IDs and staff information</param>
    /// <returns>Success or failure response</returns>
    [HttpDelete("tickets/by-channel/{channelId}/tags")]
    public async Task<IActionResult> RemoveTicketTags(ulong guildId, ulong channelId,
        [FromBody] RemoveTagsRequest request)
    {
        try
        {
            IGuild guild = client.GetGuild(guildId);
            if (guild == null)
                return NotFound("Guild not found");

            var staff = await guild.GetUserAsync(request.StaffId);
            if (staff == null)
                return NotFound("Staff member not found");

            var success = await ticketService.RemoveTicketTags(guild, channelId, request.TagIds, staff);

            if (success)
                return Ok();
            return BadRequest("Failed to remove tags from ticket");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error removing tags from ticket in channel {ChannelId} in guild {GuildId}", channelId,
                guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    ///     Adds a note to a ticket
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="channelId">The ID of the ticket channel</param>
    /// <param name="request">Request containing note content and author information</param>
    /// <returns>Success or failure response</returns>
    [HttpPost("tickets/by-channel/{channelId}/notes")]
    public async Task<IActionResult> AddTicketNote(ulong guildId, ulong channelId, [FromBody] AddNoteRequest request)
    {
        try
        {
            IGuild guild = client.GetGuild(guildId);
            if (guild == null)
                return NotFound("Guild not found");

            var author = await guild.GetUserAsync(request.AuthorId);
            if (author == null)
                return NotFound("Author not found");

            var success = await ticketService.AddNote(channelId, author, request.Content);

            if (success)
                return Ok();
            return BadRequest("Failed to add note to ticket");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error adding note to ticket in channel {ChannelId} in guild {GuildId}", channelId,
                guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    ///     Edits an existing ticket note
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="noteId">The ID of the note to edit</param>
    /// <param name="request">Request containing new content and author information</param>
    /// <returns>Success or failure response</returns>
    [HttpPut("tickets/notes/{noteId}")]
    public async Task<IActionResult> EditTicketNote(ulong guildId, int noteId, [FromBody] EditNoteRequest request)
    {
        try
        {
            IGuild guild = client.GetGuild(guildId);
            if (guild == null)
                return NotFound("Guild not found");

            var author = await guild.GetUserAsync(request.AuthorId);
            if (author == null)
                return NotFound("Author not found");

            var success = await ticketService.EditNote(noteId, author, request.Content);

            if (success)
                return Ok();
            return BadRequest("Failed to edit note");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error editing note {NoteId} in guild {GuildId}", noteId, guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    ///     Deletes a ticket note
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="noteId">The ID of the note to delete</param>
    /// <param name="request">Request containing author information</param>
    /// <returns>Success or failure response</returns>
    [HttpDelete("tickets/notes/{noteId}")]
    public async Task<IActionResult> DeleteTicketNote(ulong guildId, int noteId, [FromBody] DeleteNoteRequest request)
    {
        try
        {
            IGuild guild = client.GetGuild(guildId);
            if (guild == null)
                return NotFound("Guild not found");

            var author = await guild.GetUserAsync(request.AuthorId);
            if (author == null)
                return NotFound("Author not found");

            var success = await ticketService.DeleteNote(noteId, author);

            if (success)
                return Ok();
            return BadRequest("Failed to delete note");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting note {NoteId} in guild {GuildId}", noteId, guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    #endregion

    #region Case Management

    /// <summary>
    ///     Gets all cases for a guild
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="includeDeleted">Whether to include deleted cases</param>
    /// <returns>List of cases with their linked tickets</returns>
    [HttpGet("cases")]
    public async Task<IActionResult> GetGuildCases(ulong guildId, [FromQuery] bool includeDeleted = false)
    {
        try
        {
            var cases = await ticketService.GetGuildCasesAsync(guildId, includeDeleted);
            IGuild guild = client.GetGuild(guildId);

            var result = await Task.WhenAll(cases.Select(async ticketCase =>
            {
                var creator = await guild.GetUserAsync(ticketCase.CreatedBy);
                return new
                {
                    ticketCase.Id,
                    ticketCase.GuildId,
                    ticketCase.Title,
                    ticketCase.Description,
                    ticketCase.CreatedBy,
                    CreatedByName = creator?.Username ?? "Unknown User",
                    ticketCase.CreatedAt,
                    ticketCase.ClosedAt,
                    LinkedTickets = ticketCase.Tickets?.Count() ?? 0
                };
            }));

            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting cases for guild {GuildId}", guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    ///     Gets a specific case by ID
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="caseId">The ID of the case</param>
    /// <returns>Detailed case information with linked tickets</returns>
    [HttpGet("cases/{caseId}")]
    public async Task<IActionResult> GetCase(ulong guildId, int caseId)
    {
        try
        {
            var ticketCase = await ticketService.GetCaseAsync(caseId);
            if (ticketCase == null || ticketCase.GuildId != guildId)
                return NotFound("Case not found");

            IGuild guild = client.GetGuild(guildId);
            var creator = await guild.GetUserAsync(ticketCase.CreatedBy);

            var linkedTickets = await Task.WhenAll((ticketCase.Tickets ?? new List<Ticket>()).Select(async ticket =>
            {
                var channel = await guild.GetTextChannelAsync(ticket.ChannelId);
                var ticketCreator = await guild.GetUserAsync(ticket.CreatorId);
                return new
                {
                    ticket.Id,
                    ticket.ChannelId,
                    ChannelName = channel?.Name ?? "Deleted Channel",
                    ticket.CreatorId,
                    CreatorName = ticketCreator?.Username ?? "Unknown User",
                    ticket.CreatedAt,
                    ticket.ClosedAt,
                    ticket.IsArchived
                };
            }));

            return Ok(new
            {
                ticketCase.Id,
                ticketCase.GuildId,
                ticketCase.Title,
                ticketCase.Description,
                ticketCase.CreatedBy,
                CreatedByName = creator?.Username ?? "Unknown User",
                ticketCase.CreatedAt,
                ticketCase.ClosedAt,
                LinkedTickets = linkedTickets,
                Notes = (IEnumerable)ticketCase.CaseNotes?.Select(note => new
                {
                    note.Id, note.Content, note.AuthorId, note.CreatedAt
                })
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting case {CaseId} in guild {GuildId}", caseId, guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    ///     Creates a new case
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="request">Case creation request</param>
    /// <returns>The created case information</returns>
    [HttpPost("cases")]
    public async Task<IActionResult> CreateCase(ulong guildId, [FromBody] CreateCaseRequest request)
    {
        try
        {
            IGuild guild = client.GetGuild(guildId);
            if (guild == null)
                return NotFound("Guild not found");

            var creator = await guild.GetUserAsync(request.CreatorId);
            if (creator == null)
                return NotFound("Creator not found");

            var ticketCase = await ticketService.CreateCase(guild, creator, request.Title, request.Description);

            return Ok(new
            {
                ticketCase.Id,
                ticketCase.Title,
                ticketCase.Description,
                ticketCase.CreatedBy,
                ticketCase.CreatedAt
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating case in guild {GuildId}", guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    ///     Updates a case
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="caseId">The ID of the case to update</param>
    /// <param name="request">Case update request</param>
    /// <returns>Success or failure response</returns>
    [HttpPut("cases/{caseId}")]
    public async Task<IActionResult> UpdateCase(ulong guildId, int caseId, [FromBody] UpdateCaseRequest request)
    {
        try
        {
            var ticketCase = await ticketService.GetCaseAsync(caseId);
            if (ticketCase == null || ticketCase.GuildId != guildId)
                return NotFound("Case not found");

            await ticketService.UpdateCaseAsync(caseId, request.Title, request.Description);
            return Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating case {CaseId} in guild {GuildId}", caseId, guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    ///     Closes a case
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="caseId">The ID of the case to close</param>
    /// <param name="archiveTickets">Whether to archive linked tickets</param>
    /// <returns>Success or failure response</returns>
    [HttpPost("cases/{caseId}/close")]
    public async Task<IActionResult> CloseCase(ulong guildId, int caseId, [FromQuery] bool archiveTickets = false)
    {
        try
        {
            var ticketCase = await ticketService.GetCaseAsync(caseId);
            if (ticketCase == null || ticketCase.GuildId != guildId)
                return NotFound("Case not found");

            await ticketService.CloseCaseAsync(ticketCase, archiveTickets);
            return Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error closing case {CaseId} in guild {GuildId}", caseId, guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    ///     Reopens a closed case
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="caseId">The ID of the case to reopen</param>
    /// <returns>Success or failure response</returns>
    [HttpPost("cases/{caseId}/reopen")]
    public async Task<IActionResult> ReopenCase(ulong guildId, int caseId)
    {
        try
        {
            var ticketCase = await ticketService.GetCaseAsync(caseId);
            if (ticketCase == null || ticketCase.GuildId != guildId)
                return NotFound("Case not found");

            await ticketService.ReopenCaseAsync(ticketCase);
            return Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error reopening case {CaseId} in guild {GuildId}", caseId, guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    ///     Links tickets to a case
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="caseId">The ID of the case</param>
    /// <param name="request">Request containing ticket IDs to link</param>
    /// <returns>Success or failure response</returns>
    [HttpPost("cases/{caseId}/link-tickets")]
    public async Task<IActionResult> LinkTicketsToCase(ulong guildId, int caseId, [FromBody] LinkTicketsRequest request)
    {
        try
        {
            var tickets = await ticketService.GetTicketsAsync(request.TicketIds);
            if (tickets.Any(t => t.GuildId != guildId))
                return BadRequest("Some tickets don't belong to this guild");

            await ticketService.LinkTicketsToCase(caseId, tickets);
            return Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error linking tickets to case {CaseId} in guild {GuildId}", caseId, guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    ///     Unlinks tickets from their case
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="request">Request containing ticket IDs to unlink</param>
    /// <returns>Success or failure response</returns>
    [HttpPost("unlink-tickets")]
    public async Task<IActionResult> UnlinkTicketsFromCase(ulong guildId, [FromBody] UnlinkTicketsRequest request)
    {
        try
        {
            var tickets = await ticketService.GetTicketsAsync(request.TicketIds);
            if (tickets.Any(t => t.GuildId != guildId))
                return BadRequest("Some tickets don't belong to this guild");

            await ticketService.UnlinkTicketsFromCase(tickets);
            return Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error unlinking tickets in guild {GuildId}", guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    ///     Adds a note to a case
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="caseId">The ID of the case</param>
    /// <param name="request">Note creation request</param>
    /// <returns>The created note information</returns>
    [HttpPost("cases/{caseId}/notes")]
    public async Task<IActionResult> AddCaseNote(ulong guildId, int caseId, [FromBody] AddCaseNoteRequest request)
    {
        try
        {
            var ticketCase = await ticketService.GetCaseAsync(caseId);
            if (ticketCase == null || ticketCase.GuildId != guildId)
                return NotFound("Case not found");

            var note = await ticketService.AddCaseNoteAsync(caseId, request.AuthorId, request.Content);
            if (note == null)
                return BadRequest("Failed to add note");

            return Ok(new
            {
                note.Id, note.Content, note.AuthorId, note.CreatedAt
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error adding note to case {CaseId} in guild {GuildId}", caseId, guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    #endregion

    #region Statistics

    /// <summary>
    ///     Gets overview data optimized for dashboard without expensive Discord API calls
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="activityDays">Number of days to include in activity summary (default 30)</param>
    /// <returns>Overview data with statistics and counts</returns>
    [HttpGet("overview")]
    public async Task<IActionResult> GetOverviewData(ulong guildId, [FromQuery] int activityDays = 30)
    {
        try
        {
            var stats = await ticketService.GetGuildStatistics(guildId);
            var panels = await ticketService.GetPanelsAsync(guildId);
            var priorities = await ticketService.GetGuildPriorities(guildId);
            var tags = await ticketService.GetGuildTags(guildId);
            var cases = await ticketService.GetGuildCasesAsync(guildId);
            var activitySummary = await ticketService.GetTicketActivitySummary(guildId, activityDays);
            var staffMetrics = await ticketService.GetStaffResponseMetrics(guildId);
            IGuild guild = client.GetGuild(guildId);

            // Only fetch Discord data for staff members (much smaller set than all tickets/panels)
            var staffResponseStats = await Task.WhenAll(staffMetrics.Select(async kvp =>
            {
                var staff = await guild.GetUserAsync(kvp.Key);
                return new
                {
                    StaffId = kvp.Key,
                    StaffName = staff?.Username ?? "Unknown User",
                    AverageResponseTimeMinutes = kvp.Value
                };
            }));

            return Ok(new
            {
                Statistics = stats,
                TicketActivity = activitySummary,
                StaffResponseStats = staffResponseStats,
                Panels = panels.Select(p => new
                {
                    p.Id, p.MessageId, p.ChannelId
                }),
                Priorities = priorities,
                Tags = tags,
                Cases = cases.Select(c => new
                {
                    c.Id, c.Title, c.CreatedAt, c.ClosedAt
                })
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting overview data for guild {GuildId}", guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    ///     Gets guild-wide ticket statistics
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <returns>Comprehensive guild statistics</returns>
    [HttpGet("statistics")]
    public async Task<IActionResult> GetGuildStatistics(ulong guildId)
    {
        try
        {
            var stats = await ticketService.GetGuildStatistics(guildId);
            return Ok(stats);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting guild statistics for guild {GuildId}", guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    ///     Gets ticket statistics for a specific user
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="userId">The ID of the user</param>
    /// <returns>User-specific ticket statistics</returns>
    [HttpGet("statistics/users/{userId}")]
    public async Task<IActionResult> GetUserStatistics(ulong guildId, ulong userId)
    {
        try
        {
            var stats = await ticketService.GetUserStatistics(guildId, userId);
            return Ok(stats);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting user statistics for user {UserId} in guild {GuildId}", userId, guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    ///     Gets ticket activity summary over time
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="days">Number of days to include in the summary</param>
    /// <returns>Daily ticket activity counts</returns>
    [HttpGet("statistics/activity")]
    public async Task<IActionResult> GetTicketActivitySummary(ulong guildId, [FromQuery] int days = 30)
    {
        try
        {
            var summary = await ticketService.GetTicketActivitySummary(guildId, days);
            return Ok(summary);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting activity summary for guild {GuildId}", guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    ///     Gets staff response metrics
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <returns>Average response times by staff member</returns>
    [HttpGet("statistics/staff-response")]
    public async Task<IActionResult> GetStaffResponseMetrics(ulong guildId)
    {
        try
        {
            var metrics = await ticketService.GetStaffResponseMetrics(guildId);
            IGuild guild = client.GetGuild(guildId);

            var result = await Task.WhenAll(metrics.Select(async kvp =>
            {
                var staff = await guild.GetUserAsync(kvp.Key);
                return new
                {
                    StaffId = kvp.Key,
                    StaffName = staff?.Username ?? "Unknown User",
                    AverageResponseTimeMinutes = kvp.Value
                };
            }));

            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting staff response metrics for guild {GuildId}", guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    #endregion

    #region Priority and Tag Management

    /// <summary>
    ///     Gets all priorities for a guild
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <returns>List of available priorities</returns>
    [HttpGet("priorities")]
    public async Task<IActionResult> GetGuildPriorities(ulong guildId)
    {
        try
        {
            var priorities = await ticketService.GetGuildPriorities(guildId);
            return Ok(priorities);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting priorities for guild {GuildId}", guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    ///     Creates a new priority level
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="request">Priority creation request</param>
    /// <returns>Success or failure response</returns>
    [HttpPost("priorities")]
    public async Task<IActionResult> CreatePriority(ulong guildId, [FromBody] CreatePriorityRequest request)
    {
        try
        {
            var success = await ticketService.CreatePriority(
                guildId,
                request.Id,
                request.Name,
                request.Emoji,
                request.Level,
                request.PingStaff,
                request.ResponseTime,
                request.Color);

            if (success)
                return Ok();
            return BadRequest("Failed to create priority or priority already exists");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating priority in guild {GuildId}", guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    ///     Deletes a priority level
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="priorityId">The ID of the priority to delete</param>
    /// <returns>Success or failure response</returns>
    [HttpDelete("priorities/{priorityId}")]
    public async Task<IActionResult> DeletePriority(ulong guildId, string priorityId)
    {
        try
        {
            var success = await ticketService.DeletePriority(guildId, priorityId);

            if (success)
                return Ok();
            return NotFound("Priority not found");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting priority {PriorityId} in guild {GuildId}", priorityId, guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    ///     Gets all tags for a guild
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <returns>List of available tags</returns>
    [HttpGet("tags")]
    public async Task<IActionResult> GetGuildTags(ulong guildId)
    {
        try
        {
            var tags = await ticketService.GetGuildTags(guildId);
            return Ok(tags);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting tags for guild {GuildId}", guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    ///     Creates a new tag
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="request">Tag creation request</param>
    /// <returns>Success or failure response</returns>
    [HttpPost("tags")]
    public async Task<IActionResult> CreateTag(ulong guildId, [FromBody] CreateTagRequest request)
    {
        try
        {
            var success = await ticketService.CreateTag(
                guildId,
                request.Id,
                request.Name,
                request.Description,
                request.Color);

            if (success)
                return Ok();
            return BadRequest("Failed to create tag or tag already exists");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating tag in guild {GuildId}", guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    ///     Deletes a tag
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="tagId">The ID of the tag to delete</param>
    /// <returns>Success or failure response</returns>
    [HttpDelete("tags/{tagId}")]
    public async Task<IActionResult> DeleteTag(ulong guildId, string tagId)
    {
        try
        {
            var success = await ticketService.DeleteTag(guildId, tagId);

            if (success)
                return Ok();
            return NotFound("Tag not found");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting tag {TagId} in guild {GuildId}", tagId, guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    #endregion

    #region Blacklist Management

    /// <summary>
    ///     Gets all blacklisted users for a guild
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <returns>Dictionary of blacklisted users and their restricted ticket types</returns>
    [HttpGet("blacklist")]
    public async Task<IActionResult> GetBlacklistedUsers(ulong guildId)
    {
        try
        {
            var blacklist = await ticketService.GetBlacklistedUsers(guildId);
            IGuild guild = client.GetGuild(guildId);

            var result = await Task.WhenAll(blacklist.Select(async kvp =>
            {
                var user = await guild.GetUserAsync(kvp.Key);
                return new
                {
                    UserId = kvp.Key, Username = user?.Username ?? "Unknown User", RestrictedTypes = kvp.Value
                };
            }));

            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting blacklisted users for guild {GuildId}", guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    ///     Adds a user to the blacklist
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="userId">The ID of the user to blacklist</param>
    /// <param name="request">Blacklist request containing optional reason</param>
    /// <returns>Success or failure response</returns>
    [HttpPost("blacklist/{userId}")]
    public async Task<IActionResult> BlacklistUser(ulong guildId, ulong userId, [FromBody] BlacklistUserRequest request)
    {
        try
        {
            IGuild guild = client.GetGuild(guildId);
            if (guild == null)
                return NotFound("Guild not found");

            var success = await ticketService.BlacklistUser(guild, userId, request?.Reason);

            if (success)
                return Ok();
            return BadRequest("User is already blacklisted");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error blacklisting user {UserId} in guild {GuildId}", userId, guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    ///     Removes a user from the blacklist
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="userId">The ID of the user to unblacklist</param>
    /// <returns>Success or failure response</returns>
    [HttpDelete("blacklist/{userId}")]
    public async Task<IActionResult> UnblacklistUser(ulong guildId, ulong userId)
    {
        try
        {
            IGuild guild = client.GetGuild(guildId);
            if (guild == null)
                return NotFound("Guild not found");

            var success = await ticketService.UnblacklistUser(guild, userId);

            if (success)
                return Ok();
            return NotFound("User is not blacklisted");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error unblacklisting user {UserId} in guild {GuildId}", userId, guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    #endregion

    #region Batch Operations

    /// <summary>
    ///     Closes all inactive tickets
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="hours">Number of hours of inactivity required</param>
    /// <returns>Summary of closure results</returns>
    [HttpPost("batch/close-inactive")]
    public async Task<IActionResult> BatchCloseInactiveTickets(ulong guildId, [FromQuery] int hours)
    {
        try
        {
            IGuild guild = client.GetGuild(guildId);
            if (guild == null)
                return NotFound("Guild not found");

            var (closed, failed) = await ticketService.BatchCloseInactiveTickets(guild, TimeSpan.FromHours(hours));

            return Ok(new
            {
                Closed = closed, Failed = failed, InactiveHours = hours
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error closing inactive tickets in guild {GuildId}", guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    ///     Moves all tickets between categories
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="request">Request containing source and target category IDs</param>
    /// <returns>Summary of move results</returns>
    [HttpPost("batch/move-tickets")]
    public async Task<IActionResult> BatchMoveTickets(ulong guildId, [FromBody] BatchMoveTicketsRequest request)
    {
        try
        {
            IGuild guild = client.GetGuild(guildId);
            if (guild == null)
                return NotFound("Guild not found");

            var (moved, failed) =
                await ticketService.BatchMoveTickets(guild, request.SourceCategoryId, request.TargetCategoryId);

            return Ok(new
            {
                Moved = moved, Failed = failed
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error moving tickets in guild {GuildId}", guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    ///     Adds a role to all active tickets
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="request">Request containing role and permission information</param>
    /// <returns>Summary of update results</returns>
    [HttpPost("batch/add-role")]
    public async Task<IActionResult> BatchAddRole(ulong guildId, [FromBody] BatchAddRoleRequest request)
    {
        try
        {
            IGuild guild = client.GetGuild(guildId);
            if (guild == null)
                return NotFound("Guild not found");

            var role = guild.GetRole(request.RoleId);
            if (role == null)
                return NotFound("Role not found");

            var (updated, failed) = await ticketService.BatchAddRole(guild, role, request.ViewOnly);

            return Ok(new
            {
                Updated = updated, Failed = failed
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error adding role to tickets in guild {GuildId}", guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    ///     Transfers all tickets from one staff member to another
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="request">Request containing source and target staff IDs</param>
    /// <returns>Summary of transfer results</returns>
    [HttpPost("batch/transfer-tickets")]
    public async Task<IActionResult> BatchTransferTickets(ulong guildId, [FromBody] BatchTransferTicketsRequest request)
    {
        try
        {
            IGuild guild = client.GetGuild(guildId);
            if (guild == null)
                return NotFound("Guild not found");

            var (transferred, failed) =
                await ticketService.BatchTransferTickets(guild, request.FromStaffId, request.ToStaffId);

            return Ok(new
            {
                Transferred = transferred, Failed = failed
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error transferring tickets in guild {GuildId}", guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    #endregion

    #region Settings

    /// <summary>
    ///     Gets the ticket settings for the guild
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <returns>Guild ticket settings</returns>
    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings(ulong guildId)
    {
        try
        {
            var settings = await ticketService.GetGuildTicketSettingsAsync(guildId);
            return Ok(new
            {
                transcriptChannelId = settings?.TranscriptChannelId,
                logChannelId = settings?.LogChannelId,
                defaultMaxTickets = settings?.DefaultMaxTickets,
                blacklistedUsers = settings?.BlacklistedUsers
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting ticket settings for guild {GuildId}", guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    ///     Sets the transcript channel for the guild
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="request">Request containing the channel ID</param>
    /// <returns>Success response</returns>
    [HttpPut("settings/transcript-channel")]
    public async Task<IActionResult> SetTranscriptChannel(ulong guildId, [FromBody] SetChannelRequest request)
    {
        try
        {
            await ticketService.SetTranscriptChannelAsync(guildId, request.ChannelId);
            return Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error setting transcript channel for guild {GuildId}", guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    ///     Sets the log channel for the guild
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="request">Request containing the channel ID</param>
    /// <returns>Success response</returns>
    [HttpPut("settings/log-channel")]
    public async Task<IActionResult> SetLogChannel(ulong guildId, [FromBody] SetChannelRequest request)
    {
        try
        {
            await ticketService.SetLogChannelAsync(guildId, request.ChannelId);
            return Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error setting log channel for guild {GuildId}", guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    #endregion
}