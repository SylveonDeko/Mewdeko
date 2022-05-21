using Discord;
using Discord.Commands;
using Discord.Interactions;
using Discord.Net;
using Discord.Rest;
using Discord.WebSocket;
using Mewdeko.Common.Collections;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Database.Extensions;
using Mewdeko.Database.Models;
using Mewdeko.Extensions;
using Mewdeko.Modules.Permissions.Common;
using Mewdeko.Modules.Permissions.Services;
using Mewdeko.Services.Settings;
using Mewdeko.Services.strings;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Threading;
using ExecuteResult = Discord.Commands.ExecuteResult;
using IResult = Discord.Interactions.IResult;
using PreconditionResult = Discord.Commands.PreconditionResult;

namespace Mewdeko.Services;

public class CommandHandler : INService
{
    public const int GLOBAL_COMMANDS_COOLDOWN = 750;

    private const float ONE_THOUSANDTH = 1.0f / 1000;
    private readonly Mewdeko _bot;
    private readonly BotConfigService _bss;
    private readonly DiscordSocketClient _client;
    public readonly CommandService CommandService;
    private readonly DbService _db;
    private readonly IServiceProvider _services;
    public static ulong CommandLogChannelId { get; set; }
    // ReSharper disable once NotAccessedField.Local
    private readonly Timer _clearUsersOnShortCooldown;
    private readonly IBotStrings _strings;
    public IEnumerable<IEarlyBehavior> EarlyBehaviors;
    private IEnumerable<IInputTransformer> inputTransformers;
    public IEnumerable<ILateBlocker> LateBlockers;
    private IEnumerable<ILateExecutor> lateExecutors;
    public readonly InteractionService InteractionService;

    public ConcurrentDictionary<ulong, ConcurrentQueue<IUserMessage>> CommandParseQueue { get; } = new();
    public ConcurrentDictionary<ulong, bool> CommandParseLock { get; } = new();

    public CommandHandler(DiscordSocketClient client, DbService db, CommandService commandService,
        BotConfigService bss, Mewdeko bot, IServiceProvider services, IBotStrings strngs,
        InteractionService interactionService)
    {
        InteractionService = interactionService;
        _strings = strngs;
        _client = client;
        CommandService = commandService;
        _bss = bss;
        _bot = bot;
        _db = db;
        _services = services;
        _client.InteractionCreated += TryRunInteraction;
        InteractionService.SlashCommandExecuted += HandleCommands;
        InteractionService.ContextCommandExecuted += HandleContextCommands;
        _clearUsersOnShortCooldown = new Timer(_ => UsersOnShortCooldown.Clear(), null, GLOBAL_COMMANDS_COOLDOWN,
            GLOBAL_COMMANDS_COOLDOWN);
        _client.MessageReceived += MessageReceivedHandler;
    }

    public ConcurrentHashSet<ulong> UsersOnShortCooldown { get; } = new();

    public event Func<IUserMessage, CommandInfo, Task> CommandExecuted = delegate { return Task.CompletedTask; };

    public event Func<CommandInfo, ITextChannel, string, Task> CommandErrored = delegate { return Task.CompletedTask; };

    public event Func<IUserMessage, Task> OnMessageNoTrigger = delegate { return Task.CompletedTask; };

