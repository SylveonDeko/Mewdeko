using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using Discord.Commands;
using Discord.Interactions;
using Discord.Net;
using Discord.Rest;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using LinqToDB.EntityFrameworkCore;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Common.Autocompleters;
using Mewdeko.Common.Configs;
using Mewdeko.Common.DiscordImplementations;
using Mewdeko.Common.Modals;
using Mewdeko.Database.DbContextStuff;
using Mewdeko.Modules.OwnerOnly.Services;
using Mewdeko.Services.Settings;
using Mewdeko.Services.strings;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.EntityFrameworkCore;
using Serilog;
using ContextType = Discord.Interactions.ContextType;

namespace Mewdeko.Modules.OwnerOnly;

/// <summary>
///     Initializes a new instance of the <see cref="SlashOwnerOnly" /> class, intended for slash command based owner-only
///     operations within the Mewdeko bot framework.
/// </summary>
/// <param name="client">The Discord client used to interact with the Discord API.</param>
/// <param name="strings">Provides access to localized strings within the bot.</param>
/// <param name="serv">Interactive service for handling interactive user commands.</param>
/// <param name="coord">Coordinator for managing bot operations across different services and modules.</param>
/// <param name="db">Service for database operations and access.</param>
/// <param name="cache">Cache service for storing and retrieving temporary data.</param>
/// <param name="guildSettings">Service for accessing and modifying guild-specific settings.</param>
/// <param name="commandHandler">Handler for processing and executing commands received from users.</param>
[SlashOwnerOnly]
[Discord.Interactions.Group("owneronly", "Commands only the bot owner can use")]
public class SlashOwnerOnly(
    DiscordShardedClient client,
    IBotStrings strings,
    InteractiveService serv,
    DbContextProvider dbProvider,
    IDataCache cache,
    GuildSettingsService guildSettings,
    CommandHandler commandHandler,
    BotConfig botConfigService)
    : MewdekoSlashModuleBase<OwnerOnlyService>
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
    ///     Clears the count of used GPT tokens after confirming with the user.
    /// </summary>
    /// <remarks>
    ///     This command prompts the user for confirmation before proceeding to clear the used token count.
    ///     If the user confirms, it clears the count and notifies the user of completion.
    /// </remarks>
    [SlashCommand("clearusedtokens", "Clears the used gpt tokens count")]
    public async Task ClearUsedTokens()
    {
        if (await PromptUserConfirmAsync("Are you sure you want to clear the used token count for GPT?", ctx.User.Id))
        {
            await Service.ClearUsedTokens();
            await ctx.Interaction.SendErrorAsync("Cleared.", botConfigService);
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
    [SlashCommand("sudo", "Run a command as another user")]
    public async Task Sudo([Remainder] string args, IUser user = null)
    {
        user ??= await Context.Guild.GetOwnerAsync();
        var msg = new MewdekoUserMessage
        {
            Content = $"{await guildSettings.GetPrefix(ctx.Guild)}{args}", Author = user, Channel = ctx.Channel
        };
        commandHandler.AddCommandToParseQueue(msg);
        _ = Task.Run(() => commandHandler.ExecuteCommandsInChannelAsync(ctx.Interaction.Id))
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Executes a Redis command and returns the result.
    /// </summary>
    /// <param name="command">The Redis command to execute.</param>
    /// <remarks>
    ///     This method sends the specified command to Redis through the configured cache connection.
    ///     The result of the command execution is then sent back as a message in the Discord channel.
    /// </remarks>
    [SlashCommand("redisexec", "Run a redis command")]
    public async Task RedisExec([Remainder] string command)
    {
        var result = await cache.ExecuteRedisCommand(command).ConfigureAwait(false);
        var eb = new EmbedBuilder().WithOkColor().WithTitle(result.Resp2Type.ToString())
            .WithDescription(result.ToString());
        await ctx.Interaction.RespondAsync(embed: eb.Build()).ConfigureAwait(false);
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
    [SlashCommand("sqlexec", "Run a sql command")]
    public async Task SqlExec([Remainder] string sql)
    {
        await using var dbContext = await dbProvider.GetContextAsync();

        if (!await PromptUserConfirmAsync("Are you sure you want to execute this??", ctx.User.Id).ConfigureAwait(false))
            return;

        var affected = await dbContext.Database.ExecuteSqlRawAsync(sql).ConfigureAwait(false);
        await ctx.Interaction.SendErrorAsync($"Affected {affected} rows.", botConfigService).ConfigureAwait(false);
    }

    /// <summary>
    ///     Lists all servers the bot is currently in.
    /// </summary>
    /// <remarks>
    ///     This method creates a paginated list of servers, showing server names, IDs, member counts, online member counts,
    ///     server owners, and creation dates. Pagination allows browsing through the server list if it exceeds the page limit.
    /// </remarks>
    [SlashCommand("listservers", "List all servers the bot is in")]
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

        await serv.SendPaginatorAsync(paginator, Context.Interaction, TimeSpan.FromMinutes(60)).ConfigureAwait(false);

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
    ///     Retrieves and displays statistics on the most used command, module, guild, and user.
    /// </summary>
    /// <remarks>
    ///     This method calculates and reports the top entities based on their usage count.
    ///     It displays the most frequently used command, the module that's used the most,
    ///     the user who has used commands the most, and the guild with the highest command usage.
    ///     These statistics are presented as an embed in the Discord channel.
    /// </remarks>
    [SlashCommand("commandstats", "Get stats about commands")]
    public async Task CommandStats()
    {
        await using var dbContext = await dbProvider.GetContextAsync();

        var commandStatsTable = dbContext.CommandStats;
        var topCommandTask = commandStatsTable
            .Where(x => !x.Trigger)
            .GroupBy(q => q.NameOrId)
            .Select(g => new
            {
                g.Key, Count = g.Count()
            })
            .OrderByDescending(gc => gc.Count)
            .FirstOrDefaultAsyncLinqToDB();

        var topModuleTask = commandStatsTable
            .Where(x => !x.Trigger)
            .GroupBy(q => q.Module)
            .Select(g => new
            {
                g.Key, Count = g.Count()
            })
            .OrderByDescending(gc => gc.Count)
            .FirstOrDefaultAsyncLinqToDB();

        var topGuildTask = commandStatsTable
            .Where(x => !x.Trigger)
            .GroupBy(q => q.GuildId)
            .Select(g => new
            {
                g.Key, Count = g.Count()
            })
            .OrderByDescending(gc => gc.Count)
            .FirstOrDefaultAsyncLinqToDB();

        var topUserTask = commandStatsTable
            .Where(x => !x.Trigger)
            .GroupBy(q => q.UserId)
            .Select(g => new
            {
                g.Key, Count = g.Count()
            })
            .OrderByDescending(gc => gc.Count)
            .FirstOrDefaultAsyncLinqToDB();

        await Task.WhenAll(topCommandTask, topModuleTask, topGuildTask, topUserTask);

        var topCommand = await topCommandTask;
        var topModule = await topModuleTask;
        var topGuild = await topGuildTask;
        var topUser = await topUserTask;

        var guild = await client.Rest.GetGuildAsync(topGuild.Key);
        var user = await client.Rest.GetUserAsync(topUser.Key);

        var eb = new EmbedBuilder()
            .WithOkColor()
            .AddField("Top Command", $"{topCommand.Key} was used {topCommand.Count} times!")
            .AddField("Top Module", $"{topModule.Key} was used {topModule.Count} times!")
            .AddField("Top User", $"{user} has used commands {topUser.Count} times!")
            .AddField("Top Guild", $"{guild} has used commands {topGuild.Count} times!");

        await ctx.Interaction.RespondAsync(embed: eb.Build());
    }


    /// <summary>
    ///     Displays statistics for all shards of the bot, including their statuses, guild counts, and user counts.
    /// </summary>
    /// <remarks>
    ///     This command aggregates the current status of all shards and displays a summary followed by a detailed
    ///     paginated list of each shard's status, including the time since last update, guild count, and user count.
    ///     The statuses are represented by emojis for quick visual reference.
    /// </remarks>
    [SlashCommand("shardstats", "Shows the stats for all shards")]
    public async Task ShardStats()
    {
        var statuses = client.Shards;

        // Aggregate shard status summaries
        var status = string.Join(" : ", statuses
            .Select(x => (ConnectionStateToEmoji(x.ConnectionState), x))
            .GroupBy(x => x.Item1)
            .Select(x => $"`{x.Count()} {x.Key}`")
            .ToArray());

        // Detailed shard status for each shard
        var allShardStrings = statuses
            .Select(st =>
            {
                var stateStr = ConnectionStateToEmoji(st.ConnectionState);
                var maxGuildCountLength = statuses.Max(x => x.Guilds.Count).ToString().Length;
                return
                    $"`{stateStr} | #{st.ShardId.ToString().PadBoth(3)} | {st.Guilds.Count.ToString().PadBoth(maxGuildCountLength)} | {st.Guilds.Select(x => x.Users).Count()}`";
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

    private static string ConnectionStateToEmoji(ConnectionState status)
    {
        return status switch
        {
            ConnectionState.Connected => "✅",
            ConnectionState.Disconnected => "🔻"
        };
    }


    /// <summary>
    ///     Commands the bot to leave a server.
    /// </summary>
    /// <param name="guildStr">The identifier or name of the guild to leave.</param>
    /// <remarks>
    ///     This action is irreversible through bot commands and should be used with caution.
    /// </remarks>
    [SlashCommand("leaveserver", "Leaves a server by id or name")]
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
    [SlashCommand("die", "Shuts down the bot")]
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
        Environment.Exit(0);
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
    [SlashCommand("send", "Sends a message to a server or dm")]
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
                await ctx.Interaction.SendErrorAsync("Unable to find that user or guild! Please double check the Id!",
                        botConfigService)
                    .ConfigureAwait(false);
                return;
            }

            if (SmartEmbed.TryParse(rep.Replace(msg), ctx.Guild?.Id, out var embed, out var plainText,
                    out var components))
            {
                await potentialUser.SendMessageAsync(plainText, embeds: embed, components: components.Build())
                    .ConfigureAwait(false);
                await ctx.Interaction.SendConfirmAsync($"Message sent to {potentialUser.Mention}!")
                    .ConfigureAwait(false);
                return;
            }

            await potentialUser.SendMessageAsync(rep.Replace(msg)).ConfigureAwait(false);
            await ctx.Interaction.SendConfirmAsync($"Message sent to {potentialUser.Mention}!").ConfigureAwait(false);
            return;
        }

        if (to == 0)
        {
            await ctx.Interaction.SendErrorAsync("You need to specify a Channel or User ID after the Server ID!",
                    botConfigService)
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
                await ctx.Interaction.SendConfirmAsync($"Message sent to {potentialServer} in {channel.Mention}")
                    .ConfigureAwait(false);
                return;
            }

            await channel.SendMessageAsync(rep.Replace(msg)).ConfigureAwait(false);
            await ctx.Interaction.SendConfirmAsync($"Message sent to {potentialServer} in {channel.Mention}")
                .ConfigureAwait(false);
            return;
        }

        var user = await potentialServer.GetUserAsync(to).ConfigureAwait(false);
        if (user is null)
        {
            await ctx.Interaction
                .SendErrorAsync("Unable to find that channel or user! Please check the ID and try again.",
                    botConfigService)
                .ConfigureAwait(false);
            return;
        }

        if (SmartEmbed.TryParse(rep.Replace(msg), ctx.Guild?.Id, out var embed1, out var plainText1,
                out var components1))
        {
            await channel.SendMessageAsync(plainText1, embeds: embed1, components: components1?.Build())
                .ConfigureAwait(false);
            await ctx.Interaction.SendConfirmAsync($"Message sent to {potentialServer} to {user.Mention}")
                .ConfigureAwait(false);
            return;
        }

        await channel.SendMessageAsync(rep.Replace(msg)).ConfigureAwait(false);
        await ctx.Interaction.SendConfirmAsync($"Message sent to {potentialServer} in {user.Mention}")
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Initiates the reloading of images used by the bot.
    /// </summary>
    /// <remarks>
    ///     This command triggers a process to reload all images, ensuring that any updates to image resources are reflected
    ///     without restarting the bot.
    ///     A confirmation message is sent upon the start of the reload process.
    /// </remarks>
    [SlashCommand("imagesreload", "Recaches and redownloads all images")]
    public Task ImagesReload()
    {
        Service.ReloadImages();
        return ReplyConfirmLocalizedAsync("images_loading", 0);
    }

    /// <summary>
    ///     Initiates the reloading of bot strings (localizations).
    /// </summary>
    /// <remarks>
    ///     This command triggers a process to reload all localized strings, ensuring that any updates to text resources are
    ///     applied without restarting the bot.
    ///     A confirmation message is sent upon successful reloading of bot strings.
    /// </remarks>
    [SlashCommand("stringsreload", "Reloads localized strings")]
    public Task StringsReload()
    {
        strings.Reload();
        return ReplyConfirmLocalizedAsync("bot_strings_reloaded");
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
    [SlashCommand("bash", "Executes a bash command on the host machine")]
    public async Task Bash([Remainder] string message)
    {
        await DeferAsync();
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
                    await ctx.Interaction.FollowupAsync("```The output was blank```").ConfigureAwait(false);
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

                await serv.SendPaginatorAsync(paginator, ctx.Interaction, TimeSpan.FromMinutes(60),
                        InteractionResponseType.DeferredChannelMessageWithSource)
                    .ConfigureAwait(false);

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
                await ctx.Interaction.FollowupAsync("The process was hanging and has been terminated.")
                    .ConfigureAwait(false);
            }

            if (!process.HasExited)
            {
                await process.WaitForExitAsync().ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    ///     Evaluates a C# code snippet. Launched a modal to do so.
    /// </summary>
    /// <remarks>
    ///     The code is compiled and executed in a sandboxed environment. The result is displayed in an embed, including the
    ///     return value, compilation time, and execution time.
    /// </remarks>
    /// <exception cref="ArgumentException"></exception>
    [SlashCommand("eval", "Eval C# code")]
    [OwnerOnly]
    public Task Evaluate()
    {
        return ctx.Interaction.RespondWithModalAsync<EvalModal>("evalhandle");
    }

    /// <summary>
    ///     The modal interaction handler for evaluating C# code snippets.
    /// </summary>
    /// <param name="modal">The modal itself</param>
    [ModalInteraction("evalhandle", true)]
    public async Task EvaluateModalInteraction(EvalModal modal)
    {
        await DeferAsync();

        var embed = new EmbedBuilder
        {
            Title = "Evaluating...", Color = new Color(0xD091B2)
        };
        var msg = await ctx.Interaction.FollowupAsync(embed: embed.Build()).ConfigureAwait(false);

        var globals = new InteractionEvaluationEnvironment(ctx);
        var sopts = ScriptOptions.Default
            .WithImports("System", "System.Collections.Generic", "System.Diagnostics", "System.Linq",
                "System.Net.Http", "System.Net.Http.Headers", "System.Reflection", "System.Text",
                "System.Threading.Tasks", "Discord.Net", "Discord", "Discord.WebSocket", "Mewdeko.Modules",
                "Mewdeko.Services", "Mewdeko.Extensions", "Mewdeko.Modules.Administration",
                "Mewdeko.Modules.Chat_Triggers", "Mewdeko.Modules.Gambling", "Mewdeko.Modules.Games",
                "Mewdeko.Modules.Help", "Mewdeko.Modules.Music", "Mewdeko.Modules.Nsfw",
                "Mewdeko.Modules.Permissions", "Mewdeko.Modules.Searches", "Mewdeko.Modules.Server_Management",
                "Discord.Interactions")
            .WithReferences(AppDomain.CurrentDomain.GetAssemblies()
                .Where(xa => !xa.IsDynamic && !string.IsNullOrWhiteSpace(xa.Location)));

        var sw1 = Stopwatch.StartNew();
        var cs = CSharpScript.Create(modal.Code, sopts, typeof(InteractionEvaluationEnvironment));
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

    /// <summary>
    ///     Bot configuration command for setting various bot settings.
    /// </summary>
    /// <param name="guildSettings">Service for accessing and modifying guild-specific settings.</param>
    /// <param name="commandService">Service for bot commands </param>
    /// <param name="services">Service provider for accessing various services.</param>
    /// <param name="client">The Discord client used to interact with the Discord API.</param>
    /// <param name="settingServices">Collection of services for managing bot settings.</param>
    [Discord.Interactions.Group("config", "Commands to manage various bot things")]
    public class ConfigCommands(
        GuildSettingsService guildSettings,
        CommandService commandService,
        IServiceProvider services,
        DiscordShardedClient client,
        IEnumerable<IConfigService> settingServices)
        : MewdekoSlashModuleBase<OwnerOnlyService>
    {
        /// <summary>
        ///     Sets or displays the default command prefix.
        /// </summary>
        /// <param name="prefix">The new prefix to set. If null or whitespace, the current prefix is displayed instead.</param>
        /// <remarks>
        ///     Changes the bot's command prefix for the server or displays the current prefix if no new prefix is provided.
        ///     Confirmation of the new prefix or the current prefix is sent as a reply.
        /// </remarks>
        [SlashCommand("defprefix", "Sets the default prefix for the bots text commands")]
        public async Task DefPrefix(string? prefix = null)
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
        [SlashCommand("langsetdefault", "Sets the default language for the bot")]
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
        ///     Adds a new startup command to be executed when the bot starts.
        /// </summary>
        /// <param name="cmdText">The text of the command to add, excluding the prefix.</param>
        /// <remarks>
        ///     Requires the user to have Administrator permissions or be the owner of the bot.
        ///     Commands that could potentially restart or shut down the bot are ignored for safety reasons.
        /// </remarks>
        [SlashCommand("startupcommandadd", "Adds a command to run in the current channel on startup")]
        [Discord.Interactions.RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task StartupCommandAdd([Remainder] string cmdText)
        {
            if (cmdText.StartsWith($"{await guildSettings.GetPrefix(ctx.Guild)}die",
                    StringComparison.InvariantCulture) ||
                cmdText.StartsWith($"{await guildSettings.GetPrefix(ctx.Guild)}restart",
                    StringComparison.InvariantCulture))
                return;

            var guser = (IGuildUser)ctx.User;
            var cmd = new AutoCommand
            {
                CommandText = cmdText,
                ChannelId = ctx.Interaction.Id,
                ChannelName = ctx.Channel.Name,
                GuildId = ctx.Guild?.Id,
                GuildName = ctx.Guild?.Name,
                VoiceChannelId = guser.VoiceChannel?.Id,
                VoiceChannelName = guser.VoiceChannel?.Name,
                Interval = 0
            };
            Service.AddNewAutoCommand(cmd);

            await ctx.Interaction.RespondAsync(embed: new EmbedBuilder().WithOkColor()
                .WithTitle(GetText("scadd"))
                .AddField(efb => efb.WithName(GetText("server"))
                    .WithValue(cmd.GuildId == null ? "-" : $"{cmd.GuildName}/{cmd.GuildId}").WithIsInline(true))
                .AddField(efb => efb.WithName(GetText("channel"))
                    .WithValue($"{cmd.ChannelName}/{cmd.ChannelId}").WithIsInline(true))
                .AddField(efb => efb.WithName(GetText("command_text"))
                    .WithValue(cmdText).WithIsInline(false)).Build()).ConfigureAwait(false);
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
        [SlashCommand("autocommandadd", "Adds a command to run at a set interval")]
        [Discord.Interactions.RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task AutoCommandAdd(int interval, [Remainder] string cmdText)
        {
            if (cmdText.StartsWith($"{await guildSettings.GetPrefix(ctx.Guild)}die", StringComparison.InvariantCulture))
                return;
            var command =
                commandService.Search(cmdText.Replace(await guildSettings.GetPrefix(ctx.Guild), "").Split(" ")[0]);
            if (!command.IsSuccess)
                return;

            var currentContext = new CommandContext(ctx.Client as DiscordShardedClient, new MewdekoUserMessage
            {
                Content = "HI!", Author = ctx.User, Channel = ctx.Channel
            });

            foreach (var i in command.Commands)
            {
                if (!(await i.CheckPreconditionsAsync(currentContext, services).ConfigureAwait(false)).IsSuccess)
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
                ChannelId = ctx.Interaction.Id,
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
        ///     Lists the startup commands configured for the guild.
        /// </summary>
        /// <param name="page">The page number of the list to display, starting from 1.</param>
        /// <remarks>
        ///     Displays a paginated list of startup commands. Each page shows up to 5 commands.
        ///     Requires the user to be the owner of the bot.
        /// </remarks>
        [SlashCommand("startupcommandslist", "Lists the current startup commands")]
        [Discord.Interactions.RequireContext(ContextType.Guild)]
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
                await ReplyErrorLocalizedAsync("startcmdlist_none").ConfigureAwait(false);
            }
            else
            {
                var i = 0;
                await ctx.Interaction.SendConfirmAsync(
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
        ///     Lists the auto commands configured for the guild.
        /// </summary>
        /// <param name="page">The page number of the list to display, starting from 1.</param>
        /// <remarks>
        ///     Displays a paginated list of auto commands. Each page shows up to 5 commands.
        ///     Requires the user to be the owner of the bot and the command to be executed in a guild context.
        ///     If there are no auto commands set, an error message is displayed.
        /// </remarks>
        [SlashCommand("autocommandslist", "Lists all auto commands")]
        [Discord.Interactions.RequireContext(ContextType.Guild)]
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
                await ReplyErrorLocalizedAsync("autocmdlist_none").ConfigureAwait(false);
            }
            else
            {
                var i = 0;
                await ctx.Interaction.SendConfirmAsync(
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

        private string GetIntervalText(int interval)
        {
            return $"[{GetText("interval")}]: {interval}";
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
        [SlashCommand("autocommandremove", "Removes an auto command")]
        [Discord.Interactions.RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task AutoCommandRemove([Remainder] int index)
        {
            if (!await Service.RemoveAutoCommand(--index))
            {
                await ReplyErrorLocalizedAsync("acrm_fail").ConfigureAwait(false);
                return;
            }

            await ctx.Interaction.SendConfirmAsync("Auto Command Removed.").ConfigureAwait(false);
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
        [SlashCommand("startupcommandremove", "Removes a startup command")]
        [Discord.Interactions.RequireContext(ContextType.Guild)]
        public async Task StartupCommandRemove([Remainder] int index)
        {
            if (!await Service.RemoveStartupCommand(--index))
            {
                await ReplyErrorLocalizedAsync("scrm_fail");
                return;
            }

            await ReplyConfirmLocalizedAsync("scrm");
        }

        /// <summary>
        ///     Clears all startup commands for the guild.
        /// </summary>
        /// <remarks>
        ///     Requires the user to have Administrator permissions or to be the owner of the bot.
        ///     A confirmation message is sent upon successful clearance.
        /// </remarks>
        [SlashCommand("startupcommandsclear", "Clears all startup commands")]
        [Discord.Interactions.RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task StartupCommandsClear()
        {
            await Service.ClearStartupCommands();

            await ReplyConfirmLocalizedAsync("startcmds_cleared");
        }

        /// <summary>
        ///     Toggles the forwarding of direct messages to the bot's owner(s).
        /// </summary>
        /// <remarks>
        ///     If message forwarding is enabled, it will be disabled, and vice versa.
        ///     A confirmation message is sent indicating the new state of message forwarding.
        /// </remarks>
        [SlashCommand("forwardmessages", "Toggles whether to forward dms to the bot to owner dms")]
        public Task ForwardMessages()
        {
            var enabled = Service.ForwardMessages();

            if (enabled)
                return ReplyConfirmLocalizedAsync("fwdm_start");
            return ReplyConfirmLocalizedAsync("fwdm_stop");
        }

        /// <summary>
        ///     Toggles whether forwarded messages are sent to all of the bot's owners or just the primary owner.
        /// </summary>
        /// <remarks>
        ///     If forwarding to all owners is enabled, it will be disabled, and vice versa.
        ///     A confirmation message is sent indicating the new state of this setting.
        /// </remarks>
        [SlashCommand("forwardtoall", "Toggles whether to forward dms to the bot to all bot owners")]
        public Task ForwardToAll()
        {
            var enabled = Service.ForwardToAll();

            if (enabled)
                return ReplyConfirmLocalizedAsync("fwall_start");
            return ReplyConfirmLocalizedAsync("fwall_stop");
        }

        /// <summary>
        ///     Changes the bot's username to the specified new name.
        /// </summary>
        /// <param name="newName">The new username for the bot.</param>
        /// <remarks>
        ///     Does nothing if the new name is empty or whitespace. If a change is attempted and ratelimited, logs a warning
        ///     message.
        /// </remarks>
        [SlashCommand("setname", "Sets the bots name")]
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
        ///     Sets the bot's avatar.
        /// </summary>
        /// <param name="img">
        ///     The URL of the new avatar image. If null, the command may default to removing the current avatar or
        ///     doing nothing, based on implementation.
        /// </param>
        /// <remarks>
        ///     Attempts to change the bot's avatar to the image found at the specified URL. Confirmation is sent upon success.
        /// </remarks>
        [SlashCommand("setavatar", "Sets the bots avatar")]
        public async Task SetAvatar([Remainder] string? img = null)
        {
            var success = await Service.SetAvatar(img).ConfigureAwait(false);

            if (success)
                await ReplyConfirmLocalizedAsync("set_avatar").ConfigureAwait(false);
        }

        /// <summary>
        ///     Changes yml based config for the bot.
        /// </summary>
        /// <param name="name">The name of the config to change.</param>
        /// <param name="prop">The property of the config to change.</param>
        /// <param name="value">The new value to set for the property.</param>
        [SlashCommand("botconfig", "Config various bot settings")]
        public async Task BotConfig([Autocomplete(typeof(SettingsServiceNameAutoCompleter))] string? name = null,
            [Autocomplete(typeof(SettingsServicePropAutoCompleter))]
            string? prop = null, [Remainder] string? value = null)
        {
            await DeferAsync();
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

                    await ctx.Interaction.FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
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

                    await ctx.Interaction.FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
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

                    await ctx.Interaction.FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
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

                    await ctx.Interaction.FollowupAsync(embed: propErrorEmbed.Build()).ConfigureAwait(false);
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

                    await ctx.Interaction.FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
                    return;
                }

                var success = setting.SetSetting(prop, value);

                if (!success)
                {
                    await ReplyErrorLocalizedAsync("config_edit_fail", Format.Code(prop), Format.Code(value))
                        .ConfigureAwait(false);
                    return;
                }

                await ctx.Interaction.SendConfirmFollowupAsync("Config updated!");
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                await ctx.Interaction.SendErrorFollowupAsync(
                    "There was an error setting or printing the config, please check the logs.", new BotConfig());
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
    }

    /// <summary>
    ///     Commands to manage the bot's status and presence.
    /// </summary>
    /// <param name="bot">The bot instance to manage.</param>
    /// <param name="client">The Discord client used to interact with the Discord API.</param>
    [Discord.Interactions.Group("statuscommands", "Commands to manage bot status")]
    public class StatusCommands(Mewdeko bot, DiscordShardedClient client) : MewdekoSlashModuleBase<OwnerOnlyService>
    {
        /// <summary>
        ///     Toggles the rotation of playing statuses for the bot.
        /// </summary>
        /// <remarks>
        ///     If rotation is enabled, it will be disabled, and vice versa. Confirmation of the action is sent as a reply.
        /// </remarks>
        [SlashCommand("rotateplaying", "Toggles rotating playing status")]
        public Task RotatePlaying()
        {
            if (Service.ToggleRotatePlaying())
                return ReplyConfirmLocalizedAsync("ropl_enabled");
            return ReplyConfirmLocalizedAsync("ropl_disabled");
        }

        /// <summary>
        ///     Adds a new status to the rotation of playing statuses for the bot.
        /// </summary>
        /// <param name="t">The type of activity (e.g., Playing, Streaming).</param>
        /// <param name="status">The text of the status to add.</param>
        /// <remarks>
        ///     Adds a new status with the specified activity type and text. Confirmation of addition is sent as a reply.
        /// </remarks>
        [SlashCommand("addplaying", "Adds a playing status to the rotating status list")]
        public async Task AddPlaying(ActivityType t, [Remainder] string status)
        {
            await Service.AddPlaying(t, status).ConfigureAwait(false);

            await ReplyConfirmLocalizedAsync("ropl_added").ConfigureAwait(false);
        }

        /// <summary>
        ///     Lists all statuses currently in the rotation.
        /// </summary>
        /// <remarks>
        ///     Sends a reply with a numbered list of all statuses in the rotation. If no statuses are set, sends an error message.
        /// </remarks>
        [SlashCommand("listplaying", "Lists all rotating statuses")]
        public async Task ListPlaying()
        {
            var statuses = await Service.GetRotatingStatuses();

            if (statuses.Count == 0)
            {
                await ReplyErrorLocalizedAsync("ropl_not_set");
            }

            var i = 1;
            await ReplyConfirmLocalizedAsync("ropl_list",
                string.Join("\n\t", statuses.Select(rs => $"`{i++}.` *{rs.Type}* {rs.Status}")));
        }

        /// <summary>
        ///     Removes a status from the rotating playing statuses by its index.
        /// </summary>
        /// <param name="index">The one-based index of the status to remove. The actual removal will use zero-based indexing.</param>
        /// <remarks>
        ///     If the status at the provided index exists, it will be removed, and a confirmation message is sent.
        /// </remarks>
        [SlashCommand("removeplaying", "Removes a status from the rotating status list")]
        public async Task RemovePlaying(int index)
        {
            index--;

            var msg = await Service.RemovePlayingAsync(index).ConfigureAwait(false);

            if (msg == null)
                return;

            await ReplyConfirmLocalizedAsync("reprm", msg).ConfigureAwait(false);
        }

        /// <summary>
        ///     Sets the bot's online status.
        /// </summary>
        /// <param name="status">The new status to set.</param>
        /// <remarks>
        ///     Changes the bot's presence status to one of the specified options: Online, Idle, Do Not Disturb, or Invisible.
        /// </remarks>
        [SlashCommand("setstatus", "Sets the bots status (DND, Offline, etc)")]
        public async Task SetStatus([Remainder] SettableUserStatus status)
        {
            await client.SetStatusAsync(SettableUserStatusToUserStatus(status)).ConfigureAwait(false);

            await ReplyConfirmLocalizedAsync("bot_status", Format.Bold(status.ToString())).ConfigureAwait(false);
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
        [SlashCommand("setgame", "Sets the bots now playing. Disabled rotating status")]
        public async Task SetGame(ActivityType type, [Remainder] string? game = null)
        {
            var rep = new ReplacementBuilder()
                .WithDefault(Context)
                .Build();

            await bot.SetGameAsync(game == null ? game : rep.Replace(game), type).ConfigureAwait(false);

            await ReplyConfirmLocalizedAsync("set_game").ConfigureAwait(false);
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
        [SlashCommand("setstream", "Sets the stream url (such as Twitch)")]
        public async Task SetStream(string url, [Remainder] string? name = null)
        {
            name ??= "";

            await client.SetGameAsync(name, url, ActivityType.Streaming).ConfigureAwait(false);

            await ReplyConfirmLocalizedAsync("set_stream").ConfigureAwait(false);
        }
    }
}

/// <summary>
///     Represents an environment encapsulating common entities used during the evaluation of Discord interactions.
/// </summary>
/// <remarks>
///     This class provides streamlined access to frequently needed Discord entities such as the interaction,
///     channel, guild, user, and client associated with a specific interaction context. It simplifies handling
///     of interactions by centralizing access to these entities, making it easier to develop interaction-based commands.
/// </remarks>
public sealed class InteractionEvaluationEnvironment
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="InteractionEvaluationEnvironment" /> class with the specified
    ///     interaction context.
    /// </summary>
    /// <param name="ctx">The interaction context associated with the current interaction handling.</param>
    public InteractionEvaluationEnvironment(IInteractionContext ctx)
    {
        Ctx = ctx;
    }

    /// <summary>
    ///     Gets the interaction context associated with the current interaction handling.
    /// </summary>
    public IInteractionContext Ctx { get; }

    /// <summary>
    ///     Gets the interaction that triggered the current handling.
    /// </summary>
    public IDiscordInteraction Interaction
    {
        get
        {
            return Ctx.Interaction;
        }
    }

    /// <summary>
    ///     Gets the channel in which the current interaction was triggered.
    /// </summary>
    public IMessageChannel Channel
    {
        get
        {
            return Ctx.Channel;
        }
    }

    /// <summary>
    ///     Gets the guild in which the current interaction was triggered. May be null for interactions in direct messages.
    /// </summary>
    public IGuild Guild
    {
        get
        {
            return Ctx.Guild;
        }
    }

    /// <summary>
    ///     Gets the user who initiated the current interaction.
    /// </summary>
    public IUser User
    {
        get
        {
            return Ctx.User;
        }
    }

    /// <summary>
    ///     Gets the guild member who initiated the current interaction. This is a convenience property for accessing the user
    ///     as an IGuildUser.
    /// </summary>
    public IGuildUser Member
    {
        get
        {
            return (IGuildUser)Ctx.User;
        }
    }

    /// <summary>
    ///     Gets the Discord client instance associated with the current interaction handling.
    /// </summary>
    public DiscordShardedClient Client
    {
        get
        {
            return Ctx.Client as DiscordShardedClient;
        }
    }
}