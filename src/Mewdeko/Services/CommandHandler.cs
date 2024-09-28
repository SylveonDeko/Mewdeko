using System.Collections.Concurrent;
using System.Text;
using System.Threading;
using Discord.Commands;
using Discord.Interactions;
using Discord.Net;
using Discord.Rest;
using Mewdeko.Common.Collections;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Database.DbContextStuff;
using Mewdeko.Modules.Chat_Triggers.Services;
using Mewdeko.Modules.Permissions.Common;
using Mewdeko.Modules.Permissions.Services;
using Mewdeko.Services.Settings;
using Mewdeko.Services.strings;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using ExecuteResult = Discord.Commands.ExecuteResult;
using IResult = Discord.Interactions.IResult;

namespace Mewdeko.Services;

/// <summary>
/// Handles command parsing and execution, integrating with various services to process Discord interactions and messages.
/// </summary>
public class CommandHandler : INService
{
    private const int GlobalCommandsCooldown = 750;

    private const float OneThousandth = 1.0f / 1000;
    private readonly Mewdeko bot;
    private readonly BotConfigService bss;
    private readonly IDataCache cache;

    // ReSharper disable once NotAccessedField.Local
    private readonly Timer clearUsersOnShortCooldown;
    private readonly DiscordShardedClient client;
    private readonly CommandService commandService;
    private readonly IBotCredentials creds;
    private readonly DbContextProvider dbProvider;
    private readonly GuildSettingsService gss;
    private readonly InteractionService interactionService;
    private readonly IServiceProvider services;
    private readonly IBotStrings strings;

    /// <summary>
    ///     Initializes a new instance of the <see cref="CommandHandler" /> class.
    /// </summary>
    /// <param name="client">The Discord client.</param>
    /// <param name="db">The database service.</param>
    /// <param name="commandService">The service for handling commands.</param>
    /// <param name="bss">The bot configuration service.</param>
    /// <param name="bot">The bot instance.</param>
    /// <param name="services">The service provider for dependency injection.</param>
    /// <param name="strngs">The strings resources service.</param>
    /// <param name="interactionService">The service for handling interactions.</param>
    /// <param name="gss">The guild settings service.</param>
    /// <param name="eventHandler">The event handler for discord events.</param>
    /// <param name="creds">The bot credentials.</param>
    /// <param name="cache">The data cache service.</param>
    public CommandHandler(DiscordShardedClient client, DbContextProvider dbProvider, CommandService commandService,
        BotConfigService bss, Mewdeko bot, IServiceProvider services, IBotStrings strngs,
        InteractionService interactionService,
        GuildSettingsService gss, EventHandler eventHandler, IBotCredentials creds, IDataCache cache)
    {
        this.interactionService = interactionService;
        this.gss = gss;
        this.creds = creds;
        this.cache = cache;
        strings = strngs;
        this.client = client;
        this.commandService = commandService;
        this.bss = bss;
        this.bot = bot;
        this.dbProvider = dbProvider;
        this.services = services;
        eventHandler.InteractionCreated += TryRunInteraction;
        this.interactionService.SlashCommandExecuted += HandleCommands;
        this.interactionService.ContextCommandExecuted += HandleContextCommands;
        clearUsersOnShortCooldown = new Timer(_ => UsersOnShortCooldown.Clear(), null, GlobalCommandsCooldown,
            GlobalCommandsCooldown);
        eventHandler.MessageReceived += MessageReceivedHandler;
    }

    /// <summary>
    /// A thread-safe dictionary mapping channel IDs to command parse queues.
    /// </summary>
    private NonBlocking.ConcurrentDictionary<ulong, ConcurrentQueue<IUserMessage>> CommandParseQueue { get; } = new();

    /// <summary>
    /// A thread-safe dictionary indicating whether a command parse lock is active for a channel.
    /// </summary>
    private NonBlocking.ConcurrentDictionary<ulong, bool> CommandParseLock { get; } = new();

    private ConcurrentHashSet<ulong> UsersOnShortCooldown { get; } = [];

