using Discord;
using Discord.Commands;
using Discord.Net;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Mewdeko.Common.Collections;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Extensions;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Discord.Webhook;
using Mewdeko.Core.Common.Configs;

namespace Mewdeko.Core.Services
{
    public class GuildUserComparer : IEqualityComparer<IGuildUser>
    {
        public bool Equals(IGuildUser x, IGuildUser y) => x.Id == y.Id;

        public int GetHashCode(IGuildUser obj) => obj.Id.GetHashCode();
    }

    public class CommandHandler : INService
    {
        public const int GlobalCommandsCooldown = 750;

        private readonly DiscordSocketClient _client;
        private readonly CommandService _commandService;
        private readonly BotSettingsService _bss;
        private readonly Logger _log;
        private readonly Mewdeko _bot;
        private IServiceProvider _services;
        private IEnumerable<IEarlyBehavior> _earlyBehaviors;
        private IEnumerable<IInputTransformer> _inputTransformers;
        private IEnumerable<ILateBlocker> _lateBlockers;
        private IEnumerable<ILateExecutor> _lateExecutors;
        
        private ConcurrentDictionary<ulong, string> _prefixes { get; } = new ConcurrentDictionary<ulong, string>();
        private ConcurrentDictionary<ulong, ulong> _warnlogchannelids { get; } = new ConcurrentDictionary<ulong, ulong>();
        private ConcurrentDictionary<ulong, ulong> _ticketchannelids { get; } = new ConcurrentDictionary<ulong, ulong>();
        private ConcurrentDictionary<ulong, ulong> _miniwarnlogchannelids { get; } = new ConcurrentDictionary<ulong, ulong>();
        private ConcurrentDictionary<ulong, ulong> _sugchans { get; } = new ConcurrentDictionary<ulong, ulong>();
        private ConcurrentDictionary<ulong, ulong> _sugroles { get; } = new ConcurrentDictionary<ulong, ulong>();
        private ConcurrentDictionary<ulong, int> _removerolesonmute { get; } = new ConcurrentDictionary<ulong, int>();
        private ConcurrentDictionary<ulong, int> _fwarn { get; } = new ConcurrentDictionary<ulong, int>();
        private ConcurrentDictionary<ulong, ulong> _snum { get; } = new ConcurrentDictionary<ulong, ulong>();
        private ConcurrentDictionary<ulong, int> _invwarn { get; } = new ConcurrentDictionary<ulong, int>();
        public event Func<IUserMessage, CommandInfo, Task> CommandExecuted = delegate { return Task.CompletedTask; };
        public event Func<CommandInfo, ITextChannel, string, Task> CommandErrored = delegate { return Task.CompletedTask; };
        public event Func<IUserMessage, Task> OnMessageNoTrigger = delegate { return Task.CompletedTask; };
        public string DefaultPrefix { get; private set; }
        //userid/msg count
        public ConcurrentDictionary<ulong, uint> UserMessagesSent { get; } = new ConcurrentDictionary<ulong, uint>();

        public ConcurrentHashSet<ulong> UsersOnShortCooldown { get; } = new ConcurrentHashSet<ulong>();
        private readonly Timer _clearUsersOnShortCooldown;

