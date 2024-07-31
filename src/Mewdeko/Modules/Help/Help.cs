using System.IO;
using System.Text;
using Discord.Commands;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Common.JsonSettings;
using Mewdeko.Modules.Help.Services;
using Mewdeko.Modules.Permissions.Services;
using Mewdeko.Services.Impl;
using Mewdeko.Services.Settings;
using Mewdeko.Services.strings;
using Newtonsoft.Json;
using Serilog;
using Swan;

namespace Mewdeko.Modules.Help;

/// <summary>
/// A module containing commands for getting help.
/// </summary>
/// <param name="perms">The per server permission service</param>
/// <param name="cmds">The command service</param>
/// <param name="services">The service provider</param>
/// <param name="strings">Localization strings for the bot</param>
/// <param name="serv">Service for paginated embeds</param>
/// <param name="guildSettings">Service for fetching guildconfigs</param>
/// <param name="config">Service for fetching yml based configs</param>
public class Help(
    GlobalPermissionService perms,
    CommandService cmds,
    IServiceProvider services,
    IBotStrings strings,
    InteractiveService serv,
    GuildSettingsService guildSettings,
    BotConfigService config)
    : MewdekoModuleBase<HelpService>
{
    /// <summary>
    /// Exports all commands to a json file. Used mainly for https://mewdeko.tech/commands
    /// </summary>
    [Cmd, Aliases, Ratelimit(60)]
    public async Task ExportCommandsJson()
    {
        try
        {
            var msg = await ctx.Channel.SendConfirmAsync(
                $"{config.Data.LoadingEmote} Exporting commands to json, please wait a moment...");
            var prefix = await guildSettings.GetPrefix(ctx.Guild);
            var modules = cmds.Modules;
            var newList = new ConcurrentDictionary<string, List<Command>>();
            foreach (var i in modules)
            {
                var modulename = i.IsSubmodule ? i.Parent.Name : i.Name;
                var commands = (from j in i.Commands.OrderByDescending(x => x.Name)
                    let userPerm = j.Preconditions.FirstOrDefault(ca => ca is UserPermAttribute) as UserPermAttribute
                    let botPerm = j.Preconditions.FirstOrDefault(ca => ca is BotPermAttribute) as BotPermAttribute
                    let isDragon =
                        j.Preconditions.FirstOrDefault(ca => ca is RequireDragonAttribute) as RequireDragonAttribute
                    select new Command
                    {
                        BotVersion = StatsService.BotVersion,
                        CommandName = j.Aliases.Any() ? j.Aliases[0] : j.Name,
                        Description = j.RealSummary(strings, ctx.Guild.Id, prefix),
                        Example = j.RealRemarksArr(strings, ctx.Guild.Id, prefix).ToList() ?? [],
                        GuildUserPermissions =
                            userPerm?.UserPermissionAttribute.GuildPermission != null
                                ? userPerm.UserPermissionAttribute.GuildPermission.ToString()
                                : "",
                        ChannelUserPermissions =
                            userPerm?.UserPermissionAttribute.ChannelPermission != null
                                ? userPerm.UserPermissionAttribute.ChannelPermission.ToString()
                                : "",
                        GuildBotPermissions =
                            botPerm?.GuildPermission != null ? botPerm.GuildPermission.ToString() : "",
                        ChannelBotPermissions =
                            botPerm?.ChannelPermission != null ? botPerm.ChannelPermission.ToString() : "",
                        IsDragon = isDragon is not null
                    }).ToList();
                newList.AddOrUpdate(modulename, commands, (_, old) =>
                {
                    old.AddRange(commands);
                    return old;
                });
            }

            var settings = new JsonSerializerSettings
            {
                ContractResolver = new OrderedResolver()
            };
            var jsonVersion = JsonConvert.SerializeObject(newList.Select(x => new Module(x.Value, x.Key)),
                Formatting.Indented, settings);
            await using var stream = new MemoryStream(Encoding.Default.GetBytes(jsonVersion));
            await ctx.Channel.SendFileAsync(stream, $"Commands-{DateTime.UtcNow:u}.json");
            await msg.DeleteAsync();
            await stream.DisposeAsync();
        }
        catch (Exception e)
        {
            await ctx.Channel.SendErrorAsync(
                "Seems like there was an issue dumping commands to a json file. Please check console for issues.",
                Config);
            Log.Error(e, "An error has occured while dumping commands to json");
        }
    }

    /// <summary>
    /// Searches for a command by name or description.
    /// </summary>
    /// <param name="commandname">The term to search for</param>
    [Cmd, Aliases]
    public async Task SearchCommand(string commandname)
    {
        var commandInfos = cmds.Commands.Distinct()
            .Where(c => c.Name.Contains(commandname, StringComparison.InvariantCulture));
        if (!commandInfos.Any())
        {
            await ctx.Channel.SendErrorAsync(
                    "That command wasn't found! Please retry your search with a different term.", Config)
                .ConfigureAwait(false);
        }
        else
        {
            string? cmdnames = null;
            string? cmdremarks = null;
            foreach (var i in commandInfos)
            {
                cmdnames += $"\n{i.Name}";
                cmdremarks +=
                    $"\n{i.RealSummary(strings, ctx.Guild.Id, await guildSettings.GetPrefix(ctx.Guild)).Truncate(50)}";
            }

            var eb = new EmbedBuilder()
                .WithOkColor()
                .AddField("Command", cmdnames, true)
                .AddField("Description", cmdremarks, true);
            await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Shows a list of all modules. Is basically just a help alias at this point.
    /// </summary>
    [Cmd, Aliases]
    public async Task Modules()
    {
        var embed = await Service.GetHelpEmbed(false, ctx.Guild ?? null, ctx.Channel, ctx.User);
        try
        {
            await ctx.Channel
                .SendMessageAsync(embed: embed.Build(),
                    components: Service.GetHelpComponents(ctx.Guild, ctx.User).Build()).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            await ctx.Channel.SendErrorAsync(
                "There was an issue sending the help command, please check console and report to the support server.",
                Config);
            Log.Error(e, "There was an issue embedding the help command");
        }
    }

    /// <summary>
    /// SHows how to support the bot.
    /// </summary>
    [Cmd, Aliases]
    public async Task Donate() =>
        await ctx.Channel.SendConfirmAsync(
                "If you would like to support the project, here's how:\nKo-Fi: https://ko-fi.com/mewdeko\nI appreciate any donations as they will help improve Mewdeko for the better!")
            .ConfigureAwait(false);

    /// <summary>
    /// Shows commands for a specific module. If null, is an alias for modules which is an alias for help.
    /// </summary>
    /// <param name="module">The module to look at</param>
    [Cmd, Aliases]
    public async Task Commands([Remainder] string? module = null)
    {
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
        var commandInfos = cmds.Commands.Where(c =>
                c.Module.GetTopLevelModule().Name.ToUpperInvariant()
                    .StartsWith(module, StringComparison.InvariantCulture))
            .Where(c => !perms.BlockedCommands.Contains(c.Aliases[0].ToLowerInvariant()))
            .OrderBy(c => c.Aliases[0])
            .Distinct(new CommandTextEqualityComparer());

        // check preconditions for all commands, but only if it's not 'all'
        // because all will show all commands anyway, no need to check
        var succ = new HashSet<CommandInfo>((await Task.WhenAll(commandInfos.Select(async x =>
            {
                var pre = await x.CheckPreconditionsAsync(Context, services).ConfigureAwait(false);
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
            .WithActionOnCancellation(ActionOnStop.DeleteMessage)
            .Build();

        await serv.SendPaginatorAsync(paginator, Context.Channel,
            TimeSpan.FromMinutes(60)).ConfigureAwait(false);

        async Task<PageBuilder> PageFactory(int page)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            var transformed = groups.Select(x => x.ElementAt(page)
                    .Where(commandInfo => !commandInfo.Attributes.Any(attribute => attribute is HelpDisabled)).Select(
                        commandInfo =>
                            $"{(succ.Contains(commandInfo) ? commandInfo.Preconditions.Any(preconditionAttribute => preconditionAttribute is RequireDragonAttribute) ? "🐉" : "✅" : "❌")}{prefix + commandInfo.Aliases[0]}{(commandInfo.Aliases.Skip(1).FirstOrDefault() is not null ? $"/{prefix}{commandInfo.Aliases[1]}" : "")}"))
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

    /// <summary>
    /// Shows help for a specific command.
    /// </summary>
    /// <param name="toSearch">The string to search for</param>
    [Cmd, Aliases, Priority(0)] // Adjusted priority if needed
    public async Task H([Remainder] string toSearch = null)
    {
        CommandInfo? com = null;

        if (!string.IsNullOrWhiteSpace(toSearch))
        {
            com = cmds.Commands.FirstOrDefault(x => x.Aliases.Any(cmdName => cmdName.ToLowerInvariant() == toSearch));
            if (com == null)
            {
                await ReplyErrorLocalizedAsync("command_not_found").ConfigureAwait(false);
                return;
            }
        }

        var channel = ctx.Channel;

        if (com == null)
        {
            await Modules().ConfigureAwait(false);
            return;
        }

        var (embed, comp) = await Service.GetCommandHelp(com, ctx.Guild, (ctx.User as IGuildUser)!);
        await channel.SendMessageAsync(embed: embed.Build(), components: comp.Build()).ConfigureAwait(false);
    }


    /// <summary>
    /// Shows the guide for the bot.
    /// </summary>
    [Cmd, Aliases]
    public async Task Guide() => await ctx.Channel.SendConfirmAsync("You can find the website at https://mewdeko.tech")
        .ConfigureAwait(false);

    /// <summary>
    /// Shows the source code link for the bot.
    /// </summary>
    [Cmd, Aliases]
    public async Task Source() =>
        await ctx.Channel.SendConfirmAsync("https://github.com/SylveonDeko/Mewdeko").ConfigureAwait(false);

    /// <summary>
    /// Shows a link to vote for mewdeko.
    /// </summary>
    [Cmd, Aliases, RequireContext(ContextType.Guild)]
    public async Task Vote() =>
        await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                .WithDescription(
                    "Vote here for Mewdeko!\n[Vote Link](https://top.gg/bot/752236274261426212)\nMake sure to join the support server! \n[Link](https://mewdeko.tech/support)"))
            .ConfigureAwait(false);
}

/// <summary>
/// This class provides a way to compare two CommandInfo objects based on their aliases.
/// It implements the IEqualityComparer interface for CommandInfo objects.
/// </summary>
public class CommandTextEqualityComparer : IEqualityComparer<CommandInfo>
{
    /// <summary>
    /// Determines whether the specified CommandInfo objects are equal based on their aliases.
    /// </summary>
    /// <param name="x">The first CommandInfo object to compare.</param>
    /// <param name="y">The second CommandInfo object to compare.</param>
    /// <returns>true if the specified CommandInfo objects are equal; otherwise, false.</returns>
    public bool Equals(CommandInfo? x, CommandInfo? y) => x.Aliases[0] == y.Aliases[0];

    /// <summary>
    /// Returns a hash code for the specified CommandInfo object.
    /// </summary>
    /// <param name="obj">The CommandInfo object for which a hash code is to be returned.</param>
    /// <returns>A hash code for the specified object.</returns>
    public int GetHashCode(CommandInfo obj) => obj.Aliases[0].GetHashCode(StringComparison.InvariantCulture);
}

/// <summary>
/// Represents a module containing commands. Used only for exporting commands to a json file.
/// </summary>
/// <param name="commands"></param>
/// <param name="name"></param>
public class Module(List<Command> commands, string name)
{
    /// <summary>
    /// List of commands in the module.
    /// </summary>
    public List<Command> Commands { get; } = commands;

    /// <summary>
    /// The name of the module.
    /// </summary>
    public string Name { get; } = name;
}

/// <summary>
/// Represents a command. Used only for exporting commands to a json file.
/// </summary>
public class Command
{
    /// <summary>
    /// The bot version the specified command exists on.
    /// </summary>
    public string BotVersion { get; set; } = StatsService.BotVersion;
    /// <summary>
    /// Gets or sets a value indicating whether the command is a dragon command. Used to indicate if a command is beta only.
    /// </summary>
    public bool IsDragon { get; set; }

    /// <summary>
    /// The name of a command.
    /// </summary>
    public string CommandName { get; set; }

    /// <summary>
    /// The description of a command.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Example(s) of how to use the command.
    /// </summary>
    public List<string> Example { get; set; }

    /// <summary>
    /// The guild permissions required by the user to use the command.
    /// </summary>
    public string? GuildUserPermissions { get; set; }

    /// <summary>
    /// The channel permissions required by the user to use the command.
    /// </summary>
    public string? ChannelUserPermissions { get; set; }

    /// <summary>
    /// The channel permissions required by the bot to use the command.
    /// </summary>
    public string? ChannelBotPermissions { get; set; }

    /// <summary>
    /// The guild permissions required by the bot to use the command.
    /// </summary>
    public string? GuildBotPermissions { get; set; }
}