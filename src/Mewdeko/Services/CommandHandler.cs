using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.Net;
using Discord.WebSocket;
using Mewdeko._Extensions;
using Mewdeko.Common.Collections;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Services.Settings;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Mewdeko.Services
{

    public class CommandHandler : INService
    {
        public const int GlobalCommandsCooldown = 750;

        private const float _oneThousandth = 1.0f / 1000;
        private readonly Mewdeko _bot;
        private readonly BotConfigService _bss;
        private readonly Timer _clearUsersOnShortCooldown;

        private readonly DiscordSocketClient _client;
        public readonly CommandService _commandService;
        private readonly DbService _db;
        private readonly IServiceProvider _services;
        private IEnumerable<IEarlyBehavior> _earlyBehaviors;
        private IEnumerable<IInputTransformer> _inputTransformers;
        private IEnumerable<ILateBlocker> _lateBlockers;
        private IEnumerable<ILateExecutor> _lateExecutors;

        public CommandHandler(DiscordSocketClient client, DbService db, CommandService commandService,
            BotConfigService bss, Mewdeko bot, IServiceProvider services)
        {
            _client = client;
            _client.ThreadCreated += AttemptJoinThread;
            _commandService = commandService;
            _bss = bss;
            _bot = bot;
            _db = db;
            _services = services;
            _clearUsersOnShortCooldown = new Timer(_ => { UsersOnShortCooldown.Clear(); }, null, GlobalCommandsCooldown,
                GlobalCommandsCooldown);
            _prefixes = bot.AllGuildConfigs
                .Where(x => x.Prefix != null)
                .ToDictionary(x => x.GuildId, x => x.Prefix)
                .ToConcurrent();
        }

        private ConcurrentDictionary<ulong, string> _prefixes { get; } = new();

        //userid/msg count

        public ConcurrentHashSet<ulong> UsersOnShortCooldown { get; } = new();

        public event Func<IUserMessage, CommandInfo, Task> CommandExecuted = delegate { return Task.CompletedTask; };

        public event Func<CommandInfo, ITextChannel, string, Task> CommandErrored = delegate
        {
            return Task.CompletedTask;
        };

        public event Func<IUserMessage, Task> OnMessageNoTrigger = delegate { return Task.CompletedTask; };

        public string GetPrefix(IGuild guild)
        {
            return GetPrefix(guild?.Id);
        }

        public string GetPrefix(ulong? id = null)
        {
            if (id is null || !_prefixes.TryGetValue(id.Value, out var prefix))
                return _bss.Data.Prefix;

            return prefix;
        }

        public string SetDefaultPrefix(string prefix)
        {
            if (string.IsNullOrWhiteSpace(prefix))
                throw new ArgumentNullException(nameof(prefix));

            _bss.ModifyConfig(bs => { bs.Prefix = prefix; });

            return prefix;
        }

        public async Task AttemptJoinThread(SocketThreadChannel chan)
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
                var gc = uow.GuildConfigs.ForId(guild.Id, set => set);
                gc.Prefix = prefix;
                uow.SaveChanges();
            }

            _prefixes.AddOrUpdate(guild.Id, prefix, (key, old) => prefix);

            return prefix;
        }


        public void AddServices(IServiceCollection services)
        {
            _lateBlockers = services
                .Where(x => x.ImplementationType?.GetInterfaces().Contains(typeof(ILateBlocker)) ?? false)
                .Select(x => _services.GetService(x.ImplementationType) as ILateBlocker)
                .OrderByDescending(x => x.Priority)
                .ToArray();

            _lateExecutors = services.Where(x =>
                    x.ImplementationType?.GetInterfaces().Contains(typeof(ILateExecutor)) ?? false)
                .Select(x => _services.GetService(x.ImplementationType) as ILateExecutor)
                .ToArray();

            _inputTransformers = services.Where(x =>
                    x.ImplementationType?.GetInterfaces().Contains(typeof(IInputTransformer)) ?? false)
                .Select(x => _services.GetService(x.ImplementationType) as IInputTransformer)
                .ToArray();

            _earlyBehaviors = services.Where(x =>
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
                    //msg.DeleteAfter(5);
                }
                catch
                {
                    //exclude
                }
            }
        }
            
        public Task StartHandling()
        {
            _client.MessageReceived += msg =>
            {
                var _ = Task.Run(() => MessageReceivedHandler(msg));
                return Task.CompletedTask;
            };
            return Task.CompletedTask;
        }

        private Task LogSuccessfulExecution(IUserMessage usrMsg, ITextChannel channel, params int[] execPoints)
        {
            Log.Information("Command Executed after " +
                            string.Join("/", execPoints.Select(x => (x * _oneThousandth).ToString("F3"))) +
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

        private void LogErroredExecution(string errorMessage, IUserMessage usrMsg, ITextChannel channel,
            params int[] execPoints)
        {
            var errorafter = string.Join("/", execPoints.Select(x => (x * _oneThousandth).ToString("F3")));
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

        public async Task TryRunCommand(SocketGuild guild, IChannel channel, IUserMessage usrMsg)
        {
            var execTime = Environment.TickCount;

            //its nice to have early blockers and early blocking executors separate, but
            //i could also have one interface with priorities, and just put early blockers on
            //highest priority. :thinking:
            foreach (var beh in _earlyBehaviors)
                if (await beh.RunBehavior(_client, guild, usrMsg).ConfigureAwait(false))
                {
                    if (beh.BehaviorType == ModuleBehaviorType.Blocker)
                        Log.Information("Blocked User: [{0}] Message: [{1}] Service: [{2}]", usrMsg.Author,
                            usrMsg.Content, beh.GetType().Name);
                    else if (beh.BehaviorType == ModuleBehaviorType.Executor)
                        Log.Information("User [{0}] executed [{1}] in [{2}] User ID: {3}", usrMsg.Author,
                            usrMsg.Content,
                            beh.GetType().Name, usrMsg.Author.Id);
                    

                    return;
                }

            var exec2 = Environment.TickCount - execTime;


            var messageContent = usrMsg.Content;
            foreach (var exec in _inputTransformers)
            {
                string newContent;
                if ((newContent = await exec.TransformInput(guild, usrMsg.Channel, usrMsg.Author, messageContent)
                    .ConfigureAwait(false)) != messageContent.ToLowerInvariant())
                {
                    messageContent = newContent;
                    break;
                }
            }

            var prefix = GetPrefix(guild?.Id);
            // execute the command and measure the time it took
            if (messageContent.StartsWith(prefix, StringComparison.InvariantCulture) || messageContent.StartsWith($"<@{_client.CurrentUser.Id}> ")|| messageContent.StartsWith($"<@!{_client.CurrentUser.Id}>"))
            {
                if (messageContent.StartsWith($"<@{_client.CurrentUser.Id}>"))
                    prefix = $"<@{_client.CurrentUser.Id}> ";
                if (messageContent.StartsWith($"<@!{_client.CurrentUser.Id}>"))
                    prefix = $"<@!{_client.CurrentUser.Id}> ";
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
                        await CommandErrored(info, channel as ITextChannel, error).ConfigureAwait(false);
                }
            }
            else
            {
                await OnMessageNoTrigger(usrMsg).ConfigureAwait(false);
            }

            foreach (var exec in _lateExecutors) await exec.LateExecute(_client, guild, usrMsg).ConfigureAwait(false);
        }

        public Task<(bool Success, string Error, CommandInfo Info)> ExecuteCommandAsync(CommandContext context,
            string input, int argPos, IServiceProvider serviceProvider,
            MultiMatchHandling multiMatchHandling = MultiMatchHandling.Exception)
        {
            return ExecuteCommand(context, input[argPos..], serviceProvider, multiMatchHandling);
        }


        public async Task<(bool Success, string Error, CommandInfo Info)> ExecuteCommand(CommandContext context,
            string input, IServiceProvider services,
            MultiMatchHandling multiMatchHandling = MultiMatchHandling.Exception)
        {
            var searchResult = _commandService.Search(context, input);
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
            float CalculateScore(CommandMatch match, ParseResult parseResult)
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
                return match.Command.Priority + totalArgsScore * 0.99f;
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

            var commandName = cmd.Aliases.First();
            foreach (var exec in _lateBlockers)
                if (await exec.TryBlockLate(_client, context, cmd.Module.GetTopLevelModule().Name, cmd)
                    .ConfigureAwait(false))
                {
                    Log.Information("Late blocking User [{0}] Command: [{1}] in [{2}]", context.User, commandName,
                        exec.GetType().Name);
                    return (false, null, cmd);
                }

            //If we get this far, at least one parse was successful. Execute the most likely overload.
            var chosenOverload = successfulParses[0];
            var execResult = (ExecuteResult)await chosenOverload.Key
                .ExecuteAsync(context, chosenOverload.Value, services).ConfigureAwait(false);

            if (execResult.Exception != null &&
                (execResult.Exception is not HttpException he || he.DiscordCode == DiscordErrorCode.InsufficientPermissions))
                Log.Warning(execResult.Exception, "Command Error");

            return (true, null, cmd);
        }
    }
}