        public CommandHandler(DiscordSocketClient client, DbService db,
            IBotConfigProvider bcp, CommandService commandService, BotSettingsService bss,
            IBotCredentials credentials, Mewdeko bot, IServiceProvider services)
        {
            _client = client;
            _commandService = commandService;
            _bss = bss;
            _bot = bot;
            _db = db;
            _bcp = bcp;
            _services = services;

            _log = LogManager.GetCurrentClassLogger();

            _clearUsersOnShortCooldown = new Timer(_ =>
            {
                UsersOnShortCooldown.Clear();
            }, null, GlobalCommandsCooldown, GlobalCommandsCooldown);
            
            _prefixes = bot.AllGuildConfigs
                .Where(x => x.Prefix != null)
                .ToDictionary(x => x.GuildId, x => x.Prefix)
                .ToConcurrent();
            _warnlogchannelids = bot.AllGuildConfigs
                .Where(x => x.WarnlogChannelId != 0)
                .ToDictionary(x => x.GuildId, x => x.WarnlogChannelId)
                .ToConcurrent();
            _miniwarnlogchannelids = bot.AllGuildConfigs
                .Where(x => x.MiniWarnlogChannelId != 0)
                .ToDictionary(x => x.GuildId, x => x.MiniWarnlogChannelId)
                .ToConcurrent();
            _ticketchannelids = bot.AllGuildConfigs
                .Where(x => x.TicketCategory != 0)
                .ToDictionary(x => x.GuildId, x => x.TicketCategory)
                .ToConcurrent();
            _removerolesonmute = bot.AllGuildConfigs
                .ToDictionary(x => x.GuildId, x => x.removeroles)
                .ToConcurrent();
            _fwarn = bot.AllGuildConfigs
                .ToDictionary(x => x.GuildId, x => x.fwarn)
                .ToConcurrent();
            _invwarn = bot.AllGuildConfigs
                .ToDictionary(x => x.GuildId, x => x.invwarn)
                .ToConcurrent();
            _snum = bot.AllGuildConfigs
                .ToDictionary(x => x.GuildId, x => x.sugnum)
                .ToConcurrent();
            _sugchans = bot.AllGuildConfigs
                .ToDictionary(x => x.GuildId, x => x.sugchan)
                .ToConcurrent();
            _sugroles = bot.AllGuildConfigs
                .ToDictionary(x => x.GuildId, x => x.SuggestRole)
                .ToConcurrent();
        }

        public string GetPrefix(IGuild guild) => GetPrefix(guild?.Id);

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

            _bss.ModifyConfig(bs =>
            {
                bs.Prefix = prefix;
            });

            return prefix;
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
        public ulong GetWarnlogChannel(ulong? id)
        {
            if (id == null || !_warnlogchannelids.TryGetValue(id.Value, out var warnlogchannel))
                return 0;

            return warnlogchannel;
        }
        public ulong GetMWarnlogChannel(ulong? id)
        {
            if (id == null || !_miniwarnlogchannelids.TryGetValue(id.Value, out var mwarnlogchannel))
                return 0;

            return mwarnlogchannel;
        }
        public ulong GetTicketCategory(ulong? id)
        {
            if (id == null || !_ticketchannelids.TryGetValue(id.Value, out var ticketcat))
                return 0;

            return ticketcat;
        }
        public ulong GetSuggestionChannel(ulong? id)
        {
            if (id == null || !_sugchans.TryGetValue(id.Value, out var SugChan))
                return 0;

            return SugChan;
        }
        public ulong GetSuggestionRole(ulong? id)
        {
            if (id == null || !_sugroles.TryGetValue(id.Value, out var SugRole))
                return 0;

            return SugRole;
        }
        public int GetRemoveOnMute(ulong? id)
        {
            if (id == null || !_removerolesonmute.TryGetValue(id.Value, out var removeroles))
                return 0;

            return removeroles;
        }
        public int GetInvWarn(ulong? id)
        {
            if (id == null || !_invwarn.TryGetValue(id.Value, out var invw))
                return 0;

            return invw;
        }
        public int GetFW(ulong? id)
        {
            if (id == null || !_fwarn.TryGetValue(id.Value, out var fw))
                return 0;

            return fw;
        }
        public ulong GetSNum(ulong? id)
        {
            _snum.TryGetValue(id.Value, out var snum);
            return snum;
        }
        public async Task SetWarnlogChannelId(IGuild guild, ITextChannel channel)
        {

            using (var uow = _db.GetDbContext())
            {
                var gc = uow.GuildConfigs.ForId(guild.Id, set => set);
                gc.WarnlogChannelId = channel.Id;
                await uow.SaveChangesAsync();
            }
            _warnlogchannelids.AddOrUpdate(guild.Id, channel.Id, (key, old) => channel.Id);
        }
        public async Task SetTicketCategoryId(IGuild guild, ICategoryChannel channel)
        {

            using (var uow = _db.GetDbContext())
            {
                var gc = uow.GuildConfigs.ForId(guild.Id, set => set);
                gc.TicketCategory = channel.Id;
                await uow.SaveChangesAsync();
            }
            _ticketchannelids.AddOrUpdate(guild.Id, channel.Id, (key, old) => channel.Id);
        }
        public async Task SetSuggestionChannelId(IGuild guild, ulong channel)
        {

            using (var uow = _db.GetDbContext())
            {
                var gc = uow.GuildConfigs.ForId(guild.Id, set => set);
                gc.sugchan = channel;
                await uow.SaveChangesAsync();
            }
            _sugchans.AddOrUpdate(guild.Id, channel, (key, old) => channel);
        }

