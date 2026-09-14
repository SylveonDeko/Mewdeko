using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Threading;
using DataModel;
using Discord.Commands;
using Discord.Interactions;
using Discord.Net;
using Discord.Rest;
using Fergun.Interactive;
using LinqToDB;
using Mewdeko.Common.Collections;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Database.Enums;
using Mewdeko.Modules.Chat_Triggers.Services;
using Mewdeko.Modules.Help.Services;
using Mewdeko.Modules.Permissions.Common;
using Mewdeko.Modules.Permissions.Services;
using Mewdeko.Services.Analytics;
using Mewdeko.Services.Impl;
using Mewdeko.Services.Settings;
using Mewdeko.Services.Strings;
using Microsoft.Extensions.DependencyInjection;
using ExecuteResult = Discord.Commands.ExecuteResult;
using IResult = Discord.Interactions.IResult;

namespace Mewdeko.Services;

/// <summary>
///     Handles command parsing and execution, integrating with various services to process Discord interactions and
///     messages.
/// </summary>
public class CommandHandler : INService
{
    private const int GlobalCommandsCooldown = 750;

    private const float OneThousandth = 1.0f / 1000;

    /// <summary>
    ///     Services stuffs
    /// </summary>
    public readonly IServiceProvider Services;

    private readonly InteractionAckTracker ackTracker;
    private readonly Mewdeko bot;
    private readonly BotConfigService bss;
    private readonly IDataCache cache;

    // ReSharper disable once NotAccessedField.Local
    private readonly Timer clearUsersOnShortCooldown;
    private readonly DiscordShardedClient client;
    private readonly IAnalyticsCollector collector;
    private readonly CommandService commandService;
    private readonly IDataConnectionFactory dbFactory;
    private readonly GuildSettingsService gss;
    private readonly InteractionService interactionService;
    private readonly InteractiveService interactiveService;
    private readonly Localization localization;
    private readonly ILogger<CommandHandler> logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="CommandHandler" /> class.
    /// </summary>
    /// <param name="client">The Discord client.</param>
    /// <param name="dbFactory">The database connection factory.</param>
    /// <param name="commandService">The service for handling commands.</param>
    /// <param name="bss">The bot configuration service.</param>
    /// <param name="bot">The bot instance.</param>
    /// <param name="services">The service provider for dependency injection.</param>
    /// <param name="interactionService">The service for handling interactions.</param>
    /// <param name="gss">The guild settings service.</param>
    /// <param name="eventHandler">The event handler for discord events.</param>
    /// <param name="cache">The data cache service.</param>
    /// <param name="logger">The logger instance for structured logging.</param>
    /// <param name="interactiveService">The interactiveservice service.</param>
    /// <param name="collector">The analytics collector that records command executions.</param>
    /// <param name="ackTracker">Tracks interaction timings shared with the REST hook.</param>
    /// <param name="localization">Resolves the guild locale recorded with each command.</param>
    public CommandHandler(DiscordShardedClient client, IDataConnectionFactory dbFactory, CommandService commandService,
        BotConfigService bss, Mewdeko bot, IServiceProvider services,
        InteractionService interactionService,
        GuildSettingsService gss, EventHandler eventHandler, IDataCache cache, ILogger<CommandHandler> logger,
        InteractiveService interactiveService, IAnalyticsCollector collector, InteractionAckTracker ackTracker,
        Localization localization)
    {
        this.interactionService = interactionService;
        this.gss = gss;
        this.cache = cache;
        this.logger = logger;
        this.interactiveService = interactiveService;
        this.collector = collector;
        this.ackTracker = ackTracker;
        this.localization = localization;
        this.client = client;
        this.commandService = commandService;
        this.bss = bss;
        this.bot = bot;
        this.dbFactory = dbFactory;
        this.Services = services;
        eventHandler.Subscribe("InteractionCreated", "CommandHandler", TryRunInteraction);
        this.interactionService.SlashCommandExecuted += HandleCommands;
        this.interactionService.ContextCommandExecuted += HandleContextCommands;
        this.interactionService.ComponentCommandExecuted += HandleComponentExecuted;
        this.interactionService.ModalCommandExecuted += HandleModalExecuted;
        this.interactionService.AutocompleteCommandExecuted += HandleAutocompleteCommandExecuted;
        this.interactionService.AutocompleteHandlerExecuted += HandleAutocompleteHandlerExecuted;
        clearUsersOnShortCooldown = new Timer(_ => UsersOnShortCooldown.Clear(), null, GlobalCommandsCooldown,
            GlobalCommandsCooldown);
        eventHandler.Subscribe("MessageReceived", "CommandHandler", MessageReceivedHandler);
    }

