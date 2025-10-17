using System.Text;
using System.Text.Json;
using DataModel;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Controllers.Common.Chat;
using Mewdeko.Database.L2DB;
using Mewdeko.Modules.Tickets.Common;
using Mewdeko.Modules.Utility.Services;
using Mewdeko.Services.Impl;
using Mewdeko.Services.Strings;
using Embed = Mewdeko.Common.Embed;
using SelectMenuOption = DataModel.SelectMenuOption;

namespace Mewdeko.Modules.Tickets.Services;

/// <summary>
///     Service for managing ticket panels, tickets, and cases.
/// </summary>
public class TicketService : INService
{
    private const string ClaimButtonId = "ticket_claim";
    private const string CloseButtonId = "ticket_close";
    private readonly ChatLogService chatLogService;
    private readonly DiscordShardedClient client;
    private readonly BotCredentials credentials;
    private readonly IDataConnectionFactory dbFactory;
    private readonly EventHandler eventHandler;
    private readonly ILogger<TicketService> logger;
    private readonly GeneratedBotStrings strings;

    /// <summary>
    ///     Initializes a new instance of the <see cref="TicketService" /> class.
    /// </summary>
    /// <param name="dbFactory">The database connection factory.</param>
    /// <param name="client">The Discord client instance.</param>
    /// <param name="eventHandler">The event handler service.</param>
    /// <param name="strings">The localized strings service.</param>
    /// <param name="logger">The logger instance for structured logging.</param>
    /// <param name="chatLogService">The chat log service for transcript management.</param>
    /// <param name="credentials">The bot credentials for dashboard URL.</param>
    public TicketService(
        IDataConnectionFactory dbFactory,
        DiscordShardedClient client,
        EventHandler eventHandler,
        GeneratedBotStrings strings,
        ILogger<TicketService> logger,
        ChatLogService chatLogService,
        BotCredentials credentials)
    {
        this.dbFactory = dbFactory;
        this.client = client;
        this.eventHandler = eventHandler;
        this.strings = strings;
        this.logger = logger;
        this.chatLogService = chatLogService;
        this.credentials = credentials;

        eventHandler.Subscribe("MessageDeleted", "TicketService", HandleMessageDeleted);
    }