        public async Task SetSuggestionRole(IGuild guild, ulong name)
        {

            using (var uow = _db.GetDbContext())
            {
                var gc = uow.GuildConfigs.ForId(guild.Id, set => set);
                gc.SuggestRole = name;
                await uow.SaveChangesAsync();
            }
            _sugroles.AddOrUpdate(guild.Id, name, (key, old) => name);
        }
        public async Task removeonmute(IGuild guild, string yesnt)
        {
            int yesno = (yesnt == "yes") ? 1 : 0;
            using (var uow = _db.GetDbContext())
            {
                var gc = uow.GuildConfigs.ForId(guild.Id, set => set);
                gc.removeroles = yesno;
                await uow.SaveChangesAsync();
            }
            _removerolesonmute.AddOrUpdate(guild.Id, yesno, (key, old) => yesno);
        }
        public async Task fwarn(IGuild guild, string yesnt)
        {
            int yesno = -1;
            using (var uow = _db.GetDbContext())
            {

                switch (yesnt)
                {
                    case "y":
                        yesno = 1;
                        break;
                    case "n":
                        yesno = 0;
                        break;
                }
            }
            using (var uow = _db.GetDbContext())
            {
                var gc = uow.GuildConfigs.ForId(guild.Id, set => set);
                gc.fwarn = yesno;
                await uow.SaveChangesAsync();
            }
            _fwarn.AddOrUpdate(guild.Id, yesno, (key, old) => yesno);
        }
        public async Task sugnum(IGuild guild, ulong num)
        {
            using (var uow = _db.GetDbContext())
            {
                var gc = uow.GuildConfigs.ForId(guild.Id, set => set);
                gc.sugnum = num;
                await uow.SaveChangesAsync();
            }
            _snum.AddOrUpdate(guild.Id, num, (key, old) => num);
        }
        public async Task InvWarn(IGuild guild, string yesnt)
        {
            int yesno = -1;
            using (var uow = _db.GetDbContext())
            {

                switch (yesnt)
                {
                    case "y":
                        yesno = 1;
                        break;
                    case "n":
                        yesno = 0;
                        break;
                }
            }
            using (var uow = _db.GetDbContext())
            {
                var gc = uow.GuildConfigs.ForId(guild.Id, set => set);
                gc.invwarn = yesno;
                await uow.SaveChangesAsync();
            }
            _invwarn.AddOrUpdate(guild.Id, yesno, (key, old) => yesno);
        }
        public async Task SetMWarnlogChannelId(IGuild guild, ITextChannel channel)
        {

            using (var uow = _db.GetDbContext())
            {
                var gc = uow.GuildConfigs.ForId(guild.Id, set => set);
                gc.MiniWarnlogChannelId = channel.Id;
                await uow.SaveChangesAsync();
            }
            _miniwarnlogchannelids.AddOrUpdate(guild.Id, channel.Id, (key, old) => channel.Id);
        }

