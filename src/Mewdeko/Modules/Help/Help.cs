using Discord.Commands;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Help.Services;
using Mewdeko.Modules.Permissions.Services;
using Mewdeko.Services.Settings;
using Mewdeko.Services.strings;
using Swan;
using System.Threading.Tasks;
using Serilog;

namespace Mewdeko.Modules.Help;

public class Help : MewdekoModuleBase<HelpService>
{
    private readonly CommandService cmds;
    private readonly InteractiveService interactive;
    private readonly GlobalPermissionService perms;
    private readonly IServiceProvider services;
    private readonly IBotStrings strings;
    private readonly GuildSettingsService guildSettings;
    private readonly BotConfigService config;

    public Help(GlobalPermissionService perms, CommandService cmds,
        IServiceProvider services, IBotStrings strings,
        InteractiveService serv,
        GuildSettingsService guildSettings,
        BotConfigService config)
    {
        interactive = serv;
        this.guildSettings = guildSettings;
        this.config = config;
        this.cmds = cmds;
        this.perms = perms;
        this.services = services;
        this.strings = strings;
    }

    [Cmd, Aliases]
    public async Task SearchCommand(string commandname)
    {
        var cmds = this.cmds.Commands.Distinct().Where(c => c.Name.Contains(commandname, StringComparison.InvariantCulture));
        if (!cmds.Any())
        {
            await ctx.Channel.SendErrorAsync(
                "That command wasn't found! Please retry your search with a different term.").ConfigureAwait(false);
        }
        else
        {
            string? cmdnames = null;
            string? cmdremarks = null;
            foreach (var i in cmds)
            {
                cmdnames += $"\n{i.Name}";
                cmdremarks += $"\n{i.RealSummary(strings, ctx.Guild.Id, await guildSettings.GetPrefix(ctx.Guild)).Truncate(50)}";
            }
            var eb = new EmbedBuilder()
                     .WithOkColor()
                     .AddField("Command", cmdnames, true)
                     .AddField("Description", cmdremarks, true);
            await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
        }
    }

    [Cmd, Aliases]
    public async Task Modules()
    {
        var embed = await Service.GetHelpEmbed(false, ctx.Guild ?? null, ctx.Channel, ctx.User);
        try
        {
            await ctx.Channel.SendMessageAsync(embed: embed.Build(), components: Service.GetHelpComponents(ctx.Guild, ctx.User).Build()).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            Log.Information(e.ToString());
        }
    }

    [Cmd, Aliases]
    public async Task Donate() =>
        await ctx.Channel.SendConfirmAsync(
            "If you would like to support the project, here's how:\nKo-Fi: https://ko-fi.com/mewdeko\nI appreciate any donations as they will help improve Mewdeko for the better!").ConfigureAwait(false);

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
        var cmds = this.cmds.Commands.Where(c =>
                c.Module.GetTopLevelModule().Name.ToUpperInvariant()
                    .StartsWith(module, StringComparison.InvariantCulture))
            .Where(c => !perms.BlockedCommands.Contains(c.Aliases[0].ToLowerInvariant()))
            .OrderBy(c => c.Aliases[0])
            .Distinct(new CommandTextEqualityComparer());

        // check preconditions for all commands, but only if it's not 'all'
        // because all will show all commands anyway, no need to check
        var succ = new HashSet<CommandInfo>((await Task.WhenAll(cmds.Select(async x =>
            {
                var pre = await x.CheckPreconditionsAsync(Context, services).ConfigureAwait(false);
                return (Cmd: x, Succ: pre.IsSuccess);
            })).ConfigureAwait(false))
            .Where(x => x.Succ)
            .Select(x => x.Cmd));

        var cmdsWithGroup = cmds
            .GroupBy(c => c.Module.Name.Replace("Commands", "", StringComparison.InvariantCulture))
            .OrderBy(x => x.Key == x.First().Module.Name ? int.MaxValue : x.Count());

        if (!cmds.Any())
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

        await interactive.SendPaginatorAsync(paginator, Context.Channel,
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

    [Cmd, Aliases, Priority(0)]
    public async Task H([Remainder] string fail)
    {
        var prefixless =
            cmds.Commands.FirstOrDefault(x => x.Aliases.Any(cmdName => cmdName.ToLowerInvariant() == fail));
        if (prefixless != null)
        {
            await H(prefixless).ConfigureAwait(false);
            return;
        }

        await ReplyErrorLocalizedAsync("command_not_found").ConfigureAwait(false);
    }

    [Cmd, Aliases, Priority(1)]
    public async Task H([Remainder] CommandInfo? com = null)
    {
        var channel = ctx.Channel;

        if (com == null)
        {
            await Modules().ConfigureAwait(false);
            return;
        }

        var comp = new ComponentBuilder().WithButton(GetText("help_run_cmd"), $"runcmd.{com.Aliases[0]}", ButtonStyle.Success);
        var embed = await Service.GetCommandHelp(com, ctx.Guild);
        await channel.SendMessageAsync(embed: embed.Build(), components: comp.Build()).ConfigureAwait(false);
    }

    [Cmd, Aliases]
    public async Task Guide() => await ctx.Channel.SendConfirmAsync("You can find the website at https://mewdeko.tech").ConfigureAwait(false);
    [Cmd, Aliases]
    public async Task Source() => await ctx.Channel.SendConfirmAsync("https://github.com/Sylveon76/Mewdeko").ConfigureAwait(false);
}

public class CommandTextEqualityComparer : IEqualityComparer<CommandInfo>
{
    public bool Equals(CommandInfo? x, CommandInfo? y) => x.Aliases[0] == y.Aliases[0];

    public int GetHashCode(CommandInfo obj) => obj.Aliases[0].GetHashCode(StringComparison.InvariantCulture);
}