    /// <summary>
    ///     Unloads the service and unsubscribes from events.
    /// </summary>
    public Task Unload()
    {
        eventHandler.Unsubscribe("MessageDeleted", "TicketService", HandleMessageDeleted);
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Creates a new panel in a channel with either simple parameters or custom JSON
    /// </summary>
    /// <param name="channel">The channel to create the panel in</param>
    /// <param name="embedJson">Optional custom embed JSON</param>
    /// <param name="title">Default title if not using custom JSON</param>
    /// <param name="description">Default description if not using custom JSON</param>
    /// <param name="color">Default color if not using custom JSON</param>
    /// <returns>The created ticket panel</returns>
    public async Task<TicketPanel> CreatePanelAsync(
        ITextChannel channel,
        string? embedJson = null,
        string title = "Support Tickets",
        string description = "Click a button below to create a ticket",
        Color? color = null)
    {
        string finalJson;

        if (string.IsNullOrWhiteSpace(embedJson))
        {
            // Create default embed JSON
            var embed = new NewEmbed
            {
                Embed = new Embed
                {
                    Title = title, Description = description, Color = Mewdeko.OkColor
                }
            };
            finalJson = JsonSerializer.Serialize(embed);
        }
        else
        {
            // Validate custom JSON
            try
            {
                // Test parse to validate
                if (!SmartEmbed.TryParse(embedJson, channel.Guild.Id, out _, out _, out _))
                    throw new ArgumentException("Invalid embed JSON format");
                finalJson = embedJson;
            }
            catch (JsonException)
            {
                throw new ArgumentException("Invalid JSON format");
            }
        }

        // Create and send panel message
        SmartEmbed.TryParse(finalJson, channel.Guild.Id, out var embeds, out var plainText, out _);
        var message = await channel.SendMessageAsync(plainText, embeds: embeds);


        // Create panel
        var panel = new TicketPanel
        {
            GuildId = channel.Guild.Id,
            ChannelId = channel.Id,
            MessageId = message.Id,
            EmbedJson = finalJson,
            PanelButtons = [],
            PanelSelectMenus = []
        };

        await using var ctx = await dbFactory.CreateConnectionAsync();
        var id = await ctx.InsertWithInt32IdentityAsync(panel);
        panel.Id = id;


        return panel;
    }

    /// <summary>
    ///     Previews how an embed JSON would look
    /// </summary>
    /// <param name="channel">The channel parameter.</param>
    /// <param name="embedJson">The embedjson string.</param>
    public async Task PreviewPanelAsync(ITextChannel channel, string embedJson)
    {
        try
        {
            var replacer = new ReplacementBuilder()
                .WithServer(client, channel.Guild as SocketGuild)
                .Build();

            var content = replacer.Replace(embedJson);

            if (SmartEmbed.TryParse(content, channel.Guild.Id, out var embedData, out var plainText,
                    out var components))
            {
                await channel.SendMessageAsync(plainText, embeds: embedData, components: components?.Build());
            }
            else
            {
                await channel.SendMessageAsync(content);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error previewing panel embed");
            throw;
        }
    }

    /// <summary>
    ///     Gets all panels in a channel
    /// </summary>
    public async Task<List<TicketPanel>> GetPanelsInChannelAsync(ulong guildId, ulong channelId)
    {
        await using var ctx = await dbFactory.CreateConnectionAsync();
        return await ctx.TicketPanels
            .LoadWithAsTable(p => p.PanelButtons)
            .LoadWithAsTable(p => p.PanelSelectMenus)
            .Where(p => p.GuildId == guildId && p.ChannelId == channelId)
            .ToListAsync();
    }

    /// <summary>
    ///     Gets all panels in a guild
    /// </summary>
    public async Task<List<TicketPanel>> GetPanelsAsync(ulong guildId)
    {
        await using var ctx = await dbFactory.CreateConnectionAsync();
        return await ctx.TicketPanels
            .LoadWithAsTable(p => p.PanelButtons)
            .LoadWithAsTable(p => p.PanelSelectMenus)
            .Where(p => p.GuildId == guildId)
            .OrderBy(p => p.ChannelId)
            .ThenBy(p => p.MessageId)
            .ToListAsync();
    }

    /// <summary>
    ///     Gets a specific panel by its index (1-based) in the guild
    /// </summary>
    public async Task<TicketPanel> GetPanelByIndexAsync(ulong guildId, int index)
    {
        if (index < 1)
            throw new ArgumentException("Index must be greater than 0");

        var panels = await GetPanelsAsync(guildId);
        return panels.ElementAtOrDefault(index - 1);
    }

    /// <summary>
    ///     Gets a specific panel by its index (1-based) in a channel
    /// </summary>
    public async Task<TicketPanel> GetPanelByChannelIndexAsync(ulong guildId, ulong channelId, int index)
    {
        if (index < 1)
            throw new ArgumentException("Index must be greater than 0");

        var panels = await GetPanelsInChannelAsync(guildId, channelId);
        return panels.ElementAtOrDefault(index - 1);
    }

    /// <summary>
    ///     Adds a button to an existing ticket panel.
    /// </summary>
    /// <param name="panel">The panel to add the button to.</param>
    /// <param name="label">The button label.</param>
    /// <param name="emoji">Optional emoji for the button.</param>
    /// <param name="style">The button style.</param>
    /// <param name="openMessageJson">Optional JSON for ticket opening message.</param>
    /// <param name="modalJson">Optional JSON for ticket creation modal.</param>
    /// <param name="channelFormat">Format for ticket channel names.</param>
    /// <param name="categoryId">Optional category for ticket channels.</param>
    /// <param name="archiveCategoryId">Optional category for archived tickets.</param>
    /// <param name="supportRoles">List of support role IDs.</param>
    /// <param name="viewerRoles">List of viewer role IDs.</param>
    /// <param name="autoCloseTime">Optional auto-close duration.</param>
    /// <param name="requiredResponseTime">Optional required response time.</param>
    /// <param name="maxActiveTickets">Maximum active tickets per user.</param>
    /// <param name="allowedPriorities">List of allowed priority IDs.</param>
    /// <param name="defaultPriority">Optional default priority.</param>
    public async Task<PanelButton> AddButtonAsync(
        TicketPanel panel,
        string label,
        string emoji = null,
        ButtonStyle style = ButtonStyle.Primary,
        string openMessageJson = null,
        string modalJson = null,
        string channelFormat = "ticket-{username}-{id}",
        ulong? categoryId = null,
        ulong? archiveCategoryId = null,
        List<ulong> supportRoles = null,
        List<ulong> viewerRoles = null,
        TimeSpan? autoCloseTime = null,
        TimeSpan? requiredResponseTime = null,
        int maxActiveTickets = 1,
        List<string> allowedPriorities = null,
        string defaultPriority = null)
    {
        try
        {
            await using var ctx = await dbFactory.CreateConnectionAsync();

            // Check component limits before adding (1 button = 1 component, 1 select menu = 5 components)
            var currentButtonCount = await ctx.PanelButtons.Where(b => b.PanelId == panel.Id).CountAsync();
            var currentMenuCount = await ctx.PanelSelectMenus.Where(m => m.PanelId == panel.Id).CountAsync();
            var currentTotal = currentButtonCount + currentMenuCount * 5;

            if (currentTotal + 1 > 25)
            {
                logger.LogError(
                    "Cannot add button to panel {PanelId}: would exceed component limit ({CurrentTotal} + 1 > 25)",
                    panel.Id, currentTotal);
                return null;
            }

            var button = new PanelButton
            {
                PanelId = panel.Id,
                Label = label,
                Emoji = emoji,
                CustomId = $"ticket_btn_{Guid.NewGuid():N}",
                Style = (int)style,
                OpenMessageJson = openMessageJson,
                ModalJson = modalJson,
                ChannelNameFormat = channelFormat,
                CategoryId = categoryId,
                ArchiveCategoryId = archiveCategoryId,
                SupportRoles = supportRoles?.ToArray() ?? [],
                ViewerRoles = viewerRoles?.ToArray() ?? [],
                AutoCloseTime = autoCloseTime,
                RequiredResponseTime = requiredResponseTime,
                MaxActiveTickets = maxActiveTickets,
                AllowedPriorities = allowedPriorities?.ToArray() ?? [],
                DefaultPriority = defaultPriority,
                SaveTranscript = false
            };

            await ctx.InsertAsync(button);


            var existingButtons = panel.PanelButtons?.ToList() ?? new List<PanelButton>();
            existingButtons.Add(button);
            panel.PanelButtons = existingButtons;

            await UpdatePanelComponentsAsync(panel);

            return button;
        }
        catch (Exception e)
        {
            logger.LogError(e.ToString());
        }

        return null;
    }

    private ComponentBuilder GetDefaultTicketComponents()
    {
        var claimButton = new ButtonBuilder()
            .WithCustomId(ClaimButtonId)
            .WithLabel("Claim")
            .WithStyle(ButtonStyle.Primary)
            .WithEmote(new Emoji("🤝"));

        var closeButton = new ButtonBuilder()
            .WithCustomId(CloseButtonId)
            .WithLabel("Close")
            .WithStyle(ButtonStyle.Danger)
            .WithEmote(new Emoji("🔒"));

        return new ComponentBuilder()
            .WithButton(claimButton)
            .WithButton(closeButton);
    }

    /// <summary>
    ///     Retrieves a case by its ID.
    /// </summary>
    /// <param name="caseId">The ID of the case to retrieve.</param>
    /// <returns>A task containing the case if found, null otherwise.</returns>
    public async Task<TicketCase> GetCaseAsync(int caseId)
    {
        await using var ctx = await dbFactory.CreateConnectionAsync();
        return await ctx.TicketCases
            .LoadWithAsTable(c => c.Tickets)
            .LoadWithAsTable(c => c.CaseNotes)
            .FirstOrDefaultAsync(c => c.Id == caseId);
    }

    /// <summary>
    ///     Closes a case and optionally archives linked tickets
    /// </summary>
    /// <param name="ticketCase">The case to close</param>
    /// <param name="archiveTickets">Whether to archive linked tickets. Defaults to false</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public async Task CloseCaseAsync(TicketCase ticketCase, bool archiveTickets = false)
    {
        await using var ctx = await dbFactory.CreateConnectionAsync();

        await using var transaction = await ctx.BeginTransactionAsync();
        try
        {
            // Close the case
            await ctx.TicketCases
                .Where(tc => tc.Id == ticketCase.Id)
                .Set(tc => tc.ClosedAt, DateTime.UtcNow)
                .UpdateAsync();

            if (archiveTickets)
            {
                // Get linked tickets with their button/option data for archive categories
                var linkedTicketsWithArchiveInfo = await (
                    from ticket in ctx.Tickets
                    where ticket.CaseId == ticketCase.Id &&
                          !ticket.IsArchived &&
                          !ticket.ClosedAt.HasValue
                    select new
                    {
                        Ticket = ticket,
                        ButtonArchiveCategoryId = ticket.ButtonId.HasValue
                            ? ctx.PanelButtons.Where(b => b.Id == ticket.ButtonId).Select(b => b.ArchiveCategoryId)
                                .FirstOrDefault()
                            : null,
                        OptionArchiveCategoryId = ticket.SelectOptionId.HasValue
                            ? ctx.SelectMenuOptions.Where(o => o.Id == ticket.SelectOptionId)
                                .Select(o => o.ArchiveCategoryId).FirstOrDefault()
                            : null
                    }).ToListAsync();

                if (linkedTicketsWithArchiveInfo.Any())
                {
                    // Handle Discord channel moves for each ticket
                    foreach (var item in linkedTicketsWithArchiveInfo)
                    {
                        var archiveCategoryId = item.ButtonArchiveCategoryId ?? item.OptionArchiveCategoryId;

                        if (archiveCategoryId.HasValue)
                        {
                            var guild = await client.Rest.GetGuildAsync(item.Ticket.GuildId);
                            if (guild != null)
                            {
                                var channel = await guild.GetTextChannelAsync(item.Ticket.ChannelId);
                                if (channel != null)
                                {
                                    await channel.ModifyAsync(props =>
                                        props.CategoryId = archiveCategoryId.Value);
                                }
                            }
                        }
                    }

                    // Bulk update all linked tickets
                    var ticketIds = linkedTicketsWithArchiveInfo.Select(item => item.Ticket.Id).ToArray();
                    await ctx.Tickets
                        .Where(t => ticketIds.Contains(t.Id))
                        .Set(t => t.ClosedAt, DateTime.UtcNow)
                        .Set(t => t.IsArchived, true)
                        .Set(t => t.LastActivityAt, DateTime.UtcNow)
                        .UpdateAsync();
                }
            }

            await transaction.CommitAsync();

            // Update the in-memory object to reflect the changes
            ticketCase.ClosedAt = DateTime.UtcNow;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    /// <summary>
    ///     Retrieves all cases for a guild, ordered by creation date.
    /// </summary>
    /// <param name="guildId">The ID of the guild to get cases for.</param>
    /// <param name="includeDeleted">Whether to include soft-deleted cases. Defaults to false.</param>
    /// <returns>A task containing the list of cases.</returns>
    public async Task<List<TicketCase>> GetGuildCasesAsync(ulong guildId, bool includeDeleted = false)
    {
        await using var ctx = await dbFactory.CreateConnectionAsync();
        var query = ctx.TicketCases
            .LoadWithAsTable(c => c.Tickets)
            .LoadWithAsTable(c => c.CaseNotes)
            .Where(c => c.GuildId == guildId);

        if (!includeDeleted)
            query = query.Where(c => !c.ClosedAt.HasValue);

        return await query
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();
    }

    /// <summary>
    ///     Adds a note to a case.
    /// </summary>
    /// <param name="caseId">The ID of the case to add the note to.</param>
    /// <param name="authorId">The ID of the user adding the note.</param>
    /// <param name="content">The content of the note.</param>
    /// <returns>A task containing the created note.</returns>
    public async Task<CaseNote?> AddCaseNoteAsync(int caseId, ulong authorId, string content)
    {
        await using var ctx = await dbFactory.CreateConnectionAsync();
        var ticketCase = await ctx.TicketCases.FindAsync(caseId);
        if (ticketCase == null)
            return null;

        var note = new CaseNote
        {
            CaseId = caseId, AuthorId = authorId, Content = content, CreatedAt = DateTime.UtcNow
        };

        await ctx.InsertWithInt32IdentityAsync(note);

        return note;
    }


    /// <summary>
    ///     Deletes a ticket panel and all its components.
    /// </summary>
    /// <param name="panelId">The message ID of the panel to delete.</param>
    /// <param name="guild">The guild containing the panel.</param>
    /// <param name="force">Whether to force delete even if there are tickets referencing the panel's buttons.</param>
    /// <returns>A tuple containing success status and any error information.</returns>
    public async Task<(bool success, string error, List<int> activeTickets, List<int> deletedTickets)> DeletePanelAsync(
        ulong panelId, IGuild guild, bool force = false)
    {
        try
        {
            await using var ctx = await dbFactory.CreateConnectionAsync();

            var panel = await ctx.TicketPanels
                .LoadWithAsTable(p => p.PanelButtons)
                .LoadWithAsTable(p => p.PanelSelectMenus)
                .FirstOrDefaultAsync(p => p.MessageId == panelId && p.GuildId == guild.Id);

            if (panel == null)
                return (false, "Panel not found", null, null);

            // Get all component IDs that could be referenced
            var buttonIds = panel.PanelButtons?.Select(b => b.Id).ToList() ?? [];
            var selectOptionIds = new List<int>();

            if (panel.PanelSelectMenus?.Any() == true)
            {
                var menuIds = panel.PanelSelectMenus.Select(m => m.Id).ToList();
                var options = await ctx.SelectMenuOptions
                    .Where(o => menuIds.Contains(o.SelectMenuId))
                    .Select(o => o.Id)
                    .ToListAsync();
                selectOptionIds.AddRange(options);
            }

            // Check for ALL tickets (active and soft-deleted) referencing these components
            // We need to clear ALL references due to foreign key constraints
            var allReferencedTickets = new List<int>();
            var activeReferencedTickets = new List<int>();
            var deletedReferencedTickets = new List<int>();

            if (buttonIds.Any())
            {
                var ticketsWithButtons = await ctx.Tickets
                    .Where(t => t.ButtonId.HasValue && buttonIds.Contains(t.ButtonId.Value))
                    .Select(t => new
                    {
                        t.Id, t.IsDeleted
                    })
                    .ToListAsync();

                allReferencedTickets.AddRange(ticketsWithButtons.Select(t => t.Id));
                activeReferencedTickets.AddRange(ticketsWithButtons.Where(t => !t.IsDeleted).Select(t => t.Id));
                deletedReferencedTickets.AddRange(ticketsWithButtons.Where(t => t.IsDeleted).Select(t => t.Id));
            }

            if (selectOptionIds.Any())
            {
                var ticketsWithOptions = await ctx.Tickets
                    .Where(t => t.SelectOptionId.HasValue && selectOptionIds.Contains(t.SelectOptionId.Value))
                    .Select(t => new
                    {
                        t.Id, t.IsDeleted
                    })
                    .ToListAsync();

                allReferencedTickets.AddRange(ticketsWithOptions.Select(t => t.Id));
                activeReferencedTickets.AddRange(ticketsWithOptions.Where(t => !t.IsDeleted).Select(t => t.Id));
                deletedReferencedTickets.AddRange(ticketsWithOptions.Where(t => t.IsDeleted).Select(t => t.Id));
            }

            // Remove duplicates
            allReferencedTickets = allReferencedTickets.Distinct().ToList();
            activeReferencedTickets = activeReferencedTickets.Distinct().ToList();
            deletedReferencedTickets = deletedReferencedTickets.Distinct().ToList();

            // If there are active tickets and force is not enabled, return error
            if (activeReferencedTickets.Any() && !force)
            {
                return (false,
                    "Cannot delete panel because there are active tickets referencing its components",
                    activeReferencedTickets,
                    deletedReferencedTickets);
            }

            // If force is enabled or only soft-deleted tickets are referenced, clear ALL references
            // This is necessary because foreign key constraints don't respect soft delete flags
            if (allReferencedTickets.Any() && (force || !activeReferencedTickets.Any()))
            {
                // Clear button references from ALL tickets (active and soft-deleted)
                if (buttonIds.Any())
                {
                    await ctx.Tickets
                        .Where(t => t.ButtonId.HasValue && buttonIds.Contains(t.ButtonId.Value))
                        .Set(t => t.ButtonId, (int?)null)
                        .UpdateAsync();
                }

                // Clear select menu option references from ALL tickets (active and soft-deleted)
                if (selectOptionIds.Any())
                {
                    await ctx.Tickets
                        .Where(t => t.SelectOptionId.HasValue && selectOptionIds.Contains(t.SelectOptionId.Value))
                        .Set(t => t.SelectOptionId, (int?)null)
                        .UpdateAsync();
                }
            }

            // Now we can safely delete the panel components in the correct order

            // Delete select menu options first
            if (panel.PanelSelectMenus?.Any() == true)
            {
                var menuIds = panel.PanelSelectMenus.Select(m => m.Id).ToList();
                await ctx.SelectMenuOptions
                    .Where(o => menuIds.Contains(o.SelectMenuId))
                    .DeleteAsync();
            }

            // Delete select menus
            if (panel.PanelSelectMenus?.Any() == true)
            {
                foreach (var menu in panel.PanelSelectMenus)
                {
                    await ctx.PanelSelectMenus
                        .Where(m => m.Id == menu.Id)
                        .DeleteAsync();
                }
            }

            // Delete buttons
            if (panel.PanelButtons?.Any() == true)
            {
                foreach (var button in panel.PanelButtons)
                {
                    await ctx.PanelButtons
                        .Where(b => b.Id == button.Id)
                        .DeleteAsync();
                }
            }

            // Delete the panel itself
            await ctx.TicketPanels
                .Where(p => p.Id == panel.Id)
                .DeleteAsync();

            // Try to delete the Discord message
            try
            {
                var channel = await guild.GetTextChannelAsync(panel.ChannelId);
                if (channel != null)
                {
                    var message = await channel.GetMessageAsync(panel.MessageId);
                    if (message != null)
                    {
                        await message.DeleteAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to delete panel message {MessageId} in guild {GuildId}", panel.MessageId,
                    guild.Id);
                // Don't fail the entire operation if we can't delete the Discord message
            }

            logger.LogInformation(
                "Deleted panel {PanelId} in guild {GuildId} (force: {Force}, cleared {TotalTickets} ticket references: {ActiveTickets} active, {DeletedTickets} soft-deleted)",
                panel.Id, guild.Id, force, allReferencedTickets.Count, activeReferencedTickets.Count,
                deletedReferencedTickets.Count);

            return (true, null, activeReferencedTickets, deletedReferencedTickets);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete panel {PanelId} in guild {GuildId}", panelId, guild.Id);
            return (false, $"Failed to delete panel: {ex.Message}", null, null);
        }
    }

    /// <summary>
    ///     Removes a staff member's claim from a ticket.
    /// </summary>
    /// <param name="ticket">The ticket to unclaim.</param>
    /// <param name="moderator">The moderator performing the unclaim action.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the ticket is not claimed.</exception>
    public async Task UnclaimTicketAsync(Ticket ticket, IGuildUser moderator)
    {
        if (!ticket.ClaimedBy.HasValue)
            throw new InvalidOperationException("Ticket is not claimed");

        await using var ctx = await dbFactory.CreateConnectionAsync();

        var previousClaimer = ticket.ClaimedBy.Value;
        ticket.ClaimedBy = null;
        ticket.LastActivityAt = DateTime.UtcNow;
        await ctx.UpdateAsync(ticket);

        if (await moderator.Guild.GetChannelAsync(ticket.ChannelId) is ITextChannel channel)
        {
            var embed = new EmbedBuilder()
                .WithTitle(strings.TicketUnclaimed(moderator.Guild.Id))
                .WithDescription(strings.TicketUnclaimedBy(moderator.Guild.Id, moderator.Mention))
                .WithColor(Color.Orange)
                .Build();

            await channel.SendMessageAsync(embed: embed);

            // Notify previous claimer if enabled
            var settings = await ctx.GuildTicketSettings.FirstOrDefaultAsync(s => s.GuildId == moderator.Guild.Id);
            if (settings?.EnableDmNotifications == true)
            {
                try
                {
                    var previousUser = await moderator.Guild.GetUserAsync(previousClaimer);
                    if (previousUser != null)
                    {
                        var dmEmbed = new EmbedBuilder()
                            .WithTitle(strings.TicketUnclaimed(moderator.Guild.Id))
                            .WithDescription(strings.YourClaimOnTicketRemoved(ticket.GuildId, ticket.Id, moderator))
                            .WithColor(Color.Orange)
                            .Build();

                        await previousUser.SendMessageAsync(embed: dmEmbed);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to send DM notification for ticket unclaim");
                }
            }
        }
    }

    /// <summary>
    ///     Gets or creates the default ticket opening message.
    /// </summary>
    /// <param name="ticket">The ticket being opened.</param>
    /// <param name="customMessage">Optional custom message to override the default.</param>
    /// <returns>The configured message content in SmartEmbed format.</returns>
    private string GetTicketOpenMessage(Ticket ticket, string customMessage = null)
    {
        if (!string.IsNullOrWhiteSpace(customMessage))
            return customMessage;

        // Build default embed JSON for SmartEmbed
        return JsonSerializer.Serialize(new
        {
            embeds = new[]
            {
                new
                {
                    title = "Support Ticket",
                    description = $"Welcome to your ticket <@{ticket.CreatorId}>!\n\n" +
                                  $"• Ticket ID: {ticket.Id}\n" +
                                  $"• Created: <t:{ticket.CreatedAt}:F>\n\n" +
                                  "Please describe your issue and wait for a staff member to assist you.",
                    color = "ok", // Uses Mewdeko.OkColor
                    footer = new
                    {
                        text = "Ticket Support"
                    }
                }
            }
        });
    }

    private async Task SendDefaultOpenMessage(ITextChannel channel, Ticket ticket)
    {
        var defaultMessage = GetTicketOpenMessage(ticket);
        SmartEmbed.TryParse(defaultMessage, channel.GuildId, out var embeds, out var plainText, out _);
        await channel.SendMessageAsync(plainText, embeds: embeds, components: GetDefaultTicketComponents().Build());
    }

    /// <summary>
    ///     Unlinks a collection of tickets from their associated cases.
    /// </summary>
    /// <param name="tickets">The collection of tickets to unlink.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task UnlinkTicketsFromCase(IEnumerable<Ticket> tickets)
    {
        await using var ctx = await dbFactory.CreateConnectionAsync();
        var ticketIds = tickets.Select(t => t.Id).ToList();

        // Use bulk update query instead of updating enumerable
        await ctx.Tickets
            .Where(t => ticketIds.Contains(t.Id))
            .Set(t => t.CaseId, (int?)null)
            .UpdateAsync();

        // Update in-memory objects
        foreach (var ticket in tickets)
        {
            ticket.CaseId = null;
            ticket.Case = null;
        }
    }

    /// <summary>
    ///     Reopens a previously closed case.
    /// </summary>
    /// <param name="ticketCase">The case to reopen.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task ReopenCaseAsync(TicketCase ticketCase)
    {
        await using var ctx = await dbFactory.CreateConnectionAsync();
        ticketCase.ClosedAt = null;
        await ctx.UpdateAsync(ticketCase);
    }

    /// <summary>
    ///     Updates the details of an existing case.
    /// </summary>
    /// <param name="caseId">The ID of the case to update.</param>
    /// <param name="title">The new title for the case. If null, the title remains unchanged.</param>
    /// <param name="description">The new description for the case. If null, the description remains unchanged.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task UpdateCaseAsync(int caseId, string title, string description)
    {
        await using var ctx = await dbFactory.CreateConnectionAsync();
        var ticketCase = await ctx.TicketCases.FindAsync(caseId);
        if (ticketCase != null)
        {
            if (!string.IsNullOrEmpty(title))
                ticketCase.Title = title;
            if (!string.IsNullOrEmpty(description))
                ticketCase.Description = description;
        }

        await ctx.UpdateAsync(ticketCase);
    }

    /// <summary>
    ///     Creates a new ticket.
    /// </summary>
    /// <param name="guild">The guild where the ticket will be created.</param>
    /// <param name="creator">The user creating the ticket.</param>
    /// <param name="button">Optional button that triggered the ticket creation.</param>
    /// <param name="option">Optional select menu option that triggered the ticket creation.</param>
    /// <param name="modalResponses">Optional responses from a modal form.</param>
    /// <returns>The created ticket.</returns>
    /// <exception cref="InvalidOperationException">Thrown when ticket creation fails due to limits or permissions.</exception>
    public async Task<Ticket?> CreateTicketAsync(
        IGuild guild,
        IUser creator,
        PanelButton? button = null,
        SelectMenuOption? option = null,
        Dictionary<string, string>? modalResponses = null)
    {
        await using var ctx = await dbFactory.CreateConnectionAsync();

        // Check if user is blacklisted
        var settings = await ctx.GuildTicketSettings.FirstOrDefaultAsync(s => s.GuildId == guild.Id);
        if (settings?.BlacklistedUsers?.Contains(creator.Id) == true)
        {
            throw new InvalidOperationException("You are blacklisted from creating tickets.");
        }

        var id = button?.Id ?? option.Id;

        // Validate ticket limits
        var maxTickets = button?.MaxActiveTickets ?? option?.MaxActiveTickets ?? settings?.DefaultMaxTickets ?? 1;
        var activeTickets = await GetActiveTicketsAsync(guild.Id, creator.Id, id);

        if (activeTickets.Count >= maxTickets)
        {
            throw new InvalidOperationException(
                $"You can only have {maxTickets} active {(maxTickets == 1 ? "ticket" : "tickets")} of this type.");
        }

        // Create ticket channel
        var categoryId = button?.CategoryId ?? option?.CategoryId;
        var category = categoryId.HasValue ? await guild.GetCategoryChannelAsync(categoryId.Value) : null;

        // Get total ticket count for this user (not just active ones) to use as ID
        var totalUserTickets = await ctx.Tickets
            .Where(t => t.GuildId == guild.Id && t.CreatorId == creator.Id)
            .CountAsync();

        var channelName = (button?.ChannelNameFormat ?? option?.ChannelNameFormat ?? "ticket-{username}-{id}")
            .Replace("{username}", creator.Username.ToLower())
            .Replace("{id}", (totalUserTickets + 1).ToString());

        ITextChannel channel;
        try
        {
            channel = await guild.CreateTextChannelAsync(channelName, props =>
            {
                if (category != null)
                    props.CategoryId = category.Id;
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create ticket channel");
            throw new InvalidOperationException("Failed to create ticket channel. Please check bot permissions.");
        }

        // Set permissions
        await SetTicketPermissionsAsync(channel, creator, button, option);

        // Create ticket entity
        var ticket = new Ticket
        {
            GuildId = guild.Id,
            ChannelId = channel.Id,
            CreatorId = creator.Id,
            ButtonId = button?.Id,
            SelectOptionId = option?.Id,
            ModalResponses = modalResponses != null ? JsonSerializer.Serialize(modalResponses) : null,
            Priority = button?.DefaultPriority ?? option?.DefaultPriority,
            CreatedAt = DateTime.UtcNow,
            LastActivityAt = DateTime.UtcNow
        };

        var newId = await ctx.InsertWithInt32IdentityAsync(ticket);


        // Send messages in order
        try
        {
            // 1. Opening message (custom or default)
            var openMessageJson = button?.OpenMessageJson ?? option?.OpenMessageJson;
            if (!string.IsNullOrEmpty(openMessageJson))
            {
                var replacer = new ReplacementBuilder()
                    .WithOverride("%ticket.id%", () => newId.ToString())
                    .WithOverride("%ticket.channel%", () => channel.Mention)
                    .WithOverride("%ticket.user%", () => creator.ToString())
                    .WithOverride("%ticket.user.mention%", () => creator.Mention)
                    .WithOverride("%ticket.user.avatar%", () => creator.GetAvatarUrl())
                    .WithOverride("%ticket.user.id%", () => creator.Id.ToString())
                    .WithOverride("%ticket.created%", () => ticket.CreatedAt.ToString("g"));

                // Add modal responses if present
                if (modalResponses != null)
                {
                    foreach (var (key, value) in modalResponses)
                    {
                        replacer.WithOverride($"%modal.{key}%", () => value);
                    }
                }

                var actre = replacer.Build();

                var success = SmartEmbed.TryParse(
                    actre.Replace(openMessageJson),
                    guild.Id,
                    out var embeds,
                    out var plainText,
                    out var components
                );

                if (success)
                {
                    // Add existing components if any
                    var finalComponents = new ComponentBuilder();
                    if (components != null)
                    {
                        foreach (var i in components.ActionRows)
                        {
                            finalComponents.AddRow(i);
                        }
                    }

                    finalComponents.WithRows(GetDefaultTicketComponents().ActionRows);

                    await channel.SendMessageAsync(plainText, embeds: embeds, components: finalComponents.Build());
                }
                else
                {
                    await channel.SendMessageAsync(
                        actre.Replace(openMessageJson),
                        components: GetDefaultTicketComponents().Build()
                    );
                }
            }
            else
            {
                await SendDefaultOpenMessage(channel, ticket);
            }

            // Send notifications
            await SendTicketNotificationsAsync(ticket, creator, guild, settings);

            // Log ticket creation
            if (settings?.LogChannelId.HasValue == true)
            {
                var logChannel = await guild.GetTextChannelAsync(settings.LogChannelId.Value);
                if (logChannel != null)
                {
                    var logEmbed = new EmbedBuilder()
                        .WithTitle(strings.NewTicketCreated(guild.Id))
                        .WithDescription(strings.TicketCreatedBy(guild.Id, newId, creator.Mention))
                        .AddField("Channel", channel.Mention, true)
                        .AddField("Type", button != null ? $"Button: {button.Label}" : $"Option: {option.Label}", true)
                        .WithColor(Color.Green)
                        .WithCurrentTimestamp()
                        .Build();

                    await logChannel.SendMessageAsync(embed: logEmbed);
                }
            }

            return ticket;
        }
        catch (Exception ex)
        {
            // Cleanup on failure
            logger.LogError(ex, "Error during ticket creation messages/notifications");
            try
            {
                await channel.DeleteAsync();
                await ctx.DeleteAsync(ticket);
            }
            catch (Exception cleanupEx)
            {
                logger.LogError(cleanupEx, "Error during ticket cleanup");
            }

            throw new InvalidOperationException("Failed to complete ticket creation.");
        }
    }

    /// <summary>
    ///     Sends notifications about a new ticket to relevant staff members.
    /// </summary>
    private async Task SendTicketNotificationsAsync(Ticket ticket, IUser creator, IGuild guild,
        GuildTicketSetting settings)
    {
        try
        {
            var channel = await guild.GetTextChannelAsync(ticket.ChannelId);
            if (channel == null) return;

            var supportRoles = ticket.Button?.SupportRoles ?? ticket.SelectOption?.SupportRoles ?? [];
            var notificationRoles = settings?.NotificationRoles ?? [];
            var allRoles = supportRoles.Concat(notificationRoles).Distinct();

            if (settings?.EnableStaffPings == true)
            {
                var mentions = string.Join(" ", allRoles.Select(r => $"<@&{r}>"));
                if (!string.IsNullOrEmpty(mentions))
                {
                    await channel.SendMessageAsync(strings.NewTicketRequiresAttention(guild.Id, mentions));
                }
            }

            if (settings?.EnableDmNotifications == true)
            {
                foreach (var roleId in allRoles)
                {
                    var role = guild.GetRole(roleId);
                    if (role == null) continue;

                    foreach (var member in await role.GetMembersAsync())
                    {
                        try
                        {
                            if (member.IsBot) continue;

                            var dmEmbed = new EmbedBuilder()
                                .WithTitle(strings.NewTicketNotification(guild.Id))
                                .WithDescription(strings.TicketCreatedNotification(guild.Id, guild.Name))
                                .AddField("Creator", creator.ToString(), true)
                                .AddField("Channel", $"#{channel.Name}", true)
                                .WithColor(Color.Blue)
                                .Build();

                            await member.SendMessageAsync(embed: dmEmbed);
                        }
                        catch (Exception ex)
                        {
                            logger.LogWarning(ex, "Failed to send DM notification to {UserId}", member.Id);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error sending ticket notifications");
        }
    }


    /// <summary>
    ///     Adds a note to a ticket
    /// </summary>
    /// <param name="ticket">The ticket to add the note to</param>
    /// <param name="author">The user creating the note</param>
    /// <param name="content">The content of the note</param>
    /// <returns>The created ticket note</returns>
    public async Task<TicketNote> AddNoteAsync(Ticket ticket, IGuildUser author, string content)
    {
        await using var ctx = await dbFactory.CreateConnectionAsync();

        var note = new TicketNote
        {
            TicketId = ticket.Id, AuthorId = author.Id, Content = content, CreatedAt = DateTime.UtcNow
        };

        // Insert the note and get the generated ID
        note.Id = await ctx.InsertWithInt32IdentityAsync(note);

        // Update the ticket's last activity timestamp
        await ctx.Tickets
            .Where(t => t.Id == ticket.Id)
            .Set(t => t.LastActivityAt, DateTime.UtcNow)
            .UpdateAsync();

        return note;
    }

    /// <summary>
    ///     Creates a new case and optionally links tickets to it
    /// </summary>
    /// <param name="guild">The guild where the case is created</param>
    /// <param name="title">The title of the case</param>
    /// <param name="description">The description of the case</param>
    /// <param name="creator">The user creating the case</param>
    /// <param name="ticketsToLink">Optional tickets to link to this case</param>
    /// <returns>The created ticket case</returns>
    public async Task<TicketCase> CreateCaseAsync(
        IGuild guild,
        string title,
        string description,
        IGuildUser creator,
        IEnumerable<Ticket> ticketsToLink = null)
    {
        await using var ctx = await dbFactory.CreateConnectionAsync();

        await using var transaction = await ctx.BeginTransactionAsync();
        try
        {
            var ticketCase = new TicketCase
            {
                GuildId = guild.Id,
                Title = title,
                Description = description,
                CreatedBy = creator.Id,
                CreatedAt = DateTime.UtcNow
            };

            // Insert the case and get the generated ID
            ticketCase.Id = await ctx.InsertWithInt32IdentityAsync(ticketCase);

            // Link tickets to the case if provided
            if (ticketsToLink != null)
            {
                var ticketIds = ticketsToLink.Select(t => t.Id).ToArray();
                if (ticketIds.Length > 0)
                {
                    await ctx.Tickets
                        .Where(t => ticketIds.Contains(t.Id))
                        .Set(t => t.CaseId, ticketCase.Id)
                        .UpdateAsync();
                }
            }

            await transaction.CommitAsync();
            return ticketCase;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    /// <summary>
    ///     Archives a ticket using existing schema
    /// </summary>
    /// <param name="ticket">The ticket to archive</param>
    public async Task ArchiveTicketAsync(Ticket ticket, bool skipTranscript = false)
    {
        await using var ctx = await dbFactory.CreateConnectionAsync();

        IGuild guild = client.GetGuild(ticket.GuildId);

        if (await guild.GetChannelAsync(ticket.ChannelId) is ITextChannel channel)
        {
            try
            {
                // Get archive category from existing configuration
                var archiveCategoryId = ticket.Button?.ArchiveCategoryId ?? ticket.SelectOption?.ArchiveCategoryId;
                if (archiveCategoryId.HasValue)
                {
                    var category = await guild.GetCategoryChannelAsync(archiveCategoryId.Value);
                    if (category != null)
                    {
                        await channel.ModifyAsync(c => c.CategoryId = category.Id);
                    }
                }

                // Generate transcript if enabled and not skipped (skip when auto-archiving after close)
                if (!skipTranscript && (ticket.Button?.SaveTranscript ?? ticket.SelectOption?.SaveTranscript ?? true))
                {
                    await GenerateAndSaveTranscriptAsync(guild, channel, ticket);
                }

                // Handle archiving behaviors using existing fields
                await HandleArchiveCleanupAsync(guild, channel, ticket);

                // Update the ticket with archive status using existing IsArchived field
                await ctx.Tickets
                    .Where(t => t.Id == ticket.Id)
                    .Set(t => t.IsArchived, true)
                    .Set(t => t.LastActivityAt, DateTime.UtcNow)
                    .UpdateAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during ticket archiving process for ticket {TicketId}", ticket.Id);

                // Fallback - just mark as archived
                await ctx.Tickets
                    .Where(t => t.Id == ticket.Id)
                    .Set(t => t.IsArchived, true)
                    .Set(t => t.LastActivityAt, DateTime.UtcNow)
                    .UpdateAsync();
            }
        }
        else
        {
            // Channel not found, just mark as archived
            await ctx.Tickets
                .Where(t => t.Id == ticket.Id)
                .Set(t => t.IsArchived, true)
                .Set(t => t.LastActivityAt, DateTime.UtcNow)
                .UpdateAsync();
        }

        // Update the in-memory object to reflect the changes
        ticket.IsArchived = true;
        ticket.LastActivityAt = DateTime.UtcNow;
    }

    /// <summary>
    ///     Handles additional cleanup tasks when archiving a ticket using existing schema
    /// </summary>
    /// <param name="guild">The guild containing the ticket</param>
    /// <param name="channel">The ticket channel</param>
    /// <param name="ticket">The ticket being archived</param>
    private async Task HandleArchiveCleanupAsync(IGuild guild, ITextChannel channel, Ticket ticket)
    {
        try
        {
            // Rename channel with "archived-" prefix if not already done
            if (!channel.Name.StartsWith("archived-"))
            {
                try
                {
                    var newName = channel.Name.StartsWith("closed-")
                        ? channel.Name.Replace("closed-", "archived-")
                        : $"archived-{channel.Name}";
                    await channel.ModifyAsync(c => c.Name = newName);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to rename archived ticket channel {ChannelId}", channel.Id);
                }
            }

            // Keep channel locked (if it was closed first, it should already be locked)
            // If not locked yet, lock it now
            try
            {
                await channel.AddPermissionOverwriteAsync(guild.EveryoneRole,
                    new OverwritePermissions(
                        viewChannel: PermValue.Allow,
                        sendMessages: PermValue.Deny,
                        addReactions: PermValue.Deny,
                        useSlashCommands: PermValue.Deny
                    ));
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to lock archived ticket channel {ChannelId}", channel.Id);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during archive cleanup for ticket {TicketId}", ticket.Id);
        }
    }


    /// <summary>
    ///     Update all components related to a panel.
    /// </summary>
    /// <param name="panel">The panel who's message components to update</param>
    public async Task UpdatePanelComponentsAsync(TicketPanel panel)
    {
        try
        {
            IGuild guild = client.GetGuild(panel.GuildId);
            var channel = await guild.GetChannelAsync(panel.ChannelId) as ITextChannel;
            var message = await channel?.GetMessageAsync(panel.MessageId);

            if (message is not IUserMessage userMessage)
                return;

            // RELOAD the panel data to ensure we have all buttons and menus
            await using var ctx = await dbFactory.CreateConnectionAsync();
            var fullPanel = await ctx.TicketPanels
                .LoadWithAsTable(p => p.PanelButtons)
                .LoadWithAsTable(p => p.PanelSelectMenus)
                .FirstOrDefaultAsync(p => p.Id == panel.Id);

            if (fullPanel == null)
                return;

            // Load select menu options
            var menuIds = fullPanel.PanelSelectMenus?.Select(m => m.Id).ToList() ?? new List<int>();
            if (menuIds.Any())
            {
                var options = await ctx.SelectMenuOptions
                    .Where(o => menuIds.Contains(o.SelectMenuId))
                    .ToListAsync();

                foreach (var menu in fullPanel.PanelSelectMenus)
                {
                    menu.SelectMenuOptions = options.Where(o => o.SelectMenuId == menu.Id).ToList();
                }
            }

            // Validate Discord component limits
            // Max 25 components per message (buttons = 1, select menus = 5 each)
            var buttonCount = fullPanel.PanelButtons?.Count() ?? 0;
            var selectMenuCount = fullPanel.PanelSelectMenus?.Count() ?? 0;
            var totalComponents = buttonCount + selectMenuCount * 5;

            if (totalComponents > 25)
            {
                logger.LogError(
                    "Panel {PanelId} exceeds Discord component limit: {ButtonCount} buttons + {SelectMenuCount} select menus = {TotalComponents} components (max 25)",
                    panel.Id, buttonCount, selectMenuCount, totalComponents);
                return;
            }

            // Validate select menu option limits (max 25 per menu)
            if (fullPanel.PanelSelectMenus?.Any() == true)
            {
                foreach (var menu in fullPanel.PanelSelectMenus)
                {
                    var optionCount = menu.SelectMenuOptions?.Count() ?? 0;
                    if (optionCount > 25)
                    {
                        logger.LogError(
                            "Select menu {MenuId} on panel {PanelId} has {OptionCount} options (max 25)",
                            menu.Id, panel.Id, optionCount);
                        return;
                    }
                }
            }

            var components = new ComponentBuilder();

            // Add buttons
            if (fullPanel.PanelButtons?.Any() == true)
            {
                var buttonRow = new ActionRowBuilder();
                foreach (var button in fullPanel.PanelButtons)
                {
                    var btnBuilder = new ButtonBuilder()
                        .WithLabel(button.Label)
                        .WithCustomId(button.CustomId)
                        .WithStyle((ButtonStyle)button.Style);

                    if (!string.IsNullOrEmpty(button.Emoji))
                    {
                        try
                        {
                            btnBuilder.WithEmote(Emote.Parse(button.Emoji));
                        }
                        catch
                        {
                            // If emoji parsing fails, try as unicode emoji
                            btnBuilder.WithEmote(new Emoji(button.Emoji));
                        }
                    }

                    buttonRow.WithButton(btnBuilder);
                }

                components.AddRow(buttonRow);
            }

            // Add select menus
            if (fullPanel.PanelSelectMenus?.Any() == true)
            {
                foreach (var menu in fullPanel.PanelSelectMenus)
                {
                    var selectBuilder = new SelectMenuBuilder()
                        .WithCustomId(menu.CustomId)
                        .WithPlaceholder(menu.Placeholder)
                        .WithMaxValues(1);

                    foreach (var option in menu.SelectMenuOptions)
                    {
                        logger.LogInformation(
                            "Processing option: ID={Id}, Label='{Label}', Value='{Value}', SelectMenuId={SelectMenuId}",
                            option.Id, option.Label ?? "NULL", option.Value ?? "NULL", option.SelectMenuId);

                        if (string.IsNullOrWhiteSpace(option.Label))
                        {
                            logger.LogWarning("Skipping option {Id} - Label is null/empty", option.Id);
                            continue;
                        }

                        var optBuilder = new SelectMenuOptionBuilder()
                            .WithLabel(option.Label)
                            .WithValue(option.Value)
                            .WithDescription(option.Description);

                        if (!string.IsNullOrEmpty(option.Emoji))
                        {
                            try
                            {
                                optBuilder.WithEmote(Emote.Parse(option.Emoji));
                            }
                            catch
                            {
                                // If emoji parsing fails, try as unicode emoji
                                optBuilder.WithEmote(new Emoji(option.Emoji));
                            }
                        }

                        selectBuilder.AddOption(optBuilder);
                    }

                    components.AddRow(new ActionRowBuilder().WithSelectMenu(selectBuilder));
                }
            }

            await userMessage.ModifyAsync(m => m.Components = components.Build());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update panel components for panel {PanelId}", panel.Id);
        }
    }

    private async Task SetTicketPermissionsAsync(ITextChannel channel, IUser creator, PanelButton button = null,
        SelectMenuOption option = null)
    {
        var supportRoles = button?.SupportRoles ?? option?.SupportRoles ?? [];
        var viewerRoles = button?.ViewerRoles ?? option?.ViewerRoles ?? [];

        // Deny everyone
        await channel.AddPermissionOverwriteAsync(channel.Guild.EveryoneRole,
            new OverwritePermissions(viewChannel: PermValue.Deny));

        await channel.AddPermissionOverwriteAsync(creator,
            new OverwritePermissions(
                viewChannel: PermValue.Allow,
                sendMessages: PermValue.Allow,
                readMessageHistory: PermValue.Allow,
                attachFiles: PermValue.Allow,
                embedLinks: PermValue.Allow));

        // Support roles get full access
        foreach (var roleId in supportRoles)
        {
            var role = channel.Guild.GetRole(roleId);
            if (role != null)
            {
                await channel.AddPermissionOverwriteAsync(role,
                    new OverwritePermissions(
                        viewChannel: PermValue.Allow,
                        sendMessages: PermValue.Allow,
                        readMessageHistory: PermValue.Allow,
                        attachFiles: PermValue.Allow,
                        embedLinks: PermValue.Allow,
                        manageMessages: PermValue.Allow));
            }
        }

        // Viewer roles can only view
        foreach (var roleId in viewerRoles)
        {
            var role = channel.Guild.GetRole(roleId);
            if (role != null)
            {
                await channel.AddPermissionOverwriteAsync(role,
                    new OverwritePermissions(
                        viewChannel: PermValue.Allow,
                        readMessageHistory: PermValue.Allow,
                        sendMessages: PermValue.Deny));
            }
        }
    }

    /// <summary>
    ///     Recreates a deleted ticket panel in its original channel and updates the message ID.
    /// </summary>
    /// <param name="guildId">The ID of the guild containing the panel.</param>
    /// <param name="panelId">The message ID of the panel to recreate.</param>
    /// <returns>A tuple containing success status, new message ID, and channel mention.</returns>
    public async Task<(bool success, ulong? newMessageId, string channelMention, string error)> RecreatePanelAsync(
        ulong guildId, ulong panelId)
    {
        try
        {
            await using var ctx = await dbFactory.CreateConnectionAsync();

            // Load panel with all components
            var panel = await ctx.TicketPanels
                .LoadWithAsTable(p => p.PanelButtons)
                .LoadWithAsTable(p => p.PanelSelectMenus)
                .FirstOrDefaultAsync(p => p.MessageId == panelId && p.GuildId == guildId);

            if (panel == null)
                return (false, null, null, "Panel not found in database.");

            // Check if the channel still exists
            IGuild guild = client.GetGuild(guildId);
            var channel = await guild.GetTextChannelAsync(panel.ChannelId);
            if (channel == null)
                return (false, null, null, "The original channel for this panel no longer exists.");

            // Check if the message still exists
            try
            {
                var existingMessage = await channel.GetMessageAsync(panel.MessageId);
                if (existingMessage != null)
                    return (false, null, channel.Mention, "Panel message still exists. No recreation needed.");
            }
            catch
            {
                // Message doesn't exist, proceed with recreation
            }

            // Load select menu options if any menus exist
            if (panel.PanelSelectMenus?.Any() == true)
            {
                var menuIds = panel.PanelSelectMenus.Select(m => m.Id).ToArray();
                var options = await ctx.SelectMenuOptions
                    .Where(o => menuIds.Contains(o.SelectMenuId))
                    .ToListAsync();

                foreach (var menu in panel.PanelSelectMenus)
                {
                    menu.SelectMenuOptions = options.Where(o => o.SelectMenuId == menu.Id).ToList();
                }
            }

            // Parse the embed JSON and recreate the message
            var success = SmartEmbed.TryParse(panel.EmbedJson, panel.GuildId, out var embeds, out var plainText, out _);
            if (!success)
                return (false, null, channel.Mention, "Failed to parse panel embed configuration.");

            // Build components
            var components = new ComponentBuilder();

            // Add buttons if any exist
            if (panel.PanelButtons?.Any() == true)
            {
                var buttonRow = new ActionRowBuilder();
                foreach (var button in panel.PanelButtons)
                {
                    var btnBuilder = new ButtonBuilder()
                        .WithLabel(button.Label)
                        .WithCustomId(button.CustomId)
                        .WithStyle((ButtonStyle)button.Style);

                    if (!string.IsNullOrEmpty(button.Emoji))
                    {
                        try
                        {
                            btnBuilder.WithEmote(Emote.Parse(button.Emoji));
                        }
                        catch
                        {
                            try
                            {
                                btnBuilder.WithEmote(new Emoji(button.Emoji));
                            }
                            catch (Exception ex)
                            {
                                logger.LogWarning(ex, "Failed to parse emoji {Emoji} for button {ButtonId}",
                                    button.Emoji,
                                    button.Id);
                            }
                        }
                    }

                    buttonRow.WithButton(btnBuilder);
                }

                components.AddRow(buttonRow);
            }

            // Add select menus if any exist
            if (panel.PanelSelectMenus?.Any() == true)
            {
                foreach (var menu in panel.PanelSelectMenus)
                {
                    var selectBuilder = new SelectMenuBuilder()
                        .WithCustomId(menu.CustomId)
                        .WithPlaceholder(menu.Placeholder)
                        .WithMaxValues(1);

                    foreach (var option in menu.SelectMenuOptions)
                    {
                        var optBuilder = new SelectMenuOptionBuilder()
                            .WithLabel(option.Label)
                            .WithValue(option.Value)
                            .WithDescription(option.Description);

                        if (!string.IsNullOrEmpty(option.Emoji))
                        {
                            try
                            {
                                optBuilder.WithEmote(Emote.Parse(option.Emoji));
                            }
                            catch
                            {
                                try
                                {
                                    optBuilder.WithEmote(new Emoji(option.Emoji));
                                }
                                catch (Exception ex)
                                {
                                    logger.LogWarning(ex, "Failed to parse emoji {Emoji} for option {OptionId}",
                                        option.Emoji,
                                        option.Id);
                                }
                            }
                        }

                        selectBuilder.AddOption(optBuilder);
                    }

                    components.AddRow(new ActionRowBuilder().WithSelectMenu(selectBuilder));
                }
            }

            // Send the recreated panel message
            var newMessage = await channel.SendMessageAsync(
                plainText,
                embeds: embeds,
                components: components.Build());

            // Update the panel's message ID in the database
            await ctx.TicketPanels
                .Where(p => p.Id == panel.Id)
                .Set(p => p.MessageId, newMessage.Id)
                .UpdateAsync();

            logger.LogInformation(
                "Manually recreated panel {PanelId} with new message ID {MessageId} in guild {GuildId}",
                panel.Id, newMessage.Id, guildId);

            return (true, newMessage.Id, channel.Mention, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error recreating panel {PanelId} in guild {GuildId}", panelId, guildId);
            return (false, null, null, "An error occurred while recreating the panel.");
        }
    }

    /// <summary>
    ///     Checks all panels in a guild for missing messages.
    /// </summary>
    /// <param name="guildId">The ID of the guild to check.</param>
    /// <returns>A list of panel status information.</returns>
    public async Task<List<PanelStatusInfo>> CheckPanelStatusAsync(ulong guildId)
    {
        try
        {
            await using var ctx = await dbFactory.CreateConnectionAsync();

            var panels = await ctx.TicketPanels
                .Where(p => p.GuildId == guildId)
                .ToListAsync();

            var statusList = new List<PanelStatusInfo>();
            IGuild guild = client.GetGuild(guildId);

            foreach (var panel in panels)
            {
                var status = new PanelStatusInfo
                {
                    PanelId = panel.MessageId, ChannelId = panel.ChannelId
                };

                var channel = await guild.GetTextChannelAsync(panel.ChannelId);
                if (channel == null)
                {
                    status.Status = PanelStatus.ChannelDeleted;
                    status.ChannelName = "deleted-channel";
                }
                else
                {
                    status.ChannelName = channel.Name;
                    try
                    {
                        var message = await channel.GetMessageAsync(panel.MessageId);
                        status.Status = message != null ? PanelStatus.OK : PanelStatus.MessageMissing;
                    }
                    catch
                    {
                        status.Status = PanelStatus.MessageMissing;
                    }
                }

                statusList.Add(status);
            }

            return statusList;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error checking panel status in guild {GuildId}", guildId);
            return [];
        }
    }

    /// <summary>
    ///     Recreates all panels with missing messages in a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <returns>A tuple containing the number of successfully recreated panels and any that failed.</returns>
    public async Task<(int recreated, int failed, List<string> errors)> RecreateAllMissingPanelsAsync(ulong guildId)
    {
        var panelStatuses = await CheckPanelStatusAsync(guildId);
        var missingPanels = panelStatuses.Where(p => p.Status == PanelStatus.MessageMissing).ToList();

        var recreated = 0;
        var failed = 0;
        var errors = new List<string>();

        foreach (var panel in missingPanels)
        {
            var (success, _, _, error) = await RecreatePanelAsync(guildId, panel.PanelId);
            if (success)
            {
                recreated++;
            }
            else
            {
                failed++;
                if (!string.IsNullOrEmpty(error))
                    errors.Add($"Panel {panel.PanelId}: {error}");
            }
        }

        return (recreated, failed, errors);
    }

    /// <summary>
    ///     Handles message deletion events and recreates ticket panels if they were deleted.
    /// </summary>
    /// <param name="message">The deleted message.</param>
    /// <param name="channel">The channel where the message was deleted.</param>
    private async Task HandleMessageDeleted(Cacheable<IMessage, ulong> message,
        Cacheable<IMessageChannel, ulong> channel)
    {
        if (!message.HasValue || !channel.HasValue)
            return;

        await using var ctx = await dbFactory.CreateConnectionAsync();

        // Check if deleted message was a panel
        var panel = await ctx.TicketPanels
            .LoadWithAsTable(p => p.PanelButtons)
            .LoadWithAsTable(p => p.PanelSelectMenus)
            .FirstOrDefaultAsync(p => p.MessageId == message.Value.Id);

        if (panel != null)
        {
            try
            {
                // Load select menu options if any menus exist
                if (panel.PanelSelectMenus?.Any() == true)
                {
                    var menuIds = panel.PanelSelectMenus.Select(m => m.Id).ToArray();
                    var options = await ctx.SelectMenuOptions
                        .Where(o => menuIds.Contains(o.SelectMenuId))
                        .ToListAsync();

                    foreach (var menu in panel.PanelSelectMenus)
                    {
                        menu.SelectMenuOptions = options.Where(o => o.SelectMenuId == menu.Id).ToList();
                    }
                }

                // Get the channel where the panel was deleted
                var targetChannel = channel.Value as ITextChannel;
                if (targetChannel == null)
                {
                    logger.LogWarning("Cannot recreate panel {PanelId} - channel is not a text channel", panel.Id);
                    return;
                }

                // Parse the embed JSON and recreate the message
                var success = SmartEmbed.TryParse(panel.EmbedJson, panel.GuildId, out var embeds, out var plainText,
                    out _);
                if (!success)
                {
                    logger.LogError("Failed to parse embed JSON for panel {PanelId}", panel.Id);
                    return;
                }

                // Build components
                var components = new ComponentBuilder();

                // Add buttons if any exist
                if (panel.PanelButtons?.Any() == true)
                {
                    var buttonRow = new ActionRowBuilder();
                    foreach (var button in panel.PanelButtons)
                    {
                        var btnBuilder = new ButtonBuilder()
                            .WithLabel(button.Label)
                            .WithCustomId(button.CustomId)
                            .WithStyle((ButtonStyle)button.Style);

                        if (!string.IsNullOrEmpty(button.Emoji))
                        {
                            try
                            {
                                btnBuilder.WithEmote(Emote.Parse(button.Emoji));
                            }
                            catch
                            {
                                // If emoji parsing fails, try as unicode emoji
                                try
                                {
                                    btnBuilder.WithEmote(new Emoji(button.Emoji));
                                }
                                catch (Exception ex)
                                {
                                    logger.LogWarning(ex, "Failed to parse emoji {Emoji} for button {ButtonId}",
                                        button.Emoji,
                                        button.Id);
                                }
                            }
                        }

                        buttonRow.WithButton(btnBuilder);
                    }

                    components.AddRow(buttonRow);
                }

                // Add select menus if any exist
                if (panel.PanelSelectMenus?.Any() == true)
                {
                    foreach (var menu in panel.PanelSelectMenus)
                    {
                        var selectBuilder = new SelectMenuBuilder()
                            .WithCustomId(menu.CustomId)
                            .WithPlaceholder(menu.Placeholder)
                            .WithMaxValues(1);

                        foreach (var option in menu.SelectMenuOptions)
                        {
                            var optBuilder = new SelectMenuOptionBuilder()
                                .WithLabel(option.Label)
                                .WithValue(option.Value)
                                .WithDescription(option.Description);

                            if (!string.IsNullOrEmpty(option.Emoji))
                            {
                                try
                                {
                                    optBuilder.WithEmote(Emote.Parse(option.Emoji));
                                }
                                catch
                                {
                                    // If emoji parsing fails, try as unicode emoji
                                    try
                                    {
                                        optBuilder.WithEmote(new Emoji(option.Emoji));
                                    }
                                    catch (Exception ex)
                                    {
                                        logger.LogWarning(ex, "Failed to parse emoji {Emoji} for option {OptionId}",
                                            option.Emoji, option.Id);
                                    }
                                }
                            }

                            selectBuilder.AddOption(optBuilder);
                        }

                        components.AddRow(new ActionRowBuilder().WithSelectMenu(selectBuilder));
                    }
                }

                // Send the recreated panel message
                var newMessage = await targetChannel.SendMessageAsync(
                    plainText,
                    embeds: embeds,
                    components: components.Build());

                // Update the panel's message ID in the database
                await ctx.TicketPanels
                    .Where(p => p.Id == panel.Id)
                    .Set(p => p.MessageId, newMessage.Id)
                    .UpdateAsync();

                logger.LogInformation("Successfully recreated deleted panel {PanelId} with new message ID {MessageId}",
                    panel.Id, newMessage.Id);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to recreate deleted panel {PanelId}", panel.Id);

                // If recreation fails, we could optionally notify admins
                try
                {
                    var settings = await ctx.GuildTicketSettings.FirstOrDefaultAsync(s => s.GuildId == panel.GuildId);
                    if (settings?.LogChannelId.HasValue == true)
                    {
                        IGuild guild = client.GetGuild(panel.GuildId);
                        var logChannel = await guild?.GetTextChannelAsync(settings.LogChannelId.Value);
                        if (logChannel != null)
                        {
                            var errorEmbed = new EmbedBuilder()
                                .WithTitle(strings.PanelRecreationFailedTitle(guild.Id))
                                .WithDescription(strings.PanelRecreationFailedDesc(guild.Id, channel.Value.Id))
                                .AddField("Panel ID", panel.Id)
                                .AddField("Error", ex.Message)
                                .WithColor(Color.Red)
                                .WithCurrentTimestamp()
                                .Build();

                            await logChannel.SendMessageAsync(embed: errorEmbed);
                        }
                    }
                }
                catch (Exception logEx)
                {
                    logger.LogError(logEx, "Failed to log panel recreation failure");
                }
            }
        }
    }

    /// <summary>
    ///     Deletes a select menu and all its options from a panel
    /// </summary>
    /// <param name="guild">The guild containing the panel</param>
    /// <param name="menuId">The ID of the select menu to delete</param>
    /// <returns>True if the menu was successfully deleted, false otherwise</returns>
    public async Task<bool> DeleteSelectMenuAsync(IGuild guild, int menuId)
    {
        await using var ctx = await dbFactory.CreateConnectionAsync();

        try
        {
            // Get the menu to verify it exists and belongs to this guild
            var menu = await ctx.PanelSelectMenus
                .LoadWithAsTable(m => m.Panel)
                .LoadWithAsTable(m => m.SelectMenuOptions)
                .FirstOrDefaultAsync(m => m.Id == menuId && m.Panel.GuildId == guild.Id);

            if (menu == null)
                return false;

            // Delete all options first (due to foreign key constraints)
            await ctx.SelectMenuOptions
                .Where(o => o.SelectMenuId == menuId)
                .DeleteAsync();

            // Delete the menu itself
            await ctx.PanelSelectMenus
                .Where(m => m.Id == menuId)
                .DeleteAsync();

            // Update the panel in Discord
            await UpdatePanelComponentsAsync(menu.Panel);

            logger.LogInformation("Deleted select menu {MenuId} from guild {GuildId}", menuId, guild.Id);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete select menu {MenuId} from guild {GuildId}", menuId, guild.Id);
            return false;
        }
    }

    /// <summary>
    ///     Deletes a single option from a select menu
    /// </summary>
    /// <param name="guild">The guild containing the panel</param>
    /// <param name="optionId">The ID of the option to delete</param>
    /// <returns>True if the option was successfully deleted, false otherwise</returns>
    public async Task<bool> DeleteSelectOptionAsync(IGuild guild, int optionId)
    {
        await using var ctx = await dbFactory.CreateConnectionAsync();

        try
        {
            // Get the option and verify permissions
            var option = await ctx.SelectMenuOptions
                .LoadWithAsTable(o => o.SelectMenu)
                .LoadWithAsTable(m => m.SelectMenu.Panel)
                .FirstOrDefaultAsync(o => o.Id == optionId && o.SelectMenu.Panel.GuildId == guild.Id);

            if (option == null)
                return false;

            // Delete the option
            await ctx.SelectMenuOptions
                .Where(o => o.Id == optionId)
                .DeleteAsync();

            // Update the panel in Discord
            await UpdatePanelComponentsAsync(option.SelectMenu.Panel);

            logger.LogInformation("Deleted select menu option {OptionId} from guild {GuildId}", optionId, guild.Id);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete select menu option {OptionId} from guild {GuildId}", optionId,
                guild.Id);
            return false;
        }
    }

    /// <summary>
    ///     Gets a specific select menu option by ID
    /// </summary>
    /// <param name="optionId">The ID of the option to retrieve</param>
    /// <returns>The select menu option, or null if not found</returns>
    public async Task<SelectMenuOption?> GetSelectOptionAsync(int optionId)
    {
        await using var ctx = await dbFactory.CreateConnectionAsync();

        return await ctx.SelectMenuOptions
            .LoadWithAsTable(o => o.SelectMenu)
            .LoadWithAsTable(m => m.SelectMenu.Panel)
            .FirstOrDefaultAsync(o => o.Id == optionId);
    }

    /// <summary>
    ///     Gets the number of options in a select menu
    /// </summary>
    /// <param name="menuId">The ID of the select menu</param>
    /// <returns>The count of options in the menu</returns>
    public async Task<int> GetSelectMenuOptionCountAsync(int menuId)
    {
        await using var ctx = await dbFactory.CreateConnectionAsync();

        return await ctx.SelectMenuOptions
            .Where(o => o.SelectMenuId == menuId)
            .CountAsync();
    }

    /// <summary>
    ///     Gets active tickets for a user in a guild.
    /// </summary>
    public async Task<List<Ticket>> GetActiveTicketsAsync(ulong guildId, ulong userId, int id)
    {
        await using var ctx = await dbFactory.CreateConnectionAsync();

        return await ctx.Tickets
            .Where(t => t.GuildId == guildId &&
                        t.CreatorId == userId &&
                        !t.ClosedAt.HasValue &&
                        (t.SelectOptionId == id || t.ButtonId == id))
            .ToListAsync();
    }

    /// <summary>
    ///     Sets the priority for a ticket.
    /// </summary>
    /// <param name="ticket">The ticket parameter.</param>
    /// <param name="priority">The priority string.</param>
    /// <param name="staff">The staff parameter.</param>
    public async Task SetTicketPriorityAsync(Ticket ticket, string priority, IGuildUser staff)
    {
        await using var ctx = await dbFactory.CreateConnectionAsync();

        var allowedPriorities = ticket.Button?.AllowedPriorities ??
                                ticket.SelectOption?.AllowedPriorities ?? [];
        if (allowedPriorities.Any() && !allowedPriorities.Contains(priority))
            throw new InvalidOperationException("Invalid priority level");

        ticket.Priority = priority;
        ticket.LastActivityAt = DateTime.UtcNow;
        await ctx.UpdateAsync(ticket);

        IGuild guild = client.GetGuild(ticket.GuildId);

        if (await guild.GetChannelAsync(ticket.ChannelId) is ITextChannel channel)
        {
            var embed = new EmbedBuilder()
                .WithTitle(strings.TicketPriorityUpdated(guild.Id))
                .WithDescription(strings.TicketPrioritySetBy(guild.Id, priority, staff.Mention))
                .WithColor(Color.Blue)
                .Build();

            await channel.SendMessageAsync(embed: embed);
        }
    }

    /// <summary>
    ///     Gets the ticket settings for a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <returns>The guild ticket settings, or null if not found.</returns>
    public async Task<GuildTicketSetting?> GetGuildTicketSettingsAsync(ulong guildId)
    {
        await using var ctx = await dbFactory.CreateConnectionAsync();
        return await ctx.GuildTicketSettings.FirstOrDefaultAsync(x => x.GuildId == guildId);
    }

    /// <summary>
    ///     Sets the transcript channel for a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="channelId">The ID of the channel to set as the transcript channel.</param>
    public async Task SetTranscriptChannelAsync(ulong guildId, ulong channelId)
    {
        try
        {
            await using var ctx = await dbFactory.CreateConnectionAsync();
            var settings = await ctx.GuildTicketSettings.FirstOrDefaultAsync(x => x.GuildId == guildId);

            if (settings == null)
            {
                // Create new settings
                settings = new GuildTicketSetting
                {
                    GuildId = guildId, TranscriptChannelId = channelId
                };
                await ctx.InsertAsync(settings);
            }
            else
            {
                // Update existing settings
                settings.TranscriptChannelId = channelId;
                await ctx.UpdateAsync(settings);
            }
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to set transcript channel for guild {GuildId}", guildId);
        }
    }

    /// <summary>
    ///     Sets the log channel for a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="channelId">The ID of the channel to set as the log channel.</param>
    public async Task SetLogChannelAsync(ulong guildId, ulong channelId)
    {
        try
        {
            await using var ctx = await dbFactory.CreateConnectionAsync();
            var settings = await ctx.GuildTicketSettings.FirstOrDefaultAsync(x => x.GuildId == guildId);

            if (settings == null)
            {
                // Create new settings
                settings = new GuildTicketSetting
                {
                    GuildId = guildId, LogChannelId = channelId
                };
                await ctx.InsertAsync(settings);
            }
            else
            {
                // Update existing settings
                settings.LogChannelId = channelId;
                await ctx.UpdateAsync(settings);
            }
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to set log channel for guild {GuildId}", guildId);
        }
    }


    /// <summary>
    ///     Links tickets to an existing case.
    /// </summary>
    /// <param name="caseId">The ID of the case to link tickets to.</param>
    /// <param name="tickets">The tickets to link to the case.</param>
    public async Task LinkTicketsToCase(int caseId, IEnumerable<Ticket> tickets)
    {
        await using var ctx = await dbFactory.CreateConnectionAsync();
        var ticketCase = await ctx.TicketCases.FindAsync(caseId);
        if (ticketCase == null) throw new InvalidOperationException("Case not found.");

        var ticketIds = tickets.Select(t => t.Id).ToList();

        // Use bulk update query instead of updating enumerable
        await ctx.Tickets
            .Where(t => ticketIds.Contains(t.Id))
            .Set(t => t.CaseId, caseId)
            .UpdateAsync();

        // Update in-memory objects
        foreach (var ticket in tickets)
        {
            ticket.CaseId = caseId;
        }
    }

    /// <summary>
    ///     Gets a formatted list of all buttons on a specific ticket panel
    /// </summary>
    /// <param name="panelId">The message ID of the panel to get buttons from</param>
    /// <returns>A list of ButtonInfo objects containing button details</returns>
    public async Task<List<ButtonInfo>> GetPanelButtonsAsync(ulong panelId)
    {
        await using var ctx = await dbFactory.CreateConnectionAsync();
        var panel = await ctx.TicketPanels
            .LoadWithAsTable(p => p.PanelButtons)
            .FirstOrDefaultAsync(p => p.MessageId == panelId);

        if (panel == null)
            return [];

        return panel.PanelButtons.Select(b => new ButtonInfo
        {
            Id = b.Id,
            CustomId = b.CustomId,
            Label = b.Label,
            Style = (ButtonStyle)b.Style,
            Emoji = b.Emoji,
            CategoryId = b.CategoryId,
            ArchiveCategoryId = b.ArchiveCategoryId,
            SupportRoles = b.SupportRoles.ToList(),
            ViewerRoles = b.ViewerRoles.ToList(),
            HasModal = !string.IsNullOrEmpty(b.ModalJson),
            HasCustomOpenMessage = !string.IsNullOrEmpty(b.OpenMessageJson)
        }).ToList();
    }

    /// <summary>
    ///     Gets a formatted list of all select menus on a specific ticket panel
    /// </summary>
    /// <param name="panelId">The message ID of the panel to get select menus from</param>
    /// <returns>A list of SelectMenuInfo objects containing menu details</returns>
    public async Task<List<SelectMenuInfo>> GetPanelSelectMenusAsync(ulong panelId)
    {
        await using var ctx = await dbFactory.CreateConnectionAsync();
        var results = await (
            from ticketPanel in ctx.TicketPanels
            where ticketPanel.MessageId == panelId
            from menu in ctx.PanelSelectMenus.Where(m => m.PanelId == ticketPanel.Id).DefaultIfEmpty()
            from option in ctx.SelectMenuOptions.Where(o => o.SelectMenuId == menu.Id).DefaultIfEmpty()
            select new
            {
                Panel = ticketPanel, Menu = menu, Option = option
            }).ToListAsync();

        var panel = results.FirstOrDefault()?.Panel;
        if (panel != null)
        {
            panel.PanelSelectMenus = results
                .Where(r => r.Menu != null)
                .GroupBy(r => r.Menu.Id)
                .Select(g =>
                {
                    var menu = g.First().Menu;
                    menu.SelectMenuOptions = g
                        .Where(x => x.Option != null)
                        .Select(x => x.Option)
                        .ToList();
                    return menu;
                })
                .ToList();
        }

        if (panel == null)
            return [];

        return panel.PanelSelectMenus.Select(m => new SelectMenuInfo
        {
            Id = m.Id,
            CustomId = m.CustomId,
            Placeholder = m.Placeholder,
            Options = m.SelectMenuOptions.Select(o => new SelectOptionInfo
            {
                Id = o.Id,
                Label = o.Label,
                Value = o.Value,
                Description = o.Description ?? "",
                Emoji = o.Emoji ?? "",
                CategoryId = o.CategoryId,
                ArchiveCategoryId = o.ArchiveCategoryId,
                HasModal = !string.IsNullOrEmpty(o.ModalJson),
                HasCustomOpenMessage = !string.IsNullOrEmpty(o.OpenMessageJson)
            }).ToList()
        }).ToList();
    }

    /// <summary>
    ///     Gets a detailed list of all ticket panels and their components in a guild
    /// </summary>
    /// <param name="guildId">The ID of the guild to get panels from</param>
    /// <returns>A list of PanelInfo objects containing complete panel details</returns>
    public async Task<List<PanelInfo>> GetAllPanelsAsync(ulong guildId)
    {
        await using var ctx = await dbFactory.CreateConnectionAsync();

        var panels = await ctx.TicketPanels
            .Where(p => p.GuildId == guildId)
            .ToListAsync();

        if (panels.Any())
        {
            var panelIds = panels.Select(p => p.Id).ToArray();

            var panelButtons = await ctx.PanelButtons
                .Where(b => panelIds.Contains(b.PanelId))
                .ToListAsync();

            var selectMenus = await ctx.PanelSelectMenus
                .Where(m => panelIds.Contains(m.PanelId))
                .ToListAsync();

            var menuIds = selectMenus.Select(m => m.Id).ToArray();
            var options = menuIds.Any()
                ? await ctx.SelectMenuOptions
                    .Where(o => menuIds.Contains(o.SelectMenuId))
                    .ToListAsync()
                : [];

            foreach (var panel in panels)
            {
                panel.PanelButtons = panelButtons.Where(b => b.PanelId == panel.Id).ToList();

                var panelSelectMenus = selectMenus.Where(m => m.PanelId == panel.Id).ToList();
                foreach (var menu in panelSelectMenus)
                {
                    menu.SelectMenuOptions = options.Where(o => o.SelectMenuId == menu.Id).ToList();
                }

                panel.PanelSelectMenus = panelSelectMenus;
            }
        }

        return panels.Select(p => new PanelInfo
        {
            MessageId = p.MessageId,
            ChannelId = p.ChannelId,
            Buttons = p.PanelButtons.Select(b => new ButtonInfo
            {
                Id = b.Id,
                CustomId = b.CustomId,
                Label = b.Label,
                Style = (ButtonStyle)b.Style,
                Emoji = b.Emoji,
                CategoryId = b.CategoryId,
                ArchiveCategoryId = b.ArchiveCategoryId,
                SupportRoles = b.SupportRoles.ToList(),
                ViewerRoles = b.ViewerRoles.ToList(),
                HasModal = !string.IsNullOrEmpty(b.ModalJson),
                HasCustomOpenMessage = !string.IsNullOrEmpty(b.OpenMessageJson)
            }).ToList(),
            SelectMenus = p.PanelSelectMenus.Select(m => new SelectMenuInfo
            {
                Id = m.Id,
                CustomId = m.CustomId,
                Placeholder = m.Placeholder,
                Options = m.SelectMenuOptions.Select(o => new SelectOptionInfo
                {
                    Id = o.Id,
                    Label = o.Label,
                    Value = o.Value,
                    Description = o.Description,
                    Emoji = o.Emoji,
                    CategoryId = o.CategoryId,
                    ArchiveCategoryId = o.ArchiveCategoryId,
                    HasModal = !string.IsNullOrEmpty(o.ModalJson),
                    HasCustomOpenMessage = !string.IsNullOrEmpty(o.OpenMessageJson)
                }).ToList()
            }).ToList()
        }).ToList();
    }

    /// <summary>
    ///     Retrieves a ticket by its ID.
    /// </summary>
    /// <param name="ticketId">The ID of the ticket.</param>
    /// <returns>The ticket object, if found.</returns>
    public async Task<Ticket?> GetTicketAsync(int ticketId)
    {
        await using var ctx = await dbFactory.CreateConnectionAsync();
        return await ctx.Tickets.FirstOrDefaultAsync(t => t.Id == ticketId);
    }

    /// <summary>
    ///     Retrieves a ticket by its channel ID.
    /// </summary>
    /// <param name="channelId">The ID of the channel.</param>
    /// <returns>The ticket object, if found.</returns>
    public async Task<Ticket?> GetTicketAsync(ulong channelId)
    {
        await using var ctx = await dbFactory.CreateConnectionAsync();
        return await ctx.Tickets.FirstOrDefaultAsync(t => t.ChannelId == channelId);
    }


    /// <summary>
    ///     Retrieves tickets by their IDs.
    /// </summary>
    /// <param name="ticketIds">The IDs of the tickets to retrieve.</param>
    /// <returns>A list of tickets matching the specified IDs.</returns>
    /// <summary>
    ///     Gets all tickets for a guild
    /// </summary>
    /// <param name="guildId">The guild ID</param>
    /// <param name="includeArchived">Whether to include archived tickets</param>
    /// <param name="includeClosed">Whether to include closed tickets</param>
    /// <param name="includeDeleted">Whether to include soft-deleted tickets</param>
    /// <returns>List of tickets matching the criteria</returns>
    public async Task<List<Ticket>> GetGuildTicketsAsync(ulong guildId, bool includeArchived = true,
        bool includeClosed = true, bool includeDeleted = false)
    {
        await using var ctx = await dbFactory.CreateConnectionAsync();

        var query = ctx.Tickets
            .LoadWithAsTable(t => t.Button)
            .LoadWithAsTable(t => t.SelectOption)
            .Where(t => t.GuildId == guildId);

        if (!includeDeleted)
            query = query.Where(t => !t.IsDeleted);

        if (!includeArchived)
            query = query.Where(t => !t.IsArchived);

        if (!includeClosed)
            query = query.Where(t => !t.ClosedAt.HasValue);

        return await query.OrderByDescending(t => t.CreatedAt).ToListAsync();
    }

    /// <summary>
    ///     Retrieves tickets by their IDs.
    /// </summary>
    /// <param name="ticketIds">The IDs of the tickets to retrieve.</param>
    /// <returns>A list of tickets matching the specified IDs.</returns>
    public async Task<List<Ticket>> GetTicketsAsync(IEnumerable<int> ticketIds)
    {
        await using var ctx = await dbFactory.CreateConnectionAsync();
        return await ctx.Tickets.Where(t => ticketIds.Contains(t.Id)).ToListAsync();
    }

    /// <summary>
    ///     Retrieves a panel button by its db Id.
    /// </summary>
    /// <param name="buttonId">The db ID of the button to retrieve.</param>
    /// <returns>The panel button matching the specified ID, if found.</returns>
    public async Task<PanelButton?> GetButtonAsync(int buttonId)
    {
        await using var ctx = await dbFactory.CreateConnectionAsync();
        return await ctx.PanelButtons
            .LoadWithAsTable(b => b.Panel) // Include the related panel if needed
            .FirstOrDefaultAsync(b => b.Id == buttonId);
    }

    /// <summary>
    ///     Retrieves a panel button by its custom ID.
    /// </summary>
    /// <param name="buttonId">The custom ID of the button to retrieve.</param>
    /// <returns>The panel button matching the specified ID, if found.</returns>
    public async Task<PanelButton?> GetButtonAsync(string buttonId)
    {
        await using var ctx = await dbFactory.CreateConnectionAsync();
        return await ctx.PanelButtons
            .LoadWithAsTable(b => b.Panel) // Include the related panel if needed
            .FirstOrDefaultAsync(b => b.CustomId == buttonId);
    }

    /// <summary>
    ///     Validates and creates modal for ticket creation
    /// </summary>
    public async Task HandleModalCreation(IGuildUser user, string modalJson, string customId,
        IDiscordInteraction component)
    {
        try
        {
            logger.LogInformation(modalJson);

            ModalConfiguration modalConfig;
            Dictionary<string, ModalFieldConfig> fields;

            try
            {
                modalConfig = JsonSerializer.Deserialize<ModalConfiguration>(modalJson,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                fields = modalConfig.Fields;
            }
            catch
            {
                fields = JsonSerializer.Deserialize<Dictionary<string, ModalFieldConfig>>(modalJson,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                modalConfig = new ModalConfiguration
                {
                    Fields = fields
                };
            }

            logger.LogInformation(JsonSerializer.Serialize(fields));

            // Validate field count
            if (fields.Count > 5)
                throw new ArgumentException("Modal cannot have more than 5 fields");

            var mb = new ModalBuilder()
                .WithCustomId(customId)
                .WithTitle(modalConfig.Title ?? strings.CreateTicketModalTitle(user.GuildId));

            foreach (var (key, field) in fields)
            {
                // Validate and enforce length limits
                var minLength = Math.Max(0, Math.Min(field.MinLength ?? 0, 4000));
                var maxLength = Math.Max(minLength, Math.Min(field.MaxLength ?? 4000, 4000));

                // Determine style
                var style = field.Style == 2 ? TextInputStyle.Paragraph : TextInputStyle.Short;
                Console.WriteLine(JsonSerializer.Serialize(field));

                mb.AddTextInput(
                    field.Label,
                    key.ToLower().Replace(" ", "_"),
                    style,
                    field.Placeholder,
                    minLength == 0 ? null : minLength,
                    maxLength,
                    field.Required,
                    field.Value
                );
            }

            _ = Task.Run(async () => await component.RespondWithModalAsync(mb.Build()));
            var msg = (component as IComponentInteraction).Message;
            await (component as IComponentInteraction).Message.ModifyAsync(x =>
            {
                x.Components = ComponentBuilder.FromComponents(msg.Components).Build();
            });
        }
        catch (JsonException ex)
        {
            logger.LogError($"Invalid modal configuration format: {ex}");
            await component.RespondAsync(strings.InvalidTicketFormConfig(user.GuildId), ephemeral: true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating modal");
            await component.RespondAsync(strings.FailedCreateTicketForm(user.GuildId), ephemeral: true);
        }
    }


    /// <summary>
    ///     Gets a ticket panel by ID.
    /// </summary>
    /// <param name="panelId">The ID of the panel to retrieve.</param>
    /// <returns>The panel if found, null otherwise.</returns>
    public async Task<TicketPanel?> GetPanelAsync(ulong panelId)
    {
        await using var ctx = await dbFactory.CreateConnectionAsync();
        return await ctx.TicketPanels.FirstOrDefaultAsync(x => x.MessageId == panelId);
    }

    /// <summary>
    ///     Adds a select menu to an existing ticket panel
    /// </summary>
    /// <param name="panel">The panel to add the select menu to</param>
    /// <param name="placeholder">The placeholder text for the menu</param>
    /// <param name="firstOptionLabel">Label for the first option</param>
    /// <param name="firstOptionDescription">Description for the first option</param>
    /// <param name="firstOptionEmoji">Emoji for the first option</param>
    /// <param name="minValues">Minimum number of selections required</param>
    /// <param name="maxValues">Maximum number of selections allowed</param>
    /// <param name="updateComponents">Whether to update the panel components after adding the menu</param>
    /// <returns>The created panel select menu</returns>
    public async Task<PanelSelectMenu> AddSelectMenuAsync(
        TicketPanel panel,
        string placeholder,
        string firstOptionLabel,
        string firstOptionDescription = null,
        string firstOptionEmoji = null,
        int minValues = 1,
        int maxValues = 1, bool updateComponents = true)
    {
        await using var ctx = await dbFactory.CreateConnectionAsync();

        // Check component limits before adding (1 select menu = 5 components)
        var currentButtonCount = await ctx.PanelButtons.Where(b => b.PanelId == panel.Id).CountAsync();
        var currentMenuCount = await ctx.PanelSelectMenus.Where(m => m.PanelId == panel.Id).CountAsync();
        var currentTotal = currentButtonCount + currentMenuCount * 5;

        if (currentTotal + 5 > 25)
        {
            logger.LogError(
                "Cannot add select menu to panel {PanelId}: would exceed component limit ({CurrentTotal} + 5 > 25)",
                panel.Id, currentTotal);
            return null;
        }

        try
        {
            var menu = new PanelSelectMenu
            {
                PanelId = panel.Id, CustomId = $"ticket_select_{Guid.NewGuid():N}", Placeholder = placeholder
            };

            menu.Id = await ctx.InsertWithInt32IdentityAsync(menu);

            logger.LogInformation("Created PanelSelectMenu with ID: {MenuId}", menu.Id);

            var firstOption = new SelectMenuOption
            {
                SelectMenuId = menu.Id,
                Label = firstOptionLabel,
                Value = $"option_{Guid.NewGuid():N}",
                Description = firstOptionDescription,
                Emoji = firstOptionEmoji,
                ChannelNameFormat = "ticket-{username}-{id}",
                SaveTranscript = true,
                DeleteOnClose = false,
                LockOnClose = true,
                RenameOnClose = true,
                RemoveCreatorOnClose = true,
                DeleteDelay = TimeSpan.FromMinutes(5),
                LockOnArchive = true,
                RenameOnArchive = true,
                RemoveCreatorOnArchive = true,
                AutoArchiveOnClose = true,
                MaxActiveTickets = 1
            };

            logger.LogInformation("Attempting to insert SelectMenuOption with SelectMenuId: {SelectMenuId}",
                firstOption.SelectMenuId);

            firstOption.Id = await ctx.InsertWithInt32IdentityAsync(firstOption);

            logger.LogInformation("Created SelectMenuOption with ID: {OptionId}", firstOption.Id);

            var completeMenu = await ctx.PanelSelectMenus
                .LoadWithAsTable(m => m.SelectMenuOptions)
                .FirstOrDefaultAsync(m => m.Id == menu.Id);

            if (completeMenu == null)
            {
                logger.LogError("Failed to load complete menu after creation");
                throw new InvalidOperationException("Failed to load created menu");
            }

            if (updateComponents)
                await UpdatePanelComponentsAsync(panel);

            return completeMenu;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to add select menu to panel {PanelId}", panel.Id);

            try
            {
                if (ex.Data.Contains("MenuId"))
                {
                    var menuId = (int)ex.Data["MenuId"];
                    await ctx.SelectMenuOptions.Where(o => o.SelectMenuId == menuId).DeleteAsync();
                    await ctx.PanelSelectMenus.Where(m => m.Id == menuId).DeleteAsync();
                }
            }
            catch (Exception cleanupEx)
            {
                logger.LogWarning(cleanupEx, "Failed to cleanup partial menu creation");
            }

            throw;
        }
    }

    /// <summary>
    ///     Updates a select menu's properties
    /// </summary>
    /// <param name="menu">The menu to update</param>
    /// <param name="updateAction">Action containing the updates to apply</param>
    public async Task UpdateSelectMenuAsync(PanelSelectMenu menu, Action<PanelSelectMenu> updateAction)
    {
        await using var ctx = await dbFactory.CreateConnectionAsync();

        updateAction(menu);

        await ctx.PanelSelectMenus
            .Where(m => m.Id == menu.Id)
            .Set(m => m.Placeholder, menu.Placeholder)
            .Set(m => m.CustomId, menu.CustomId)
            .UpdateAsync();

        menu.Panel ??= await ctx.TicketPanels
            .FirstOrDefaultAsync(p => p.Id == menu.PanelId);

        await UpdatePanelComponentsAsync(menu.Panel);
    }

    /// <summary>
    ///     Adds an option to a select menu
    /// </summary>
    /// <param name="menu">The menu to add the option to</param>
    /// <param name="label">The option label</param>
    /// <param name="value">The option value</param>
    /// <param name="description">Optional description for the option</param>
    /// <param name="emoji">Optional emoji for the option</param>
    /// <param name="openMessageJson">Optional JSON for ticket opening message</param>
    /// <param name="modalJson">Optional JSON for ticket creation modal</param>
    /// <param name="channelFormat">Format for ticket channel names</param>
    /// <param name="categoryId">Optional category for ticket channels</param>
    /// <param name="archiveCategoryId">Optional category for archived tickets</param>
    /// <param name="supportRoles">List of support role IDs</param>
    /// <param name="viewerRoles">List of viewer role IDs</param>
    /// <param name="autoCloseTime">Optional auto-close duration</param>
    /// <param name="requiredResponseTime">Optional required response time</param>
    /// <param name="maxActiveTickets">Maximum active tickets per user</param>
    /// <param name="allowedPriorities">List of allowed priority IDs</param>
    /// <param name="defaultPriority">Optional default priority</param>
    /// <param name="updateComponents">Whether to update the panel components after adding the option</param>
    /// <returns>The created select menu option</returns>
    public async Task<SelectMenuOption> AddSelectOptionAsync(
        PanelSelectMenu menu,
        string label,
        string value,
        string description = null,
        string emoji = null,
        string openMessageJson = null,
        string modalJson = null,
        string channelFormat = "ticket-{username}-{id}",
        ulong? categoryId = null,
        ulong? archiveCategoryId = null,
        List<ulong> supportRoles = null,
        List<ulong> viewerRoles = null,
        TimeSpan? autoCloseTime = null,
        TimeSpan? requiredResponseTime = null,
        int maxActiveTickets = 1,
        List<string> allowedPriorities = null,
        string defaultPriority = null, bool updateComponents = true)
    {
        try
        {
            await using var ctx = await dbFactory.CreateConnectionAsync();

            // Check option limit (max 25 per select menu)
            var currentOptionCount = await ctx.SelectMenuOptions.Where(o => o.SelectMenuId == menu.Id).CountAsync();
            if (currentOptionCount >= 25)
            {
                logger.LogError(
                    "Cannot add option to select menu {MenuId}: already has 25 options (Discord limit)",
                    menu.Id);
                return null;
            }

            var option = new SelectMenuOption
            {
                SelectMenuId = menu.Id,
                Label = label,
                Value = value,
                Description = description,
                Emoji = emoji,
                OpenMessageJson = openMessageJson,
                ModalJson = modalJson,
                ChannelNameFormat = channelFormat,
                CategoryId = categoryId,
                ArchiveCategoryId = archiveCategoryId,
                SupportRoles = supportRoles?.ToArray() ?? [],
                ViewerRoles = viewerRoles?.ToArray() ?? [],
                AutoCloseTime = autoCloseTime,
                RequiredResponseTime = requiredResponseTime,
                MaxActiveTickets = maxActiveTickets,
                AllowedPriorities = allowedPriorities?.ToArray() ?? [],
                DefaultPriority = defaultPriority,
                SaveTranscript = true,
                RemoveCreatorOnArchive = true,
                RemoveCreatorOnClose = true,
                LockOnArchive = true,
                RenameOnClose = true
            };

            logger.LogInformation("About to save option: Label='{Label}', Value='{Value}', SelectMenuId={SelectMenuId}",
                option.Label, option.Value, option.SelectMenuId);
            option.Id = await ctx.InsertWithInt32IdentityAsync(option);
            var savedOption = await ctx.SelectMenuOptions.FirstOrDefaultAsync(o => o.Id == option.Id);
            logger.LogInformation("Retrieved saved option: ID={Id}, Label='{Label}', Value='{Value}'",
                savedOption?.Id, savedOption?.Label ?? "NULL", savedOption?.Value ?? "NULL");


            menu.Panel ??= await ctx.TicketPanels
                .FirstOrDefaultAsync(p => p.Id == menu.PanelId);
            if (updateComponents)
                await UpdatePanelComponentsAsync(menu.Panel);

            return option;
        }
        catch (Exception e)
        {
            logger.LogInformation(e, "ERRRRROR");
            return null;
        }
    }

    /// <summary>
    ///     Retrieves a select menu by its custom ID.
    /// </summary>
    /// <param name="menuId">The custom ID of the menu to retrieve.</param>
    /// <returns>The select menu matching the specified ID, if found.</returns>
    public async Task<PanelSelectMenu?> GetSelectMenuAsync(string menuId)
    {
        var parsedInt = int.TryParse(menuId, out var potentialInt);
        await using var ctx = await dbFactory.CreateConnectionAsync();
        var menu = await ctx.PanelSelectMenus
            .LoadWithAsTable(m => m.Panel)
            .LoadWithAsTable(m => m.SelectMenuOptions)
            .FirstOrDefaultAsync(m => m.CustomId == menuId);

        if (menu is not null) return menu;
        {
            if (!parsedInt)
                return null;
            menu = await ctx.PanelSelectMenus.LoadWithAsTable(m => m.Panel).LoadWithAsTable(m => m.SelectMenuOptions)
                .FindAsync(potentialInt) ?? await ctx.PanelSelectMenus.LoadWithAsTable(m => m.Panel)
                .LoadWithAsTable(m => m.SelectMenuOptions)
                .FirstOrDefaultAsync(m => m.PanelId == potentialInt);
        }

        return menu;
    }

    /// <summary>
    ///     Retrieves a select menu option by its value.
    /// </summary>
    /// <param name="menuId">The ID of the menu containing the option.</param>
    /// <param name="value">The value of the option to retrieve.</param>
    /// <returns>The select menu option matching the specified value, if found.</returns>
    public async Task<SelectMenuOption?> GetSelectOptionAsync(int menuId, string value)
    {
        await using var ctx = await dbFactory.CreateConnectionAsync();
        return await ctx.SelectMenuOptions
            .LoadWithAsTable(o => o.SelectMenu)
            .FirstOrDefaultAsync(o => o.SelectMenuId == menuId && o.Value == value);
    }

    private async Task<string> GenerateTranscriptAsync(ITextChannel channel, Ticket ticket)
    {
        var messages = await channel.GetMessagesAsync(5000).FlattenAsync();
        var messagesList = messages.Reverse().ToList();

        // Get all roles in the guild once
        var guildRoles = channel.Guild.Roles.ToDictionary(r => r.Id, r => r);

        // Create a cache for user colors to avoid recalculating for the same user
        var userColorCache = new Dictionary<ulong, string>();

        // Helper function to get user color
        string GetUserColor(IGuildUser user)
        {
            if (userColorCache.TryGetValue(user.Id, out var cachedColor))
                return cachedColor;

            var highestRole = user.RoleIds
                .Select(roleId => guildRoles.GetValueOrDefault(roleId))
                .Where(role => role != null)
                .OrderByDescending(role => role.Position)
                .FirstOrDefault();

            var colorHex = highestRole?.Color.RawValue is uint color ? $"#{color:X6}" : "#7289da";
            userColorCache[user.Id] = colorHex;
            return colorHex;
        }

        var html = new StringBuilder();
        // [Previous HTML header code remains the same]

        foreach (var msg in messagesList)
        {
            var guildUser = msg.Author as IGuildUser;
            var colorHex = guildUser != null ? GetUserColor(guildUser) : "#7289da";

            html.AppendLine($"""
                             <div class='message'>
                                         <div class='message-info'>
                                             <img class='avatar' src='{msg.Author.GetAvatarUrl() ?? msg.Author.GetDefaultAvatarUrl()}' />
                                             <span class='username' style='color: {colorHex}'>{msg.Author.Username}</span>
                                             <span class='timestamp'>{msg.Timestamp.ToString("f")}</span>
                                         </div>
                                         <div class='content'>
                             """);

            // [Rest of message formatting remains the same]
        }

        html.AppendLine("</div></body></html>");
        return html.ToString();
    }


    /// <summary>
    ///     Closes a ticket
    /// </summary>
    /// <param name="guild">The guild containing the ticket.</param>
    /// <param name="channelId">The ID of the ticket channel to close.</param>
    /// <param name="forceArchive">Force archive regardless of configuration</param>
    /// <returns>True if the ticket was successfully closed, false otherwise.</returns>
    public async Task<bool> CloseTicket(IGuild guild, ulong channelId, bool forceArchive = false)
    {
        await using var ctx = await dbFactory.CreateConnectionAsync();
        var ticket = await ctx.Tickets
            .LoadWithAsTable(t => t.Button)
            .LoadWithAsTable(t => t.SelectOption)
            .FirstOrDefaultAsync(t => t.ChannelId == channelId && t.GuildId == guild.Id);

        if (ticket is not { ClosedAt: null })
            return false;

        try
        {
            ticket.ClosedAt = DateTime.UtcNow;
            ticket.LastActivityAt = DateTime.UtcNow;
            await ctx.UpdateAsync(ticket);

            var channel = await guild.GetTextChannelAsync(channelId);
            if (channel != null)
            {
                // Send closure message
                var embed = new EmbedBuilder()
                    .WithTitle(strings.TicketClosed(guild.Id))
                    .WithDescription(strings.TicketClosedDesc(guild.Id))
                    .WithColor(Color.Red)
                    .WithCurrentTimestamp()
                    .Build();

                await channel.SendMessageAsync(embed: embed);

                // Generate transcript if enabled
                if (ticket.Button?.SaveTranscript == true || ticket.SelectOption?.SaveTranscript == true)
                {
                    await GenerateAndSaveTranscriptAsync(guild, channel, ticket);
                }

                // Check if we should auto-archive
                var shouldAutoArchive = forceArchive ||
                                        ticket.Button?.AutoArchiveOnClose == true ||
                                        ticket.SelectOption?.AutoArchiveOnClose == true;

                if (shouldAutoArchive)
                {
                    // Auto-archive the ticket (skip transcript since we already generated it above)
                    var transcriptGenerated = ticket.Button?.SaveTranscript == true ||
                                              ticket.SelectOption?.SaveTranscript == true;
                    await ArchiveTicketAsync(ticket, transcriptGenerated);

                    var archiveEmbed = new EmbedBuilder()
                        .WithDescription(strings.TicketAutoArchived(guild.Id))
                        .WithColor(Color.LightGrey)
                        .Build();

                    await channel.SendMessageAsync(embed: archiveEmbed);
                }
                else
                {
                    // Just handle normal close cleanup
                    await HandleChannelCleanupAsync(guild, channel, ticket);
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error closing ticket {TicketId}", ticket.Id);
            return false;
        }
    }

    /// <summary>
    ///     Cleans up channel stuffs for tickets.
    /// </summary>
    /// <param name="guild">The guild containing the ticket</param>
    /// <param name="channel">The ticket channel</param>
    /// <param name="ticket">The ticket being closed</param>
    private async Task HandleChannelCleanupAsync(IGuild guild, ITextChannel channel, Ticket ticket)
    {
        try
        {
            await using var ctx = await dbFactory.CreateConnectionAsync();
            var settings = await ctx.GuildTicketSettings.FirstOrDefaultAsync(s => s.GuildId == guild.Id);

            // Get configuration from button/option or fall back to guild defaults
            var deleteOnClose = ticket.Button?.DeleteOnClose ??
                                ticket.SelectOption?.DeleteOnClose ?? settings?.DeleteTicketsOnClose ?? false;
            var lockOnClose = ticket.Button?.LockOnClose ??
                              ticket.SelectOption?.LockOnClose ?? settings?.LockTicketsOnClose ?? true;
            var renameOnClose = ticket.Button?.RenameOnClose ??
                                ticket.SelectOption?.RenameOnClose ?? settings?.RenameTicketsOnClose ?? true;
            var removeCreatorOnClose = ticket.Button?.RemoveCreatorOnClose ??
                                       ticket.SelectOption?.RemoveCreatorOnClose ??
                                       settings?.RemoveCreatorOnClose ?? true;
            var deleteDelay = ticket.Button?.DeleteDelay ??
                              ticket.SelectOption?.DeleteDelay ?? settings?.DeleteDelay ?? TimeSpan.FromMinutes(5);

            // 1. Rename channel if enabled
            if (renameOnClose && !channel.Name.StartsWith("closed-"))
            {
                try
                {
                    var newName = $"closed-{channel.Name}";
                    await channel.ModifyAsync(c => c.Name = newName);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to rename closed ticket channel {ChannelId}", channel.Id);
                }
            }

            // 2. Remove creator permissions if enabled
            if (removeCreatorOnClose)
            {
                try
                {
                    var creator = await guild.GetUserAsync(ticket.CreatorId);
                    if (creator != null)
                    {
                        await channel.RemovePermissionOverwriteAsync(creator);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to remove creator permissions from ticket {TicketId}", ticket.Id);
                }
            }

            // 3. Lock channel if enabled (and not deleting)
            if (lockOnClose && !deleteOnClose)
            {
                try
                {
                    await channel.AddPermissionOverwriteAsync(guild.EveryoneRole,
                        new OverwritePermissions(
                            viewChannel: PermValue.Deny,
                            sendMessages: PermValue.Deny,
                            addReactions: PermValue.Deny,
                            useSlashCommands: PermValue.Deny
                        ));

                    // Keep staff access
                    var supportRoles = ticket.Button?.SupportRoles ?? ticket.SelectOption?.SupportRoles ?? [];
                    foreach (var roleId in supportRoles)
                    {
                        var role = guild.GetRole(roleId);
                        if (role != null)
                        {
                            await channel.AddPermissionOverwriteAsync(role,
                                new OverwritePermissions(
                                    viewChannel: PermValue.Allow,
                                    sendMessages: PermValue.Allow,
                                    readMessageHistory: PermValue.Allow
                                ));
                        }
                    }

                    var lockEmbed = new EmbedBuilder()
                        .WithDescription(strings.TicketLocked(guild.Id))
                        .WithColor(Color.Orange)
                        .Build();

                    await channel.SendMessageAsync(embed: lockEmbed);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to lock ticket channel {ChannelId}", channel.Id);
                }
            }

            switch (deleteOnClose)
            {
                // 4. Move to archive category (if not deleting and not auto-archiving)
                case false:
                {
                    var archiveCategoryId = ticket.Button?.ArchiveCategoryId ?? ticket.SelectOption?.ArchiveCategoryId;
                    if (archiveCategoryId.HasValue)
                    {
                        try
                        {
                            await channel.ModifyAsync(c => c.CategoryId = archiveCategoryId.Value);
                        }
                        catch (Exception ex)
                        {
                            logger.LogWarning(ex, "Failed to move ticket to archive category");
                        }
                    }

                    break;
                }
                // 5. Schedule deletion if enabled
                case true:
                {
                    var deleteEmbed = new EmbedBuilder()
                        .WithTitle(strings.TicketScheduledDeletion(guild.Id))
                        .WithDescription(
                            strings.TicketDeletionWarning(guild.Id, deleteDelay.TotalMinutes))
                        .WithColor(Color.Red)
                        .Build();

                    await channel.SendMessageAsync(embed: deleteEmbed);

                    // Schedule deletion using the new ScheduledTicketDeletions table
                    await ScheduleTicketDeletionAsync(ticket, deleteDelay);
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during channel cleanup for ticket {TicketId}", ticket.Id);
        }
    }

    private async Task ScheduleTicketDeletionAsync(Ticket ticket, TimeSpan delay)
    {
        try
        {
            await using var ctx = await dbFactory.CreateConnectionAsync();

            var scheduledDeletion = new ScheduledTicketDeletion
            {
                TicketId = ticket.Id,
                GuildId = ticket.GuildId,
                ChannelId = ticket.ChannelId,
                ScheduledAt = DateTime.UtcNow,
                ExecuteAt = DateTime.UtcNow.Add(delay),
                IsProcessed = false,
                RetryCount = 0
            };

            await ctx.InsertAsync(scheduledDeletion);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to schedule ticket deletion for ticket {TicketId}", ticket.Id);
        }
    }

    /// <summary>
    ///     Execute Order 66
    /// </summary>
    public async Task ProcessScheduledDeletionsAsync()
    {
        await using var ctx = await dbFactory.CreateConnectionAsync();

        var pendingDeletions = await ctx.ScheduledTicketDeletions
            .Where(d => !d.IsProcessed && d.ExecuteAt <= DateTime.UtcNow)
            .OrderBy(d => d.ExecuteAt)
            .Take(50) // Process in batches
            .ToListAsync();

        foreach (var deletion in pendingDeletions)
        {
            try
            {
                IGuild guild = client.GetGuild(deletion.GuildId);
                var channel = await guild?.GetChannelAsync(deletion.ChannelId);

                if (channel != null)
                {
                    await channel.DeleteAsync();
                    logger.LogInformation("Deleted scheduled ticket channel {ChannelId}", deletion.ChannelId);
                }

                // Mark ticket as deleted
                await ctx.Tickets
                    .Where(t => t.Id == deletion.TicketId)
                    .Set(t => t.IsDeleted, true)
                    .UpdateAsync();

                // Mark deletion as processed
                await ctx.ScheduledTicketDeletions
                    .Where(d => d.Id == deletion.Id)
                    .Set(d => d.IsProcessed, true)
                    .Set(d => d.ProcessedAt, DateTime.UtcNow)
                    .UpdateAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to process scheduled deletion {DeletionId}", deletion.Id);

                // Mark as failed and increment retry count
                await ctx.ScheduledTicketDeletions
                    .Where(d => d.Id == deletion.Id)
                    .Set(d => d.FailureReason, ex.Message)
                    .Set(d => d.RetryCount, d => d.RetryCount + 1)
                    .UpdateAsync();
            }
        }
    }

    /// <summary>
    ///     Generates and saves transcript for a closed ticket using existing settings
    /// </summary>
    /// <param name="guild">The guild containing the ticket</param>
    /// <param name="channel">The ticket channel</param>
    /// <param name="ticket">The ticket being closed</param>
    private async Task GenerateAndSaveTranscriptAsync(IGuild guild, ITextChannel channel, Ticket ticket)
    {
        try
        {
            await using var ctx = await dbFactory.CreateConnectionAsync();
            var settings = await ctx.GuildTicketSettings.FirstOrDefaultAsync(s => s.GuildId == guild.Id);

            if (settings?.TranscriptChannelId == null) return;

            var transcriptChannel = await guild.GetTextChannelAsync(settings.TranscriptChannelId.Value);
            if (transcriptChannel == null) return;

            // Fetch messages from the ticket channel
            var messages = await channel.GetMessagesAsync(5000).FlattenAsync();
            var messagesList = messages.Reverse().ToList();

            // Convert Discord messages to ChatLogMessageDto format
            var chatLogMessages = messagesList.Select(msg => new ChatLogMessageDto
            {
                Id = msg.Id.ToString(),
                Content = msg.Content ?? string.Empty,
                Author = new ChatLogAuthorDto
                {
                    Id = msg.Author.Id.ToString(),
                    Username = msg.Author.Username,
                    AvatarUrl = msg.Author.GetAvatarUrl() ?? msg.Author.GetDefaultAvatarUrl()
                },
                Timestamp = msg.Timestamp.UtcDateTime.ToString("O"),
                Attachments = msg.Attachments.Select(att => new ChatLogAttachmentDto
                {
                    Url = att.Url, ProxyUrl = att.ProxyUrl ?? att.Url, Filename = att.Filename, FileSize = att.Size
                }).ToList(),
                Embeds = msg.Embeds.Select(embed => new ChatLogEmbedDto
                {
                    Type = embed.Type.ToString(),
                    Title = embed.Title,
                    Description = embed.Description,
                    Url = embed.Url,
                    Thumbnail = embed.Thumbnail?.Url,
                    Author = embed.Author.HasValue
                        ? new ChatLogEmbedAuthorDto
                        {
                            Name = embed.Author.Value.Name, IconUrl = embed.Author.Value.IconUrl
                        }
                        : null,
                    Fields = embed.Fields.Select(field => new ChatLogEmbedFieldDto
                    {
                        Name = field.Name, Value = field.Value, Inline = field.Inline
                    }).ToList()
                }).ToList()
            }).ToList();

            // Save transcript using ChatLogService
            var chatLogId = await chatLogService.SaveTicketTranscriptAsync(
                guild.Id,
                channel.Id,
                channel.Name,
                ticket.Id,
                ticket.CreatorId,
                chatLogMessages
            );

            // Create encoded share code with chatLogId, guildId, and instance port (similar to forms system)
            var shareData = $"{chatLogId}_{guild.Id}_{credentials.ApiPort}";
            var shareCodeBytes = Encoding.UTF8.GetBytes(shareData);
            var shareCode = Convert.ToBase64String(shareCodeBytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');

            // Store full dashboard URL with encoded share code
            var dashboardUrl = credentials.DashboardUrl?.TrimEnd('/') ?? "https://mewdeko.tech";
            ticket.TranscriptUrl = $"{dashboardUrl}/dashboard/tickets/transcript/{shareCode}";
            await ctx.UpdateAsync(ticket);

            // Post notification embed with link to dashboard view
            var transcriptEmbed = new EmbedBuilder()
                .WithTitle(strings.TicketTranscriptTitle(guild.Id, ticket.Id))
                .WithDescription(
                    strings.TicketTranscriptCreator(guild.Id, ticket.CreatorId) +
                    strings.TicketTranscriptType(guild.Id,
                        ticket.Button?.Label ?? ticket.SelectOption?.Label ?? "Unknown") +
                    $"**Created:** {TimestampTag.FromDateTime(ticket.CreatedAt)}\n" +
                    $"**Closed:** {TimestampTag.FromDateTime(ticket.ClosedAt ?? DateTime.UtcNow)}\n\n" +
                    $"[View Transcript]({ticket.TranscriptUrl})")
                .AddField("Channel", channel.Name, true)
                .AddField("Category", (await guild.GetCategoryChannelAsync(channel.CategoryId ?? 0))?.Name ?? "None",
                    true)
                .AddField("Messages", chatLogMessages.Count.ToString(), true)
                .WithColor(Mewdeko.OkColor)
                .WithCurrentTimestamp()
                .Build();

            await transcriptChannel.SendMessageAsync(embed: transcriptEmbed);

            logger.LogInformation("Saved transcript for ticket {TicketId} as ChatLog {ChatLogId}", ticket.Id,
                chatLogId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to generate transcript for ticket {TicketId}", ticket.Id);
        }
    }

    /// <summary>
    ///     Claims a ticket for a staff member.
    /// </summary>
    /// <param name="guild">The guild containing the ticket.</param>
    /// <param name="channelId">The ID of the ticket channel to claim.</param>
    /// <param name="staff">The staff member claiming the ticket.</param>
    /// <returns>True if the ticket was successfully claimed, false otherwise.</returns>
    public async Task<bool> ClaimTicket(IGuild guild, ulong channelId, IGuildUser staff)
    {
        await using var ctx = await dbFactory.CreateConnectionAsync();
        var ticket = await ctx.Tickets
            .LoadWithAsTable(t => t.Button)
            .LoadWithAsTable(t => t.SelectOption)
            .FirstOrDefaultAsync(t => t.ChannelId == channelId && t.GuildId == guild.Id);

        if (ticket is not { ClosedAt: null } || ticket.ClaimedBy.HasValue)
            return false;

        // Verify staff member has permission to claim using existing SupportRoles
        var supportRoles = ticket.Button?.SupportRoles ?? ticket.SelectOption?.SupportRoles ?? [];
        var hasPermission = supportRoles.Any(roleId => staff.RoleIds.Contains(roleId));

        if (!hasPermission && !staff.GuildPermissions.Administrator)
            return false;

        try
        {
            ticket.ClaimedBy = staff.Id;
            ticket.LastActivityAt = DateTime.UtcNow;
            await ctx.UpdateAsync(ticket);

            var channel = await guild.GetTextChannelAsync(channelId);
            if (channel != null)
            {
                var embed = new EmbedBuilder()
                    .WithTitle(strings.TicketClaimedTitle(guild.Id))
                    .WithDescription(strings.TicketClaimedBy(guild.Id, staff.Mention))
                    .WithColor(Color.Green)
                    .WithCurrentTimestamp()
                    .Build();

                await channel.SendMessageAsync(embed: embed);

                // Send DM notification to ticket creator if enabled (using existing EnableDmNotifications)
                var settings = await ctx.GuildTicketSettings
                    .FirstOrDefaultAsync(s => s.GuildId == guild.Id);

                if (settings?.EnableDmNotifications == true)
                {
                    try
                    {
                        var creator = await guild.GetUserAsync(ticket.CreatorId);
                        if (creator != null)
                        {
                            var dmEmbed = new EmbedBuilder()
                                .WithTitle(strings.TicketClaimedDm(guild.Id))
                                .WithDescription(strings.TicketClaimedDmDesc(guild.Id, staff))
                                .WithColor(Color.Green)
                                .WithCurrentTimestamp()
                                .Build();

                            await creator.SendMessageAsync(embed: dmEmbed);
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Failed to send DM notification to ticket creator");
                    }
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error claiming ticket {TicketId}", ticket.Id);
            return false;
        }
    }

    /// <summary>
    ///     Unclaims a ticket, removing the staff member's claim.
    /// </summary>
    /// <param name="guild">The guild containing the ticket.</param>
    /// <param name="channelId">The ID of the ticket channel to unclaim.</param>
    /// <param name="staff">The staff member unclaiming the ticket.</param>
    /// <returns>True if the ticket was successfully unclaimed, false otherwise.</returns>
    public async Task<bool> UnclaimTicket(IGuild guild, ulong channelId, IGuildUser staff)
    {
        await using var ctx = await dbFactory.CreateConnectionAsync();
        var ticket = await ctx.Tickets
            .FirstOrDefaultAsync(t => t.ChannelId == channelId && t.GuildId == guild.Id);

        if (ticket is not { ClosedAt: null } || !ticket.ClaimedBy.HasValue)
            return false;

        // Only allow the claimer or admins to unclaim
        if (ticket.ClaimedBy != staff.Id && !staff.GuildPermissions.Administrator)
            return false;

        try
        {
            var previousClaimer = ticket.ClaimedBy.Value;
            ticket.ClaimedBy = null;
            ticket.LastActivityAt = DateTime.UtcNow;
            await ctx.UpdateAsync(ticket);

            var channel = await guild.GetTextChannelAsync(channelId);
            if (channel != null)
            {
                var embed = new EmbedBuilder()
                    .WithTitle(strings.TicketUnclaimed(guild.Id))
                    .WithDescription(strings.TicketUnclaimedBy(guild.Id, staff.Mention))
                    .WithColor(Color.Orange)
                    .WithCurrentTimestamp()
                    .Build();

                await channel.SendMessageAsync(embed: embed);

                // Notify previous claimer if enabled (using existing EnableDmNotifications)
                var settings = await ctx.GuildTicketSettings
                    .FirstOrDefaultAsync(s => s.GuildId == guild.Id);

                if (settings?.EnableDmNotifications == true)
                {
                    try
                    {
                        var previousUser = await guild.GetUserAsync(previousClaimer);
                        if (previousUser != null)
                        {
                            var dmEmbed = new EmbedBuilder()
                                .WithTitle(strings.TicketUnclaimed(guild.Id))
                                .WithDescription(strings.YourClaimOnTicketRemoved(ticket.GuildId, ticket.Id, staff))
                                .WithColor(Color.Orange)
                                .WithCurrentTimestamp()
                                .Build();

                            await previousUser.SendMessageAsync(embed: dmEmbed);
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Failed to send DM notification for ticket unclaim");
                    }
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error unclaiming ticket {TicketId}", ticket.Id);
            return false;
        }
    }

    /// <summary>
    ///     Adds a note to a ticket
    /// </summary>
    /// <param name="channelId">The ID of the ticket channel</param>
    /// <param name="author">The staff member adding the note</param>
    /// <param name="content">The content of the note</param>
    /// <returns>True if the note was successfully added, false otherwise</returns>
    public async Task<bool> AddNote(ulong channelId, IGuildUser author, string content)
    {
        await using var ctx = await dbFactory.CreateConnectionAsync();

        var ticket = await ctx.Tickets
            .FirstOrDefaultAsync(t => t.ChannelId == channelId && t.GuildId == author.GuildId);

        if (ticket is not { ClosedAt: null })
            return false;

        try
        {
            await using var transaction = await ctx.BeginTransactionAsync();
            try
            {
                var note = new TicketNote
                {
                    TicketId = ticket.Id, AuthorId = author.Id, Content = content, CreatedAt = DateTime.UtcNow
                };

                // Insert the note
                note.Id = await ctx.InsertWithInt32IdentityAsync(note);

                // Update ticket's last activity
                await ctx.Tickets
                    .Where(t => t.Id == ticket.Id)
                    .Set(t => t.LastActivityAt, DateTime.UtcNow)
                    .UpdateAsync();

                await transaction.CommitAsync();

                // Send notification message
                var channel = await author.Guild.GetTextChannelAsync(channelId);
                if (channel != null)
                {
                    var embed = new EmbedBuilder()
                        .WithTitle(strings.NoteAdded(author.Guild.Id))
                        .WithDescription(content)
                        .WithFooter(strings.NoteAddedFooter(author.GuildId, author))
                        .WithColor(Color.Blue)
                        .WithCurrentTimestamp()
                        .Build();

                    await channel.SendMessageAsync(embed: embed);
                }

                return true;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error adding note to ticket {TicketId}", ticket.Id);
            return false;
        }
    }

    /// <summary>
    ///     Edits an existing ticket note
    /// </summary>
    /// <param name="noteId">The ID of the note to edit</param>
    /// <param name="author">The staff member editing the note</param>
    /// <param name="newContent">The new content for the note</param>
    /// <returns>True if the note was successfully edited, false otherwise</returns>
    public async Task<bool> EditNote(int noteId, IGuildUser author, string newContent)
    {
        await using var ctx = await dbFactory.CreateConnectionAsync();

        // Load the note
        var note = await ctx.TicketNotes
            .FirstOrDefaultAsync(n => n.Id == noteId);

        if (note == null)
            return false;

        // Load the associated ticket
        var ticket = await ctx.Tickets
            .FirstOrDefaultAsync(t => t.Id == note.TicketId);

        if (ticket is not { ClosedAt: null })
            return false;

        // Only allow the original author or admins to edit
        if (note.AuthorId != author.Id && !author.GuildPermissions.Administrator)
            return false;

        try
        {
            await using var transaction = await ctx.BeginTransactionAsync();
            try
            {
                var edit = new NoteEdit
                {
                    CaseNoteId = noteId,
                    OldContent = note.Content,
                    NewContent = newContent,
                    EditorId = author.Id,
                    EditedAt = DateTime.UtcNow
                };

                // Insert the edit history record
                edit.Id = await ctx.InsertWithInt32IdentityAsync(edit);

                // Update the note content
                await ctx.TicketNotes
                    .Where(n => n.Id == noteId)
                    .Set(n => n.Content, newContent)
                    .UpdateAsync();

                // Update ticket's last activity
                await ctx.Tickets
                    .Where(t => t.Id == ticket.Id)
                    .Set(t => t.LastActivityAt, DateTime.UtcNow)
                    .UpdateAsync();

                await transaction.CommitAsync();

                // Send notification message
                var channel = await author.Guild.GetTextChannelAsync(ticket.ChannelId);
                if (channel != null)
                {
                    var embed = new EmbedBuilder()
                        .WithTitle(strings.NoteEditedTitle(author.Guild.Id))
                        .WithDescription(strings.NoteEditedDesc(author.Guild.Id, edit.OldContent, newContent))
                        .WithFooter(strings.NoteEditedFooter(author.Guild.Id, author))
                        .WithColor(Color.Blue)
                        .WithCurrentTimestamp()
                        .Build();

                    await channel.SendMessageAsync(embed: embed);
                }

                return true;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error editing note {NoteId}", noteId);
            return false;
        }
    }

    /// <summary>
    ///     Deletes a ticket note.
    /// </summary>
    /// <param name="noteId">The ID of the note to delete.</param>
    /// <param name="author">The staff member deleting the note.</param>
    /// <returns>True if the note was successfully deleted, false otherwise.</returns>
    public async Task<bool> DeleteNote(int noteId, IGuildUser author)
    {
        await using var ctx = await dbFactory.CreateConnectionAsync();
        var note = await ctx.TicketNotes
            .LoadWithAsTable(n => n.Ticket)
            .FirstOrDefaultAsync(n => n.Id == noteId);

        if (note == null || note.Ticket.ClosedAt.HasValue)
            return false;

        // Only allow the original author or admins to delete
        if (note.AuthorId != author.Id && !author.GuildPermissions.Administrator)
            return false;

        try
        {
            await ctx.DeleteAsync(note);
            note.Ticket.LastActivityAt = DateTime.UtcNow;

            var channel = await author.Guild.GetTextChannelAsync(note.Ticket.ChannelId);
            if (channel != null)
            {
                var embed = new EmbedBuilder()
                    .WithTitle(strings.NoteDeletedTitle(author.Guild.Id))
                    .WithDescription(strings.NoteDeletedDesc(author.Guild.Id, note.AuthorId, author.Mention))
                    .WithColor(Color.Red)
                    .WithCurrentTimestamp()
                    .Build();

                await channel.SendMessageAsync(embed: embed);
            }


            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting note {NoteId}", noteId);
            return false;
        }
    }

    /// <summary>
    ///     Creates a new ticket case.
    /// </summary>
    /// <param name="guild">The guild where the case will be created.</param>
    /// <param name="creator">The user creating the case.</param>
    /// <param name="name">The name or title of the case.</param>
    /// <param name="description">The description of the case.</param>
    /// <returns>The created case.</returns>
    public async Task<TicketCase> CreateCase(IGuild guild, IGuildUser creator, string name, string description)
    {
        await using var ctx = await dbFactory.CreateConnectionAsync();
        var ticketCase = new TicketCase
        {
            GuildId = guild.Id,
            Title = name,
            Description = description,
            CreatedBy = creator.Id,
            CreatedAt = DateTime.UtcNow
        };

        await ctx.InsertWithInt32IdentityAsync(ticketCase);


        // Log case creation if logging is enabled
        var settings = await ctx.GuildTicketSettings.FirstOrDefaultAsync(s => s.GuildId == guild.Id);
        if (settings?.LogChannelId == null) return ticketCase;
        var logChannel = await guild.GetTextChannelAsync(settings.LogChannelId.Value);
        if (logChannel == null) return ticketCase;
        var embed = new EmbedBuilder()
            .WithTitle(strings.CaseCreatedTitle(guild.Id))
            .WithDescription(strings.CaseCreatedBy(guild.Id, ticketCase.Id, creator.Mention))
            .AddField("Title", name)
            .AddField("Description", description)
            .WithColor(Color.Green)
            .WithCurrentTimestamp()
            .Build();

        await logChannel.SendMessageAsync(embed: embed);

        return ticketCase;
    }

    /// <summary>
    ///     Links a ticket to an existing case.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="caseId">The ID of the case.</param>
    /// <param name="ticketId">The ID of the ticket to link.</param>
    /// <returns>True if the ticket was successfully linked, false otherwise.</returns>
    public async Task<bool> AddTicketToCase(ulong guildId, int caseId, int ticketId)
    {
        await using var ctx = await dbFactory.CreateConnectionAsync();
        var ticketCase = await ctx.TicketCases
            .LoadWithAsTable(c => c.Tickets)
            .FirstOrDefaultAsync(c => c.Id == caseId && c.GuildId == guildId);

        if (ticketCase == null)
            return false;

        var ticket = await ctx.Tickets.FindAsync(ticketId);
        if (ticket == null || ticket.GuildId != guildId)
            return false;

        try
        {
            ticket.CaseId = caseId;
            ticket.Case = ticketCase;

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error linking ticket {TicketId} to case {CaseId}", ticketId, caseId);
            return false;
        }
    }

    /// <summary>
    ///     Removes a ticket from its associated case.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="ticketId">The ID of the ticket to unlink.</param>
    /// <returns>True if the ticket was successfully unlinked, false otherwise.</returns>
    public async Task<bool> RemoveTicketFromCase(ulong guildId, int ticketId)
    {
        await using var ctx = await dbFactory.CreateConnectionAsync();
        var ticket = await ctx.Tickets
            .LoadWithAsTable(t => t.Case)
            .FirstOrDefaultAsync(t => t.Id == ticketId && t.GuildId == guildId);

        if (ticket == null || ticket.CaseId == null)
            return false;

        try
        {
            ticket.CaseId = null;
            ticket.Case = null;

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error unlinking ticket {TicketId} from case", ticketId);
            return false;
        }
    }

    /// <summary>
    ///     Gets statistics about tickets in a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <returns>Statistics about the guild's tickets.</returns>
    public async Task<GuildStatistics> GetGuildStatistics(ulong guildId)
    {
        await using var ctx = await dbFactory.CreateConnectionAsync();
        var tickets = await ctx.Tickets
            .LoadWithAsTable(t => t.Button)
            .LoadWithAsTable(t => t.SelectOption)
            .Where(t => t.GuildId == guildId)
            .ToListAsync();

        var stats = new GuildStatistics
        {
            TotalTickets = tickets.Count,
            OpenTickets = tickets.Count(t => !t.ClosedAt.HasValue),
            ClosedTickets = tickets.Count(t => t.ClosedAt.HasValue),
            TicketsByType = new Dictionary<string, int>(),
            TicketsByPriority = new Dictionary<string, int>()
        };

        // Calculate average response times
        var responseTimeMinutes = 0.0;
        var responseCount = 0;
        foreach (var ticket in tickets.Where(t => t.ClaimedBy.HasValue))
        {
            var firstMessage = await ctx.TicketNotes
                .Where(n => n.TicketId == ticket.Id)
                .OrderBy(n => n.CreatedAt)
                .FirstOrDefaultAsync();

            if (firstMessage != null)
            {
                responseTimeMinutes += (firstMessage.CreatedAt - ticket.CreatedAt).TotalMinutes;
                responseCount++;
            }
        }

        stats.AverageResponseTime = responseCount > 0 ? responseTimeMinutes / responseCount : 0;

        // Calculate average resolution time
        var resolutionTimeHours = 0.0;
        var resolutionCount = 0;
        foreach (var ticket in tickets.Where(t => t.ClosedAt.HasValue))
        {
            resolutionTimeHours += (ticket.ClosedAt.Value - ticket.CreatedAt).TotalHours;
            resolutionCount++;
        }

        stats.AverageResolutionTime = resolutionCount > 0 ? resolutionTimeHours / resolutionCount : 0;

        // Group by type
        foreach (var ticket in tickets)
        {
            string type;
            if (ticket.Button != null)
                type = ticket.Button.Label;
            else if (ticket.SelectOption != null)
                type = ticket.SelectOption.Label;
            else
                type = "Unknown";

            stats.TicketsByType.TryAdd(type, 0);
            stats.TicketsByType[type]++;

            if (!string.IsNullOrEmpty(ticket.Priority))
            {
                stats.TicketsByPriority.TryAdd(ticket.Priority, 0);
                stats.TicketsByPriority[ticket.Priority]++;
            }
        }

        return stats;
    }

    /// <summary>
    ///     Gets statistics about a user's tickets in a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="userId">The ID of the user.</param>
    /// <returns>Statistics about the user's tickets.</returns>
    public async Task<UserStatistics> GetUserStatistics(ulong guildId, ulong userId)
    {
        await using var ctx = await dbFactory.CreateConnectionAsync();
        var tickets = await ctx.Tickets
            .LoadWithAsTable(t => t.Button)
            .LoadWithAsTable(t => t.SelectOption)
            .Where(t => t.GuildId == guildId && t.CreatorId == userId)
            .ToListAsync();

        var stats = new UserStatistics
        {
            TotalTickets = tickets.Count,
            OpenTickets = tickets.Count(t => !t.ClosedAt.HasValue),
            ClosedTickets = tickets.Count(t => t.ClosedAt.HasValue),
            TicketsByType = new Dictionary<string, int>(),
            RecentTickets = []
        };

        // Group by type
        foreach (var ticket in tickets)
        {
            string type;
            if (ticket.Button != null)
                type = ticket.Button.Label;
            else if (ticket.SelectOption != null)
                type = ticket.SelectOption.Label;
            else
                type = "Unknown";

            stats.TicketsByType.TryAdd(type, 0);
            stats.TicketsByType[type]++;
        }

        // Get recent tickets
        stats.RecentTickets = tickets
            .OrderByDescending(t => t.CreatedAt)
            .Take(5)
            .Select(t => new UserTicketInfo
            {
                TicketId = t.Id,
                Type = t.Button?.Label ?? t.SelectOption?.Label ?? "Unknown",
                CreatedAt = t.CreatedAt,
                ClosedAt = t.ClosedAt
            })
            .ToList();

        return stats;
    }

    /// <summary>
    ///     Gets a summary of ticket activity over time.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="days">The number of days to include in the summary.</param>
    /// <returns>Dictionary mapping dates to ticket counts.</returns>
    public async Task<Dictionary<DateTime, int>> GetTicketActivitySummary(ulong guildId, int days)
    {
        await using var ctx = await dbFactory.CreateConnectionAsync();
        var startDate = DateTime.UtcNow.Date.AddDays(-days);

        var tickets = await ctx.Tickets
            .Where(t => t.GuildId == guildId && t.CreatedAt >= startDate)
            .ToListAsync();

        var summary = new Dictionary<DateTime, int>();
        for (var date = startDate; date <= DateTime.UtcNow.Date; date = date.AddDays(1))
        {
            summary[date] = tickets.Count(t => t.CreatedAt.Date == date);
        }

        return summary;
    }

    /// <summary>
    ///     Gets response time metrics for staff members.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <returns>Dictionary mapping staff IDs to their average response times in minutes.</returns>
    public async Task<Dictionary<ulong, double>> GetStaffResponseMetrics(ulong guildId)
    {
        await using var ctx = await dbFactory.CreateConnectionAsync();
        var tickets = await ctx.Tickets
            .LoadWithAsTable(t => t.TicketNotes)
            .Where(t => t.GuildId == guildId && t.ClaimedBy.HasValue)
            .ToListAsync();

        var metrics = new Dictionary<ulong, (double totalMinutes, int count)>();

        foreach (var ticket in tickets)
        {
            if (!ticket.ClaimedBy.HasValue) continue;

            var firstResponse = ticket.TicketNotes
                .Where(n => n.AuthorId == ticket.ClaimedBy.Value)
                .OrderBy(n => n.CreatedAt)
                .FirstOrDefault();

            if (firstResponse == null) continue;
            var responseTime = (firstResponse.CreatedAt - ticket.CreatedAt).TotalMinutes;
            if (!metrics.ContainsKey(ticket.ClaimedBy.Value))
                metrics[ticket.ClaimedBy.Value] = (0, 0);

            var current = metrics[ticket.ClaimedBy.Value];
            metrics[ticket.ClaimedBy.Value] = (current.totalMinutes + responseTime, current.count + 1);
        }

        return metrics.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.count > 0 ? kvp.Value.totalMinutes / kvp.Value.count : 0
        );
    }

    /// <summary>
    ///     Creates a new ticket priority level.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="id">The unique identifier for the priority.</param>
    /// <param name="name">The display name of the priority.</param>
    /// <param name="emoji">The emoji associated with the priority.</param>
    /// <param name="level">The priority level (1-5).</param>
    /// <param name="pingStaff">Whether to ping staff for tickets with this priority.</param>
    /// <param name="responseTime">The required response time for this priority level.</param>
    /// <param name="color">The color associated with this priority.</param>
    /// <returns>True if the priority was successfully created, false otherwise.</returns>
    public async Task<bool> CreatePriority(ulong guildId, string id, string name, string emoji, int level,
        bool pingStaff, TimeSpan responseTime, Color color)
    {
        if (level is < 1 or > 5)
            throw new ArgumentException("Priority level must be between 1 and 5", nameof(level));

        await using var ctx = await dbFactory.CreateConnectionAsync();

        // Check for existing priority
        if (await ctx.TicketPriorities.AnyAsync(p => p.GuildId == guildId && p.PriorityId == id))
            return false;

        try
        {
            var priority = new TicketPriority
            {
                GuildId = guildId,
                PriorityId = id,
                Name = name,
                Emoji = emoji,
                Level = level,
                PingStaff = pingStaff,
                ResponseTime = responseTime,
                Color = color
            };

            await ctx.InsertWithInt32IdentityAsync(priority);


            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating priority {PriorityId} for guild {GuildId}", id, guildId);
            return false;
        }
    }

    /// <summary>
    ///     Deletes a ticket priority level.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="id">The unique identifier of the priority to delete.</param>
    /// <returns>True if the priority was successfully deleted, false otherwise.</returns>
    public async Task<bool> DeletePriority(ulong guildId, string id)
    {
        await using var ctx = await dbFactory.CreateConnectionAsync();
        var priority = await ctx.TicketPriorities
            .FirstOrDefaultAsync(p => p.GuildId == guildId && p.PriorityId == id);

        if (priority == null)
            return false;

        try
        {
            // Clear priority from tickets using it
            var tickets = await ctx.Tickets
                .Where(t => t.GuildId == guildId && t.Priority == id)
                .ToListAsync();

            foreach (var ticket in tickets)
            {
                ticket.Priority = null;
            }

            await ctx.DeleteAsync(priority);


            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting priority {PriorityId} for guild {GuildId}", id, guildId);
            return false;
        }
    }

    /// <summary>
    ///     Sets the priority of a ticket.
    /// </summary>
    /// <param name="guild">The guild containing the ticket.</param>
    /// <param name="channelId">The ID of the ticket channel.</param>
    /// <param name="priorityId">The ID of the priority to set.</param>
    /// <param name="staff">The staff member setting the priority.</param>
    /// <returns>True if the priority was successfully set, false otherwise.</returns>
    public async Task<bool> SetTicketPriority(IGuild guild, ulong channelId, string priorityId, IGuildUser staff)
    {
        await using var ctx = await dbFactory.CreateConnectionAsync();
        var ticket = await ctx.Tickets
            .LoadWithAsTable(t => t.Button)
            .LoadWithAsTable(t => t.SelectOption)
            .FirstOrDefaultAsync(t => t.ChannelId == channelId && t.GuildId == guild.Id);

        if (ticket is not { ClosedAt: null })
            return false;

        var priority = await ctx.TicketPriorities
            .FirstOrDefaultAsync(p => p.GuildId == guild.Id && p.PriorityId == priorityId);

        if (priority == null)
            return false;

        // Validate allowed priorities
        var allowedPriorities = ticket.Button?.AllowedPriorities ?? ticket.SelectOption?.AllowedPriorities;
        if (allowedPriorities?.Any() == true && !allowedPriorities.Contains(priorityId))
            return false;

        try
        {
            ticket.Priority = priorityId;
            ticket.LastActivityAt = DateTime.UtcNow;

            var channel = await guild.GetTextChannelAsync(channelId);
            if (channel != null)
            {
                var embed = new EmbedBuilder()
                    .WithTitle(strings.TicketPriorityUpdatedTitle(guild.Id))
                    .WithDescription(strings.TicketPriorityUpdatedDetailed(guild.Id, priority.Emoji, priority.Name,
                        staff.Mention))
                    .WithColor(new Color((uint)priority.Color))
                    .WithCurrentTimestamp()
                    .Build();

                await channel.SendMessageAsync(embed: embed);

                // Ping staff if enabled
                if (priority.PingStaff)
                {
                    var supportRoles = ticket.Button?.SupportRoles ??
                                       ticket.SelectOption?.SupportRoles ?? [];
                    if (supportRoles.Any())
                    {
                        var mentions = string.Join(" ", supportRoles.Select(r => $"<@&{r}>"));
                        await channel.SendMessageAsync(
                            strings.TicketPriorityMention(guild.Id, mentions, priority.Name));
                    }
                }
            }


            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error setting priority for ticket {TicketId}", ticket.Id);
            return false;
        }
    }

    /// <summary>
    ///     Creates a new ticket tag.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="id">The unique identifier for the tag.</param>
    /// <param name="name">The display name of the tag.</param>
    /// <param name="description">The description of the tag.</param>
    /// <param name="color">The color associated with the tag.</param>
    /// <returns>True if the tag was successfully created, false otherwise.</returns>
    public async Task<bool> CreateTag(ulong guildId, string id, string name, string description, Color color)
    {
        await using var ctx = await dbFactory.CreateConnectionAsync();

        if (await ctx.TicketTags.AnyAsync(t => t.GuildId == guildId && t.TagId == id))
            return false;

        try
        {
            var tag = new TicketTag
            {
                GuildId = guildId,
                TagId = id,
                Name = name,
                Description = description,
                Color = color
            };

            await ctx.InsertWithInt32IdentityAsync(tag);


            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating tag {TagId} for guild {GuildId}", id, guildId);
            return false;
        }
    }

    /// <summary>
    ///     Deletes a ticket tag.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="id">The unique identifier of the tag to delete.</param>
    /// <returns>True if the tag was successfully deleted, false otherwise.</returns>
    public async Task<bool> DeleteTag(ulong guildId, string id)
    {
        await using var ctx = await dbFactory.CreateConnectionAsync();
        var tag = await ctx.TicketTags
            .FirstOrDefaultAsync(t => t.GuildId == guildId && t.TagId == id);

        if (tag == null)
            return false;

        try
        {
            // Remove tag from all tickets
            var tickets = await ctx.Tickets
                .Where(t => t.GuildId == guildId && t.Tags.Contains(id))
                .ToListAsync();

            foreach (var ticket in tickets)
            {
                ((IList)ticket.Tags).Remove(id);
            }

            await ctx.DeleteAsync(tag);


            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting tag {TagId} for guild {GuildId}", id, guildId);
            return false;
        }
    }

    /// <summary>
    ///     Adds tags to a ticket.
    /// </summary>
    /// <param name="guild">The guild containing the ticket.</param>
    /// <param name="channelId">The ID of the ticket channel.</param>
    /// <param name="tagIds">The IDs of the tags to add.</param>
    /// <param name="staff">The staff member adding the tags.</param>
    /// <returns>True if the tags were successfully added, false otherwise.</returns>
    public async Task<bool> AddTicketTags(IGuild guild, ulong channelId, IEnumerable<string> tagIds, IGuildUser staff)
    {
        await using var ctx = await dbFactory.CreateConnectionAsync();
        var ticket = await ctx.Tickets
            .FirstOrDefaultAsync(t => t.ChannelId == channelId && t.GuildId == guild.Id);

        if (ticket is not { ClosedAt: null })
            return false;

        try
        {
            var tags = await ctx.TicketTags
                .Where(t => t.GuildId == guild.Id && tagIds.Contains(t.TagId))
                .ToListAsync();

            if (!tags.Any())
                return false;

            var removedTags = new List<TicketTag>();

            foreach (var tag in tags)
            {
                if (ticket.Tags.Contains(tag.TagId))
                {
                    ((IList)ticket.Tags).Remove(tag.TagId);
                    removedTags.Add(tag);
                }
            }

            if (removedTags.Any())
            {
                ticket.LastActivityAt = DateTime.UtcNow;

                var channel = await guild.GetTextChannelAsync(channelId);
                if (channel != null)
                {
                    var embed = new EmbedBuilder()
                        .WithTitle(strings.TagsRemoved(guild.Id))
                        .WithDescription(strings.TagsRemovedBy(guild.Id, staff.Mention) +
                                         string.Join("\n", removedTags.Select(t => Format.Italics(t.Name))))
                        .WithColor(Color.Orange)
                        .WithCurrentTimestamp()
                        .Build();

                    await channel.SendMessageAsync(embed: embed);
                }
            }


            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error removing tags from ticket {TicketId}", ticket.Id);
            return false;
        }
    }

    /// <summary>
    ///     Gets all available priorities in a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <returns>A list of all priorities in the guild.</returns>
    public async Task<List<TicketPriority>> GetGuildPriorities(ulong guildId)
    {
        await using var ctx = await dbFactory.CreateConnectionAsync();
        return await ctx.TicketPriorities
            .Where(p => p.GuildId == guildId)
            .OrderBy(p => p.Level)
            .ToListAsync();
    }

    /// <summary>
    ///     Gets all available tags in a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <returns>A list of all tags in the guild.</returns>
    public async Task<List<TicketTag>> GetGuildTags(ulong guildId)
    {
        await using var ctx = await dbFactory.CreateConnectionAsync();

        return await ctx.TicketTags
            .Where(t => t.GuildId == guildId)
            .OrderBy(t => t.Name)
            .ToListAsync();
    }

    /// <summary>
    ///     Removes tags from a ticket.
    /// </summary>
    /// <param name="guild">The guild containing the ticket.</param>
    /// <param name="channelId">The ID of the ticket channel.</param>
    /// <param name="tagIds">The IDs of the tags to remove.</param>
    /// <param name="staff">The staff member removing the tags.</param>
    /// <returns>True if the tags were successfully removed, false otherwise.</returns>
    public async Task<bool> RemoveTicketTags(IGuild guild, ulong channelId, IEnumerable<string> tagIds,
        IGuildUser staff)
    {
        await using var ctx = await dbFactory.CreateConnectionAsync();
        var ticket = await ctx.Tickets
            .FirstOrDefaultAsync(t => t.ChannelId == channelId && t.GuildId == guild.Id);

        if (ticket is not { ClosedAt: null } || ticket.Tags == null)
            return false;

        try
        {
            var validTagIds = tagIds.Where(id => ticket.Tags.Contains(id)).ToList();
            if (!validTagIds.Any())
                return false;

            var tags = await ctx.TicketTags
                .Where(t => t.GuildId == guild.Id && validTagIds.Contains(t.TagId))
                .ToListAsync();

            foreach (var tagId in validTagIds)
            {
                ((IList)ticket.Tags).Remove(tagId);
            }

            ticket.LastActivityAt = DateTime.UtcNow;

            var channel = await guild.GetTextChannelAsync(channelId);
            if (channel != null)
            {
                var embed = new EmbedBuilder()
                    .WithTitle(strings.TagsRemoved(guild.Id))
                    .WithDescription(strings.TagsRemovedBy(guild.Id, staff.Mention) +
                                     string.Join("\n", tags.Select(t => $"• **{t.Name}**")))
                    .WithColor(Color.Orange)
                    .WithCurrentTimestamp()
                    .Build();

                await channel.SendMessageAsync(embed: embed);
            }


            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error removing tags from ticket {TicketId}", ticket.Id);
            return false;
        }
    }

    /// <summary>
    ///     Blacklists a user from creating tickets in the guild.
    /// </summary>
    /// <param name="guild">The guild where the user should be blacklisted.</param>
    /// <param name="userId">The ID of the user to blacklist.</param>
    /// <param name="reason">The optional reason for the blacklist.</param>
    /// <returns>True if the user was successfully blacklisted, false if they were already blacklisted.</returns>
    public async Task<bool> BlacklistUser(IGuild guild, ulong userId, string reason = null)
    {
        await using var ctx = await dbFactory.CreateConnectionAsync();
        var settings = await ctx.GuildTicketSettings
            .FirstOrDefaultAsync(s => s.GuildId == guild.Id);

        if (settings == null)
        {
            settings = new GuildTicketSetting
            {
                GuildId = guild.Id, BlacklistedUsers = []
            };
            await ctx.InsertAsync(settings);
        }

        if (settings.BlacklistedUsers.Contains(userId))
            return false;

        try
        {
            ((IList)settings.BlacklistedUsers).Add(userId);

            // Persist the change
            await ctx.UpdateAsync(settings);

            // Log the blacklist if logging is enabled
            if (settings.LogChannelId.HasValue)
            {
                var logChannel = await guild.GetTextChannelAsync(settings.LogChannelId.Value);
                if (logChannel != null)
                {
                    var embed = new EmbedBuilder()
                        .WithTitle(strings.UserBlacklistedTitle(guild.Id))
                        .WithDescription(strings.UserBlacklistedDesc(guild.Id, userId))
                        .AddField("Reason", reason ?? "No reason provided")
                        .WithColor(Color.Red)
                        .WithCurrentTimestamp()
                        .Build();

                    await logChannel.SendMessageAsync(embed: embed);
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error blacklisting user {UserId} in guild {GuildId}", userId, guild.Id);
            return false;
        }
    }

    /// <summary>
    ///     Removes a user from the ticket blacklist.
    /// </summary>
    /// <param name="guild">The guild object.</param>
    /// <param name="userId">The ID of the user to unblacklist.</param>
    /// <returns>True if the user was successfully unblacklisted, false if they weren't blacklisted.</returns>
    public async Task<bool> UnblacklistUser(IGuild guild, ulong userId)
    {
        await using var ctx = await dbFactory.CreateConnectionAsync();
        var settings = await ctx.GuildTicketSettings
            .FirstOrDefaultAsync(s => s.GuildId == guild.Id);

        if (settings?.BlacklistedUsers == null || !settings.BlacklistedUsers.Contains(userId))
            return false;

        try
        {
            ((IList)settings.BlacklistedUsers).Remove(userId);

            // Persist the change
            await ctx.UpdateAsync(settings);

            // Log the unblacklist if logging is enabled
            if (settings.LogChannelId.HasValue)
            {
                var logChannel = await guild.GetTextChannelAsync(settings.LogChannelId.Value);
                if (logChannel != null)
                {
                    var embed = new EmbedBuilder()
                        .WithTitle(strings.UserUnblacklistedTitle(guild.Id))
                        .WithDescription(strings.UserUnblacklistedFromTickets(guild.Id, userId))
                        .WithColor(Color.Green)
                        .WithCurrentTimestamp()
                        .Build();

                    await logChannel.SendMessageAsync(embed: embed);
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error unblacklisting user {UserId} in guild {GuildId}", userId, guild.Id);
            return false;
        }
    }

    /// <summary>
    ///     Gets a list of all blacklisted users in a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <returns>A list of blacklisted user IDs and their blacklisted ticket types.</returns>
    public async Task<Dictionary<ulong, List<string>>> GetBlacklistedUsers(ulong guildId)
    {
        await using var ctx = await dbFactory.CreateConnectionAsync();
        var settings = await ctx.GuildTicketSettings
            .FirstOrDefaultAsync(s => s.GuildId == guildId);

        if (settings == null || settings.BlacklistedUsers == null)
            return new Dictionary<ulong, List<string>>();

        var result = new Dictionary<ulong, List<string>>();

        // Add globally blacklisted users
        foreach (var userId in settings.BlacklistedUsers)
        {
            result[userId] = [];
        }

        return result;
    }

    /// <summary>
    ///     Batch closes inactive tickets.
    /// </summary>
    /// <param name="guild">The guild containing the tickets.</param>
    /// <param name="inactiveTime">The duration of inactivity required for closure.</param>
    /// <returns>A tuple containing the number of tickets closed and failed attempts.</returns>
    public async Task<(int closed, int failed)> BatchCloseInactiveTickets(IGuild guild, TimeSpan inactiveTime)
    {
        int closed = 0, failed = 0;
        await using var ctx = await dbFactory.CreateConnectionAsync();

        var cutoffTime = DateTime.UtcNow - inactiveTime;
        var inactiveTickets = await ctx.Tickets
            .LoadWithAsTable(t => t.Button)
            .LoadWithAsTable(t => t.SelectOption)
            .Where(t => t.GuildId == guild.Id
                        && !t.ClosedAt.HasValue
                        && t.LastActivityAt <= cutoffTime)
            .ToListAsync();

        foreach (var ticket in inactiveTickets)
        {
            try
            {
                var channel = await guild.GetTextChannelAsync(ticket.ChannelId);
                if (channel != null)
                {
                    var embed = new EmbedBuilder()
                        .WithTitle(strings.TicketAutoClosedTitle(guild.Id))
                        .WithDescription(
                            strings.TicketAutoClosedInactivity(guild.Id, inactiveTime.TotalHours))
                        .WithColor(Color.Red)
                        .WithCurrentTimestamp()
                        .Build();

                    await channel.SendMessageAsync(embed: embed);
                }

                ticket.ClosedAt = DateTime.UtcNow;

                closed++;

                // Archive if configured
                if (ticket.Button?.ArchiveCategoryId != null || ticket.SelectOption?.ArchiveCategoryId != null)
                {
                    await ArchiveTicketAsync(ticket);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to close inactive ticket {TicketId}", ticket.Id);
                failed++;
            }
        }

        return (closed, failed);
    }

    /// <summary>
    ///     Moves all tickets from one category to another.
    /// </summary>
    /// <param name="guild">The guild containing the tickets.</param>
    /// <param name="sourceCategoryId">The source category ID.</param>
    /// <param name="targetCategoryId">The target category ID.</param>
    /// <returns>A tuple containing the number of tickets moved and failed attempts.</returns>
    public async Task<(int moved, int failed)> BatchMoveTickets(IGuild guild, ulong sourceCategoryId,
        ulong targetCategoryId)
    {
        int moved = 0, failed = 0;
        await using var ctx = await dbFactory.CreateConnectionAsync();

        var sourceChannels = await guild.GetTextChannelsAsync();
        sourceChannels = (IReadOnlyCollection<ITextChannel>)sourceChannels.Where(x => x.CategoryId == sourceCategoryId);
        var targetCategory = await guild.GetCategoryChannelAsync(targetCategoryId);

        if (!sourceChannels.Any() || targetCategory == null)
            throw new InvalidOperationException("Source or target category not found.");

        // Get all tickets in the database for this guild to check custom names
        var guildTickets = await ctx.Tickets
            .Where(t => t.GuildId == guild.Id)
            .ToListAsync();

        var ticketChannels = sourceChannels
            .Where(c => c.Name.StartsWith("ticket-") ||
                        guildTickets.Any(t => t.ChannelId == c.Id)); // Check both default and custom names

        foreach (var channel in ticketChannels)
        {
            try
            {
                await channel.ModifyAsync(c => c.CategoryId = targetCategoryId);

                // Update the ticket's last activity if it exists
                var ticket = guildTickets.FirstOrDefault(t => t.ChannelId == channel.Id);
                if (ticket != null)
                {
                    ticket.LastActivityAt = DateTime.UtcNow;
                }

                moved++;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to move ticket channel {ChannelId}", channel.Id);
                failed++;
            }
        }

        if (moved > 0)
        {
        }

        return (moved, failed);
    }

    /// <summary>
    ///     Adds a role to all active tickets.
    /// </summary>
    /// <param name="guild">The guild containing the tickets.</param>
    /// <param name="role">The role to add.</param>
    /// <param name="viewOnly">Whether the role should have view-only permissions.</param>
    /// <returns>A tuple containing the number of tickets updated and failed attempts.</returns>
    public async Task<(int updated, int failed)> BatchAddRole(IGuild guild, IRole role, bool viewOnly = false)
    {
        int updated = 0, failed = 0;
        await using var ctx = await dbFactory.CreateConnectionAsync();

        var activeTickets = await ctx.Tickets
            .Where(t => t.GuildId == guild.Id && !t.ClosedAt.HasValue)
            .ToListAsync();

        foreach (var ticket in activeTickets)
        {
            try
            {
                var channel = await guild.GetTextChannelAsync(ticket.ChannelId);
                if (channel == null) continue;

                var permissions = new OverwritePermissions(
                    viewChannel: PermValue.Allow,
                    readMessageHistory: PermValue.Allow,
                    sendMessages: viewOnly ? PermValue.Deny : PermValue.Allow,
                    attachFiles: viewOnly ? PermValue.Deny : PermValue.Allow,
                    embedLinks: viewOnly ? PermValue.Deny : PermValue.Allow
                );

                await channel.AddPermissionOverwriteAsync(role, permissions);
                updated++;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to add role to ticket {TicketId}", ticket.Id);
                failed++;
            }
        }

        return (updated, failed);
    }

    /// <summary>
    ///     Transfers all tickets from one staff member to another.
    /// </summary>
    /// <param name="guild">The guild containing the tickets.</param>
    /// <param name="fromStaffId">The ID of the staff member to transfer from.</param>
    /// <param name="toStaffId">The ID of the staff member to transfer to.</param>
    /// <returns>A tuple containing the number of tickets transferred and failed attempts.</returns>
    public async Task<(int transferred, int failed)> BatchTransferTickets(IGuild guild, ulong fromStaffId,
        ulong toStaffId)
    {
        int transferred = 0, failed = 0;
        await using var ctx = await dbFactory.CreateConnectionAsync();

        var claimedTickets = await ctx.Tickets
            .Where(t => t.GuildId == guild.Id &&
                        t.ClaimedBy == fromStaffId &&
                        !t.ClosedAt.HasValue)
            .ToListAsync();

        var toStaff = await guild.GetUserAsync(toStaffId);
        if (toStaff == null)
            throw new InvalidOperationException("Target staff member not found.");

        foreach (var ticket in claimedTickets)
        {
            try
            {
                var channel = await guild.GetTextChannelAsync(ticket.ChannelId);
                if (channel == null) continue;

                ticket.ClaimedBy = toStaffId;
                ticket.LastActivityAt = DateTime.UtcNow;

                var embed = new EmbedBuilder()
                    .WithTitle(strings.TicketTransferredTitle(guild.Id))
                    .WithDescription(strings.TicketTransferredTo(guild.Id, toStaff.Mention))
                    .WithColor(Color.Blue)
                    .WithCurrentTimestamp()
                    .Build();

                await channel.SendMessageAsync(embed: embed);
                transferred++;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to transfer ticket {TicketId}", ticket.Id);
                failed++;
            }
        }


        return (transferred, failed);
    }

    /// <summary>
    ///     Updates an existing panel's embed.
    /// </summary>
    /// <param name="guild">The guild containing the panel.</param>
    /// <param name="panelId">The ID of the panel to update.</param>
    /// <param name="embedJson">The new embed JSON configuration.</param>
    /// <returns>True if the panel was successfully updated, false otherwise.</returns>
    public async Task<bool> UpdatePanelEmbedAsync(IGuild guild, ulong panelId, string embedJson)
    {
        await using var ctx = await dbFactory.CreateConnectionAsync();
        var panel = await ctx.TicketPanels.FindAsync((int)panelId);

        if (panel == null)
        {
            panel = await ctx.TicketPanels.FirstOrDefaultAsync(x => x.GuildId == guild.Id && x.MessageId == panelId);
        }

        if (panel == null || panel.GuildId != guild.Id)
            return false;

        try
        {
            var channel = await guild.GetTextChannelAsync(panel.ChannelId);
            if (channel == null)
                return false;

            var message = await channel.GetMessageAsync(panel.MessageId) as IUserMessage;
            if (message == null)
                return false;

            SmartEmbed.TryParse(embedJson, guild.Id, out var embeds, out var plainText, out _);
            await message.ModifyAsync(m =>
            {
                m.Content = plainText;
                m.Embeds = embeds;
            });

            panel.EmbedJson = embedJson;

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update panel {PanelId}", panelId);
            return false;
        }
    }

    /// <summary>
    ///     Moves a panel to a different channel.
    /// </summary>
    /// <param name="guild">The guild containing the panel.</param>
    /// <param name="panelId">The ID of the panel to move.</param>
    /// <param name="newChannelId">The ID of the channel to move the panel to.</param>
    /// <returns>True if the panel was successfully moved, false otherwise.</returns>
    public async Task<bool> MovePanelAsync(IGuild guild, ulong panelId, ulong newChannelId)
    {
        await using var ctx = await dbFactory.CreateConnectionAsync();
        var panel = await ctx.TicketPanels
            .LoadWithAsTable(p => p.PanelButtons)
            .LoadWithAsTable(p => p.PanelSelectMenus)
            .FirstOrDefaultAsync(p => p.MessageId == panelId);

        if (panel == null || panel.GuildId != guild.Id)
            return false;

        try
        {
            // Delete old message
            var oldChannel = await guild.GetTextChannelAsync(panel.ChannelId);
            if (oldChannel != null)
            {
                try
                {
                    var oldMessage = await oldChannel.GetMessageAsync(panel.MessageId);
                    if (oldMessage != null)
                        await oldMessage.DeleteAsync();
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to delete old panel message");
                }
            }

            // Create new message
            var newChannel = await guild.GetTextChannelAsync(newChannelId);
            if (newChannel == null)
                return false;

            SmartEmbed.TryParse(panel.EmbedJson, guild.Id, out var embeds, out var plainText, out _);
            var components = new ComponentBuilder();

            // Rebuild buttons
            if (panel.PanelButtons?.Any() == true)
            {
                var buttonRow = new ActionRowBuilder();
                foreach (var button in panel.PanelButtons)
                {
                    var btnBuilder = new ButtonBuilder()
                        .WithLabel(button.Label)
                        .WithCustomId(button.CustomId)
                        .WithStyle((ButtonStyle)button.Style);

                    if (!string.IsNullOrEmpty(button.Emoji))
                        btnBuilder.WithEmote(Emote.Parse(button.Emoji));

                    buttonRow.WithButton(btnBuilder);
                }

                components.AddRow(buttonRow);
            }

            // Rebuild select menus
            if (panel.PanelSelectMenus?.Any() == true)
            {
                foreach (var menu in panel.PanelSelectMenus)
                {
                    var selectBuilder = new SelectMenuBuilder()
                        .WithCustomId(menu.CustomId)
                        .WithPlaceholder(menu.Placeholder)
                        .WithMaxValues(1);

                    foreach (var option in menu.SelectMenuOptions)
                    {
                        var optBuilder = new SelectMenuOptionBuilder()
                            .WithLabel(option.Label)
                            .WithValue(option.Value)
                            .WithDescription(option.Description);

                        if (!string.IsNullOrEmpty(option.Emoji))
                            optBuilder.WithEmote(Emote.Parse(option.Emoji));

                        selectBuilder.AddOption(optBuilder);
                    }

                    components.AddRow(new ActionRowBuilder().WithSelectMenu(selectBuilder));
                }
            }

            var message = await newChannel.SendMessageAsync(plainText, embeds: embeds, components: components.Build());

            panel.ChannelId = newChannelId;
            panel.MessageId = message.Id;


            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to move panel {PanelId}", panelId);
            return false;
        }
    }

    /// <summary>
    ///     Duplicates an existing panel to a new channel
    /// </summary>
    /// <param name="guild">The guild containing the panel</param>
    /// <param name="panelId">The ID of the panel to duplicate</param>
    /// <param name="newChannelId">The ID of the channel to create the duplicate in</param>
    /// <returns>The newly created panel, or null if duplication failed</returns>
    public async Task<TicketPanel?> DuplicatePanelAsync(IGuild guild, ulong panelId, ulong newChannelId)
    {
        await using var ctx = await dbFactory.CreateConnectionAsync();

        // Load the source panel
        var sourcePanel = await ctx.TicketPanels
            .FirstOrDefaultAsync(p => p.MessageId == panelId);

        if (sourcePanel == null || sourcePanel.GuildId != guild.Id)
            return null;

        // Load buttons for the source panel
        var sourceButtons = await ctx.PanelButtons
            .Where(b => b.PanelId == sourcePanel.Id)
            .ToListAsync();

        // Load select menus for the source panel
        var sourceMenus = await ctx.PanelSelectMenus
            .Where(m => m.PanelId == sourcePanel.Id)
            .ToListAsync();

        // Load options for all source menus
        var sourceMenuIds = sourceMenus.Select(m => m.Id).ToArray();
        var sourceOptions = sourceMenuIds.Any()
            ? await ctx.SelectMenuOptions
                .Where(o => sourceMenuIds.Contains(o.SelectMenuId))
                .ToListAsync()
            : new List<SelectMenuOption>();

        try
        {
            var newChannel = await guild.GetTextChannelAsync(newChannelId);
            if (newChannel == null)
                return null;

            SmartEmbed.TryParse(sourcePanel.EmbedJson, guild.Id, out var embeds, out var plainText, out _);
            var message = await newChannel.SendMessageAsync(plainText, embeds: embeds);

            await using var transaction = await ctx.BeginTransactionAsync();
            try
            {
                var newPanel = new TicketPanel
                {
                    GuildId = guild.Id,
                    ChannelId = newChannelId,
                    MessageId = message.Id,
                    EmbedJson = sourcePanel.EmbedJson
                };

                // Insert the new panel and get its ID
                newPanel.Id = await ctx.InsertWithInt32IdentityAsync(newPanel);

                // Duplicate buttons
                var newButtons = new List<PanelButton>();
                foreach (var sourceButton in sourceButtons)
                {
                    var newButton = new PanelButton
                    {
                        PanelId = newPanel.Id,
                        Label = sourceButton.Label,
                        Emoji = sourceButton.Emoji,
                        CustomId = $"ticket_btn_{Guid.NewGuid():N}",
                        Style = sourceButton.Style,
                        OpenMessageJson = sourceButton.OpenMessageJson,
                        ModalJson = sourceButton.ModalJson,
                        ChannelNameFormat = sourceButton.ChannelNameFormat,
                        CategoryId = sourceButton.CategoryId,
                        ArchiveCategoryId = sourceButton.ArchiveCategoryId,
                        SupportRoles = [..sourceButton.SupportRoles ?? []],
                        ViewerRoles = [..sourceButton.ViewerRoles ?? []],
                        AutoCloseTime = sourceButton.AutoCloseTime,
                        RequiredResponseTime = sourceButton.RequiredResponseTime,
                        MaxActiveTickets = sourceButton.MaxActiveTickets,
                        AllowedPriorities = [..sourceButton.AllowedPriorities ?? []],
                        DefaultPriority = sourceButton.DefaultPriority,
                        SaveTranscript = sourceButton.SaveTranscript
                    };
                    newButton.Id = await ctx.InsertWithInt32IdentityAsync(newButton);
                    newButtons.Add(newButton);
                }

                // Duplicate select menus
                var newMenus = new List<PanelSelectMenu>();
                foreach (var sourceMenu in sourceMenus)
                {
                    var newMenu = new PanelSelectMenu
                    {
                        PanelId = newPanel.Id,
                        CustomId = $"ticket_select_{Guid.NewGuid():N}",
                        Placeholder = sourceMenu.Placeholder
                    };
                    newMenu.Id = await ctx.InsertWithInt32IdentityAsync(newMenu);

                    // Duplicate menu options
                    var menuSourceOptions = sourceOptions.Where(o => o.SelectMenuId == sourceMenu.Id).ToList();
                    var newOptions = new List<SelectMenuOption>();

                    foreach (var sourceOption in menuSourceOptions)
                    {
                        var newOption = new SelectMenuOption
                        {
                            SelectMenuId = newMenu.Id,
                            Label = sourceOption.Label,
                            Value = $"option_{Guid.NewGuid():N}",
                            Description = sourceOption.Description,
                            Emoji = sourceOption.Emoji,
                            OpenMessageJson = sourceOption.OpenMessageJson,
                            ModalJson = sourceOption.ModalJson,
                            ChannelNameFormat = sourceOption.ChannelNameFormat,
                            CategoryId = sourceOption.CategoryId,
                            ArchiveCategoryId = sourceOption.ArchiveCategoryId,
                            SupportRoles = [..sourceOption.SupportRoles ?? []],
                            ViewerRoles = [..sourceOption.ViewerRoles ?? []],
                            AutoCloseTime = sourceOption.AutoCloseTime,
                            RequiredResponseTime = sourceOption.RequiredResponseTime,
                            MaxActiveTickets = sourceOption.MaxActiveTickets,
                            AllowedPriorities = [..sourceOption.AllowedPriorities ?? []],
                            DefaultPriority = sourceOption.DefaultPriority,
                            SaveTranscript = sourceOption.SaveTranscript
                        };
                        newOption.Id = await ctx.InsertWithInt32IdentityAsync(newOption);
                        newOptions.Add(newOption);
                    }

                    newMenu.SelectMenuOptions = newOptions;
                    newMenus.Add(newMenu);
                }

                // Set up in-memory relationships for UpdatePanelComponentsAsync
                newPanel.PanelButtons = newButtons;
                newPanel.PanelSelectMenus = newMenus;

                await transaction.CommitAsync();

                // Update message with components
                await UpdatePanelComponentsAsync(newPanel);

                return newPanel;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to duplicate panel {PanelId}", panelId);
            return null;
        }
    }

    /// <summary>
    ///     Reorders buttons on a panel
    /// </summary>
    /// <param name="guild">The guild containing the panel</param>
    /// <param name="panelId">The ID of the panel</param>
    /// <param name="buttonOrder">List of button IDs in the desired order</param>
    /// <returns>True if the buttons were successfully reordered, false otherwise</returns>
    public async Task<bool> ReorderPanelButtonsAsync(IGuild guild, ulong panelId, List<int> buttonOrder)
    {
        await using var ctx = await dbFactory.CreateConnectionAsync();

        // Load the panel
        var panel = await ctx.TicketPanels
            .FirstOrDefaultAsync(p => p.MessageId == panelId && p.GuildId == guild.Id);

        if (panel == null)
            return false;

        // Load the panel's buttons
        var panelButtons = await ctx.PanelButtons
            .Where(b => b.PanelId == panel.Id)
            .ToListAsync();

        if (!panelButtons.Any())
            return false;

        // Validate that all buttons exist and are part of this panel
        if (!buttonOrder.All(id => panelButtons.Any(b => b.Id == id)))
            return false;

        try
        {
            await using var transaction = await ctx.BeginTransactionAsync();
            try
            {
                // Delete all existing buttons for this panel
                await ctx.PanelButtons
                    .Where(b => b.PanelId == panel.Id)
                    .DeleteAsync();

                // Re-insert buttons in the new order
                var reorderedButtons = new List<PanelButton>();
                for (var i = 0; i < buttonOrder.Count; i++)
                {
                    var buttonId = buttonOrder[i];
                    var originalButton = panelButtons.First(b => b.Id == buttonId);

                    // Create new button with same data
                    var reorderedButton = new PanelButton
                    {
                        PanelId = panel.Id,
                        Label = originalButton.Label,
                        Emoji = originalButton.Emoji,
                        CustomId = originalButton.CustomId, // Keep the same CustomId
                        Style = originalButton.Style,
                        OpenMessageJson = originalButton.OpenMessageJson,
                        ModalJson = originalButton.ModalJson,
                        ChannelNameFormat = originalButton.ChannelNameFormat,
                        CategoryId = originalButton.CategoryId,
                        ArchiveCategoryId = originalButton.ArchiveCategoryId,
                        SupportRoles = [..originalButton.SupportRoles ?? []],
                        ViewerRoles = [..originalButton.ViewerRoles ?? []],
                        AutoCloseTime = originalButton.AutoCloseTime,
                        RequiredResponseTime = originalButton.RequiredResponseTime,
                        MaxActiveTickets = originalButton.MaxActiveTickets,
                        AllowedPriorities = [..originalButton.AllowedPriorities ?? []],
                        DefaultPriority = originalButton.DefaultPriority,
                        SaveTranscript = originalButton.SaveTranscript
                    };

                    // Insert and get new ID
                    reorderedButton.Id = await ctx.InsertWithInt32IdentityAsync(reorderedButton);
                    reorderedButtons.Add(reorderedButton);
                }

                await transaction.CommitAsync();

                // Update the in-memory collection for UpdatePanelComponentsAsync
                panel.PanelButtons = reorderedButtons;

                await UpdatePanelComponentsAsync(panel);

                return true;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to reorder buttons for panel {PanelId}", panelId);
            return false;
        }
    }


    /// <summary>
    ///     Updates the required response time for all tickets created by a button or select option.
    /// </summary>
    /// <param name="guild">The guild containing the panel.</param>
    /// <param name="buttonId">The ID of the button to update.</param>
    /// <param name="responseTime">The new required response time.</param>
    /// <returns>True if the response time was successfully updated, false otherwise.</returns>
    public async Task<bool> UpdateRequiredResponseTimeAsync(IGuild guild, int buttonId, TimeSpan? responseTime)
    {
        await using var ctx = await dbFactory.CreateConnectionAsync();
        var button = await ctx.PanelButtons
            .LoadWithAsTable(b => b.Panel)
            .FirstOrDefaultAsync(b => b.Id == buttonId && b.Panel.GuildId == guild.Id);

        if (button == null)
            return false;

        try
        {
            button.RequiredResponseTime = responseTime;


            // Notify support roles of the change
            if (button.SupportRoles?.Any() == true)
            {
                var channel = await guild.GetTextChannelAsync(button.Panel.ChannelId);
                if (channel != null)
                {
                    var embed = new EmbedBuilder()
                        .WithTitle(strings.ResponseTimeUpdatedTitle(guild.Id))
                        .WithDescription(
                            strings.ResponseTimeUpdatedDesc(guild.Id, button.Label, responseTime?.TotalHours ?? 0))
                        .WithColor(Color.Blue)
                        .WithCurrentTimestamp()
                        .Build();

                    await channel.SendMessageAsync(
                        string.Join(" ", button.SupportRoles.Select(r => $"<@&{r}>")),
                        embed: embed);
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update response time for button {ButtonId}", buttonId);
            return false;
        }
    }

    /// <summary>
    ///     Updates multiple settings for a panel button in a single operation.
    /// </summary>
    /// <param name="guild">The guild containing the panel.</param>
    /// <param name="buttonId">The ID of the button to update.</param>
    /// <param name="settings">Dictionary of setting names and their new values.</param>
    /// <returns>True if all settings were successfully updated, false if any failed.</returns>
    public async Task<bool> UpdateButtonSettingsAsync(IGuild guild, int buttonId, Dictionary<string, object> settings)
    {
        await using var ctx = await dbFactory.CreateConnectionAsync();
        var button = await ctx.PanelButtons
            .LoadWithAsTable(b => b.Panel)
            .FirstOrDefaultAsync(b => b.Id == buttonId && b.Panel.GuildId == guild.Id);

        if (button == null)
            return false;

        try
        {
            foreach (var setting in settings)
            {
                switch (setting.Key.ToLower())
                {
                    case "label":
                        button.Label = (string)setting.Value;
                        break;
                    case "emoji":
                        button.Emoji = (string)setting.Value;
                        break;
                    case "style":
                        button.Style = (int)setting.Value;
                        break;
                    case "categoryid":
                        button.CategoryId = (ulong?)setting.Value;
                        break;
                    case "archivecategoryid":
                        button.ArchiveCategoryId = (ulong?)setting.Value;
                        break;
                    case "supportroles":
                        button.SupportRoles = (ulong[])setting.Value;
                        break;
                    case "viewerroles":
                        button.ViewerRoles = (ulong[])setting.Value;
                        break;
                    case "autoclosetime":
                        button.AutoCloseTime = (TimeSpan?)setting.Value;
                        break;
                    case "requiredresponsetime":
                        button.RequiredResponseTime = (TimeSpan?)setting.Value;
                        break;
                    case "maxactivetickets":
                        button.MaxActiveTickets = (int)setting.Value;
                        break;
                    case "allowedpriorities":
                        button.AllowedPriorities = (string[])setting.Value;
                        break;
                    case "defaultpriority":
                        button.DefaultPriority = (string)setting.Value;
                        break;
                    case "savetranscript":
                        button.SaveTranscript = (bool)setting.Value;
                        break;
                    case "deleteonclose":
                        button.DeleteOnClose = (bool)setting.Value;
                        break;
                    case "lockonclose":
                        button.LockOnClose = (bool)setting.Value;
                        break;
                    case "renameonclose":
                        button.RenameOnClose = (bool)setting.Value;
                        break;
                    case "removecreatoronclose":
                        button.RemoveCreatorOnClose = (bool)setting.Value;
                        break;
                    case "deletedelay":
                        button.DeleteDelay = (TimeSpan)setting.Value;
                        break;
                    case "lockonarchive":
                        button.LockOnArchive = (bool)setting.Value;
                        break;
                    case "renameonarchive":
                        button.RenameOnArchive = (bool)setting.Value;
                        break;
                    case "removecreatoronarchive":
                        button.RemoveCreatorOnArchive = (bool)setting.Value;
                        break;
                    case "autoarchiveonclose":
                        button.AutoArchiveOnClose = (bool)setting.Value;
                        break;
                    case "modaljson":
                        button.ModalJson = (string)setting.Value;
                        break;
                    case "openmessagejson":
                        button.OpenMessageJson = (string)setting.Value;
                        break;

                    default:
                        logger.LogWarning("Unknown button setting: {SettingKey}", setting.Key);
                        break;
                }
            }

            // Actually save the changes to the database
            await ctx.UpdateAsync(button);

            // Update the panel components in Discord
            await UpdatePanelComponentsAsync(button.Panel);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update settings for button {ButtonId}", buttonId);
            return false;
        }
    }

    /// <summary>
    ///     Updates multiple settings for a select menu option in a single operation.
    /// </summary>
    /// <param name="guild">The guild containing the panel.</param>
    /// <param name="optionId">The ID of the option to update.</param>
    /// <param name="settings">Dictionary of setting names and their new values.</param>
    /// <returns>True if all settings were successfully updated, false if any failed.</returns>
    public async Task<bool> UpdateSelectOptionSettingsAsync(IGuild guild, int optionId,
        Dictionary<string, object> settings)
    {
        await using var ctx = await dbFactory.CreateConnectionAsync();
        var option = await ctx.SelectMenuOptions
            .LoadWithAsTable(o => o.SelectMenu)
            .LoadWithAsTable(o => o.SelectMenu.Panel)
            .FirstOrDefaultAsync(o => o.Id == optionId && o.SelectMenu.Panel.GuildId == guild.Id);

        if (option == null)
            return false;

        try
        {
            foreach (var setting in settings)
            {
                switch (setting.Key.ToLower())
                {
                    case "label":
                        option.Label = (string)setting.Value;
                        break;
                    case "description":
                        option.Description = (string)setting.Value;
                        break;
                    case "emoji":
                        option.Emoji = (string)setting.Value;
                        break;
                    case "categoryid":
                        option.CategoryId = (ulong?)setting.Value;
                        break;
                    case "archivecategoryid":
                        option.ArchiveCategoryId = (ulong?)setting.Value;
                        break;
                    case "supportroles":
                        option.SupportRoles = (ulong[])setting.Value;
                        break;
                    case "viewerroles":
                        option.ViewerRoles = (ulong[])setting.Value;
                        break;
                    case "autoclosetime":
                        option.AutoCloseTime = (TimeSpan?)setting.Value;
                        break;
                    case "requiredresponsetime":
                        option.RequiredResponseTime = (TimeSpan?)setting.Value;
                        break;
                    case "maxactivetickets":
                        option.MaxActiveTickets = (int)setting.Value;
                        break;
                    case "allowedpriorities":
                        option.AllowedPriorities = (string[])setting.Value;
                        break;
                    case "defaultpriority":
                        option.DefaultPriority = (string)setting.Value;
                        break;
                    case "savetranscript":
                        option.SaveTranscript = (bool)setting.Value;
                        break;
                    case "deleteonclose":
                        option.DeleteOnClose = (bool)setting.Value;
                        break;
                    case "lockonclose":
                        option.LockOnClose = (bool)setting.Value;
                        break;
                    case "renameonclose":
                        option.RenameOnClose = (bool)setting.Value;
                        break;
                    case "removecreatoronclose":
                        option.RemoveCreatorOnClose = (bool)setting.Value;
                        break;
                    case "deletedelay":
                        option.DeleteDelay = (TimeSpan)setting.Value;
                        break;
                    case "lockonarchive":
                        option.LockOnArchive = (bool)setting.Value;
                        break;
                    case "renameonarchive":
                        option.RenameOnArchive = (bool)setting.Value;
                        break;
                    case "removecreatoronarchive":
                        option.RemoveCreatorOnArchive = (bool)setting.Value;
                        break;
                    case "autoarchiveonclose":
                        option.AutoArchiveOnClose = (bool)setting.Value;
                        break;
                    case "modaljson":
                        option.ModalJson = (string)setting.Value;
                        break;
                    case "openmessagejson":
                        option.OpenMessageJson = (string)setting.Value;
                        break;

                    default:
                        logger.LogWarning("Unknown select option setting: {SettingKey}", setting.Key);
                        break;
                }
            }

            // Actually save the changes to the database
            await ctx.UpdateAsync(option);

            // Update the panel components in Discord
            await UpdatePanelComponentsAsync(option.SelectMenu.Panel);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update settings for select option {OptionId}", optionId);
            return false;
        }
    }

    /// <summary>
    ///     Deletes a button from a panel
    /// </summary>
    /// <param name="guild">The guild containing the panel</param>
    /// <param name="buttonId">The ID of the button to delete</param>
    /// <returns>True if the button was successfully deleted, false otherwise</returns>
    public async Task<bool> DeleteButtonAsync(IGuild guild, int buttonId)
    {
        await using var ctx = await dbFactory.CreateConnectionAsync();

        try
        {
            // Get the button to verify it exists and belongs to this guild
            var button = await ctx.PanelButtons
                .LoadWithAsTable(b => b.Panel)
                .FirstOrDefaultAsync(b => b.Id == buttonId && b.Panel.GuildId == guild.Id);

            if (button == null)
                return false;

            // Check if any tickets reference this button
            var referencingTickets = await ctx.Tickets
                .Where(t => t.ButtonId == buttonId)
                .ToListAsync();

            if (referencingTickets.Any())
            {
                // Clear the button reference from all tickets
                await ctx.Tickets
                    .Where(t => t.ButtonId == buttonId)
                    .Set(t => t.ButtonId, (int?)null)
                    .UpdateAsync();
            }

            // Delete the button
            await ctx.PanelButtons
                .Where(b => b.Id == buttonId)
                .DeleteAsync();

            // Update the panel in Discord
            await UpdatePanelComponentsAsync(button.Panel);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete button {ButtonId} from guild {GuildId}", buttonId, guild.Id);
            return false;
        }
    }

    /// <summary>
    ///     Represents statistics about tickets in a guild.
    /// </summary>
    public class GuildStatistics
    {
        /// <summary>
        ///     Gets or sets the total number of tickets ever created in the guild.
        /// </summary>
        public int TotalTickets { get; set; }

        /// <summary>
        ///     Gets or sets the number of currently open tickets in the guild.
        /// </summary>
        public int OpenTickets { get; set; }

        /// <summary>
        ///     Gets or sets the number of closed tickets in the guild.
        /// </summary>
        public int ClosedTickets { get; set; }

        /// <summary>
        ///     Gets or sets the average time in minutes between ticket creation and first staff response.
        /// </summary>
        public double AverageResponseTime { get; set; }

        /// <summary>
        ///     Gets or sets the average time in hours between ticket creation and closure.
        /// </summary>
        public double AverageResolutionTime { get; set; }

        /// <summary>
        ///     Gets or sets the distribution of tickets by their type, where the key is the ticket type label
        ///     and the value is the number of tickets of that type.
        /// </summary>
        public Dictionary<string, int> TicketsByType { get; set; }

        /// <summary>
        ///     Gets or sets the distribution of tickets by their priority level, where the key is the priority name
        ///     and the value is the number of tickets with that priority.
        /// </summary>
        public Dictionary<string, int> TicketsByPriority { get; set; }
    }

    /// <summary>
    ///     Represents a user's ticket statistics.
    /// </summary>
    public class UserStatistics
    {
        /// <summary>
        ///     Gets or sets the total number of tickets created by the user.
        /// </summary>
        public int TotalTickets { get; set; }

        /// <summary>
        ///     Gets or sets the number of currently open tickets created by the user.
        /// </summary>
        public int OpenTickets { get; set; }

        /// <summary>
        ///     Gets or sets the number of closed tickets created by the user.
        /// </summary>
        public int ClosedTickets { get; set; }

        /// <summary>
        ///     Gets or sets the distribution of the user's tickets by type, where the key is the ticket type label
        ///     and the value is the number of tickets of that type.
        /// </summary>
        public Dictionary<string, int> TicketsByType { get; set; }

        /// <summary>
        ///     Gets or sets a list of the user's most recent tickets.
        /// </summary>
        public List<UserTicketInfo> RecentTickets { get; set; }
    }

    /// <summary>
    ///     Represents information about a specific ticket for user statistics.
    /// </summary>
    public class UserTicketInfo
    {
        /// <summary>
        ///     Gets or sets the unique identifier of the ticket.
        /// </summary>
        public int TicketId { get; set; }

        /// <summary>
        ///     Gets or sets the type or category of the ticket.
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        ///     Gets or sets the date and time when the ticket was created.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        ///     Gets or sets the optional date and time when the ticket was closed.
        ///     If null, the ticket is still open.
        /// </summary>
        public DateTime? ClosedAt { get; set; }
    }
}