        public void AddServices(IServiceCollection services)
        {
            _lateBlockers = services.Where(x => x.ImplementationType?.GetInterfaces().Contains(typeof(ILateBlocker)) ?? false)
                .Select(x => _services.GetService(x.ImplementationType) as ILateBlocker)
                .OrderByDescending(x => x.Priority)
                .ToArray();

            _lateExecutors = services.Where(x => x.ImplementationType?.GetInterfaces().Contains(typeof(ILateExecutor)) ?? false)
                .Select(x => _services.GetService(x.ImplementationType) as ILateExecutor)
                .ToArray();

            _inputTransformers = services.Where(x => x.ImplementationType?.GetInterfaces().Contains(typeof(IInputTransformer)) ?? false)
                .Select(x => _services.GetService(x.ImplementationType) as IInputTransformer)
                .ToArray();

            _earlyBehaviors = services.Where(x => x.ImplementationType?.GetInterfaces().Contains(typeof(IEarlyBehavior)) ?? false)
                .Select(x => _services.GetService(x.ImplementationType) as IEarlyBehavior)
                .ToArray();
        }

        public async Task ExecuteExternal(ulong? guildId, ulong channelId, string commandText)
        {
            if (guildId != null)
            {
                var guild = _client.GetGuild(guildId.Value);
                if (!(guild?.GetChannel(channelId) is SocketTextChannel channel))
                {
                    _log.Warn("Channel for external execution not found.");
                    return;
                }

                try
                {
                    IUserMessage msg = await channel.SendMessageAsync(commandText).ConfigureAwait(false);
                    msg = (IUserMessage)await channel.GetMessageAsync(msg.Id).ConfigureAwait(false);
                    await TryRunCommand(guild, channel, msg).ConfigureAwait(false);
                    //msg.DeleteAfter(5);
                }
                catch { }
            }
        }

        public Task StartHandling()
        {
            _client.MessageReceived += (msg) => { var _ = Task.Run(() => MessageReceivedHandler(msg)); return Task.CompletedTask; };
            return Task.CompletedTask;
        }

        private const float _oneThousandth = 1.0f / 1000;
        private readonly DbService _db;
        private readonly IBotConfigProvider _bcp;

        private Task LogSuccessfulExecution(IUserMessage usrMsg, ITextChannel channel, params int[] execPoints)
        {
            var bss = _services.GetService<BotSettingsService>();
            if (bss.Data.ConsoleOutputType == ConsoleOutputType.Normal)
            {
                _log.Info($"Command Executed after " + string.Join("/", execPoints.Select(x => (x * _oneThousandth).ToString("F3"))) + "s\n\t" +
                        "User: {0}\n\t" +
                        "Server: {1}\n\t" +
                        "Channel: {2}\n\t" +
                        "Message: {3}",
                        usrMsg.Author + " [" + usrMsg.Author.Id + "]", // {0}
                        (channel == null ? "PRIVATE" : channel.Guild.Name + " [" + channel.Guild.Id + "]"), // {1}
                        (channel == null ? "PRIVATE" : channel.Name + " [" + channel.Id + "]"), // {2}
                        usrMsg.Content // {3}
                        );
                var var1 = usrMsg.Author + " [" + usrMsg.Author.Id + "]";
                var var2 = (channel == null ? "PRIVATE" : channel.Guild.Name + " [" + channel.Guild.Id + "]");
                var var3 = (channel == null ? "PRIVATE" : channel.Name + " [" + channel.Id + "]");
                var var4 = usrMsg.Content;
                var eb = new EmbedBuilder();
                var em = new List<Embed>();
                eb.Description = ($"Command Executed after " +
                                  string.Join("/", execPoints.Select(x => (x * _oneThousandth).ToString("F3"))) +
                                  "s\n\t" +
                                  $"User: {var1}\n\t" +
                                  $"Server: {var2}\n\t" +
                                  $"Channel: {var3}\n\t" +
                                  $"Message: {var4}\n\t"
                    );
                eb.Color = Mewdeko.OkColor;
                em.Add(eb.Build());
                var web = new DiscordWebhookClient(
                    "https://discord.com/api/webhooks/840925374992482334/rJJLCgIYCchbn4q4H3BuhqGA1pS-R_zTOnFpd3CfCQbb9fScRMW61-phkL8Skfi5cr9p");
                web.SendMessageAsync(embeds: em);
            }
            else
            {
                _log.Info("Succ | g:{0} | c: {1} | u: {2} | msg: {3}",
                    channel?.Guild.Id.ToString() ?? "-",
                    channel?.Id.ToString() ?? "-",
                    usrMsg.Author.Id,
                    usrMsg.Content.TrimTo(10));
            }
            return Task.CompletedTask;
        }

