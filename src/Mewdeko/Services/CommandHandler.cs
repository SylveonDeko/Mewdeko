using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using Discord;
using Discord.Commands;
using Discord.Interactions;
using Discord.Net;
using Discord.WebSocket;
using Mewdeko._Extensions;
using Mewdeko.Common.Collections;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Database;
using Mewdeko.Database.Extensions;
using Mewdeko.Modules.Administration.Services;
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
    public const int GLOBAL_COMMANDS_COOLDOWN = 750;

    private const float ONE_THOUSANDTH = 1.0f / 1000;
    private readonly Mewdeko _bot;
    private readonly BotConfigService _bss;
    private readonly Timer _clearUsersOnShortCooldown;

    private readonly DiscordSocketClient _client;
    public readonly CommandService CommandService;
    private readonly DiscordPermOverrideService dpo;
    private readonly DbService _db;
    private readonly IServiceProvider _services;
    private readonly IBotStrings _strings;
    public IEnumerable<IEarlyBehavior> earlyBehaviors;
    private IEnumerable<IInputTransformer> inputTransformers;
    public IEnumerable<ILateBlocker> lateBlockers;
    private IEnumerable<ILateExecutor> lateExecutors;
    public InteractionService InteractionService;

    public CommandHandler(DiscordSocketClient client, DbService db, CommandService commandService,
        BotConfigService bss, Mewdeko bot, IServiceProvider services, IBotStrings strngs,
        InteractionService interactionService, DiscordPermOverrideService dpos)
    {
        dpo = dpos;
        InteractionService = interactionService;
        _strings = strngs;
        _client = client;
        _client.ThreadCreated += AttemptJoinThread;
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
        Prefixes = bot.AllGuildConfigs
            .Where(x => x.Prefix != null)
            .ToDictionary(x => x.GuildId, x => x.Prefix)
            .ToConcurrent();
        _client.MessageReceived += msg =>
        {
            var _ = Task.Run(async () => await MessageReceivedHandler(msg));
            return Task.CompletedTask;
        };
    }

    private ConcurrentDictionary<ulong, string> Prefixes { get; } = new();

    public ConcurrentHashSet<ulong> UsersOnShortCooldown { get; } = new();

    public event Func<IUserMessage, CommandInfo, Task> CommandExecuted = delegate { return Task.CompletedTask; };

    public event Func<CommandInfo, ITextChannel, string, Task> CommandErrored = delegate { return Task.CompletedTask; };
    
    public event Func<IUserMessage, Task> OnMessageNoTrigger = delegate { return Task.CompletedTask; };

    public async Task HandleContextCommands(ContextCommandInfo info, IInteractionContext ctx, IResult result )
    {
        if (!result.IsSuccess)
        {
            await ctx.Interaction.SendEphemeralErrorAsync(
                $"Command failed for the following reason:\n{result.ErrorReason}");
            Log.Warning($"Slash Command Errored\n\t" +
                        "User: {0}\n\t" +
                        "Server: {1}\n\t" +
                        "Channel: {2}\n\t" +
                        "Message: {3}\n\t" +
                        "Error: {4}",
                $"{ctx.User} [{ctx.User.Id}]", // {0}
                ctx.Guild == null ? "PRIVATE" : $"{ctx.Guild.Name} [{ctx.Guild.Id}]", // {1}
                ctx.Channel == null ? "PRIVATE" : $"{ctx.Channel.Name} [{ctx.Channel.Id}]", // {2}
                info.MethodName,
                result.ErrorReason);
            return;
        }
        var chan = ctx.Channel as ITextChannel;
        Log.Information(
            "Slash Command Executed"
            + "\n\t"
            + "User: {0}\n\t"
            + "Server: {1}\n\t"
            + "Channel: {2}\n\t"
            + "Module: {3}\n\t"
            + "Command: {4}", $"{ctx.User} [{ctx.User.Id}]", // {0}
            chan == null ? "PRIVATE" : $"{chan.Guild.Name} [{chan.Guild.Id}]", // {1}
            chan == null ? "PRIVATE" : $"{chan.Name} [{chan.Id}]", // {2}
            info.Module.SlashGroupName, info.MethodName); // {3}
    }
    private async Task HandleCommands(SlashCommandInfo slashInfo, IInteractionContext ctx, IResult result)
    {
        if (!result.IsSuccess)
        {
            await ctx.Interaction.SendEphemeralErrorAsync(
                $"Command failed for the following reason:\n{result.ErrorReason}");
            Log.Warning($"Slash Command Errored\n\t" +
                        "User: {0}\n\t" +
                        "Server: {1}\n\t" +
                        "Channel: {2}\n\t" +
                        "Message: {3}\n\t" +
                        "Error: {4}",
                $"{ctx.User} [{ctx.User.Id}]", // {0}
                ctx.Guild == null ? "PRIVATE" : $"{ctx.Guild.Name} [{ctx.Guild.Id}]", // {1}
                ctx.Channel == null ? "PRIVATE" : $"{ctx.Channel.Name} [{ctx.Channel.Id}]", // {2}
                slashInfo.MethodName,
                result.ErrorReason);
            return;
        }
        var chan = ctx.Channel as ITextChannel;
            Log.Information(
                "Slash Command Executed"
                + "\n\t"
                + "User: {0}\n\t"
                + "Server: {1}\n\t"
                + "Channel: {2}\n\t"
                + "Module: {3}\n\t"
                + "Command: {4}", $"{ctx.User} [{ctx.User.Id}]", // {0}
                chan == null ? "PRIVATE" : $"{chan.Guild.Name} [{chan.Guild.Id}]", // {1}
                chan == null ? "PRIVATE" : $"{chan.Name} [{chan.Id}]", // {2}
                slashInfo.Module.SlashGroupName, slashInfo.MethodName); // {3}
    }
    private async Task TryRunInteraction(SocketInteraction interaction) 
    {
        var ctx = new SocketInteractionContext(_client, interaction);
        await InteractionService.ExecuteCommandAsync(ctx, _services);
    }

    public string GetPrefix(IGuild guild) => GetPrefix(guild?.Id);

    public string GetPrefix(ulong? id = null)
    {
        if (id is null || !Prefixes.TryGetValue(id.Value, out var prefix))
            return _bss.Data.Prefix;

        return prefix;
    }

    public string SetDefaultPrefix(string prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix))
            throw new ArgumentNullException(nameof(prefix));

        _bss.ModifyConfig(bs => bs.Prefix = prefix);

        return prefix;
    }

    private static async Task AttemptJoinThread(SocketThreadChannel chan)
    {
        try
        {
            await chan.JoinAsync();
        }
        catch
        {
            //exclude
        }
    }

    public string SetPrefix(IGuild guild, string prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix))
            throw new ArgumentNullException(nameof(prefix));
        if (guild == null)
            throw new ArgumentNullException(nameof(guild));

        using (var uow = _db.GetDbContext())
        {
            var gc = uow.ForGuildId(guild.Id, set => set);
            gc.Prefix = prefix;
            uow.SaveChanges();
        }

        Prefixes.AddOrUpdate(guild.Id, prefix, (_, _) => prefix);

        return prefix;
    }


    public void AddServices(IServiceCollection services)
    {
        lateBlockers = services
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

        earlyBehaviors = services.Where(x =>
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
                msg = (IUserMessage) await channel.GetMessageAsync(msg.Id).ConfigureAwait(false);
                await TryRunCommand(guild, channel, msg).ConfigureAwait(false);
            }
            catch
            {
                //exclude
            }
        }
    }
    

    private static Task LogSuccessfulExecution(IUserMessage usrMsg, ITextChannel channel, params int[] execPoints)
    {
        Log.Information("Command Executed after " +
                        string.Join("/", execPoints.Select(x => (x * ONE_THOUSANDTH).ToString("F3"))) +
                        "s\n\t" +
                        "User: {0}\n\t" +
                        "Server: {1}\n\t" +
                        "Channel: {2}\n\t" +
                        "Message: {3}",
            usrMsg.Author + " [" + usrMsg.Author.Id + "]", // {0}
            channel == null ? "PRIVATE" : channel.Guild.Name + " [" + channel.Guild.Id + "]", // {1}
            channel == null ? "PRIVATE" : channel.Name + " [" + channel.Id + "]", // {2}
            usrMsg.Content // {3}
        );
        return Task.CompletedTask;
    }

    private static void LogErroredExecution(string errorMessage, IUserMessage usrMsg, ITextChannel channel,
        params int[] execPoints)
    {
        var errorafter = string.Join("/", execPoints.Select(x => (x * ONE_THOUSANDTH).ToString("F3")));
        Log.Warning($"Command Errored after {errorafter}\n\t" +
                    "User: {0}\n\t" +
                    "Server: {1}\n\t" +
                    "Channel: {2}\n\t" +
                    "Message: {3}\n\t" +
                    "Error: {4}",
            $"{usrMsg.Author} [{usrMsg.Author.Id}]", // {0}
            channel == null ? "PRIVATE" : $"{channel.Guild.Name} [{channel.Guild.Id}]", // {1}
            channel == null ? "PRIVATE" : $"{channel.Name} [{channel.Id}]", // {2}
            usrMsg.Content,
            errorMessage);
    }

    private async Task MessageReceivedHandler(SocketMessage msg)
    {
        try
        {

            if (msg.Author.IsBot ||
                !_bot.Ready.Task.IsCompleted) //no bots, wait until bot connected and initialized
                return;

            if (msg is not SocketUserMessage usrMsg)
                return;

            var channel = msg.Channel;
            var guild = (msg.Channel as SocketTextChannel)?.Guild;

            await TryRunCommand(guild, channel, usrMsg).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error in CommandHandler");
            if (ex.InnerException != null)
                Log.Warning(ex.InnerException, "Inner Exception of the error in CommandHandler");
        }
    }

    private async Task TryRunCommand(SocketGuild guild, IChannel channel, IUserMessage usrMsg)
    {
        var execTime = Environment.TickCount;

        //its nice to have early blockers and early blocking executors separate, but
        //i could also have one interface with priorities, and just put early blockers on
        //highest priority. :thinking:
        foreach (var beh in earlyBehaviors)
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

        var exec2 = Environment.TickCount - execTime;


        var messageContent = usrMsg.Content;
        foreach (var exec in inputTransformers)
        {
            string newContent;
            if ((newContent = await exec.TransformInput(guild, usrMsg.Channel, usrMsg.Author, messageContent)
                                        .ConfigureAwait(false))
                == messageContent.ToLowerInvariant()) continue;
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
                return;
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

    private Task<(bool Success, string Error, CommandInfo Info)> ExecuteCommandAsync(CommandContext context,
        string input, int argPos, IServiceProvider serviceProvider,
        MultiMatchHandling multiMatchHandling = MultiMatchHandling.Exception) =>
        ExecuteCommand(context, input[argPos..], serviceProvider, multiMatchHandling);


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
            preconditionResults[match] =
                await match.Command.CheckPreconditionsAsync(context, services).ConfigureAwait(false);

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
                IReadOnlyList<TypeReaderValue> argList, paramList;
                switch (multiMatchHandling)
                {
                    case MultiMatchHandling.Best:
                        argList = parseResult.ArgValues
                            .Select(x => x.Values.OrderByDescending(y => y.Score).First()).ToImmutableArray();
                        paramList = parseResult.ParamValues
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
        // GlobalCommandsCooldown constant (miliseconds)
        if (!UsersOnShortCooldown.Add(context.Message.Author.Id))
            return (false, null, cmd);
        //return SearchResult.FromError(CommandError.Exception, "You are on a global cooldown.");

        var commandName = cmd.Aliases[0];
        foreach (var exec in lateBlockers)
            if (await exec.TryBlockLate(_client, context, cmd.Module.GetTopLevelModule().Name, cmd)
                    .ConfigureAwait(false))
            {
                Log.Information("Late blocking User [{0}] Command: [{1}] in [{2}]", context.User, commandName,
                    exec.GetType().Name);
                return (false, null, cmd);
            }

        //If we get this far, at least one parse was successful. Execute the most likely overload.
        var chosenOverload = successfulParses[0];
        var execResult = (ExecuteResult) await chosenOverload.Key
            .ExecuteAsync(context, chosenOverload.Value, services).ConfigureAwait(false);

        if (execResult.Exception != null &&
            (execResult.Exception is not HttpException he ||
             he.DiscordCode == DiscordErrorCode.InsufficientPermissions))
            Log.Warning(execResult.Exception, "Command Error");

        return (true, null, cmd);
    }
}