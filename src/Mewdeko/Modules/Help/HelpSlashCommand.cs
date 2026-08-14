using Discord.Commands;
using Discord.Interactions;
using Fergun.Interactive;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Common.Autocompleters;
using Mewdeko.Common.DiscordImplementations;
using Mewdeko.Common.Modals;
using Mewdeko.Modules.Help.Services;
using Mewdeko.Services.Settings;

namespace Mewdeko.Modules.Help;

/// <summary>
///     Slash command module for help commands.
/// </summary>
/// <param name="interactivity">The service for embed pagination</param>
/// <param name="cmds">The command service</param>
/// <param name="ch">The command handler (yes they are different now shut up)</param>
/// <param name="guildSettings">The service to retrieve guildconfigs</param>
/// <param name="config">Service to retrieve yml based configs</param>
[Discord.Interactions.Group("help", "Help Commands, what else is there to say?")]
public class HelpSlashCommand(
    InteractiveService interactivity,
    CommandService cmds,
    CommandHandler ch,
    GuildSettingsService guildSettings,
    BotConfigService config)
    : MewdekoSlashModuleBase<HelpService>
{
    /// <summary>
    ///     Shows all modules as well as additional information.
    /// </summary>
    [SlashCommand("help", "Shows help on how to use the bot")]
    [CheckPermissions]
    public async Task Modules()
    {
        var embed = await Service.GetHelpEmbed(false, ctx.Guild, ctx.Channel, ctx.User);
        await RespondAsync(embed: embed.Build(), components: Service.GetHelpComponents(ctx.Guild, ctx.User).Build())
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Handles the category select menu on the landing help menu.
    /// </summary>
    /// <param name="selected">The selected category key</param>
    [ComponentInteraction("helpcat", true)]
    public async Task HelpCategorySelect(string[] selected)
    {
        var category = selected.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(category))
            return;

        var (embed, components) = await Service.GetCategoryEmbed(category, ctx.Guild, ctx.Channel, ctx.User);
        await UpdateHelpMessage(embed, components).ConfigureAwait(false);
    }

    /// <summary>
    ///     Handles the module select menu shown inside a category.
    /// </summary>
    /// <param name="category">The category the module belongs to</param>
    /// <param name="selected">The selected module name</param>
    [ComponentInteraction("helpmodule:*", true)]
    public async Task HelpModuleSelect(string category, string[] selected)
    {
        var module = selected.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(module))
            return;

        await ShowModule(module, null, null).ConfigureAwait(false);
    }

    /// <summary>
    ///     Handles the section select menu shown for large modules.
    /// </summary>
    /// <param name="module">The module the section belongs to</param>
    /// <param name="selected">The selected section name</param>
    [ComponentInteraction("helpsection:*", true)]
    public async Task HelpSectionSelect(string module, string[] selected)
    {
        await ShowCommandList(module, selected.FirstOrDefault(), null).ConfigureAwait(false);
    }

    /// <summary>
    ///     Lists every command in a module, skipping the section index.
    /// </summary>
    /// <param name="module">The module to list</param>
    [ComponentInteraction("helpall:*", true)]
    public async Task HelpAllCommands(string module)
    {
        await ShowCommandList(module, null, null).ConfigureAwait(false);
    }

    /// <summary>
    ///     Opens the search modal scoped to a module.
    /// </summary>
    /// <param name="module">The module to search within</param>
    [ComponentInteraction("helpsearch:*", true)]
    public async Task HelpSearchPrompt(string module)
    {
        await RespondWithModalAsync<HelpSearchModal>($"helpsearchmodal:{module}").ConfigureAwait(false);
    }

    /// <summary>
    ///     Handles the submitted in-module search, listing the matching commands.
    /// </summary>
    /// <param name="module">The module searched within</param>
    /// <param name="modal">The submitted modal</param>
    [ModalInteraction("helpsearchmodal:*", true)]
    public async Task HelpSearchSubmit(string module, HelpSearchModal modal)
    {
        await ShowCommandList(module, null, modal.Term).ConfigureAwait(false);
    }

    /// <summary>
    ///     Returns to a module's section index from a command list.
    /// </summary>
    /// <param name="module">The module whose section index to show</param>
    [ComponentInteraction("helpoverview:*", true)]
    public async Task HelpBackToOverview(string module)
    {
        var resolved = Service.ResolveModule(module, out _);
        var overview = resolved is null ? null : Service.GetModuleOverview(resolved.Name, ctx.Guild);
        if (overview is null)
        {
            await HelpBackToCategories().ConfigureAwait(false);
            return;
        }

        await UpdateHelpMessage(overview.Value.Embed, overview.Value.Components).ConfigureAwait(false);
    }

    /// <summary>
    ///     Returns to the category landing menu.
    /// </summary>
    [ComponentInteraction("helpback:categories", true)]
    public async Task HelpBackToCategories()
    {
        var embed = await Service.GetHelpEmbed(false, ctx.Guild, ctx.Channel, ctx.User);
        await UpdateHelpMessage(embed, Service.GetHelpComponents(ctx.Guild, ctx.User)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Returns to the module list of a category.
    /// </summary>
    /// <param name="category">The category to return to</param>
    [ComponentInteraction("helpback:modules:*", true)]
    public async Task HelpBackToModules(string category)
    {
        var (embed, components) = await Service.GetCategoryEmbed(category, ctx.Guild, ctx.Channel, ctx.User);
        await UpdateHelpMessage(embed, components).ConfigureAwait(false);
    }

    private async Task ShowModule(string module, string? section, string? filter)
    {
        var resolved = Service.ResolveModule(module, out _);
        if (resolved is null)
        {
            await RespondAsync(Strings.ModuleNotFoundOrCantExec(ctx.Guild?.Id ?? 0), ephemeral: true)
                .ConfigureAwait(false);
            return;
        }

        if (section is null && filter is null)
        {
            var overview = Service.GetModuleOverview(resolved.Name, ctx.Guild);
            if (overview is not null)
            {
                await UpdateHelpMessage(overview.Value.Embed, overview.Value.Components).ConfigureAwait(false);
                return;
            }
        }

        await ShowCommandList(resolved.Name, section, filter).ConfigureAwait(false);
    }

    private async Task ShowCommandList(string module, string? section, string? filter)
    {
        var resolved = Service.ResolveModule(module, out _);
        if (resolved is null)
        {
            await RespondAsync(Strings.ModuleNotFoundOrCantExec(ctx.Guild?.Id ?? 0), ephemeral: true)
                .ConfigureAwait(false);
            return;
        }

        var builder = await Service.BuildCommandPaginator(resolved.Name, section, filter, ctx.Guild, ctx.User);
        if (builder is null)
        {
            await RespondAsync(Strings.HelpSearchNoResults(ctx.Guild?.Id ?? 0, resolved.Name, filter ?? ""),
                ephemeral: true).ConfigureAwait(false);
            return;
        }

        // Only a component interaction can edit the message it came from. A modal submit has to reply instead,
        // otherwise the paginator has no message to take over.
        var responseType = ctx.Interaction is SocketMessageComponent
            ? InteractionResponseType.UpdateMessage
            : InteractionResponseType.ChannelMessageWithSource;

        await interactivity.SendPaginatorAsync(builder.Build(), ctx.Interaction, TimeSpan.FromMinutes(60),
            responseType).ConfigureAwait(false);
    }

    private async Task UpdateHelpMessage(EmbedBuilder embed, ComponentBuilder components)
    {
        if (ctx.Interaction is SocketMessageComponent component)
        {
            await component.UpdateAsync(x =>
            {
                x.Embed = embed.Build();
                x.Components = components.Build();
            }).ConfigureAwait(false);
            return;
        }

        await RespondAsync(embed: embed.Build(), components: components.Build()).ConfigureAwait(false);
    }

    /// <summary>
    ///     Shows the invite link for the bot.
    /// </summary>
    /// <returns></returns>
    [SlashCommand("invite", "You should invite me to your server and check all my features!")]
    [CheckPermissions]
    public async Task Invite()
    {
        var eb = new EmbedBuilder()
            .AddField(Strings.InviteFieldInvite(ctx.Guild?.Id ?? 0),
                Strings.InviteLinkText(ctx.Guild?.Id ?? 0))
            .AddField(Strings.InviteFieldWebsite(ctx.Guild?.Id ?? 0),
                "https://mewdeko.tech")
            .AddField(Strings.InviteFieldSupport(ctx.Guild?.Id ?? 0),
                config.Data.SupportServer)
            .WithOkColor();
        await ctx.Interaction.RespondAsync(embed: eb.Build());
    }

    /// <summary>
    ///     ALlows you to search for a command using the autocompleter. Can also show help for the command thats chosen from
    ///     autocomplete.
    /// </summary>
    /// <param name="command">The command to search for or to get help for</param>
    [SlashCommand("search", "get information on a specific command")]
    [CheckPermissions]
    public async Task SearchCommand
    (
        [Discord.Interactions.Summary("command", "the command to get information about")]
        [Autocomplete(typeof(GenericCommandAutocompleter))]
        string command
    )
    {
        var com = cmds.Commands.FirstOrDefault(x => x.Aliases.Contains(command));
        if (com == null)
        {
            await Modules().ConfigureAwait(false);
            return;
        }

        var (embed, comp) = await Service.GetCommandHelp(com, ctx.Guild, (ctx.User as IGuildUser)!);
        await RespondAsync(embed: embed.Build(), components: comp.Build()).ConfigureAwait(false);
    }

    /// <summary>
    ///     Allows you to run a command from the commands help.
    /// </summary>
    /// <param name="command">The command in question</param>
    [ComponentInteraction("runcmd.*", true)]
    public async Task RunCmd(string command)
    {
        var com = cmds.Commands.FirstOrDefault(x => x.Aliases.Contains(command));
        if (com.Parameters.Count == 0)
        {
            ch.AddCommandToParseQueue(new MewdekoUserMessage
            {
                Content = await guildSettings.GetPrefix(ctx.Guild) + command, Author = ctx.User, Channel = ctx.Channel
            });
            _ = ch.ExecuteCommandsInChannelAsync(ctx.Channel.Id);
            return;
        }

        await RespondWithModalAsync<CommandModal>($"runcmdmodal.{command}").ConfigureAwait(false);
    }

    /// <summary>
    ///     A modal that displays if the command has any arguments.
    /// </summary>
    /// <param name="command">The command to run</param>
    /// <param name="modal">The modal itself</param>
    [ModalInteraction("runcmdmodal.*", true)]
    public async Task RunModal(string command, CommandModal modal)
    {
        await DeferAsync().ConfigureAwait(false);
        var msg = new MewdekoUserMessage
        {
            Content = $"{await guildSettings.GetPrefix(ctx.Guild)}{command} {modal.Args}",
            Author = ctx.User,
            Channel = ctx.Channel
        };
        ch.AddCommandToParseQueue(msg);
        _ = ch.ExecuteCommandsInChannelAsync(ctx.Channel.Id);
    }

    /// <summary>
    ///     Toggles module descriptions in help.
    /// </summary>
    /// <param name="sDesc">Bool thats parsed to either true or false to show the descriptions</param>
    /// <param name="sId">The server id the button is ran in</param>
    [ComponentInteraction("toggle-descriptions:*,*", true)]
    public async Task ToggleHelpDescriptions(string sDesc, string sId)
    {
        if (ctx.User.Id.ToString() != sId) return;

        await DeferAsync().ConfigureAwait(false);
        var description = bool.TryParse(sDesc, out var desc) && desc;
        var message = (ctx.Interaction as SocketMessageComponent)?.Message;
        var embed = await Service.GetHelpEmbed(description, ctx.Guild, ctx.Channel, ctx.User);

        await message.ModifyAsync(x =>
        {
            x.Embed = embed.Build();
            x.Components = Service.GetHelpComponents(ctx.Guild, ctx.User, !description).Build();
        }).ConfigureAwait(false);
    }
}