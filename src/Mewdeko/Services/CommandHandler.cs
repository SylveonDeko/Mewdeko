using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Discord.Commands;
using Discord.Interactions;
using Discord.Net;
using Discord.Rest;
using Mewdeko.Common.Collections;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Modules.Chat_Triggers.Services;
using Mewdeko.Modules.Permissions.Common;
using Mewdeko.Modules.Permissions.Services;
using Mewdeko.Services.Settings;
using Mewdeko.Services.strings;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using ExecuteResult = Discord.Commands.ExecuteResult;
using IResult = Discord.Interactions.IResult;
using PreconditionResult = Discord.Commands.PreconditionResult;

namespace Mewdeko.Services;

public class CommandHandler : INService
{
    public const int GlobalCommandsCooldown = 750;

    private const float OneThousandth = 1.0f / 1000;
    private readonly Mewdeko bot;
    private readonly BotConfigService bss;
    private readonly DiscordSocketClient client;
    private readonly CommandService commandService;
    private readonly DbService db;
    private readonly IServiceProvider services;

    // ReSharper disable once NotAccessedField.Local
    private readonly Timer clearUsersOnShortCooldown;
    private readonly IBotStrings strings;
    public IEnumerable<IEarlyBehavior> EarlyBehaviors;
    private IEnumerable<IInputTransformer> inputTransformers;
    public IEnumerable<ILateBlocker> LateBlockers;
    private IEnumerable<ILateExecutor> lateExecutors;
    public readonly InteractionService InteractionService;
    private readonly GuildSettingsService gss;

    public NonBlocking.ConcurrentDictionary<ulong, ConcurrentQueue<IUserMessage>> CommandParseQueue { get; } = new();
    public NonBlocking.ConcurrentDictionary<ulong, bool> CommandParseLock { get; } = new();

    public CommandHandler(DiscordSocketClient client, DbService db, CommandService commandService,
        BotConfigService bss, Mewdeko bot, IServiceProvider services, IBotStrings strngs,
        InteractionService interactionService,
        GuildSettingsService gss, EventHandler eventHandler)
    {
        InteractionService = interactionService;
        this.gss = gss;
        strings = strngs;
        this.client = client;
        this.commandService = commandService;
        this.bss = bss;
        this.bot = bot;
        this.db = db;
        this.services = services;
        eventHandler.InteractionCreated += TryRunInteraction;
        InteractionService.SlashCommandExecuted += HandleCommands;
        InteractionService.ContextCommandExecuted += HandleContextCommands;
        clearUsersOnShortCooldown = new Timer(_ => UsersOnShortCooldown.Clear(), null, GlobalCommandsCooldown,
            GlobalCommandsCooldown);
        eventHandler.MessageReceived += MessageReceivedHandler;
    }

    public ConcurrentHashSet<ulong> UsersOnShortCooldown { get; } = new();

    public event Func<IUserMessage, CommandInfo, Task> CommandExecuted = delegate { return Task.CompletedTask; };

    public event Func<CommandInfo, ITextChannel, string, IUser?, Task> CommandErrored = delegate { return Task.CompletedTask; };

    public event Func<IUserMessage, Task> OnMessageNoTrigger = delegate { return Task.CompletedTask; };