    public Task HandleContextCommands(ContextCommandInfo info, IInteractionContext ctx, IResult result)
    {
        _ = Task.Run(async () =>
        {
            if (!result.IsSuccess)
            {
                await ctx.Interaction.SendEphemeralErrorAsync($"Command failed for the following reason:\n{result.ErrorReason}");
                Log.Warning("Slash Command Errored\n\t" + "User: {0}\n\t" + "Server: {1}\n\t" + "Channel: {2}\n\t" + "Message: {3}\n\t" + "Error: {4}",
                    $"{ctx.User} [{ctx.User.Id}]", // {0}
                    ctx.Guild == null ? "PRIVATE" : $"{ctx.Guild.Name} [{ctx.Guild.Id}]", // {1}
                    ctx.Channel == null ? "PRIVATE" : $"{ctx.Channel.Name} [{ctx.Channel.Id}]", // {2}
                    info.MethodName, result.ErrorReason);
                var tofetch = await _client.Rest.GetChannelAsync(_bss.Data.CommandLogChannel);
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

                    await restChannel.SendMessageAsync(embed: eb.Build());
                }

                if (ctx.Guild is null) return;
                {
                    if (info.MethodName.ToLower() is "confess" or "confessreport")
                        return;

                    var gc = _bot.GetGuildConfig(ctx.Guild.Id);
                    if (gc.CommandLogChannel is 0) return;
                    var channel = await ctx.Guild.GetTextChannelAsync(gc.CommandLogChannel);
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

                    await channel.SendMessageAsync(embed: eb.Build());
                }
                return;
            }

            var chan = ctx.Channel as ITextChannel;
            Log.Information("Slash Command Executed" + "\n\t" + "User: {0}\n\t" + "Server: {1}\n\t" + "Channel: {2}\n\t" + "Module: {3}\n\t" + "Command: {4}",
                $"{ctx.User} [{ctx.User.Id}]", // {0}
                chan == null ? "PRIVATE" : $"{chan.Guild.Name} [{chan.Guild.Id}]", // {1}
                chan == null ? "PRIVATE" : $"{chan.Name} [{chan.Id}]", // {2}
                info.Module.SlashGroupName, info.MethodName); // {3}

            var tofetch1 = await _client.Rest.GetChannelAsync(_bss.Data.CommandLogChannel);
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

                await restChannel1.SendMessageAsync(embed: eb.Build());
            }

            if (ctx.Guild is null) return;
            {
                if (info.MethodName.ToLower() is "confess" or "confessreport")
                    return;

                var gc = _bot.GetGuildConfig(ctx.Guild.Id);
                if (gc.CommandLogChannel is 0) return;
                var channel = await ctx.Guild.GetTextChannelAsync(gc.CommandLogChannel);
                if (channel is null)
                    return;
                var eb = new EmbedBuilder()
                         .WithOkColor()
                         .WithTitle("Slash Command Executed.")
                         .AddField("Module", info.Module.Name ?? "None")
                         .AddField("Command", info.Name)
                         .AddField("User", $"{ctx.User.Mention} `{ctx.User.Id}`")
                         .AddField("Channel", $"{ctx.Channel.Name} `{ctx.Channel.Id}`");

                await channel.SendMessageAsync(embed: eb.Build());
            }
        });
        return Task.CompletedTask;
    }
    private Task HandleCommands(SlashCommandInfo slashInfo, IInteractionContext ctx, IResult result)
    {
        _ = Task.Run(async () =>
        {
            if (!result.IsSuccess)
            {
                await ctx.Interaction.SendEphemeralErrorAsync($"Command failed for the following reason:\n{result.ErrorReason}");
                Log.Warning("Slash Command Errored\n\t" + "User: {0}\n\t" + "Server: {1}\n\t" + "Channel: {2}\n\t" + "Message: {3}\n\t" + "Error: {4}",
                    $"{ctx.User} [{ctx.User.Id}]", // {0}
                    ctx.Guild == null ? "PRIVATE" : $"{ctx.Guild.Name} [{ctx.Guild.Id}]", // {1}
                    ctx.Channel == null ? "PRIVATE" : $"{ctx.Channel.Name} [{ctx.Channel.Id}]", // {2}
                    slashInfo.MethodName, result.ErrorReason);

                var tofetch = await _client.Rest.GetChannelAsync(_bss.Data.CommandLogChannel);
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

                    await restChannel.SendMessageAsync(embed: eb.Build());
                }

                if (ctx.Guild is null) return;
                {
                    if (slashInfo.MethodName.ToLower() is "confess" or "confessreport")
                        return;

                    var gc = _bot.GetGuildConfig(ctx.Guild.Id);
                    if (gc.CommandLogChannel is 0) return;
                    var channel = await ctx.Guild.GetTextChannelAsync(gc.CommandLogChannel);
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

                    await channel.SendMessageAsync(embed: eb.Build());
                }
                return;
            }

            var chan = ctx.Channel as ITextChannel;
            Log.Information("Slash Command Executed" + "\n\t" + "User: {0}\n\t" + "Server: {1}\n\t" + "Channel: {2}\n\t" + "Module: {3}\n\t" + "Command: {4}",
                $"{ctx.User} [{ctx.User.Id}]", // {0}
                chan == null ? "PRIVATE" : $"{chan.Guild.Name} [{chan.Guild.Id}]", // {1}
                chan == null ? "PRIVATE" : $"{chan.Name} [{chan.Id}]", // {2}
                slashInfo.Module.SlashGroupName, slashInfo.MethodName); // {3}

            var tofetch1 = await _client.Rest.GetChannelAsync(_bss.Data.CommandLogChannel);
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

                await restChannel1.SendMessageAsync(embed: eb.Build());
            }

            if (ctx.Guild is null) return;
            {
                if (slashInfo.MethodName.ToLower() is "confess" or "confessreport")
                    return;

                var gc = _bot.GetGuildConfig(ctx.Guild.Id);
                if (gc.CommandLogChannel is 0) return;
                var channel = await ctx.Guild.GetTextChannelAsync(gc.CommandLogChannel);
                if (channel is null)
                    return;
                var eb = new EmbedBuilder()
                         .WithOkColor()
                         .WithTitle("Slash Command Executed.")
                         .AddField("Module", slashInfo.Module.Name ?? "None")
                         .AddField("Command", slashInfo.Name)
                         .AddField("User", $"{ctx.User.Mention} `{ctx.User.Id}`")
                         .AddField("Channel", $"{ctx.Channel.Name} `{ctx.Channel.Id}`");

                await channel.SendMessageAsync(embed: eb.Build());
            }
        });
        return Task.CompletedTask;
    }
    private Task TryRunInteraction(SocketInteraction interaction)
    {
        _ = Task.Run(async () =>
        {
            var ctx = new SocketInteractionContext(_client, interaction);
            var blacklistService = _services.GetService<BlacklistService>();
            var cb = new ComponentBuilder().WithButton("Support Server", null, ButtonStyle.Link,
                url: "https://discord.gg/mewdeko").Build();
            foreach (var bl in blacklistService.BlacklistEntries)
            {
                if (ctx.Guild != null && bl.Type == BlacklistType.Server && bl.ItemId == ctx.Guild.Id)
                {
                    await ctx.Interaction.RespondAsync($"*This guild is blacklisted from Mewdeko for **{bl.Reason}**! You can visit the support server below to try and resolve this.*", components: cb);
                    return;
                }

                if (bl.Type == BlacklistType.User && bl.ItemId == ctx.User.Id)
                {
                    await ctx.Interaction.RespondAsync($"*You are blacklisted from Mewdeko for **{bl.Reason}**! You can visit the support server below to try and resolve this.*", ephemeral: true, components: cb);
                    return;
                }
            }
            await InteractionService.ExecuteCommandAsync(ctx, _services);
        });
        return Task.CompletedTask;
    }

    public string GetPrefix(IGuild? guild) => GetPrefix(guild?.Id);

    public string GetPrefix(ulong? id = null)
    {
        if (id is null)
            return _bss.GetSetting("prefix");
        return _bot.GetGuildConfig(id.Value).Prefix ??= _bss.GetSetting("prefix");
    }

    public string SetDefaultPrefix(string prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix))
            throw new ArgumentNullException(nameof(prefix));

        _bss.ModifyConfig(bs => bs.Prefix = prefix);

        return prefix;
    }

    public string SetPrefix(IGuild guild, string prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix))
            throw new ArgumentNullException(nameof(prefix));
        if (guild == null)
            throw new ArgumentNullException(nameof(guild));

        using var uow = _db.GetDbContext();
        var gc = uow.ForGuildId(guild.Id, set => set);
        gc.Prefix = prefix;
        uow.SaveChanges();
        _bot.UpdateGuildConfig(guild.Id, gc);
        return prefix;
    }

    public void AddServices(IServiceCollection services)
    {
        LateBlockers = services
            .Where(x => x.ImplementationType?.GetInterfaces().Contains(typeof(ILateBlocker)) ?? false)
            .Select(x => _services.GetService(x.ImplementationType) as ILateBlocker)
            .OrderByDescending(x => x.Priority)
            .ToArray();

        lateExecutors = services.Where(x =>
                x.ImplementationType?.GetInterfaces().Contains(typeof(ILateExecutor)) ?? false)
            .Select(x => _services.GetService(x.ImplementationType) as ILateExecutor)
            .ToArray();

        inputTransformers = services.Where(x =>
                x.ImplementationType?.GetInterfaces().Contains(typeof(IInputTransformer)) ?? false)
            .Select(x => _services.GetService(x.ImplementationType) as IInputTransformer)
            .ToArray();

        EarlyBehaviors = services.Where(x =>
                x.ImplementationType?.GetInterfaces().Contains(typeof(IEarlyBehavior)) ?? false)
            .Select(x => _services.GetService(x.ImplementationType) as IEarlyBehavior)
            .ToArray();
    }

    public async Task ExecuteExternal(ulong? guildId, ulong channelId, string commandText)
    {
        if (guildId != null)
        {
            var guild = _client.GetGuild(guildId.Value);
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

    private Task LogSuccessfulExecution(IUserMessage usrMsg, ITextChannel? channel, params int[] execPoints)
    {
        _ = Task.Run(async () =>
        {
            Log.Information(
                "Command Executed after "
                + string.Join("/", execPoints.Select(x => (x * ONE_THOUSANDTH).ToString("F3")))
                + "s\n\t"
                + "User: {0}\n\t"
                + "Server: {1}\n\t"
                + "Channel: {2}\n\t"
                + "Message: {3}", $"{usrMsg.Author} [{usrMsg.Author.Id}]", // {0}
                channel == null ? "PRIVATE" : $"{channel.Guild.Name} [{channel.Guild.Id}]", // {1}
                channel == null ? "PRIVATE" : $"{channel.Name} [{channel.Id}]", // {2}
                usrMsg.Content); // {3}
            var toFetch = await _client.Rest.GetChannelAsync(_bss.Data.CommandLogChannel);
            if (toFetch is RestTextChannel restChannel)
            {
                var eb = new EmbedBuilder()
                    .WithOkColor()
                    .WithTitle("Text Command Executed")
                    .AddField("Executed Time", string.Join("/", execPoints.Select(x => (x * ONE_THOUSANDTH).ToString("F3"))))
                    .AddField("User", $"{usrMsg.Author.Mention} {usrMsg.Author} {usrMsg.Author.Id}")
                    .AddField("Guild", channel == null ? "PRIVATE" : $"{channel.Guild.Name} `{channel.Guild.Id}`")
                    .AddField("Channel", channel == null ? "PRIVATE" : $"{channel.Name} `{channel.Id}`")
                    .AddField("Message", usrMsg.Content.TrimTo(1000));

                await restChannel.SendMessageAsync(embed: eb.Build());
            }

            if (channel?.Guild is null) return;
            var guildChannel = _bot.GetGuildConfig(channel.Guild.Id).CommandLogChannel;
            if (guildChannel == 0) return;
            var toSend = await _client.Rest.GetChannelAsync(guildChannel);
            if (toSend is RestTextChannel restTextChannel)
            {
                var eb = new EmbedBuilder()
                         .WithOkColor()
                         .WithTitle("Text Command Executed")
                         .AddField("Executed Time", string.Join("/", execPoints.Select(x => (x * ONE_THOUSANDTH).ToString("F3"))))
                         .AddField("User", $"{usrMsg.Author.Mention} {usrMsg.Author} {usrMsg.Author.Id}")
                         .AddField("Channel", channel == null ? "PRIVATE" : $"{channel.Name} `{channel.Id}`")
                         .AddField("Message", usrMsg.Content.TrimTo(1000));

                await restTextChannel.SendMessageAsync(embed: eb.Build());
            }
        });
        return Task.CompletedTask;
    }

    private void LogErroredExecution(string errorMessage, IUserMessage usrMsg, ITextChannel? channel, params int[] execPoints)
    {
        _ = Task.Run(async () =>
        {
            var errorafter = string.Join("/", execPoints.Select(x => (x * ONE_THOUSANDTH).ToString("F3")));
            Log.Warning($"Command Errored after {errorafter}\n\t" + "User: {0}\n\t" + "Server: {1}\n\t" + "Channel: {2}\n\t" + "Message: {3}\n\t" + "Error: {4}",
                $"{usrMsg.Author} [{usrMsg.Author.Id}]", // {0}
                channel == null ? "PRIVATE" : $"{channel.Guild.Name} [{channel.Guild.Id}]", // {1}
                channel == null ? "PRIVATE" : $"{channel.Name} [{channel.Id}]", // {2}
                usrMsg.Content, errorMessage);

            var toFetch = await _client.Rest.GetChannelAsync(_bss.Data.CommandLogChannel);
            if (toFetch is RestTextChannel restChannel)
            {
                var eb = new EmbedBuilder()
                         .WithOkColor()
                         .WithTitle("Text Command Errored")
                         .AddField("Error Reason", errorMessage)
                         .AddField("Errored Time", execPoints.Select(x => (x * ONE_THOUSANDTH).ToString("F3")))
                         .AddField("User", $"{usrMsg.Author} {usrMsg.Author.Id}")
                         .AddField("Guild", channel == null ? "PRIVATE" : $"{channel.Guild.Name} `{channel.Guild.Id}`")
                         .AddField("Channel", channel == null ? "PRIVATE" : $"{channel.Name} `{channel.Id}`")
                         .AddField("Message", usrMsg.Content.TrimTo(1000));

                await restChannel.SendMessageAsync(embed: eb.Build());
            }
        });
    }

    public Task MessageReceivedHandler(SocketMessage msg)
    {
        try
        {
            if (msg.Author.IsBot ||
                !_bot.Ready.Task.IsCompleted) //no bots, wait until bot connected and initialized
            {
                return Task.CompletedTask;
            }

            if (msg is not SocketUserMessage usrMsg)
                return Task.CompletedTask;

            AddCommandToParseQueue(usrMsg);
            _ = Task.Run(() => ExecuteCommandsInChannelAsync(usrMsg.Channel.Id));
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error in CommandHandler");
            if (ex.InnerException != null)
                Log.Warning(ex.InnerException, "Inner Exception of the error in CommandHandler");
        }
        return Task.CompletedTask;
    }

    public void AddCommandToParseQueue(IUserMessage usrMsg) => CommandParseQueue.AddOrUpdate(usrMsg.Channel.Id,
        x => new ConcurrentQueue<IUserMessage>(new List<IUserMessage> { usrMsg }), (_, y) =>
        {
            y.Enqueue(usrMsg);
            return y;
        });

    public async Task<bool> ExecuteCommandsInChannelAsync(ulong channelId)
    {
        try
        {
            if (CommandParseLock.GetValueOrDefault(channelId, false)) return false;
            if (CommandParseQueue.GetValueOrDefault(channelId) is null || CommandParseQueue[channelId].Count == 0) return false;
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

    private async Task TryRunCommand(IGuild guild, IChannel channel, IUserMessage usrMsg)
    {
        var execTime = Environment.TickCount;

        //its nice to have early blockers and early blocking executors separate, but
        //i could also have one interface with priorities, and just put early blockers on
        //highest priority. :thinking:
        foreach (var beh in EarlyBehaviors)
        {
            if (await beh.RunBehavior(_client, guild, usrMsg).ConfigureAwait(false))
            {
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
        var prefix = GetPrefix(guild?.Id);
        // execute the command and measure the time it took
        if (messageContent.StartsWith(prefix, StringComparison.InvariantCulture) ||
            messageContent.StartsWith($"<@{_client.CurrentUser.Id}> ") ||
            messageContent.StartsWith($"<@!{_client.CurrentUser.Id}>"))
        {
            if (messageContent.StartsWith($"<@{_client.CurrentUser.Id}>"))
                prefix = $"<@{_client.CurrentUser.Id}> ";
            if (messageContent.StartsWith($"<@!{_client.CurrentUser.Id}>"))
                prefix = $"<@!{_client.CurrentUser.Id}> ";
            if (messageContent == $"<@{_client.CurrentUser.Id}>"
                || messageContent == $"<@!{_client.CurrentUser.Id}>")
            {
                return;
            }

            var (success, error, info) = await ExecuteCommandAsync(new CommandContext(_client, usrMsg),
                    messageContent, prefix.Length, _services, MultiMatchHandling.Best)
                .ConfigureAwait(false);
            execTime = Environment.TickCount - execTime;

            if (success)
            {
                await LogSuccessfulExecution(usrMsg, channel as ITextChannel, exec2, execTime)
                    .ConfigureAwait(false);
                await CommandExecuted(usrMsg, info).ConfigureAwait(false);
                return;
            }

            if (error != null)
            {
                LogErroredExecution(error, usrMsg, channel as ITextChannel, exec2, execTime);
                if (guild != null)
                {
                    var perms = new PermissionService(_client, _db, this, _strings);
                    var pc = perms.GetCacheFor(guild.Id);
                    if (pc != null && pc.Permissions.CheckPermissions(usrMsg, info.Name, info.Module.Name, out _))
                        await CommandErrored(info, channel as ITextChannel, error).ConfigureAwait(false);
                    if (pc == null)
                        await CommandErrored(info, channel as ITextChannel, error).ConfigureAwait(false);
                }
            }
        }
        else
        {
            await OnMessageNoTrigger(usrMsg).ConfigureAwait(false);
        }

        foreach (var exec in lateExecutors) await exec.LateExecute(_client, guild, usrMsg).ConfigureAwait(false);
    }

    private async Task<(bool Success, string Error, CommandInfo Info)> ExecuteCommandAsync(CommandContext context,
        string input, int argPos, IServiceProvider serviceProvider,
        MultiMatchHandling multiMatchHandling = MultiMatchHandling.Exception) =>
        await ExecuteCommand(context, input[argPos..], serviceProvider, multiMatchHandling);

    private async Task<(bool Success, string Error, CommandInfo Info)> ExecuteCommand(CommandContext context,
        string input, IServiceProvider services,
        MultiMatchHandling multiMatchHandling = MultiMatchHandling.Exception)
    {
        var searchResult = CommandService.Search(context, input);
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
                                                                            .Select(x => x.Values.OrderByDescending(y => y.Score).First()).ToImmutableArray();
                        IReadOnlyList<TypeReaderValue> paramList = parseResult.ParamValues
                                                                              .Select(x => x.Values.OrderByDescending(y => y.Score).First()).ToImmutableArray();
                        parseResult = ParseResult.FromSuccess(argList, paramList);
                        break;
                }
            }

            parseResultsDict[pair.Key] = parseResult;
        }

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
            if (await exec.TryBlockLate(_client, context, cmd.Module.GetTopLevelModule().Name, cmd)
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
