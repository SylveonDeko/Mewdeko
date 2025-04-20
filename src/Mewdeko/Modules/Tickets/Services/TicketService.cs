using System.IO;
using System.Text;
using System.Text.Json;
using Mewdeko.Database.DbContextStuff;
using Mewdeko.Modules.Tickets.Common;
using Microsoft.EntityFrameworkCore;
using Serilog;
using SelectMenuOption = Mewdeko.Database.Models.SelectMenuOption;

namespace Mewdeko.Modules.Tickets.Services;

/// <summary>
///     Service for managing ticket panels, tickets, and cases.
/// </summary>
public class TicketService : INService
{
    private readonly DbContextProvider _db;
    private readonly DiscordShardedClient _client;
    private readonly IDataCache _cache;
    private const string ClaimButtonId = "ticket_claim";
    private const string CloseButtonId = "ticket_close";

    /// <summary>
    ///     Initializes a new instance of the <see cref="TicketService" /> class.
    /// </summary>
    public TicketService(
        DbContextProvider db,
        DiscordShardedClient client,
        GuildSettingsService guildSettings,
        IDataCache cache,
        EventHandler eventHandler)
    {
        _db = db;
        _client = client;
        _cache = cache;

        eventHandler.MessageDeleted += HandleMessageDeleted;
        eventHandler.ModalSubmitted += HandleModalSubmitted;
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
        string embedJson = null,
        string title = "Support Tickets",
        string description = "Click a button below to create a ticket",
        Color? color = null)
    {
        string finalJson;

        if (string.IsNullOrWhiteSpace(embedJson))
        {
            // Create default embed JSON
            finalJson =
                "{\n  \"embeds\": [\n    {\n      \"title\": \"Support Tickets\",\n      \"description\": \"Click a button below to create a ticket\",\n      \"color\": \"#00e584\"\n    }\n  ]\n}";
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
            Buttons = [],
            SelectMenus = []
        };

        await using var ctx = await _db.GetContextAsync();
        ctx.TicketPanels.Add(panel);
        await ctx.SaveChangesAsync();

        return panel;
    }

