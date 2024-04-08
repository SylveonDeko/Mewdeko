using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using Discord.Commands;
using Discord.Net;
using Discord.Rest;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using LinqToDB.EntityFrameworkCore;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Common.Configs;
using Mewdeko.Common.DiscordImplementations;
using Mewdeko.Modules.OwnerOnly.Services;
using Mewdeko.Services.Settings;
using Mewdeko.Services.strings;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.EntityFrameworkCore;
using Octokit;
using Serilog;

namespace Mewdeko.Modules.OwnerOnly;

/// <summary>
/// Initializes a new instance of the <see cref="OwnerOnly"/> class, intended for owner-only operations within the Mewdeko bot framework.
/// </summary>
/// <param name="client">The Discord client used to interact with the Discord API.</param>
/// <param name="bot">The main instance of the Mewdeko bot.</param>
/// <param name="strings">Provides access to localized strings within the bot.</param>
/// <param name="serv">Interactive service for handling interactive user commands.</param>
/// <param name="coord">Coordinator for managing bot operations across different services and modules.</param>
/// <param name="settingServices">A collection of configuration services for managing bot settings.</param>
/// <param name="db">Service for database operations and access.</param>
/// <param name="cache">Cache service for storing and retrieving temporary data.</param>
/// <param name="commandService">Service for handling and executing Discord commands.</param>
/// <param name="services">The service provider for dependency injection.</param>
/// <param name="guildSettings">Service for accessing and modifying guild-specific settings.</param>
/// <param name="commandHandler">Handler for processing and executing commands received from users.</param>
[OwnerOnly]
public class OwnerOnly(
    DiscordSocketClient client,
    Mewdeko bot,
    IBotStrings strings,
    InteractiveService serv,
    ICoordinator coord,
    IEnumerable<IConfigService> settingServices,
    DbService db,
    IDataCache cache,
    CommandService commandService,
    IServiceProvider services,
    GuildSettingsService guildSettings,
    CommandHandler commandHandler,
    BotConfig config)
    : MewdekoModuleBase<OwnerOnlyService>
{
    /// <summary>
    /// Defines the set of user statuses that can be programmatically assigned.
    /// </summary>
    public enum SettableUserStatus
    {
        /// <summary>
        /// Indicates the user is online and available.
        /// </summary>
        Online,

        /// <summary>
        /// Indicates the user is online but appears as offline or invisible to others.
        /// </summary>
        Invisible,

        /// <summary>
        /// Indicates the user is idle and may be away from their device.
        /// </summary>
        Idle,

        /// <summary>
        /// Indicates the user does not wish to be disturbed (Do Not Disturb).
        /// </summary>
        Dnd
    }


    /// <summary>
    /// Clears the count of used GPT tokens after confirming with the user.
    /// </summary>
    /// <remarks>
    /// This command prompts the user for confirmation before proceeding to clear the used token count.
    /// If the user confirms, it clears the count and notifies the user of completion.
    /// </remarks>
    [Cmd, Aliases]
    public async Task ClearUsedTokens()
    {
        // Assuming PromptUserConfirmAsync is a method that prompts the user and waits for a confirmation response.
        if (await PromptUserConfirmAsync("Are you sure you want to clear the used token count for GPT?", ctx.User.Id))
        {
            await Service.ClearUsedTokens();
            await ctx.Channel.SendErrorAsync("Cleared.",
                config); // Assuming SendErrorAsync sends a message to the channel.
        }
    }


    /// <summary>
    /// Updates the bot to the latest version available on the repository.
    /// </summary>
    [Cmd, Aliases]
    public async Task Update()
    {
        var shell = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "cmd" : "/bin/bash";

        var buttons = new ComponentBuilder()
            .WithButton("Stable", "stable")
            .WithButton("Nightly", "nightly", ButtonStyle.Danger);

        var eb = new EmbedBuilder()
            .WithOkColor()
            .WithDescription("Which version would you like to check updates against?");

        var msg = await ctx.Channel.SendMessageAsync(embed: eb.Build(), components: buttons.Build());
        var result = await GetButtonInputAsync(ctx.Channel.Id, msg.Id, ctx.User.Id);

        if (result is null)
        {
            await msg.ModifyAsync(x => x.Embed = new EmbedBuilder()
                .WithErrorColor()
                .WithDescription("Timed out.")
                .Build());
            return;
        }

        var branch = result switch
        {
            "stable" => "main",
            "nightly" => "psqldeko",
            _ => "main"
        };

        var process = Process.Start(new ProcessStartInfo
        {
            FileName = shell,
            Arguments = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? "/c \"git rev-parse --abbrev-ref HEAD\""
                : "-c \"git rev-parse --abbrev-ref HEAD\"",
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        });
        await process.WaitForExitAsync();
        var text = await process.StandardOutput.ReadToEndAsync();
        if (text.Replace("\n", "") != branch)
        {
            if (await PromptUserConfirmAsync(
                    "Switching branches can cause issues like database incompatibility," +
                    " or in the case of going from stable to nightly, " +
                    "major bugs, ***Are you sure you want to continue?***",
                    ctx.User.Id))
            {
                await ctx.Channel.SendConfirmAsync("Switching branches and updating, please wait...");
                var typing = ctx.Channel.EnterTypingState();
                var sw = Stopwatch.StartNew();
                var process2 = Process.Start(new ProcessStartInfo
                {
                    FileName = shell,
                    Arguments = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                        ? $"/c \"git checkout {branch} && git pull\""
                        : $"-c \"git checkout {branch} && git pull\"",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                await process2.WaitForExitAsync();
                Log.Information("Update Logs: {UpdateLogs}", await process2.StandardOutput.ReadToEndAsync());
                sw.Stop();
                var eb2 = new EmbedBuilder()
                    .WithDescription("Update complete.")
                    .WithOkColor()
                    .WithFooter("Time taken: " + sw.Elapsed.ToString("g"));
                await ctx.Channel.SendMessageAsync(embed: eb2.Build());
                typing.Dispose();
            }
            else
            {
                await ctx.Channel.SendErrorAsync("Cancelled.", config);
            }
        }
        else
        {
            var getCommit = Process.Start(new ProcessStartInfo
            {
                FileName = shell,
                Arguments = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                    ? "/c \"git rev-parse HEAD\""
                    : "-c \"git rev-parse HEAD\"",
                RedirectStandardOutput = true,
                CreateNoWindow = true
            });
            await getCommit.WaitForExitAsync();
            var commit = await getCommit.StandardOutput.ReadToEndAsync();
            var github = new GitHubClient(new ProductHeaderValue("Mewdeko"));
            var repo = await github.Repository.Branch.Get("sylveondeko", "Mewdeko", branch);
            if (repo is null)
            {
                await ctx.Channel.SendErrorAsync(
                    "Failed to get repo info. Please create an issue on the repo or join the support server.", config);
                return;
            }

            var commitSha = repo.Commit.Sha;
            if (commitSha != commit.Replace("\n", ""))
            {
                if (await PromptUserConfirmAsync(
                        "Are you sure you want to update?",
                        ctx.User.Id))
                {
                    await ctx.Channel.SendConfirmAsync("Updating, please wait...");
                    var typing = ctx.Channel.EnterTypingState();
                    var sw = Stopwatch.StartNew();
                    var process2 = Process.Start(new ProcessStartInfo
                    {
                        FileName = shell,
                        Arguments = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                            ? "/c git pull"
                            : "-c git pull",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    });
                    await process2.WaitForExitAsync();
                    Log.Information("Update Logs: {UpdateLogs}", await process2.StandardOutput.ReadToEndAsync());
                    sw.Stop();
                    var eb2 = new EmbedBuilder()
                        .WithDescription("Update complete.")
                        .WithOkColor()
                        .WithFooter("Time taken: " + sw.Elapsed.ToString("g"));
                    await ctx.Channel.SendMessageAsync(embed: eb2.Build());
                    typing.Dispose();
                }
                else
                {
                    await ctx.Channel.SendErrorAsync("Cancelled.", config);
                }
            }
            else
            {
                await ctx.Channel.SendErrorAsync("Already up to date.", config);
            }
        }
    }

    /// <summary>
    /// Executes a command as if it were sent by the specified guild user.
    /// </summary>
    /// <param name="user">The guild user to impersonate when executing the command.</param>
    /// <param name="args">The command string to execute, including command name and arguments.</param>
    /// <remarks>
    /// This method constructs a fake message with the specified user as the author and the given command string,
    /// then enqueues it for command parsing and execution.
    /// </remarks>
    [Cmd, Aliases]
    public async Task Sudo(IGuildUser user, [Remainder] string args)
    {
        var msg = new MewdekoUserMessage
        {
            Content = $"{await guildSettings.GetPrefix(ctx.Guild)}{args}", Author = user, Channel = ctx.Channel
        };
        commandHandler.AddCommandToParseQueue(msg);
        _ = Task.Run(() => commandHandler.ExecuteCommandsInChannelAsync(ctx.Channel.Id))
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Executes a command as if it were sent by the owner of the guild.
    /// </summary>
    /// <param name="args">The command string to execute, including command name and arguments.</param>
    /// <remarks>
    /// This method constructs a fake message with the guild owner as the author and the given command string,
    /// then enqueues it for command parsing and execution. Useful for performing actions that require owner permissions.
    /// </remarks>
    [Cmd, Aliases]
    public async Task Sudo([Remainder] string args)
    {
        var msg = new MewdekoUserMessage
        {
            Content = $"{await guildSettings.GetPrefix(ctx.Guild)}{args}",
            Author = await Context.Guild.GetOwnerAsync(),
            Channel = ctx.Channel
        };
        commandHandler.AddCommandToParseQueue(msg);
        _ = Task.Run(() => commandHandler.ExecuteCommandsInChannelAsync(ctx.Channel.Id))
            .ConfigureAwait(false);
    }


    /// <summary>
    /// Executes a Redis command and returns the result.
    /// </summary>
    /// <param name="command">The Redis command to execute.</param>
    /// <remarks>
    /// This method sends the specified command to Redis through the configured cache connection.
    /// The result of the command execution is then sent back as a message in the Discord channel.
    /// </remarks>
    [Cmd, Aliases]
    public async Task RedisExec([Remainder] string command)
    {
        var result = await cache.ExecuteRedisCommand(command).ConfigureAwait(false);
        var eb = new EmbedBuilder().WithOkColor().WithTitle(result.Resp2Type.ToString())
            .WithDescription(result.ToString());
        await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
    }

    /// <summary>
    /// Executes a raw SQL command against the database.
    /// </summary>
    /// <param name="sql">The SQL command to execute.</param>
    /// <remarks>
    /// Prompts the user for confirmation before executing the SQL command.
    /// The number of affected rows is sent back as a message in the Discord channel.
    /// Use with caution, as executing raw SQL can directly affect the database integrity.
    /// </remarks>
    [Cmd, Aliases]
    public async Task SqlExec([Remainder] string sql)
    {
        if (!await PromptUserConfirmAsync("Are you sure you want to execute this??", ctx.User.Id).ConfigureAwait(false))
            return;
        await using var uow = db.GetDbContext();
        var affected = await uow.Database.ExecuteSqlRawAsync(sql).ConfigureAwait(false);
        await ctx.Channel.SendErrorAsync($"Affected {affected} rows.", config).ConfigureAwait(false);
    }

    /// <summary>
    /// Lists all servers the bot is currently in.
    /// </summary>
    /// <remarks>
    /// This method creates a paginated list of servers, showing server names, IDs, member counts, online member counts,
    /// server owners, and creation dates. Pagination allows browsing through the server list if it exceeds the page limit.
    /// </remarks>
    [Cmd, Aliases]
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
            var newGuilds = guilds.Skip(10 * page);
            var eb = new PageBuilder().WithOkColor().WithTitle("Servers List");
            foreach (var i in newGuilds)
            {
                eb.AddField($"{i.Name} | {i.Id}", $"Members: {i.Users.Count}"
                                                  + $"\nOnline Members: {i.Users.Count(x => x.Status is UserStatus.Online or UserStatus.DoNotDisturb or UserStatus.Idle)}"
                                                  + $"\nOwner: {i.Owner} | {i.OwnerId}"
                                                  + $"\n Created On: {TimestampTag.FromDateTimeOffset(i.CreatedAt)}");
            }

            return eb;
        }
    }

    /// <summary>
    /// Retrieves and displays statistics on the most used command, module, guild, and user.
    /// </summary>
    /// <remarks>
    /// This method calculates and reports the top entities based on their usage count.
    /// It displays the most frequently used command, the module that's used the most,
    /// the user who has used commands the most, and the guild with the highest command usage.
    /// These statistics are presented as an embed in the Discord channel.
    /// </remarks>
    [Cmd, Aliases]
    public async Task CommandStats()
    {
        await using var uow = db.GetDbContext();
        var commandStatsTable = uow.CommandStats;
        // Fetch actual tops
        var topCommand = await commandStatsTable.Where(x => x.Trigger == 0).GroupBy(q => q.NameOrId)
            .OrderByDescending(gp => gp.Count()).Select(x => x.Key).FirstOrDefaultAsyncLinqToDB();
        var topModule = await commandStatsTable.Where(x => x.Trigger == 0).GroupBy(q => q.Module)
            .OrderByDescending(gp => gp.Count()).Select(x => x.Key).FirstOrDefaultAsyncLinqToDB();
        var topGuild = await commandStatsTable.Where(x => x.Trigger == 0).GroupBy(q => q.GuildId)
            .OrderByDescending(gp => gp.Count()).Select(x => x.Key).FirstOrDefaultAsyncLinqToDB();
        var topUser = await commandStatsTable.Where(x => x.Trigger == 0).GroupBy(q => q.UserId)
            .OrderByDescending(gp => gp.Count()).Select(x => x.Key).FirstOrDefaultAsyncLinqToDB();

        // Then fetch their counts... This can probably be done better....
        var topCommandCount = commandStatsTable.Count(x => x.NameOrId == topCommand);
        var topModuleCount = commandStatsTable.Count(x => x.NameOrId == topCommand);
        var topGuildCount = commandStatsTable.Count(x => x.GuildId == topGuild);
        var topUserCount = commandStatsTable.Count(x => x.UserId == topUser);

        var guild = await client.Rest.GetGuildAsync(topGuild);
        var user = await client.Rest.GetUserAsync(topUser);

        var eb = new EmbedBuilder()
            .WithOkColor()
            .AddField("Top Command", $"{topCommand} was used {topCommandCount} times!")
            .AddField("Top Module", $"{topModule} was used {topModuleCount} times!")
            .AddField("Top User", $"{user} has used commands {topUserCount} times!")
            .AddField("Top Guild", $"{guild} has used commands {topGuildCount} times!");

        await ctx.Channel.SendMessageAsync(embed: eb.Build());
    }


    /// <summary>
    /// Changes yml based config for the bot.
    /// </summary>
    /// <param name="name">The name of the config to change.</param>
    /// <param name="prop">The property of the config to change.</param>
    /// <param name="value">The new value to set for the property.</param>
    [Cmd, Aliases]
    public async Task Config(string? name = null, string? prop = null, [Remainder] string? value = null)
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
                    .WithTitle(GetText("config_list"))
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
                    .WithDescription(GetText("config_not_found", Format.Code(name)))
                    .AddField(GetText("config_list"), string.Join("\n", configNames));

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
                    .WithTitle($"⚙️ {setting.Name}")
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
                    .WithDescription(GetText("config_prop_not_found", Format.Code(prop), Format.Code(name)))
                    .AddField($"⚙️ {setting.Name}", propStrings);

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
                await ReplyErrorLocalizedAsync("config_edit_fail", Format.Code(prop), Format.Code(value))
                    .ConfigureAwait(false);
                return;
            }

            await ctx.OkAsync().ConfigureAwait(false);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            await ctx.Channel.SendErrorAsync(
                "There was an error setting or printing the config, please check the logs.", config);
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
    /// Toggles the rotation of playing statuses for the bot.
    /// </summary>
    /// <remarks>
    /// If rotation is enabled, it will be disabled, and vice versa. Confirmation of the action is sent as a reply.
    /// </remarks>
    [Cmd, Aliases]
    public async Task RotatePlaying()
    {
        if (Service.ToggleRotatePlaying())
            await ReplyConfirmLocalizedAsync("ropl_enabled").ConfigureAwait(false);
        else
            await ReplyConfirmLocalizedAsync("ropl_disabled").ConfigureAwait(false);
    }

    /// <summary>
    /// Adds a new status to the rotation of playing statuses for the bot.
    /// </summary>
    /// <param name="t">The type of activity (e.g., Playing, Streaming).</param>
    /// <param name="status">The text of the status to add.</param>
    /// <remarks>
    /// Adds a new status with the specified activity type and text. Confirmation of addition is sent as a reply.
    /// </remarks>
    [Cmd, Aliases]
    public async Task AddPlaying(ActivityType t, [Remainder] string status)
    {
        await Service.AddPlaying(t, status).ConfigureAwait(false);

        await ReplyConfirmLocalizedAsync("ropl_added").ConfigureAwait(false);
    }

    /// <summary>
    /// Lists all statuses currently in the rotation.
    /// </summary>
    /// <remarks>
    /// Sends a reply with a numbered list of all statuses in the rotation. If no statuses are set, sends an error message.
    /// </remarks>
    [Cmd, Aliases]
    public async Task ListPlaying()
    {
        var statuses = Service.GetRotatingStatuses();

        if (statuses.Count == 0)
        {
            await ReplyErrorLocalizedAsync("ropl_not_set").ConfigureAwait(false);
        }
        else
        {
            var i = 1;
            await ReplyConfirmLocalizedAsync("ropl_list",
                    string.Join("\n\t", statuses.Select(rs => $"`{i++}.` *{rs.Type}* {rs.Status}")))
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Sets or displays the default command prefix.
    /// </summary>
    /// <param name="prefix">The new prefix to set. If null or whitespace, the current prefix is displayed instead.</param>
    /// <remarks>
    /// Changes the bot's command prefix for the server or displays the current prefix if no new prefix is provided.
    /// Confirmation of the new prefix or the current prefix is sent as a reply.
    /// </remarks>
    [Cmd, Aliases]
    public async Task DefPrefix([Remainder] string? prefix = null)
    {
        if (string.IsNullOrWhiteSpace(prefix))
        {
            await ReplyConfirmLocalizedAsync("defprefix_current", await guildSettings.GetPrefix())
                .ConfigureAwait(false);
            return;
        }

        var oldPrefix = await guildSettings.GetPrefix();
        var newPrefix = Service.SetDefaultPrefix(prefix);

        await ReplyConfirmLocalizedAsync("defprefix_new", Format.Code(oldPrefix), Format.Code(newPrefix))
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Removes a status from the rotating playing statuses by its index.
    /// </summary>
    /// <param name="index">The one-based index of the status to remove. The actual removal will use zero-based indexing.</param>
    /// <remarks>
    /// If the status at the provided index exists, it will be removed, and a confirmation message is sent.
    /// </remarks>
    [Cmd, Aliases]
    public async Task RemovePlaying(int index)
    {
        index--;

        var msg = await Service.RemovePlayingAsync(index).ConfigureAwait(false);

        if (msg == null)
            return;

        await ReplyConfirmLocalizedAsync("reprm", msg).ConfigureAwait(false);
    }

    /// <summary>
    /// Sets the default language for the bot by specifying a culture name.
    /// </summary>
    /// <param name="name">The name of the culture to set as the default language. Use "default" to reset to the bot's original default language.</param>
    /// <remarks>
    /// This method allows changing the bot's default language or resetting it to its original default.
    /// A confirmation message will be sent upon successful change.
    /// </remarks>
    [Cmd, Aliases]
    public async Task LanguageSetDefault(string name)
    {
        try
        {
            CultureInfo? ci;
            if (string.Equals(name.Trim(), "default", StringComparison.InvariantCultureIgnoreCase))
            {
                Localization.ResetDefaultCulture();
                ci = Localization.DefaultCultureInfo;
            }
            else
            {
                ci = new CultureInfo(name);
                Localization.SetDefaultCulture(ci);
            }

            await ReplyConfirmLocalizedAsync("lang_set_bot", Format.Bold(ci.ToString()),
                Format.Bold(ci.NativeName)).ConfigureAwait(false);
        }
        catch (Exception)
        {
            await ReplyErrorLocalizedAsync("lang_set_fail").ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Adds a new startup command to be executed when the bot starts.
    /// </summary>
    /// <param name="cmdText">The text of the command to add, excluding the prefix.</param>
    /// <remarks>
    /// Requires the user to have Administrator permissions or be the owner of the bot.
    /// Commands that could potentially restart or shut down the bot are ignored for safety reasons.
    /// </remarks>
    [Cmd, Aliases, RequireContext(ContextType.Guild),
     UserPerm(GuildPermission.Administrator), OwnerOnly]
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
        Service.AddNewAutoCommand(cmd);

        await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
            .WithTitle(GetText("scadd"))
            .AddField(efb => efb.WithName(GetText("server"))
                .WithValue(cmd.GuildId == null ? "-" : $"{cmd.GuildName}/{cmd.GuildId}").WithIsInline(true))
            .AddField(efb => efb.WithName(GetText("channel"))
                .WithValue($"{cmd.ChannelName}/{cmd.ChannelId}").WithIsInline(true))
            .AddField(efb => efb.WithName(GetText("command_text"))
                .WithValue(cmdText).WithIsInline(false))).ConfigureAwait(false);
    }

    /// <summary>
    /// Adds an auto command to be executed periodically in the specified guild.
    /// </summary>
    /// <param name="interval">The interval in seconds at which the command should be executed. Must be 5 seconds or more.</param>
    /// <param name="cmdText">The command text to be executed automatically.</param>
    /// <remarks>
    /// Requires the user to have Administrator permissions or to be the owner of the bot.
    /// The command will not be added if it fails any precondition checks,
    /// if it matches a forbidden command (e.g., a command to shut down the bot),
    /// or if the maximum number of auto commands (15) for the guild has been reached.
    /// </remarks>
    [Cmd, Aliases, RequireContext(ContextType.Guild),
     UserPerm(GuildPermission.Administrator), OwnerOnly]
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

        var count = Service.GetAutoCommands().Where(x => x.GuildId == ctx.Guild.Id);

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
        Service.AddNewAutoCommand(cmd);

        await ReplyConfirmLocalizedAsync("autocmd_add", Format.Code(Format.Sanitize(cmdText)), cmd.Interval)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Lists the startup commands configured for the guild.
    /// </summary>
    /// <param name="page">The page number of the list to display, starting from 1.</param>
    /// <remarks>
    /// Displays a paginated list of startup commands. Each page shows up to 5 commands.
    /// Requires the user to be the owner of the bot.
    /// </remarks>
    [Cmd, Aliases, RequireContext(ContextType.Guild), OwnerOnly]
    public async Task StartupCommandsList(int page = 1)
    {
        if (page-- < 1)
            return;

        var scmds = Service.GetStartupCommands()
            .Skip(page * 5)
            .Take(5)
            .ToList();

        if (scmds.Count == 0)
        {
            await ReplyErrorLocalizedAsync("startcmdlist_none").ConfigureAwait(false);
        }
        else
        {
            var i = 0;
            await ctx.Channel.SendConfirmAsync(
                    text: string.Join("\n", scmds
                        .Select(x => $@"```css
#{++i}
[{GetText("server")}]: {(x.GuildId.HasValue ? $"{x.GuildName} #{x.GuildId}" : "-")}
[{GetText("channel")}]: {x.ChannelName} #{x.ChannelId}
[{GetText("command_text")}]: {x.CommandText}```")),
                    title: string.Empty,
                    footer: GetText("page", page + 1))
                .ConfigureAwait(false);
        }
    }


    /// <summary>
    /// Lists the auto commands configured for the guild.
    /// </summary>
    /// <param name="page">The page number of the list to display, starting from 1.</param>
    /// <remarks>
    /// Displays a paginated list of auto commands. Each page shows up to 5 commands.
    /// Requires the user to be the owner of the bot and the command to be executed in a guild context.
    /// If there are no auto commands set, an error message is displayed.
    /// </remarks>
    [Cmd, Aliases, RequireContext(ContextType.Guild), OwnerOnly]
    public async Task AutoCommandsList(int page = 1)
    {
        if (page-- < 1)
            return;

        var scmds = Service.GetAutoCommands()
            .Skip(page * 5)
            .Take(5)
            .ToList();
        if (scmds.Count == 0)
        {
            await ReplyErrorLocalizedAsync("autocmdlist_none").ConfigureAwait(false);
        }
        else
        {
            var i = 0;
            await ctx.Channel.SendConfirmAsync(
                    text: string.Join("\n", scmds
                        .Select(x => $@"```css
#{++i}
[{GetText("server")}]: {(x.GuildId.HasValue ? $"{x.GuildName} #{x.GuildId}" : "-")}
[{GetText("channel")}]: {x.ChannelName} #{x.ChannelId}
{GetIntervalText(x.Interval)}
[{GetText("command_text")}]: {x.CommandText}```")),
                    title: string.Empty,
                    footer: GetText("page", page + 1))
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Provides a formatted text displaying the interval of an auto command.
    /// </summary>
    /// <param name="interval">The interval at which the auto command executes.</param>
    /// <returns>A string representing the interval in a readable format.</returns>
    private string GetIntervalText(int interval) => $"[{GetText("interval")}]: {interval}";

    /// <summary>
    /// Executes a wait command that delays for a specified number of milliseconds.
    /// </summary>
    /// <param name="miliseconds">The number of milliseconds to delay.</param>
    /// <remarks>
    /// The command message is immediately deleted, and a new message showing the delay is sent.
    /// This message is then deleted after the delay period has passed.
    /// If the provided milliseconds value is less than or equal to 0, the command does nothing.
    /// </remarks>
    [Cmd, Aliases]
    public async Task Wait(int miliseconds)
    {
        if (miliseconds <= 0)
            return;
        ctx.Message.DeleteAfter(0);
        try
        {
            var msg = await ctx.Channel.SendConfirmAsync($"⏲ {miliseconds}ms")
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
    /// Removes an auto command based on its index.
    /// </summary>
    /// <param name="index">The one-based index of the auto command to remove.</param>
    /// <remarks>
    /// Requires the user to have Administrator permissions or to be the owner of the bot.
    /// The command will decrement the index to match zero-based indexing before attempting removal.
    /// If the removal fails, an error message is sent.
    /// </remarks>
    [Cmd, Aliases, RequireContext(ContextType.Guild),
     UserPerm(GuildPermission.Administrator), OwnerOnly]
    public async Task AutoCommandRemove([Remainder] int index)
    {
        if (!Service.RemoveAutoCommand(--index, out _))
        {
            await ReplyErrorLocalizedAsync("acrm_fail").ConfigureAwait(false);
            return;
        }

        await ctx.OkAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Removes a startup command based on its index.
    /// </summary>
    /// <param name="index">The one-based index of the startup command to remove.</param>
    /// <remarks>
    /// Requires the user to be the owner of the bot.
    /// The command will decrement the index to match zero-based indexing before attempting removal.
    /// If the removal fails, an error message is sent; otherwise, a confirmation message is sent.
    /// </remarks>
    [Cmd, Aliases, RequireContext(ContextType.Guild), OwnerOnly]
    public async Task StartupCommandRemove([Remainder] int index)
    {
        if (!Service.RemoveStartupCommand(--index, out _))
            await ReplyErrorLocalizedAsync("scrm_fail").ConfigureAwait(false);
        else
            await ReplyConfirmLocalizedAsync("scrm").ConfigureAwait(false);
    }

    /// <summary>
    /// Clears all startup commands for the guild.
    /// </summary>
    /// <remarks>
    /// Requires the user to have Administrator permissions or to be the owner of the bot.
    /// A confirmation message is sent upon successful clearance.
    /// </remarks>
    [Cmd, Aliases, RequireContext(ContextType.Guild),
     UserPerm(GuildPermission.Administrator), OwnerOnly]
    public async Task StartupCommandsClear()
    {
        Service.ClearStartupCommands();

        await ReplyConfirmLocalizedAsync("startcmds_cleared").ConfigureAwait(false);
    }

    /// <summary>
    /// Toggles the forwarding of direct messages to the bot's owner(s).
    /// </summary>
    /// <remarks>
    /// If message forwarding is enabled, it will be disabled, and vice versa.
    /// A confirmation message is sent indicating the new state of message forwarding.
    /// </remarks>
    [Cmd, Aliases]
    public async Task ForwardMessages()
    {
        var enabled = Service.ForwardMessages();

        if (enabled)
            await ReplyConfirmLocalizedAsync("fwdm_start").ConfigureAwait(false);
        else
            await ReplyConfirmLocalizedAsync("fwdm_stop").ConfigureAwait(false);
    }

    /// <summary>
    /// Toggles whether forwarded messages are sent to all of the bot's owners or just the primary owner.
    /// </summary>
    /// <remarks>
    /// If forwarding to all owners is enabled, it will be disabled, and vice versa.
    /// A confirmation message is sent indicating the new state of this setting.
    /// </remarks>
    [Cmd, Aliases]
    public async Task ForwardToAll()
    {
        var enabled = Service.ForwardToAll();

        if (enabled)
            await ReplyConfirmLocalizedAsync("fwall_start").ConfigureAwait(false);
        else
            await ReplyConfirmLocalizedAsync("fwall_stop").ConfigureAwait(false);
    }


    /// <summary>
    /// Displays statistics for all shards of the bot, including their statuses, guild counts, and user counts.
    /// </summary>
    /// <remarks>
    /// This command aggregates the current status of all shards and displays a summary followed by a detailed
    /// paginated list of each shard's status, including the time since last update, guild count, and user count.
    /// The statuses are represented by emojis for quick visual reference.
    /// </remarks>
    [Cmd, Aliases]
    public async Task ShardStats()
    {
        var statuses = coord.GetAllShardStatuses();

        // Aggregate shard status summaries
        var status = string.Join(" : ", statuses
            .Select(x => (ConnectionStateToEmoji(x), x))
            .GroupBy(x => x.Item1)
            .Select(x => $"`{x.Count()} {x.Key}`")
            .ToArray());

        // Detailed shard status for each shard
        var allShardStrings = statuses
            .Select(st =>
            {
                var stateStr = ConnectionStateToEmoji(st);
                var timeDiff = DateTime.UtcNow - st.LastUpdate;
                var maxGuildCountLength = statuses.Max(x => x.GuildCount).ToString().Length;
                return
                    $"`{stateStr} | #{st.ShardId.ToString().PadBoth(3)} | {timeDiff:mm\\:ss} | {st.GuildCount.ToString().PadBoth(maxGuildCountLength)} | {st.UserCount}`";
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
                str = GetText("no_shards_on_page");

            return new PageBuilder()
                .WithAuthor(a => a.WithName(GetText("shard_stats")))
                .WithTitle(status)
                .WithColor(Mewdeko.OkColor)
                .WithDescription(str);
        }
    }


    private static string ConnectionStateToEmoji(ShardStatus status)
    {
        var timeDiff = DateTime.UtcNow - status.LastUpdate;
        return status.ConnectionState switch
        {
            ConnectionState.Connected => "✅",
            ConnectionState.Disconnected => "🔻",
            _ when timeDiff > TimeSpan.FromSeconds(30) => " ❗ ",
            _ => " ⏳"
        };
    }

    /// <summary>
    /// Attempts to restart a specified shard by its ID.
    /// </summary>
    /// <param name="shardId">The ID of the shard to restart.</param>
    /// <remarks>
    /// Sends a confirmation message if the shard is successfully marked for restart, or an error message if the shard ID is not found.
    /// </remarks>
    [Cmd, Aliases]
    public async Task RestartShard(int shardId)
    {
        var success = coord.RestartShard(shardId);
        if (success)
            await ReplyConfirmLocalizedAsync("shard_reconnecting", Format.Bold($"#{shardId}")).ConfigureAwait(false);
        else
            await ReplyErrorLocalizedAsync("no_shard_id").ConfigureAwait(false);
    }

    /// <summary>
    /// Commands the bot to leave a server.
    /// </summary>
    /// <param name="guildStr">The identifier or name of the guild to leave.</param>
    /// <remarks>
    /// This action is irreversible through bot commands and should be used with caution.
    /// </remarks>
    [Cmd, Aliases]
    public Task LeaveServer([Remainder] string guildStr) => Service.LeaveGuild(guildStr);

    /// <summary>
    /// Initiates a shutdown of the bot.
    /// </summary>
    /// <remarks>
    /// Before shutting down, the bot attempts to send a confirmation message. Delays for a short period before triggering the shutdown sequence.
    /// </remarks>
    [Cmd, Aliases]
    public async Task Die()
    {
        try
        {
            await ReplyConfirmLocalizedAsync("shutting_down").ConfigureAwait(false);
        }
        catch
        {
            // ignored
        }

        await Task.Delay(2000).ConfigureAwait(false);
        Environment.SetEnvironmentVariable("SNIPE_CACHED", "0");
        Environment.SetEnvironmentVariable("AFK_CACHED", "0");
        coord.Die();
    }

    /// <summary>
    /// Restarts the entire bot.
    /// </summary>
    /// <remarks>
    /// Sends an error message if the restart fails, otherwise sends a confirmation message before initiating the restart.
    /// </remarks>
    [Cmd, Aliases]
    public async Task Restart()
    {
        var success = coord.RestartBot();
        if (!success)
        {
            await ReplyErrorLocalizedAsync("restart_fail").ConfigureAwait(false);
            return;
        }

        try
        {
            await ReplyConfirmLocalizedAsync("restarting").ConfigureAwait(false);
        }
        catch
        {
            // ignored
        }
    }

    /// <summary>
    /// Changes the bot's username to the specified new name.
    /// </summary>
    /// <param name="newName">The new username for the bot.</param>
    /// <remarks>
    /// Does nothing if the new name is empty or whitespace. If a change is attempted and ratelimited, logs a warning message.
    /// </remarks>
    [Cmd, Aliases]
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
            Log.Warning("You've been ratelimited. Wait 2 hours to change your name");
        }

        await ReplyConfirmLocalizedAsync("bot_name", Format.Bold(newName)).ConfigureAwait(false);
    }


    /// <summary>
    /// Sets the bot's online status.
    /// </summary>
    /// <param name="status">The new status to set.</param>
    /// <remarks>
    /// Changes the bot's presence status to one of the specified options: Online, Idle, Do Not Disturb, or Invisible.
    /// </remarks>
    [Cmd, Aliases]
    public async Task SetStatus([Remainder] SettableUserStatus status)
    {
        await client.SetStatusAsync(SettableUserStatusToUserStatus(status)).ConfigureAwait(false);

        await ReplyConfirmLocalizedAsync("bot_status", Format.Bold(status.ToString())).ConfigureAwait(false);
    }

    /// <summary>
    /// Sets the bot's avatar.
    /// </summary>
    /// <param name="img">The URL of the new avatar image. If null, the command may default to removing the current avatar or doing nothing, based on implementation.</param>
    /// <remarks>
    /// Attempts to change the bot's avatar to the image found at the specified URL. Confirmation is sent upon success.
    /// </remarks>
    [Cmd, Aliases]
    public async Task SetAvatar([Remainder] string? img = null)
    {
        var success = await Service.SetAvatar(img).ConfigureAwait(false);

        if (success)
            await ReplyConfirmLocalizedAsync("set_avatar").ConfigureAwait(false);
    }

    /// <summary>
    /// Sets the bot's currently playing game.
    /// </summary>
    /// <param name="type">The type of activity (e.g., Playing, Streaming).</param>
    /// <param name="game">The name of the game or activity. If null, might clear the current game.</param>
    /// <remarks>
    /// This method updates the bot's "Playing" status. The actual displayed status will depend on the provided activity type.
    /// </remarks>
    [Cmd, Aliases]
    public async Task SetGame(ActivityType type, [Remainder] string? game = null)
    {
        var rep = new ReplacementBuilder()
            .WithDefault(Context)
            .Build();

        await bot.SetGameAsync(game == null ? game : rep.Replace(game), type).ConfigureAwait(false);

        await ReplyConfirmLocalizedAsync("set_game").ConfigureAwait(false);
    }

    /// <summary>
    /// Sets the bot's streaming status.
    /// </summary>
    /// <param name="url">The URL of the stream.</param>
    /// <param name="name">The name of the stream. If null, might use a default name or no name.</param>
    /// <remarks>
    /// Changes the bot's activity to streaming, using the provided URL and name for the stream. Useful for when the bot is used to indicate live streams.
    /// </remarks>
    [Cmd, Aliases]
    public async Task SetStream(string url, [Remainder] string? name = null)
    {
        name ??= "";

        await client.SetGameAsync(name, url, ActivityType.Streaming).ConfigureAwait(false);

        await ReplyConfirmLocalizedAsync("set_stream").ConfigureAwait(false);
    }

    /// <summary>
    /// Sends a message to a specified channel or user.
    /// </summary>
    /// <param name="whereOrTo">The ID of the channel or user to send the message to.</param>
    /// <param name="msg">The message to send.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [Cmd, Aliases]
    public Task Send(ulong whereOrTo, [Remainder] string msg)
        => Send(whereOrTo, 0, msg);

    /// <summary>
    /// Sends a message to a specified channel or user.
    /// </summary>
    /// <param name="whereOrTo">The ID of the channel or user to send the message to.</param>
    /// <param name="to">The ID of the user to send the message to.</param>
    /// <param name="msg">The message to send.</param>
    /// <remarks>
    /// If the first ID is a server, the second ID is a channel, and the message is sent to that channel.
    /// </remarks>
    [Cmd, Aliases]
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
                await ctx.Channel.SendErrorAsync("Unable to find that user or guild! Please double check the Id!",
                        config)
                    .ConfigureAwait(false);
                return;
            }

            if (SmartEmbed.TryParse(rep.Replace(msg), ctx.Guild?.Id, out var embed, out var plainText,
                    out var components))
            {
                await potentialUser.SendMessageAsync(plainText, embeds: embed, components: components.Build())
                    .ConfigureAwait(false);
                await ctx.Channel.SendConfirmAsync($"Message sent to {potentialUser.Mention}!").ConfigureAwait(false);
                return;
            }

            await potentialUser.SendMessageAsync(rep.Replace(msg)).ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync($"Message sent to {potentialUser.Mention}!").ConfigureAwait(false);
            return;
        }

        if (to == 0)
        {
            await ctx.Channel.SendErrorAsync("You need to specify a Channel or User ID after the Server ID!", config)
                .ConfigureAwait(false);
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
                await ctx.Channel.SendConfirmAsync($"Message sent to {potentialServer} in {channel.Mention}")
                    .ConfigureAwait(false);
                return;
            }

            await channel.SendMessageAsync(rep.Replace(msg)).ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync($"Message sent to {potentialServer} in {channel.Mention}")
                .ConfigureAwait(false);
            return;
        }

        var user = await potentialServer.GetUserAsync(to).ConfigureAwait(false);
        if (user is null)
        {
            await ctx.Channel.SendErrorAsync("Unable to find that channel or user! Please check the ID and try again.",
                    config)
                .ConfigureAwait(false);
            return;
        }

        if (SmartEmbed.TryParse(rep.Replace(msg), ctx.Guild?.Id, out var embed1, out var plainText1,
                out var components1))
        {
            await channel.SendMessageAsync(plainText1, embeds: embed1, components: components1?.Build())
                .ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync($"Message sent to {potentialServer} to {user.Mention}")
                .ConfigureAwait(false);
            return;
        }

        await channel.SendMessageAsync(rep.Replace(msg)).ConfigureAwait(false);
        await ctx.Channel.SendConfirmAsync($"Message sent to {potentialServer} in {user.Mention}")
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Initiates the reloading of images used by the bot.
    /// </summary>
    /// <remarks>
    /// This command triggers a process to reload all images, ensuring that any updates to image resources are reflected without restarting the bot.
    /// A confirmation message is sent upon the start of the reload process.
    /// </remarks>
    [Cmd, Aliases]
    public async Task ImagesReload()
    {
        Service.ReloadImages();
        await ReplyConfirmLocalizedAsync("images_loading", 0).ConfigureAwait(false);
    }

    /// <summary>
    /// Initiates the reloading of bot strings (localizations).
    /// </summary>
    /// <remarks>
    /// This command triggers a process to reload all localized strings, ensuring that any updates to text resources are applied without restarting the bot.
    /// A confirmation message is sent upon successful reloading of bot strings.
    /// </remarks>
    [Cmd, Aliases]
    public async Task StringsReload()
    {
        strings.Reload();
        await ReplyConfirmLocalizedAsync("bot_strings_reloaded").ConfigureAwait(false);
    }

    private static UserStatus SettableUserStatusToUserStatus(SettableUserStatus sus) =>
        sus switch
        {
            SettableUserStatus.Online => UserStatus.Online,
            SettableUserStatus.Invisible => UserStatus.Invisible,
            SettableUserStatus.Idle => UserStatus.AFK,
            SettableUserStatus.Dnd => UserStatus.DoNotDisturb,
            _ => UserStatus.Online
        };

    /// <summary>
    /// Executes a bash command. Depending on the platform, the command is executed in either bash or PowerShell.
    /// </summary>
    /// <param name="message">The command to execute.</param>
    /// <remarks>
    /// The command is executed in a new process, and the output is sent as a paginated message. If the process hangs, it is terminated. The command has a timeout of 2 hours. The output is split into chunks of 1988 characters to avoid Discord message limits.
    /// </remarks>
    [Cmd, Aliases]
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
                    await ctx.Channel.SendMessageAsync("```The output was blank```").ConfigureAwait(false);
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
                        .WithAuthor("Bash Output")
                        .AddField("Input", message)
                        .WithDescription($"```{(isLinux ? "bash" : "powershell")}\n{stringList[page]}```");
                }
            }
            else
            {
                process.Kill();
                await ctx.Channel.SendErrorAsync("The process was hanging and has been terminated.", config)
                    .ConfigureAwait(false);
            }

            if (!process.HasExited)
            {
                await process.WaitForExitAsync().ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Evaluates a C# code snippet.
    /// </summary>
    /// <param name="code">The C# code to evaluate.</param>
    /// <remarks>
    /// The code is compiled and executed in a sandboxed environment. The result is displayed in an embed, including the return value, compilation time, and execution time.
    /// </remarks>
    /// <exception cref="ArgumentException"></exception>
    [Cmd, Aliases, OwnerOnly]
    public async Task Evaluate([Remainder] string code)
    {
        var cs1 = code.IndexOf("```", StringComparison.Ordinal) + 3;
        cs1 = code.IndexOf('\n', cs1) + 1;
        var cs2 = code.LastIndexOf("```", StringComparison.Ordinal);

        if (cs1 == -1 || cs2 == -1)
            throw new ArgumentException("You need to wrap the code into a code block.", nameof(code));

        code = code[cs1..cs2];

        var embed = new EmbedBuilder
        {
            Title = "Evaluating...", Color = new Color(0xD091B2)
        };
        var msg = await ctx.Channel.SendMessageAsync(embed: embed.Build()).ConfigureAwait(false);

        var globals = new EvaluationEnvironment((CommandContext)Context);
        var sopts = ScriptOptions.Default
            .WithImports("System", "System.Collections.Generic", "System.Diagnostics", "System.Linq",
                "System.Net.Http", "System.Net.Http.Headers", "System.Reflection", "System.Text",
                "System.Threading.Tasks", "Discord.Net", "Discord", "Discord.WebSocket", "Mewdeko.Modules",
                "Mewdeko.Services", "Mewdeko.Extensions", "Mewdeko.Modules.Administration",
                "Mewdeko.Modules.Chat_Triggers", "Mewdeko.Modules", "Mewdeko.Modules.Games",
                "Mewdeko.Modules.Help", "Mewdeko.Modules.Music", "Mewdeko.Modules.Nsfw",
                "Mewdeko.Modules.Permissions", "Mewdeko.Modules.Searches", "Mewdeko.Modules.Server_Management")
            .WithReferences(AppDomain.CurrentDomain.GetAssemblies()
                .Where(xa => !xa.IsDynamic && !string.IsNullOrWhiteSpace(xa.Location)));

        var sw1 = Stopwatch.StartNew();
        var cs = CSharpScript.Create(code, sopts, typeof(EvaluationEnvironment));
        var csc = cs.Compile();
        sw1.Stop();

        if (csc.Any(xd => xd.Severity == DiagnosticSeverity.Error))
        {
            embed = new EmbedBuilder
            {
                Title = "Compilation failed",
                Description =
                    $"Compilation failed after {sw1.ElapsedMilliseconds:#,##0}ms with {csc.Length:#,##0} errors.",
                Color = new Color(0xD091B2)
            };
            foreach (var xd in csc.Take(3))
            {
                var ls = xd.Location.GetLineSpan();
                embed.AddField($"Error at {ls.StartLinePosition.Line:#,##0}, {ls.StartLinePosition.Character:#,##0}",
                    Format.Code(xd.GetMessage()));
            }

            if (csc.Length > 3)
                embed.AddField("Some errors omitted", $"{csc.Length - 3:#,##0} more errors not displayed");
            await msg.ModifyAsync(x => x.Embed = embed.Build()).ConfigureAwait(false);
            return;
        }

        Exception rex;
        ScriptState<object> css = default;
        var sw2 = Stopwatch.StartNew();
        try
        {
            css = await cs.RunAsync(globals).ConfigureAwait(false);
            rex = css.Exception;
        }
        catch (Exception ex)
        {
            rex = ex;
        }

        sw2.Stop();

        if (rex != null)
        {
            embed = new EmbedBuilder
            {
                Title = "Execution failed",
                Description =
                    $"Execution failed after {sw2.ElapsedMilliseconds:#,##0}ms with `{rex.GetType()}: {rex.Message}`.",
                Color = new Color(0xD091B2)
            };
            await msg.ModifyAsync(x => x.Embed = embed.Build()).ConfigureAwait(false);
            return;
        }

        // execution succeeded
        embed = new EmbedBuilder
        {
            Title = "Evaluation successful", Color = new Color(0xD091B2)
        };

        embed.AddField("Result", css.ReturnValue != null ? css.ReturnValue.ToString() : "No value returned")
            .AddField("Compilation time", $"{sw1.ElapsedMilliseconds:#,##0}ms", true)
            .AddField("Execution time", $"{sw2.ElapsedMilliseconds:#,##0}ms", true);

        if (css.ReturnValue != null)
            embed.AddField("Return type", css.ReturnValue.GetType().ToString(), true);

        await msg.ModifyAsync(x => x.Embed = embed.Build()).ConfigureAwait(false);
    }
}