    /// <summary>
    /// Event that occurs when a command is executed.
    /// </summary>
    public event Func<IUserMessage, CommandInfo, Task> CommandExecuted = delegate { return Task.CompletedTask; };

    /// <summary>
    /// Event that occurs when a command is errored.w
    /// </summary>
    public event Func<CommandInfo, ITextChannel, string, IUser?, Task> CommandErrored = delegate
    {
        return Task.CompletedTask;
    };

    /// <summary>
    /// Used for xp, for some reason.
    /// </summary>
    public event Func<IUserMessage, Task> OnMessageNoTrigger = delegate { return Task.CompletedTask; };

    private Task HandleContextCommands(ContextCommandInfo info, IInteractionContext ctx, IResult result)
    {
        _ = Task.Run(async () =>
        {
            await using var dbContext = await dbProvider.GetContextAsync();

            if (ctx.Guild is not null)
            {
                var gconf = await gss.GetGuildConfig(ctx.Guild.Id);
                if (!gconf.StatsOptOut)
                {
                    var user = await dbContext.GetOrCreateUser(ctx.User);
                    if (!user.StatsOptOut)
                    {
                        var comStats = new CommandStats
                        {
                            ChannelId = ctx.Channel.Id,
                            GuildId = ctx.Guild.Id,
                            IsSlash = true,
                            NameOrId = info.Name,
                            UserId = ctx.User.Id,
                            Module = info.Module.Name
                        };
                        await dbContext.CommandStats.AddAsync(comStats);
                        await dbContext.SaveChangesAsync();
                    }
                }
            }

            if (!result.IsSuccess)
            {
                await ctx.Interaction
                    .SendEphemeralErrorAsync($"Command failed for the following reason:\n{result.ErrorReason}",
                        bss.Data)
                    .ConfigureAwait(false);
                Log.Warning(
                    "Slash Command Errored\n\t" + "User: {0}\n\t" + "Server: {1}\n\t" + "Channel: {2}\n\t" +
                    "Message: {3}\n\t" + "Error: {4}",
                    $"{ctx.User} [{ctx.User.Id}]", // {0}
                    ctx.Guild == null ? "PRIVATE" : $"{ctx.Guild.Name} [{ctx.Guild.Id}]", // {1}
                    ctx.Channel == null ? "PRIVATE" : $"{ctx.Channel.Name} [{ctx.Channel.Id}]", // {2}
                    info.MethodName, result.ErrorReason);
                var tofetch = await client.Rest.GetChannelAsync(bss.Data.CommandLogChannel).ConfigureAwait(false);
                if (tofetch is RestTextChannel restChannel)
                {
                    var eb = new EmbedBuilder()
                        .WithErrorColor()
                        .WithTitle("Slash Command Errored")
                        .AddField("Reason", result.ErrorReason)
                        .AddField("Module", info.Module.Name ?? "None")
                        .AddField("Command", info.Name)
                        .AddField("User", $"{ctx.User.Mention} `{ctx.User.Id}`")
                        .AddField("Channel", ctx.Channel == null ? "PRIVATE" : $"{ctx.Channel.Name} `{ctx.Channel.Id}`")
                        .AddField("Guild", ctx.Guild == null ? "PRIVATE" : $"{ctx.Guild.Name} `{ctx.Guild.Id}`");

                    await restChannel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
                }

                if (ctx.Guild is null)
                    return;
                {
                    if (info.MethodName.ToLower() is "confess" or "confessreport")
                        return;

                    var gc = await gss.GetGuildConfig(ctx.Guild.Id);
                    if (gc.CommandLogChannel is 0)
                        return;
                    var channel = await ctx.Guild.GetTextChannelAsync(gc.CommandLogChannel).ConfigureAwait(false);
                    if (channel is null)
                        return;
                    var eb = new EmbedBuilder()
                        .WithErrorColor()
                        .WithTitle("Slash Command Errored")
                        .AddField("Reason", result.ErrorReason)
                        .AddField("Module", info.Module.Name ?? "None")
                        .AddField("Command", info.Name)
                        .AddField("User", $"{ctx.User} `{ctx.User.Id}`")
                        .AddField("Channel", $"{ctx.Channel.Name} `{ctx.Channel.Id}`");

                    await channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
                }
                return;
            }

            var chan = ctx.Channel as ITextChannel;
            Log.Information(
                "Slash Command Executed" + "\n\t" + "User: {0}\n\t" + "Server: {1}\n\t" + "Channel: {2}\n\t" +
                "Module: {3}\n\t" + "Command: {4}",
                $"{ctx.User} [{ctx.User.Id}]", // {0}
                chan == null ? "PRIVATE" : $"{chan.Guild.Name} [{chan.Guild.Id}]", // {1}
                chan == null ? "PRIVATE" : $"{chan.Name} [{chan.Id}]", // {2}
                info.Module.SlashGroupName, info.MethodName); // {3}

            var tofetch1 = await client.Rest.GetChannelAsync(bss.Data.CommandLogChannel).ConfigureAwait(false);
            if (tofetch1 is RestTextChannel restChannel1)
            {
                var eb = new EmbedBuilder()
                    .WithOkColor()
                    .WithTitle("Slash Command Executed")
                    .AddField("Module", info.Module.Name ?? "None")
                    .AddField("Command", info.Name)
                    .AddField("User", $"{ctx.User} `{ctx.User.Id}`")
                    .AddField("Channel", ctx.Channel == null ? "PRIVATE" : $"{ctx.Channel.Name} `{ctx.Channel.Id}`")
                    .AddField("Guild", ctx.Guild == null ? "PRIVATE" : $"{ctx.Guild.Name} `{ctx.Guild.Id}`");

                await restChannel1.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
            }

            if (ctx.Guild is null)
                return;
            {
                if (info.MethodName.ToLower() is "confess" or "confessreport")
                    return;

                var gc = await gss.GetGuildConfig(ctx.Guild.Id);
                if (gc.CommandLogChannel is 0)
                    return;
                var channel = await ctx.Guild.GetTextChannelAsync(gc.CommandLogChannel).ConfigureAwait(false);
                if (channel is null)
                    return;
                var eb = new EmbedBuilder()
                    .WithOkColor()
                    .WithTitle("Slash Command Executed.")
                    .AddField("Module", info.Module.Name ?? "None")
                    .AddField("Command", info.Name)
                    .AddField("User", $"{ctx.User.Mention} `{ctx.User.Id}`")
                    .AddField("Channel", $"{ctx.Channel.Name} `{ctx.Channel.Id}`");

                await channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
            }
        });
        return Task.CompletedTask;
    }

