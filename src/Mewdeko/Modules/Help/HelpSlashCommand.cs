using System.Threading.Tasks;
using Discord.Commands;
using Discord.Interactions;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Common.Autocompleters;
using Mewdeko.Common.DiscordImplementations;
using Mewdeko.Common.Modals;
using Mewdeko.Modules.Help.Services;
using Mewdeko.Modules.Permissions.Services;
using Mewdeko.Services.Settings;

namespace Mewdeko.Modules.Help;

[Discord.Interactions.Group("help", "Help Commands, what else is there to say?")]
public class HelpSlashCommand : MewdekoSlashModuleBase<HelpService>
{
    private readonly InteractiveService interactivity;
    private readonly IServiceProvider serviceProvider;
    private readonly GlobalPermissionService permissionService;
    private readonly CommandService cmds;
    private readonly GuildSettingsService guildSettings;
    private readonly CommandHandler ch;
    private readonly BotConfigService config;

    public HelpSlashCommand(
        GlobalPermissionService permissionService,
        InteractiveService interactivity,
        IServiceProvider serviceProvider,
        CommandService cmds,
        CommandHandler ch,
        GuildSettingsService guildSettings,
        BotConfigService config)
    {
        this.permissionService = permissionService;
        this.interactivity = interactivity;
        this.serviceProvider = serviceProvider;
        this.cmds = cmds;
        this.ch = ch;
        this.guildSettings = guildSettings;
        this.config = config;
    }

    [SlashCommand("help", "Shows help on how to use the bot"), CheckPermissions]
    public async Task Modules()
    {
        var embed = await Service.GetHelpEmbed(false, ctx.Guild, ctx.Channel, ctx.User);
        await RespondAsync(embed: embed.Build(), components: Service.GetHelpComponents(ctx.Guild, ctx.User).Build()).ConfigureAwait(false);
    }

