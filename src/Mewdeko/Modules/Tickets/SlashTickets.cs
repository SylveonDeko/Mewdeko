using Discord.Interactions;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Database.DbContextStuff;
using Mewdeko.Modules.Tickets.Services;

namespace Mewdeko.Modules.Tickets;

/// <inheritdoc />
[Group("tickets", "Manage ticket panels and tickets")]
public class TicketInteractionModule(DbContextProvider dbProvider) : MewdekoSlashModuleBase<TicketService>
{
    /// <summary>
    /// </summary>
    /// <param name="channel"></param>
    /// <param name="title"></param>
    /// <param name="description"></param>
    [SlashCommand("createpanel", "Create a new ticket panel")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    public async Task CreateTicketPanel()
    {
        //var channel =
    }

    /// <summary>
    /// </summary>
    /// <param name="panelId"></param>
    /// <param name="label"></param>
    /// <param name="emoji"></param>
    /// <param name="openMessage"></param>
    [SlashCommand("addbutton", "Add a button to a ticket panel")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    public async Task AddButton(
        [Summary(description: "The ID of the panel to add the button to")]
        int panelId,
        string label,
        string emoji,
        string openMessage)
    {
        await DeferAsync();
        var panels = await Service.GetTicketPanels(ctx.Guild.Id);
        var panel = panels.FirstOrDefault(p => p.Id == panelId);

        if (panel == null)
        {
            await ErrorLocalizedAsync("ticket_panel_not_found");
            return;
        }

        if (panel.Buttons.Count >= 5)
        {
            await ErrorLocalizedAsync("ticket_panel_max_buttons");
            return;
        }

        panel.Buttons.Add(new TicketButton
        {
            Label = label, Emoji = emoji, OpenMessage = openMessage
        });

        await using var db = await dbProvider.GetContextAsync();
        db.Update(panel);
        await db.SaveChangesAsync();

        await UpdateTicketPanelMessage(panel);
        await ConfirmLocalizedAsync("ticket_button_added");
    }

    private async Task UpdateTicketPanelMessage(TicketPanel panel)
    {
        // var channel = await ctx.Guild.GetTextChannelAsync(panel.ChannelId);
        // if (channel == null) return;
        //
        // var messages = await channel.GetMessagesAsync(1).FlattenAsync();
        // var message = messages.FirstOrDefault();
        //
        // var embed = new EmbedBuilder()
        //     .WithTitle(panel.Title)
        //     .WithDescription(panel.Description)
        //     .WithColor(Color.Blue)
        //     .Build();
        //
        // var componentBuilder = new ComponentBuilder();
        // foreach (var button in panel.Buttons)
        // {
        //     componentBuilder.WithButton(button.Label, $"ticket:{button.Id}", ButtonStyle.Primary, Emote.Parse(button.Emoji));
        // }
        //
        // if (message != null)
        // {
        //     await channel.ModifyMessageAsync(message.Id, properties =>
        //     {
        //         properties.Embed = embed;
        //         properties.Components = componentBuilder.Build();
        //     });
        // }
        // else
        // {
        //     await channel.SendMessageAsync(embed: embed, components: componentBuilder.Build());
        // }
    }

    /// <summary>
    /// </summary>
    /// <param name="buttonId"></param>
    [ComponentInteraction("ticket:*")]
    public async Task HandleTicketButton(string buttonId)
    {
        await DeferAsync(true);

        var guildUser = (IGuildUser)ctx.User;
        var button = await Service.GetTicketButton(int.Parse(buttonId));

        if (button != null)
        {
            var ticketChannel = await Service.CreateTicketChannel(ctx.Guild, guildUser, button);
            await ConfirmLocalizedAsync("ticket_created", ticketChannel.Mention);
        }
        else
        {
            await ErrorLocalizedAsync("ticket_button_not_found");
        }
    }

    /// <summary>
    /// </summary>
    /// <param name="panelId"></param>
    [SlashCommand("deletepanel", "Delete a ticket panel")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    public async Task DeletePanel(int panelId)
    {
        await DeferAsync();
        await Service.DeleteTicketPanel(ctx.Guild.Id, panelId);
        await ConfirmLocalizedAsync("ticket_panel_deleted", panelId);
    }
}