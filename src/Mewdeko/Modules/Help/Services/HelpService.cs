using System.Reflection;
using System.Text;
using CommandLine;
using Discord.Commands;
using Discord.Interactions;
using Discord.Rest;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Modules.Administration.Services;
using Mewdeko.Modules.OwnerOnly.Services;
using Mewdeko.Modules.Permissions.Common;
using Mewdeko.Modules.Permissions.Services;
using Mewdeko.Services.Settings;
using Mewdeko.Services.strings;
using Mewdeko.Services.Strings;
using ModuleInfo = Discord.Commands.ModuleInfo;

namespace Mewdeko.Modules.Help.Services;

/// <summary>
///     A service for handling help commands.
/// </summary>
public class HelpService : INService, IReadyExecutor
{
    private const int CommandsPerPage = 10;
    private const int MaxFieldLength = 1024;
    private const int SubmoduleIndexThreshold = 30;
    private const string UncategorizedCategory = "other";

    private static readonly string[] CategoryOrder =
        ["moderation", "serversetup", "fun", "economy", "music", "utility", "owner", UncategorizedCategory];

    private static readonly IReadOnlyDictionary<string, string> ModuleCategories =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Administration"] = "moderation",
            ["Moderation"] = "moderation",
            ["Permissions"] = "moderation",
            ["ServerManagement"] = "moderation",
            ["RoleStates"] = "moderation",
            ["StatusRoles"] = "moderation",
            ["CountingModeration"] = "moderation",
            ["MultiGreets"] = "serversetup",
            ["RoleGreets"] = "serversetup",
            ["Starboard"] = "serversetup",
            ["StatChannels"] = "serversetup",
            ["Suggestions"] = "serversetup",
            ["Tickets"] = "serversetup",
            ["Confessions"] = "serversetup",
            ["CustomVoice"] = "serversetup",
            ["ChatTriggers"] = "serversetup",
            ["PollCommands"] = "serversetup",
            ["Giveaways"] = "serversetup",
            ["Games"] = "fun",
            ["Counting"] = "fun",
            ["Nsfw"] = "fun",
            ["Searches"] = "fun",
            ["Minecraft"] = "fun",
            ["Switch"] = "fun",
            ["Currency"] = "economy",
            ["Xp"] = "economy",
            ["Reputation"] = "economy",
            ["Vote"] = "economy",
            ["Patreon"] = "economy",
            ["Music"] = "music",
            ["Utility"] = "utility",
            ["Help"] = "utility",
            ["Afk"] = "utility",
            ["Todo"] = "utility",
            ["UserProfile"] = "utility",
            ["Birthday"] = "utility",
            ["Highlights"] = "utility",
            ["CoprMonitoring"] = "utility",
            ["OwnerOnly"] = "owner",
            ["InstanceManagement"] = "owner"
        };

    private readonly BlacklistService blacklistService;
    private readonly Mewdeko bot;
    private readonly BotConfigService bss;
    private readonly ConcurrentDictionary<ulong, IReadOnlyCollection<RestGuildCommand>> cachedGuildCommands = new();
    private readonly DiscordShardedClient client;
    private readonly CommandService cmds;
    private readonly DiscordPermOverrideService dpos;
    private readonly GeneratedBotStrings genStrings;
    private readonly GuildSettingsService guildSettings;
    private readonly InteractionService interactionService;
    private readonly ILocalization localization;
    private readonly ILogger<HelpService> logger;
    private readonly PermissionService nPerms;
    private readonly GlobalPermissionService perms;
    private readonly IBotStrings strings;
    private readonly IBotStringsProvider stringsProvider;

    // Cached slash commands - fetched once at startup
    private IReadOnlyCollection<RestGlobalCommand>? cachedGlobalCommands;


    /// <summary>
    ///     Initializes a new instance of <see cref="HelpService" />.
    /// </summary>
    /// <param name="strings">Bot localization strings</param>
    /// <param name="dpos">Permission override service for commands</param>
    /// <param name="bss">Settings service for yml based configs</param>
    /// <param name="client">The discord client</param>
    /// <param name="bot">The bot itself</param>
    /// <param name="blacklistService">The user/server blacklist service</param>
    /// <param name="cmds">The command service</param>
    /// <param name="perms">The global permissions service</param>
    /// <param name="nPerms">The per server permission service</param>
    /// <param name="interactionService">The discord interaction service</param>
    /// <param name="guildSettings">Service to get guild configs</param>
    /// <param name="eventHandler">The event handler Sylveon made because the events in dnet were single threaded.</param>
    /// <param name="genStrings">The class that holds generated locale strings.</param>
    /// <param name="stringsProvider">The raw strings provider, used for convention based module descriptions.</param>
    /// <param name="localization">The localization service used to resolve a guild's culture.</param>
    /// <param name="logger">The logger instance.</param>
    public HelpService(
        IBotStrings strings,
        DiscordPermOverrideService dpos,
        BotConfigService bss,
        DiscordShardedClient client,
        Mewdeko bot,
        BlacklistService blacklistService,
        CommandService cmds,
        GlobalPermissionService perms,
        PermissionService nPerms,
        InteractionService interactionService,
        GuildSettingsService guildSettings,
        EventHandler eventHandler,
        GeneratedBotStrings genStrings,
        IBotStringsProvider stringsProvider,
        ILocalization localization,
        ILogger<HelpService> logger)
    {
        this.dpos = dpos;
        this.strings = strings;
        this.stringsProvider = stringsProvider;
        this.localization = localization;
        this.client = client;
        this.bot = bot;
        this.blacklistService = blacklistService;
        this.cmds = cmds;
        this.bss = bss;
        this.logger = logger;
        eventHandler.Subscribe("MessageReceived", "HelpService", HandlePing);
        eventHandler.Subscribe("JoinedGuild", "HelpService", HandleJoin);
        this.perms = perms;
        this.nPerms = nPerms;
        this.interactionService = interactionService;
        this.guildSettings = guildSettings;
        this.genStrings = genStrings;
    }

    /// <summary>
    ///     Caches global slash commands on bot ready. Called by IReadyExecutor.
    /// </summary>
    public async Task OnReadyAsync()
    {
        try
        {
            cachedGlobalCommands = await client.Rest.GetGlobalApplicationCommands().ConfigureAwait(false);
            logger.LogInformation("Cached {Count} global slash commands", cachedGlobalCommands.Count);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to cache global slash commands");
        }

        var uncategorized = GetVisibleModules()
            .Where(m => !ModuleCategories.ContainsKey(m.Name))
            .Select(m => m.Name)
            .ToList();
        if (uncategorized.Count > 0)
        {
            logger.LogWarning(
                "Modules {Modules} have no help category and will show under '{Fallback}'. Add them to HelpService.ModuleCategories",
                string.Join(", ", uncategorized), UncategorizedCategory);
        }

        var undescribed = GetVisibleModules()
            .Where(m => stringsProvider.GetText("en-US", $"module_description_{m.Name.ToLowerInvariant()}") is null)
            .Select(m => m.Name)
            .ToList();
        if (undescribed.Count > 0)
        {
            logger.LogWarning("Modules {Modules} have no module_description_ key in en-US help.json",
                string.Join(", ", undescribed));
        }
    }

    /// <summary>
    ///     Gets cached guild commands, fetching and caching if not already cached.
    /// </summary>
    private async Task<IReadOnlyCollection<RestGuildCommand>?> GetGuildCommandsAsync(ulong guildId)
    {
        if (cachedGuildCommands.TryGetValue(guildId, out var commands))
            return commands;

        try
        {
            var guildCommands = await client.Rest.GetGuildApplicationCommands(guildId).ConfigureAwait(false);
            cachedGuildCommands.TryAdd(guildId, guildCommands);
            return guildCommands;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to fetch guild slash commands for {GuildId}", guildId);
            return null;
        }
    }

    /// <summary>
    ///     Executes the help text when someone attempts to dm the bot with a bad command
    /// </summary>
    /// <param name="DiscordShardedClient">The client</param>
    /// <param name="guild">The guild (hopefully null otherwise this method is useless)</param>
    /// <param name="msg">The message of the user</param>
    /// <returns></returns>
    public async Task BadCommand(DiscordShardedClient DiscordShardedClient, IGuild? guild, IUserMessage msg)
    {
        var settings = bss.Data;
        if (guild != null) return;
        if (string.IsNullOrWhiteSpace(settings.DmHelpText) || settings.DmHelpText == "-")
            return;
        var replacer = new ReplacementBuilder()
            .WithDefault(msg.Author, msg.Channel, null, DiscordShardedClient).Build();
        if (SmartEmbed.TryParse(replacer.Replace(settings.DmHelpText), null, out var embed, out var plainText,
                out var components))
            await msg.Channel.SendMessageAsync(plainText, embeds: embed, components: components?.Build());
        else
            await msg.Channel.SendMessageAsync(settings.DmHelpText);
    }

    /// <summary>
    ///     Builds the category select menu shown on the landing help menu.
    /// </summary>
    /// <param name="guild">The guild the help menu was executed in, may be null if in dm</param>
    /// <param name="user">The user that executed the help menu</param>
    /// <param name="descriptions">Whether the module lists per category are expanded</param>
    /// <returns>A <see cref="ComponentBuilder" /> instance with the bots categories in it</returns>
    public ComponentBuilder GetHelpComponents(IGuild? guild, IUser user, bool descriptions = true)
    {
        var guildId = guild?.Id ?? 0;
        var compBuilder = new ComponentBuilder();
        var selMenu = new SelectMenuBuilder()
            .WithCustomId("helpcat")
            .WithPlaceholder(genStrings.HelpSelectCategory(guildId));

        foreach (var category in GetPopulatedCategories())
        {
            var modules = GetModulesInCategory(category);
            selMenu.Options.Add(new SelectMenuOptionBuilder()
                .WithLabel(GetCategoryName(category, guildId))
                .WithDescription(genStrings.HelpCategoryCounts(guildId, modules.Count,
                    modules.Sum(CountVisibleCommands)))
                .WithValue(category));
        }

        compBuilder.WithSelectMenu(selMenu);

        compBuilder.WithButton(genStrings.ToggleDescriptions(guildId),
            $"toggle-descriptions:{descriptions},{user.Id}");
        compBuilder.WithButton(genStrings.InviteMe(guildId), style: ButtonStyle.Link,
            url:
            "https://discord.com/oauth2/authorize?client_id=752236274261426212&scope=bot&permissions=66186303&scope=bot%20applications.commands");
        compBuilder.WithButton(genStrings.Donatetext(guildId), style: ButtonStyle.Link,
            url: "https://ko-fi.com/mewdeko");
        return compBuilder;
    }


    /// <summary>
    ///     Builds the landing help embed listing the command categories.
    /// </summary>
    /// <param name="description">Whether each category is expanded into its module list</param>
    /// <param name="guild">The guild where the help menu was executed</param>
    /// <param name="channel">The channel where the help menu was executed</param>
    /// <param name="user">The user who executed the help menu</param>
    /// <returns>An <see cref="EmbedBuilder" /> listing every populated category</returns>
    public async Task<EmbedBuilder> GetHelpEmbed(bool description, IGuild? guild, IMessageChannel channel, IUser user)
    {
        var guildId = guild?.Id ?? 0;
        var prefix = await guildSettings.GetPrefix(guild);
        EmbedBuilder embed = new();
        embed.WithAuthor(new EmbedAuthorBuilder()
            .WithName(genStrings.HelpmenuHelptext(guildId, client.CurrentUser))
            .WithIconUrl(client.CurrentUser.RealAvatarUrl().AbsoluteUri));
        embed.WithOkColor();
        embed.WithDescription(
            genStrings.HelpCategoriesDescription(guildId, prefix) +
            $"\n\n[Documentation](https://mewdeko.tech) | [Support Server]({bss.Data.SupportServer}) | [Invite Me](https://discord.com/oauth2/authorize?client_id={bot.Client.CurrentUser.Id}&scope=bot&permissions=66186303&scope=bot%20applications.commands) | [Top.gg Listing](https://top.gg/bot/752236274261426212) | [Donate!](https://ko-fi.com/mewdeko)");

        var categories = GetPopulatedCategories();

        if (description)
        {
            foreach (var category in categories)
            {
                var modules = GetModulesInCategory(category);
                var lines = await Task.WhenAll(modules.Select(async m =>
                    $"> {await CheckEnabled(guild?.Id, channel, user, m.Name)} {Format.Bold(m.Name)}"));
                embed.AddField(GetCategoryName(category, guildId), string.Join("\n", lines), true);
            }
        }
        else
        {
            var lines = new List<string>();
            foreach (var category in categories)
            {
                var modules = GetModulesInCategory(category);
                lines.Add(
                    $"> {Format.Bold(GetCategoryName(category, guildId))} - {genStrings.HelpCategoryCounts(guildId, modules.Count, modules.Sum(CountVisibleCommands))}");
            }

            embed.AddField(genStrings.HelpCategoriesTitle(guildId), string.Join("\n", lines));
        }

        return embed;
    }

    /// <summary>
    ///     Builds the embed and components listing every module inside a category.
    /// </summary>
    /// <param name="category">The category key</param>
    /// <param name="guild">The guild where the help menu was executed</param>
    /// <param name="channel">The channel where the help menu was executed</param>
    /// <param name="user">The user who executed the help menu</param>
    /// <returns>A tuple containing an <see cref="EmbedBuilder" /> and <see cref="ComponentBuilder" /></returns>
    public async Task<(EmbedBuilder Embed, ComponentBuilder Components)> GetCategoryEmbed(string category,
        IGuild? guild, IMessageChannel channel, IUser user)
    {
        var guildId = guild?.Id ?? 0;
        var modules = GetModulesInCategory(category);
        var embed = new EmbedBuilder()
            .WithOkColor()
            .WithTitle(GetCategoryName(category, guildId))
            .WithDescription(genStrings.HelpCategoryCounts(guildId, modules.Count,
                modules.Sum(CountVisibleCommands)));

        // A stale component from before a restart can name a category that no longer has modules, and Discord
        // rejects a select menu with zero options, so fall back to the back button on its own.
        if (modules.Count == 0)
        {
            return (embed.WithDescription(genStrings.ModuleNotFoundOrCantExec(guildId)),
                new ComponentBuilder().WithButton(genStrings.HelpBack(guildId), "helpback:categories",
                    ButtonStyle.Secondary));
        }

        var selMenu = new SelectMenuBuilder()
            .WithCustomId($"helpmodule:{category}")
            .WithPlaceholder(genStrings.HelpSelectModule(guildId));

        foreach (var module in modules)
        {
            var count = CountVisibleCommands(module);
            embed.AddField($"{await CheckEnabled(guild?.Id, channel, user, module.Name)} {module.Name}",
                $">>> {GetModuleDescription(module.Name, guild) ?? genStrings.HelpCommandCount(guildId, count)}", true);
            selMenu.Options.Add(new SelectMenuOptionBuilder()
                .WithLabel(module.Name)
                .WithDescription(genStrings.HelpCommandCount(guildId, count))
                .WithValue(module.Name.ToLowerInvariant()));
        }

        var components = new ComponentBuilder()
            .WithSelectMenu(selMenu)
            .WithButton(genStrings.HelpBack(guildId), "helpback:categories", ButtonStyle.Secondary);

        return (embed, components);
    }

    /// <summary>
    ///     Builds the section index for a module, letting users jump straight to a submodule instead of paging
    ///     through every command. Returns null when the module is small enough to list directly.
    /// </summary>
    /// <param name="moduleName">The top level module name</param>
    /// <param name="guild">The guild where the help menu was executed</param>
    /// <returns>
    ///     A tuple containing an <see cref="EmbedBuilder" /> and <see cref="ComponentBuilder" />, or null if the
    ///     module should be listed directly
    /// </returns>
    public (EmbedBuilder Embed, ComponentBuilder Components)? GetModuleOverview(string moduleName, IGuild? guild)
    {
        var guildId = guild?.Id ?? 0;
        if (!HasSectionIndex(moduleName))
            return null;

        var commands = GetModuleCommands(moduleName);
        var sections = GroupBySubmodule(commands);

        var embed = new EmbedBuilder()
            .WithOkColor()
            .WithTitle(moduleName)
            .WithDescription(genStrings.HelpModuleOverview(guildId,
                GetModuleDescription(moduleName, guild) ?? genStrings.HelpCommandCount(guildId, commands.Count)));

        var selMenu = new SelectMenuBuilder()
            .WithCustomId($"helpsection:{moduleName.ToLowerInvariant()}")
            .WithPlaceholder(genStrings.HelpSelectSection(guildId));

        foreach (var section in sections.Take(25))
        {
            embed.AddField(section.Name, genStrings.HelpCommandCount(guildId, section.Commands.Count), true);
            selMenu.Options.Add(new SelectMenuOptionBuilder()
                .WithLabel(section.Name)
                .WithDescription(genStrings.HelpCommandCount(guildId, section.Commands.Count))
                .WithValue(section.Name.ToLowerInvariant()));
        }

        var components = new ComponentBuilder()
            .WithSelectMenu(selMenu)
            .WithButton(genStrings.HelpAllCommands(guildId), $"helpall:{moduleName.ToLowerInvariant()}")
            .WithButton(genStrings.HelpSearchButton(guildId), $"helpsearch:{moduleName.ToLowerInvariant()}",
                ButtonStyle.Secondary)
            .WithButton(genStrings.HelpBack(guildId), $"helpback:modules:{GetCategoryFor(moduleName)}",
                ButtonStyle.Secondary);

        return (embed, components);
    }

    /// <summary>
    ///     Builds the paginator listing the commands of a module, optionally narrowed to a single section or
    ///     filtered by a search term. Shared by the text and slash help entry points.
    /// </summary>
    /// <param name="moduleName">The top level module name</param>
    /// <param name="section">The submodule to restrict the listing to, or null for every command</param>
    /// <param name="filter">A search term matched against command aliases and descriptions, or null</param>
    /// <param name="guild">The guild where the help menu was executed</param>
    /// <param name="user">The user allowed to control the paginator</param>
    /// <returns>A configured <see cref="ComponentPaginatorBuilder" />, or null when nothing matched</returns>
    public async Task<ComponentPaginatorBuilder?> BuildCommandPaginator(string moduleName, string? section,
        string? filter, IGuild? guild, IUser user)
    {
        var guildId = guild?.Id ?? 0;
        var prefix = await guildSettings.GetPrefix(guild);
        var commands = GetModuleCommands(moduleName);

        if (!string.IsNullOrWhiteSpace(section))
        {
            commands = commands
                .Where(c => SubmoduleName(c).Equals(section, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (!string.IsNullOrWhiteSpace(filter))
        {
            commands = commands
                .Where(c => c.Aliases.Any(a => a.Contains(filter, StringComparison.OrdinalIgnoreCase))
                            || c.RealSummary(strings, guildId, prefix)
                                .Contains(filter, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (commands.Count == 0)
            return null;

        var sections = GroupBySubmodule(commands);
        var totalPages = (int)Math.Ceiling(commands.Count / (double)CommandsPerPage);

        var header = string.IsNullOrWhiteSpace(filter)
            ? string.IsNullOrWhiteSpace(section) ? moduleName : $"{moduleName} - {section}"
            : genStrings.HelpSearchResults(guildId, filter, moduleName);

        // Going back has to land somewhere useful: modules with a section index return to it, the rest return to
        // their category's module list.
        var backId = HasSectionIndex(moduleName)
            ? $"helpoverview:{moduleName.ToLowerInvariant()}"
            : $"helpback:modules:{GetCategoryFor(moduleName)}";

        return new ComponentPaginatorBuilder()
            .AddUser(user)
            .WithPageCount(totalPages)
            .WithPageFactory(PageFactory)
            .WithActionOnCancellation(ActionOnStop.DeleteMessage)
            // Back replaces the message with a nav embed while this paginator is still running, so a later timeout
            // must not touch the message. Stop is safe because Back has already removed the stop button by then.
            .WithActionOnTimeout(ActionOnStop.None);

        IPage PageFactory(IComponentPaginator paginator)
        {
            var page = paginator.CurrentPageIndex;
            var pageBuilder = new PageBuilder()
                .WithOkColor()
                .WithTitle(header)
                .WithDescription(genStrings.HelpModuleListHint(guildId, prefix))
                .WithFooter($"{page + 1}/{totalPages}");

            var skipped = 0;
            var taken = 0;

            foreach (var group in sections)
            {
                if (taken >= CommandsPerPage)
                    break;

                if (skipped + group.Commands.Count <= page * CommandsPerPage)
                {
                    skipped += group.Commands.Count;
                    continue;
                }

                var offset = Math.Max(0, page * CommandsPerPage - skipped);
                var entries = group.Commands.Skip(offset).Take(CommandsPerPage - taken).ToList();
                skipped += offset + entries.Count;
                taken += entries.Count;

                AddChunkedField(pageBuilder, group.Name,
                    entries.Select(c => FormatCommandListEntry(c, prefix, guildId)));
            }

            var components = new ComponentBuilder()
                .AddFirstButton(paginator, emote: new Emoji("⏮"))
                .AddPreviousButton(paginator, emote: new Emoji("◀"))
                .AddJumpButton(paginator, emote: new Emoji("🔢"))
                .AddNextButton(paginator, emote: new Emoji("▶"))
                .AddLastButton(paginator, emote: new Emoji("⏭"))
                .WithButton(genStrings.HelpBack(guildId), backId, ButtonStyle.Secondary, row: 1)
                .AddStopButton(paginator, emote: new Emoji("🗑"), row: 1);

            return pageBuilder.WithComponents(components.Build()).Build();
        }
    }

    /// <summary>
    ///     Gets whether a module is large enough to be presented as a section index rather than a flat listing.
    /// </summary>
    /// <param name="moduleName">The top level module name</param>
    /// <returns>True when the module gets a section index</returns>
    public bool HasSectionIndex(string moduleName)
    {
        var commands = GetModuleCommands(moduleName);
        return commands.Count > SubmoduleIndexThreshold && GroupBySubmodule(commands).Count >= 3;
    }

    /// <summary>
    ///     Adds the given lines under one field name, splitting into continuation fields whenever the accumulated
    ///     text would exceed Discord's per field character limit.
    /// </summary>
    private static void AddChunkedField(PageBuilder page, string name, IEnumerable<string> lines)
    {
        var current = new StringBuilder();
        var first = true;

        foreach (var line in lines)
        {
            if (current.Length > 0 && current.Length + line.Length + 1 > MaxFieldLength)
            {
                page.AddField(first ? name : "​", current.ToString());
                current.Clear();
                first = false;
            }

            if (current.Length > 0)
                current.Append('\n');
            current.Append(line);
        }

        if (current.Length > 0)
            page.AddField(first ? name : "​", current.ToString());
    }

    private string FormatCommandListEntry(CommandInfo cmd, string prefix, ulong guildId)
    {
        var summary = cmd.RealSummary(strings, guildId, prefix);
        summary = string.IsNullOrWhiteSpace(summary)
            ? genStrings.NoDescriptionAvailable(guildId)
            : summary.Replace('\n', ' ').Trim();
        if (summary.Length > 70)
            summary = $"{summary[..67]}...";

        return $"`{prefix}{cmd.Aliases[0]}` {summary}";
    }

    /// <summary>
    ///     Resolves user input to a top level module, accepting an exact name or an unambiguous prefix.
    /// </summary>
    /// <param name="input">The module name typed by the user</param>
    /// <param name="matches">Every module matching the input as a prefix</param>
    /// <returns>The single matching module, or null when there is no match or the input is ambiguous</returns>
    public ModuleInfo? ResolveModule(string input, out List<ModuleInfo> matches)
    {
        var trimmed = input.Trim().Replace(" ", "");
        var modules = GetVisibleModules();

        var exact = modules.Find(m => m.Name.Equals(trimmed, StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
        {
            matches = [exact];
            return exact;
        }

        matches = modules
            .Where(m => m.Name.StartsWith(trimmed, StringComparison.OrdinalIgnoreCase))
            .ToList();
        return matches.Count == 1 ? matches[0] : null;
    }

    /// <summary>
    ///     Gets every top level module that has at least one visible command.
    /// </summary>
    /// <returns>The list of visible top level modules, ordered by name</returns>
    public List<ModuleInfo> GetVisibleModules()
    {
        return cmds.Commands
            .Select(c => c.Module.GetTopLevelModule())
            .Where(m => !m.Attributes.Any(a => a is HelpDisabled))
            .DistinctBy(m => m.Name)
            .OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private List<ModuleInfo> GetModulesInCategory(string category)
    {
        return GetVisibleModules()
            .Where(m => GetCategoryFor(m.Name) == category)
            .ToList();
    }

    private List<string> GetPopulatedCategories()
    {
        var present = GetVisibleModules().Select(m => GetCategoryFor(m.Name)).ToHashSet();
        return CategoryOrder.Where(present.Contains).ToList();
    }

    private static string GetCategoryFor(string moduleName)
    {
        return ModuleCategories.GetValueOrDefault(moduleName, UncategorizedCategory);
    }

    private string GetCategoryName(string category, ulong guildId)
    {
        return category switch
        {
            "moderation" => genStrings.HelpCategoryModeration(guildId),
            "serversetup" => genStrings.HelpCategoryServersetup(guildId),
            "fun" => genStrings.HelpCategoryFun(guildId),
            "economy" => genStrings.HelpCategoryEconomy(guildId),
            "music" => genStrings.HelpCategoryMusic(guildId),
            "utility" => genStrings.HelpCategoryUtility(guildId),
            "owner" => genStrings.HelpCategoryOwner(guildId),
            _ => genStrings.HelpCategoryOther(guildId)
        };
    }

    private List<CommandInfo> GetModuleCommands(string moduleName)
    {
        var blocked = perms.BlockedCommands.Select(c => c.ToLowerInvariant()).ToHashSet();
        return cmds.Commands
            .Where(c => c.Module.GetTopLevelModule().Name.Equals(moduleName, StringComparison.OrdinalIgnoreCase))
            .Where(c => !blocked.Contains(c.Aliases[0].ToLowerInvariant()))
            .Distinct(new CommandTextEqualityComparer())
            .OrderBy(c => c.Aliases[0], StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private int CountVisibleCommands(ModuleInfo module)
    {
        return GetModuleCommands(module.Name).Count;
    }

    private static string SubmoduleName(CommandInfo cmd)
    {
        return cmd.Module.Name.Replace("Commands", "", StringComparison.InvariantCulture);
    }

    private static List<(string Name, List<CommandInfo> Commands)> GroupBySubmodule(List<CommandInfo> commands)
    {
        return commands
            .GroupBy(SubmoduleName)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => (g.Key, g.ToList()))
            .ToList();
    }

    private async Task<string> CheckEnabled(ulong? guildId, IMessageChannel channel, IUser user, string moduleName)
    {
        if (!guildId.HasValue)
            return "✅";
        var pc = await nPerms.GetCacheFor(guildId.Value);
        if (perms.BlockedModules.Contains(moduleName.ToLower())) return "🌐❌";
        return !pc.Permissions.CheckSlashPermissions(moduleName, "none", user, channel, out _) ? "❌" : "✅";
    }

    private string? GetModuleDescription(string module, IGuild? guild)
    {
        var locale = localization.GetCultureInfo(guild?.Id).Name;
        var key = $"module_description_{module.ToLowerInvariant()}";
        return stringsProvider.GetText(locale, key) ?? stringsProvider.GetText("en-US", key);
    }

    private async Task HandlePing(SocketMessage msg)
    {
        if (msg.Content == $"<@{client.CurrentUser.Id}>" || msg.Content == $"<@!{client.CurrentUser.Id}>")
        {
            if (msg.Channel is ITextChannel chan)
            {
                var cb = new ComponentBuilder();
                var prefix = await guildSettings.GetPrefix(chan.Guild);
                var eb = new EmbedBuilder();
                eb.WithOkColor();
                eb.WithDescription(
                    $"Hi there! To see my command categories do `{prefix}cmds`\nMy current Prefix is `{prefix}`\nIf you need help using the bot feel free to join the [Support Server]({bss.Data.SupportServer})!\n**Please support me! While this bot is free it's not free to run! https://ko-fi.com/mewdeko**\n\n I hope you have a great day!");
                eb.WithThumbnailUrl("https://cdn.discordapp.com/emojis/914307922287276052.gif");
                eb.WithFooter(new EmbedFooterBuilder().WithText(client.CurrentUser.Username)
                    .WithIconUrl(client.CurrentUser.RealAvatarUrl().ToString()));

                if (bss.Data.ShowInviteButton)
                    cb.WithButton("Invite Me!", style: ButtonStyle.Link,
                            url:
                            "https://discord.com/oauth2/authorize?client_id=752236274261426212&permissions=8&response_type=code&redirect_uri=https%3A%2F%2Fmewdeko.tech&scope=bot%20applications.commands")
                        .WithButton("Support Us!", style: ButtonStyle.Link, url: "https://ko-fi.com/Mewdeko");

                await chan.SendMessageAsync(embed: eb.Build(),
                    components: bss.Data.ShowInviteButton ? cb.Build() : null).ConfigureAwait(false);
            }
        }
    }

    private async Task HandleJoin(IGuild guild)
    {
        if (blacklistService.BlacklistEntries.Select(x => x.ItemId).Contains(guild.Id))
            return;

        var cb = new ComponentBuilder();
        var e = await guild.GetDefaultChannelAsync();
        var px = await guildSettings.GetPrefix(guild);
        var eb = new EmbedBuilder
        {
            Description =
                $"Hi, thanks for inviting Mewdeko! I hope you like the bot, and discover all its features! The default prefix is `{px}.` This can be changed with the prefix command."
        };
        eb.AddField("How to look for commands",
            $"1) Use the {px}cmds command to see all the categories\n2) use {px}cmds with the category name to glance at what commands it has. ex: `{px}cmds mod`\n3) Use {px}h with a command name to view its help. ex: `{px}h purge`");
        eb.AddField("Have any questions, or need my invite link?",
            "Support Server: https://discord.gg/mewdeko \nInvite Link: https://mewdeko.tech/invite");
        eb.AddField("Youtube Channel", "https://youtube.com/channel/UCKJEaaZMJQq6lH33L3b_sTg");
        eb.WithThumbnailUrl(
            "https://cdn.discordapp.com/emojis/968564817784877066.gif");
        eb.WithOkColor();
        if (bss.Data.ShowInviteButton)
            cb.WithButton("Invite Me!", style: ButtonStyle.Link,
                    url:
                    "https://discord.com/oauth2/authorize?client_id=752236274261426212&permissions=8&response_type=code&redirect_uri=https%3A%2F%2Fmewdeko.tech&scope=bot%20applications.commands")
                .WithButton("Support Us!", style: ButtonStyle.Link, url: "https://ko-fi.com/Mewdeko");
        await e.SendMessageAsync(embed: eb.Build(), components: bss.Data.ShowInviteButton ? cb.Build() : null)
            .ConfigureAwait(false);
    }


    /// <summary>
    ///     Gets the help for a command
    /// </summary>
    /// <param name="com">The command in question</param>
    /// <param name="guild">The guild where this was executed</param>
    /// <param name="user">The user who executed the command</param>
    /// <returns>A tuple containing a <see cref="EmbedBuilder" /> and <see cref="ComponentBuilder" /></returns>
    public async Task<(EmbedBuilder, ComponentBuilder)> GetCommandHelp(CommandInfo com, IGuild? guild, IGuildUser user)
    {
        var actualUrl = GenerateDocumentationUrl(com);
        if (com.Attributes.Any(x => x is HelpDisabled))
            return (new EmbedBuilder().WithDescription(genStrings.HelpDisabled(guild?.Id ?? 0)),
                new ComponentBuilder());

        var prefix = await guildSettings.GetPrefix(guild);
        var potentialCommand = interactionService.SlashCommands.FirstOrDefault(x =>
            string.Equals(x.MethodName, com.MethodName(), StringComparison.CurrentCultureIgnoreCase));

        var str = $"**{prefix + com.Aliases[0]}**";
        var alias = com.Aliases.Skip(1).FirstOrDefault();
        if (alias != null)
            str += $" **| {prefix + alias}**";

        var em = new EmbedBuilder().AddField(fb =>
            fb.WithName(str).WithValue($"{com.RealSummary(strings, guild?.Id, prefix)}").WithIsInline(true));

        var tryGetOverrides = dpos.TryGetOverrides(guild.Id, com.Name, out var overrides);
        var reqs = GetCommandRequirements(com, tryGetOverrides ? overrides : null);
        var botReqs = GetCommandBotRequirements(com);
        var attribute = (RatelimitAttribute)com.Preconditions.FirstOrDefault(x => x is RatelimitAttribute);

        if (reqs.Length > 0)
            em.AddField("User Permissions", string.Join("\n", reqs));
        if (botReqs.Length > 0)
            em.AddField("Bot Permissions", string.Join("\n", botReqs));
        if (actualUrl != null)
            em.AddField("Documentation", $"[Click here]({actualUrl})");
        if (attribute?.Seconds > 0)
            em.AddField("Cooldown", $"{attribute.Seconds} seconds");

        var cb = new ComponentBuilder()
            .WithButton(genStrings.HelpRunCmd(guild?.Id ?? 0), $"runcmd.{com.Aliases[0]}", ButtonStyle.Success);

        if (user.GuildPermissions.Administrator)
            cb.WithButton(genStrings.HelpPermenuLink(guild.Id), $"permenu_update.{com.Aliases[0]}", ButtonStyle
                .Primary, Emote.Parse("<:IconPrivacySettings:845090111976636446>"));

        if (potentialCommand is not null)
        {
            // Use cached commands instead of REST calls
            var globalCommand =
                cachedGlobalCommands?.FirstOrDefault(x => x.Name == potentialCommand.Module.SlashGroupName);
            var guildCommands = await GetGuildCommandsAsync(guild.Id).ConfigureAwait(false);
            var guildCommand = guildCommands?.FirstOrDefault(x => x.Name == potentialCommand.Module.SlashGroupName);

            if (globalCommand is not null)
                em.AddField("Slash Command",
                    $"</{potentialCommand.Module.SlashGroupName} {potentialCommand.Name}:{globalCommand.Id}>");
            else if (guildCommand is not null)
                em.AddField("Slash Command",
                    $"</{potentialCommand.Module.SlashGroupName} {potentialCommand.Name}:{guildCommand.Id}>");
        }

        // Get command strings from YAML documentation
        var commandStrings = strings.GetCommandStrings(com.Name, guild?.Id);

        // Add parameter descriptions if available
        if (commandStrings.Parameters?.Count > 0)
        {
            var sb = new StringBuilder();
            foreach (var param in commandStrings.Parameters)
            {
                var optionalText = param.IsOptional
                    ? $" (Optional{(string.IsNullOrEmpty(param.DefaultValue) ? "" : $", default: {param.DefaultValue}")})"
                    : "";

                sb.AppendLine($"• `{param.Name}`{optionalText}: {param.Description}");
            }

            em.AddField(genStrings.Parameters(guild?.Id ?? 0), sb.ToString());
        }

        // Add overload information if available
        if (commandStrings.Overloads?.Count > 0)
        {
            var sb = new StringBuilder();

            // Show the main command format first
            var mainParams = string.Join(" ", com.Parameters.Select(p =>
                p.IsOptional ? $"[{p.Name}]" : p.Name));
            sb.AppendLine($"**{prefix}{com.Name} {mainParams}**");

            // Show overloads
            sb.AppendLine("\n**Other versions:**");

            foreach (var overload in commandStrings.Overloads)
            {
                var overloadParams = string.Join(" ", overload.Parameters.Select(p =>
                    p.IsOptional ? $"[{p.Name}]" : p.Name));

                sb.AppendLine($"• **{prefix}{com.Name} {overloadParams}**");

                // Add detailed parameter descriptions for this overload if needed
                if (overload.Parameters?.Count > 0)
                {
                    foreach (var param in overload.Parameters)
                    {
                        var optionalText = param.IsOptional
                            ? $" (Optional{(string.IsNullOrEmpty(param.DefaultValue) ? "" : $", default: {param.DefaultValue}")})"
                            : "";

                        sb.AppendLine($"  → `{param.Name}`{optionalText}: {param.Description}");
                    }
                }
            }

            em.AddField(genStrings.Overloads(guild?.Id ?? 0), sb.ToString());
        }

        em.AddField(fb => fb.WithName(genStrings.Usage(guild.Id)).WithValue(string.Join("\n",
                    Array.ConvertAll(com.RealRemarksArr(strings, guild?.Id, prefix),
                        arg => Format.Code(arg))))
                .WithIsInline(false))
            .WithFooter(
                $"Module: {com.Module.GetTopLevelModule().Name} || Submodule: {com.Module.Name.Replace("Commands", "")} || Method Name: {com.MethodName()}")
            .WithColor(Mewdeko.OkColor);

        var opt = ((MewdekoOptionsAttribute)com.Attributes.FirstOrDefault(x => x is MewdekoOptionsAttribute))
            ?.OptionType;
        if (opt == null) return (em, cb);
        var hs = GetCommandOptionHelp(opt);
        if (!string.IsNullOrWhiteSpace(hs))
            em.AddField(genStrings.Options(guild.Id), hs);

        if (bss.Data.ShowInviteButton)
            cb.WithButton(style: ButtonStyle.Link,
                    url:
                    "https://discord.com/oauth2/authorize?client_id=752236274261426212&permissions=8&response_type=code&redirect_uri=https%3A%2F%2Fmewdeko.tech&scope=bot%20applications.commands",
                    label: "Invite Me!",
                    emote: "<a:HaneMeow:968564817784877066>".ToIEmote())
                .WithButton("Support Us!", style: ButtonStyle.Link, url: "https://ko-fi.com/Mewdeko");

        return (em, cb);
    }

    private static string GetCommandOptionHelp(Type opt)
    {
        var strs = GetCommandOptionHelpList(opt);

        return string.Join("\n", strs);
    }

    private static List<string> GetCommandOptionHelpList(Type opt)
    {
        return opt.GetProperties()
            .Select(x => Array.Find(x.GetCustomAttributes(true), a => a is OptionAttribute))
            .Where(x => x != null).Cast<OptionAttribute>().Select(x =>
            {
                var toReturn = $"`--{x.LongName}`";

                if (!string.IsNullOrWhiteSpace(x.ShortName))
                    toReturn += $" (`-{x.ShortName}`)";

                toReturn += $"   {x.HelpText}  ";
                return toReturn;
            }).ToList();
    }

    private static string[] GetCommandRequirements(CommandInfo cmd, GuildPermission? overrides = null)
    {
        var toReturn = new List<string>();

        if (cmd.Preconditions.Any(x => x is OwnerOnlyAttribute))
            toReturn.Add("Bot Owner Only");

        var userPerm = (UserPermAttribute)cmd.Preconditions.FirstOrDefault(ca => ca is UserPermAttribute);

        var userPermString = string.Empty;
        if (userPerm is not null)
        {
            if (userPerm.UserPermissionAttribute.ChannelPermission is { } cPerm)
                userPermString = GetPreconditionString(cPerm);
            if (userPerm.UserPermissionAttribute.GuildPermission is { } gPerm)
                userPermString = GetPreconditionString(gPerm);
        }

        if (overrides is null)
        {
            if (!string.IsNullOrWhiteSpace(userPermString))
                toReturn.Add(userPermString);
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(userPermString))
                toReturn.Add(Format.Strikethrough(userPermString));

            toReturn.Add(GetPreconditionString(overrides.Value));
        }

        return toReturn.ToArray();
    }

    /// <summary>
    /// </summary>
    /// <param name="commandName"></param>
    /// <param name="overloads"></param>
    /// <param name="prefix"></param>
    /// <returns></returns>
    public string FormatCommandHelp(string commandName, List<OwnerOnlyService.CommandInfo> overloads, string prefix)
    {
        var sb = new StringBuilder();

        // If there's only one version, format it normally
        if (overloads.Count == 1 && !overloads[0].IsOverload)
        {
            var cmd = overloads[0];
            sb.AppendLine($"**{prefix}{commandName}**");
            sb.AppendLine(cmd.Desc);

            // Add usage examples
            if (cmd.Args.Count > 0)
            {
                sb.AppendLine("\n**Usage:**");
                foreach (var usage in cmd.Args)
                {
                    sb.AppendLine($"`{prefix}{commandName} {usage}`");
                }
            }

            // Add parameter descriptions if available
            if (cmd.Parameters.Count > 0)
            {
                sb.AppendLine("\n**Parameters:**");
                foreach (var param in cmd.Parameters)
                {
                    var optional = param.IsOptional
                        ? " (Optional" + (param.DefaultValue != null ? $", default: {param.DefaultValue}" : "") + ")"
                        : "";
                    var paramDesc = !string.IsNullOrEmpty(param.Description) ? $" - {param.Description}" : "";
                    sb.AppendLine($"• `{param.Name}`: {param.Type}{optional}{paramDesc}");
                }
            }
        }
        else
        {
            // Multiple overloads
            sb.AppendLine($"**{prefix}{commandName}** (Multiple Versions)");

            // Add the first description (they should be similar)
            sb.AppendLine(overloads[0].Desc);

            // Show each overload
            sb.AppendLine("\n**Overloads:**");

            for (var i = 0; i < overloads.Count; i++)
            {
                var cmd = overloads[i];

                // Format parameters for this overload
                var paramList = string.Join(", ", cmd.Parameters.Select(p =>
                {
                    var paramString = $"{p.Name}: {p.Type}";
                    if (p.IsOptional) paramString = $"[{paramString}]";
                    return paramString;
                }));

                sb.AppendLine($"\n**Version {i + 1}:** `{prefix}{commandName} {paramList}`");

                // Add parameter descriptions
                if (cmd.Parameters.Count > 0)
                {
                    sb.AppendLine("Parameters:");
                    foreach (var param in cmd.Parameters)
                    {
                        var optional = param.IsOptional
                            ? " (Optional" + (param.DefaultValue != null ? $", default: {param.DefaultValue}" : "") +
                              ")"
                            : "";
                        var paramDesc = !string.IsNullOrEmpty(param.Description) ? $" - {param.Description}" : "";
                        sb.AppendLine($"• `{param.Name}`: {param.Type}{optional}{paramDesc}");
                    }
                }

                // Add usage examples for this overload
                if (cmd.Args.Count > 0)
                {
                    sb.AppendLine("Examples:");
                    foreach (var usage in cmd.Args)
                    {
                        sb.AppendLine($"`{prefix}{commandName} {usage}`");
                    }
                }
            }
        }

        return sb.ToString();
    }

    private static string[] GetCommandBotRequirements(CommandInfo cmd)
    {
        var toReturn = new List<string>();

        if (cmd.Preconditions.Any(x => x is OwnerOnlyAttribute))
            toReturn.Add("Bot Owner Only");

        var botPerm = (BotPermAttribute)cmd.Preconditions.FirstOrDefault(ca => ca is BotPermAttribute);

        var botPermString = string.Empty;
        if (botPerm is not null)
        {
            if (botPerm.ChannelPermission is { } cPerm)
                botPermString = GetPreconditionString(cPerm);
            if (botPerm.GuildPermission is { } gPerm)
                botPermString = GetPreconditionString(gPerm);
        }

        if (!string.IsNullOrWhiteSpace(botPermString))
            toReturn.Add(botPermString);

        return toReturn.ToArray();
    }

    private static string? GenerateDocumentationUrl(CommandInfo com)
    {
        const string baseUrl = "https://docs.mewdeko.tech/api/";

        // Get the module's type
        Type moduleType = null;
        if (com.Module is ModuleInfo moduleInfo)
        {
            var assembly = typeof(Mewdeko).Assembly;
            var possibleTypes = assembly.GetTypes()
                .Where(t =>
                    t.IsSubclassOf(typeof(MewdekoSubmodule)) || t.IsSubclassOf(typeof(MewdekoModule)))
                .Where(t => t.Name.Equals(moduleInfo.Name, StringComparison.OrdinalIgnoreCase))
                .ToList();

            moduleType = possibleTypes.Count switch
            {
                1 => possibleTypes[0],
                > 1 => possibleTypes.FirstOrDefault(t => !t.IsNested) ?? possibleTypes[0],
                _ => null
            };

            if (moduleType == null)
            {
                // If we still can't find the type, we can't generate the URL
                return null;
            }
        }
        else
        {
            // Fallback to the type of Module if it's not ModuleInfo
            moduleType = com.Module.GetType();
        }

        // Get the method name
        var methodName = com.Name;

        // Get the parameter types
        var parameterTypes = com.Parameters.Select(p => p.Type).ToArray();

        // Find the MethodInfo
        var methodInfo = moduleType.GetMethod(
            methodName,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic,
            null,
            parameterTypes,
            null);

        if (methodInfo == null)
        {
            // Handle method overloads
            var methods = moduleType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic)
                .Where(m => m.Name.Equals(com.MethodName(), StringComparison.OrdinalIgnoreCase))
                .ToArray();

            foreach (var method in methods)
            {
                var methodParams = method.GetParameters();
                if (methodParams.Length != parameterTypes.Length) continue;
                var parametersMatch =
                    !parameterTypes.Select((t, i) => new
                        {
                            Type = t, Index = i
                        })
                        .Any(x => !x.Type.IsAssignableFrom(methodParams[x.Index].ParameterType));
                if (!parametersMatch) continue;
                methodInfo = method;
                break;
            }
        }

        if (methodInfo == null)
        {
            // Can't find method info
            return null;
        }

        // Adjust the class full name for the URL
        var classFullNameForUrl = moduleType.FullName.Replace('+', '.');

        // Construct the class URL
        var classUrl = baseUrl + classFullNameForUrl + ".html";

        // Generate the anchor
        var typeAnchor = moduleType.FullName.Replace('+', '_').Replace('.', '_');
        var methodAnchorName = methodInfo.Name;
        var anchor = $"{typeAnchor}_{methodAnchorName}";

        // Get parameter types for anchor
        var methodParameterTypes = methodInfo.GetParameters().Select(p => p.ParameterType);

        if (methodParameterTypes.Any())
        {
            // Append parameter types to the anchor
            var parameterAnchor = string.Join("_", methodParameterTypes.Select(FormatParameterType));
            anchor += $"_{parameterAnchor}_"; // Note the extra underscore at the end
        }

        // Construct the full URL
        var actualUrl = $"{classUrl}#{anchor}";
        return actualUrl;
    }

    private static string FormatParameterType(Type type)
    {
        // Handle arrays
        if (type.IsArray)
        {
            var elementType = type.GetElementType();
            var formattedElementType = FormatParameterType(elementType);
            // Use triple underscores for arrays
            var underscores = new string('_', type.GetArrayRank() * 3);
            return $"{formattedElementType}{underscores}";
        }

        // Handle generic types
        if (type.IsGenericType)
        {
            var genericTypeDef = type.GetGenericTypeDefinition();
            var genericTypeName = genericTypeDef.FullName.Split('`')[0].Replace('+', '.').Replace('.', '_');
            var genericArgs = string.Join("_", type.GetGenericArguments().Select(FormatParameterType));
            return $"{genericTypeName}_{genericArgs}";
        }

        // Handle nested types and replace '+' with '.'
        var fullName = type.FullName.Replace('+', '.').Replace('.', '_');
        return fullName;
    }


    private static string GetPreconditionString(ChannelPermission perm)
    {
        return (perm + " Channel Permission").Replace("Guild", "Server", StringComparison.InvariantCulture);
    }

    private static string GetPreconditionString(GuildPermission perm)
    {
        return (perm + " Server Permission").Replace("Guild", "Server", StringComparison.InvariantCulture);
    }

    private string? GetText(string? text, IGuild? guild, params object?[] replacements)
    {
        return strings.GetText(text, guild?.Id, replacements);
    }
}