    private Task HandleCommands(SlashCommandInfo slashInfo, IInteractionContext ctx, IResult result)
    {
        _ = Task.Run(async () =>
        {
            await using var dbContext = await dbProvider.GetContextAsync();

            if (ctx.Guild is not null)
            {
                var gconf = await gss.GetGuildConfig(ctx.Guild.Id);
                if (!gconf.StatsOptOut)
                {
                    var user = await dbContext.GetOrCreateUser(ctx.User);
                    if (!user.StatsOptOut)
                    {
                        var comStats = new CommandStats
                        {
                            ChannelId = ctx.Channel.Id,
                            GuildId = ctx.Guild.Id,
                            IsSlash = true,
                            NameOrId = slashInfo.Name,
                            UserId = ctx.User.Id,
                            Module = slashInfo.Module.Name
                        };
                        await dbContext.CommandStats.AddAsync(comStats);
                        await dbContext.SaveChangesAsync();
                    }
                }
            }

            if (!result.IsSuccess)
            {
                await ctx.Interaction
                    .SendEphemeralErrorAsync($"Command failed for the following reason:\n{result.ErrorReason}",
                        bss.Data)
                    .ConfigureAwait(false);
                Log.Warning(
                    "Slash Command Errored\n\t" + "User: {0}\n\t" + "Server: {1}\n\t" + "Channel: {2}\n\t" +
                    "Message: {3}\n\t" + "Error: {4}",
                    $"{ctx.User} [{ctx.User.Id}]", // {0}
                    ctx.Guild == null ? "PRIVATE" : $"{ctx.Guild.Name} [{ctx.Guild.Id}]", // {1}
                    ctx.Channel == null ? "PRIVATE" : $"{ctx.Channel.Name} [{ctx.Channel.Id}]", // {2}
                    slashInfo.MethodName, result.ErrorReason);

                var tofetch = await client.Rest.GetChannelAsync(bss.Data.CommandLogChannel).ConfigureAwait(false);
                if (tofetch is RestTextChannel restChannel)
                {
                    var eb = new EmbedBuilder()
                        .WithErrorColor()
                        .WithTitle("Slash Command Errored.")
                        .AddField("Reason", result.ErrorReason)
                        .AddField("Module", slashInfo.Module.Name ?? "None")
                        .AddField("Command", slashInfo.Name)
                        .AddField("User", $"{ctx.User.Mention} `{ctx.User.Id}`")
                        .AddField("Channel", ctx.Channel == null ? "PRIVATE" : $"{ctx.Channel.Name} `{ctx.Channel.Id}`")
                        .AddField("Guild", ctx.Guild == null ? "PRIVATE" : $"{ctx.Guild.Name} `{ctx.Guild.Id}`");

                    await restChannel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
                }

                if (ctx.Guild is null)
                    return;
                {
                    if (slashInfo.MethodName.ToLower() is "confess" or "confessreport")
                        return;

                    var gc = await gss.GetGuildConfig(ctx.Guild.Id);
                    if (gc.CommandLogChannel is 0)
                        return;
                    var channel = await ctx.Guild.GetTextChannelAsync(gc.CommandLogChannel).ConfigureAwait(false);
                    if (channel is null)
                        return;
                    var eb = new EmbedBuilder()
                        .WithErrorColor()
                        .WithTitle("Slash Command Errored.")
                        .AddField("Reason", result.ErrorReason)
                        .AddField("Module", slashInfo.Module.Name ?? "None")
                        .AddField("Command", slashInfo.Name)
                        .AddField("User", $"{ctx.User.Mention} `{ctx.User.Id}`")
                        .AddField("Channel", $"{ctx.Channel.Name} `{ctx.Channel.Id}`");

                    await channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
                }
                return;
            }

            var chan = ctx.Channel as ITextChannel;
            Log.Information(
                "Slash Command Executed" + "\n\t" + "User: {0}\n\t" + "Server: {1}\n\t" + "Channel: {2}\n\t" +
                "Module: {3}\n\t" + "Command: {4}",
                $"{ctx.User} [{ctx.User.Id}]", // {0}
                chan == null ? "PRIVATE" : $"{chan.Guild.Name} [{chan.Guild.Id}]", // {1}
                chan == null ? "PRIVATE" : $"{chan.Name} [{chan.Id}]", // {2}
                slashInfo.Module.SlashGroupName, slashInfo.MethodName); // {3}

            var tofetch1 = await client.Rest.GetChannelAsync(bss.Data.CommandLogChannel).ConfigureAwait(false);
            if (tofetch1 is RestTextChannel restChannel1)
            {
                var eb = new EmbedBuilder()
                    .WithOkColor()
                    .WithTitle("Slash Command Executed.")
                    .AddField("Module", slashInfo.Module.Name ?? "None")
                    .AddField("Command", slashInfo.Name)
                    .AddField("User", $"{ctx.User.Mention} `{ctx.User.Id}`")
                    .AddField("Channel", ctx.Channel == null ? "PRIVATE" : $"{ctx.Channel.Name} `{ctx.Channel.Id}`")
                    .AddField("Guild", ctx.Guild == null ? "PRIVATE" : $"{ctx.Guild.Name} `{ctx.Guild.Id}`");

                await restChannel1.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
            }

            if (ctx.Guild is null)
                return;
            {
                if (slashInfo.MethodName.ToLower() is "confess" or "confessreport")
                    return;

                var gc = await gss.GetGuildConfig(ctx.Guild.Id);
                if (gc.CommandLogChannel is 0)
                    return;
                var channel = await ctx.Guild.GetTextChannelAsync(gc.CommandLogChannel).ConfigureAwait(false);
                if (channel is null)
                    return;
                var eb = new EmbedBuilder()
                    .WithOkColor()
                    .WithTitle("Slash Command Executed.")
                    .AddField("Module", slashInfo.Module.Name ?? "None")
                    .AddField("Command", slashInfo.Name)
                    .AddField("User", $"{ctx.User.Mention} `{ctx.User.Id}`")
                    .AddField("Channel", $"{ctx.Channel.Name} `{ctx.Channel.Id}`");

                await channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
            }
        });
        return Task.CompletedTask;
    }