    /// <summary>
    ///     Previews how an embed JSON would look
    /// </summary>
    public async Task PreviewPanelAsync(ITextChannel channel, string embedJson)
    {
        try
        {
            var replacer = new ReplacementBuilder()
                .WithServer(_client, channel.Guild as SocketGuild)
                .Build();

            var content = replacer.Replace(embedJson);

            if (SmartEmbed.TryParse(content, channel.Guild.Id, out var embedData, out var plainText,
                    out var components))
            {
                await channel.SendMessageAsync(plainText, embeds: embedData, components: components);
            }
            else
            {
                await channel.SendMessageAsync(content);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error previewing panel embed");
            throw;
        }
    }

    /// <summary>
    ///     Gets all panels in a channel
    /// </summary>
    public async Task<List<TicketPanel>> GetPanelsInChannelAsync(ulong guildId, ulong channelId)
    {
        await using var ctx = await _db.GetContextAsync();
        return await ctx.TicketPanels
            .Include(p => p.Buttons)
            .Include(p => p.SelectMenus)
            .Where(p => p.GuildId == guildId && p.ChannelId == channelId)
            .ToListAsync();
    }

    /// <summary>
    ///     Gets all panels in a guild
    /// </summary>
    public async Task<List<TicketPanel>> GetPanelsAsync(ulong guildId)
    {
        await using var ctx = await _db.GetContextAsync();
        return await ctx.TicketPanels
            .Include(p => p.Buttons)
            .Include(p => p.SelectMenus)
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
        await using var ctx = await _db.GetContextAsync();

        var button = new PanelButton
        {
            PanelId = panel.Id,
            Label = label,
            Emoji = emoji,
            CustomId = $"ticket_btn_{Guid.NewGuid():N}",
            Style = style,
            OpenMessageJson = openMessageJson,
            ModalJson = modalJson,
            ChannelNameFormat = channelFormat,
            CategoryId = categoryId,
            ArchiveCategoryId = archiveCategoryId,
            SupportRoles = supportRoles ?? [],
            ViewerRoles = viewerRoles ?? [],
            AutoCloseTime = autoCloseTime,
            RequiredResponseTime = requiredResponseTime,
            MaxActiveTickets = maxActiveTickets,
            AllowedPriorities = allowedPriorities ?? [],
            DefaultPriority = defaultPriority
        };

        ctx.Attach(panel);
        panel.Buttons.Add(button);
        await ctx.SaveChangesAsync();
        await UpdatePanelComponentsAsync(panel);

        return button;
    }

    /// <summary>
    ///     Updates a button's properties.
    /// </summary>
    public async Task UpdateButtonAsync(PanelButton button, Action<PanelButton> updateAction)
    {
        await using var ctx = await _db.GetContextAsync();

        ctx.Attach(button);
        updateAction(button);

        await UpdatePanelComponentsAsync(button.Panel);
        await ctx.SaveChangesAsync();
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
        await using var ctx = await _db.GetContextAsync();
        return await ctx.TicketCases
            .Include(c => c.LinkedTickets)
            .Include(c => c.Notes)
            .FirstOrDefaultAsync(c => c.Id == caseId);
    }

    /// <summary>
    ///     Closes a case and optionally archives linked tickets.
    /// </summary>
    /// <param name="ticketCase">The case to close.</param>
    /// <param name="archiveTickets">Whether to archive linked tickets. Defaults to false.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task CloseCaseAsync(TicketCase ticketCase, bool archiveTickets = false)
    {
        await using var ctx = await _db.GetContextAsync();
        ctx.Attach(ticketCase);
        ticketCase.ClosedAt = DateTime.UtcNow;

        if (archiveTickets && ticketCase.LinkedTickets.Any())
        {
            foreach (var ticket in ticketCase.LinkedTickets)
            {
                if (!ticket.IsArchived && !ticket.ClosedAt.HasValue)
                {
                    ticket.ClosedAt = DateTime.UtcNow;
                    ticket.IsArchived = true;

                    // If the ticket has an archive category set, move it
                    var button = ticket.Button;
                    var option = ticket.SelectOption;
                    var archiveCategoryId = button?.ArchiveCategoryId ?? option?.ArchiveCategoryId;

                    if (archiveCategoryId.HasValue)
                    {
                        var guild = await _client.Rest.GetGuildAsync(ticket.GuildId);
                        if (guild != null)
                        {
                            var channel = await guild.GetTextChannelAsync(ticket.ChannelId);
                            if (channel != null)
                            {
                                await channel.ModifyAsync(props =>
                                    props.CategoryId = archiveCategoryId.Value);
                            }
                        }
                    }
                }
            }
        }

        await ctx.SaveChangesAsync();
    }

    /// <summary>
    ///     Retrieves all cases for a guild, ordered by creation date.
    /// </summary>
    /// <param name="guildId">The ID of the guild to get cases for.</param>
    /// <param name="includeDeleted">Whether to include soft-deleted cases. Defaults to false.</param>
    /// <returns>A task containing the list of cases.</returns>
    public async Task<List<TicketCase>> GetGuildCasesAsync(ulong guildId, bool includeDeleted = false)
    {
        await using var ctx = await _db.GetContextAsync();
        var query = ctx.TicketCases
            .Include(c => c.LinkedTickets)
            .Include(c => c.Notes)
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
        await using var ctx = await _db.GetContextAsync();
        var ticketCase = await ctx.TicketCases.FindAsync(caseId);
        if (ticketCase == null)
            return null;

        var note = new CaseNote
        {
            CaseId = caseId, AuthorId = authorId, Content = content, CreatedAt = DateTime.UtcNow
        };

        ctx.CaseNotes.Add(note);
        await ctx.SaveChangesAsync();
        return note;
    }

    /// <summary>
    ///     Deletes a ticket panel and all its associated components.
    /// </summary>
    /// <param name="panelId">The ID of the panel to delete.</param>
    /// <param name="guild">The guild containing the panel.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the panel is not found.</exception>
    public async Task DeletePanelAsync(ulong panelId, IGuild guild)
    {
        await using var ctx = await _db.GetContextAsync();
        await using var transaction = await ctx.Database.BeginTransactionAsync();

        try
        {
            // First get the panel with its buttons to handle dependencies
            var panel = await ctx.TicketPanels
                .Include(p => p.Buttons)
                .FirstOrDefaultAsync(p => p.MessageId == panelId);

            if (panel == null)
                throw new InvalidOperationException("Panel not found");

            // Try to delete the Discord message if it exists
            try
            {
                var channel = await guild.GetChannelAsync(panel.ChannelId) as ITextChannel;
                if (channel != null)
                {
                    var message = await channel.GetMessageAsync(panel.MessageId);
                    if (message != null)
                        await message.DeleteAsync();
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to delete panel message");
                // Continue with database cleanup
            }

            // Handle tickets that reference the panel's buttons
            if (panel.Buttons?.Any() == true)
            {
                var buttonIds = panel.Buttons.Select(b => b.Id).ToList();

                // Find all tickets referencing these buttons
                var tickets = await ctx.Tickets
                    .Where(t => t.ButtonId.HasValue && buttonIds.Contains(t.ButtonId.Value))
                    .ToListAsync();

                // Update tickets to remove button references
                foreach (var ticket in tickets)
                {
                    ticket.ButtonId = null;
                    ticket.Button = null;
                }

                await ctx.SaveChangesAsync();
            }

            // Handle select menu options that may be referenced by tickets
            try
            {
                var menus = await ctx.PanelSelectMenus
                    .Include(m => m.Options)
                    .Where(m => m.Panel.MessageId == panelId)
                    .ToListAsync();

                if (menus.Any())
                {
                    var optionIds = menus.SelectMany(m => m.Options).Select(o => o.Id).ToList();

                    // Update tickets to remove select option references
                    var tickets = await ctx.Tickets
                        .Where(t => t.SelectOptionId.HasValue && optionIds.Contains(t.SelectOptionId.Value))
                        .ToListAsync();

                    foreach (var ticket in tickets)
                    {
                        ticket.SelectOptionId = null;
                        ticket.SelectOption = null;
                    }

                    await ctx.SaveChangesAsync();

                    // Now safe to remove menus and options
                    ctx.PanelSelectMenus.RemoveRange(menus);
                    await ctx.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to handle select menus - table may not exist");
                // Continue with deletion
            }

            // Finally remove the panel itself
            ctx.TicketPanels.Remove(panel);
            await ctx.SaveChangesAsync();

            await transaction.CommitAsync();
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            Log.Error(ex, "Failed to delete panel {PanelId}", panelId);
            throw new InvalidOperationException($"Failed to delete panel: {ex.Message}", ex);
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

        await using var ctx = await _db.GetContextAsync();
        ctx.Attach(ticket);

        var previousClaimer = ticket.ClaimedBy.Value;
        ticket.ClaimedBy = null;
        ticket.LastActivityAt = DateTime.UtcNow;

        if (await moderator.Guild.GetChannelAsync(ticket.ChannelId) is ITextChannel channel)
        {
            var embed = new EmbedBuilder()
                .WithTitle("Ticket Unclaimed")
                .WithDescription($"This ticket has been unclaimed by {moderator.Mention}")
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
                            .WithTitle("Ticket Unclaimed")
                            .WithDescription($"Your claim on ticket #{ticket.Id} has been removed by {moderator}")
                            .WithColor(Color.Orange)
                            .Build();

                        await previousUser.SendMessageAsync(embed: dmEmbed);
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to send DM notification for ticket unclaim");
                }
            }
        }

        await ctx.SaveChangesAsync();
    }

    /// <summary>
    ///     Gets or creates the default ticket opening message.
    /// </summary>
    /// <param name="ticket">The ticket being opened.</param>
    /// <param name="customMessage">Optional custom message to override the default.</param>
    /// <returns>The configured message content in SmartEmbed format.</returns>
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
    ///     Edits an existing case note.
    /// </summary>
    /// <param name="noteId">The ID of the note to edit.</param>
    /// <param name="editorId">The ID of the user editing the note.</param>
    /// <param name="newContent">The new content for the note.</param>
    /// <returns>A task containing true if the edit was successful, false otherwise.</returns>
    public async Task<bool> EditCaseNoteAsync(int noteId, ulong editorId, string newContent)
    {
        await using var ctx = await _db.GetContextAsync();
        var note = await ctx.CaseNotes.FindAsync(noteId);
        if (note == null)
            return false;

        var oldContent = note.Content;
        note.Content = newContent;

        var edit = new NoteEdit
        {
            OldContent = oldContent, NewContent = newContent, EditorId = editorId, EditedAt = DateTime.UtcNow
        };

        note.EditHistory.Add(edit);
        await ctx.SaveChangesAsync();
        return true;
    }

    /// <summary>
    ///     Deletes a case note.
    /// </summary>
    /// <param name="noteId">The ID of the note to delete.</param>
    /// <returns>A task containing true if the deletion was successful, false otherwise.</returns>
    public async Task<bool> DeleteCaseNoteAsync(int noteId)
    {
        await using var ctx = await _db.GetContextAsync();
        var note = await ctx.CaseNotes.FindAsync(noteId);
        if (note == null)
            return false;

        ctx.CaseNotes.Remove(note);
        await ctx.SaveChangesAsync();
        return true;
    }

    /// <summary>
    ///     Unlinks a collection of tickets from their associated cases.
    /// </summary>
    /// <param name="tickets">The collection of tickets to unlink.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task UnlinkTicketsFromCase(IEnumerable<Ticket> tickets)
    {
        await using var ctx = await _db.GetContextAsync();
        foreach (var ticket in tickets)
        {
            ctx.Attach(ticket);
            ticket.CaseId = null;
            ticket.Case = null;
        }

        await ctx.SaveChangesAsync();
    }

    /// <summary>
    ///     Reopens a previously closed case.
    /// </summary>
    /// <param name="ticketCase">The case to reopen.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task ReopenCaseAsync(TicketCase ticketCase)
    {
        await using var ctx = await _db.GetContextAsync();
        ctx.Attach(ticketCase);
        ticketCase.ClosedAt = null;
        await ctx.SaveChangesAsync();
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
        await using var ctx = await _db.GetContextAsync();
        var ticketCase = await ctx.TicketCases.FindAsync(caseId);
        if (ticketCase != null)
        {
            if (!string.IsNullOrEmpty(title))
                ticketCase.Title = title;
            if (!string.IsNullOrEmpty(description))
                ticketCase.Description = description;
            await ctx.SaveChangesAsync();
        }
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
    public async Task<Ticket> CreateTicketAsync(
        IGuild guild,
        IUser creator,
        PanelButton button = null,
        SelectMenuOption option = null,
        Dictionary<string, string> modalResponses = null)
    {
        await using var ctx = await _db.GetContextAsync();

        // Check if user is blacklisted
        var settings = await ctx.GuildTicketSettings.FirstOrDefaultAsync(s => s.GuildId == guild.Id);
        if (settings?.BlacklistedUsers?.Contains(creator.Id) == true)
        {
            throw new InvalidOperationException("You are blacklisted from creating tickets.");
        }

        var id = button?.Id ?? option.Id;

        // Check if user is blacklisted from this specific ticket type
        if (settings?.BlacklistedTypes?.TryGetValue(creator.Id, out var blacklistedTypes) == true)
        {
            if (blacklistedTypes.Contains(id.ToString()))
            {
                throw new InvalidOperationException("You are blacklisted from creating this type of ticket.");
            }
        }

        // Validate ticket limits
        var maxTickets = button?.MaxActiveTickets ?? option?.MaxActiveTickets ?? settings?.DefaultMaxTickets ?? 1;
        var activeTickets = await GetActiveTicketsAsync(guild.Id, creator.Id, id);

        if (activeTickets.Count >= maxTickets)
        {
            throw new InvalidOperationException($"You can only have {maxTickets} active tickets of this type.");
        }

        // Create ticket channel
        var categoryId = button?.CategoryId ?? option?.CategoryId;
        var category = categoryId.HasValue ? await guild.GetCategoryChannelAsync(categoryId.Value) : null;

        var channelName = (button?.ChannelNameFormat ?? option?.ChannelNameFormat ?? "ticket-{username}-{id}")
            .Replace("{username}", creator.Username.ToLower())
            .Replace("{id}", (activeTickets.Count + 1).ToString());

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
            Log.Error(ex, "Failed to create ticket channel");
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

        ctx.Tickets.Add(ticket);
        await ctx.SaveChangesAsync();

        // Send messages in order
        try
        {
            // 1. Opening message (custom or default)
            var openMessageJson = button?.OpenMessageJson ?? option?.OpenMessageJson;
            if (!string.IsNullOrEmpty(openMessageJson))
            {
                var replacer = new ReplacementBuilder()
                    .WithOverride("%ticket.id%", () => ticket.Id.ToString())
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
                    out _
                );

                if (success)
                    await channel.SendMessageAsync(plainText, embeds: embeds, components: GetDefaultTicketComponents().Build());
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

            // 2. Modal responses if any
            if (modalResponses?.Any() == true)
            {
                var modalEmbed = new EmbedBuilder()
                    .WithTitle("Ticket Information")
                    .WithDescription(string.Join("\n", modalResponses.Select(r => $"**{r.Key}**: {r.Value}")))
                    .WithColor(Color.Blue)
                    .Build();

                await channel.SendMessageAsync(embed: modalEmbed);
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
                        .WithTitle("New Ticket Created")
                        .WithDescription($"Ticket #{ticket.Id} created by {creator.Mention}")
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
            Log.Error(ex, "Error during ticket creation messages/notifications");
            try
            {
                await channel.DeleteAsync();
                ctx.Tickets.Remove(ticket);
                await ctx.SaveChangesAsync();
            }
            catch (Exception cleanupEx)
            {
                Log.Error(cleanupEx, "Error during ticket cleanup");
            }

            throw new InvalidOperationException("Failed to complete ticket creation.");
        }
    }

    /// <summary>
    ///     Sends notifications about a new ticket to relevant staff members.
    /// </summary>
    private async Task SendTicketNotificationsAsync(Ticket ticket, IUser creator, IGuild guild,
        GuildTicketSettings settings)
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
                    await channel.SendMessageAsync($"{mentions} A new ticket requires attention.");
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
                                .WithTitle("New Ticket Notification")
                                .WithDescription($"A new ticket has been created in {guild.Name}")
                                .AddField("Creator", creator.ToString(), true)
                                .AddField("Channel", $"#{channel.Name}", true)
                                .WithColor(Color.Blue)
                                .Build();

                            await member.SendMessageAsync(embed: dmEmbed);
                        }
                        catch (Exception ex)
                        {
                            Log.Warning(ex, "Failed to send DM notification to {UserId}", member.Id);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error sending ticket notifications");
        }
    }

    /// <summary>
    ///     Claims a ticket for a staff member.
    /// </summary>
    public async Task ClaimTicketAsync(Ticket ticket, IGuildUser staff)
    {
        if (ticket.ClaimedBy.HasValue)
            throw new InvalidOperationException("Ticket is already claimed");

        await using var ctx = await _db.GetContextAsync();

        ctx.Attach(ticket);
        ticket.ClaimedBy = staff.Id;
        ticket.ClosedAt = DateTime.UtcNow;
        ticket.LastActivityAt = DateTime.UtcNow;

        if (await staff.Guild.GetChannelAsync(ticket.ChannelId) is ITextChannel channel)
        {
            var embed = new EmbedBuilder()
                .WithTitle("Ticket Claimed")
                .WithDescription($"This ticket has been claimed by {staff.Mention}")
                .WithColor(Color.Green)
                .Build();

            await channel.SendMessageAsync(embed: embed);
        }

        await ctx.SaveChangesAsync();
    }

    /// <summary>
    ///     Adds a note to a ticket.
    /// </summary>
    public async Task<TicketNote> AddNoteAsync(Ticket ticket, IGuildUser author, string content)
    {
        await using var ctx = await _db.GetContextAsync();

        var note = new TicketNote
        {
            TicketId = ticket.Id, AuthorId = author.Id, Content = content, CreatedAt = DateTime.UtcNow
        };

        ctx.Attach(ticket);
        ticket.Notes.Add(note);
        ticket.LastActivityAt = DateTime.UtcNow;

        await ctx.SaveChangesAsync();

        return note;
    }

    /// <summary>
    ///     Creates a new case and optionally links tickets to it.
    /// </summary>
    public async Task<TicketCase> CreateCaseAsync(
        IGuild guild,
        string title,
        string description,
        IGuildUser creator,
        IEnumerable<Ticket> ticketsToLink = null)
    {
        await using var ctx = await _db.GetContextAsync();

        var ticketCase = new TicketCase
        {
            GuildId = guild.Id,
            Title = title,
            Description = description,
            CreatedBy = creator.Id,
            CreatedAt = DateTime.UtcNow
        };

        if (ticketsToLink != null)
        {
            foreach (var ticket in ticketsToLink)
            {
                ctx.Attach(ticket);
                ticket.Case = ticketCase;
            }
        }

        ctx.TicketCases.Add(ticketCase);
        await ctx.SaveChangesAsync();

        return ticketCase;
    }

    /// <summary>
    ///     Archives a ticket.
    /// </summary>
    public async Task ArchiveTicketAsync(Ticket ticket)
    {
        await using var ctx = await _db.GetContextAsync();

        ctx.Attach(ticket);

        IGuild guild = _client.GetGuild(ticket.GuildId);

        if (await guild.GetChannelAsync(ticket.ChannelId) is ITextChannel channel)
        {
            if (ticket.Button?.ArchiveCategoryId != null || ticket.SelectOption?.ArchiveCategoryId != null)
            {
                var categoryId = ticket.Button?.ArchiveCategoryId ?? ticket.SelectOption?.ArchiveCategoryId;
                var category = await channel.Guild.GetCategoryChannelAsync(categoryId.Value);

                if (category != null)
                    await channel.ModifyAsync(c => c.CategoryId = category.Id);
            }

            // Generate transcript if enabled
            if (ticket.Button?.SaveTranscript ?? true)
            {
                // Implementation for transcript generation
                ticket.TranscriptUrl = "transcript_url_here"; // Replace with actual transcript generation
            }
        }

        ticket.IsArchived = true;
        ticket.LastActivityAt = DateTime.UtcNow;

        await ctx.SaveChangesAsync();
    }

    private async Task UpdatePanelComponentsAsync(TicketPanel panel)
    {
        IGuild guild = _client.GetGuild(panel.GuildId);
        var channel = await guild.GetChannelAsync(panel.ChannelId) as ITextChannel;
        var message = await channel?.GetMessageAsync(panel.MessageId);

        if (message is not IUserMessage userMessage)
            return;

        var components = new ComponentBuilder();

        // Add buttons
        if (panel.Buttons?.Any() == true)
        {
            var buttonRow = new ActionRowBuilder();
            foreach (var button in panel.Buttons)
            {
                var btnBuilder = new ButtonBuilder()
                    .WithLabel(button.Label)
                    .WithCustomId(button.CustomId)
                    .WithStyle(button.Style);

                if (!string.IsNullOrEmpty(button.Emoji))
                    btnBuilder.WithEmote(Emote.Parse(button.Emoji));

                buttonRow.WithButton(btnBuilder);
            }

            components.AddRow(buttonRow);
        }

        // Add select menus
        if (panel.SelectMenus?.Any() == true)
        {
            foreach (var menu in panel.SelectMenus)
            {
                var selectBuilder = new SelectMenuBuilder()
                    .WithCustomId(menu.CustomId)
                    .WithPlaceholder(menu.Placeholder);

                foreach (var option in menu.Options)
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

        await userMessage.ModifyAsync(m => m.Components = components.Build());
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

    private async Task HandleMessageComponent(SocketMessageComponent component)
    {
        await using var ctx = await _db.GetContextAsync();

        // Handle button clicks
        if (component.Data.Type == ComponentType.Button && component.Data.CustomId.StartsWith("ticket_btn_"))
        {
            var button = await ctx.PanelButtons.FirstOrDefaultAsync(b => b.CustomId == component.Data.CustomId);
            if (button == null)
                return;

            try
            {
                if (!string.IsNullOrEmpty(button.ModalJson))
                {
                    // Show modal if configured
                    var modalData = JsonSerializer.Deserialize<Dictionary<string, string>>(button.ModalJson);
                    var modal = new ModalBuilder()
                        .WithTitle("Create Ticket")
                        .WithCustomId($"ticket_modal_{button.Id}");

                    foreach (var field in modalData)
                    {
                        modal.AddTextInput(field.Key, field.Value, required: true);
                    }

                    await component.RespondWithModalAsync(modal.Build());
                }
                else
                {
                    // Create ticket directly
                    await CreateTicketAsync(
                        (component.Channel as IGuildChannel)?.Guild,
                        component.User,
                        button);

                    await component.RespondAsync("Ticket created!", ephemeral: true);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating ticket from button");
                await component.RespondAsync("Failed to create ticket. Please try again later.", ephemeral: true);
            }
        }

        // Handle select menu selections
        if (component.Data.Type == ComponentType.SelectMenu && component.Data.CustomId.StartsWith("ticket_select_"))
        {
            var menuOption = await ctx.SelectMenuOptions
                .Include(o => o.SelectMenu)
                .FirstOrDefaultAsync(o => o.Value == component.Data.Values.First());

            if (menuOption == null)
                return;

            try
            {
                if (!string.IsNullOrEmpty(menuOption.ModalJson))
                {
                    var modalData = JsonSerializer.Deserialize<Dictionary<string, string>>(menuOption.ModalJson);
                    var modal = new ModalBuilder()
                        .WithTitle("Create Ticket")
                        .WithCustomId($"ticket_modal_select_{menuOption.Id}");

                    foreach (var field in modalData)
                    {
                        modal.AddTextInput(field.Key, field.Value, required: true);
                    }

                    await component.RespondWithModalAsync(modal.Build());
                }
                else
                {
                    await CreateTicketAsync(
                        (component.Channel as IGuildChannel)?.Guild,
                        component.User,
                        option: menuOption);

                    await component.RespondAsync("Ticket created!", ephemeral: true);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating ticket from select menu");
                await component.RespondAsync("Failed to create ticket. Please try again later.", ephemeral: true);
            }
        }
    }

    private async Task HandleModalSubmitted(SocketModal modal)
    {
        await using var ctx = await _db.GetContextAsync();

        try
        {
            var responses = modal.Data.Components.ToDictionary(
                x => x.CustomId,
                x => x.Value);

            if (modal.Data.CustomId.StartsWith("ticket_modal_"))
            {
                if (modal.Data.CustomId.Contains("select_"))
                {
                    // Handle select menu modal
                    var optionId = int.Parse(modal.Data.CustomId.Split('_').Last());
                    var option = await ctx.SelectMenuOptions.FindAsync(optionId);

                    if (option != null)
                    {
                        await CreateTicketAsync(
                            (modal.Channel as IGuildChannel)?.Guild,
                            modal.User,
                            option: option,
                            modalResponses: responses);

                        await modal.RespondAsync("Ticket created!", ephemeral: true);
                    }
                }
                else
                {
                    // Handle button modal
                    var buttonId = int.Parse(modal.Data.CustomId.Split('_').Last());
                    var button = await ctx.PanelButtons.FindAsync(buttonId);

                    if (button != null)
                    {
                        await CreateTicketAsync(
                            (modal.Channel as IGuildChannel)?.Guild,
                            modal.User,
                            button,
                            modalResponses: responses);

                        await modal.RespondAsync("Ticket created!", ephemeral: true);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error handling modal submission");
            await modal.RespondAsync("Failed to create ticket. Please try again later.", ephemeral: true);
        }
    }

    private async Task HandleMessageDeleted(Cacheable<IMessage, ulong> message,
        Cacheable<IMessageChannel, ulong> channel)
    {
        if (!message.HasValue || !channel.HasValue)
            return;

        await using var ctx = await _db.GetContextAsync();

        // Check if deleted message was a panel
        var panel = await ctx.TicketPanels
            .FirstOrDefaultAsync(p => p.MessageId == message.Value.Id);

        if (panel != null)
        {
            // Panel was deleted, clean up
            ctx.TicketPanels.Remove(panel);
            await ctx.SaveChangesAsync();
        }
    }

    /// <summary>
    ///     Checks for tickets that should be auto-closed and handles them.
    /// </summary>
    public async Task CheckAutoCloseTicketsAsync()
    {
        await using var ctx = await _db.GetContextAsync();

        var tickets = await ctx.Tickets
            .Include(t => t.Button)
            .Include(t => t.SelectOption)
            .Where(t => !t.IsArchived && !t.ClosedAt.HasValue)
            .ToListAsync();

        foreach (var ticket in tickets)
        {
            IGuild guild = _client.GetGuild(ticket.GuildId);
            var autoCloseTime = ticket.Button?.AutoCloseTime ?? ticket.SelectOption?.AutoCloseTime;
            if (!autoCloseTime.HasValue || !ticket.LastActivityAt.HasValue)
                continue;

            if (DateTime.UtcNow - ticket.LastActivityAt.Value > autoCloseTime.Value)
            {
                if (await guild.GetChannelAsync(ticket.ChannelId) is ITextChannel channel)
                {
                    var embed = new EmbedBuilder()
                        .WithTitle("Ticket Auto-Closed")
                        .WithDescription("This ticket has been automatically closed due to inactivity.")
                        .WithColor(Color.Red)
                        .Build();

                    await channel.SendMessageAsync(embed: embed);
                }

                ticket.ClosedAt = DateTime.UtcNow;
                await ArchiveTicketAsync(ticket);
            }
        }

        await ctx.SaveChangesAsync();
    }

    /// <summary>
    ///     Gets active tickets for a user in a guild.
    /// </summary>
    public async Task<List<Ticket>> GetActiveTicketsAsync(ulong guildId, ulong userId, int id)
    {
        await using var ctx = await _db.GetContextAsync();

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
    public async Task SetTicketPriorityAsync(Ticket ticket, string priority, IGuildUser staff)
    {
        await using var ctx = await _db.GetContextAsync();

        var allowedPriorities = ticket.Button?.AllowedPriorities ??
                                ticket.SelectOption?.AllowedPriorities ?? [];
        if (allowedPriorities.Any() && !allowedPriorities.Contains(priority))
            throw new InvalidOperationException("Invalid priority level");

        ctx.Attach(ticket);
        ticket.Priority = priority;
        ticket.LastActivityAt = DateTime.UtcNow;
        IGuild guild = _client.GetGuild(ticket.GuildId);

        if (await guild.GetChannelAsync(ticket.ChannelId) is ITextChannel channel)
        {
            var embed = new EmbedBuilder()
                .WithTitle("Ticket Priority Updated")
                .WithDescription($"Priority set to **{priority}** by {staff.Mention}")
                .WithColor(Color.Blue)
                .Build();

            await channel.SendMessageAsync(embed: embed);
        }

        await ctx.SaveChangesAsync();
    }

    /// <summary>
    ///     Adds tags to a ticket.
    /// </summary>
    public async Task AddTicketTagsAsync(Ticket ticket, IEnumerable<string> tags)
    {
        await using var ctx = await _db.GetContextAsync();

        ctx.Attach(ticket);
        ticket.Tags ??= [];
        ticket.Tags.AddRange(tags);
        ticket.LastActivityAt = DateTime.UtcNow;

        await ctx.SaveChangesAsync();
    }

    /// <summary>
    ///     Checks response times for tickets and sends notifications if needed.
    /// </summary>
    public async Task CheckResponseTimesAsync()
    {
        await using var ctx = await _db.GetContextAsync();

        var tickets = await ctx.Tickets
            .Include(t => t.Button)
            .Include(t => t.SelectOption)
            .Where(t => !t.IsArchived &&
                        !t.ClosedAt.HasValue &&
                        !t.ClaimedBy.HasValue)
            .ToListAsync();

        foreach (var ticket in tickets)
        {
            var requiredResponseTime = ticket.Button?.RequiredResponseTime ?? ticket.SelectOption?.RequiredResponseTime;
            if (!requiredResponseTime.HasValue || !ticket.LastActivityAt.HasValue)
                continue;

            if (DateTime.UtcNow - ticket.CreatedAt > requiredResponseTime.Value)
            {
                // TODO: Send notifications to staff
                Log.Warning("Ticket {TicketId} has exceeded response time threshold", ticket.Id);
            }
        }
    }

    /// <summary>
    ///     Sets the transcript channel for a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="channelId">The ID of the channel to set as the transcript channel.</param>
    public async Task SetTranscriptChannelAsync(ulong guildId, ulong channelId)
    {
        await using var ctx = await _db.GetContextAsync();
        var settings = await ctx.GuildTicketSettings.FirstOrDefaultAsync(x => x.GuildId == guildId) ??
                       new GuildTicketSettings();

        settings.TranscriptChannelId = channelId;
        ctx.GuildTicketSettings.Update(settings);
        await ctx.SaveChangesAsync();
    }

    /// <summary>
    ///     Sets the log channel for a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="channelId">The ID of the channel to set as the log channel.</param>
    public async Task SetLogChannelAsync(ulong guildId, ulong channelId)
    {
        await using var ctx = await _db.GetContextAsync();
        var settings = await ctx.GuildTicketSettings.FirstOrDefaultAsync(x => x.GuildId == guildId) ??
                       new GuildTicketSettings();

        settings.LogChannelId = channelId;
        ctx.GuildTicketSettings.Update(settings);
        await ctx.SaveChangesAsync();
    }


    /// <summary>
    ///     Links tickets to an existing case.
    /// </summary>
    /// <param name="caseId">The ID of the case to link tickets to.</param>
    /// <param name="tickets">The tickets to link to the case.</param>
    public async Task LinkTicketsToCase(int caseId, IEnumerable<Ticket> tickets)
    {
        await using var ctx = await _db.GetContextAsync();
        var ticketCase = await ctx.TicketCases.FindAsync(caseId);
        if (ticketCase == null) throw new InvalidOperationException("Case not found.");

        foreach (var ticket in tickets)
        {
            ctx.Attach(ticket);
            ticket.CaseId = caseId;
        }

        await ctx.SaveChangesAsync();
    }

    /// <summary>
/// Gets a formatted list of all buttons on a specific ticket panel
/// </summary>
/// <param name="panelId">The message ID of the panel to get buttons from</param>
/// <returns>A list of ButtonInfo objects containing button details</returns>
public async Task<List<ButtonInfo>> GetPanelButtonsAsync(ulong panelId)
{
    await using var ctx = await _db.GetContextAsync();
    var panel = await ctx.TicketPanels
        .Include(p => p.Buttons)
        .FirstOrDefaultAsync(p => p.MessageId == panelId);

    if (panel == null)
        return [];

    return panel.Buttons.Select(b => new ButtonInfo
    {
        Id = b.Id,
        CustomId = b.CustomId,
        Label = b.Label,
        Style = b.Style,
        Emoji = b.Emoji,
        CategoryId = b.CategoryId,
        ArchiveCategoryId = b.ArchiveCategoryId,
        SupportRoles = b.SupportRoles,
        ViewerRoles = b.ViewerRoles,
        HasModal = !string.IsNullOrEmpty(b.ModalJson),
        HasCustomOpenMessage = !string.IsNullOrEmpty(b.OpenMessageJson)
    }).ToList();
}

/// <summary>
/// Gets a formatted list of all select menus on a specific ticket panel
/// </summary>
/// <param name="panelId">The message ID of the panel to get select menus from</param>
/// <returns>A list of SelectMenuInfo objects containing menu details</returns>
public async Task<List<SelectMenuInfo>> GetPanelSelectMenusAsync(ulong panelId)
{
    await using var ctx = await _db.GetContextAsync();
    var panel = await ctx.TicketPanels
        .Include(p => p.SelectMenus)
        .ThenInclude(m => m.Options)
        .FirstOrDefaultAsync(p => p.MessageId == panelId);

    if (panel == null)
        return [];

    return panel.SelectMenus.Select(m => new SelectMenuInfo
    {
        Id = m.Id,
        CustomId = m.CustomId,
        Placeholder = m.Placeholder,
        Options = m.Options.Select(o => new SelectOptionInfo
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
    }).ToList();
}

/// <summary>
/// Gets a detailed list of all ticket panels and their components in a guild
/// </summary>
/// <param name="guildId">The ID of the guild to get panels from</param>
/// <returns>A list of PanelInfo objects containing complete panel details</returns>
public async Task<List<PanelInfo>> GetAllPanelsAsync(ulong guildId)
{
    await using var ctx = await _db.GetContextAsync();
    var panels = await ctx.TicketPanels
        .Include(p => p.Buttons)
        .Include(p => p.SelectMenus)
        .ThenInclude(m => m.Options)
        .Where(p => p.GuildId == guildId)
        .ToListAsync();

    return panels.Select(p => new PanelInfo
    {
        MessageId = p.MessageId,
        ChannelId = p.ChannelId,
        Buttons = p.Buttons.Select(b => new ButtonInfo
        {
            Id = b.Id,
            CustomId = b.CustomId,
            Label = b.Label,
            Style = b.Style,
            Emoji = b.Emoji,
            CategoryId = b.CategoryId,
            ArchiveCategoryId = b.ArchiveCategoryId,
            SupportRoles = b.SupportRoles,
            ViewerRoles = b.ViewerRoles,
            HasModal = !string.IsNullOrEmpty(b.ModalJson),
            HasCustomOpenMessage = !string.IsNullOrEmpty(b.OpenMessageJson)
        }).ToList(),
        SelectMenus = p.SelectMenus.Select(m => new SelectMenuInfo
        {
            Id = m.Id,
            CustomId = m.CustomId,
            Placeholder = m.Placeholder,
            Options = m.Options.Select(o => new SelectOptionInfo
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
        await using var ctx = await _db.GetContextAsync();
        return await ctx.Tickets.FirstOrDefaultAsync(t => t.Id == ticketId);
    }

    /// <summary>
    ///     Retrieves a ticket by its channel ID.
    /// </summary>
    /// <param name="channelId">The ID of the channel.</param>
    /// <returns>The ticket object, if found.</returns>
    public async Task<Ticket?> GetTicketAsync(ulong channelId)
    {
        await using var ctx = await _db.GetContextAsync();
        return await ctx.Tickets.FirstOrDefaultAsync(t => t.ChannelId == channelId);
    }


    /// <summary>
    ///     Retrieves tickets by their IDs.
    /// </summary>
    /// <param name="ticketIds">The IDs of the tickets to retrieve.</param>
    /// <returns>A list of tickets matching the specified IDs.</returns>
    public async Task<List<Ticket>> GetTicketsAsync(IEnumerable<int> ticketIds)
    {
        await using var ctx = await _db.GetContextAsync();
        return await ctx.Tickets.Where(t => ticketIds.Contains(t.Id)).ToListAsync();
    }

    /// <summary>
    ///     Retrieves a panel button by its db Id.
    /// </summary>
    /// <param name="buttonId">The db ID of the button to retrieve.</param>
    /// <returns>The panel button matching the specified ID, if found.</returns>
    public async Task<PanelButton?> GetButtonAsync(int buttonId)
    {
        await using var ctx = await _db.GetContextAsync();
        return await ctx.PanelButtons
            .Include(b => b.Panel) // Include the related panel if needed
            .FirstOrDefaultAsync(b => b.Id == buttonId);
    }

    /// <summary>
    ///     Retrieves a panel button by its custom ID.
    /// </summary>
    /// <param name="buttonId">The custom ID of the button to retrieve.</param>
    /// <returns>The panel button matching the specified ID, if found.</returns>
    public async Task<PanelButton?> GetButtonAsync(string buttonId)
    {
        await using var ctx = await _db.GetContextAsync();
        return await ctx.PanelButtons
            .Include(b => b.Panel) // Include the related panel if needed
            .FirstOrDefaultAsync(b => b.CustomId == buttonId);
    }

    /// <summary>
    ///     Validates and creates modal for ticket creation with full field configuration
    /// </summary>
    public async Task HandleModalCreation(IGuildUser user, string modalJson, string customId,
        IDiscordInteraction component)
    {
        try
        {
            var fields = JsonSerializer.Deserialize<Dictionary<string, ModalFieldConfig>>(modalJson);

            // Validate field count
            if (fields.Count > 5)
                throw new ArgumentException("Modal cannot have more than 5 fields");

            var mb = new ModalBuilder()
                .WithCustomId(customId)
                .WithTitle("Create Ticket");

            foreach (var (key, field) in fields)
            {
                // Validate and enforce length limits
                var minLength = Math.Max(0, Math.Min(field.MinLength ?? 0, 4000));
                var maxLength = Math.Max(minLength, Math.Min(field.MaxLength ?? 4000, 4000));

                // Determine style
                var style = field.Style == 2 ? TextInputStyle.Paragraph : TextInputStyle.Short;

                mb.AddTextInput(
                    field.Label ?? key,
                    key.ToLower().Replace(" ", "_"),
                    style,
                    field.Placeholder,
                    minLength == 0 ? null : minLength,
                    maxLength,
                    field.Required,
                    field.Value
                );
            }

            await component.RespondWithModalAsync(mb.Build());
        }
        catch (JsonException ex)
        {
            Log.Error(ex, "Invalid modal configuration format");
            await component.RespondAsync("Invalid ticket form configuration.", ephemeral: true);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error creating modal");
            await component.RespondAsync("Failed to create ticket form.", ephemeral: true);
        }
    }


    /// <summary>
    ///     Gets a ticket panel by ID.
    /// </summary>
    /// <param name="panelId">The ID of the panel to retrieve.</param>
    /// <returns>The panel if found, null otherwise.</returns>
    public async Task<TicketPanel?> GetPanelAsync(ulong panelId)
    {
        await using var ctx = await _db.GetContextAsync();
        return await ctx.TicketPanels.FirstOrDefaultAsync(x => x.MessageId == panelId);
    }

    /// <summary>
    ///     Adds a select menu to an existing ticket panel.
    /// </summary>
    /// <param name="panel">The panel to add the select menu to.</param>
    /// <param name="placeholder">The placeholder text for the menu.</param>
    /// <param name="minValues">Minimum number of selections required.</param>
    /// <param name="maxValues">Maximum number of selections allowed.</param>
    /// <summary>
    ///     First service method modification needed
    /// </summary>
    public async Task<PanelSelectMenu> AddSelectMenuAsync(
        TicketPanel panel,
        string placeholder,
        string firstOptionLabel,
        string firstOptionDescription = null,
        string firstOptionEmoji = null,
        int minValues = 1,
        int maxValues = 1)
    {
        await using var ctx = await _db.GetContextAsync();

        var menu = new PanelSelectMenu
        {
            PanelId = panel.Id,
            CustomId = $"ticket_select_{Guid.NewGuid():N}",
            Placeholder = placeholder,
            Options =
            [
                new()
                {
                    Label = firstOptionLabel,
                    Value = $"option_{Guid.NewGuid():N}",
                    Description = firstOptionDescription,
                    Emoji = firstOptionEmoji,
                    ChannelNameFormat = "ticket-{username}-{id}", // Default format
                    SaveTranscript = true // Default value
                }
            ]
        };

        ctx.Attach(panel);
        panel.SelectMenus.Add(menu);
        await ctx.SaveChangesAsync();
        await UpdatePanelComponentsAsync(panel);

        return menu;
    }

    /// <summary>
    ///     Updates a select menu's properties.
    /// </summary>
    /// <param name="menu">The menu to update.</param>
    /// <param name="updateAction">Action containing the updates to apply.</param>
    public async Task UpdateSelectMenuAsync(PanelSelectMenu menu, Action<PanelSelectMenu> updateAction)
    {
        await using var ctx = await _db.GetContextAsync();

        ctx.Attach(menu);
        updateAction(menu);

        await UpdatePanelComponentsAsync(menu.Panel);
        await ctx.SaveChangesAsync();
    }

    /// <summary>
    ///     Adds an option to a select menu.
    /// </summary>
    /// <param name="menu">The menu to add the option to.</param>
    /// <param name="label">The option label.</param>
    /// <param name="value">The option value.</param>
    /// <param name="description">Optional description for the option.</param>
    /// <param name="emoji">Optional emoji for the option.</param>
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
        string defaultPriority = null)
    {
        await using var ctx = await _db.GetContextAsync();

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
            SupportRoles = supportRoles ?? [],
            ViewerRoles = viewerRoles ?? [],
            AutoCloseTime = autoCloseTime,
            RequiredResponseTime = requiredResponseTime,
            MaxActiveTickets = maxActiveTickets,
            AllowedPriorities = allowedPriorities ?? [],
            DefaultPriority = defaultPriority
        };

        ctx.Attach(menu);
        menu.Options.Add(option);
        await UpdatePanelComponentsAsync(menu.Panel);
        await ctx.SaveChangesAsync();

        return option;
    }

    /// <summary>
    ///     Updates a select menu option's properties.
    /// </summary>
    /// <param name="option">The option to update.</param>
    /// <param name="updateAction">Action containing the updates to apply.</param>
    public async Task UpdateSelectOptionAsync(SelectMenuOption option, Action<SelectMenuOption> updateAction)
    {
        await using var ctx = await _db.GetContextAsync();

        ctx.Attach(option);
        updateAction(option);

        await UpdatePanelComponentsAsync(option.SelectMenu.Panel);
        await ctx.SaveChangesAsync();
    }

    /// <summary>
    ///     Retrieves a select menu by its custom ID.
    /// </summary>
    /// <param name="menuId">The custom ID of the menu to retrieve.</param>
    /// <returns>The select menu matching the specified ID, if found.</returns>
    public async Task<PanelSelectMenu> GetSelectMenuAsync(string menuId)
    {
        await using var ctx = await _db.GetContextAsync();
        return await ctx.PanelSelectMenus
            .Include(m => m.Panel)
            .Include(m => m.Options)
            .FirstOrDefaultAsync(m => m.CustomId == menuId);
    }

    /// <summary>
    ///     Retrieves a select menu option by its value.
    /// </summary>
    /// <param name="menuId">The ID of the menu containing the option.</param>
    /// <param name="value">The value of the option to retrieve.</param>
    /// <returns>The select menu option matching the specified value, if found.</returns>
    public async Task<SelectMenuOption> GetSelectOptionAsync(int menuId, string value)
    {
        await using var ctx = await _db.GetContextAsync();
        return await ctx.SelectMenuOptions
            .Include(o => o.SelectMenu)
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

            html.AppendLine($@"<div class='message'>
            <div class='message-info'>
                <img class='avatar' src='{msg.Author.GetAvatarUrl() ?? msg.Author.GetDefaultAvatarUrl()}' />
                <span class='username' style='color: {colorHex}'>{msg.Author.Username}</span>
                <span class='timestamp'>{msg.Timestamp.ToString("f")}</span>
            </div>
            <div class='content'>");

            // [Rest of message formatting remains the same]
        }

        html.AppendLine("</div></body></html>");
        return html.ToString();
    }

    /// <summary>
    ///     Closes a ticket in the specified guild.
    /// </summary>
    /// <param name="guild">The guild containing the ticket.</param>
    /// <param name="channelId">The ID of the ticket channel to close.</param>
    /// <returns>True if the ticket was successfully closed, false otherwise.</returns>
    public async Task<bool> CloseTicket(IGuild guild, ulong channelId)
    {
        await using var ctx = await _db.GetContextAsync();
        var ticket = await ctx.Tickets
            .Include(t => t.Button)
            .Include(t => t.SelectOption)
            .FirstOrDefaultAsync(t => t.ChannelId == channelId && t.GuildId == guild.Id);

        if (ticket == null || ticket.ClosedAt.HasValue)
            return false;

        try
        {
            ticket.ClosedAt = DateTime.UtcNow;
            ticket.LastActivityAt = DateTime.UtcNow;

            var channel = await guild.GetTextChannelAsync(channelId);
            if (channel != null)
            {
                // Send closure message
                var embed = new EmbedBuilder()
                    .WithTitle("Ticket Closed")
                    .WithDescription("This ticket has been closed.")
                    .WithColor(Color.Red)
                    .WithCurrentTimestamp()
                    .Build();

                await channel.SendMessageAsync(embed: embed);

                // Generate transcript if enabled
                if (ticket.Button?.SaveTranscript == true || ticket.SelectOption?.SaveTranscript == true)
                {
                    // Implement transcript generation
                    var settings = await ctx.GuildTicketSettings
                        .FirstOrDefaultAsync(s => s.GuildId == guild.Id);

                    if (settings?.TranscriptChannelId != null)
                    {
                        var transcriptChannel = await guild.GetTextChannelAsync(settings.TranscriptChannelId.Value);
                        if (transcriptChannel != null)
                        {
                            try
                            {
                                var tscript = await GenerateTranscriptAsync(channel, ticket);

                                // Convert HTML string to byte array
                                using var stream = new MemoryStream(Encoding.UTF8.GetBytes(tscript));

                                // Create file attachment
                                var transcriptFile = new FileAttachment(stream, $"ticket-{ticket.Id}-transcript.html");

                                // Create info embed
                                var transcriptEmbed = new EmbedBuilder()
                                    .WithTitle($"Ticket Transcript #{ticket.Id}")
                                    .WithDescription(
                                        $"Ticket created by <@{ticket.CreatorId}>\n" +
                                        $"Created: {TimestampTag.FromDateTime(ticket.CreatedAt)}\n" +
                                        $"Closed: {TimestampTag.FromDateTime(ticket.ClosedAt ?? DateTime.UtcNow)}")
                                    .WithColor(Mewdeko.OkColor)
                                    .Build();

                                // Send transcript with info
                                var msg = await transcriptChannel.SendFileAsync(transcriptFile, embed: transcriptEmbed);

                                // Store the transcript URL
                                ticket.TranscriptUrl = msg.Attachments.FirstOrDefault()?.Url;
                            }
                            catch (Exception ex)
                            {
                                Log.Error(ex, "Failed to generate/save transcript for ticket {TicketId}", ticket.Id);
                            }
                        }
                    }
                }

                // Move to archive category if configured
                var archiveCategoryId = ticket.Button?.ArchiveCategoryId ?? ticket.SelectOption?.ArchiveCategoryId;
                if (archiveCategoryId.HasValue)
                {
                    await channel.ModifyAsync(c => c.CategoryId = archiveCategoryId.Value);
                }
            }

            await ctx.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error closing ticket {TicketId}", ticket.Id);
            return false;
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
        await using var ctx = await _db.GetContextAsync();
        var ticket = await ctx.Tickets
            .Include(t => t.Button)
            .Include(t => t.SelectOption)
            .FirstOrDefaultAsync(t => t.ChannelId == channelId && t.GuildId == guild.Id);

        if (ticket == null || ticket.ClosedAt.HasValue || ticket.ClaimedBy.HasValue)
            return false;

        // Verify staff member has permission to claim
        var hasPermission = false;
        var supportRoles = ticket.Button?.SupportRoles ?? ticket.SelectOption?.SupportRoles ?? [];

        foreach (var roleId in supportRoles)
        {
            if (staff.RoleIds.Contains(roleId))
            {
                hasPermission = true;
                break;
            }
        }

        if (!hasPermission && !staff.GuildPermissions.Administrator)
            return false;

        try
        {
            ticket.ClaimedBy = staff.Id;
            ticket.LastActivityAt = DateTime.UtcNow;

            var channel = await guild.GetTextChannelAsync(channelId);
            if (channel != null)
            {
                var embed = new EmbedBuilder()
                    .WithTitle("Ticket Claimed")
                    .WithDescription($"This ticket has been claimed by {staff.Mention}")
                    .WithColor(Color.Green)
                    .WithCurrentTimestamp()
                    .Build();

                await channel.SendMessageAsync(embed: embed);

                // Send DM notification to ticket creator if enabled
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
                                .WithTitle("Ticket Claimed")
                                .WithDescription($"Your ticket has been claimed by {staff}")
                                .WithColor(Color.Green)
                                .WithCurrentTimestamp()
                                .Build();

                            await creator.SendMessageAsync(embed: dmEmbed);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Failed to send DM notification to ticket creator");
                    }
                }
            }

            await ctx.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error claiming ticket {TicketId}", ticket.Id);
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
        await using var ctx = await _db.GetContextAsync();
        var ticket = await ctx.Tickets
            .FirstOrDefaultAsync(t => t.ChannelId == channelId && t.GuildId == guild.Id);

        if (ticket == null || ticket.ClosedAt.HasValue || !ticket.ClaimedBy.HasValue)
            return false;

        // Only allow the claimer or admins to unclaim
        if (ticket.ClaimedBy != staff.Id && !staff.GuildPermissions.Administrator)
            return false;

        try
        {
            var previousClaimer = ticket.ClaimedBy.Value;
            ticket.ClaimedBy = null;
            ticket.LastActivityAt = DateTime.UtcNow;

            var channel = await guild.GetTextChannelAsync(channelId);
            if (channel != null)
            {
                var embed = new EmbedBuilder()
                    .WithTitle("Ticket Unclaimed")
                    .WithDescription($"This ticket has been unclaimed by {staff.Mention}")
                    .WithColor(Color.Orange)
                    .WithCurrentTimestamp()
                    .Build();

                await channel.SendMessageAsync(embed: embed);

                // Notify previous claimer if enabled
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
                                .WithTitle("Ticket Unclaimed")
                                .WithDescription($"Your claim on ticket #{ticket.Id} has been removed by {staff}")
                                .WithColor(Color.Orange)
                                .WithCurrentTimestamp()
                                .Build();

                            await previousUser.SendMessageAsync(embed: dmEmbed);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Failed to send DM notification for ticket unclaim");
                    }
                }
            }

            await ctx.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error unclaiming ticket {TicketId}", ticket.Id);
            return false;
        }
    }

    /// <summary>
    ///     Adds a note to a ticket.
    /// </summary>
    /// <param name="channelId">The ID of the ticket channel.</param>
    /// <param name="author">The staff member adding the note.</param>
    /// <param name="content">The content of the note.</param>
    /// <returns>True if the note was successfully added, false otherwise.</returns>
    public async Task<bool> AddNote(ulong channelId, IGuildUser author, string content)
    {
        await using var ctx = await _db.GetContextAsync();
        var ticket = await ctx.Tickets
            .Include(t => t.Notes)
            .FirstOrDefaultAsync(t => t.ChannelId == channelId && t.GuildId == author.GuildId);

        if (ticket == null || ticket.ClosedAt.HasValue)
            return false;

        try
        {
            var note = new TicketNote
            {
                TicketId = ticket.Id, AuthorId = author.Id, Content = content, CreatedAt = DateTime.UtcNow
            };

            ticket.Notes.Add(note);
            ticket.LastActivityAt = DateTime.UtcNow;

            var channel = await author.Guild.GetTextChannelAsync(channelId);
            if (channel != null)
            {
                var embed = new EmbedBuilder()
                    .WithTitle("Note Added")
                    .WithDescription(content)
                    .WithFooter($"Added by {author}")
                    .WithColor(Color.Blue)
                    .WithCurrentTimestamp()
                    .Build();

                await channel.SendMessageAsync(embed: embed);
            }

            await ctx.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error adding note to ticket {TicketId}", ticket.Id);
            return false;
        }
    }

    /// <summary>
    ///     Edits an existing ticket note.
    /// </summary>
    /// <param name="noteId">The ID of the note to edit.</param>
    /// <param name="author">The staff member editing the note.</param>
    /// <param name="newContent">The new content for the note.</param>
    /// <returns>True if the note was successfully edited, false otherwise.</returns>
    public async Task<bool> EditNote(int noteId, IGuildUser author, string newContent)
    {
        await using var ctx = await _db.GetContextAsync();
        var note = await ctx.TicketNotes
            .Include(n => n.Ticket)
            .Include(n => n.EditHistory)
            .FirstOrDefaultAsync(n => n.Id == noteId);

        if (note == null || note.Ticket.ClosedAt.HasValue)
            return false;

        // Only allow the original author or admins to edit
        if (note.AuthorId != author.Id && !author.GuildPermissions.Administrator)
            return false;

        try
        {
            var edit = new NoteEdit
            {
                OldContent = note.Content, NewContent = newContent, EditorId = author.Id, EditedAt = DateTime.UtcNow
            };

            note.Content = newContent;
            note.EditHistory.Add(edit);
            note.Ticket.LastActivityAt = DateTime.UtcNow;

            var channel = await author.Guild.GetTextChannelAsync(note.Ticket.ChannelId);
            if (channel != null)
            {
                var embed = new EmbedBuilder()
                    .WithTitle("Note Edited")
                    .WithDescription($"**Original:** {edit.OldContent}\n**New:** {newContent}")
                    .WithFooter($"Edited by {author}")
                    .WithColor(Color.Blue)
                    .WithCurrentTimestamp()
                    .Build();

                await channel.SendMessageAsync(embed: embed);
            }

            await ctx.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error editing note {NoteId}", noteId);
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
        await using var ctx = await _db.GetContextAsync();
        var note = await ctx.TicketNotes
            .Include(n => n.Ticket)
            .FirstOrDefaultAsync(n => n.Id == noteId);

        if (note == null || note.Ticket.ClosedAt.HasValue)
            return false;

        // Only allow the original author or admins to delete
        if (note.AuthorId != author.Id && !author.GuildPermissions.Administrator)
            return false;

        try
        {
            ctx.TicketNotes.Remove(note);
            note.Ticket.LastActivityAt = DateTime.UtcNow;

            var channel = await author.Guild.GetTextChannelAsync(note.Ticket.ChannelId);
            if (channel != null)
            {
                var embed = new EmbedBuilder()
                    .WithTitle("Note Deleted")
                    .WithDescription($"A note by <@{note.AuthorId}> was deleted by {author.Mention}")
                    .WithColor(Color.Red)
                    .WithCurrentTimestamp()
                    .Build();

                await channel.SendMessageAsync(embed: embed);
            }

            await ctx.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error deleting note {NoteId}", noteId);
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
        await using var ctx = await _db.GetContextAsync();
        var ticketCase = new TicketCase
        {
            GuildId = guild.Id,
            Title = name,
            Description = description,
            CreatedBy = creator.Id,
            CreatedAt = DateTime.UtcNow
        };

        ctx.TicketCases.Add(ticketCase);
        await ctx.SaveChangesAsync();

        // Log case creation if logging is enabled
        var settings = await ctx.GuildTicketSettings.FirstOrDefaultAsync(s => s.GuildId == guild.Id);
        if (settings?.LogChannelId != null)
        {
            var logChannel = await guild.GetTextChannelAsync(settings.LogChannelId.Value);
            if (logChannel != null)
            {
                var embed = new EmbedBuilder()
                    .WithTitle("Case Created")
                    .WithDescription($"Case #{ticketCase.Id} created by {creator.Mention}")
                    .AddField("Title", name)
                    .AddField("Description", description)
                    .WithColor(Color.Green)
                    .WithCurrentTimestamp()
                    .Build();

                await logChannel.SendMessageAsync(embed: embed);
            }
        }

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
        await using var ctx = await _db.GetContextAsync();
        var ticketCase = await ctx.TicketCases
            .Include(c => c.LinkedTickets)
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
            await ctx.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error linking ticket {TicketId} to case {CaseId}", ticketId, caseId);
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
        await using var ctx = await _db.GetContextAsync();
        var ticket = await ctx.Tickets
            .Include(t => t.Case)
            .FirstOrDefaultAsync(t => t.Id == ticketId && t.GuildId == guildId);

        if (ticket == null || ticket.CaseId == null)
            return false;

        try
        {
            ticket.CaseId = null;
            ticket.Case = null;
            await ctx.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error unlinking ticket {TicketId} from case", ticketId);
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

    /// <summary>
    ///     Gets statistics about tickets in a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <returns>Statistics about the guild's tickets.</returns>
    public async Task<GuildStatistics> GetGuildStatistics(ulong guildId)
    {
        await using var ctx = await _db.GetContextAsync();
        var tickets = await ctx.Tickets
            .Include(t => t.Button)
            .Include(t => t.SelectOption)
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

            if (!stats.TicketsByType.ContainsKey(type))
                stats.TicketsByType[type] = 0;
            stats.TicketsByType[type]++;

            if (!string.IsNullOrEmpty(ticket.Priority))
            {
                if (!stats.TicketsByPriority.ContainsKey(ticket.Priority))
                    stats.TicketsByPriority[ticket.Priority] = 0;
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
        await using var ctx = await _db.GetContextAsync();
        var tickets = await ctx.Tickets
            .Include(t => t.Button)
            .Include(t => t.SelectOption)
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

            if (!stats.TicketsByType.ContainsKey(type))
                stats.TicketsByType[type] = 0;
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
        await using var ctx = await _db.GetContextAsync();
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
        await using var ctx = await _db.GetContextAsync();
        var tickets = await ctx.Tickets
            .Include(t => t.Notes)
            .Where(t => t.GuildId == guildId && t.ClaimedBy.HasValue)
            .ToListAsync();

        var metrics = new Dictionary<ulong, (double totalMinutes, int count)>();

        foreach (var ticket in tickets)
        {
            if (!ticket.ClaimedBy.HasValue) continue;

            var firstResponse = ticket.Notes
                .Where(n => n.AuthorId == ticket.ClaimedBy.Value)
                .OrderBy(n => n.CreatedAt)
                .FirstOrDefault();

            if (firstResponse != null)
            {
                var responseTime = (firstResponse.CreatedAt - ticket.CreatedAt).TotalMinutes;
                if (!metrics.ContainsKey(ticket.ClaimedBy.Value))
                    metrics[ticket.ClaimedBy.Value] = (0, 0);

                var current = metrics[ticket.ClaimedBy.Value];
                metrics[ticket.ClaimedBy.Value] = (current.totalMinutes + responseTime, current.count + 1);
            }
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
        if (level < 1 || level > 5)
            throw new ArgumentException("Priority level must be between 1 and 5", nameof(level));

        await using var ctx = await _db.GetContextAsync();

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

            ctx.TicketPriorities.Add(priority);
            await ctx.SaveChangesAsync();

            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error creating priority {PriorityId} for guild {GuildId}", id, guildId);
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
        await using var ctx = await _db.GetContextAsync();
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

            ctx.TicketPriorities.Remove(priority);
            await ctx.SaveChangesAsync();

            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error deleting priority {PriorityId} for guild {GuildId}", id, guildId);
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
        await using var ctx = await _db.GetContextAsync();
        var ticket = await ctx.Tickets
            .Include(t => t.Button)
            .Include(t => t.SelectOption)
            .FirstOrDefaultAsync(t => t.ChannelId == channelId && t.GuildId == guild.Id);

        if (ticket == null || ticket.ClosedAt.HasValue)
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
                    .WithTitle("Ticket Priority Updated")
                    .WithDescription($"Priority set to {priority.Emoji} **{priority.Name}** by {staff.Mention}")
                    .WithColor(new Color(priority.Color))
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
                            $"{mentions} This ticket has been marked as {priority.Name} priority and requires attention.");
                    }
                }
            }

            await ctx.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error setting priority for ticket {TicketId}", ticket.Id);
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
        await using var ctx = await _db.GetContextAsync();

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

            ctx.TicketTags.Add(tag);
            await ctx.SaveChangesAsync();

            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error creating tag {TagId} for guild {GuildId}", id, guildId);
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
        await using var ctx = await _db.GetContextAsync();
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
                ticket.Tags.Remove(id);
            }

            ctx.TicketTags.Remove(tag);
            await ctx.SaveChangesAsync();

            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error deleting tag {TagId} for guild {GuildId}", id, guildId);
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
        await using var ctx = await _db.GetContextAsync();
        var ticket = await ctx.Tickets
            .FirstOrDefaultAsync(t => t.ChannelId == channelId && t.GuildId == guild.Id);

        if (ticket == null || ticket.ClosedAt.HasValue)
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
                    ticket.Tags.Remove(tag.TagId);
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
                        .WithTitle("Tags Removed")
                        .WithDescription($"Tags removed by {staff.Mention}:\n" +
                                         string.Join("\n", removedTags.Select(t => $"• **{t.Name}**")))
                        .WithColor(Color.Orange)
                        .WithCurrentTimestamp()
                        .Build();

                    await channel.SendMessageAsync(embed: embed);
                }
            }

            await ctx.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error removing tags from ticket {TicketId}", ticket.Id);
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
        await using var ctx = await _db.GetContextAsync();
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
        await using var ctx = await _db.GetContextAsync();

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
        await using var ctx = await _db.GetContextAsync();
        var ticket = await ctx.Tickets
            .FirstOrDefaultAsync(t => t.ChannelId == channelId && t.GuildId == guild.Id);

        if (ticket == null || ticket.ClosedAt.HasValue || ticket.Tags == null)
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
                ticket.Tags.Remove(tagId);
            }

            ticket.LastActivityAt = DateTime.UtcNow;

            var channel = await guild.GetTextChannelAsync(channelId);
            if (channel != null)
            {
                var embed = new EmbedBuilder()
                    .WithTitle("Tags Removed")
                    .WithDescription($"Tags removed by {staff.Mention}:\n" +
                                     string.Join("\n", tags.Select(t => $"• **{t.Name}**")))
                    .WithColor(Color.Orange)
                    .WithCurrentTimestamp()
                    .Build();

                await channel.SendMessageAsync(embed: embed);
            }

            await ctx.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error removing tags from ticket {TicketId}", ticket.Id);
            return false;
        }
    }

    /// <summary>
    ///     Blacklists a user from creating tickets in the guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="userId">The ID of the user to blacklist.</param>
    /// <param name="reason">The optional reason for the blacklist.</param>
    /// <returns>True if the user was successfully blacklisted, false if they were already blacklisted.</returns>
    public async Task<bool> BlacklistUser(IGuild guild, ulong userId, string reason = null)
    {
        await using var ctx = await _db.GetContextAsync();
        var settings = await ctx.GuildTicketSettings
            .FirstOrDefaultAsync(s => s.GuildId == guild.Id);

        if (settings == null)
        {
            settings = new GuildTicketSettings
            {
                GuildId = guild.Id, BlacklistedUsers = []
            };
            ctx.GuildTicketSettings.Add(settings);
        }

        if (settings.BlacklistedUsers.Contains(userId))
            return false;

        try
        {
            settings.BlacklistedUsers.Add(userId);
            await ctx.SaveChangesAsync();

            // Log the blacklist if logging is enabled
            if (settings.LogChannelId.HasValue)
            {
                var logChannel = await guild.GetTextChannelAsync(settings.LogChannelId.Value);
                if (logChannel != null)
                {
                    var embed = new EmbedBuilder()
                        .WithTitle("User Blacklisted")
                        .WithDescription($"<@{userId}> has been blacklisted from creating tickets.")
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
            Log.Error(ex, "Error blacklisting user {UserId} in guild {GuildId}", userId, guild.Id);
            return false;
        }
    }

    /// <summary>
    ///     Blacklists a user from creating tickets using a specific button or select option.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="userId">The ID of the user to blacklist.</param>
    /// <param name="ticketCreatorId">The ID of the button or select option to blacklist from.</param>
    /// <param name="reason">The optional reason for the blacklist.</param>
    /// <returns>True if the user was successfully blacklisted from the ticket type, false otherwise.</returns>
    public async Task<bool> BlacklistUserFromTicketType(IGuild guild, ulong userId, string ticketCreatorId,
        string reason = null)
    {
        await using var ctx = await _db.GetContextAsync();
        var settings = await ctx.GuildTicketSettings
            .FirstOrDefaultAsync(s => s.GuildId == guild.Id);

        if (settings == null)
        {
            settings = new GuildTicketSettings
            {
                GuildId = guild.Id, BlacklistedTypes = new Dictionary<ulong, List<string>>()
            };
            ctx.GuildTicketSettings.Add(settings);
        }

        try
        {
            // Initialize user's blacklisted types if not exists
            if (!settings.BlacklistedTypes.ContainsKey(userId))
            {
                settings.BlacklistedTypes[userId] = [];
            }

            // Check if already blacklisted from this type
            if (settings.BlacklistedTypes[userId].Contains(ticketCreatorId))
                return false;

            // Verify the ticket creator ID exists
            var creatorExists = await ctx.PanelButtons.AnyAsync(b => b.CustomId == ticketCreatorId) ||
                                await ctx.SelectMenuOptions.AnyAsync(o => o.Value == ticketCreatorId);

            if (!creatorExists)
                return false;

            settings.BlacklistedTypes[userId].Add(ticketCreatorId);
            await ctx.SaveChangesAsync();

            // Log the type-specific blacklist if logging is enabled
            if (settings.LogChannelId.HasValue)
            {
                var logChannel = await guild.GetTextChannelAsync(settings.LogChannelId.Value);
                if (logChannel != null)
                {
                    // Get the label of the button or select option
                    var creatorLabel = await GetTicketCreatorLabel(ctx, ticketCreatorId);

                    var embed = new EmbedBuilder()
                        .WithTitle("User Blacklisted from Ticket Type")
                        .WithDescription($"<@{userId}> has been blacklisted from creating {creatorLabel} tickets.")
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
            Log.Error(ex, "Error blacklisting user {UserId} from ticket type {TicketType} in guild {GuildId}",
                userId, ticketCreatorId, guild.Id);
            return false;
        }
    }

    /// <summary>
    ///     Removes a user from the ticket blacklist.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="userId">The ID of the user to unblacklist.</param>
    /// <returns>True if the user was successfully unblacklisted, false if they weren't blacklisted.</returns>
    public async Task<bool> UnblacklistUser(IGuild guild, ulong userId)
    {
        await using var ctx = await _db.GetContextAsync();
        var settings = await ctx.GuildTicketSettings
            .FirstOrDefaultAsync(s => s.GuildId == guild.Id);

        if (settings?.BlacklistedUsers == null || !settings.BlacklistedUsers.Contains(userId))
            return false;

        try
        {
            settings.BlacklistedUsers.Remove(userId);
            // Also clear any type-specific blacklists
            if (settings.BlacklistedTypes.ContainsKey(userId))
            {
                settings.BlacklistedTypes.Remove(userId);
            }

            await ctx.SaveChangesAsync();

            // Log the unblacklist if logging is enabled
            if (settings.LogChannelId.HasValue)
            {
                var logChannel = await guild.GetTextChannelAsync(settings.LogChannelId.Value);
                if (logChannel != null)
                {
                    var embed = new EmbedBuilder()
                        .WithTitle("User Unblacklisted")
                        .WithDescription($"<@{userId}> has been removed from the ticket blacklist.")
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
            Log.Error(ex, "Error unblacklisting user {UserId} in guild {GuildId}", userId, guild.Id);
            return false;
        }
    }

    /// <summary>
    ///     Removes a user's blacklist from a specific button or select option.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="userId">The ID of the user to unblacklist.</param>
    /// <param name="ticketCreatorId">The ID of the button or select option to unblacklist from.</param>
    /// <returns>True if the user was successfully unblacklisted from the ticket type, false otherwise.</returns>
    public async Task<bool> UnblacklistUserFromTicketType(IGuild guild, ulong userId, string ticketCreatorId)
    {
        await using var ctx = await _db.GetContextAsync();
        var settings = await ctx.GuildTicketSettings
            .FirstOrDefaultAsync(s => s.GuildId == guild.Id);

        if (settings?.BlacklistedTypes == null ||
            !settings.BlacklistedTypes.ContainsKey(userId) ||
            !settings.BlacklistedTypes[userId].Contains(ticketCreatorId))
            return false;

        try
        {
            settings.BlacklistedTypes[userId].Remove(ticketCreatorId);

            // Remove the user's entry if no more blacklisted types
            if (settings.BlacklistedTypes[userId].Count == 0)
            {
                settings.BlacklistedTypes.Remove(userId);
            }

            await ctx.SaveChangesAsync();

            // Log the type-specific unblacklist if logging is enabled
            if (!settings.LogChannelId.HasValue) return true;
            var logChannel = await guild.GetTextChannelAsync(settings.LogChannelId.Value);
            if (logChannel == null) return true;
            var creatorLabel = await GetTicketCreatorLabel(ctx, ticketCreatorId);

            var embed = new EmbedBuilder()
                .WithTitle("User Unblacklisted from Ticket Type")
                .WithDescription($"<@{userId}> has been unblacklisted from creating {creatorLabel} tickets.")
                .WithColor(Color.Green)
                .WithCurrentTimestamp()
                .Build();

            await logChannel.SendMessageAsync(embed: embed);

            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error unblacklisting user {UserId} from ticket type {TicketType} in guild {GuildId}",
                userId, ticketCreatorId, guild.Id);
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
        await using var ctx = await _db.GetContextAsync();
        var settings = await ctx.GuildTicketSettings
            .FirstOrDefaultAsync(s => s.GuildId == guildId);

        if (settings == null)
            return new Dictionary<ulong, List<string>>();

        var result = new Dictionary<ulong, List<string>>();

        // Add globally blacklisted users
        foreach (var userId in settings.BlacklistedUsers)
        {
            result[userId] = [];
        }

        // Add type-specific blacklists
        foreach (var kvp in settings.BlacklistedTypes)
        {
            if (result.ContainsKey(kvp.Key))
            {
                result[kvp.Key].AddRange(kvp.Value);
            }
            else
            {
                result[kvp.Key] = kvp.Value;
            }
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
        await using var ctx = await _db.GetContextAsync();

        var cutoffTime = DateTime.UtcNow - inactiveTime;
        var inactiveTickets = await ctx.Tickets
            .Include(t => t.Button)
            .Include(t => t.SelectOption)
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
                        .WithTitle("Ticket Auto-Closed")
                        .WithDescription(
                            $"This ticket has been automatically closed due to {inactiveTime.TotalHours} hours of inactivity.")
                        .WithColor(Color.Red)
                        .WithCurrentTimestamp()
                        .Build();

                    await channel.SendMessageAsync(embed: embed);
                }

                ticket.ClosedAt = DateTime.UtcNow;
                await ctx.SaveChangesAsync();
                closed++;

                // Archive if configured
                if (ticket.Button?.ArchiveCategoryId != null || ticket.SelectOption?.ArchiveCategoryId != null)
                {
                    await ArchiveTicketAsync(ticket);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to close inactive ticket {TicketId}", ticket.Id);
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
        await using var ctx = await _db.GetContextAsync();

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
                Log.Error(ex, "Failed to move ticket channel {ChannelId}", channel.Id);
                failed++;
            }
        }

        if (moved > 0)
        {
            await ctx.SaveChangesAsync();
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
        await using var ctx = await _db.GetContextAsync();

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
                Log.Error(ex, "Failed to add role to ticket {TicketId}", ticket.Id);
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
        await using var ctx = await _db.GetContextAsync();

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
                    .WithTitle("Ticket Transferred")
                    .WithDescription($"This ticket has been transferred to {toStaff.Mention}")
                    .WithColor(Color.Blue)
                    .WithCurrentTimestamp()
                    .Build();

                await channel.SendMessageAsync(embed: embed);
                transferred++;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to transfer ticket {TicketId}", ticket.Id);
                failed++;
            }
        }

        await ctx.SaveChangesAsync();
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
        await using var ctx = await _db.GetContextAsync();
        var panel = await ctx.TicketPanels.FindAsync(panelId);

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
            await ctx.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to update panel {PanelId}", panelId);
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
        await using var ctx = await _db.GetContextAsync();
        var panel = await ctx.TicketPanels
            .Include(p => p.Buttons)
            .Include(p => p.SelectMenus)
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
                    Log.Warning(ex, "Failed to delete old panel message");
                }
            }

            // Create new message
            var newChannel = await guild.GetTextChannelAsync(newChannelId);
            if (newChannel == null)
                return false;

            SmartEmbed.TryParse(panel.EmbedJson, guild.Id, out var embeds, out var plainText, out _);
            var components = new ComponentBuilder();

            // Rebuild buttons
            if (panel.Buttons?.Any() == true)
            {
                var buttonRow = new ActionRowBuilder();
                foreach (var button in panel.Buttons)
                {
                    var btnBuilder = new ButtonBuilder()
                        .WithLabel(button.Label)
                        .WithCustomId(button.CustomId)
                        .WithStyle(button.Style);

                    if (!string.IsNullOrEmpty(button.Emoji))
                        btnBuilder.WithEmote(Emote.Parse(button.Emoji));

                    buttonRow.WithButton(btnBuilder);
                }

                components.AddRow(buttonRow);
            }

            // Rebuild select menus
            if (panel.SelectMenus?.Any() == true)
            {
                foreach (var menu in panel.SelectMenus)
                {
                    var selectBuilder = new SelectMenuBuilder()
                        .WithCustomId(menu.CustomId)
                        .WithPlaceholder(menu.Placeholder);

                    foreach (var option in menu.Options)
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
            await ctx.SaveChangesAsync();

            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to move panel {PanelId}", panelId);
            return false;
        }
    }

    /// <summary>
    ///     Duplicates an existing panel to a new channel.
    /// </summary>
    /// <param name="guild">The guild containing the panel.</param>
    /// <param name="panelId">The ID of the panel to duplicate.</param>
    /// <param name="newChannelId">The ID of the channel to create the duplicate in.</param>
    /// <returns>The newly created panel, or null if duplication failed.</returns>
    public async Task<TicketPanel?> DuplicatePanelAsync(IGuild guild, ulong panelId, ulong newChannelId)
    {
        await using var ctx = await _db.GetContextAsync();
        var sourcePanel = await ctx.TicketPanels
            .Include(p => p.Buttons)
            .Include(p => p.SelectMenus)
            .ThenInclude(m => m.Options)
            .FirstOrDefaultAsync(p => p.MessageId == panelId);

        if (sourcePanel == null || sourcePanel.GuildId != guild.Id)
            return null;

        try
        {
            var newChannel = await guild.GetTextChannelAsync(newChannelId);
            if (newChannel == null)
                return null;

            SmartEmbed.TryParse(sourcePanel.EmbedJson, guild.Id, out var embeds, out var plainText, out _);
            var message = await newChannel.SendMessageAsync(plainText, embeds: embeds);

            var newPanel = new TicketPanel
            {
                GuildId = guild.Id,
                ChannelId = newChannelId,
                MessageId = message.Id,
                EmbedJson = sourcePanel.EmbedJson,
                Buttons = [],
                SelectMenus = []
            };

            // Duplicate buttons
            if (sourcePanel.Buttons != null)
            {
                foreach (var sourceButton in sourcePanel.Buttons)
                {
                    var newButton = new PanelButton
                    {
                        Label = sourceButton.Label,
                        Emoji = sourceButton.Emoji,
                        CustomId = $"ticket_btn_{Guid.NewGuid():N}",
                        Style = sourceButton.Style,
                        OpenMessageJson = sourceButton.OpenMessageJson,
                        ModalJson = sourceButton.ModalJson,
                        ChannelNameFormat = sourceButton.ChannelNameFormat,
                        CategoryId = sourceButton.CategoryId,
                        ArchiveCategoryId = sourceButton.ArchiveCategoryId,
                        SupportRoles = [..sourceButton.SupportRoles],
                        ViewerRoles = [..sourceButton.ViewerRoles],
                        AutoCloseTime = sourceButton.AutoCloseTime,
                        RequiredResponseTime = sourceButton.RequiredResponseTime,
                        MaxActiveTickets = sourceButton.MaxActiveTickets,
                        AllowedPriorities = [..sourceButton.AllowedPriorities ?? []],
                        DefaultPriority = sourceButton.DefaultPriority,
                        SaveTranscript = sourceButton.SaveTranscript
                    };
                    newPanel.Buttons.Add(newButton);
                }
            }

            // Duplicate select menus
            if (sourcePanel.SelectMenus != null)
            {
                foreach (var sourceMenu in sourcePanel.SelectMenus)
                {
                    var newMenu = new PanelSelectMenu
                    {
                        CustomId = $"ticket_select_{Guid.NewGuid():N}",
                        Placeholder = sourceMenu.Placeholder,
                        Options = []
                    };

                    // Duplicate menu options
                    if (sourceMenu.Options != null)
                    {
                        foreach (var sourceOption in sourceMenu.Options)
                        {
                            var newOption = new SelectMenuOption
                            {
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
                                AllowedPriorities =
                                    [..sourceOption.AllowedPriorities ?? []],
                                DefaultPriority = sourceOption.DefaultPriority
                            };
                            newMenu.Options.Add(newOption);
                        }
                    }

                    newPanel.SelectMenus.Add(newMenu);
                }
            }

            ctx.TicketPanels.Add(newPanel);
            await ctx.SaveChangesAsync();

            // Update message with components
            await UpdatePanelComponentsAsync(newPanel);

            return newPanel;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to duplicate panel {PanelId}", panelId);
            return null;
        }
    }

    /// <summary>
    ///     Reorders buttons on a panel.
    /// </summary>
    /// <param name="guild">The guild containing the panel.</param>
    /// <param name="panelId">The ID of the panel.</param>
    /// <param name="buttonOrder">List of button IDs in the desired order.</param>
    /// <returns>True if the buttons were successfully reordered, false otherwise.</returns>
    public async Task<bool> ReorderPanelButtonsAsync(IGuild guild, ulong panelId, List<int> buttonOrder)
    {
        await using var ctx = await _db.GetContextAsync();
        var panel = await ctx.TicketPanels
            .Include(p => p.Buttons)
            .FirstOrDefaultAsync(p => p.MessageId == panelId && p.GuildId == guild.Id);

        if (panel == null || panel.Buttons == null)
            return false;

        // Validate that all buttons exist and are part of this panel
        if (!buttonOrder.All(id => panel.Buttons.Any(b => b.Id == id)))
            return false;

        try
        {
            // Create a temporary list to store the new order
            var reorderedButtons = new List<PanelButton>();
            foreach (var buttonId in buttonOrder)
            {
                var button = panel.Buttons.First(b => b.Id == buttonId);
                reorderedButtons.Add(button);
            }

            // Clear and reassign the buttons in the new order
            panel.Buttons.Clear();
            foreach (var button in reorderedButtons)
            {
                panel.Buttons.Add(button);
            }

            await ctx.SaveChangesAsync();
            await UpdatePanelComponentsAsync(panel);

            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to reorder buttons for panel {PanelId}", panelId);
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
        await using var ctx = await _db.GetContextAsync();
        var button = await ctx.PanelButtons
            .Include(b => b.Panel)
            .FirstOrDefaultAsync(b => b.Id == buttonId && b.Panel.GuildId == guild.Id);

        if (button == null)
            return false;

        try
        {
            button.RequiredResponseTime = responseTime;
            await ctx.SaveChangesAsync();

            // Notify support roles of the change
            if (button.SupportRoles?.Any() == true)
            {
                var channel = await guild.GetTextChannelAsync(button.Panel.ChannelId);
                if (channel != null)
                {
                    var embed = new EmbedBuilder()
                        .WithTitle("Response Time Updated")
                        .WithDescription(
                            $"The required response time for tickets created with the '{button.Label}' button " +
                            $"has been updated to {responseTime?.TotalHours ?? 0} hours.")
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
            Log.Error(ex, "Failed to update response time for button {ButtonId}", buttonId);
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
        await using var ctx = await _db.GetContextAsync();
        var button = await ctx.PanelButtons
            .Include(b => b.Panel)
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
                        button.Style = (ButtonStyle)setting.Value;
                        break;
                    case "categoryid":
                        button.CategoryId = (ulong?)setting.Value;
                        break;
                    case "archivecategoryid":
                        button.ArchiveCategoryId = (ulong?)setting.Value;
                        break;
                    case "supportroles":
                        button.SupportRoles = (List<ulong>)setting.Value;
                        break;
                    case "viewerroles":
                        button.ViewerRoles = (List<ulong>)setting.Value;
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
                        button.AllowedPriorities = (List<string>)setting.Value;
                        break;
                    case "defaultpriority":
                        button.DefaultPriority = (string)setting.Value;
                        break;
                    case "savetranscript":
                        button.SaveTranscript = (bool)setting.Value;
                        break;
                }
            }

            await ctx.SaveChangesAsync();
            await UpdatePanelComponentsAsync(button.Panel);

            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to update settings for button {ButtonId}", buttonId);
            return false;
        }
    }

    private static async Task<string> GetTicketCreatorLabel(MewdekoContext ctx, string creatorId)
    {
        // Try to find as button first
        var button = await ctx.PanelButtons
            .FirstOrDefaultAsync(b => b.CustomId == creatorId);

        if (button != null)
            return $"{button.Label} (Button)";

        // Try to find as select option
        var option = await ctx.SelectMenuOptions
            .FirstOrDefaultAsync(o => o.Value == creatorId);

        return option != null ? $"{option.Label} (Select Option)" : "Unknown";
    }
}