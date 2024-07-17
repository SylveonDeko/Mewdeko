using System.Collections.Concurrent;
using System.Collections.Immutable;
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
using ParseResult = Discord.Commands.ParseResult;
using PreconditionResult = Discord.Commands.PreconditionResult;

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

    private ConcurrentHashSet<ulong> UsersOnShortCooldown { get; } = new();

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

    private async Task LogSuccessfulExecution(IMessage usrMsg, IGuildChannel? channel, params int[] execPoints)
    {
        Log.Information(
            "Command Executed after "
            + string.Join("/", execPoints.Select(x => (x * OneThousandth).ToString("F3")))
            + "s\n\t"
            + "User: {0}\n\t"
            + "Server: {1}\n\t"
            + "Channel: {2}\n\t"
            + "Message: {3}", $"{usrMsg.Author} [{usrMsg.Author.Id}]", // {0}
            channel == null ? "PRIVATE" : $"{channel.Guild.Name} [{channel.Guild.Id}]", // {1}
            channel == null ? "PRIVATE" : $"{channel.Name} [{channel.Id}]", // {2}
            usrMsg.Content); // {3}
        if (bss.Data.CommandLogChannel < 1)
            return;

        var toFetch = await client.Rest.GetChannelAsync(bss.Data.CommandLogChannel).ConfigureAwait(false);
        if (toFetch is RestTextChannel restChannel)
        {
            var eb = new EmbedBuilder()
                .WithOkColor()
                .WithTitle("Text Command Executed")
                .AddField("Executed Time",
                    string.Join("/", execPoints.Select(x => (x * OneThousandth).ToString("F3"))))
                .AddField("User", $"{usrMsg.Author.Mention} {usrMsg.Author} {usrMsg.Author.Id}")
                .AddField("Guild", channel == null ? "PRIVATE" : $"{channel.Guild.Name} `{channel.Guild.Id}`")
                .AddField("Channel", channel == null ? "PRIVATE" : $"{channel.Name} `{channel.Id}`")
                .AddField("Message", usrMsg.Content.TrimTo(1000));

            await restChannel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
        }

        if (channel?.Guild is null)
            return;
        var guildChannel = (await gss.GetGuildConfig(channel.Guild.Id)).CommandLogChannel;
        if (guildChannel == 0)
            return;
        var toSend = await client.Rest.GetChannelAsync(guildChannel).ConfigureAwait(false);
        if (toSend is RestTextChannel restTextChannel)
        {
            var eb = new EmbedBuilder()
                .WithOkColor()
                .WithTitle("Text Command Executed")
                .AddField("Executed Time",
                    string.Join("/", execPoints.Select(x => (x * OneThousandth).ToString("F3"))))
                .AddField("User", $"{usrMsg.Author.Mention} {usrMsg.Author} {usrMsg.Author.Id}")
                .AddField("Channel", channel == null ? "PRIVATE" : $"{channel.Name} `{channel.Id}`")
                .AddField("Message", usrMsg.Content.TrimTo(1000));

            await restTextChannel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
        }
    }

    private async Task LogErroredExecution(string errorMessage, IMessage usrMsg, IGuildChannel? channel,
        params int[] execPoints)
    {
        var errorafter = string.Join("/", execPoints.Select(x => (x * OneThousandth).ToString("F3")));
        Log.Warning(
            $"Command Errored after {errorafter}\n\t" + "User: {0}\n\t" + "Server: {1}\n\t" + "Channel: {2}\n\t" +
            "Message: {3}\n\t" + "Error: {4}",
            $"{usrMsg.Author} [{usrMsg.Author.Id}]", // {0}
            channel == null ? "PRIVATE" : $"{channel.Guild.Name} [{channel.Guild.Id}]", // {1}
            channel == null ? "PRIVATE" : $"{channel.Name} [{channel.Id}]", // {2}
            usrMsg.Content, errorMessage);

        if (bss.Data.CommandLogChannel < 1)
            return;

        var toFetch = await client.Rest.GetChannelAsync(bss.Data.CommandLogChannel).ConfigureAwait(false);
        if (toFetch is RestTextChannel restChannel)
        {
            var eb = new EmbedBuilder().WithOkColor().WithTitle("Text Command Errored")
                .AddField("Error Reason", errorMessage)
                .AddField("Errored Time", execPoints.Select(x => (x * OneThousandth).ToString("F3")))
                .AddField("User", $"{usrMsg.Author} {usrMsg.Author.Id}")
                .AddField("Guild", channel == null ? "PRIVATE" : $"{channel.Guild.Name} `{channel.Guild.Id}`")
                .AddField("Channel", channel == null ? "PRIVATE" : $"{channel.Name} `{channel.Id}`")
                .AddField("Message", usrMsg.Content.TrimTo(1000));

            await restChannel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
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

        var earlyBehaviors = services.GetServices<IEarlyBehavior>()
            .ToArray();

        foreach (var beh in earlyBehaviors)
        {
            if (!await beh.RunBehavior(client, guild, usrMsg).ConfigureAwait(false))
                continue;

            switch (beh.BehaviorType)
            {
                case ModuleBehaviorType.Blocker:
                    Log.Information("Blocked User: [{0}] Message: [{1}] Service: [{2}]",
                        $"{usrMsg.Author} | {usrMsg.Author.Id}", usrMsg.Content, beh.GetType().Name);
                    break;
                case ModuleBehaviorType.Executor:
                    Log.Information("User [{0}] executed [{1}] in [{2}] User ID: {3}", usrMsg.Author, usrMsg.Content,
                        beh.GetType().Name, usrMsg.Author.Id);
                    break;
            }

            return;
        }

        var exec2 = Environment.TickCount - execTime;

        var messageContent = usrMsg.Content;
        foreach (var exec in inputTransformers)
        {
            var newContent = await exec.TransformInput(guild, usrMsg.Channel, usrMsg.Author, messageContent)
                .ConfigureAwait(false);

            if (newContent.Equals(messageContent, StringComparison.OrdinalIgnoreCase))
                continue;
            messageContent = newContent;
            break;
        }

        var prefix = await gss.GetPrefix(guild?.Id);

        if (prefix is null /*somehow*/)
            return;

        var startsWithPrefix = messageContent.StartsWith(prefix, StringComparison.InvariantCulture);
        var startsWithBotMention =
            messageContent.StartsWith($"<@{client.CurrentUser.Id}> ", StringComparison.InvariantCulture) ||
            messageContent.StartsWith($"<@!{client.CurrentUser.Id}> ", StringComparison.InvariantCulture);

        if (!startsWithPrefix && !startsWithBotMention)
        {
            await OnMessageNoTrigger(usrMsg).ConfigureAwait(false);
            return;
        }

        if (startsWithBotMention)
        {
            prefix = messageContent.IndexOf('!') == -1
                ? $"<@{client.CurrentUser.Id}> "
                : $"<@!{client.CurrentUser.Id}> ";
        }

        if (messageContent.Equals(prefix.Trim(), StringComparison.InvariantCulture))
        {
            return;
        }

        var (success, error, info) = await ExecuteCommandAsync(new CommandContext(client, usrMsg),
                messageContent, prefix.Length, MultiMatchHandling.Best)
            .ConfigureAwait(false);

        execTime = Environment.TickCount - execTime;

        if (guild is not null)
        {
            var gconf = await gss.GetGuildConfig(guild.Id);
            if (!gconf.StatsOptOut && info is not null)
            {
                await using var dbContext = await dbProvider.GetContextAsync();

                var user = await dbContext.GetOrCreateUser(usrMsg.Author);
                if (!user.StatsOptOut)
                {
                    var comStats = new CommandStats
                    {
                        ChannelId = channel.Id,
                        GuildId = guild.Id,
                        NameOrId = info.Name,
                        UserId = usrMsg.Author.Id,
                        Module = info.Module.Name
                    };
                    await dbContext.CommandStats.AddAsync(comStats);
                    await dbContext.SaveChangesAsync();
                }
            }
        }

        if (success)
        {
            await LogSuccessfulExecution(usrMsg, channel as ITextChannel, exec2, execTime).ConfigureAwait(false);
            await CommandExecuted(usrMsg, info).ConfigureAwait(false);
            return;
        }

        if (!string.IsNullOrEmpty(error))
        {
            await LogErroredExecution(error, usrMsg, channel as ITextChannel, exec2, execTime);
            if (guild != null)
            {
                var perms = new PermissionService(dbProvider, strings, gss, client, bss.Data);
                var pc = await perms.GetCacheFor(guild.Id);
                if (pc != null && pc.Permissions.CheckPermissions(usrMsg, info.Name, info.Module.Name, out _))
                    await CommandErrored(info, channel as ITextChannel, error, usrMsg.Author).ConfigureAwait(false);
                if (pc == null)
                    await CommandErrored(info, channel as ITextChannel, error, usrMsg.Author).ConfigureAwait(false);
            }
        }

        foreach (var exec in lateExecutors)
        {
            await exec.LateExecute(client, guild, usrMsg).ConfigureAwait(false);
        }
    }


    private Task<(bool Success, string Error, CommandInfo Info)> ExecuteCommandAsync(CommandContext context,
        string input, int argPos,
        MultiMatchHandling multiMatchHandling = MultiMatchHandling.Exception) =>
        ExecuteCommand(context, input[argPos..], multiMatchHandling);

    private async Task<(bool Success, string Error, CommandInfo Info)> ExecuteCommand(CommandContext context,
        string input,
        MultiMatchHandling multiMatchHandling = MultiMatchHandling.Exception)
    {
        var lateBlockers = services.GetServices<ILateBlocker>()
            .OrderByDescending(x => x.Priority);
        var searchResult = commandService.Search(context, input);
        if (!searchResult.IsSuccess)
            return (false, null, null);

        var commands = searchResult.Commands;
        var preconditionResults = new Dictionary<CommandMatch, PreconditionResult>();

        foreach (var match in commands)
        {
            preconditionResults[match] =
                await match.Command.CheckPreconditionsAsync(context, services).ConfigureAwait(false);
        }

        var successfulPreconditions = preconditionResults
            .Where(x => x.Value.IsSuccess)
            .ToArray();

        if (successfulPreconditions.Length == 0)
        {
            //All preconditions failed, return the one from the highest priority command
            var bestCandidate = preconditionResults
                .OrderByDescending(x => x.Key.Command.Priority)
                .FirstOrDefault(x => !x.Value.IsSuccess);
            return (false, bestCandidate.Value.ErrorReason, commands[0].Command);
        }

        var parseResultsDict = new Dictionary<CommandMatch, ParseResult>();
        foreach (var pair in successfulPreconditions)
        {
            var parseResult = await pair.Key.ParseAsync(context, searchResult, pair.Value, services)
                .ConfigureAwait(false);

            if (parseResult.Error == CommandError.MultipleMatches)
            {
                switch (multiMatchHandling)
                {
                    case MultiMatchHandling.Best:
                        IReadOnlyList<TypeReaderValue> argList = parseResult.ArgValues
                            .Select(x => x.Values.MaxBy(y => y.Score)).ToImmutableArray();
                        IReadOnlyList<TypeReaderValue> paramList = parseResult.ParamValues
                            .Select(x => x.Values.MaxBy(y => y.Score)).ToImmutableArray();
                        parseResult = ParseResult.FromSuccess(argList, paramList);
                        break;
                }
            }

            parseResultsDict[pair.Key] = parseResult;
        }

        //Order the parse results by their score so that we choose the most likely result to execute
        var parseResults = parseResultsDict
            .OrderByDescending(x => CalculateScore(x.Key, x.Value));

        var successfulParses = parseResults
            .Where(x => x.Value.IsSuccess)
            .ToArray();

        if (successfulParses.Length == 0)
        {
            //All parses failed, return the one from the highest priority command, using score as a tie breaker
            var bestMatch = parseResults
                .FirstOrDefault(x => !x.Value.IsSuccess);
            return (false, bestMatch.Value.ErrorReason, commands[0].Command);
        }

        var cmd = successfulParses[0].Key.Command;

        // Bot will ignore commands which are ran more often than what specified by
        // GlobalCommandsCooldown constant (milliseconds)
        if (!UsersOnShortCooldown.Add(context.Message.Author.Id))
            return (false, null, cmd);

        var commandName = cmd.Aliases[0];
        foreach (var exec in lateBlockers)
        {
            if (!await exec.TryBlockLate(client, context, cmd.Module.GetTopLevelModule().Name, cmd)
                    .ConfigureAwait(false)) continue;
            Log.Information("Late blocking User [{0}] Command: [{1}] in [{2}]", context.User, commandName,
                exec.GetType().Name);
            return (false, null, cmd);
        }

        //If we get this far, at least one parse was successful. Execute the most likely overload.
        var chosenOverload = successfulParses[0];
        var execResult = (ExecuteResult)await chosenOverload.Key
            .ExecuteAsync(context, chosenOverload.Value, services).ConfigureAwait(false);

        if (execResult.Exception != null &&
            (execResult.Exception is not HttpException he ||
             he.DiscordCode == DiscordErrorCode.InsufficientPermissions))
        {
            Log.Warning(execResult.Exception, "Command Error");
        }

        return (true, null, cmd);

        // Calculates the 'score' of a command given a parse result
        static float CalculateScore(CommandMatch match, ParseResult parseResult)
        {
            float argValuesScore = 0, paramValuesScore = 0;

            if (match.Command.Parameters.Count > 0)
            {
                var argValuesSum =
                    parseResult.ArgValues?.Sum(x =>
                        x.Values.OrderByDescending(y => y.Score).FirstOrDefault().Score) ?? 0;
                var paramValuesSum = parseResult.ParamValues?.Sum(x =>
                    x.Values.OrderByDescending(y => y.Score).FirstOrDefault().Score) ?? 0;

                argValuesScore = argValuesSum / match.Command.Parameters.Count;
                paramValuesScore = paramValuesSum / match.Command.Parameters.Count;
            }

            var totalArgsScore = (argValuesScore + paramValuesScore) / 2;
            return match.Command.Priority + (totalArgsScore * 0.99f);
        }
    }
}