    private async Task TryRunInteraction(SocketInteraction interaction)
    {
        try
        {
            var blacklistService = services.GetService<BlacklistService>();
            var cb = new ComponentBuilder().WithButton("Support Server", null, ButtonStyle.Link,
                url: "https://discord.gg/mewdeko").Build();
            foreach (var bl in blacklistService.BlacklistEntries)
            {
                if ((interaction.Channel as IGuildChannel)?.Guild != null && bl.Type == BlacklistType.Server &&
                    bl.ItemId == (interaction.Channel as IGuildChannel)?.Guild?.Id)
                {
                    await interaction.RespondAsync(
                        $"*This guild is blacklisted from Mewdeko for **{bl.Reason}**! You can visit the support server below to try and resolve this.*",
                        components: cb).ConfigureAwait(false);
                    return;
                }

                if (bl.Type == BlacklistType.User && bl.ItemId == interaction.User.Id)
                {
                    await interaction.RespondAsync(
                        $"*You are blacklisted from Mewdeko for **{bl.Reason}**! You can visit the support server below to try and resolve this.*",
                        ephemeral: true, components: cb).ConfigureAwait(false);
                    return;
                }
            }

            if (interaction.Type == InteractionType.ApplicationCommand)
            {
                var ctS = services.GetService<ChatTriggersService>();
                var triggers = await ctS.GetChatTriggersFor((interaction.Channel as IGuildChannel)?.Guild?.Id);
                var trigger = triggers.FirstOrDefault(x => x.RealName == interaction.GetRealName());
                if (trigger is not null)
                {
                    await ctS.RunInteractionTrigger(interaction, trigger).ConfigureAwait(false);
                    return;
                }
            }
            // i hate discord
            // if (interaction is IComponentInteraction compInter
            //     && compInter.Message.Author.IsWebhook
            //     && !compInter.Data.CustomId.StartsWith("trigger.")) return;

            var ctx = new ShardedInteractionContext(client, interaction);
            var result = await interactionService.ExecuteCommandAsync(ctx, services).ConfigureAwait(false);
#if DEBUG
            Log.Information($"Button was executed:{result.IsSuccess}\nReason:{result.ErrorReason}");
#endif
        }
        catch (Exception e)
        {
            Log.Error(e, "Interaction failed to execute");
            throw;
        }
    }

