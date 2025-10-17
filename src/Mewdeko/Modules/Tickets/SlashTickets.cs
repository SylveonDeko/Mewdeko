using System.Text;
using System.Text.Json;
using Discord.Interactions;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Common.Modals;
using Mewdeko.Modules.Tickets.Services;

namespace Mewdeko.Modules.Tickets;

/// <summary>
///     Provides commands for managing the ticket system.
/// </summary>
[Group("tickets", "Manage the ticket system.")]
public partial class TicketsSlash : MewdekoSlashModuleBase<TicketService>
{
    private readonly IDataCache cache;
    private readonly InteractiveService interactivity;
    private readonly ILogger<TicketsSlash> logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="TicketsSlash" /> class.
    /// </summary>
    /// <param name="interactivity">The interactive service.</param>
    /// <param name="cache">The cache service.</param>
    /// <param name="logger">The logger instance for structured logging.</param>
    public TicketsSlash(InteractiveService interactivity, IDataCache cache, ILogger<TicketsSlash> logger)
    {
        this.interactivity = interactivity;
        this.cache = cache;
        this.logger = logger;
    }

    /// <summary>
    ///     Creates a new ticket panel.
    /// </summary>
    /// <param name="channel">The channel to create the panel in.</param>
    [SlashCommand("createpanel", "Creates a new ticket panel")]
    [SlashUserPerm(GuildPermission.Administrator)]
    [RequireContext(ContextType.Guild)]
    public Task CreatePanel(
        [Summary("channel", "Channel to create the panel in")]
        ITextChannel channel
    )
    {
        return RespondWithModalAsync<PanelCreationModal>($"create_panel:{channel.Id}");
    }