        private void LogErroredExecution(string errorMessage, IUserMessage usrMsg, ITextChannel channel, params int[] execPoints)
        {
            var bss = _services.GetService<BotSettingsService>();
            if (bss.Data.ConsoleOutputType == ConsoleOutputType.Normal)
            {
                List<Embed> em = new List<Embed>();
                _log.Warn($"Command Errored after " + string.Join("/", execPoints.Select(x => (x * _oneThousandth).ToString("F3"))) + "s\n\t" +
                            "User: {0}\n\t" +
                            "Server: {1}\n\t" +
                            "Channel: {2}\n\t" +
                            "Message: {3}\n\t" +
                            "Error: {4}",
                            usrMsg.Author + " [" + usrMsg.Author.Id + "]", // {0}
                            (channel == null ? "PRIVATE" : channel.Guild.Name + " [" + channel.Guild.Id + "]"), // {1}
                            (channel == null ? "PRIVATE" : channel.Name + " [" + channel.Id + "]"), // {2}
                            usrMsg.Content,// {3}
                            errorMessage
                            //exec.Result.ErrorReason // {4}
                            );
                var eb = new EmbedBuilder();
                var var1 = usrMsg.Author + " [" + usrMsg.Author.Id + "]";
                var var2 = (channel == null ? "PRIVATE" : channel.Guild.Name + " [" + channel.Guild.Id + "]");
                var var3 = channel == null ? "PRIVATE" : channel.Name + " [" + channel.Id + "]";
                var var4 = usrMsg.Content;
                var var5 = errorMessage;
                eb.Description = ($"Command Errored after " +
                                  string.Join("/", execPoints.Select(x => (x * _oneThousandth).ToString("F3"))) +
                                  "s\n\t" +
                                  $"User: {var1}\n\t" +
                                  $"Server: {var2}\n\t" +
                                  $"Channel: {var3}\n\t" +
                                  $"Message: {var4}\n\t" +
                                  $"Error: {var5}"
                        //exec.Result.ErrorReason // {4}
                    );
                eb.Color = Mewdeko.ErrorColor;
                em.Add(eb.Build());
                var web = new DiscordWebhookClient(
                    "https://discord.com/api/webhooks/840925374992482334/rJJLCgIYCchbn4q4H3BuhqGA1pS-R_zTOnFpd3CfCQbb9fScRMW61-phkL8Skfi5cr9p");
                web.SendMessageAsync(embeds: em);
            }
            else
            {
                _log.Warn("Err | g:{0} | c: {1} | u: {2} | msg: {3}\n\tErr: {4}",
                    channel?.Guild.Id.ToString() ?? "-",
                    channel?.Id.ToString() ?? "-",
                    usrMsg.Author.Id,
                    usrMsg.Content.TrimTo(10),
                    errorMessage);
            }
        }

        private async Task MessageReceivedHandler(SocketMessage msg)
        {
            try
            {
                if (msg.Author.IsBot || !_bot.Ready.Task.IsCompleted) //no bots, wait until bot connected and initialized
                    return;

                if (!(msg is SocketUserMessage usrMsg))
                    return;
#if !GLOBAL_Mewdeko
                // track how many messagges each user is sending
                UserMessagesSent.AddOrUpdate(usrMsg.Author.Id, 1, (key, old) => ++old);
#endif

                var channel = msg.Channel as ISocketMessageChannel;
                var guild = (msg.Channel as SocketTextChannel)?.Guild;

                await TryRunCommand(guild, channel, usrMsg).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _log.Warn("Error in CommandHandler");
                _log.Warn(ex);
                if (ex.InnerException != null)
                {
                    _log.Warn("Inner Exception of the error in CommandHandler");
                    _log.Warn(ex.InnerException);
                }
            }
        }