    /// <summary>
    /// Executes an external command within a specific guild and channel context.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="channelId">The ID of the channel.</param>
    /// <param name="commandText">The text of the command to execute.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task ExecuteExternal(ulong? guildId, ulong channelId, string commandText)
    {
        if (guildId != null)
        {
            var guild = client.GetGuild(guildId.Value);
            if (guild?.GetChannel(channelId) is not SocketTextChannel channel)
            {
                Log.Warning("Channel for external execution not found");
                return;
            }

            try
            {
                IUserMessage msg = await channel.SendMessageAsync(commandText).ConfigureAwait(false);
                msg = (IUserMessage)await channel.GetMessageAsync(msg.Id).ConfigureAwait(false);
                await TryRunCommand(guild, channel, msg).ConfigureAwait(false);
            }
            catch
            {
                //exclude
            }
        }
    }

    private async Task MessageReceivedHandler(IMessage msg)
    {
        try
        {
            if (msg.Author.IsBot ||
                !bot.Ready.Task.IsCompleted) //no bots, wait until bot connected and initialized
            {
                return;
            }

            if (msg is not SocketUserMessage usrMsg)
                return;

            AddCommandToParseQueue(usrMsg);
            await ExecuteCommandsInChannelAsync(usrMsg.Channel.Id);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error in CommandHandler");
            if (ex.InnerException != null)
                Log.Warning(ex.InnerException, "Inner Exception of the error in CommandHandler");
        }
    }

