using System.IO;
using System.Text;
using System.Text.Json;
using Discord.Commands;
using Fergun.Interactive;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Common.JsonSettings;
using Mewdeko.Modules.Help.Services;
using Mewdeko.Services.Impl;
using Mewdeko.Services.Settings;
using Mewdeko.Services.strings;
using Swan;

namespace Mewdeko.Modules.Help;

/// <summary>
///     A module containing commands for getting help.
/// </summary>
/// <param name="cmds">The command service</param>
/// <param name="strings">Localization strings for the bot</param>
/// <param name="serv">Service for paginated embeds</param>
/// <param name="guildSettings">Service for fetching guildconfigs</param>
/// <param name="config">Service for fetching yml based configs</param>
/// <param name="logger">The logger instance for structured logging.</param>
public class Help(
    CommandService cmds,
    IBotStrings strings,
    InteractiveService serv,
    GuildSettingsService guildSettings,
    BotConfigService config,
    ILogger<Help> logger)
    : MewdekoModuleBase<HelpService>
{
    /// <summary>
    ///     Exports all commands to a json file. Used mainly for https://mewdeko.tech/commands
    /// </summary>
    [Cmd]
    [Aliases]
    [Ratelimit(60)]
    public async Task ExportCommandsJson()
    {
        try
        {
            var msg = await ctx.Channel.SendConfirmAsync(
                Strings.CommandsExportInProgress(ctx.Guild.Id, config.Data.LoadingEmote));
            var prefix = await guildSettings.GetPrefix(ctx.Guild);
            var modules = cmds.Modules;
            var newList = new ConcurrentDictionary<string, List<Command>>();
            var options = new JsonSerializerOptions
            {
                WriteIndented = true, PropertyNamingPolicy = new OrderedResolver()
            };
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

            var jsonVersion = JsonSerializer.Serialize(newList.Select(x => new Module(x.Value, x.Key)), options);
            await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(jsonVersion));
            await ctx.Channel.SendFileAsync(stream,
                Strings.CommandsExport(ctx.Guild.Id, DateTime.UtcNow.ToString("u")));
            await msg.DeleteAsync();
            await stream.DisposeAsync();
        }
        catch (Exception e)
        {
            await ctx.Channel.SendErrorAsync(Strings.CommandsExportError(ctx.Guild.Id), Config);
            logger.LogError(e, "An error has occured while dumping commands to json");
        }
    }

    /// <summary>
    ///     Searches for a command by name or description.
    /// </summary>
    /// <param name="commandname">The term to search for</param>
    [Cmd]
    [Aliases]
    public async Task SearchCommand([Remainder] string commandname)
    {
        var prefix = await guildSettings.GetPrefix(ctx.Guild);
        var commandInfos = cmds.Commands
            .Distinct(new CommandTextEqualityComparer())
            .Where(c => c.Aliases.Any(a => a.Contains(commandname, StringComparison.OrdinalIgnoreCase))
                        || c.RealSummary(strings, ctx.Guild.Id, prefix)
                            .Contains(commandname, StringComparison.OrdinalIgnoreCase))
            .OrderBy(c => c.Aliases[0], StringComparer.OrdinalIgnoreCase)
            .Take(20)
            .ToList();

        if (commandInfos.Count == 0)
        {
            await ctx.Channel.SendErrorAsync(Strings.SearchCommandNotFound(ctx.Guild.Id), Config);
            return;
        }

        var cmdnames = string.Join("\n", commandInfos.Select(c => c.Aliases[0]));
        var cmdremarks = string.Join("\n",
            commandInfos.Select(c => c.RealSummary(strings, ctx.Guild.Id, prefix).Truncate(50)));

        var eb = new EmbedBuilder()
            .WithOkColor()
            .AddField(Strings.SearchCommandTitleCommand(ctx.Guild.Id), cmdnames, true)
            .AddField(Strings.SearchCommandTitleDescription(ctx.Guild.Id), cmdremarks, true);
        await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
    }

    /// <summary>
    ///     Shows a list of all modules. Is basically just a help alias at this point.
    /// </summary>
    [Cmd]
    [Aliases]
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
            await ctx.Channel.SendErrorAsync(Strings.HelpError(ctx.Guild?.Id ?? 0), Config);

            logger.LogError(e, "There was an issue embedding the help command");
        }
    }

    /// <summary>
    ///     SHows how to support the bot.
    /// </summary>
    [Cmd]
    [Aliases]
    public async Task Donate()
    {
        await ctx.Channel.SendConfirmAsync(Strings.DonateMessage(ctx.Guild.Id));
    }

    /// <summary>
    ///     Shows commands for a specific module, optionally narrowed by a search term. If null, is an alias for
    ///     modules which is an alias for help.
    /// </summary>
    /// <param name="module">The module to look at, optionally followed by a term to filter its commands by</param>
    [Cmd]
    [Aliases]
    public async Task Commands([Remainder] string? module = null)
    {
        if (string.IsNullOrWhiteSpace(module))
        {
            await Modules().ConfigureAwait(false);
            return;
        }

        var parts = module.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var filter = parts.Length > 1 ? parts[1] : null;

        var resolved = Service.ResolveModule(parts[0], out var candidates);
        if (resolved is null)
        {
            if (candidates.Count > 1)
            {
                await ReplyErrorAsync(Strings.ModuleAmbiguous(ctx.Guild.Id,
                    string.Join(", ", candidates.Select(c => c.Name)))).ConfigureAwait(false);
                return;
            }

            await ReplyErrorAsync(Strings.ModuleNotFoundOrCantExec(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        if (filter is null)
        {
            var overview = Service.GetModuleOverview(resolved.Name, ctx.Guild);
            if (overview is not null)
            {
                await ctx.Channel.SendMessageAsync(embed: overview.Value.Embed.Build(),
                    components: overview.Value.Components.Build()).ConfigureAwait(false);
                return;
            }
        }

        var builder = await Service.BuildCommandPaginator(resolved.Name, null, filter, ctx.Guild, ctx.User);
        if (builder is null)
        {
            await ReplyErrorAsync(Strings.HelpSearchNoResults(ctx.Guild.Id, resolved.Name, filter ?? ""))
                .ConfigureAwait(false);
            return;
        }

        await serv.SendPaginatorAsync(builder.Build(), ctx.Channel, TimeSpan.FromMinutes(60)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Shows help for a specific command.
    /// </summary>
    /// <param name="toSearch">The string to search for</param>
    [Cmd]
    [Aliases]
    [Priority(0)] // Adjusted priority if needed
    public async Task H([Remainder] string toSearch = null)
    {
        CommandInfo? com = null;

        if (!string.IsNullOrWhiteSpace(toSearch))
        {
            var term = toSearch.Trim();
            com = cmds.Commands.FirstOrDefault(x =>
                x.Aliases.Any(cmdName => cmdName.Equals(term, StringComparison.OrdinalIgnoreCase)));
            if (com == null)
            {
                await ReplyErrorAsync(Strings.CommandNotFound(ctx.Guild.Id)).ConfigureAwait(false);
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
    ///     Shows the guide for the bot.
    /// </summary>
    [Cmd]
    [Aliases]
    public async Task Guide()
    {
        await ctx.Channel.SendConfirmAsync(Strings.GuideMessage(ctx.Guild.Id));
    }

    /// <summary>
    ///     Shows the source code link for the bot.
    /// </summary>
    [Cmd]
    [Aliases]
    public async Task Source()
    {
        await ctx.Channel.SendConfirmAsync(Strings.SourceMessage(ctx.Guild.Id));
    }

    /// <summary>
    ///     Shows a link to vote for mewdeko.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task Vote()
    {
        await ctx.Channel.EmbedAsync(new EmbedBuilder()
            .WithOkColor()
            .WithDescription(Strings.VoteDescription(ctx.Guild.Id)));
    }
}

/// <summary>
///     This class provides a way to compare two CommandInfo objects based on their aliases.
///     It implements the IEqualityComparer interface for CommandInfo objects.
/// </summary>
public class CommandTextEqualityComparer : IEqualityComparer<CommandInfo>
{
    /// <summary>
    ///     Determines whether the specified CommandInfo objects are equal based on their aliases.
    /// </summary>
    /// <param name="x">The first CommandInfo object to compare.</param>
    /// <param name="y">The second CommandInfo object to compare.</param>
    /// <returns>true if the specified CommandInfo objects are equal; otherwise, false.</returns>
    public bool Equals(CommandInfo? x, CommandInfo? y)
    {
        return x.Aliases[0] == y.Aliases[0];
    }

    /// <summary>
    ///     Returns a hash code for the specified CommandInfo object.
    /// </summary>
    /// <param name="obj">The CommandInfo object for which a hash code is to be returned.</param>
    /// <returns>A hash code for the specified object.</returns>
    public int GetHashCode(CommandInfo obj)
    {
        return obj.Aliases[0].GetHashCode(StringComparison.InvariantCulture);
    }
}

/// <summary>
///     Represents a module containing commands. Used only for exporting commands to a json file.
/// </summary>
/// <param name="commands"></param>
/// <param name="name"></param>
public class Module(List<Command> commands, string name)
{
    /// <summary>
    ///     List of commands in the module.
    /// </summary>
    public List<Command> Commands { get; } = commands;

    /// <summary>
    ///     The name of the module.
    /// </summary>
    public string Name { get; } = name;
}

/// <summary>
///     Represents a command. Used only for exporting commands to a json file.
/// </summary>
public class Command
{
    /// <summary>
    ///     The bot version the specified command exists on.
    /// </summary>
    public string BotVersion { get; set; } = StatsService.BotVersion;

    /// <summary>
    ///     Gets or sets a value indicating whether the command is a dragon command. Used to indicate if a command is beta
    ///     only.
    /// </summary>
    public bool IsDragon { get; set; }

    /// <summary>
    ///     The name of a command.
    /// </summary>
    public string CommandName { get; set; }

    /// <summary>
    ///     The description of a command.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    ///     Example(s) of how to use the command.
    /// </summary>
    public List<string> Example { get; set; }

    /// <summary>
    ///     The guild permissions required by the user to use the command.
    /// </summary>
    public string? GuildUserPermissions { get; set; }

    /// <summary>
    ///     The channel permissions required by the user to use the command.
    /// </summary>
    public string? ChannelUserPermissions { get; set; }

    /// <summary>
    ///     The channel permissions required by the bot to use the command.
    /// </summary>
    public string? ChannelBotPermissions { get; set; }

    /// <summary>
    ///     The guild permissions required by the bot to use the command.
    /// </summary>
    public string? GuildBotPermissions { get; set; }
}