        public async Task TryRunCommand(SocketGuild guild, ISocketMessageChannel channel, IUserMessage usrMsg)
        {
            var execTime = Environment.TickCount;

            //its nice to have early blockers and early blocking executors separate, but
            //i could also have one interface with priorities, and just put early blockers on
            //highest priority. :thinking:
            foreach (var beh in _earlyBehaviors)
            {
                if (await beh.RunBehavior(_client, guild, usrMsg).ConfigureAwait(false))
                {
                    if (beh.BehaviorType == ModuleBehaviorType.Blocker)
                    {
                        _log.Info("Blocked User: [{0}] Message: [{1}] Service: [{2}]", usrMsg.Author, usrMsg.Content, beh.GetType().Name);
                        List<Embed> em = new List<Embed>();
                        var web = new DiscordWebhookClient(
                            "https://discord.com/api/webhooks/840925374992482334/rJJLCgIYCchbn4q4H3BuhqGA1pS-R_zTOnFpd3CfCQbb9fScRMW61-phkL8Skfi5cr9p");
                        var embed = new EmbedBuilder();
                        embed.Description = ("Blocked User: [{0}] Message: [{1}] Service: [{2}]", usrMsg.Author,
                            usrMsg.Content, beh.GetType().Name).ToString();
                        embed.Color = Mewdeko.ErrorColor;
                        em.Add(embed.Build());
                        await web.SendMessageAsync(embeds: em);
                    }
                    else if (beh.BehaviorType == ModuleBehaviorType.Executor)
                    {
                        _log.Info("User [{0}] executed [{1}] in [{2}]", usrMsg.Author, usrMsg.Content, beh.GetType().Name);
                        List<Embed> em = new List<Embed>();
                        var embed = new EmbedBuilder();
                        embed.Description =
                            $"User [{usrMsg.Author}] executed [{usrMsg.Content}] in [{beh.GetType().Name}]";
                        embed.Color = Mewdeko.ErrorColor;
                        em.Add(embed.Build());
                        var web = new DiscordWebhookClient(
                            "https://discord.com/api/webhooks/840925374992482334/rJJLCgIYCchbn4q4H3BuhqGA1pS-R_zTOnFpd3CfCQbb9fScRMW61-phkL8Skfi5cr9p");
                        await web.SendMessageAsync(embeds: em);
                    }
                    return;
                }
            }

            var exec2 = Environment.TickCount - execTime;

            string messageContent = usrMsg.Content;
            foreach (var exec in _inputTransformers)
            {
                string newContent;
                if ((newContent = await exec.TransformInput(guild, usrMsg.Channel, usrMsg.Author, messageContent).ConfigureAwait(false)) != messageContent.ToLowerInvariant())
                {
                    messageContent = newContent;
                    break;
                }
            }
            var prefix = GetPrefix(guild?.Id);
            var isPrefixCommand = messageContent.StartsWith(".prefix", StringComparison.InvariantCultureIgnoreCase);
            // execute the command and measure the time it took
            if (messageContent.StartsWith(prefix, StringComparison.InvariantCulture) || isPrefixCommand)
            {
                var (Success, Error, Info) = await ExecuteCommandAsync(new CommandContext(_client, usrMsg), messageContent, isPrefixCommand ? 1 : prefix.Length, _services, MultiMatchHandling.Best).ConfigureAwait(false);
                execTime = Environment.TickCount - execTime;

                if (Success)
                {
                    await LogSuccessfulExecution(usrMsg, channel as ITextChannel, exec2, execTime).ConfigureAwait(false);
                    await CommandExecuted(usrMsg, Info).ConfigureAwait(false);
                    return;
                }
                else if (Error != null)
                {
                    LogErroredExecution(Error, usrMsg, channel as ITextChannel, exec2, execTime);
                    if (guild != null)
                        await CommandErrored(Info, channel as ITextChannel, Error).ConfigureAwait(false);
                }
            }
            else
            {
                await OnMessageNoTrigger(usrMsg).ConfigureAwait(false);
            }

            foreach (var exec in _lateExecutors)
            {
                await exec.LateExecute(_client, guild, usrMsg).ConfigureAwait(false);
            }

        }