    /// <summary>
    /// Adds a command to the parse queue for a given channel.
    /// </summary>
    /// <param name="usrMsg">The user message to add to the queue.</param>
    public void AddCommandToParseQueue(IUserMessage usrMsg) => CommandParseQueue.AddOrUpdate(usrMsg.Channel.Id,
        _ => new ConcurrentQueue<IUserMessage>(new List<IUserMessage>
        {
            usrMsg
        }), (_, y) =>
        {
            y.Enqueue(usrMsg);
            return y;
        });

    /// <summary>
    /// Attempts to execute commands in the parse queue for a given channel.
    /// </summary>
    /// <param name="channelId">The ID of the channel.</param>
    /// <returns>A task that represents the asynchronous operation, returning true if commands were executed.</returns>
    public async Task<bool> ExecuteCommandsInChannelAsync(ulong channelId)
    {
        if (CommandParseLock.GetValueOrDefault(channelId, false) ||
            CommandParseQueue.GetValueOrDefault(channelId)?.IsEmpty != false)
            return false;

        CommandParseLock[channelId] = true;
        try
        {
            while (CommandParseQueue[channelId].TryDequeue(out var msg))
            {
                try
                {
                    await TryRunCommand((msg.Channel as IGuildChannel)?.Guild, msg.Channel, msg).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    Log.Error("Error occured in the handler: {E}", e);
                }
            }

            CommandParseQueue[channelId] = new ConcurrentQueue<IUserMessage>();
            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            CommandParseLock[channelId] = false;
        }
    }

    private async Task TryRunCommand(IGuild? guild, IChannel channel, IUserMessage usrMsg)
    {
        var execTime = Environment.TickCount;

        var lateExecutors = services.GetServices<ILateExecutor>();
        var inputTransformers = services.GetServices<IInputTransformer>();
        var earlyBehaviors = services.GetServices<IEarlyBehavior>().ToArray();

        foreach (var beh in earlyBehaviors)
        {
            if (!await beh.RunBehavior(client, guild, usrMsg).ConfigureAwait(false)) continue;
            Log.Information("Executed {BehaviorType} behavior: {BehaviorName} for user: {User} in: {Guild}",
                beh.BehaviorType, beh.GetType().Name, $"{usrMsg.Author} | {usrMsg.Id}", $"{guild} | {guild.Id}");
            return;
        }

        var messageContent = usrMsg.Content;
        foreach (var exec in inputTransformers)
        {
            messageContent = await exec.TransformInput(guild, usrMsg.Channel, usrMsg.Author, messageContent).ConfigureAwait(false);
            if (messageContent != usrMsg.Content) break;
        }

        var prefix = await gss.GetPrefix(guild?.Id);
        if (prefix == null) return;

        var prefixLength = GetPrefixLength(messageContent, prefix);
        if (prefixLength == 0)
        {
            await OnMessageNoTrigger(usrMsg).ConfigureAwait(false);
            return;
        }

        var (success, error, info) = await ExecuteCommandAsync(new CommandContext(client, usrMsg),
            messageContent, prefixLength, MultiMatchHandling.Best).ConfigureAwait(false);

        execTime = Environment.TickCount - execTime;

        await UpdateCommandStats(guild, channel, usrMsg, info).ConfigureAwait(false);

        if (success)
        {
            await LogCommandExecution(usrMsg, channel as ITextChannel, info, true, execTime).ConfigureAwait(false);
            await CommandExecuted(usrMsg, info).ConfigureAwait(false);
        }
        else if (!string.IsNullOrEmpty(error))
        {
            if (info is null)
                return;

            await LogCommandExecution(usrMsg, channel as ITextChannel, info, false, execTime, error).ConfigureAwait(false);
            if (guild != null)
            {
                var permissionService = services.GetService<PermissionService>();
                var pc = await permissionService.GetCacheFor(guild.Id).ConfigureAwait(false);
                if (pc?.Permissions.CheckPermissions(usrMsg, info?.Name, info?.Module.Name, out _) ?? true)
                    await CommandErrored(info, channel as ITextChannel, error, usrMsg.Author).ConfigureAwait(false);
            }
        }

        foreach (var exec in lateExecutors)
        {
            await exec.LateExecute(client, guild, usrMsg).ConfigureAwait(false);
        }
    }