    /// <summary>
    ///     A thread-safe dictionary mapping channel IDs to command parse queues.
    /// </summary>
    private NonBlocking.ConcurrentDictionary<ulong, ConcurrentQueue<IUserMessage>> CommandParseQueue { get; } = new();

    /// <summary>
    ///     A thread-safe dictionary indicating whether a command parse lock is active for a channel.
    /// </summary>
    private NonBlocking.ConcurrentDictionary<ulong, bool> CommandParseLock { get; } = new();

    private ConcurrentHashSet<ulong> UsersOnShortCooldown { get; } = [];

    /// <summary>
    ///     Event that occurs when a command is executed.
    /// </summary>
    public event Func<IUserMessage, CommandInfo, Task> CommandExecuted = delegate { return Task.CompletedTask; };

    /// <summary>
    ///     Event that occurs when a command is errored.w
    /// </summary>
    public event Func<CommandInfo, ITextChannel, string, IUser?, Task> CommandErrored = delegate
    {
        return Task.CompletedTask;
    };

    /// <summary>
    ///     Used for xp, for some reason.
    /// </summary>
    public event Func<IUserMessage, Task> OnMessageNoTrigger = delegate { return Task.CompletedTask; };

    private Task HandleContextCommands(ContextCommandInfo info, IInteractionContext ctx, IResult result)
    {
        _ = HandleContextCommandsInternal(info, ctx, result);
        return Task.CompletedTask;
    }