        public Task<(bool Success, string Error, CommandInfo Info)> ExecuteCommandAsync(CommandContext context, string input, int argPos, IServiceProvider serviceProvider, MultiMatchHandling multiMatchHandling = MultiMatchHandling.Exception)
            => ExecuteCommand(context, input.Substring(argPos), serviceProvider, multiMatchHandling);


        public async Task<(bool Success, string Error, CommandInfo Info)> ExecuteCommand(CommandContext context, string input, IServiceProvider services, MultiMatchHandling multiMatchHandling = MultiMatchHandling.Exception)
        {
            var searchResult = _commandService.Search(context, input);
            if (!searchResult.IsSuccess)
                return (false, null, null);

            var commands = searchResult.Commands;
            var preconditionResults = new Dictionary<CommandMatch, PreconditionResult>();

            foreach (var match in commands)
            {
                preconditionResults[match] = await match.Command.CheckPreconditionsAsync(context, services).ConfigureAwait(false);
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
                var parseResult = await pair.Key.ParseAsync(context, searchResult, pair.Value, services).ConfigureAwait(false);

                if (parseResult.Error == CommandError.MultipleMatches)
                {
                    IReadOnlyList<TypeReaderValue> argList, paramList;
                    switch (multiMatchHandling)
                    {
                        case MultiMatchHandling.Best:
                            argList = parseResult.ArgValues.Select(x => x.Values.OrderByDescending(y => y.Score).First()).ToImmutableArray();
                            paramList = parseResult.ParamValues.Select(x => x.Values.OrderByDescending(y => y.Score).First()).ToImmutableArray();
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
                    var argValuesSum = parseResult.ArgValues?.Sum(x => x.Values.OrderByDescending(y => y.Score).FirstOrDefault().Score) ?? 0;
                    var paramValuesSum = parseResult.ParamValues?.Sum(x => x.Values.OrderByDescending(y => y.Score).FirstOrDefault().Score) ?? 0;

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
            {
                if (await exec.TryBlockLate(_client, context, cmd.Module.GetTopLevelModule().Name, cmd).ConfigureAwait(false))
                {
                    _log.Info("Late blocking User [{0}] Command: [{1}] in [{2}]", context.User, commandName, exec.GetType().Name);
                    var eb = new EmbedBuilder();
                    eb.Description = ($"Late blocking User [{context.User}] Command: [{commandName}] in [{exec.GetType().Name}]");
                    eb.Color = Mewdeko.OkColor;
                    var web = new DiscordWebhookClient(
                        "https://discord.com/api/webhooks/840925374992482334/rJJLCgIYCchbn4q4H3BuhqGA1pS-R_zTOnFpd3CfCQbb9fScRMW61-phkL8Skfi5cr9p");
                    List<Embed> em = new List<Embed>();
                    em.Add(eb.Build());
                    await web.SendMessageAsync(embeds: em);
                    return (false, null, cmd);
                }
            }

            //If we get this far, at least one parse was successful. Execute the most likely overload.
            var chosenOverload = successfulParses[0];
            var execResult = (ExecuteResult)await chosenOverload.Key.ExecuteAsync(context, chosenOverload.Value, services).ConfigureAwait(false);

            if (execResult.Exception != null && (!(execResult.Exception is HttpException he) || he.DiscordCode != 50013))
            {
                lock (errorLogLock)
                {
                    var now = DateTime.Now;
                    File.AppendAllText($"./command_errors_{now:yyyy-MM-dd}.txt",
                        $"[{now:HH:mm-yyyy-MM-dd}]" + Environment.NewLine
                        + execResult.Exception.ToString() + Environment.NewLine
                        + "------" + Environment.NewLine);
                    _log.Warn(execResult.Exception);
                }
            }

            return (true, null, cmd);
        }

        private readonly object errorLogLock = new object();
    }
}