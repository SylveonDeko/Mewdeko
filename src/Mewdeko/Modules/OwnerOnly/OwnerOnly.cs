using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using DataModel;
using Discord.Commands;
using Discord.Net;
using Discord.Rest;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using LibGit2Sharp;
using LinqToDB.Async;
using LinqToDB.Data;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Common.Configs;
using Mewdeko.Common.DiscordImplementations;
using Mewdeko.Modules.OwnerOnly.Services;
using Mewdeko.Services.Impl;
using Mewdeko.Services.Settings;
using Mewdeko.Services.strings;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.Extensions.DependencyInjection;
using Swan.Formatters;

namespace Mewdeko.Modules.OwnerOnly;

/// <summary>
///     Initializes a new instance of the <see cref="OwnerOnly" /> class, intended for owner-only operations within the
///     Mewdeko bot framework.
/// </summary>
/// <param name="client">The Discord client used to interact with the Discord API.</param>
/// <param name="bot">The main instance of the Mewdeko bot.</param>
/// <param name="strings">Provides access to localized strings within the bot.</param>
/// <param name="serv">Interactive service for handling interactive user commands.</param>
/// <param name="settingServices">A collection of configuration services for managing bot settings.</param>
/// <param name="dbFactory">Service for database operations and access.</param>
/// <param name="cache">Cache service for storing and retrieving temporary data.</param>
/// <param name="commandService">Service for handling and executing Discord commands.</param>
/// <param name="services">The service provider for dependency injection.</param>
/// <param name="guildSettings">Service for accessing and modifying guild-specific settings.</param>
/// <param name="commandHandler">Handler for processing and executing commands received from users.</param>
/// <param name="botConfig">Configuration settings for the bot.</param>
/// <param name="httpClient">HTTP client for making web requests.</param>
/// <param name="localization">Service for handling localization and translations.</param>
/// <param name="logger">The logger instance for structured logging.</param>
/// <param name="creds">Bot credentials and configuration secrets.</param>
[OwnerOnly]
public class OwnerOnly(
    DiscordShardedClient client,
    Mewdeko bot,
    IBotStrings strings,
    InteractiveService serv,
    IEnumerable<IConfigService> settingServices,
    IDataConnectionFactory dbFactory,
    IDataCache cache,
    CommandService commandService,
    IServiceProvider services,
    GuildSettingsService guildSettings,
    CommandHandler commandHandler,
    BotConfig botConfig,
    HttpClient httpClient,
    Localization localization,
    ILogger<OwnerOnly> logger,
    BotCredentials creds)
    : MewdekoModuleBase<OwnerOnlyService>
{
    /// <summary>
    ///     Defines the set of user statuses that can be programmatically assigned.
    /// </summary>
    public enum SettableUserStatus
    {
        /// <summary>
        ///     Indicates the user is online and available.
        /// </summary>
        Online,

        /// <summary>
        ///     Indicates the user is online but appears as offline or invisible to others.
        /// </summary>
        Invisible,

        /// <summary>
        ///     Indicates the user is idle and may be away from their device.
        /// </summary>
        Idle,

        /// <summary>
        ///     Indicates the user does not wish to be disturbed (Do Not Disturb).
        /// </summary>
        Dnd
    }


    /// <summary>
    ///     Updates the bot to the latest version available on the repository.
    /// </summary>
    [Cmd]
    [Aliases]
    public async Task Update()
    {
        var buttons = new ComponentBuilder()
            .WithButton("Stable", "main")
            .WithButton("Nightly", "psqldeko", ButtonStyle.Danger);

        var embed = new EmbedBuilder()
            .WithColor(Color.Orange)
            .WithDescription(Strings.UpdateVersionSelect(ctx.Guild.Id))
            .Build();

        var msg = await ReplyAsync(embed: embed, components: buttons.Build());

        // Wait for button interaction
        var branch = await GetButtonInputAsync(ctx.Channel.Id, msg.Id, ctx.User.Id);

        // Provide initial feedback
        var updatingEmbed = new EmbedBuilder()
            .WithColor(Color.Blue)
            .WithDescription(Strings.UpdateInProgress(ctx.Guild.Id, branch))
            .Build();
        await msg.ModifyAsync(x => x.Embed = updatingEmbed);

        try
        {
            var repoPath = Directory.GetCurrentDirectory();
            var discovered = Repository.Discover(repoPath);

            if (string.IsNullOrWhiteSpace(discovered))
            {
                throw new Exception(Strings.InvalidRepoPath(ctx.Guild.Id));
            }


            using var repo = new Repository(discovered);

            // Fetch updates
            var remote = repo.Network.Remotes["origin"];
            var refSpecs = remote.FetchRefSpecs.Select(x => x.Specification);
            Commands.Fetch(repo, remote.Name, refSpecs, null, "");

            // Check current branch
            var currentBranch = repo.Head.FriendlyName;
            if (currentBranch != branch)
            {
                // Switch branches
                Commands.Checkout(repo, branch);
            }

            // Pull changes
            var options = new PullOptions
            {
                FetchOptions = new FetchOptions(),
                MergeOptions = new MergeOptions
                {
                    FailOnConflict = true
                }
            };

            var signature = new Signature(new Identity("Mewdeko", "mewdeko@mewdeko.tech"), DateTimeOffset.Now);
            var result = Commands.Pull(repo, signature, options);

            // Provide success feedback
            var successEmbed = new EmbedBuilder()
                .WithColor(Color.Green)
                .WithDescription(Strings.UpdateComplete(ctx.Guild.Id))
                .AddField("Branch", branch, true)
                .AddField("Status", result.Status, true)
                .Build();
            await msg.ModifyAsync(x =>
            {
                x.Embed = successEmbed;
                x.Components = null;
            });
        }
        catch (Exception ex)
        {
            // Handle exceptions and provide error feedback
            var errorEmbed = new EmbedBuilder()
                .WithColor(Color.Red)
                .WithTitle(Strings.UpdateFailed(ctx.Guild.Id))
                .WithDescription(Strings.UpdateError(ctx.Guild.Id, ex.Message))
                .Build();
            await msg.ModifyAsync(x =>
            {
                x.Embed = errorEmbed;
                x.Components = null;
            });
        }
    }

    /// <summary>
    ///     Executes a command as if it were sent by the specified guild user.
    /// </summary>
    /// <param name="user">The guild user to impersonate when executing the command.</param>
    /// <param name="args">The command string to execute, including command name and arguments.</param>
    /// <remarks>
    ///     This method constructs a fake message with the specified user as the author and the given command string,
    ///     then enqueues it for command parsing and execution.
    /// </remarks>
    [Cmd]
    [Aliases]
    public async Task Sudo(IGuildUser user, [Remainder] string args)
    {
        var content = $"{await guildSettings.GetPrefix(ctx.Guild)}{args}";
        var msg = MewdekoUserMessage.CreateWithMentions(content, user, ctx.Channel);
        commandHandler.AddCommandToParseQueue(msg);
        _ = commandHandler.ExecuteCommandsInChannelAsync(ctx.Channel.Id);
    }

    /// <summary>
    ///     Executes a command as if it were sent by the owner of the guild.
    /// </summary>
    /// <param name="args">The command string to execute, including command name and arguments.</param>
    /// <remarks>
    ///     This method constructs a fake message with the guild owner as the author and the given command string,
    ///     then enqueues it for command parsing and execution. Useful for performing actions that require owner permissions.
    /// </remarks>
    [Cmd]
    [Aliases]
    public async Task Sudo([Remainder] string args)
    {
        var content = $"{await guildSettings.GetPrefix(ctx.Guild)}{args}";
        var msg = MewdekoUserMessage.CreateWithMentions(content, await Context.Guild.GetOwnerAsync(), ctx.Channel);
        commandHandler.AddCommandToParseQueue(msg);
        _ = commandHandler.ExecuteCommandsInChannelAsync(ctx.Channel.Id);
    }

    /// <summary>
    ///     Generates command documentation YML files
    /// </summary>
    [Cmd]
    [Aliases]
    [OwnerOnly]
    public async Task GenerateDocs()
    {
        await ctx.Channel.SendMessageAsync(Strings.GeneratingDocs(ctx.Guild.Id));

        try
        {
            await Service.GenerateDocumentationAsync("/home/sylv/RiderProjects/mewdeko/src/Mewdeko/Modules");
            await ctx.Channel.SendMessageAsync(Strings.DocsGenerated(ctx.Guild.Id));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating command documentation");
            await ctx.Channel.SendMessageAsync(Strings.DocsError(ctx.Guild.Id));
        }
    }

    /// <summary>
    ///     Executes a Redis command and returns the result.
    /// </summary>
    /// <param name="command">The Redis command to execute.</param>
    /// <remarks>
    ///     This method sends the specified command to Redis through the configured cache connection.
    ///     The result of the command execution is then sent back as a message in the Discord channel.
    /// </remarks>
    [Cmd]
    [Aliases]
    public async Task RedisExec([Remainder] string command)
    {
        var result = await cache.ExecuteRedisCommand(command).ConfigureAwait(false);
        var eb = new EmbedBuilder().WithOkColor().WithTitle(Strings.RedisResponse(ctx.Guild.Id))
            .WithDescription(result.ToString());
        await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
    }

    /// <summary>
    ///     Executes a raw SQL command against the database.
    /// </summary>
    /// <param name="sql">The SQL command to execute.</param>
    /// <remarks>
    ///     Prompts the user for confirmation before executing the SQL command.
    ///     The number of affected rows is sent back as a message in the Discord channel.
    ///     Use with caution, as executing raw SQL can directly affect the database integrity.
    /// </remarks>
    [Cmd]
    [Aliases]
    public async Task SqlExec([Remainder] string sql)
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();

        if (!await PromptUserConfirmAsync(Strings.SqlExecConfirm(ctx.Guild.Id), ctx.User.Id).ConfigureAwait(false))
            return;

        var affected = await dbContext.ExecuteAsync(sql).ConfigureAwait(false);
        await ctx.Channel.SendErrorAsync(Strings.SqlAffectedRows(ctx.Guild.Id, affected), botConfig)
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Updates the list of top guilds based on member count, excluding certain guilds by name or ID,
    ///     and stores the updated information in the cache for further use.
    /// </summary>
    /// <returns>
    ///     An asynchronous task that completes upon successful update of the top guilds list in the cache.
    /// </returns>
    [Cmd]
    [Aliases]
    public async Task TopServersUpdate()
    {
        if (!creds.IsMasterInstance)
        {
            await ErrorAsync(Strings.NotMasterInstance(ctx.Guild.Id));
            return;
        }

        try
        {
            var guilds = (await ctx.Client.GetGuildsAsync().ConfigureAwait(false))
                .Cast<SocketGuild>();

            var excludedTerms = new[]
            {
                "botlist", "bots", "xhamster", "nsfw", "18+"
            };
            const ulong excludedId = 374071874222686211;

            var servers = guilds
                .Where(x => x.Id != excludedId &&
                            !excludedTerms.Any(term => x.Name.Contains(term, StringComparison.OrdinalIgnoreCase)))
                .OrderByDescending(x => x.MemberCount)
                .Take(11)
                .Select(x => new StatsService.MewdekoPartialGuild
                {
                    IconUrl = x.IconId.StartsWith("a_") ? x.IconUrl.Replace(".jpg", ".gif") : x.IconUrl,
                    MemberCount = x.MemberCount,
                    Name = x.Name
                })
                .ToList();

            var serialied = Json.Serialize(servers);
            await cache.Redis.GetDatabase().StringSetAsync($"{client.CurrentUser.Id}_topguilds", serialied)
                .ConfigureAwait(false);
            await SuccessAsync(Strings.TopGuildsUpdated(ctx.Guild.Id)).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            await ErrorAsync(Strings.ErrorUpdatingTopGuilds(ctx.Guild.Id, e.Message)).ConfigureAwait(false);
            logger.LogError(e, "Error updating top guilds");
        }
    }

    /// <summary>
    ///     Lists all servers the bot is currently in.
    /// </summary>
    /// <remarks>
    ///     This method creates a paginated list of servers, showing server names, IDs, member counts, online member counts,
    ///     server owners, and creation dates. Pagination allows browsing through the server list if it exceeds the page limit.
    /// </remarks>
    [Cmd]
    [Aliases]
    public async Task ListServers()
    {
        var guilds = client.Guilds;
        var paginator = new LazyPaginatorBuilder()
            .AddUser(ctx.User)
            .WithPageFactory(PageFactory)
            .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
            .WithMaxPageIndex(guilds.Count / 10)
            .WithDefaultEmotes()
            .WithActionOnCancellation(ActionOnStop.DeleteMessage)
            .Build();

        await serv.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60)).ConfigureAwait(false);

        async Task<PageBuilder> PageFactory(int page)
        {
            await Task.CompletedTask;
            var newGuilds = guilds.Skip(10 * page).Take(10);
            var eb = new PageBuilder()
                .WithOkColor()
                .WithTitle(Strings.ServersList(ctx.Guild.Id));

            foreach (var i in newGuilds)
            {
                eb.AddField(
                    Strings.ServerInfoName(ctx.Guild.Id, i.Name, i.Id),
                    Strings.ServerInfoMembers(ctx.Guild.Id, i.Users.Count) + "\n" +
                    Strings.ServerInfoOnline(ctx.Guild.Id,
                        i.Users.Count(x =>
                            x.Status is UserStatus.Online or UserStatus.DoNotDisturb or UserStatus.Idle)) + "\n" +
                    Strings.ServerInfoOwner(ctx.Guild.Id, i.Owner, i.OwnerId) + "\n" +
                    Strings.ServerInfoCreated(ctx.Guild.Id, TimestampTag.FromDateTimeOffset(i.CreatedAt))
                );
            }

            return eb;
        }
    }

    /// <summary>
    ///     Retrieves and displays statistics on the most used command, module, guild, and user.
    /// </summary>
    /// <remarks>
    ///     This method calculates and reports the top entities based on their usage count.
    ///     It displays the most frequently used command, the module that's used the most,
    ///     the user who has used commands the most, and the guild with the highest command usage.
    ///     These statistics are presented as an embed in the Discord channel.
    /// </remarks>
    [Cmd]
    [Aliases]
    public async Task CommandStats()
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();

        var topCommand = await dbContext.CommandStats
            .Where(x => !x.Trigger)
            .GroupBy(q => q.NameOrId)
            .Select(g => new
            {
                Name = g.Key, Count = g.Count()
            })
            .OrderByDescending(gc => gc.Count)
            .FirstOrDefaultAsync();

        var topModule = await dbContext.CommandStats
            .Where(x => !x.Trigger)
            .GroupBy(q => q.Module)
            .Select(g => new
            {
                Name = g.Key, Count = g.Count()
            })
            .OrderByDescending(gc => gc.Count)
            .FirstOrDefaultAsync();

        var topGuildStat = await dbContext.CommandStats
            .Where(x => !x.Trigger)
            .GroupBy(q => q.GuildId)
            .Select(g => new
            {
                GuildId = g.Key, Count = g.Count()
            })
            .OrderByDescending(gc => gc.Count)
            .FirstOrDefaultAsync();

        var topUserStat = await dbContext.CommandStats
            .Where(x => !x.Trigger)
            .GroupBy(q => q.UserId)
            .Select(g => new
            {
                UserId = g.Key, Count = g.Count()
            })
            .OrderByDescending(gc => gc.Count)
            .FirstOrDefaultAsync();

        IGuild? guild = null;
        IUser? user = null;

        // Parallel fetch of guild and user details
        await Task.WhenAll(
            Task.Run(async () =>
            {
                if (topGuildStat?.GuildId != null)
                    guild = await client.Rest.GetGuildAsync(topGuildStat.GuildId);
            }),
            Task.Run(async () =>
            {
                if (topUserStat?.UserId != null)
                    user = await client.Rest.GetUserAsync(topUserStat.UserId);
            })
        );

        var eb = new EmbedBuilder()
            .WithOkColor()
            .WithTitle(Strings.CommandStatsTitle(ctx.Guild.Id))
            .AddField(Strings.CommandStats(ctx.Guild.Id),
                Strings.StatsTopCommand(ctx.Guild.Id, topCommand?.Name ?? "N/A", topCommand?.Count ?? 0))
            .AddField(Strings.ModuleStats(ctx.Guild.Id),
                Strings.StatsTopModule(ctx.Guild.Id, topModule?.Name ?? "N/A", topModule?.Count ?? 0))
            .AddField(Strings.UserStats(ctx.Guild.Id),
                Strings.StatsTopUser(ctx.Guild.Id, user, topUserStat?.Count ?? 0))
            .AddField(Strings.GuildStats(ctx.Guild.Id),
                Strings.StatsTopGuild(ctx.Guild.Id, guild?.Name ?? Strings.UnknownGuild(ctx.Guild.Id),
                    topGuildStat?.Count ?? 0));

        await ctx.Channel.SendMessageAsync(embed: eb.Build());
    }


    /// <summary>
    ///     Changes yml based config for the bot.
    /// </summary>
    /// <param name="name">The name of the config to change.</param>
    /// <param name="prop">The property of the config to change.</param>
    /// <param name="value">The new value to set for the property.</param>
    [Cmd]
    [Aliases]
    public new async Task Config(string? name = null, string? prop = null, [Remainder] string? value = null)
    {
        try
        {
            var configNames = settingServices.Select(x => x.Name);

            // if name is not provided, print available configs
            name = name?.ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(name))
            {
                var embed = new EmbedBuilder()
                    .WithOkColor()
                    .WithTitle(Strings.ConfigList(ctx.Guild.Id))
                    .WithDescription(string.Join("\n", configNames));

                await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
                return;
            }

            var setting = settingServices.FirstOrDefault(x =>
                x.Name.StartsWith(name, StringComparison.InvariantCultureIgnoreCase));

            // if config name is not found, print error and the list of configs
            if (setting is null)
            {
                var embed = new EmbedBuilder()
                    .WithErrorColor()
                    .WithDescription(Strings.ConfigNotFound(ctx.Guild.Id, Format.Code(name)))
                    .AddField(Strings.ConfigList(ctx.Guild.Id), string.Join("\n", configNames));

                await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
                return;
            }

            name = setting.Name;

            // if prop is not sent, then print the list of all props and values in that config
            prop = prop?.ToLowerInvariant();
            var propNames = setting.GetSettableProps();
            if (string.IsNullOrWhiteSpace(prop))
            {
                var propStrings = GetPropsAndValuesString(setting, propNames);
                var embed = new EmbedBuilder()
                    .WithOkColor()
                    .WithTitle(Strings.OwnerSettingTitle(ctx.Guild.Id, setting.Name))
                    .WithDescription(propStrings);

                await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
                return;
            }
            // if the prop is invalid -> print error and list of

            var exists = propNames.Any(x => x == prop);

            if (!exists)
            {
                var propStrings = GetPropsAndValuesString(setting, propNames);
                var propErrorEmbed = new EmbedBuilder()
                    .WithErrorColor()
                    .WithDescription(Strings.ConfigPropNotFound(ctx.Guild.Id, Format.Code(prop), Format.Code(name)))
                    .AddField(Strings.OwnerSettingTitle(ctx.Guild.Id, setting.Name), propStrings);

                await ctx.Channel.EmbedAsync(propErrorEmbed).ConfigureAwait(false);
                return;
            }

            // if prop is sent, but value is not, then we have to check
            // if prop is valid ->
            if (string.IsNullOrWhiteSpace(value))
            {
                value = setting.GetSetting(prop);
                if (prop != "currency.sign")
                    Format.Code(Format.Sanitize(value.TrimTo(1000)), "json");

                if (string.IsNullOrWhiteSpace(value))
                    value = "-";

                var embed = new EmbedBuilder()
                    .WithOkColor()
                    .AddField("Config", Format.Code(setting.Name), true)
                    .AddField("Prop", Format.Code(prop), true)
                    .AddField("Value", value);

                var comment = setting.GetComment(prop);
                if (!string.IsNullOrWhiteSpace(comment))
                    embed.AddField("Comment", comment);

                await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
                return;
            }

            var success = setting.SetSetting(prop, value);

            if (!success)
            {
                await ReplyErrorAsync(Strings.ConfigEditFail(ctx.Guild.Id, Format.Code(prop), Format.Code(value)))
                    .ConfigureAwait(false);
                return;
            }

            await ctx.OkAsync().ConfigureAwait(false);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            await ctx.Channel.SendErrorAsync(
                Strings.OwnerConfigError(ctx.Guild.Id), botConfig);
        }
    }

    private static string GetPropsAndValuesString(IConfigService config, IEnumerable<string> names)
    {
        var propValues = names.Select(pr =>
        {
            var val = config.GetSetting(pr);
            if (pr != "currency.sign")
                val = val.TrimTo(40);
            return val?.Replace("\n", "") ?? "-";
        });

        var strings = names.Zip(propValues, (name, value) =>
            $"{name,-25} = {value}\n");

        return string.Concat(strings);
    }

    /// <summary>
    ///     Toggles the rotation of playing statuses for the bot.
    /// </summary>
    /// <remarks>
    ///     If rotation is enabled, it will be disabled, and vice versa. Confirmation of the action is sent as a reply.
    /// </remarks>
    [Cmd]
    [Aliases]
    public async Task RotatePlaying()
    {
        if (Service.ToggleRotatePlaying())
            await ReplyConfirmAsync(Strings.RoplEnabled(ctx.Guild.Id)).ConfigureAwait(false);
        else
            await ReplyConfirmAsync(Strings.RoplDisabled(ctx.Guild.Id)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Adds a new status to the rotation of playing statuses for the bot.
    /// </summary>
    /// <param name="t">The type of activity (e.g., Playing, Streaming).</param>
    /// <param name="status">The text of the status to add.</param>
    /// <remarks>
    ///     Adds a new status with the specified activity type and text. Confirmation of addition is sent as a reply.
    /// </remarks>
    [Cmd]
    [Aliases]
    public async Task AddPlaying(ActivityType t, [Remainder] string status)
    {
        await Service.AddPlaying(t, status).ConfigureAwait(false);

        await ReplyConfirmAsync(Strings.RoplAdded(ctx.Guild.Id)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Lists all statuses currently in the rotation.
    /// </summary>
    /// <remarks>
    ///     Sends a reply with a numbered list of all statuses in the rotation. If no statuses are set, sends an error message.
    /// </remarks>
    [Cmd]
    [Aliases]
    public async Task ListPlaying()
    {
        var statuses = await Service.GetRotatingStatuses();

        if (statuses.Count == 0)
        {
            await ReplyErrorAsync(Strings.RoplNotSet(ctx.Guild.Id)).ConfigureAwait(false);
        }
        else
        {
            var i = 1;
            await ReplyConfirmAsync(Strings.RoplList(ctx.Guild.Id,
                    string.Join("\n\t", statuses.Select(rs => $"`{i++}.` *{rs.Type}* {rs.Status}"))))
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Sets or displays the default command prefix.
    /// </summary>
    /// <param name="prefix">The new prefix to set. If null or whitespace, the current prefix is displayed instead.</param>
    /// <remarks>
    ///     Changes the bot's command prefix for the server or displays the current prefix if no new prefix is provided.
    ///     Confirmation of the new prefix or the current prefix is sent as a reply.
    /// </remarks>
    [Cmd]
    [Aliases]
    public async Task DefPrefix([Remainder] string? prefix = null)
    {
        if (string.IsNullOrWhiteSpace(prefix))
        {
            await ReplyConfirmAsync(Strings.DefprefixCurrent(ctx.Guild.Id, await guildSettings.GetPrefix(null)))
                .ConfigureAwait(false);
            return;
        }

        var oldPrefix = await guildSettings.GetPrefix(null);
        var newPrefix = Service.SetDefaultPrefix(prefix);

        await ReplyConfirmAsync(Strings.DefprefixNew(ctx.Guild.Id, Format.Code(oldPrefix), Format.Code(newPrefix)))
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Removes a status from the rotating playing statuses by its index.
    /// </summary>
    /// <param name="index">The one-based index of the status to remove. The actual removal will use zero-based indexing.</param>
    /// <remarks>
    ///     If the status at the provided index exists, it will be removed, and a confirmation message is sent.
    /// </remarks>
    [Cmd]
    [Aliases]
    public async Task RemovePlaying(int index)
    {
        index--;

        var msg = await Service.RemovePlayingAsync(index).ConfigureAwait(false);

        if (msg == null)
            return;

        await ReplyConfirmAsync(Strings.Reprm(ctx.Guild.Id, msg)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets the default language for the bot by specifying a culture name.
    /// </summary>
    /// <param name="name">
    ///     The name of the culture to set as the default language. Use "default" to reset to the bot's original
    ///     default language.
    /// </param>
    /// <remarks>
    ///     This method allows changing the bot's default language or resetting it to its original default.
    ///     A confirmation message will be sent upon successful change.
    /// </remarks>
    [Cmd]
    [Aliases]
    public async Task LanguageSetDefault(string name)
    {
        try
        {
            CultureInfo? ci;
            if (string.Equals(name.Trim(), "default", StringComparison.InvariantCultureIgnoreCase))
            {
                localization.ResetDefaultCulture();
                ci = localization.DefaultCultureInfo;
            }
            else
            {
                ci = new CultureInfo(name);
                localization.SetDefaultCulture(ci);
            }

            await ReplyConfirmAsync(Strings.LangSetBot(ctx.Guild.Id, Format.Bold(ci.ToString()),
                Format.Bold(ci.NativeName))).ConfigureAwait(false);
        }
        catch (Exception)
        {
            await ReplyErrorAsync(Strings.LangSetFail(ctx.Guild.Id)).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Adds a new startup command to be executed when the bot starts.
    /// </summary>
    /// <param name="cmdText">The text of the command to add, excluding the prefix.</param>
    /// <remarks>
    ///     Requires the user to have Administrator permissions or be the owner of the bot.
    ///     Commands that could potentially restart or shut down the bot are ignored for safety reasons.
    /// </remarks>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.Administrator)]
    [OwnerOnly]
    public async Task StartupCommandAdd([Remainder] string cmdText)
    {
        if (cmdText.StartsWith($"{await guildSettings.GetPrefix(ctx.Guild)}die", StringComparison.InvariantCulture) ||
            cmdText.StartsWith($"{await guildSettings.GetPrefix(ctx.Guild)}restart", StringComparison.InvariantCulture))
            return;

        var guser = (IGuildUser)ctx.User;
        var cmd = new AutoCommand
        {
            CommandText = cmdText,
            ChannelId = ctx.Channel.Id,
            ChannelName = ctx.Channel.Name,
            GuildId = ctx.Guild?.Id,
            GuildName = ctx.Guild?.Name,
            VoiceChannelId = guser.VoiceChannel?.Id,
            VoiceChannelName = guser.VoiceChannel?.Name,
            Interval = 0
        };
        _ = Service.AddNewAutoCommand(cmd);

        await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
            .WithTitle(Strings.Scadd(ctx.Guild.Id))
            .AddField(efb => efb.WithName(Strings.Server(ctx.Guild.Id))
                .WithValue(cmd.GuildId == null ? "-" : $"{cmd.GuildName}/{cmd.GuildId}").WithIsInline(true))
            .AddField(efb => efb.WithName(Strings.Channel(ctx.Guild.Id))
                .WithValue($"{cmd.ChannelName}/{cmd.ChannelId}").WithIsInline(true))
            .AddField(efb => efb.WithName(Strings.CommandText(ctx.Guild.Id))
                .WithValue(cmdText).WithIsInline(false))).ConfigureAwait(false);
    }

    /// <summary>
    ///     Adds an auto command to be executed periodically in the specified guild.
    /// </summary>
    /// <param name="interval">The interval in seconds at which the command should be executed. Must be 5 seconds or more.</param>
    /// <param name="cmdText">The command text to be executed automatically.</param>
    /// <remarks>
    ///     Requires the user to have Administrator permissions or to be the owner of the bot.
    ///     The command will not be added if it fails any precondition checks,
    ///     if it matches a forbidden command (e.g., a command to shut down the bot),
    ///     or if the maximum number of auto commands (15) for the guild has been reached.
    /// </remarks>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.Administrator)]
    [OwnerOnly]
    public async Task AutoCommandAdd(int interval, [Remainder] string cmdText)
    {
        if (cmdText.StartsWith($"{await guildSettings.GetPrefix(ctx.Guild)}die", StringComparison.InvariantCulture))
            return;
        var command =
            commandService.Search(cmdText.Replace(await guildSettings.GetPrefix(ctx.Guild), "").Split(" ")[0]);
        if (!command.IsSuccess)
            return;
        foreach (var i in command.Commands)
        {
            if (!(await i.CheckPreconditionsAsync(ctx, services).ConfigureAwait(false)).IsSuccess)
                return;
        }

        var count = (await Service.GetAutoCommands()).Where(x => x.GuildId == ctx.Guild.Id);

        if (count.Count() == 15)
            return;
        if (interval < 5)
            return;

        var guser = (IGuildUser)ctx.User;
        var cmd = new AutoCommand
        {
            CommandText = cmdText,
            ChannelId = ctx.Channel.Id,
            ChannelName = ctx.Channel.Name,
            GuildId = ctx.Guild?.Id,
            GuildName = ctx.Guild?.Name,
            VoiceChannelId = guser.VoiceChannel?.Id,
            VoiceChannelName = guser.VoiceChannel?.Name,
            Interval = interval
        };
        _ = Service.AddNewAutoCommand(cmd);

        await ReplyConfirmAsync(Strings.AutocmdAdd(ctx.Guild.Id, Format.Code(Format.Sanitize(cmdText)), cmd.Interval))
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Lists the startup commands configured for the guild.
    /// </summary>
    /// <param name="page">The page number of the list to display, starting from 1.</param>
    /// <remarks>
    ///     Displays a paginated list of startup commands. Each page shows up to 5 commands.
    ///     Requires the user to be the owner of the bot.
    /// </remarks>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [OwnerOnly]
    public async Task StartupCommandsList(int page = 1)
    {
        if (page-- < 1)
            return;

        var scmds = (await Service.GetStartupCommands())
            .Skip(page * 5)
            .Take(5)
            .ToList();

        if (scmds.Count == 0)
        {
            await ReplyErrorAsync(Strings.StartcmdlistNone(ctx.Guild.Id)).ConfigureAwait(false);
        }
        else
        {
            var i = 0;
            await ctx.Channel.SendConfirmAsync(
                    text: string.Join("\n", scmds
                        .Select(x => $"""
                                      ```css
                                      #{++i}
                                      [{Strings.Server(ctx.Guild.Id)}]: {(x.GuildId.HasValue ? $"{x.GuildName} #{x.GuildId}" : "-")}
                                      [{Strings.Channel(ctx.Guild.Id)}]: {x.ChannelName} #{x.ChannelId}
                                      [{Strings.CommandText(ctx.Guild.Id)}]: {x.CommandText}```
                                      """)),
                    title: string.Empty,
                    footer: Strings.Page(ctx.Guild.Id, page + 1))
                .ConfigureAwait(false);
        }
    }


    /// <summary>
    ///     Lists the auto commands configured for the guild.
    /// </summary>
    /// <param name="page">The page number of the list to display, starting from 1.</param>
    /// <remarks>
    ///     Displays a paginated list of auto commands. Each page shows up to 5 commands.
    ///     Requires the user to be the owner of the bot and the command to be executed in a guild context.
    ///     If there are no auto commands set, an error message is displayed.
    /// </remarks>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [OwnerOnly]
    public async Task AutoCommandsList(int page = 1)
    {
        if (page-- < 1)
            return;

        var scmds = (await Service.GetAutoCommands())
            .Skip(page * 5)
            .Take(5)
            .ToList();
        if (scmds.Count == 0)
        {
            await ReplyErrorAsync(Strings.AutocmdlistNone(ctx.Guild.Id)).ConfigureAwait(false);
        }
        else
        {
            var i = 0;
            await ctx.Channel.SendConfirmAsync(
                    text: string.Join("\n", scmds
                        .Select(x => $"""
                                      ```css
                                      #{++i}
                                      [{Strings.Server(ctx.Guild.Id)}]: {(x.GuildId.HasValue ? $"{x.GuildName} #{x.GuildId}" : "-")}
                                      [{Strings.Channel(ctx.Guild.Id)}]: {x.ChannelName} #{x.ChannelId}
                                      {GetIntervalText(x.Interval)}
                                      [{Strings.CommandText(ctx.Guild.Id)}]: {x.CommandText}```
                                      """)),
                    title: string.Empty,
                    footer: Strings.Page(ctx.Guild.Id, page + 1))
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Provides a formatted text displaying the interval of an auto command.
    /// </summary>
    /// <param name="interval">The interval at which the auto command executes.</param>
    /// <returns>A string representing the interval in a readable format.</returns>
    private string GetIntervalText(int interval)
    {
        return $"[{Strings.Interval(ctx.Guild.Id)}]: {interval}";
    }

    /// <summary>
    ///     Executes a wait command that delays for a specified number of milliseconds.
    /// </summary>
    /// <param name="miliseconds">The number of milliseconds to delay.</param>
    /// <remarks>
    ///     The command message is immediately deleted, and a new message showing the delay is sent.
    ///     This message is then deleted after the delay period has passed.
    ///     If the provided milliseconds value is less than or equal to 0, the command does nothing.
    /// </remarks>
    [Cmd]
    [Aliases]
    public async Task Wait(int miliseconds)
    {
        if (miliseconds <= 0)
            return;
        ctx.Message.DeleteAfter(0);
        try
        {
            var msg = await ctx.Channel.SendConfirmAsync(Strings.TimingMs(ctx.Guild.Id, miliseconds))
                .ConfigureAwait(false);
            msg.DeleteAfter(miliseconds / 1000);
        }
        catch
        {
            // ignored
        }

        await Task.Delay(miliseconds).ConfigureAwait(false);
    }


    /// <summary>
    ///     Removes an auto command based on its index.
    /// </summary>
    /// <param name="index">The one-based index of the auto command to remove.</param>
    /// <remarks>
    ///     Requires the user to have Administrator permissions or to be the owner of the bot.
    ///     The command will decrement the index to match zero-based indexing before attempting removal.
    ///     If the removal fails, an error message is sent.
    /// </remarks>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.Administrator)]
    [OwnerOnly]
    public async Task AutoCommandRemove([Remainder] int index)
    {
        if (!await Service.RemoveAutoCommand(--index))
        {
            await ReplyErrorAsync(Strings.AcrmFail(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await ctx.OkAsync().ConfigureAwait(false);
    }

    /// <summary>
    ///     Removes a startup command based on its index.
    /// </summary>
    /// <param name="index">The one-based index of the startup command to remove.</param>
    /// <remarks>
    ///     Requires the user to be the owner of the bot.
    ///     The command will decrement the index to match zero-based indexing before attempting removal.
    ///     If the removal fails, an error message is sent; otherwise, a confirmation message is sent.
    /// </remarks>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [OwnerOnly]
    public async Task StartupCommandRemove([Remainder] int index)
    {
        if (!await Service.RemoveStartupCommand(--index))
            await ReplyErrorAsync(Strings.ScrmFail(ctx.Guild.Id)).ConfigureAwait(false);
        else
            await ReplyConfirmAsync(Strings.Scrm(ctx.Guild.Id)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Clears all startup commands for the guild.
    /// </summary>
    /// <remarks>
    ///     Requires the user to have Administrator permissions or to be the owner of the bot.
    ///     A confirmation message is sent upon successful clearance.
    /// </remarks>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.Administrator)]
    [OwnerOnly]
    public async Task StartupCommandsClear()
    {
        _ = Service.ClearStartupCommands();

        await ReplyConfirmAsync(Strings.StartcmdsCleared(ctx.Guild.Id)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Toggles the forwarding of direct messages to the bot's owner(s).
    /// </summary>
    /// <remarks>
    ///     If message forwarding is enabled, it will be disabled, and vice versa.
    ///     A confirmation message is sent indicating the new state of message forwarding.
    /// </remarks>
    [Cmd]
    [Aliases]
    public async Task ForwardMessages()
    {
        var enabled = Service.ForwardMessages();

        if (enabled)
            await ReplyConfirmAsync(Strings.FwdmStart(ctx.Guild.Id)).ConfigureAwait(false);
        else
            await ReplyConfirmAsync(Strings.FwdmStop(ctx.Guild.Id)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Toggles whether forwarded messages are sent to all of the bot's owners or just the primary owner.
    /// </summary>
    /// <remarks>
    ///     If forwarding to all owners is enabled, it will be disabled, and vice versa.
    ///     A confirmation message is sent indicating the new state of this setting.
    /// </remarks>
    [Cmd]
    [Aliases]
    public async Task ForwardToAll()
    {
        var enabled = Service.ForwardToAll();

        if (enabled)
            await ReplyConfirmAsync(Strings.FwallStart(ctx.Guild.Id)).ConfigureAwait(false);
        else
            await ReplyConfirmAsync(Strings.FwallStop(ctx.Guild.Id)).ConfigureAwait(false);
    }


    /// <summary>
    ///     Displays statistics for all shards of the bot, including their statuses, guild counts, and user counts.
    /// </summary>
    /// <remarks>
    ///     This command aggregates the current status of all shards and displays a summary followed by a detailed
    ///     paginated list of each shard's status, including the time since last update, guild count, and user count.
    ///     The statuses are represented by emojis for quick visual reference.
    /// </remarks>
    [Cmd]
    [Aliases]
    public async Task ShardStats()
    {
        var statuses = client.Shards;

        // Aggregate shard status summaries
        var status = string.Join(" : ", statuses
            .Select(x => (ConnectionStateToEmoji(x.ConnectionState), x))
            .GroupBy(x => x.Item1)
            .Select(x => Strings.ShardStatusGroup(ctx.Guild.Id, x.Count(), x.Key))
            .ToArray());

        var allShardStrings = statuses
            .Select(st =>
            {
                var stateStr = ConnectionStateToEmoji(st.ConnectionState);
                var maxGuildCountLength = statuses.Max(x => x.Guilds.Count).ToString().Length;
                return Strings.ShardInfo(
                    ctx.Guild.Id,
                    stateStr,
                    st.ShardId.ToString().PadBoth(3),
                    st.Guilds.Count.ToString().PadBoth(maxGuildCountLength),
                    st.Guilds.Select(x => x.Users.Count).Sum()
                );
            })
            .ToArray();


        // Setup and send a paginator for detailed shard stats
        var paginator = new LazyPaginatorBuilder()
            .AddUser(ctx.User)
            .WithPageFactory(PageFactory)
            .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
            .WithMaxPageIndex(allShardStrings.Length / 25)
            .WithDefaultEmotes()
            .WithActionOnCancellation(ActionOnStop.DeleteMessage)
            .Build();

        await serv.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60)).ConfigureAwait(false);

        async Task<PageBuilder> PageFactory(int page)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            var str = string.Join("\n", allShardStrings.Skip(25 * page).Take(25));

            if (string.IsNullOrWhiteSpace(str))
                str = Strings.NoShardsOnPage(ctx.Guild.Id);

            return new PageBuilder()
                .WithAuthor(a => a.WithName(Strings.ShardStatsTitle(ctx.Guild.Id)))
                .WithTitle(status)
                .WithColor(Mewdeko.OkColor)
                .WithDescription(str)
                .WithFooter(Strings.PageSuffix(ctx.Guild.Id, page + 1));
        }
    }


    private static string ConnectionStateToEmoji(ConnectionState status)
    {
        return status switch
        {
            ConnectionState.Connected => "✅",
            ConnectionState.Disconnected => "🔻",
            ConnectionState.Connecting => "🔄",
            _ => "❓"
        };
    }

    /// <summary>
    ///     Commands the bot to leave a server.
    /// </summary>
    /// <param name="guildStr">The identifier or name of the guild to leave.</param>
    /// <remarks>
    ///     This action is irreversible through bot commands and should be used with caution.
    /// </remarks>
    [Cmd]
    [Aliases]
    public Task LeaveServer([Remainder] string guildStr)
    {
        return Service.LeaveGuild(guildStr);
    }

    /// <summary>
    ///     Initiates a shutdown of the bot.
    /// </summary>
    /// <remarks>
    ///     Before shutting down, the bot attempts to send a confirmation message. Delays for a short period before triggering
    ///     the shutdown sequence.
    /// </remarks>
    [Cmd]
    [Aliases]
    public async Task Die()
    {
        try
        {
            await ReplyConfirmAsync(Strings.ShuttingDown(ctx.Guild.Id)).ConfigureAwait(false);
        }
        catch
        {
            // ignored
        }

        await Task.Delay(2000).ConfigureAwait(false);
        Environment.SetEnvironmentVariable("SNIPE_CACHED", "0");
        Environment.SetEnvironmentVariable("AFK_CACHED", "0");
        Environment.Exit(0);
    }


    /// <summary>
    ///     Changes the bot's username to the specified new name.
    /// </summary>
    /// <param name="newName">The new username for the bot.</param>
    /// <remarks>
    ///     Does nothing if the new name is empty or whitespace. If a change is attempted and ratelimited, logs a warning
    ///     message.
    /// </remarks>
    [Cmd]
    [Aliases]
    public async Task SetName([Remainder] string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
            return;

        try
        {
            await client.CurrentUser.ModifyAsync(u => u.Username = newName).ConfigureAwait(false);
        }
        catch (RateLimitedException)
        {
            logger.LogWarning("You've been ratelimited. Wait 2 hours to change your name");
        }

        await ReplyConfirmAsync(Strings.BotName(ctx.Guild.Id, Format.Bold(newName))).ConfigureAwait(false);
    }


    /// <summary>
    ///     Sets the bot's online status.
    /// </summary>
    /// <param name="status">The new status to set.</param>
    /// <remarks>
    ///     Changes the bot's presence status to one of the specified options: Online, Idle, Do Not Disturb, or Invisible.
    /// </remarks>
    [Cmd]
    [Aliases]
    public async Task SetStatus([Remainder] SettableUserStatus status)
    {
        await client.SetStatusAsync(SettableUserStatusToUserStatus(status)).ConfigureAwait(false);

        await ReplyConfirmAsync(Strings.BotStatus(ctx.Guild.Id, Format.Bold(status.ToString()))).ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets the bot's avatar.
    /// </summary>
    /// <param name="img">
    ///     The URL of the new avatar image. If null, the command may default to removing the current avatar or
    ///     doing nothing, based on implementation.
    /// </param>
    /// <remarks>
    ///     Attempts to change the bot's avatar to the image found at the specified URL. Confirmation is sent upon success.
    /// </remarks>
    [Cmd]
    [Aliases]
    public async Task SetAvatar([Remainder] string? img = null)
    {
        var success = await Service.SetAvatar(img).ConfigureAwait(false);

        if (success)
            await ReplyConfirmAsync(Strings.SetAvatar(ctx.Guild.Id)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets the bot's currently playing game.
    /// </summary>
    /// <param name="type">The type of activity (e.g., Playing, Streaming).</param>
    /// <param name="game">The name of the game or activity. If null, might clear the current game.</param>
    /// <remarks>
    ///     This method updates the bot's "Playing" status. The actual displayed status will depend on the provided activity
    ///     type.
    /// </remarks>
    [Cmd]
    [Aliases]
    public async Task SetGame(ActivityType type, [Remainder] string? game = null)
    {
        var rep = new ReplacementBuilder()
            .WithDefault(Context)
            .Build();

        await bot.SetGameAsync(game == null ? game : rep.Replace(game), type).ConfigureAwait(false);

        await ReplyConfirmAsync(Strings.SetGame(ctx.Guild.Id)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets the bot's streaming status.
    /// </summary>
    /// <param name="url">The URL of the stream.</param>
    /// <param name="name">The name of the stream. If null, might use a default name or no name.</param>
    /// <remarks>
    ///     Changes the bot's activity to streaming, using the provided URL and name for the stream. Useful for when the bot is
    ///     used to indicate live streams.
    /// </remarks>
    [Cmd]
    [Aliases]
    public async Task SetStream(string url, [Remainder] string? name = null)
    {
        name ??= "";

        await client.SetGameAsync(name, url, ActivityType.Streaming).ConfigureAwait(false);

        await ReplyConfirmAsync(Strings.SetStream(ctx.Guild.Id)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Sends a message to a specified channel or user.
    /// </summary>
    /// <param name="whereOrTo">The ID of the channel or user to send the message to.</param>
    /// <param name="msg">The message to send.</param>
    /// <returns>A <see cref="Task" /> representing the asynchronous operation.</returns>
    [Cmd]
    [Aliases]
    public Task Send(ulong whereOrTo, [Remainder] string msg)
    {
        return Send(whereOrTo, 0, msg);
    }

    /// <summary>
    ///     Sends a message to a specified channel or user.
    /// </summary>
    /// <param name="whereOrTo">The ID of the channel or user to send the message to.</param>
    /// <param name="to">The ID of the user to send the message to.</param>
    /// <param name="msg">The message to send.</param>
    /// <remarks>
    ///     If the first ID is a server, the second ID is a channel, and the message is sent to that channel.
    /// </remarks>
    [Cmd]
    [Aliases]
    public async Task Send(ulong whereOrTo, ulong to = 0, [Remainder] string? msg = null)
    {
        var rep = new ReplacementBuilder().WithDefault(Context).Build();
        RestGuild potentialServer;
        try
        {
            potentialServer = await client.Rest.GetGuildAsync(whereOrTo).ConfigureAwait(false);
        }
        catch
        {
            var potentialUser = client.GetUser(whereOrTo);
            if (potentialUser is null)
            {
                await ctx.Channel.SendErrorAsync(Strings.UserNotFound(ctx.Guild.Id), botConfig);
                return;
            }

            if (SmartEmbed.TryParse(rep.Replace(msg), ctx.Guild?.Id, out var embed, out var plainText,
                    out var components))
            {
                await potentialUser.SendMessageAsync(plainText, embeds: embed, components: components.Build())
                    .ConfigureAwait(false);
                await ctx.Channel.SendConfirmAsync(Strings.MessageSentToUser(ctx.Guild.Id, potentialUser.Mention))
                    .ConfigureAwait(false);
                return;
            }

            await potentialUser.SendMessageAsync(rep.Replace(msg)).ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync(Strings.MessageSentToUser(ctx.Guild.Id, potentialUser.Mention))
                .ConfigureAwait(false);
            return;
        }

        if (to == 0)
        {
            await ctx.Channel.SendErrorAsync(Strings.NeedChannelId(ctx.Guild.Id), botConfig);

            return;
        }

        var channel = await potentialServer.GetTextChannelAsync(to).ConfigureAwait(false);
        if (channel is not null)
        {
            if (SmartEmbed.TryParse(rep.Replace(msg), ctx.Guild.Id, out var embed, out var plainText,
                    out var components))
            {
                await channel.SendMessageAsync(plainText, embeds: embed, components: components?.Build())
                    .ConfigureAwait(false);
                await ctx.Channel
                    .SendConfirmAsync(Strings.MessageSentToServer(ctx.Guild.Id, potentialServer, channel.Mention))
                    .ConfigureAwait(false);
                return;
            }

            await channel.SendMessageAsync(rep.Replace(msg)).ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync(Strings.MessageSentChannel(ctx.Guild.Id, potentialServer,
                channel.Mention));
            return;
        }

        var user = await potentialServer.GetUserAsync(to).ConfigureAwait(false);
        if (user is null)
        {
            await ctx.Channel.SendErrorAsync(Strings.ChannelNotFound(ctx.Guild.Id), botConfig);

            return;
        }

        if (SmartEmbed.TryParse(rep.Replace(msg), ctx.Guild?.Id, out var embed1, out var plainText1,
                out var components1))
        {
            await channel.SendMessageAsync(plainText1, embeds: embed1, components: components1?.Build())
                .ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync(Strings.MessageSentGuild(ctx.Guild.Id, potentialServer, user.Mention));

            return;
        }

        await channel.SendMessageAsync(rep.Replace(msg)).ConfigureAwait(false);
        await ctx.Channel.SendConfirmAsync(Strings.MessageSentUser(ctx.Guild.Id, user.Mention));
    }

    /// <summary>
    ///     Initiates the reloading of images used by the bot.
    /// </summary>
    /// <remarks>
    ///     This command triggers a process to reload all images, ensuring that any updates to image resources are reflected
    ///     without restarting the bot.
    ///     A confirmation message is sent upon the start of the reload process.
    /// </remarks>
    [Cmd]
    [Aliases]
    public async Task ImagesReload()
    {
        Service.ReloadImages();
        await ReplyConfirmAsync(Strings.ImagesLoading(ctx.Guild.Id)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Initiates the reloading of bot strings (localizations).
    /// </summary>
    /// <remarks>
    ///     This command triggers a process to reload all localized strings, ensuring that any updates to text resources are
    ///     applied without restarting the bot.
    ///     A confirmation message is sent upon successful reloading of bot strings.
    /// </remarks>
    [Cmd]
    [Aliases]
    public async Task StringsReload()
    {
        strings.Reload();
        await ReplyConfirmAsync(Strings.BotStringsReloaded(ctx.Guild.Id)).ConfigureAwait(false);
    }

    private static UserStatus SettableUserStatusToUserStatus(SettableUserStatus sus)
    {
        return sus switch
        {
            SettableUserStatus.Online => UserStatus.Online,
            SettableUserStatus.Invisible => UserStatus.Invisible,
            SettableUserStatus.Idle => UserStatus.AFK,
            SettableUserStatus.Dnd => UserStatus.DoNotDisturb,
            _ => UserStatus.Online
        };
    }

    /// <summary>
    ///     Executes a bash command. Depending on the platform, the command is executed in either bash or PowerShell.
    /// </summary>
    /// <param name="message">The command to execute.</param>
    /// <remarks>
    ///     The command is executed in a new process, and the output is sent as a paginated message. If the process hangs, it
    ///     is terminated. The command has a timeout of 2 hours. The output is split into chunks of 1988 characters to avoid
    ///     Discord message limits.
    /// </remarks>
    [Cmd]
    [Aliases]
    public async Task Bash([Remainder] string message)
    {
        using var process = new Process();
        var isLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
        if (isLinux)
        {
            process.StartInfo = new ProcessStartInfo
            {
                FileName = "/bin/bash",
                Arguments = $"-c \"{message} 2>&1\"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
        }
        else
        {
            process.StartInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"/c \"{message} 2>&1\"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
        }

        using (ctx.Channel.EnterTypingState())
        {
            process.Start();
            var reader = process.StandardOutput;
            var timeout = TimeSpan.FromHours(2);
            var task = Task.Run(() => reader.ReadToEndAsync());
            if (await Task.WhenAny(task, Task.Delay(timeout)) == task)
            {
                var output = await task.ConfigureAwait(false);

                if (string.IsNullOrEmpty(output))
                {
                    await ctx.Channel.SendMessageAsync($"```{Strings.EvalOutputBlank(ctx.Guild.Id)}```");
                    return;
                }

                var chunkSize = 1988;
                var stringList = new List<string>();

                for (var i = 0; i < output.Length; i += chunkSize)
                {
                    if (i + chunkSize > output.Length)
                        chunkSize = output.Length - i;
                    stringList.Add(output.Substring(i, chunkSize));

                    if (stringList.Count < 50) continue;
                    process.Kill();
                    break;
                }

                var paginator = new LazyPaginatorBuilder()
                    .AddUser(ctx.User)
                    .WithPageFactory(PageFactory)
                    .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                    .WithMaxPageIndex(stringList.Count - 1)
                    .WithDefaultEmotes()
                    .WithActionOnCancellation(ActionOnStop.DeleteMessage)
                    .Build();

                await serv.SendPaginatorAsync(paginator, ctx.Channel, TimeSpan.FromMinutes(60)).ConfigureAwait(false);

                async Task<PageBuilder> PageFactory(int page)
                {
                    await Task.CompletedTask;
                    return new PageBuilder()
                        .WithOkColor()
                        .WithAuthor(Strings.BashOutput(ctx.Guild.Id))
                        .AddField(Strings.Input(ctx.Guild.Id), message)
                        .WithDescription(Strings.CodeBlockShell(ctx.Guild.Id, isLinux ? "bash" : "powershell",
                            stringList[page]));
                }
            }
            else
            {
                process.Kill();
                await ctx.Channel.SendErrorAsync(Strings.ProcessTerminated(ctx.Guild.Id), botConfig)
                    .ConfigureAwait(false);
            }

            if (!process.HasExited)
            {
                await process.WaitForExitAsync().ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    ///     Evaluates a C# code snippet.
    /// </summary>
    /// <param name="code">The C# code to evaluate.</param>
    /// <remarks>
    ///     The code is compiled and executed in a sandboxed environment. The result is displayed in an embed, including the
    ///     return value, compilation time, and execution time.
    /// </remarks>
    /// <exception cref="ArgumentException"></exception>
    [Cmd]
    [Aliases]
    [OwnerOnly]
    public async Task Evaluate([Remainder] string code = null)
    {
        var codeToEvaluate = string.Empty;
        var evaluationSource = "code block";

        // Check if there's at least one attachment
        if (ctx.Message.Attachments.Count != 0)
        {
            var attachment = Context.Message.Attachments.First();

            // Validate the file extension
            // Optional: Validate the file size (e.g., limit to 100 KB)
            if (attachment.Size > 100 * 1024)
            {
                await ReplyAsync(Strings.EvalTooLarge(ctx.Guild.Id));
                return;
            }

            try
            {
                // Download the attachment content
                await using var stream = await httpClient.GetStreamAsync(attachment.Url);
                using var reader = new StreamReader(stream);
                codeToEvaluate = await reader.ReadToEndAsync();
                evaluationSource = $"attachment `{attachment.Filename}`";
            }
            catch (Exception ex)
            {
                await ReplyAsync(Strings.EvalReadError(ctx.Guild.Id, ex.Message));
                return;
            }
        }
        else
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                await ReplyAsync(Strings.EvalNoCode(ctx.Guild.Id));
                return;
            }

            var startIndex = code.IndexOf("```", StringComparison.Ordinal);
            if (startIndex != -1)
            {
                startIndex += 3;
                var languageSpecifierEnd = code.IndexOf('\n', startIndex);
                if (languageSpecifierEnd != -1)
                {
                    startIndex = languageSpecifierEnd + 1;
                    var endIndex = code.LastIndexOf("```", StringComparison.Ordinal);
                    if (endIndex != -1 && endIndex > startIndex)
                    {
                        codeToEvaluate = code.Substring(startIndex, endIndex - startIndex);
                        evaluationSource = "code block";
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(codeToEvaluate))
            {
                await ReplyAsync(Strings.EvalNoCode(ctx.Guild.Id));
                return;
            }
        }

        var embed = new EmbedBuilder
        {
            Title = Strings.EvalCompiling(ctx.Guild.Id), Color = new Color(0xD091B2)
        };
        var msg = await Context.Channel.SendMessageAsync(embed: embed.Build()).ConfigureAwait(false);

        // Get all namespaces from loaded assemblies
        var allNamespaces = new HashSet<string>();
        var assemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(xa => !xa.IsDynamic && !string.IsNullOrWhiteSpace(xa.Location));

        foreach (var assembly in assemblies)
        {
            try
            {
                var types = assembly.GetExportedTypes();
                foreach (var type in types)
                {
                    if (!string.IsNullOrWhiteSpace(type.Namespace))
                    {
                        allNamespaces.Add(type.Namespace);
                    }
                }
            }
            catch
            {
                // Some assemblies might not allow us to get types
            }
        }

        // Add commonly used namespaces explicitly (in case they were missed)
        var additionalNamespaces = new[]
        {
            "System", "System.Collections", "System.Collections.Generic", "System.Collections.Concurrent",
            "System.Diagnostics", "System.Dynamic", "System.IO", "System.Linq", "System.Linq.Expressions", "System.Net",
            "System.Net.Http", "System.Net.Http.Headers", "System.Reflection", "System.Runtime.CompilerServices",
            "System.Text", "System.Text.RegularExpressions", "System.Threading", "System.Threading.Tasks",
            "System.Globalization", "Discord", "Discord.Net", "Discord.Commands", "Discord.WebSocket", "Discord.Rest",
            "LinqToDB", "LinqToDB.Linq", "Microsoft.Extensions.DependencyInjection", "Newtonsoft.Json",
            "Newtonsoft.Json.Linq"
        };

        foreach (var ns in additionalNamespaces)
        {
            allNamespaces.Add(ns);
        }

        // Filter out problematic namespaces
        var filteredNamespaces = allNamespaces
            .Where(ns => !ns.StartsWith("Internal.") &&
                         !ns.StartsWith("Microsoft.CodeAnalysis") &&
                         !ns.Contains("Private") &&
                         !ns.Contains("<"))
            .OrderBy(ns => ns)
            .ToList();

        // Create globals with access to all major services
        var globals = new EvaluationEnvironment((CommandContext)Context)
        {
            // Add references to commonly used services
            Client = client,
            Services = services,
            DbFactory = dbFactory,
            Cache = cache,
            CommandService = commandService,
            GuildSettings = guildSettings,
            BotConfig = botConfig,
            Http = httpClient,
            Strings = strings,
            Bot = bot
        };

        // Set up the script options with all imports and references
        var scriptOptions = ScriptOptions.Default
            .WithImports(filteredNamespaces)
            .WithReferences(assemblies)
            .WithAllowUnsafe(true);

        // Add a preprocessor to the code to include commonly used extension methods
        var preprocessedCode = $@"
// Auto-imported extension methods and helpers
using static System.Math;
using static System.Console;
using static Newtonsoft.Json.JsonConvert;

{codeToEvaluate}";

        logger.LogInformation(preprocessedCode);

        // Start measuring compilation time
        var compilationStopwatch = Stopwatch.StartNew();
        var script = CSharpScript.Create(preprocessedCode, scriptOptions, typeof(EvaluationEnvironment));
        var compilationDiagnostics = script.Compile();
        compilationStopwatch.Stop();

        // Check for compilation errors
        if (compilationDiagnostics.Any(diag => diag.Severity == DiagnosticSeverity.Error))
        {
            embed = new EmbedBuilder
            {
                Title = "Compilation Failed",
                Description =
                    $"Compilation failed after {compilationStopwatch.ElapsedMilliseconds:#,##0}ms with {compilationDiagnostics.Length:#,##0} error(s).",
                Color = new Color(0xE74C3C)
            };

            foreach (var diagnostic in compilationDiagnostics.Where(diag => diag.Severity == DiagnosticSeverity.Error)
                         .Take(3))
            {
                var lineSpan = diagnostic.Location.GetLineSpan();
                embed.AddField(
                    $"Error at Line {lineSpan.StartLinePosition.Line + 1}, Character {lineSpan.StartLinePosition.Character + 1}",
                    $"```csharp\n{diagnostic.GetMessage()}\n```");
            }

            if (compilationDiagnostics.Length > 3)
            {
                embed.AddField("Additional Errors",
                    $"{compilationDiagnostics.Length - 3:#,##0} more error(s) not displayed.");
            }

            await msg.ModifyAsync(x => x.Embed = embed.Build()).ConfigureAwait(false);
            return;
        }

        // Execute the script and measure execution time
        Exception executionException = null;
        ScriptState<object> scriptState = null;
        var executionStopwatch = Stopwatch.StartNew();
        try
        {
            scriptState = await script.RunAsync(globals).ConfigureAwait(false);
            executionException = scriptState.Exception;
        }
        catch (Exception ex)
        {
            executionException = ex;
        }

        executionStopwatch.Stop();

        // Handle execution exceptions
        if (executionException != null)
        {
            embed = new EmbedBuilder
            {
                Title = "Execution Failed",
                Description =
                    $"Execution failed after {executionStopwatch.ElapsedMilliseconds:#,##0}ms with `{executionException.GetType()}: {executionException.Message}`.",
                Color = new Color(0xE74C3C)
            };
            await msg.ModifyAsync(x => x.Embed = embed.Build()).ConfigureAwait(false);
            return;
        }

        // Execution succeeded
        embed = new EmbedBuilder
        {
            Title = "Evaluation Successful", Color = new Color(0x2ECC71)
        };

        var result = scriptState.ReturnValue != null ? scriptState.ReturnValue.ToString() : "No value returned";

        embed.AddField("Result", $"```csharp\n{result}\n```")
            .AddField("Source", evaluationSource, true)
            .AddField("Compilation Time", $"{compilationStopwatch.ElapsedMilliseconds:#,##0}ms", true)
            .AddField("Execution Time", $"{executionStopwatch.ElapsedMilliseconds:#,##0}ms", true);

        if (scriptState.ReturnValue != null)
        {
            embed.AddField("Return Type", scriptState.ReturnValue.GetType().ToString(), true);
        }

        await msg.ModifyAsync(x => x.Embed = embed.Build()).ConfigureAwait(false);
    }
}

/// <summary>
///     Evaluation environment with additional service references for use in eval commands
/// </summary>
public sealed class EvaluationEnvironment
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="EvaluationEnvironment" /> class with the specified command context
    /// </summary>
    /// <param name="ctx">The command context associated with the current eval command execution</param>
    public EvaluationEnvironment(CommandContext ctx)
    {
        Ctx = ctx;
    }

    /// <summary>
    ///     Gets the command context associated with the current eval execution
    /// </summary>
    public CommandContext Ctx { get; }

    /// <summary>
    ///     Gets the message that triggered the current eval command
    /// </summary>
    public IUserMessage Message
    {
        get
        {
            return Ctx.Message;
        }
    }

    /// <summary>
    ///     Gets the channel in which the eval command was executed
    /// </summary>
    public IMessageChannel Channel
    {
        get
        {
            return Ctx.Channel;
        }
    }

    /// <summary>
    ///     Gets the guild in which the eval command was executed. May be null for commands executed in DMs
    /// </summary>
    public IGuild Guild
    {
        get
        {
            return Ctx.Guild;
        }
    }

    /// <summary>
    ///     Gets the user who executed the eval command
    /// </summary>
    public IUser User
    {
        get
        {
            return Ctx.User;
        }
    }

    /// <summary>
    ///     Gets the guild member who executed the eval command. This is a convenience property for accessing the user as an
    ///     IGuildUser
    /// </summary>
    public IGuildUser Member
    {
        get
        {
            return (IGuildUser)Ctx.User;
        }
    }

    /// <summary>
    ///     Gets or sets the Discord sharded client instance for interacting with the Discord API
    /// </summary>
    public DiscordShardedClient Client { get; set; }

    /// <summary>
    ///     Gets or sets the service provider for dependency injection and service resolution
    /// </summary>
    public IServiceProvider Services { get; set; }

    /// <summary>
    ///     Gets or sets the database connection factory for creating database connections
    /// </summary>
    public IDataConnectionFactory DbFactory { get; set; }

    /// <summary>
    ///     Gets or sets the cache service for Redis operations and data caching
    /// </summary>
    public IDataCache Cache { get; set; }

    /// <summary>
    ///     Gets or sets the command service for command registration and execution
    /// </summary>
    public CommandService CommandService { get; set; }

    /// <summary>
    ///     Gets or sets the guild settings service for managing guild-specific configurations
    /// </summary>
    public GuildSettingsService GuildSettings { get; set; }

    /// <summary>
    ///     Gets or sets the bot configuration containing global bot settings
    /// </summary>
    public BotConfig BotConfig { get; set; }

    /// <summary>
    ///     Gets or sets the HTTP client for making web requests
    /// </summary>
    public HttpClient Http { get; set; }

    /// <summary>
    ///     Gets or sets the bot strings service for localization and string resources
    /// </summary>
    public IBotStrings Strings { get; set; }

    /// <summary>
    ///     Gets or sets the main Mewdeko bot instance
    /// </summary>
    public Mewdeko Bot { get; set; }

    /// <summary>
    ///     Executes a database operation within a managed connection scope
    /// </summary>
    /// <typeparam name="T">The return type of the database operation</typeparam>
    /// <param name="func">The async function to execute with the database connection</param>
    /// <returns>The result of the database operation</returns>
    /// <remarks>
    ///     This method automatically handles connection lifecycle, ensuring the connection is properly disposed after use
    /// </remarks>
    public async Task<T> Db<T>(Func<DataConnection, Task<T>> func)
    {
        await using var db = await DbFactory.CreateConnectionAsync();
        return await func(db);
    }

    /// <summary>
    ///     Retrieves a service of the specified type from the dependency injection container
    /// </summary>
    /// <typeparam name="T">The type of service to retrieve</typeparam>
    /// <returns>The requested service instance, or null if the service is not registered</returns>
    public T GetService<T>()
    {
        return Services.GetService<T>();
    }
}