    public Task HandleContextCommands(ContextCommandInfo info, IInteractionContext ctx, IResult result)
    {
        _ = Task.Run(async () =>
        {
            if (ctx.Guild is not null)
            {
                var gconf = await gss.GetGuildConfig(ctx.Guild.Id);
                if (!gconf.StatsOptOut)
                {
                    await using var uow = db.GetDbContext();
                    var user = await uow.GetOrCreateUser(ctx.User);
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
                        await uow.CommandStats.AddAsync(comStats);
                        await uow.SaveChangesAsync();
                    }
                }
            }

            if (!result.IsSuccess)
            {
                await ctx.Interaction.SendEphemeralErrorAsync($"Command failed for the following reason:\n{result.ErrorReason}").ConfigureAwait(false);
                Log.Warning("Slash Command Errored\n\t" + "User: {0}\n\t" + "Server: {1}\n\t" + "Channel: {2}\n\t" + "Message: {3}\n\t" + "Error: {4}",
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

                if (ctx.Guild is null) return;
                {
                    if (info.MethodName.ToLower() is "confess" or "confessreport")
                        return;

                    var gc = await gss.GetGuildConfig(ctx.Guild.Id);
                    if (gc.CommandLogChannel is 0) return;
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
            Log.Information("Slash Command Executed" + "\n\t" + "User: {0}\n\t" + "Server: {1}\n\t" + "Channel: {2}\n\t" + "Module: {3}\n\t" + "Command: {4}",
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

            if (ctx.Guild is null) return;
            {
                if (info.MethodName.ToLower() is "confess" or "confessreport")
                    return;

                var gc = await gss.GetGuildConfig(ctx.Guild.Id);
                if (gc.CommandLogChannel is 0) return;
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
            if (ctx.Guild is not null)
            {
                var gconf = await gss.GetGuildConfig(ctx.Guild.Id);
                if (!gconf.StatsOptOut)
                {
                    await using var uow = db.GetDbContext();
                    var user = await uow.GetOrCreateUser(ctx.User);
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
                        await uow.CommandStats.AddAsync(comStats);
                        await uow.SaveChangesAsync();
                    }
                }
            }

            if (!result.IsSuccess)
            {
                await ctx.Interaction.SendEphemeralErrorAsync($"Command failed for the following reason:\n{result.ErrorReason}").ConfigureAwait(false);
                Log.Warning("Slash Command Errored\n\t" + "User: {0}\n\t" + "Server: {1}\n\t" + "Channel: {2}\n\t" + "Message: {3}\n\t" + "Error: {4}",
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

                if (ctx.Guild is null) return;
                {
                    if (slashInfo.MethodName.ToLower() is "confess" or "confessreport")
                        return;

                    var gc = await gss.GetGuildConfig(ctx.Guild.Id);
                    if (gc.CommandLogChannel is 0) return;
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
            Log.Information("Slash Command Executed" + "\n\t" + "User: {0}\n\t" + "Server: {1}\n\t" + "Channel: {2}\n\t" + "Module: {3}\n\t" + "Command: {4}",
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

            if (ctx.Guild is null) return;
            {
                if (slashInfo.MethodName.ToLower() is "confess" or "confessreport")
                    return;

                var gc = await gss.GetGuildConfig(ctx.Guild.Id);
                if (gc.CommandLogChannel is 0) return;
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
        var blacklistService = services.GetService<BlacklistService>();
        var cb = new ComponentBuilder().WithButton("Support Server", null, ButtonStyle.Link,
            url: "https://discord.gg/mewdeko").Build();
        foreach (var bl in blacklistService.BlacklistEntries)
        {
            if ((interaction.Channel as IGuildChannel)?.Guild != null && bl.Type == BlacklistType.Server && bl.ItemId == (interaction.Channel as IGuildChannel)?.Guild?.Id)
            {
                await interaction.RespondAsync($"*This guild is blacklisted from Mewdeko for **{bl.Reason}**! You can visit the support server below to try and resolve this.*",
                    components: cb).ConfigureAwait(false);
                return;
            }

            if (bl.Type == BlacklistType.User && bl.ItemId == interaction.User.Id)
            {
                await interaction.RespondAsync($"*You are blacklisted from Mewdeko for **{bl.Reason}**! You can visit the support server below to try and resolve this.*",
                    ephemeral: true, components: cb).ConfigureAwait(false);
                return;
            }
        }

        if (interaction.Type == InteractionType.ApplicationCommand)
        {
            var ctS = services.GetService<ChatTriggersService>();
            var triggers = ctS.GetChatTriggersFor((interaction.Channel as IGuildChannel)?.Guild?.Id);
            var trigger = triggers.FirstOrDefault(x => x.RealName == interaction.GetRealName());
            if (trigger is not null)
            {
                await ctS.RunInteractionTrigger(interaction, trigger).ConfigureAwait(false);
                return;
            }
        }

        // filter webhook interactions
        // if (interaction is IComponentInteraction compInter
        //     && compInter.Message.Author.IsWebhook
        //     && !compInter.Data.CustomId.StartsWith("trigger.")) return;

        var ctx = new SocketInteractionContext(client, interaction);
        var result = await InteractionService.ExecuteCommandAsync(ctx, services).ConfigureAwait(false);
        Log.Information($"Button was executed:{result.IsSuccess}\nReason:{result.ErrorReason}");
    }

    public string SetDefaultPrefix(string prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix))
            throw new ArgumentNullException(nameof(prefix));

        bss.ModifyConfig(bs => bs.Prefix = prefix);

        return prefix;
    }


    public void AddServices(IServiceCollection services)
    {
        LateBlockers = services
            .Where(x => x.ImplementationType?.GetInterfaces().Contains(typeof(ILateBlocker)) ?? false)
            .Select(x => this.services.GetService(x.ImplementationType) as ILateBlocker)
            .OrderByDescending(x => x.Priority)
            .ToArray();

        lateExecutors = services.Where(x =>
                x.ImplementationType?.GetInterfaces().Contains(typeof(ILateExecutor)) ?? false)
            .Select(x => this.services.GetService(x.ImplementationType) as ILateExecutor)
            .ToArray();

        inputTransformers = services.Where(x =>
                x.ImplementationType?.GetInterfaces().Contains(typeof(IInputTransformer)) ?? false)
            .Select(x => this.services.GetService(x.ImplementationType) as IInputTransformer)
            .ToArray();

        EarlyBehaviors = services.Where(x =>
                x.ImplementationType?.GetInterfaces().Contains(typeof(IEarlyBehavior)) ?? false)
            .Select(x => this.services.GetService(x.ImplementationType) as IEarlyBehavior)
            .ToArray();
    }

    public async Task ExecuteExternal(ulong? guildId, ulong channelId, string commandText)
    {
        if (guildId != null)
        {
            var guild = client.GetGuild(guildId.Value);
            if (guild?.GetChannel(channelId) is not SocketTextChannel channel)
            {
                Log.Warning("Channel for external execution not found.");
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

    private Task LogSuccessfulExecution(IMessage usrMsg, IGuildChannel? channel, params int[] execPoints)
    {
        _ = Task.Run(async () =>
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
            var toFetch = await client.Rest.GetChannelAsync(bss.Data.CommandLogChannel).ConfigureAwait(false);
            if (toFetch is RestTextChannel restChannel)
            {
                var eb = new EmbedBuilder()
                    .WithOkColor()
                    .WithTitle("Text Command Executed")
                    .AddField("Executed Time", string.Join("/", execPoints.Select(x => (x * OneThousandth).ToString("F3"))))
                    .AddField("User", $"{usrMsg.Author.Mention} {usrMsg.Author} {usrMsg.Author.Id}")
                    .AddField("Guild", channel == null ? "PRIVATE" : $"{channel.Guild.Name} `{channel.Guild.Id}`")
                    .AddField("Channel", channel == null ? "PRIVATE" : $"{channel.Name} `{channel.Id}`")
                    .AddField("Message", usrMsg.Content.TrimTo(1000));

                await restChannel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
            }

            if (channel?.Guild is null) return;
            var guildChannel = (await gss.GetGuildConfig(channel.Guild.Id)).CommandLogChannel;
            if (guildChannel == 0) return;
            var toSend = await client.Rest.GetChannelAsync(guildChannel).ConfigureAwait(false);
            if (toSend is RestTextChannel restTextChannel)
            {
                var eb = new EmbedBuilder()
                    .WithOkColor()
                    .WithTitle("Text Command Executed")
                    .AddField("Executed Time", string.Join("/", execPoints.Select(x => (x * OneThousandth).ToString("F3"))))
                    .AddField("User", $"{usrMsg.Author.Mention} {usrMsg.Author} {usrMsg.Author.Id}")
                    .AddField("Channel", channel == null ? "PRIVATE" : $"{channel.Name} `{channel.Id}`")
                    .AddField("Message", usrMsg.Content.TrimTo(1000));

                await restTextChannel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
            }
        });
        return Task.CompletedTask;
    }

    private Task LogErroredExecution(string errorMessage, IMessage usrMsg, IGuildChannel? channel, params int[] execPoints)
    {
        _ = Task.Run(async () =>
        {
            var errorafter = string.Join("/", execPoints.Select(x => (x * OneThousandth).ToString("F3")));
            Log.Warning($"Command Errored after {errorafter}\n\t" + "User: {0}\n\t" + "Server: {1}\n\t" + "Channel: {2}\n\t" + "Message: {3}\n\t" + "Error: {4}",
                $"{usrMsg.Author} [{usrMsg.Author.Id}]", // {0}
                channel == null ? "PRIVATE" : $"{channel.Guild.Name} [{channel.Guild.Id}]", // {1}
                channel == null ? "PRIVATE" : $"{channel.Name} [{channel.Id}]", // {2}
                usrMsg.Content, errorMessage);

            var toFetch = await client.Rest.GetChannelAsync(bss.Data.CommandLogChannel).ConfigureAwait(false);
            if (toFetch is RestTextChannel restChannel)
            {
                var eb = new EmbedBuilder().WithOkColor().WithTitle("Text Command Errored").AddField("Error Reason", errorMessage)
                    .AddField("Errored Time", execPoints.Select(x => (x * OneThousandth).ToString("F3")))
                    .AddField("User", $"{usrMsg.Author} {usrMsg.Author.Id}")
                    .AddField("Guild", channel == null ? "PRIVATE" : $"{channel.Guild.Name} `{channel.Guild.Id}`")
                    .AddField("Channel", channel == null ? "PRIVATE" : $"{channel.Name} `{channel.Id}`").AddField("Message", usrMsg.Content.TrimTo(1000));

                await restChannel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
            }
        });
        return Task.CompletedTask;
    }

    public async Task MessageReceivedHandler(IMessage msg)
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

    public void AddCommandToParseQueue(IUserMessage usrMsg) => CommandParseQueue.AddOrUpdate(usrMsg.Channel.Id,
        _ => new ConcurrentQueue<IUserMessage>(new List<IUserMessage>
        {
            usrMsg
        }), (_, y) =>
        {
            y.Enqueue(usrMsg);
            return y;
        });

    public async Task<bool> ExecuteCommandsInChannelAsync(ulong channelId)
    {
        try
        {
            if (CommandParseLock.GetValueOrDefault(channelId, false)) return false;
            if (CommandParseQueue.GetValueOrDefault(channelId) is null || CommandParseQueue[channelId].IsEmpty) return false;
            CommandParseLock[channelId] = true;
            while (CommandParseQueue[channelId].TryDequeue(out var msg))
            {
                await TryRunCommand((msg.Channel as IGuildChannel)?.Guild, msg.Channel, msg).ConfigureAwait(false);
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

        //its nice to have early blockers and early blocking executors separate, but
        //i could also have one interface with priorities, and just put early blockers on
        //highest priority. :thinking:
        foreach (var beh in EarlyBehaviors)
        {
            if (!await beh.RunBehavior(client, guild, usrMsg).ConfigureAwait(false)) continue;
            switch (beh.BehaviorType)
            {
                case ModuleBehaviorType.Blocker:
                    Log.Information("Blocked User: [{0}] Message: [{1}] Service: [{2}]", usrMsg.Author,
                        usrMsg.Content, beh.GetType().Name);
                    break;
                case ModuleBehaviorType.Executor:
                    Log.Information("User [{0}] executed [{1}] in [{2}] User ID: {3}", usrMsg.Author,
                        usrMsg.Content,
                        beh.GetType().Name, usrMsg.Author.Id);
                    break;
            }

            return;
        }

        var exec2 = Environment.TickCount - execTime;

        var messageContent = usrMsg.Content;
        foreach (var exec in inputTransformers)
        {
            string newContent;
            if ((newContent = await exec.TransformInput(guild, usrMsg.Channel, usrMsg.Author, messageContent)
                    .ConfigureAwait(false))
                == messageContent.ToLowerInvariant())
            {
                continue;
            }

            messageContent = newContent;
            break;
        }

        var prefix = await gss.GetPrefix(guild?.Id);
        // execute the command and measure the time it took
        if (messageContent.StartsWith(prefix, StringComparison.InvariantCulture) ||
            messageContent.StartsWith($"<@{client.CurrentUser.Id}> ") ||
            messageContent.StartsWith($"<@!{client.CurrentUser.Id}>"))
        {
            if (messageContent.StartsWith($"<@{client.CurrentUser.Id}>"))
                prefix = $"<@{client.CurrentUser.Id}> ";
            if (messageContent.StartsWith($"<@!{client.CurrentUser.Id}>"))
                prefix = $"<@!{client.CurrentUser.Id}> ";
            if (messageContent == $"<@{client.CurrentUser.Id}>"
                || messageContent == $"<@!{client.CurrentUser.Id}>")
            {
                return;
            }

            var (success, error, info) = await ExecuteCommandAsync(new CommandContext(client, usrMsg),
                    messageContent, prefix.Length, services, MultiMatchHandling.Best)
                .ConfigureAwait(false);
            execTime = Environment.TickCount - execTime;
            try
            {
                if (guild is not null)
                {
                    var gconf = await gss.GetGuildConfig(guild.Id);
                    if (!gconf.StatsOptOut && info is not null)
                    {
                        await using var uow = db.GetDbContext();
                        var user = await uow.GetOrCreateUser(usrMsg.Author);
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
                            await uow.CommandStats.AddAsync(comStats);
                            await uow.SaveChangesAsync();
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error($"Error saving command stats:\n{e}");
            }

            if (success)
            {
                await LogSuccessfulExecution(usrMsg, channel as ITextChannel, exec2, execTime)
                    .ConfigureAwait(false);
                await CommandExecuted(usrMsg, info).ConfigureAwait(false);
                return;
            }

            if (!string.IsNullOrEmpty(error))
            {
                await LogErroredExecution(error, usrMsg, channel as ITextChannel, exec2, execTime);
                if (guild != null)
                {
                    var perms = new PermissionService(client, db, strings, gss);
                    var pc = await perms.GetCacheFor(guild.Id);
                    if (pc != null && pc.Permissions.CheckPermissions(usrMsg, info.Name, info.Module.Name, out _))
                        await CommandErrored(info, channel as ITextChannel, error, usrMsg.Author).ConfigureAwait(false);
                    if (pc == null)
                        await CommandErrored(info, channel as ITextChannel, error, usrMsg.Author).ConfigureAwait(false);
                }
            }
        }
        else
        {
            await OnMessageNoTrigger(usrMsg).ConfigureAwait(false);
        }

        foreach (var exec in lateExecutors) await exec.LateExecute(client, guild, usrMsg).ConfigureAwait(false);
    }

    private async Task<(bool Success, string Error, CommandInfo Info)> ExecuteCommandAsync(CommandContext context,
        string input, int argPos, IServiceProvider serviceProvider,
        MultiMatchHandling multiMatchHandling = MultiMatchHandling.Exception) =>
        await ExecuteCommand(context, input[argPos..], serviceProvider, multiMatchHandling).ConfigureAwait(false);

    private async Task<(bool Success, string Error, CommandInfo Info)> ExecuteCommand(CommandContext context,
        string input, IServiceProvider services,
        MultiMatchHandling multiMatchHandling = MultiMatchHandling.Exception)
    {
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

        var parseResultsDict = new Dictionary<CommandMatch, Discord.Commands.ParseResult>();
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
                        parseResult = Discord.Commands.ParseResult.FromSuccess(argList, paramList);
                        break;
                }
            }

            parseResultsDict[pair.Key] = parseResult;
        }

        // Calculates the 'score' of a command given a parse result
        static float CalculateScore(CommandMatch match, Discord.Commands.ParseResult parseResult)
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
        //return SearchResult.FromError(CommandError.Exception, "You are on a global cooldown.");

        var commandName = cmd.Aliases[0];
        foreach (var exec in LateBlockers)
        {
            if (await exec.TryBlockLate(client, context, cmd.Module.GetTopLevelModule().Name, cmd)
                    .ConfigureAwait(false))
            {
                Log.Information("Late blocking User [{0}] Command: [{1}] in [{2}]", context.User, commandName,
                    exec.GetType().Name);
                return (false, null, cmd);
            }
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
    }
}