/// <summary>
/// Represents an environment encapsulating common entities used during command evaluation.
/// </summary>
/// <remarks>
/// This class provides quick access to frequently needed Discord entities such as the message,
/// channel, guild, user, and client related to the current command context. It's designed to
/// simplify command handling by centralizing access to these entities.
/// </remarks>
public sealed class EvaluationEnvironment
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EvaluationEnvironment"/> class with the specified command context.
    /// </summary>
    /// <param name="ctx">The command context associated with the current command execution.</param>
    public EvaluationEnvironment(CommandContext ctx)
    {
        Ctx = ctx;
    }

    /// <summary>
    /// Gets the command context associated with the current command execution.
    /// </summary>
    public CommandContext Ctx { get; }

    /// <summary>
    /// Gets the message that triggered the current command execution.
    /// </summary>
    public IUserMessage Message => Ctx.Message;

    /// <summary>
    /// Gets the channel in which the current command was executed.
    /// </summary>
    public IMessageChannel Channel => Ctx.Channel;

    /// <summary>
    /// Gets the guild in which the current command was executed. May be null for commands executed in direct messages.
    /// </summary>
    public IGuild Guild => Ctx.Guild;

    /// <summary>
    /// Gets the user who executed the current command.
    /// </summary>
    public IUser User => Ctx.User;

    /// <summary>
    /// Gets the guild member who executed the current command. This is a convenience property for accessing the user as an IGuildUser.
    /// </summary>
    public IGuildUser Member => (IGuildUser)Ctx.User;

    /// <summary>
    /// Gets the Discord client instance associated with the current command execution.
    /// </summary>
    public DiscordSocketClient Client => Ctx.Client as DiscordSocketClient;
}