    /// <summary>
    ///     Lists all components of a specific ticket panel
    /// </summary>
    /// <remarks>
    ///     This command provides detailed information about all components on a panel, including:
    ///     - Button and select menu IDs
    ///     - Component configurations
    ///     - Associated categories and roles
    ///     - Modal and custom message settings
    /// </remarks>
    /// <param name="panelId">The message ID of the panel to list components from</param>
    [SlashCommand("listpanel", "Lists all components of a ticket panel")]
    [RequireContext(ContextType.Guild)]
    public async Task ListPanel(
        [Summary("panel-id", "Message ID of the panel to list")]
        ulong panelId)
    {
        try
        {
            var buttons = await Service.GetPanelButtonsAsync(panelId);
            var menus = await Service.GetPanelSelectMenusAsync(panelId);

            var embed = new EmbedBuilder()
                .WithTitle(Strings.PanelComponents(ctx.Guild.Id))
                .WithOkColor();

            if (buttons.Any())
            {
                var buttonText = new StringBuilder();
                foreach (var button in buttons)
                {
                    buttonText.AppendLine($"**Button ID: {button.Id}**")
                        .AppendLine($"└ Label: {button.Label}")
                        .AppendLine($"└ Style: {button.Style}")
                        .AppendLine($"└ Custom ID: {button.CustomId}")
                        .AppendLine($"└ Has Modal: {(button.HasModal ? "Yes" : "No")}")
                        .AppendLine($"└ Has Custom Open Message: {(button.HasCustomOpenMessage ? "Yes" : "No")}")
                        .AppendLine($"└ Category: {(button.CategoryId.HasValue ? $"<#{button.CategoryId}>" : "None")}")
                        .AppendLine(
                            $"└ Archive Category: {(button.ArchiveCategoryId.HasValue ? $"<#{button.ArchiveCategoryId}>" : "None")}")
                        .AppendLine(
                            $"└ Support Roles: {string.Join(", ", button.SupportRoles.Select(r => $"<@&{r}>"))}")
                        .AppendLine($"└ Viewer Roles: {string.Join(", ", button.ViewerRoles.Select(r => $"<@&{r}>"))}")
                        .AppendLine();
                }

                embed.AddField("Buttons", buttonText.ToString());
            }

            if (menus.Any())
            {
                var menuText = new StringBuilder();
                foreach (var menu in menus)
                {
                    menuText.AppendLine($"**Menu ID: {menu.Id}**")
                        .AppendLine($"└ Custom ID: {menu.CustomId}")
                        .AppendLine($"└ Placeholder: {menu.Placeholder}")
                        .AppendLine("└ Options:");

                    foreach (var option in menu.Options)
                    {
                        menuText.AppendLine($"  **Option ID: {option.Id}**")
                            .AppendLine($"  └ Label: {option.Label}")
                            .AppendLine($"  └ Value: {option.Value}")
                            .AppendLine($"  └ Description: {option.Description}")
                            .AppendLine($"  └ Has Modal: {(option.HasModal ? "Yes" : "No")}")
                            .AppendLine($"  └ Has Custom Open Message: {(option.HasCustomOpenMessage ? "Yes" : "No")}")
                            .AppendLine(
                                $"  └ Category: {(option.CategoryId.HasValue ? $"<#{option.CategoryId}>" : "None")}")
                            .AppendLine(
                                $"  └ Archive Category: {(option.ArchiveCategoryId.HasValue ? $"<#{option.ArchiveCategoryId}>" : "None")}");
                    }

                    menuText.AppendLine();
                }

                embed.AddField("Select Menus", menuText.ToString());
            }

            if (!buttons.Any() && !menus.Any())
            {
                embed.WithDescription(Strings.NoPanelComponents(ctx.Guild.Id));
            }

            await RespondAsync(embed: embed.Build());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing panel components for panel {PanelId}", panelId);
            await RespondAsync("An error occurred while listing panel components.", ephemeral: true);
        }
    }

    /// <summary>
    ///     Lists all ticket panels in the guild
    /// </summary>
    /// <remarks>
    ///     This command displays paginated information about all ticket panels in the server, including:
    ///     - Channel locations
    ///     - Message IDs
    ///     - Component configurations
    ///     - Associated categories and roles
    ///     Each panel's information is shown on its own page for easy navigation.
    /// </remarks>
    [SlashCommand("listpanels", "Lists all ticket panels in the server")]
    [RequireContext(ContextType.Guild)]
    public async Task ListPanels()
    {
        try
        {
            var panels = await Service.GetAllPanelsAsync(Context.Guild.Id);

            if (!panels.Any())
            {
                await RespondAsync("No ticket panels found in this server.", ephemeral: true);
                return;
            }

            var paginator = new LazyPaginatorBuilder()
                .AddUser(ctx.User)
                .WithPageFactory(PageFactory)
                .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                .WithMaxPageIndex(panels.Count / 5)
                .WithDefaultEmotes()
                .WithActionOnCancellation(ActionOnStop.DeleteMessage)
                .Build();

            await interactivity.SendPaginatorAsync(paginator, Context.Interaction, TimeSpan.FromMinutes(60));

            async Task<PageBuilder> PageFactory(int page)
            {
                await Task.CompletedTask;
                var pagePanels = panels.Skip(5 * page).Take(5);
                var pageBuilder = new PageBuilder()
                    .WithTitle(Strings.TicketPanels(ctx.Guild.Id))
                    .WithOkColor();

                foreach (var panel in pagePanels)
                {
                    var channel = await Context.Guild.GetChannelAsync(panel.ChannelId) as ITextChannel;
                    var fieldBuilder = new StringBuilder();

                    fieldBuilder.AppendLine($"Channel: #{channel?.Name ?? "deleted-channel"}");

                    if (panel.Buttons.Any())
                    {
                        fieldBuilder.AppendLine("\n**Buttons:**");
                        foreach (var button in panel.Buttons)
                        {
                            fieldBuilder.AppendLine($"• ID: {button.Id} | Label: {button.Label}")
                                .AppendLine($"  Style: {button.Style}")
                                .AppendLine(
                                    $"  Category: {(button.CategoryId.HasValue ? $"<#{button.CategoryId}>" : "None")}")
                                .AppendLine(
                                    $"  Support Roles: {string.Join(", ", button.SupportRoles.Select(r => $"<@&{r}>"))}");
                        }
                    }

                    if (panel.SelectMenus.Any())
                    {
                        fieldBuilder.AppendLine("\n**Select Menus:**");
                        foreach (var menu in panel.SelectMenus)
                        {
                            fieldBuilder.AppendLine($"• ID: {menu.Id} | Options: {menu.Options.Count}");
                            foreach (var option in menu.Options)
                            {
                                fieldBuilder.AppendLine($"  - Option ID: {option.Id} | Label: {option.Label}");
                            }
                        }
                    }

                    pageBuilder.AddField($"Panel ID: {panel.MessageId}", fieldBuilder.ToString());
                }

                return pageBuilder;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing panels");
            await RespondAsync("An error occurred while listing panels.", ephemeral: true);
        }
    }

    /// <summary>
    ///     Deletes a ticket panel.
    /// </summary>
    /// <param name="panelId">The ID of the panel to delete.</param>
    /// <param name="force">Whether to force deletion even if there are active tickets.</param>
    [SlashCommand("deletepanel", "Deletes a ticket panel")]
    [SlashUserPerm(GuildPermission.Administrator)]
    [RequireContext(ContextType.Guild)]
    public async Task DeletePanel(
        [Summary("panel-id", "Message ID of the panel to delete")]
        ulong panelId,
        [Summary("force", "Force delete even if there are active tickets referencing it")]
        bool force = false)
    {
        var (success, error, activeTickets, deletedTickets) = await Service.DeletePanelAsync(panelId, ctx.Guild, force);

        if (success)
        {
            var totalCleared = (activeTickets?.Count ?? 0) + (deletedTickets?.Count ?? 0);
            if (totalCleared > 0)
            {
                await RespondAsync(
                    Strings.TicketPanelDeletedWithReferences(ctx.Guild.Id, panelId,
                        activeTickets?.Count ?? 0, deletedTickets?.Count ?? 0), ephemeral: true);
            }
            else
            {
                await RespondAsync(Strings.TicketPanelDeleted(ctx.Guild.Id, panelId), ephemeral: true);
            }
        }
        else
        {
            if (activeTickets?.Any() == true || deletedTickets?.Any() == true)
            {
                var embed = new EmbedBuilder()
                    .WithTitle(Strings.CannotDeletePanelTitle(ctx.Guild.Id))
                    .WithDescription(Strings.CannotDeletePanelDescription(ctx.Guild.Id, activeTickets?.Count ?? 0))
                    .WithErrorColor();

                if (activeTickets?.Any() == true)
                {
                    embed.AddField(Strings.ActiveTickets(ctx.Guild.Id),
                        string.Join(", ", activeTickets.Select(id => $"#{id}")), true);
                }

                if (deletedTickets?.Any() == true)
                {
                    embed.AddField(Strings.SoftDeletedTickets(ctx.Guild.Id),
                        string.Join(", ", deletedTickets.Select(id => $"#{id}")), true);
                }

                embed.AddField(Strings.Options(ctx.Guild.Id),
                    "Use the `force` parameter to delete anyway (this will unlink all ticket references)");

                await RespondAsync(embed: embed.Build(), ephemeral: true);
            }
            else
            {
                await RespondAsync(error, ephemeral: true);
            }
        }
    }

    /// <summary>
    ///     Claims ownership of a ticket as a staff member.
    /// </summary>
    /// <param name="channel">Optional channel to claim. If not provided, uses the current channel.</param>
    /// <remarks>
    ///     Staff members can use this command to claim responsibility for a ticket.
    ///     This shows other staff members who is handling the ticket.
    /// </remarks>
    [SlashCommand("claim", "Claims a ticket")]
    [RequireContext(ContextType.Guild)]
    public async Task ClaimTicket(
        [Summary("channel", "The ticket channel to claim")]
        ITextChannel? channel = null
    )
    {
        channel ??= ctx.Channel as ITextChannel;
        if (channel == null)
        {
            await RespondAsync("This command must be used in a text channel.", ephemeral: true);
            return;
        }

        try
        {
            var success = await Service.ClaimTicket(ctx.Guild, channel.Id, ctx.User as IGuildUser);
            if (success)
                await RespondAsync("Ticket claimed successfully!", ephemeral: true);
            else
                await RespondAsync("Failed to claim ticket. It may already be claimed or closed.", ephemeral: true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error claiming ticket in channel {ChannelId}", channel.Id);
            await RespondAsync("An error occurred while claiming the ticket.", ephemeral: true);
        }
    }

    /// <summary>
    ///     Handles the close button interaction for tickets.
    /// </summary>
    /// <remarks>
    ///     This method is called when a user clicks the close button on a ticket.
    ///     It will close the ticket and notify the user of the result.
    ///     The button uses the custom ID "ticket_close".
    /// </remarks>
    [ComponentInteraction("ticket_close", true)]
    public async Task HandleTicketClose()
    {
        try
        {
            var success = await Service.CloseTicket(ctx.Guild, ctx.Channel.Id);
            if (success)
                await RespondAsync("Ticket closed successfully!", ephemeral: true);
            else
                await RespondAsync("Failed to close ticket. It may already be closed.", ephemeral: true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error closing ticket in channel {ChannelId}", ctx.Channel.Id);
            await RespondAsync("An error occurred while closing the ticket.", ephemeral: true);
        }
    }

    /// <summary>
    ///     Closes a ticket channel.
    /// </summary>
    /// <param name="channel">Optional channel to close. If not provided, uses the current channel.</param>
    /// <remarks>
    ///     This command allows staff to close tickets either in the current channel
    ///     or in a specified channel. Closed tickets may be moved to an archive category
    ///     if one is configured.
    /// </remarks>
    [SlashCommand("close", "Closes a ticket")]
    [RequireContext(ContextType.Guild)]
    public async Task CloseTicket(
        [Summary("channel", "The ticket channel to close")]
        ITextChannel? channel = null
    )
    {
        channel ??= ctx.Channel as ITextChannel;
        if (channel == null)
        {
            await RespondAsync("This command must be used in a text channel.", ephemeral: true);
            return;
        }

        try
        {
            var success = await Service.CloseTicket(ctx.Guild, channel.Id);
            if (success)
                await RespondAsync("Ticket closed successfully!", ephemeral: true);
            else
                await RespondAsync("Failed to close ticket. It may already be closed.", ephemeral: true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error closing ticket in channel {ChannelId}", channel.Id);
            await RespondAsync("An error occurred while closing the ticket.", ephemeral: true);
        }
    }

    /// <summary>
    ///     Removes a staff member's claim from a ticket.
    /// </summary>
    /// <param name="ticketId">The ID of the ticket to unclaim.</param>
    [SlashCommand("unclaim", "Removes a staff member's claim from a ticket")]
    [RequireContext(ContextType.Guild)]
    public async Task UnclaimTicket(
        [Summary("ticket-id", "ID of the ticket to unclaim")]
        int ticketId)
    {
        try
        {
            var ticket = await Service.GetTicketAsync(ticketId);
            if (ticket == null)
            {
                await RespondAsync("Ticket not found!", ephemeral: true);
                return;
            }

            // Verify permissions
            var guildUser = ctx.User as IGuildUser;
            var channel = await ctx.Guild.GetChannelAsync(ticket.ChannelId) as ITextChannel;

            if (channel == null)
            {
                await RespondAsync("Ticket channel not found!", ephemeral: true);
                return;
            }

            var permissions = guildUser.GetPermissions(channel);
            if (!permissions.ManageChannel && !guildUser.GuildPermissions.Administrator)
            {
                await RespondAsync("You don't have permission to unclaim tickets!", ephemeral: true);
                return;
            }

            await Service.UnclaimTicketAsync(ticket, guildUser);
            await RespondAsync("Ticket unclaimed successfully!", ephemeral: true);
        }
        catch (InvalidOperationException ex)
        {
            await RespondAsync($"Error: {ex.Message}", ephemeral: true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error unclaiming ticket {TicketId}", ticketId);
            await RespondAsync("An error occurred while unclaiming the ticket.", ephemeral: true);
        }
    }

    /// <summary>
    ///     Adds a note to a ticket.
    /// </summary>
    /// <param name="ticket">The ID of the ticket.</param>
    [SlashCommand("note", "Adds a note to a ticket")]
    [RequireContext(ContextType.Guild)]
    public Task AddNote(
        [Summary("ticket", "The ticket to add a note to")]
        int ticket
    )
    {
        return RespondWithModalAsync<TicketNoteModal>($"ticket_note:{ticket}");
    }

    /// <summary>
    ///     Archives a ticket.
    /// </summary>
    /// <param name="ticket">The ID of the ticket to archive.</param>
    [SlashCommand("archive", "Archives a ticket")]
    [RequireContext(ContextType.Guild)]
    public async Task ArchiveTicket(
        [Summary("ticket", "The ticket to archive")]
        int ticket
    )
    {
        var ticketObj = await Service.GetTicketAsync(ticket);
        if (ticketObj == null)
        {
            await RespondAsync("Ticket not found!", ephemeral: true);
            return;
        }

        await Service.ArchiveTicketAsync(ticketObj);
        await RespondAsync("Ticket archived successfully!", ephemeral: true);
    }

    /// <summary>
    ///     Sets a ticket's priority.
    /// </summary>
    /// <param name="ticket">The ID of the ticket.</param>
    [SlashCommand("priority", "Sets a ticket's priority")]
    [RequireContext(ContextType.Guild)]
    public Task SetPriority(
        [Summary("ticket", "The ticket to set priority for")]
        int ticket
    )
    {
        return RespondWithModalAsync<TicketPriorityModal>($"ticket_priority:{ticket}");
    }

    #region Select Menu Interaction Handlers

    /// <summary>
    ///     Handles ticket creation through select menu interactions using wildcard pattern matching.
    /// </summary>
    /// <param name="menuId">The unique identifier portion of the select menu's custom ID.</param>
    /// <param name="values">Array of selected option values from the select menu.</param>
    /// <remarks>
    ///     This method handles all ticket creation select menus using a wildcard pattern.
    ///     If the selected option has a modal configuration, it will display the modal.
    ///     Otherwise, it creates the ticket immediately.
    /// </remarks>
    [ComponentInteraction("ticket_select_*", true)]
    public async Task HandleTicketSelectMenu(string menuId, string[] values)
    {
        try
        {
            var selectedValue = values.FirstOrDefault();
            if (string.IsNullOrEmpty(selectedValue))
            {
                await RespondAsync("No option was selected.", ephemeral: true);
                return;
            }

            var menu = await Service.GetSelectMenuAsync($"ticket_select_{menuId}");
            if (menu == null)
            {
                await RespondAsync("This ticket menu is no longer available.", ephemeral: true);
                return;
            }

            var option = menu.SelectMenuOptions.FirstOrDefault(o => o.Value == selectedValue);
            if (option == null)
            {
                await RespondAsync("The selected option is no longer available.", ephemeral: true);
                return;
            }

            // Update to clear the select menu selection (rebuilds components without changing content)
            if (!string.IsNullOrEmpty(option.ModalJson))
            {
                await Service.HandleModalCreation(
                    ctx.User as IGuildUser,
                    option.ModalJson,
                    $"ticket_modal_select:{option.Id}",
                    ctx.Interaction
                );
            }
            else
            {
                try
                {
                    await Service.CreateTicketAsync(ctx.Guild, ctx.User, option: option);
                    await FollowupAsync($"{Config.SuccessEmote} Ticket created successfully!", ephemeral: true);
                    await (Context.Interaction as IComponentInteraction).Message.ModifyAsync(x =>
                    {
                        // Don't change content or embeds - just trigger component rebuild to clear selection
                        x.Components = x.Components;
                    });
                }
                catch (InvalidOperationException ex)
                {
                    await FollowupAsync($"{Config.ErrorEmote} {ex.Message}", ephemeral: true);
                }
            }
        }
        catch (InvalidOperationException ex)
        {
            await RespondAsync(ex.Message, ephemeral: true);
            await (Context.Interaction as IComponentInteraction).Message.ModifyAsync(x =>
            {
                // Don't change content or embeds - just trigger component rebuild to clear selection
                x.Components = x.Components;
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling select menu interaction for menuId {MenuId}", menuId);
            await RespondAsync("An error occurred while creating your ticket.", ephemeral: true);
        }
    }

    #endregion

    /// <summary>
    ///     Group for managing ticket panels.
    /// </summary>
    [Group("panel", "Manage ticket panels")]
    public class PanelCommands : MewdekoSlashModuleBase<TicketService>
    {
        private readonly IDataCache cache;

        /// <summary>
        ///     Initializes a new instance of the <see cref="PanelCommands" /> class.
        /// </summary>
        /// <param name="cache">The cache service.</param>
        public PanelCommands(IDataCache cache)
        {
            this.cache = cache;
        }

        /// <summary>
        ///     Adds a button to a panel.
        /// </summary>
        /// <param name="panelId">The ID of the panel.</param>
        [SlashCommand("addbutton", "Adds a button to a panel")]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task AddButton(
            [Summary("panel", "The panel to add a button to")]
            string panelId
        )
        {
            var components = new ComponentBuilder()
                .WithButton("Primary", $"btn_style:{panelId}:primary")
                .WithButton("Success", $"btn_style:{panelId}:success", ButtonStyle.Success)
                .WithButton("Secondary", $"btn_style:{panelId}:secondary", ButtonStyle.Secondary)
                .WithButton("Danger", $"btn_style:{panelId}:danger", ButtonStyle.Danger);

            await RespondAsync("Choose the button style:", components: components.Build());
        }

        /// <summary>
        ///     Adds a select menu to a panel.
        /// </summary>
        /// <param name="panelId">The ID of the panel.</param>
        [SlashCommand("addmenu", "Adds a select menu to a panel")]
        [SlashUserPerm(GuildPermission.Administrator)]
        public Task AddSelectMenu(
            [Summary("panel", "The panel to add a menu to")]
            string panelId
        )
        {
            return RespondWithModalAsync<SelectMenuCreationModal>($"create_menu:{panelId}");
        }
    }

    /// <summary>
    ///     Group for managing ticket settings.
    /// </summary>
    [Group("settings", "Manage ticket settings")]
    public class SettingsCommands : MewdekoSlashModuleBase<TicketService>
    {
        /// <summary>
        ///     Sets the transcript channel.
        /// </summary>
        /// <param name="channel">The channel for ticket transcripts.</param>
        [SlashCommand("transcripts", "Sets the transcript channel")]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task SetTranscriptChannel(
            [Summary("channel", "Channel for ticket transcripts")]
            ITextChannel channel
        )
        {
            await Service.SetTranscriptChannelAsync(ctx.Guild.Id, channel.Id);
            await RespondAsync($"Transcript channel set to {channel.Mention}");
        }

        /// <summary>
        ///     Sets the log channel.
        /// </summary>
        /// <param name="channel">The channel for ticket logs.</param>
        [SlashCommand("logs", "Sets the log channel")]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task SetLogChannel(
            [Summary("channel", "Channel for ticket logs")]
            ITextChannel channel
        )
        {
            await Service.SetLogChannelAsync(ctx.Guild.Id, channel.Id);
            await RespondAsync($"Log channel set to {channel.Mention}");
        }
    }

    /// <summary>
    ///     Provides commands for managing ticket cases and their relationships with tickets.
    /// </summary>
    [Group("cases", "Manage ticket cases")]
    public class CaseManagementCommands : MewdekoSlashModuleBase<TicketService>
    {
        private readonly InteractiveService _interactivity;

        /// <summary>
        ///     Initializes a new instance of the <see cref="CaseManagementCommands" /> class.
        /// </summary>
        /// <param name="interactivity">The interactive service for handling paginated responses.</param>
        public CaseManagementCommands(InteractiveService interactivity)
        {
            _interactivity = interactivity;
        }

        /// <summary>
        ///     Creates a new case for organizing tickets.
        /// </summary>
        [SlashCommand("create", "Creates a new case")]
        [RequireContext(ContextType.Guild)]
        public Task CreateCase()
        {
            return RespondWithModalAsync<CaseCreationModal>("create_case");
        }

        /// <summary>
        ///     Views details of a specific case or lists all cases if no ID is provided.
        /// </summary>
        /// <param name="caseId">Optional ID of the specific case to view. If not provided, lists all cases.</param>
        [SlashCommand("view", "Views case details")]
        [RequireContext(ContextType.Guild)]
        public async Task ViewCase(
            [Summary("case-id", "ID of the case to view")]
            int? caseId = null)
        {
            if (caseId.HasValue)
            {
                var ticketCase = await Service.GetCaseAsync(caseId.Value);
                if (ticketCase == null)
                {
                    await RespondAsync("Case not found!", ephemeral: true);
                    return;
                }

                var creator = await ctx.Guild.GetUserAsync(ticketCase.CreatedBy);
                var eb = new EmbedBuilder()
                    .WithTitle(Strings.TicketCase(ctx.Guild.Id, ticketCase.Id.ToString(), ticketCase.Title))
                    .WithDescription(ticketCase.Description)
                    .AddField("Created By", creator?.Mention ?? "Unknown", true)
                    .AddField("Created At", ticketCase.CreatedAt.ToString("g"), true)
                    .AddField("Status", ticketCase.ClosedAt.HasValue ? "Closed" : "Open", true)
                    .WithOkColor();

                if (ticketCase.Tickets.Any())
                {
                    eb.AddField("Linked Tickets",
                        string.Join("\n", ticketCase.Tickets.Select(t => $"#{t.Id}")));
                }

                if (ticketCase.CaseNotes.Any())
                {
                    eb.AddField("Notes",
                        string.Join("\n\n", ticketCase.CaseNotes
                            .OrderByDescending(n => n.CreatedAt)
                            .Take(5)
                            .Select(n => $"{n.Content}\n- <@{n.AuthorId}> at {n.CreatedAt:g}")));
                }

                await RespondAsync(embed: eb.Build());
            }
            else
            {
                await ListAllCases();
            }
        }

        /// <summary>
        ///     Links one or more tickets to an existing case.
        /// </summary>
        /// <param name="caseId">The ID of the case to link tickets to.</param>
        /// <param name="ticketIds">Comma-separated list of ticket IDs to link to the case.</param>
        [SlashCommand("link", "Links tickets to a case")]
        [RequireContext(ContextType.Guild)]
        public async Task LinkTickets(
            [Summary("case", "The case to link tickets to")]
            int caseId,
            [Summary("tickets", "Comma-separated list of ticket IDs")]
            string ticketIds)
        {
            var ticketCase = await Service.GetCaseAsync(caseId);
            if (ticketCase == null)
            {
                await RespondAsync("Case not found!", ephemeral: true);
                return;
            }

            var tickets = await Service.GetTicketsAsync(ticketIds.Split(',').Select(int.Parse));
            await Service.LinkTicketsToCase(caseId, tickets);
            await RespondAsync($"Successfully linked {tickets.Count} ticket(s) to case #{caseId}");
        }

        /// <summary>
        ///     Unlinks one or more tickets from their associated case.
        /// </summary>
        /// <param name="caseId">The ID of the case to unlink tickets from.</param>
        /// <param name="ticketIds">Comma-separated list of ticket IDs to unlink.</param>
        [SlashCommand("unlink", "Unlinks tickets from a case")]
        [RequireContext(ContextType.Guild)]
        public async Task UnlinkTickets(
            [Summary("case", "The case to unlink tickets from")]
            int caseId,
            [Summary("tickets", "Comma-separated list of ticket IDs")]
            string ticketIds)
        {
            var ticketCase = await Service.GetCaseAsync(caseId);
            if (ticketCase == null)
            {
                await RespondAsync("Case not found!", ephemeral: true);
                return;
            }

            var tickets = await Service.GetTicketsAsync(ticketIds.Split(',').Select(int.Parse));
            await Service.UnlinkTicketsFromCase(tickets);
            await RespondAsync($"Successfully unlinked {tickets.Count} ticket(s) from case #{caseId}");
        }

        /// <summary>
        ///     Adds a note to an existing case.
        /// </summary>
        /// <param name="caseId">The ID of the case to add a note to.</param>
        [SlashCommand("note", "Adds a note to a case")]
        [RequireContext(ContextType.Guild)]
        public Task AddNote(
            [Summary("case-id", "ID of the case")] int caseId)
        {
            return RespondWithModalAsync<CaseNoteModal>($"case_note:{caseId}");
        }

        /// <summary>
        ///     Closes an open case.
        /// </summary>
        /// <param name="caseId">The ID of the case to close.</param>
        [SlashCommand("close", "Closes a case")]
        [RequireContext(ContextType.Guild)]
        public async Task CloseCase(
            [Summary("case-id", "ID of the case to close")]
            int caseId)
        {
            var ticketCase = await Service.GetCaseAsync(caseId);
            if (ticketCase == null)
            {
                await RespondAsync("Case not found!", ephemeral: true);
                return;
            }

            if (ticketCase.ClosedAt.HasValue)
            {
                await RespondAsync("This case is already closed!", ephemeral: true);
                return;
            }

            await Service.CloseCaseAsync(ticketCase);
            await RespondAsync($"Case #{caseId} closed successfully!");
        }

        /// <summary>
        ///     Reopens a previously closed case.
        /// </summary>
        /// <param name="caseId">The ID of the case to reopen.</param>
        [SlashCommand("reopen", "Reopens a closed case")]
        [RequireContext(ContextType.Guild)]
        public async Task ReopenCase(
            [Summary("case-id", "ID of the case to reopen")]
            int caseId)
        {
            var ticketCase = await Service.GetCaseAsync(caseId);
            if (ticketCase == null)
            {
                await RespondAsync("Case not found!", ephemeral: true);
                return;
            }

            if (!ticketCase.ClosedAt.HasValue)
            {
                await RespondAsync("This case is already open!", ephemeral: true);
                return;
            }

            await Service.ReopenCaseAsync(ticketCase);
            await RespondAsync($"Case #{caseId} reopened successfully!");
        }

        /// <summary>
        ///     Updates the title and/or description of an existing case.
        /// </summary>
        /// <param name="caseId">The ID of the case to update.</param>
        [SlashCommand("update", "Updates a case's details")]
        [RequireContext(ContextType.Guild)]
        public Task UpdateCase(
            [Summary("case-id", "ID of the case to update")]
            int caseId)
        {
            return RespondWithModalAsync<CaseUpdateModal>($"case_update:{caseId}");
        }

        /// <summary>
        ///     Displays a paginated list of all cases in the guild.
        /// </summary>
        private async Task ListAllCases()
        {
            var cases = await Service.GetGuildCasesAsync(ctx.Guild.Id);
            var paginator = new LazyPaginatorBuilder()
                .AddUser(ctx.User)
                .WithPageFactory(PageFactory)
                .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                .WithMaxPageIndex(cases.Count / 10)
                .WithDefaultEmotes()
                .Build();

            await _interactivity.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60));

            async Task<PageBuilder> PageFactory(int page)
            {
                var pageBuilder = new PageBuilder()
                    .WithTitle(Strings.Cases(ctx.Guild.Id))
                    .WithOkColor();

                foreach (var ticketCase in cases.Skip(page * 10).Take(10))
                {
                    var creator = await ctx.Guild.GetUserAsync(ticketCase.CreatedBy);
                    pageBuilder.AddField(Strings.TicketCase(ctx.Guild.Id, ticketCase.Id.ToString(), ticketCase.Title),
                        $"Created by: {creator?.Mention ?? "Unknown"}\n" +
                        $"Status: {(ticketCase.ClosedAt.HasValue ? "Closed" : "Open")}\n" +
                        $"Linked Tickets: {ticketCase.Tickets.Count()}\n" +
                        $"Notes: {ticketCase.CaseNotes.Count()}");
                }

                return pageBuilder;
            }
        }
    }


    #region Modal Handlers

    /// <summary>
    ///     Handles the submission of case notes through the modal.
    /// </summary>
    /// <param name="caseId">The ID of the case being noted.</param>
    /// <param name="modal">The modal containing the note content.</param>
    [ModalInteraction("case_note:*", true)]
    public async Task HandleCaseNote(string caseId, CaseNoteModal modal)
    {
        var note = await Service.AddCaseNoteAsync(
            int.Parse(caseId),
            ctx.User.Id,
            modal.Content);

        if (note != null)
            await RespondAsync("Note added successfully!", ephemeral: true);
        else
            await RespondAsync("Failed to add note. The case may not exist.", ephemeral: true);
    }

    /// <summary>
    ///     Handles panel creation modal submission.
    /// </summary>
    [ModalInteraction("create_panel:*", true)]
    public async Task HandlePanelCreation(string channelId, PanelCreationModal modal)
    {
        var channel = await ctx.Guild.GetTextChannelAsync(ulong.Parse(channelId));
        await Service.CreatePanelAsync(channel, modal.EmbedJson);
        await RespondAsync("Panel created successfully!", ephemeral: true);
    }

    /// <summary>
    ///     Handles note modal submission.
    /// </summary>
    [ModalInteraction("ticket_note:*", true)]
    public async Task HandleTicketNote(string ticketId, TicketNoteModal modal)
    {
        var ticket = await Service.GetTicketAsync(int.Parse(ticketId));
        await Service.AddNoteAsync(ticket, ctx.User as IGuildUser, modal.Content);
        await RespondAsync("Note added successfully!", ephemeral: true);
    }

    /// <summary>
    ///     Handles priority modal submission.
    /// </summary>
    [ModalInteraction("ticket_priority:*", true)]
    public async Task HandleTicketPriority(string ticketId, TicketPriorityModal modal)
    {
        var ticket = await Service.GetTicketAsync(int.Parse(ticketId));
        await Service.SetTicketPriorityAsync(ticket, modal.Priority, ctx.User as IGuildUser);
        await RespondAsync($"Priority set to {modal.Priority}!", ephemeral: true);
    }

    #endregion

    #region Button Style Handlers

    /// <summary>
    ///     Handles the ticket claim button interaction.
    /// </summary>
    /// <remarks>
    ///     This method is triggered when a user clicks the claim button on a ticket.
    ///     It will attempt to claim the ticket for the current user if they have appropriate permissions.
    /// </remarks>
    [ComponentInteraction("ticket_claim", true)]
    public async Task HandleTicketClaim()
    {
        try
        {
            var success = await Service.ClaimTicket(ctx.Guild, ctx.Channel.Id, ctx.User as IGuildUser);
            if (success)
                await RespondAsync("Ticket claimed successfully!", ephemeral: true);
            else
                await RespondAsync("Failed to claim ticket. It may already be claimed or you lack permissions.",
                    ephemeral: true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error claiming ticket in channel {ChannelId}", ctx.Channel.Id);
            await RespondAsync("An error occurred while claiming the ticket.", ephemeral: true);
        }
    }

    /// <summary>
    ///     Handles ticket creation button interactions with wildcard pattern matching.
    /// </summary>
    /// <param name="buttonId">The unique identifier portion of the button's custom ID.</param>
    /// <remarks>
    ///     This method handles all ticket creation buttons using a wildcard pattern.
    ///     If the button has a modal configuration, it will display the modal.
    ///     Otherwise, it creates the ticket immediately.
    /// </remarks>
    [ComponentInteraction("ticket_btn_*", true)]
    public async Task HandleTicketButton(string buttonId)
    {
        try
        {
            var panelButton = await Service.GetButtonAsync($"ticket_btn_{buttonId}");
            if (panelButton == null)
            {
                await RespondAsync("This ticket type is no longer available.", ephemeral: true);
                return;
            }

            if (!string.IsNullOrEmpty(panelButton.ModalJson))
            {
                await Service.HandleModalCreation(
                    ctx.User as IGuildUser,
                    panelButton.ModalJson,
                    $"ticket_modal:{panelButton.Id}",
                    ctx.Interaction
                );
            }
            else
            {
                await Service.CreateTicketAsync(
                    ctx.Guild,
                    ctx.User,
                    panelButton
                );
                await RespondAsync("Ticket created successfully!", ephemeral: true);
            }
        }
        catch (InvalidOperationException ex)
        {
            await RespondAsync(ex.Message, ephemeral: true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling ticket button for buttonId {ButtonId}", buttonId);
            await RespondAsync("An error occurred while creating your ticket.", ephemeral: true);
        }
    }

    /// <summary>
    ///     Handles button style selection during the button creation workflow.
    /// </summary>
    /// <param name="panelId">The ID of the panel the button is being added to.</param>
    /// <param name="style">The selected button style (primary, success, secondary, or danger).</param>
    /// <remarks>
    ///     This method is part of the button creation workflow and stores the selected style
    ///     in Redis cache for later use when the button is actually created.
    /// </remarks>
    [ComponentInteraction("btn_style:*:*", true)]
    public async Task HandleButtonStyle(string panelId, string style)
    {
        try
        {
            await cache.Redis.GetDatabase().StringSetAsync($"btn_creation:{ctx.User.Id}:style", style);

            await ctx.Interaction.SendConfirmAsync(Strings.EnterButtonLabel(ctx.Guild.Id));
            var label = await NextMessageAsync(ctx.Channel.Id, ctx.User.Id);

            if (string.IsNullOrEmpty(label))
            {
                await ctx.Interaction.SendErrorAsync(Strings.ButtonCreationCancelled(ctx.Guild.Id), Config);
                return;
            }

            await cache.Redis.GetDatabase().StringSetAsync($"btn_creation:{ctx.User.Id}:label", label);

            var components = new ComponentBuilder()
                .WithButton("Yes", $"btn_emoji:{panelId}:yes")
                .WithButton("No", $"btn_emoji:{panelId}:no")
                .Build();

            await ctx.Interaction.FollowupAsync(
                embed: new EmbedBuilder().WithDescription(Strings.AddEmojiToButton(ctx.Guild.Id)).WithOkColor()
                    .Build(),
                components: components);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling button style selection for panel {PanelId}", panelId);
            await RespondAsync("An error occurred during button creation.", ephemeral: true);
        }
    }

    /// <summary>
    ///     Handles emoji choice selection during button creation.
    /// </summary>
    /// <param name="panelId">The ID of the panel the button is being added to.</param>
    /// <param name="choice">The user's choice regarding emoji addition (yes or no).</param>
    /// <remarks>
    ///     If the user chooses 'yes', prompts for emoji input. Then proceeds to the
    ///     ticket settings configuration step.
    /// </remarks>
    [ComponentInteraction("btn_emoji:*:*", true)]
    public async Task HandleEmojiChoice(string panelId, string choice)
    {
        try
        {
            if (choice == "yes")
            {
                await ctx.Interaction.SendConfirmAsync(Strings.PleaseEnterEmoji(ctx.Guild.Id));
                var emoji = await NextMessageAsync(ctx.Channel.Id, ctx.User.Id);

                if (!string.IsNullOrEmpty(emoji))
                {
                    await cache.Redis.GetDatabase().StringSetAsync($"btn_creation:{ctx.User.Id}:emoji", emoji);
                }
            }

            await PromptTicketSettings(panelId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling emoji choice for panel {PanelId}", panelId);
            await RespondAsync("An error occurred during button creation.", ephemeral: true);
        }
    }

    /// <summary>
    ///     Handles category selection during button creation.
    /// </summary>
    /// <param name="panelId">The ID of the panel the button is being added to.</param>
    /// <param name="values">Array of selected category values in format "type:categoryId".</param>
    /// <remarks>
    ///     Processes category selections for both ticket creation and archiving.
    ///     Values are expected in format "create:categoryId" or "archive:categoryId".
    /// </remarks>
    [ComponentInteraction("btn_category:*", true)]
    public async Task HandleCategorySelect(string panelId, string[] values)
    {
        try
        {
            await DeferAsync();
            ulong? createCategory = null;
            ulong? archiveCategory = null;

            foreach (var selection in values)
            {
                var parts = selection.Split(':');
                if (parts.Length == 2 && ulong.TryParse(parts[1], out var id))
                {
                    switch (parts[0])
                    {
                        case "create":
                            createCategory = id;
                            break;
                        case "archive":
                            archiveCategory = id;
                            break;
                    }
                }
            }

            await cache.Redis.GetDatabase()
                .StringSetAsync($"btn_creation:{ctx.User.Id}:category", createCategory?.ToString());
            await cache.Redis.GetDatabase()
                .StringSetAsync($"btn_creation:{ctx.User.Id}:archive_category", archiveCategory?.ToString());

            var roleMenuBuilder = new SelectMenuBuilder()
                .WithPlaceholder("Select Support Roles")
                .WithCustomId($"btn_roles:{panelId}")
                .WithMinValues(0);

            foreach (var role in ctx.Guild.Roles.Where(r => r.Permissions.ManageChannels))
            {
                roleMenuBuilder.AddOption(role.Name, role.Id.ToString(), $"Support role: {role.Name}");
            }

            var components = new ComponentBuilder()
                .WithSelectMenu(roleMenuBuilder)
                .WithButton("Continue", $"btn_roles:{panelId}:done")
                .Build();

            await FollowupAsync("Select support roles (optional):", components: components);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling category selection for panel {PanelId}", panelId);
            await FollowupAsync("An error occurred during button creation.", ephemeral: true);
        }
    }

    /// <summary>
    ///     Handles support role selection during button creation.
    /// </summary>
    /// <param name="panelId">The ID of the panel the button is being added to.</param>
    /// <param name="values">Array of selected role IDs.</param>
    /// <remarks>
    ///     Stores the selected support roles and proceeds to viewer role selection.
    /// </remarks>
    [ComponentInteraction("btn_roles:*", true)]
    public async Task HandleRoleSelect(string panelId, string[] values)
    {
        try
        {
            if (values.Any())
            {
                await cache.Redis.GetDatabase()
                    .StringSetAsync($"btn_creation:{ctx.User.Id}:roles", JsonSerializer.Serialize(values));
            }

            var viewerRoleMenu = new SelectMenuBuilder()
                .WithPlaceholder("Select Viewer Roles")
                .WithCustomId($"btn_viewer_roles:{panelId}")
                .WithMinValues(0);

            foreach (var role in ctx.Guild.Roles)
            {
                viewerRoleMenu.AddOption(role.Name, role.Id.ToString(), $"Viewer role: {role.Name}");
            }

            var components = new ComponentBuilder()
                .WithSelectMenu(viewerRoleMenu)
                .WithButton("Skip", $"btn_viewer_roles:{panelId}:skip")
                .Build();

            await RespondAsync("Select viewer roles (optional):", components: components);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling role selection for panel {PanelId}", panelId);
            await RespondAsync("An error occurred during button creation.", ephemeral: true);
        }
    }

    /// <summary>
    ///     Handles the completion of support role selection.
    /// </summary>
    /// <param name="panelId">The ID of the panel the button is being added to.</param>
    /// <remarks>
    ///     This method is called when the user clicks "Continue" after support role selection.
    ///     It presents the viewer role selection interface.
    /// </remarks>
    [ComponentInteraction("btn_roles:*:done", true)]
    public async Task HandleRoleDone(string panelId)
    {
        try
        {
            var viewerRoleMenu = new SelectMenuBuilder()
                .WithPlaceholder("Select Viewer Roles")
                .WithCustomId($"btn_viewer_roles:{panelId}")
                .WithMinValues(0);

            foreach (var role in ctx.Guild.Roles)
            {
                viewerRoleMenu.AddOption(role.Name, role.Id.ToString(), $"Viewer role: {role.Name}");
            }

            var components = new ComponentBuilder()
                .WithSelectMenu(viewerRoleMenu)
                .WithButton("Skip", $"btn_viewer_roles:{panelId}:skip")
                .Build();

            await RespondAsync("Select viewer roles (optional):", components: components);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error transitioning to viewer role selection for panel {PanelId}", panelId);
            await RespondAsync("An error occurred during button creation.", ephemeral: true);
        }
    }

    /// <summary>
    ///     Handles viewer role selection and additional configuration prompts.
    /// </summary>
    /// <param name="panelId">The ID of the panel the button is being added to.</param>
    /// <param name="values">Array of selected viewer role IDs.</param>
    /// <remarks>
    ///     Stores viewer roles and sequentially prompts for ticket opening message,
    ///     modal JSON configuration, allowed priorities, default priority, and response time.
    /// </remarks>
    [ComponentInteraction("btn_viewer_roles:*", true)]
    public async Task HandleViewerRoleSelect(string panelId, string[] values)
    {
        try
        {
            if (values.Any())
            {
                await cache.Redis.GetDatabase()
                    .StringSetAsync($"btn_creation:{ctx.User.Id}:viewer_roles", JsonSerializer.Serialize(values));
            }

            await ctx.Interaction.SendConfirmAsync(
                "Please enter a custom ticket opening message (or type 'skip' to use default):");
            var openMsg = await NextMessageAsync(ctx.Channel.Id, ctx.User.Id);
            if (!string.IsNullOrEmpty(openMsg) && openMsg.ToLower() != "skip")
            {
                await cache.Redis.GetDatabase()
                    .StringSetAsync($"btn_creation:{ctx.User.Id}:open_message", openMsg);
            }

            await ctx.Interaction.SendConfirmAsync("Please enter modal JSON configuration (or type 'skip' to skip):");
            var modalJson = await NextMessageAsync(ctx.Channel.Id, ctx.User.Id);
            if (!string.IsNullOrEmpty(modalJson) && modalJson.ToLower() != "skip")
            {
                await cache.Redis.GetDatabase()
                    .StringSetAsync($"btn_creation:{ctx.User.Id}:modal_json", modalJson);
            }

            await ctx.Interaction.SendConfirmAsync(
                "Please enter allowed priorities, comma separated (or type 'skip' to skip):");
            var priorities = await NextMessageAsync(ctx.Channel.Id, ctx.User.Id);
            if (!string.IsNullOrEmpty(priorities) && priorities.ToLower() != "skip")
            {
                await cache.Redis.GetDatabase()
                    .StringSetAsync($"btn_creation:{ctx.User.Id}:priorities",
                        JsonSerializer.Serialize(priorities.Split(',').Select(p => p.Trim())));

                await ctx.Interaction.SendConfirmAsync("Please enter the default priority (or type 'skip' to skip):");
                var defaultPriority = await NextMessageAsync(ctx.Channel.Id, ctx.User.Id);
                if (!string.IsNullOrEmpty(defaultPriority) && defaultPriority.ToLower() != "skip")
                {
                    await cache.Redis.GetDatabase()
                        .StringSetAsync($"btn_creation:{ctx.User.Id}:default_priority", defaultPriority);
                }
            }

            var responseTimeMenu = new SelectMenuBuilder()
                .WithPlaceholder("Select Response Time")
                .WithCustomId($"btn_response_time:{panelId}")
                .WithMinValues(0)
                .WithMaxValues(1)
                .AddOption("1 hour", "1", "Response required within 1 hour")
                .AddOption("4 hours", "4", "Response required within 4 hours")
                .AddOption("12 hours", "12", "Response required within 12 hours")
                .AddOption("24 hours", "24", "Response required within 24 hours");

            var components = new ComponentBuilder()
                .WithSelectMenu(responseTimeMenu)
                .WithButton("Skip", $"btn_response_time:{panelId}:skip")
                .Build();

            await ctx.Interaction.FollowupAsync("Select required response time (optional):", components: components);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling viewer role selection for panel {PanelId}", panelId);
            await RespondAsync("An error occurred during button creation.", ephemeral: true);
        }
    }

    /// <summary>
    ///     Handles response time selection during button creation.
    /// </summary>
    /// <param name="panelId">The ID of the panel the button is being added to.</param>
    /// <param name="values">Array containing the selected response time in hours.</param>
    /// <remarks>
    ///     Stores the response time setting and shows the final confirmation dialog.
    /// </remarks>
    [ComponentInteraction("btn_response_time:*", true)]
    public async Task HandleResponseTimeSelect(string panelId, string[] values)
    {
        try
        {
            if (values.Any())
            {
                await cache.Redis.GetDatabase()
                    .StringSetAsync($"btn_creation:{ctx.User.Id}:response_time", values[0]);
            }

            var components = new ComponentBuilder()
                .WithButton("Confirm", $"btn_confirm:{panelId}")
                .WithButton("Cancel", $"btn_cancel:{panelId}", ButtonStyle.Danger)
                .Build();

            await RespondAsync("Review and confirm button creation:", components: components);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling response time selection for panel {PanelId}", panelId);
            await RespondAsync("An error occurred during button creation.", ephemeral: true);
        }
    }

    /// <summary>
    ///     Handles skipping response time selection during button creation.
    /// </summary>
    /// <param name="panelId">The ID of the panel the button is being added to.</param>
    /// <remarks>
    ///     Shows the final confirmation dialog without setting a response time requirement.
    /// </remarks>
    [ComponentInteraction("btn_response_time:*:skip", true)]
    public async Task HandleResponseTimeSkip(string panelId)
    {
        try
        {
            var components = new ComponentBuilder()
                .WithButton("Confirm", $"btn_confirm:{panelId}")
                .WithButton("Cancel", $"btn_cancel:{panelId}", ButtonStyle.Danger)
                .Build();

            await RespondAsync("Review and confirm button creation:", components: components);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling response time skip for panel {PanelId}", panelId);
            await RespondAsync("An error occurred during button creation.", ephemeral: true);
        }
    }

    /// <summary>
    ///     Handles auto-close time selection during button creation.
    /// </summary>
    /// <param name="panelId">The ID of the panel the button is being added to.</param>
    /// <param name="values">Array containing the selected auto-close time in hours.</param>
    /// <remarks>
    ///     Stores the auto-close setting and shows the final confirmation dialog.
    /// </remarks>
    [ComponentInteraction("btn_autoclose:*", true)]
    public async Task HandleAutoCloseSelect(string panelId, string[] values)
    {
        try
        {
            if (values.Any())
            {
                await cache.Redis.GetDatabase()
                    .StringSetAsync($"btn_creation:{ctx.User.Id}:autoclose", values[0]);
            }

            var components = new ComponentBuilder()
                .WithButton("Confirm", $"btn_confirm:{panelId}")
                .WithButton("Cancel", $"btn_cancel:{panelId}", ButtonStyle.Danger)
                .Build();

            await RespondAsync("Review and confirm button creation:", components: components);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling auto-close selection for panel {PanelId}", panelId);
            await RespondAsync("An error occurred during button creation.", ephemeral: true);
        }
    }

    /// <summary>
    ///     Handles skipping auto-close time selection during button creation.
    /// </summary>
    /// <param name="panelId">The ID of the panel the button is being added to.</param>
    /// <remarks>
    ///     Shows the final confirmation dialog without setting an auto-close time.
    /// </remarks>
    [ComponentInteraction("btn_autoclose:*:skip", true)]
    public async Task HandleAutoCloseSkip(string panelId)
    {
        try
        {
            var components = new ComponentBuilder()
                .WithButton("Confirm", $"btn_confirm:{panelId}")
                .WithButton("Cancel", $"btn_cancel:{panelId}", ButtonStyle.Danger)
                .Build();

            await RespondAsync("Review and confirm button creation:", components: components);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling auto-close skip for panel {PanelId}", panelId);
            await RespondAsync("An error occurred during button creation.", ephemeral: true);
        }
    }

    /// <summary>
    ///     Handles the final confirmation and creation of a button.
    /// </summary>
    /// <param name="panelId">The ID of the panel to add the button to.</param>
    /// <remarks>
    ///     Retrieves all stored settings from Redis cache, validates them, creates the button
    ///     using the ticket service, and cleans up the cache data.
    /// </remarks>
    [ComponentInteraction("btn_confirm:*", true)]
    public async Task HandleConfirmation(ulong panelId)
    {
        try
        {
            await DeferAsync();

            // Get all stored settings
            var style = await cache.Redis.GetDatabase().StringGetAsync($"btn_creation:{ctx.User.Id}:style");
            var label = await cache.Redis.GetDatabase().StringGetAsync($"btn_creation:{ctx.User.Id}:label");
            var emoji = await cache.Redis.GetDatabase().StringGetAsync($"btn_creation:{ctx.User.Id}:emoji");
            var category = await cache.Redis.GetDatabase().StringGetAsync($"btn_creation:{ctx.User.Id}:category");
            var archiveCategory = await cache.Redis.GetDatabase()
                .StringGetAsync($"btn_creation:{ctx.User.Id}:archive_category");
            var roles = await cache.Redis.GetDatabase().StringGetAsync($"btn_creation:{ctx.User.Id}:roles");
            var viewerRoles =
                await cache.Redis.GetDatabase().StringGetAsync($"btn_creation:{ctx.User.Id}:viewer_roles");
            var openMessage =
                await cache.Redis.GetDatabase().StringGetAsync($"btn_creation:{ctx.User.Id}:open_message");
            var modalJson = await cache.Redis.GetDatabase().StringGetAsync($"btn_creation:{ctx.User.Id}:modal_json");
            var priorities = await cache.Redis.GetDatabase().StringGetAsync($"btn_creation:{ctx.User.Id}:priorities");
            var defaultPriority = await cache.Redis.GetDatabase()
                .StringGetAsync($"btn_creation:{ctx.User.Id}:default_priority");
            var autoClose = await cache.Redis.GetDatabase().StringGetAsync($"btn_creation:{ctx.User.Id}:autoclose");
            var responseTime =
                await cache.Redis.GetDatabase().StringGetAsync($"btn_creation:{ctx.User.Id}:response_time");

            var panel = await Service.GetPanelAsync(panelId);
            if (panel == null)
            {
                await FollowupAsync("Panel not found!", ephemeral: true);
                return;
            }

            // Parse and validate all settings
            var buttonStyle = ButtonStyle.Primary;
            if (style.HasValue && Enum.TryParse<ButtonStyle>(style.ToString(), true, out var parsedStyle))
            {
                buttonStyle = parsedStyle;
            }

            ulong? categoryId = null;
            if (category.HasValue && ulong.TryParse((string)category, out var parsedCategory))
            {
                categoryId = parsedCategory;
            }

            ulong? archiveCategoryId = null;
            if (archiveCategory.HasValue && ulong.TryParse((string)archiveCategory, out var parsedArchiveCategory))
            {
                archiveCategoryId = parsedArchiveCategory;
            }

            List<ulong> supportRoles = null;
            if (roles.HasValue)
            {
                try
                {
                    var rolesArray = JsonSerializer.Deserialize<string[]>((string)roles);
                    supportRoles = rolesArray.Select(ulong.Parse).ToList();
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to parse support roles");
                }
            }

            List<ulong> viewerRolesList = null;
            if (viewerRoles.HasValue)
            {
                try
                {
                    viewerRolesList = JsonSerializer.Deserialize<string[]>((string)viewerRoles).Select(ulong.Parse)
                        .ToList();
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to parse viewer roles");
                }
            }

            List<string> allowedPriorities = null;
            if (priorities.HasValue)
            {
                try
                {
                    allowedPriorities = JsonSerializer.Deserialize<List<string>>((string)priorities);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to parse priorities");
                }
            }

            TimeSpan? autoCloseTime = null;
            if (autoClose.HasValue && int.TryParse((string)autoClose, out var autoCloseHours))
            {
                autoCloseTime = TimeSpan.FromHours(autoCloseHours);
            }

            TimeSpan? requiredResponseTime = null;
            if (responseTime.HasValue && int.TryParse((string)responseTime, out var responseHours))
            {
                requiredResponseTime = TimeSpan.FromHours(responseHours);
            }

            await Service.AddButtonAsync(
                panel,
                label.HasValue ? label.ToString() : "Button",
                emoji.HasValue ? emoji.ToString() : null,
                buttonStyle,
                categoryId: categoryId,
                archiveCategoryId: archiveCategoryId,
                supportRoles: supportRoles,
                viewerRoles: viewerRolesList,
                openMessageJson: openMessage.HasValue ? openMessage.ToString() : null,
                modalJson: modalJson.HasValue ? modalJson.ToString() : null,
                allowedPriorities: allowedPriorities,
                defaultPriority: defaultPriority.HasValue ? defaultPriority.ToString() : null,
                autoCloseTime: autoCloseTime,
                requiredResponseTime: requiredResponseTime
            );

            await FollowupAsync("Button created successfully!", ephemeral: true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error confirming button creation for panel {PanelId}", panelId);
            await FollowupAsync($"Error creating button: {ex.Message}", ephemeral: true);
        }
        finally
        {
            // Cleanup Redis cache
            await cache.Redis.GetDatabase().KeyDeleteAsync([
                $"btn_creation:{ctx.User.Id}:style", $"btn_creation:{ctx.User.Id}:label",
                $"btn_creation:{ctx.User.Id}:emoji", $"btn_creation:{ctx.User.Id}:category",
                $"btn_creation:{ctx.User.Id}:archive_category", $"btn_creation:{ctx.User.Id}:roles",
                $"btn_creation:{ctx.User.Id}:viewer_roles", $"btn_creation:{ctx.User.Id}:open_message",
                $"btn_creation:{ctx.User.Id}:modal_json", $"btn_creation:{ctx.User.Id}:priorities",
                $"btn_creation:{ctx.User.Id}:default_priority", $"btn_creation:{ctx.User.Id}:autoclose",
                $"btn_creation:{ctx.User.Id}:response_time"
            ]);
        }
    }

    /// <summary>
    ///     Handles cancellation of button creation.
    /// </summary>
    /// <param name="panelId">The ID of the panel for which button creation was cancelled.</param>
    /// <remarks>
    ///     Cleans up the Redis cache and notifies the user that button creation was cancelled.
    /// </remarks>
    [ComponentInteraction("btn_cancel:*", true)]
    public async Task HandleButtonCancel(string panelId)
    {
        try
        {
            // Cleanup Redis cache
            await cache.Redis.GetDatabase().KeyDeleteAsync([
                $"btn_creation:{ctx.User.Id}:style", $"btn_creation:{ctx.User.Id}:label",
                $"btn_creation:{ctx.User.Id}:emoji", $"btn_creation:{ctx.User.Id}:category",
                $"btn_creation:{ctx.User.Id}:archive_category", $"btn_creation:{ctx.User.Id}:roles",
                $"btn_creation:{ctx.User.Id}:viewer_roles", $"btn_creation:{ctx.User.Id}:open_message",
                $"btn_creation:{ctx.User.Id}:modal_json", $"btn_creation:{ctx.User.Id}:priorities",
                $"btn_creation:{ctx.User.Id}:default_priority", $"btn_creation:{ctx.User.Id}:autoclose",
                $"btn_creation:{ctx.User.Id}:response_time"
            ]);

            await RespondAsync("Button creation cancelled.", ephemeral: true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error cancelling button creation for panel {PanelId}", panelId);
            await RespondAsync("An error occurred while cancelling button creation.", ephemeral: true);
        }
    }

    #endregion

    #region Modal Interaction Handlers

    /// <summary>
    ///     Handles the submission of case creation modals.
    /// </summary>
    /// <param name="modal">The modal containing case creation information.</param>
    /// <remarks>
    ///     Creates a new ticket case with the provided title and description.
    /// </remarks>
    [ModalInteraction("create_case", true)]
    public async Task HandleCaseCreation(CaseCreationModal modal)
    {
        try
        {
            var ticketCase = await Service.CreateCaseAsync(
                ctx.Guild,
                modal.CaseTitle,
                modal.Description,
                ctx.User as IGuildUser);

            await RespondAsync(Strings.TicketCaseCreated(ctx.Guild.Id, ticketCase.Id.ToString()), ephemeral: true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating case with title {Title}", modal.CaseTitle);
            await RespondAsync("Failed to create case. Please try again.", ephemeral: true);
        }
    }

    /// <summary>
    ///     Handles the submission of case update modals.
    /// </summary>
    /// <param name="caseId">The ID of the case being updated.</param>
    /// <param name="modal">The modal containing updated case information.</param>
    /// <remarks>
    ///     Updates an existing case with new title and/or description.
    /// </remarks>
    [ModalInteraction("case_update:*", true)]
    public async Task HandleCaseUpdate(string caseId, CaseUpdateModal modal)
    {
        try
        {
            if (!int.TryParse(caseId, out var parsedCaseId))
            {
                await RespondAsync("Invalid case ID.", ephemeral: true);
                return;
            }

            await Service.UpdateCaseAsync(parsedCaseId, modal.CaseTitle, modal.Description);
            await RespondAsync($"Case #{parsedCaseId} updated successfully!", ephemeral: true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating case {CaseId}", caseId);
            await RespondAsync("Failed to update case. Please try again.", ephemeral: true);
        }
    }

    /// <summary>
    ///     Handles the submission of select menu creation modals.
    /// </summary>
    /// <param name="panelId">The ID of the panel to add the select menu to.</param>
    /// <param name="modal">The modal containing select menu configuration.</param>
    /// <remarks>
    ///     Creates a new select menu on the specified panel with the provided configuration.
    /// </remarks>
    [ModalInteraction("create_menu:*", true)]
    public async Task HandleSelectMenuCreation(string panelId, SelectMenuCreationModal modal)
    {
        try
        {
            if (!ulong.TryParse(panelId, out var parsedPanelId))
            {
                await RespondAsync("Invalid panel ID.", ephemeral: true);
                return;
            }

            var panel = await Service.GetPanelAsync(parsedPanelId);
            if (panel == null)
            {
                await RespondAsync("Panel not found!", ephemeral: true);
                return;
            }

            var menu = await Service.AddSelectMenuAsync(
                panel,
                modal.Placeholder,
                modal.Title
            );

            await RespondAsync($"Select menu created successfully with ID {menu.Id}!", ephemeral: true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating select menu for panel {PanelId}", panelId);
            await RespondAsync("Failed to create select menu. Please try again.", ephemeral: true);
        }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    ///     Prompts the user to configure ticket settings during button creation.
    /// </summary>
    /// <param name="panelId">The ID of the panel the button is being added to.</param>
    /// <remarks>
    ///     Displays a select menu for choosing ticket categories (creation and archive).
    /// </remarks>
    private async Task PromptTicketSettings(string panelId)
    {
        try
        {
            var categories = await ctx.Guild.GetCategoriesAsync();

            var menuBuilder = new SelectMenuBuilder()
                .WithPlaceholder("Select Categories")
                .WithCustomId($"btn_category:{panelId}")
                .WithMinValues(0)
                .WithMaxValues(2);

            foreach (var category in categories)
            {
                menuBuilder.AddOption(
                    $"Create: {category.Name}",
                    $"create:{category.Id}",
                    "Category for new tickets"
                );
                menuBuilder.AddOption(
                    $"Archive: {category.Name}",
                    $"archive:{category.Id}",
                    "Category for archived tickets"
                );
            }

            var components = new ComponentBuilder()
                .WithSelectMenu(menuBuilder)
                .WithButton("Skip", $"btn_category:{panelId}:skip")
                .Build();

            await FollowupAsync("Select ticket categories (optional):", components: components);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error prompting ticket settings for panel {PanelId}", panelId);
            await FollowupAsync("An error occurred during button creation.", ephemeral: true);
        }
    }

    /// <summary>
    ///     Handles custom modals for ticket creation from buttons.
    /// </summary>
    /// <param name="buttonId">The buttons db ID</param>
    /// <param name="unused">Unused modal parameter</param>
    [ModalInteraction("ticket_modal:*", true)]
    public async Task HandleTicketModalSubmission(string buttonId, SimpleInputModal unused)
    {
        await DeferAsync(true);

        try
        {
            if (!int.TryParse(buttonId, out var buttonIdParsed))
            {
                await FollowupAsync($"{Config.ErrorEmote} Invalid button ID.", ephemeral: true);
                return;
            }

            var button = await Service.GetButtonAsync(buttonIdParsed);
            if (button == null)
            {
                await FollowupAsync($"{Config.ErrorEmote} Button configuration not found.", ephemeral: true);
                return;
            }

            // Get the modal data from the interaction
            var modal = (IModalInteraction)Context.Interaction;
            var modalResponses = ExtractModalResponses(modal.Data.Components);

            // Create the ticket using the submitted modal data
            var ticket = await Service.CreateTicketAsync(
                ctx.Guild,
                ctx.User,
                button,
                modalResponses: modalResponses
            );

            if (ticket != null)
            {
                await FollowupAsync($"{Config.SuccessEmote} Ticket created: <#{ticket.ChannelId}>", ephemeral: true);
            }
            else
            {
                await FollowupAsync($"{Config.ErrorEmote} Failed to create ticket. Please try again.", ephemeral: true);
            }
        }
        catch (InvalidOperationException ex)
        {
            await FollowupAsync($"There was an issue creating your ticket:\n{ex.Message}", ephemeral: true);
        }
        catch (Exception ex)
        {
            logger.LogError($"Error handling ticket modal submission: {ex}");
            await FollowupAsync($"{Config.ErrorEmote} An error occurred while creating your ticket.", ephemeral: true);
        }
    }

    /// <summary>
    ///     Handles custom modals for ticket creation from select menu options.
    /// </summary>
    /// <param name="optionId">The select menu option's db ID</param>
    /// <param name="unused">Unused modal parameter</param>
    [ModalInteraction("ticket_modal_select:*", true)]
    public async Task HandleTicketModalSelectSubmission(string optionId, SimpleInputModal unused)
    {
        await DeferAsync(true);

        try
        {
            if (!int.TryParse(optionId, out var optionIdParsed))
            {
                await FollowupAsync($"{Config.ErrorEmote} Invalid option ID.", ephemeral: true);
                return;
            }

            var option = await Service.GetSelectOptionAsync(optionIdParsed);
            if (option == null)
            {
                await FollowupAsync($"{Config.ErrorEmote} Select option configuration not found.", ephemeral: true);
                return;
            }

            // Get the modal data from the interaction
            var modal = (IModalInteraction)Context.Interaction;
            var modalResponses = ExtractModalResponses(modal.Data.Components);

            // Create the ticket using the submitted modal data
            var ticket = await Service.CreateTicketAsync(
                ctx.Guild,
                ctx.User,
                option: option,
                modalResponses: modalResponses
            );

            if (ticket != null)
            {
                await FollowupAsync($"{Config.SuccessEmote} Ticket created: <#{ticket.ChannelId}>", ephemeral: true);
            }
            else
            {
                await FollowupAsync($"{Config.ErrorEmote} Failed to create ticket. Please try again.", ephemeral: true);
            }
        }
        catch (InvalidOperationException ex)
        {
            await FollowupAsync($"There was an issue creating your ticket:\n{ex.Message}", ephemeral: true);
        }
        catch (Exception ex)
        {
            logger.LogError($"Error handling ticket modal submission from select option: {ex}");
            await FollowupAsync($"{Config.ErrorEmote} An error occurred while creating your ticket.", ephemeral: true);
        }
    }

    /// <summary>
    ///     Extracts modal responses into a dictionary format
    /// </summary>
    /// <param name="components">The submitted modal components</param>
    /// <returns>Dictionary with field IDs as keys and user responses as values</returns>
    private static Dictionary<string, string> ExtractModalResponses(
        IReadOnlyCollection<IComponentInteractionData> components)
    {
        var responses = new Dictionary<string, string>();

        foreach (var component in components)
        {
            if (!string.IsNullOrEmpty(component.Value))
            {
                responses[component.CustomId] = component.Value;
            }
        }

        return responses;
    }

    /// <summary>
    ///     Starts the button creation process.
    /// </summary>
    [SlashCommand("addbutton", "Add a button to a ticket panel")]
    [SlashUserPerm(GuildPermission.Administrator)]
    public async Task AddButton(
        [Summary("panel-id")] string panelId)
    {
        var components = new ComponentBuilder()
            .WithButton("Primary", $"btn_style:{panelId}:primary")
            .WithButton("Success", $"btn_style:{panelId}:success", ButtonStyle.Success)
            .WithButton("Secondary", $"btn_style:{panelId}:secondary", ButtonStyle.Secondary)
            .WithButton("Danger", $"btn_style:{panelId}:danger", ButtonStyle.Danger);

        await RespondAsync("Choose the button style:", components: components.Build());
    }

    /// <summary>
    ///     Handles label input.
    /// </summary>
    [ModalInteraction("btn_label:*", true)]
    public async Task HandleButtonLabel(string panelId, SimpleInputModal modal)
    {
        // Store the label
        await cache.Redis.GetDatabase().StringSetAsync($"btn_creation:{ctx.User.Id}:label", modal.Input);

        // Ask if they want an emoji
        var components = new ComponentBuilder()
            .WithButton("Yes", $"btn_emoji:{panelId}:yes")
            .WithButton("No", $"btn_emoji:{panelId}:no");

        await RespondAsync(Strings.AddEmojiToButton(ctx.Guild.Id), components: components.Build());
    }


    /// <summary>
    ///     Handles emoji input.
    /// </summary>
    [ModalInteraction("btn_emoji_input:*", true)]
    public async Task HandleEmojiInput(string panelId, SimpleInputModal modal)
    {
        await cache.Redis.GetDatabase().StringSetAsync($"btn_creation:{ctx.User.Id}:emoji", modal.Input);
        await PromptTicketSettings(panelId);
    }

    #endregion
}