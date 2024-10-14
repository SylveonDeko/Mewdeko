using Discord.Commands;
using Discord.Interactions;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Common.Autocompleters;
using Mewdeko.Common.DiscordImplementations;
using Mewdeko.Common.Modals;
using Mewdeko.Modules.Help.Services;
using Mewdeko.Modules.Permissions.Services;
using Mewdeko.Services.Settings;
using RequireDragonAttribute = Mewdeko.Common.Attributes.InteractionCommands.RequireDragonAttribute;

namespace Mewdeko.Modules.Help;

/// <summary>
///     Slash command module for help commands.
/// </summary>
/// <param name="permissionService">The server permission service</param>
/// <param name="interactivity">The service for embed pagination</param>
/// <param name="serviceProvider">Service provider</param>
/// <param name="cmds">The command service</param>
/// <param name="ch">The command handler (yes they are different now shut up)</param>
/// <param name="guildSettings">The service to retrieve guildconfigs</param>
/// <param name="config">Service to retrieve yml based configs</param>
[Discord.Interactions.Group("help", "Help Commands, what else is there to say?")]
public class HelpSlashCommand(
    GlobalPermissionService permissionService,
    InteractiveService interactivity,
    IServiceProvider serviceProvider,
    CommandService cmds,
    CommandHandler ch,
    GuildSettingsService guildSettings,
    BotConfigService config, GlobalPermissionService perms)
    : MewdekoSlashModuleBase<HelpService>
{
    private static readonly ConcurrentDictionary<ulong, ulong> HelpMessages = new();

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
    ///     Handles select menus for the help menu.
    /// </summary>
    /// <param name="unused">Literally unused</param>
    /// <param name="selected">The selected module</param>
    [ComponentInteraction("helpselect:*", true)]
    public async Task HelpSlash(string unused, string[] selected)
    {
        var currentmsg = new MewdekoUserMessage
        {
            Content = "help", Author = ctx.User, Channel = ctx.Channel
        };

        if (HelpMessages.TryGetValue(ctx.Channel.Id, out var msgId))
        {
            try
            {
                await ctx.Channel.DeleteMessageAsync(msgId);
                HelpMessages.TryRemove(ctx.Channel.Id, out _);
            }

            catch
            {
                // ignored
            }
        }

        var module = selected.FirstOrDefault();
        module = module?.Trim().ToUpperInvariant().Replace(" ", "");
        if (string.IsNullOrWhiteSpace(module))
        {
            await Modules().ConfigureAwait(false);
            return;
        }

        var prefix = await guildSettings.GetPrefix(ctx.Guild);

        // Pre-filter commands and create a lookup for blocked commands
        var blockedCommandsSet = new HashSet<string>(perms.BlockedCommands.Select(c => c.ToLowerInvariant()));
        var commandInfos = cmds.Commands
            .Where(c => c.Module.GetTopLevelModule().Name.ToUpperInvariant()
                            .StartsWith(module, StringComparison.InvariantCulture) &&
                        !blockedCommandsSet.Contains(c.Aliases[0].ToLowerInvariant()))
            .Distinct(new CommandTextEqualityComparer())
            .ToList();

        if (!commandInfos.Any())
        {
            await ReplyErrorLocalizedAsync("module_not_found_or_cant_exec").ConfigureAwait(false);
            return;
        }

        // Check preconditions
        var preconditionTasks = commandInfos.Select(async x =>
        {
            var pre = await x.CheckPreconditionsAsync(new CommandContext(ctx.Client, currentmsg), serviceProvider);
            return (Cmd: x, Succ: pre.IsSuccess);
        });
        var preconditionResults = await Task.WhenAll(preconditionTasks).ConfigureAwait(false);
        var succ = new HashSet<CommandInfo>(preconditionResults.Where(x => x.Succ).Select(x => x.Cmd));

        // Group and sort commands, ensuring no duplicates
        var seenCommands = new HashSet<string>();
        var cmdsWithGroup = commandInfos
            .GroupBy(c => c.Module.Name.Replace("Commands", "", StringComparison.InvariantCulture))
            .Select(g => new
            {
                ModuleName = g.Key,
                Commands = g.Where(c => seenCommands.Add(c.Aliases[0].ToLowerInvariant()))
                    .OrderBy(c => c.Aliases[0])
                    .ToList()
            })
            .Where(g => g.Commands.Any())
            .OrderBy(g => g.ModuleName)
            .ToList();

        var pageSize = 24;
        var totalCommands = cmdsWithGroup.Sum(g => g.Commands.Count);
        var totalPages = (int)Math.Ceiling(totalCommands / (double)pageSize);

        var paginator = new LazyPaginatorBuilder()
            .AddUser(ctx.User)
            .WithPageFactory(PageFactory)
            .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
            .WithMaxPageIndex(totalPages - 1)
            .WithDefaultEmotes()
            .WithActionOnCancellation(ActionOnStop.DeleteMessage)
            .Build();

        await interactivity.SendPaginatorAsync(paginator, ctx.Interaction, TimeSpan.FromMinutes(60)).ConfigureAwait(false);

        Task<PageBuilder> PageFactory(int page)
        {
            var pageBuilder = new PageBuilder().WithOkColor();
            var commandsOnPage = new List<string>();
            var currentModule = "";
            var commandCount = 0;

            foreach (var group in cmdsWithGroup)
            {
                foreach (var cmd in group.Commands)
                {
                    if (commandCount >= page * pageSize && commandCount < (page + 1) * pageSize)
                    {
                        if (currentModule != group.ModuleName)
                        {
                            if (commandsOnPage.Any())
                                pageBuilder.AddField(currentModule,
                                    $"```css\n{string.Join("\n", commandsOnPage)}\n```");
                            commandsOnPage.Clear();
                            currentModule = group.ModuleName;
                        }

                        var cmdString =
                            $"{(succ.Contains(cmd) ? cmd.Preconditions.Any(p => p is RequireDragonAttribute) ? "🐉" : "✅" : "❌")}" +
                            $"{prefix}{cmd.Aliases[0]}" +
                            $"{(cmd.Aliases.Skip(1).FirstOrDefault() is not null ? $"/{prefix}{cmd.Aliases[1]}" : "")}";
                        commandsOnPage.Add(cmdString);
                    }

                    commandCount++;
                    if (commandCount >= (page + 1) * pageSize) break;
                }

                if (commandCount >= (page + 1) * pageSize) break;
            }

            if (commandsOnPage.Any())
                pageBuilder.AddField(currentModule, $"```css\n{string.Join("\n", commandsOnPage)}\n```");

            pageBuilder.WithDescription(
                $"✅: You can use this command.\n❌: You cannot use this command.\n" +
                $"{config.Data.LoadingEmote}: If you need any help don't hesitate to join [The Support Server](https://discord.gg/mewdeko)\n" +
                $"Do `{prefix}h commandname` to see info on that command");

            return Task.FromResult(pageBuilder);
        }
    }

    /// <summary>
    ///     Shows the invite link for the bot.
    /// </summary>
    /// <returns></returns>
    [SlashCommand("invite", "You should invite me to your server and check all my features!")]
    [CheckPermissions]
    public Task Invite()
    {
        var eb = new EmbedBuilder()
            .AddField("Invite Link",
                "[Mewdeko](https://discord.com/oauth2/authorize?client_id=752236274261426212&scope=bot&permissions=66186303)\n[Mewdeko Nightly](https://discord.com/oauth2/authorize?client_id=964590728397344868&scope=bot&permissions=66186303)")
            .AddField("Website/Docs", "https://mewdeko.tech")
            .AddField("Support Server", config.Data.SupportServer)
            .WithOkColor();
        return ctx.Interaction.RespondAsync(embed: eb.Build());
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
            _ = Task.Run(() => ch.ExecuteCommandsInChannelAsync(ctx.Channel.Id)).ConfigureAwait(false);
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
        _ = Task.Run(() => ch.ExecuteCommandsInChannelAsync(ctx.Channel.Id)).ConfigureAwait(false);
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