   private async Task<(bool Success, string Error, CommandInfo Info)> ExecuteCommandAsync(CommandContext context, string input, int argPos, MultiMatchHandling multiMatchHandling = MultiMatchHandling.Exception)
    {
        var searchResult = commandService.Search(context, input[argPos..]);
        if (!searchResult.IsSuccess)
            return (false, searchResult.ErrorReason, null);

        var commands = searchResult.Commands;
        var preconditionResults = await Task.WhenAll(commands.Select(async match =>
            (match, await match.Command.CheckPreconditionsAsync(context, services).ConfigureAwait(false))));

        var successfulPreconditions = preconditionResults.Where(x => x.Item2.IsSuccess).ToArray();

        if (successfulPreconditions.Length == 0)
        {
            var bestCandidate = preconditionResults
                .OrderByDescending(x => x.match.Command.Priority)
                .FirstOrDefault(x => !x.Item2.IsSuccess);
            return (false, bestCandidate.Item2.ErrorReason, commands[0].Command);
        }

        var parseResults = await Task.WhenAll(successfulPreconditions.Select(async x =>
        {
            var parseResult = await x.match.ParseAsync(context, searchResult, x.Item2, services).ConfigureAwait(false);
            return (x.match, parseResult);
        }));

        var successfulParses = parseResults
            .Where(x => x.parseResult.IsSuccess)
            .OrderByDescending(x => x.match.Command.Priority)
            .ThenByDescending(x => x.parseResult.ArgValues.Sum(y => y.Values.Sum(z => z.Score)))
            .ToArray();

        if (successfulParses.Length == 0)
        {
            var bestMatch = parseResults.FirstOrDefault(x => !x.parseResult.IsSuccess);
            return (false, bestMatch.parseResult.ErrorReason, commands[0].Command);
        }

        var cmd = successfulParses[0].match.Command;

        if (!UsersOnShortCooldown.Add(context.User.Id))
            return (false, "You are on a short cooldown.", cmd);

        var chosenOverload = successfulParses[0];
        var result = await chosenOverload.match.ExecuteAsync(context, chosenOverload.parseResult, services).ConfigureAwait(false);

        if (result is not ExecuteResult executeResult) return (result.IsSuccess, result.ErrorReason, cmd);
        if (executeResult.Exception != null && executeResult.Exception is not HttpException { DiscordCode: DiscordErrorCode.InsufficientPermissions })
        {
            Log.Warning(executeResult.Exception, "Command execution error");
        }
        return (executeResult.IsSuccess, executeResult.ErrorReason, cmd);

    }