    private async Task HandleContextCommandsInternal(ContextCommandInfo info, IInteractionContext ctx, IResult result)
    {
        var measured = ackTracker.End(ctx.Interaction.Id);
        await using var dbContext = await dbFactory.CreateConnectionAsync();

        var optedOut = false;
        if (ctx.Guild is not null)
        {
            var gconf = await gss.GetGuildConfig(ctx.Guild.Id);
            optedOut = gconf.StatsOptOut;
            if (!optedOut)
            {
                var user = await dbContext.GetOrCreateUser(ctx.User);
                optedOut = user.StatsOptOut;
                if (!optedOut)
                {
                    var comStats = new CommandStat
                    {
                        ChannelId = ctx.Channel.Id,
                        GuildId = ctx.Guild.Id,
                        IsSlash = true,
                        NameOrId = info.Name,
                        UserId = ctx.User.Id,
                        Module = info.Module.Name
                    };
                    await dbContext.InsertAsync(comStats);
                }
            }
        }

        var kind = info?.CommandType == ApplicationCommandType.User ? "user_ctx" : "msg_ctx";
        EmitInteraction(kind, info?.Module.Name, info?.Name ?? InteractionName(ctx), ctx, result, measured,
            optedOut);

        if (!result.IsSuccess)
        {
            await ctx.Interaction
                .SendEphemeralErrorAsync($"Command failed for the following reason:\n{result.ErrorReason}",
                    bss.Data)
                .ConfigureAwait(false);
            if (bss.Data.LogCommandExecutions)
            {
                logger.LogWarning(
                    "Slash Command Errored\n\t" + "User: {0}\n\t" + "Server: {1}\n\t" + "Channel: {2}\n\t" +
                    "Message: {3}\n\t" + "Error: {4}",
                    $"{ctx.User} [{ctx.User.Id}]", // {0}
                    ctx.Guild == null ? "PRIVATE" : $"{ctx.Guild.Name} [{ctx.Guild.Id}]", // {1}
                    ctx.Channel == null ? "PRIVATE" : $"{ctx.Channel.Name} [{ctx.Channel.Id}]", // {2}
                    info.MethodName, result.ErrorReason);
            }

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
        if (bss.Data.LogCommandExecutions)
        {
            logger.LogInformation(
                "Slash Command Executed" + "\n\t" + "User: {0}\n\t" + "Server: {1}\n\t" + "Channel: {2}\n\t" +
                "Module: {3}\n\t" + "Command: {4}",
                $"{ctx.User} [{ctx.User.Id}]", // {0}
                chan == null ? "PRIVATE" : $"{chan.Guild.Name} [{chan.Guild.Id}]", // {1}
                chan == null ? "PRIVATE" : $"{chan.Name} [{chan.Id}]", // {2}
                info.Module.SlashGroupName, info.MethodName); // {3}
        }

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
    }

    /// <summary>
    ///     Runs the late blockers (guild permissions, cooldowns, global permissions, overrides, and reputation
    ///     requirements) against an application command before it executes, mirroring the text command pipeline.
    /// </summary>
    /// <param name="ctx">The interaction context.</param>
    /// <param name="interaction">The incoming interaction.</param>
    /// <returns>True when a blocker refused the command and the interaction has been answered.</returns>
    private async Task<bool> IsBlockedByLateBlockers(IInteractionContext ctx, SocketInteraction interaction)
    {
        ICommandInfo? command = interaction switch
        {
            ISlashCommandInteraction slash => interactionService.SearchSlashCommand(slash) is
            {
                IsSuccess: true
            } slashResult
                ? slashResult.Command
                : null,
            IUserCommandInteraction user => interactionService.SearchUserCommand(user) is
            {
                IsSuccess: true
            } userResult
                ? userResult.Command
                : null,
            IMessageCommandInteraction message => interactionService.SearchMessageCommand(message) is
            {
                IsSuccess: true
            } messageResult
                ? messageResult.Command
                : null,
            _ => null
        };

        if (command is null)
            return false;

        foreach (var blocker in Services.GetServices<ILateBlocker>())
        {
            if (!await blocker.TryBlockLate(client, ctx, command).ConfigureAwait(false))
                continue;

            if (!interaction.HasResponded)
            {
                try
                {
                    var strings = Services.GetRequiredService<GeneratedBotStrings>();
                    await interaction.SendEphemeralErrorAsync(
                        strings.CommandBlocked((interaction.Channel as IGuildChannel)?.GuildId ?? 0), bss.Data);
                }
                catch
                {
                }
            }

            return true;
        }

        return false;
    }

    private Task HandleCommands(SlashCommandInfo slashInfo, IInteractionContext ctx, IResult result)
    {
        var measured = ackTracker.End(ctx.Interaction.Id);
        _ = Task.Run(async () =>
        {
            await using var dbContext = await dbFactory.CreateConnectionAsync();

            var optedOut = false;
            if (ctx.Guild is not null)
            {
                var gconf = await gss.GetGuildConfig(ctx.Guild.Id);
                optedOut = gconf.StatsOptOut;
                if (!optedOut)
                {
                    var user = await dbContext.GetOrCreateUser(ctx.User);
                    optedOut = user.StatsOptOut;
                    if (!optedOut)
                    {
                        var comStats = new CommandStat
                        {
                            ChannelId = ctx.Channel.Id,
                            GuildId = ctx.Guild.Id,
                            IsSlash = true,
                            NameOrId = slashInfo.Name,
                            UserId = ctx.User.Id,
                            Module = slashInfo.Module.Name
                        };
                        await dbContext.InsertAsync(comStats);
                    }
                }
            }

            EmitInteraction("slash", slashInfo?.Module.Name, slashInfo?.Name ?? InteractionName(ctx), ctx, result,
                measured, optedOut);

            if (!result.IsSuccess)
            {
                await ctx.Interaction
                    .SendEphemeralErrorAsync($"Command failed for the following reason:\n{result.ErrorReason}",
                        bss.Data)
                    .ConfigureAwait(false);
                if (bss.Data.LogCommandExecutions)
                {
                    logger.LogWarning(
                        "Slash Command Errored\n\t" + "User: {0}\n\t" + "Server: {1}\n\t" + "Channel: {2}\n\t" +
                        "Message: {3}\n\t" + "Error: {4}",
                        $"{ctx.User} [{ctx.User.Id}]", // {0}
                        ctx.Guild == null ? "PRIVATE" : $"{ctx.Guild.Name} [{ctx.Guild.Id}]", // {1}
                        ctx.Channel == null ? "PRIVATE" : $"{ctx.Channel.Name} [{ctx.Channel.Id}]", // {2}
                        slashInfo.MethodName, result.ErrorReason);
                }

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
            if (bss.Data.LogCommandExecutions)
            {
                logger.LogInformation(
                    "Slash Command Executed" + "\n\t" + "User: {0}\n\t" + "Server: {1}\n\t" + "Channel: {2}\n\t" +
                    "Module: {3}\n\t" + "Command: {4}",
                    $"{ctx.User} [{ctx.User.Id}]", // {0}
                    chan == null ? "PRIVATE" : $"{chan.Guild.Name} [{chan.Guild.Id}]", // {1}
                    chan == null ? "PRIVATE" : $"{chan.Name} [{chan.Id}]", // {2}
                    slashInfo.Module.SlashGroupName, slashInfo.MethodName); // {3}
            }

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

    private Task HandleComponentExecuted(ComponentCommandInfo info, IInteractionContext ctx, IResult result)
    {
        var kind = ctx.Interaction is SocketMessageComponent { Data.Type: ComponentType.Button } ? "button" : "select";
        return RecordInteractionAsync(kind, info?.Module.Name, info?.Name ?? InteractionName(ctx), ctx, result);
    }

    private Task HandleModalExecuted(ModalCommandInfo info, IInteractionContext ctx, IResult result)
    {
        return RecordInteractionAsync("modal", info?.Module.Name, info?.Name ?? InteractionName(ctx), ctx, result);
    }

    private Task HandleAutocompleteCommandExecuted(AutocompleteCommandInfo info, IInteractionContext ctx,
        IResult result)
    {
        return RecordInteractionAsync("autocomplete", info?.Module.Name, info?.Name ?? InteractionName(ctx), ctx,
            result);
    }

    private Task HandleAutocompleteHandlerExecuted(IAutocompleteHandler handler, IInteractionContext ctx,
        IResult result)
    {
        return RecordInteractionAsync("autocomplete", null, InteractionName(ctx), ctx, result);
    }

    private async Task RecordInteractionAsync(string kind, string? module, string command, IInteractionContext ctx,
        IResult result)
    {
        var measured = ackTracker.End(ctx.Interaction.Id);
        var optedOut = false;
        if (ctx.Guild is not null)
        {
            var gconf = await gss.GetGuildConfig(ctx.Guild.Id).ConfigureAwait(false);
            optedOut = gconf?.StatsOptOut ?? false;
        }

        EmitInteraction(kind, module, command, ctx, result, measured, optedOut);
    }

    private void EmitInteraction(string kind, string? module, string command, IInteractionContext ctx,
        IResult result, (long? AckMs, long DurationMs) measured, bool optedOut)
    {
        var guild = ctx.Guild as SocketGuild;
        var shard = guild is null ? (int?)null : client.GetShardIdFor(guild);
        var exception = result is Discord.Interactions.ExecuteResult executeResult ? executeResult.Exception : null;
        string? errorClass = null;
        if (!result.IsSuccess)
            errorClass = exception is null ? result.Error?.ToString() : Innermost(exception).GetType().Name;

        var language = ctx.Guild is null
            ? ctx.Interaction.UserLocale
            : localization.GetCultureInfo(ctx.Guild.Id)?.Name;

        collector.Command(new CommandSample(kind, module, command, result.IsSuccess, errorClass,
            result.IsSuccess ? null : result.ErrorReason, measured.DurationMs, measured.AckMs, ctx.Guild?.Id,
            guild?.MemberCount, shard, language, optedOut));

        if (exception is not null)
            collector.Error(exception, $"CommandHandler.{kind}", module, ctx.Guild?.Id, shard);
    }

    private static string InteractionName(IInteractionContext ctx)
    {
        return ctx.Interaction switch
        {
            SocketCommandBase command => command.CommandName,
            SocketMessageComponent component => component.Data.CustomId,
            SocketModal modal => modal.Data.CustomId,
            SocketAutocompleteInteraction autocomplete => autocomplete.Data.CommandName,
            _ => ctx.Interaction.Type.ToString()
        };
    }

    private static Exception Innermost(Exception exception)
    {
        var current = exception;
        while (current.InnerException is not null)
            current = current.InnerException;
        return current;
    }

    private async Task TryRunInteraction(SocketInteraction interaction)
    {
        try
        {
            var blacklistService = Services.GetService<BlacklistService>();
            var cb = new ComponentBuilder().WithButton("Support Server", null, ButtonStyle.Link,
                url: "https://discord.gg/mewdeko").Build();
            foreach (var bl in blacklistService.BlacklistEntries)
            {
                if ((interaction.Channel as IGuildChannel)?.Guild != null && bl.Type == (int)BlacklistType.Server &&
                    bl.ItemId == (interaction.Channel as IGuildChannel)?.Guild?.Id)
                {
                    await interaction.RespondAsync(
                        $"*This guild is blacklisted from Mewdeko for **{bl.Reason}**! You can visit the support server below to try and resolve this.*",
                        components: cb).ConfigureAwait(false);
                    return;
                }

                if (bl.Type != (int)BlacklistType.User || bl.ItemId != interaction.User.Id) continue;
                await interaction.RespondAsync(
                    $"*You are blacklisted from Mewdeko for **{bl.Reason}**! You can visit the support server below to try and resolve this.*",
                    ephemeral: true, components: cb).ConfigureAwait(false);
                return;
            }

            if (interaction.Type == InteractionType.ApplicationCommand)
            {
                var ctS = Services.GetService<ChatTriggersService>();
                var triggers = await ctS.GetChatTriggersFor((interaction.Channel as IGuildChannel)?.Guild?.Id);
                var trigger = triggers.FirstOrDefault(x => x.RealName() == interaction.GetRealName());
                if (trigger is not null)
                {
                    await ctS.RunInteractionTrigger(interaction, trigger).ConfigureAwait(false);
                    return;
                }
            }

            // Ignore fergun interactions, getting tired of the cant find interaction handler crap
            if (interactiveService.IsManaged(interaction))
                return;

            // i hate discord
            // if (interaction is IComponentInteraction compInter
            //     && compInter.Message.Author.IsWebhook
            //     && !compInter.Data.CustomId.StartsWith("trigger.")) return;

            var ctx = new ShardedInteractionContext(client, interaction);
            ackTracker.Begin(interaction.Id, interaction.CreatedAt);
            if (await IsBlockedByLateBlockers(ctx, interaction).ConfigureAwait(false))
            {
                ackTracker.End(interaction.Id);
                return;
            }

            var result = await interactionService.ExecuteCommandAsync(ctx, Services).ConfigureAwait(false);
            if (!result.IsSuccess)
                ackTracker.End(interaction.Id);
#if DEBUG
            logger.LogInformation($"Button was executed:{result.IsSuccess}\nReason:{result.ErrorReason}");
#endif
        }
        catch (Exception e)
        {
            ackTracker.End(interaction.Id);
            collector.Error(e, "CommandHandler.TryRunInteraction", null,
                (interaction.Channel as IGuildChannel)?.GuildId);
            logger.LogError(e, "Interaction failed to execute");
            throw;
        }
    }

    /// <summary>
    ///     Executes an external command within a specific guild and channel context.
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
                logger.LogWarning("Channel for external execution not found");
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
            logger.LogWarning(ex, "Error in CommandHandler");
            if (ex.InnerException != null)
                logger.LogWarning(ex.InnerException, "Inner Exception of the error in CommandHandler");
        }
    }

    /// <summary>
    ///     Adds a command to the parse queue for a given channel.
    /// </summary>
    /// <param name="usrMsg">The user message to add to the queue.</param>
    public void AddCommandToParseQueue(IUserMessage usrMsg)
    {
        CommandParseQueue.AddOrUpdate(usrMsg.Channel.Id,
            _ => new ConcurrentQueue<IUserMessage>(new List<IUserMessage>
            {
                usrMsg
            }), (_, y) =>
            {
                y.Enqueue(usrMsg);
                return y;
            });
    }

    /// <summary>
    ///     Attempts to execute commands in the parse queue for a given channel.
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
                    collector.Error(e, "CommandHandler.ExecuteCommandsInChannelAsync", null,
                        (msg.Channel as IGuildChannel)?.GuildId);
                    logger.LogError("Error occured in the handler: {E}", e);
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

        var lateExecutors = Services.GetServices<ILateExecutor>();
        var inputTransformers = Services.GetServices<IInputTransformer>();
        var earlyBehaviors = Services.GetServices<IEarlyBehavior>().ToArray();

        foreach (var beh in earlyBehaviors)
        {
            if (!await beh.RunBehavior(client, guild, usrMsg).ConfigureAwait(false)) continue;
            if (ShouldLogEarlyBehavior(beh))
            {
                logger.LogInformation(
                    "Executed {BehaviorType} behavior: {BehaviorName} for user: {User} in: {Guild}",
                    beh.BehaviorType, beh.GetType().Name, $"{usrMsg.Author} | {usrMsg.Id}", $"{guild} | {guild.Id}");
            }

            return;
        }

        var messageContent = usrMsg.Content;
        foreach (var exec in inputTransformers)
        {
            messageContent = await exec.TransformInput(guild, usrMsg.Channel, usrMsg.Author, messageContent)
                .ConfigureAwait(false);
            if (messageContent != usrMsg.Content) break;
        }

        var prefix = await gss.GetPrefix(guild);
        if (prefix == null) return;

        var prefixLength = GetPrefixLength(messageContent, prefix);
        if (prefixLength == 0)
        {
            OnMessageNoTrigger?.Invoke(usrMsg).ConfigureAwait(false);
            return;
        }

        var started = Stopwatch.GetTimestamp();
        var (success, error, info, errorClass) = await ExecuteCommandAsync(new CommandContext(client, usrMsg),
            messageContent, prefixLength, MultiMatchHandling.Best).ConfigureAwait(false);
        var durationMs = (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds;

        execTime = Environment.TickCount - execTime;

        var optedOut = await UpdateCommandStats(guild, channel, usrMsg, info).ConfigureAwait(false);
        if (info is not null)
        {
            var socketGuild = guild as SocketGuild;
            collector.Command(new CommandSample("text", info.Module.Name, info.Name, success,
                success ? null : errorClass, success ? null : error, durationMs, null, guild?.Id,
                socketGuild?.MemberCount, socketGuild is null ? null : client.GetShardIdFor(socketGuild),
                localization.GetCultureInfo(guild?.Id)?.Name, optedOut));
        }

        if (success)
        {
            await LogCommandExecution(usrMsg, channel as ITextChannel, info, true, execTime).ConfigureAwait(false);
            await CommandExecuted(usrMsg, info).ConfigureAwait(false);
        }
        else if (!string.IsNullOrEmpty(error))
        {
            if (info is null)
            {
                var serv = Services.GetRequiredService<HelpService>();
                if (channel is IDMChannel)
                    await serv.BadCommand(client, null, usrMsg);
                return;
            }

            await LogCommandExecution(usrMsg, channel as ITextChannel, info, false, execTime, error)
                .ConfigureAwait(false);
            if (guild != null)
            {
                var permissionService = Services.GetService<PermissionService>();
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

    private bool ShouldLogEarlyBehavior(IEarlyBehavior beh)
    {
        return beh switch
        {
            BlacklistService => bss.Data.LogBlacklistedAttempts,
            FilterService => bss.Data.LogFilteredMessages,
            ChatTriggersService => bss.Data.LogChatTriggerFires,
            _ => true
        };
    }

    private async Task<(bool Success, string Error, CommandInfo? Info, string? ErrorClass)> ExecuteCommandAsync(
        CommandContext context, string input, int argPos,
        MultiMatchHandling multiMatchHandling = MultiMatchHandling.Exception)
    {
        var searchResult = commandService.Search(context, input[argPos..]);
        if (!searchResult.IsSuccess)
            return (false, searchResult.ErrorReason, null, searchResult.Error?.ToString());

        var lateBlockers = Services.GetServices<ILateBlocker>().ToArray();

        var commands = searchResult.Commands;
        var preconditionResults = await Task.WhenAll(commands.Select(async match =>
            (match, await match.Command.CheckPreconditionsAsync(context, Services).ConfigureAwait(false))));

        var successfulPreconditions = preconditionResults.Where(x => x.Item2.IsSuccess).ToArray();

        if (successfulPreconditions.Length == 0)
        {
            var bestCandidate = preconditionResults
                .OrderByDescending(x => x.match.Command.Priority)
                .FirstOrDefault(x => !x.Item2.IsSuccess);
            return (false, bestCandidate.Item2.ErrorReason, commands[0].Command,
                bestCandidate.Item2.Error?.ToString());
        }

        var parseResults = await Task.WhenAll(successfulPreconditions.Select(async x =>
        {
            var parseResult = await x.match.ParseAsync(context, searchResult, x.Item2, Services).ConfigureAwait(false);
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
            return (false, bestMatch.parseResult.ErrorReason, commands[0].Command,
                bestMatch.parseResult.Error?.ToString());
        }

        var cmd = successfulParses[0].match.Command;

        if (!UsersOnShortCooldown.Add(context.User.Id))
            return (false, "You are on a short cooldown.", cmd, "Cooldown");

        var chosenOverload = successfulParses[0];

        foreach (var i in lateBlockers)
        {
            var blocked = await i.TryBlockLate(client, context,
                chosenOverload.match.Command.Module.GetTopLevelModule().Name,
                chosenOverload.match.Command);
            if (blocked)
                return (false, "lateblocker", null, "Blocked");
        }

        var result = await chosenOverload.match.ExecuteAsync(context, chosenOverload.parseResult, Services)
            .ConfigureAwait(false);

        if (result is not ExecuteResult executeResult)
            return (result.IsSuccess, result.ErrorReason, cmd, result.Error?.ToString());
        if (executeResult.Exception != null && executeResult.Exception is not HttpException
            {
                DiscordCode: DiscordErrorCode.InsufficientPermissions
            })
        {
            var guild = context.Guild as SocketGuild;
            collector.Error(executeResult.Exception, "CommandHandler.ExecuteCommandAsync", cmd.Module.Name,
                guild?.Id, guild is null ? null : client.GetShardIdFor(guild));
            logger.LogWarning(executeResult.Exception, "Command execution error");
        }

        return (executeResult.IsSuccess, executeResult.ErrorReason, cmd,
            executeResult.Exception is null
                ? executeResult.Error?.ToString()
                : Innermost(executeResult.Exception).GetType().Name);
    }

    private async Task LogCommandExecution(IMessage usrMsg, ITextChannel? channel, CommandInfo? commandInfo,
        bool success, int executionTime, string errorMessage = null)
    {
        var logBuilder = new StringBuilder()
            .AppendLine(success ? "Command Executed" : "Command Errored")
            .AppendLine($"User: {usrMsg.Author} [{usrMsg.Author.Id}]")
            .AppendLine($"Server: {(channel == null ? "PRIVATE" : $"{channel.Guild.Name} [{channel.Guild.Id}]")}")
            .AppendLine($"Channel: {(channel == null ? "PRIVATE" : $"{channel.Name} [{channel.Id}]")}")
            .AppendLine($"Message: {usrMsg.Content}")
            .AppendLine($"Execution Time: {executionTime}ms");

        if (!success && !string.IsNullOrEmpty(errorMessage))
            logBuilder.AppendLine($"Error: {errorMessage}");

        if (bss.Data.LogCommandExecutions)
        {
            if (success)
                logger.LogInformation(logBuilder.ToString());
            else
                logger.LogWarning(logBuilder.ToString());
        }


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

        if (channel is null)
            return;

        var gc = await gss.GetGuildConfig(channel.GuildId);
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
            client.CurrentUser.Mention, // e.g., "@BotName"
            $"<@{client.CurrentUser.Id}>", // e.g., "<@1234567890>"
            $"<@!{client.CurrentUser.Id}>" // e.g., "<@!1234567890>" (for nicknames)
        };

        // Find the longest matching mention at the start
        return (from mention in mentions
            where content.StartsWith(mention + " ", StringComparison.InvariantCulture)
            select mention.Length + 1).FirstOrDefault();
    }

    private async Task<bool> UpdateCommandStats(IGuild? guild, IChannel channel, IUserMessage usrMsg,
        CommandInfo? info)
    {
        if (guild == null || info == null) return false;

        var guildConfig = await gss.GetGuildConfig(guild.Id).ConfigureAwait(false);
        if (guildConfig.StatsOptOut) return true;

        await using var dbContext = await dbFactory.CreateConnectionAsync().ConfigureAwait(false);
        var user = await dbContext.GetOrCreateUser(usrMsg.Author).ConfigureAwait(false);
        if (user.StatsOptOut) return true;

        var commandStats = new CommandStat
        {
            ChannelId = channel.Id,
            GuildId = guild.Id,
            IsSlash = false,
            NameOrId = info.Name,
            UserId = usrMsg.Author.Id,
            Module = info.Module.Name
        };
        await dbContext.InsertAsync(commandStats).ConfigureAwait(false);
        return false;
    }
}