    [ComponentInteraction("helpselect", true)]
    public async Task HelpSlash(string[] selected)
    {
        var currentmsg = new MewdekoUserMessage
        {
            Content = "help", Author = ctx.User
        };
        var module = selected.FirstOrDefault();
        module = module?.Trim().ToUpperInvariant().Replace(" ", "");
        if (string.IsNullOrWhiteSpace(module))
        {
            await Modules().ConfigureAwait(false);
            return;
        }

        var prefix = await guildSettings.GetPrefix(ctx.Guild);
        // Find commands for that module
        // don't show commands which are blocked
        // order by name
        var commandInfos = this.cmds.Commands.Where(c =>
                c.Module.GetTopLevelModule().Name.ToUpperInvariant()
                    .StartsWith(module, StringComparison.InvariantCulture) && !permissionService.BlockedCommands.Contains(c.Aliases[0].ToLowerInvariant()))
            .OrderBy(c => c.Aliases[0])
            .Distinct(new CommandTextEqualityComparer());
        // check preconditions for all commands, but only if it's not 'all'
        // because all will show all commands anyway, no need to check
        var succ = new HashSet<CommandInfo>((await Task.WhenAll(commandInfos.Select(async x =>
            {
                var pre = await x.CheckPreconditionsAsync(new CommandContext(ctx.Client, currentmsg), serviceProvider).ConfigureAwait(false);
                return (Cmd: x, Succ: pre.IsSuccess);
            })).ConfigureAwait(false))
            .Where(x => x.Succ)
            .Select(x => x.Cmd));

        var cmdsWithGroup = commandInfos
            .GroupBy(c => c.Module.Name.Replace("Commands", "", StringComparison.InvariantCulture))
            .OrderBy(x => x.Key == x.First().Module.Name ? int.MaxValue : x.Count());

        if (!commandInfos.Any())
        {
            await ReplyErrorLocalizedAsync("module_not_found_or_cant_exec").ConfigureAwait(false);
            return;
        }

        var i = 0;
        var groups = cmdsWithGroup.GroupBy(_ => i++ / 48).ToArray();
        var paginator = new LazyPaginatorBuilder()
            .AddUser(ctx.User)
            .WithPageFactory(PageFactory)
            .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
            .WithMaxPageIndex(groups.Select(x => x.Count()).FirstOrDefault() - 1)
            .WithDefaultEmotes()
            .Build();

        await interactivity.SendPaginatorAsync(paginator, ctx.Interaction as SocketInteraction,
            TimeSpan.FromMinutes(60)).ConfigureAwait(false);

        async Task<PageBuilder> PageFactory(int page)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            var transformed = groups.Select(x => x.ElementAt(page).Where(commandInfo => !commandInfo.Attributes.Any(attribute => attribute is HelpDisabled)).Select(commandInfo =>
                    $"{(succ.Contains(commandInfo) ? "✅" : "❌")}{prefix + commandInfo.Aliases[0]}{(commandInfo.Aliases.Skip(1).FirstOrDefault() is not null ? $"/{prefix}{commandInfo.Aliases[1]}" : "")}"))
                .FirstOrDefault();
            var last = groups.Select(x => x.Count()).FirstOrDefault();
            for (i = 0; i < last; i++)
            {
                if (i != last - 1 || (i + 1) % 1 == 0) continue;
                var grp = 0;
                var count = transformed.Count();
                transformed = transformed
                    .GroupBy(_ => grp++ % count / 2)
                    .Select(x => x.Count() == 1 ? $"{x.First()}" : string.Concat(x));
            }

            return new PageBuilder()
                .AddField(groups.Select(x => x.ElementAt(page).Key).FirstOrDefault(),
                    $"```css\n{string.Join("\n", transformed)}\n```")
                .WithDescription(
                    $"✅: You can use this command.\n❌: You cannot use this command.\n{config.Data.LoadingEmote}: If you need any help don't hesitate to join [The Support Server](https://discord.gg/mewdeko)\nDo `{prefix}h commandname` to see info on that command")
                .WithOkColor();
        }
    }

    [SlashCommand("invite", "You should invite me to your server and check all my features!"), CheckPermissions]
    public async Task Invite()
    {
        var eb = new EmbedBuilder()
            .AddField("Invite Link",
                "[Click Here](https://discord.com/oauth2/authorize?client_id=752236274261426212&scope=bot&permissions=66186303&scope=bot%20applications.commands)")
            .AddField("Website/Docs", "https://mewdeko.tech")
            .AddField("Support Server", "https://discord.gg/mewdeko")
            .WithOkColor();
        await ctx.Interaction.RespondAsync(embed: eb.Build()).ConfigureAwait(false);
    }

    [SlashCommand("search", "get information on a specific command"), CheckPermissions]
    public async Task SearchCommand
    (
        [Discord.Interactions.Summary("command", "the command to get information about"), Autocomplete(typeof(GenericCommandAutocompleter))]
        string command
    )
    {
        var com = cmds.Commands.FirstOrDefault(x => x.Aliases.Contains(command));
        if (com == null)
        {
            await Modules().ConfigureAwait(false);
            return;
        }

        var comp = new ComponentBuilder().WithButton(GetText("help_run_cmd"), $"runcmd.{command}", ButtonStyle.Success, disabled: com.Parameters.Count != 0);

        var embed = await Service.GetCommandHelp(com, ctx.Guild);
        await RespondAsync(embed: embed.Build(), components: comp.Build()).ConfigureAwait(false);
    }

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
            _ = Task.Run(() => ch.ExecuteCommandsInChannelAsync(ctx.Channel.Id)).ConfigureAwait(false);
            return;
        }

        await RespondWithModalAsync<CommandModal>($"runcmdmodal.{command}").ConfigureAwait(false);
    }

    [ModalInteraction("runcmdmodal.*", true)]
    public async Task RunModal(string command, CommandModal modal)
    {
        await DeferAsync().ConfigureAwait(false);
        var msg = new MewdekoUserMessage
        {
            Content = $"{await guildSettings.GetPrefix(ctx.Guild)}{command} {modal.Args}", Author = ctx.User, Channel = ctx.Channel
        };
        ch.AddCommandToParseQueue(msg);
        _ = Task.Run(() => ch.ExecuteCommandsInChannelAsync(ctx.Channel.Id)).ConfigureAwait(false);
    }

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