    private async Task LogCommandExecution(IMessage usrMsg, ITextChannel channel, CommandInfo? commandInfo, bool success, int executionTime, string errorMessage = null)
    {
        var gc = await gss.GetGuildConfig(channel.GuildId);
        var logBuilder = new StringBuilder()
            .AppendLine(success ? "Command Executed" : "Command Errored")
            .AppendLine($"User: {usrMsg.Author} [{usrMsg.Author.Id}]")
            .AppendLine($"Server: {(channel == null ? "PRIVATE" : $"{channel.Guild.Name} [{channel.Guild.Id}]")}")
            .AppendLine($"Channel: {(channel == null ? "PRIVATE" : $"{channel.Name} [{channel.Id}]")}")
            .AppendLine($"Message: {usrMsg.Content}")
            .AppendLine($"Execution Time: {executionTime}ms");

        if (!success && !string.IsNullOrEmpty(errorMessage))
            logBuilder.AppendLine($"Error: {errorMessage}");

        if (success)
            Log.Information(logBuilder.ToString());
        else
            Log.Warning(logBuilder.ToString());


        var embed = new EmbedBuilder()
            .WithColor(success ? Mewdeko.OkColor : Mewdeko.ErrorColor)
            .WithTitle(success ? "Command Executed" : "Command Errored")
            .AddField("User", $"{usrMsg.Author.Mention} {usrMsg.Author} {usrMsg.Author.Id}")
            .AddField("Guild", channel == null ? "PRIVATE" : $"{channel.Guild.Name} `{channel.Guild.Id}`")
            .AddField("Channel", channel == null ? "PRIVATE" : $"{channel.Name} `{channel.Id}`")
            .AddField("Message", usrMsg.Content.TrimTo(1000))
            .AddField("Execution Time", $"{executionTime}ms");

        if (!success && !string.IsNullOrEmpty(errorMessage))
            embed.AddField("Error", errorMessage);

        if (commandInfo != null)
            embed.AddField("Command", $"{commandInfo.Module.Name} | {commandInfo.Name}");

        if (bss.Data.CommandLogChannel > 0)
        {
            if (await client.Rest.GetChannelAsync(bss.Data.CommandLogChannel) is ITextChannel logChannel)
                await logChannel.SendMessageAsync(embed: embed.Build()).ConfigureAwait(false);
        }

        if (gc.CommandLogChannel != 0)
        {
            if (await client.Rest.GetChannelAsync(gc.CommandLogChannel) is ITextChannel commandLogChannel)
                await commandLogChannel.SendMessageAsync(embed: embed.Build()).ConfigureAwait(false);
        }
    }

    private int GetPrefixLength(string content, string prefix)
    {
        // Check if content starts with the custom prefix
        if (content.StartsWith(prefix, StringComparison.InvariantCulture))
            return prefix.Length;

        // Get possible mention formats
        var mentions = new[]
        {
            client.CurrentUser.Mention,                // e.g., "@BotName"
            $"<@{client.CurrentUser.Id}>",             // e.g., "<@1234567890>"
            $"<@!{client.CurrentUser.Id}>"             // e.g., "<@!1234567890>" (for nicknames)
        };

        // Find the longest matching mention at the start
        return (from mention in mentions where content.StartsWith(mention + " ", StringComparison.InvariantCulture) select mention.Length + 1).FirstOrDefault();
    }

    private async Task UpdateCommandStats(IGuild? guild, IChannel channel, IUserMessage usrMsg, CommandInfo? info)
    {
        if (guild == null || info == null) return;

        var guildConfig = await gss.GetGuildConfig(guild.Id).ConfigureAwait(false);
        if (guildConfig.StatsOptOut) return;

        await using var dbContext = await dbProvider.GetContextAsync().ConfigureAwait(false);
        var user = await dbContext.GetOrCreateUser(usrMsg.Author).ConfigureAwait(false);
        if (user.StatsOptOut) return;

        var commandStats = new CommandStats
        {
            ChannelId = channel.Id,
            GuildId = guild.Id,
            IsSlash = false,
            NameOrId = info.Name,
            UserId = usrMsg.Author.Id,
            Module = info.Module.Name
        };
        await dbContext.CommandStats.AddAsync(commandStats).ConfigureAwait(false);
        await dbContext.SaveChangesAsync().ConfigureAwait(false);
    }
}