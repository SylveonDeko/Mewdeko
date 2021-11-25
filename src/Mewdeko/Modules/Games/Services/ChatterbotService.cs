using System.Collections.Concurrent;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Mewdeko._Extensions;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Services;
using Mewdeko.Modules.Games.Common.ChatterBot;
using Mewdeko.Modules.Permissions.Common;
using Mewdeko.Modules.Permissions.Services;
using Mewdeko.Services.strings;
using Serilog;

namespace Mewdeko.Modules.Games.Services
{
    public class ChatterBotService : INService
    {
        private readonly DiscordSocketClient _client;
        private readonly CommandHandler _cmd;
        private readonly IBotCredentials _creds;
        private readonly IHttpClientFactory _httpFactory;
        private readonly PermissionService _perms;
        private readonly IBotStrings _strings;
        private readonly DbService _db;

        public ChatterBotService(DiscordSocketClient client, PermissionService perms,
            Mewdeko.Services.Mewdeko bot, CommandHandler cmd, IBotStrings strings, IHttpClientFactory factory,
            IBotCredentials creds, DbService db)
        {
            _db = db;
            _client = client;
            _perms = perms;
            _cmd = cmd;
            _strings = strings;
            _creds = creds;
            _httpFactory = factory;
            _client.MessageReceived += MessageRecieved;

            ChatterBotChannels = new ConcurrentDictionary<ulong, Lazy<IChatterBotSession>>(
                bot.AllGuildConfigs
                    .Where(gc => gc.CleverbotChannel != 0)
                    .ToDictionary(gc => gc.CleverbotChannel, gc => new Lazy<IChatterBotSession>(() => CreateSession(), true)));
        }

        public ConcurrentDictionary<ulong, Lazy<IChatterBotSession>> ChatterBotChannels { get; }

        public int Priority => -1;
        public ModuleBehaviorType BehaviorType => ModuleBehaviorType.Executor;
        public async Task SetCleverbotChannel(IGuild guild, ulong id)
        {
            using (var uow = _db.GetDbContext())
            {
                var gc = uow.GuildConfigs.ForId(guild.Id, set => set);
                gc.CleverbotChannel = id;
                await uow.SaveChangesAsync();
            }
            if (id == 0)
                ChatterBotChannels.TryRemove(id, out _);
            else
                ChatterBotChannels.TryAdd(id,
                    new Lazy<IChatterBotSession>(() => CreateSession(), true));
        }
        public ulong GetCleverbotChannel(ulong id)
        {
            return _db.GetDbContext().GuildConfigs.GetCleverbotChannel(id);
        }
        public async Task MessageRecieved(SocketMessage usrMsg)
        {
            if (usrMsg.Author.IsBot)
                return;
            if (usrMsg.Channel is not ITextChannel chan)
                return;
            try
            {
                var message = PrepareMessage(usrMsg as IUserMessage , out var cbs);
                if (message == null || cbs == null)
                    return;

                var cleverbotExecuted = await TryAsk(cbs, (ITextChannel)usrMsg.Channel, message).ConfigureAwait(false);
                if (cleverbotExecuted)
                {
                    Log.Information(
                    $@"CleverBot Executed
                    Server: {chan.Guild.Name} {chan.Guild.Name}]
                    Channel: {usrMsg.Channel?.Name} [{usrMsg.Channel?.Id}]
                    UserId: {usrMsg.Author} [{usrMsg.Author.Id}]
                    Message: {usrMsg.Content}");
                    return;
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error in cleverbot");
            }
        }

        public IChatterBotSession CreateSession()
        {
            if (!string.IsNullOrWhiteSpace(_creds.CleverbotApiKey))
                return new OfficialCleverbotSession(_creds.CleverbotApiKey, _httpFactory);
            return new CleverbotIOSession("GAh3wUfzDCpDpdpT", "RStKgqn7tcO9blbrv4KbXM8NDlb7H37C", _httpFactory);
        }

        public string PrepareMessage(IUserMessage msg, out IChatterBotSession cleverbot)
        {
            var channel = msg.Channel as ITextChannel;
            cleverbot = null;

            if (channel == null)
                return null;

            if (!ChatterBotChannels.TryGetValue(channel.Id, out var lazyCleverbot))
                return null;

            cleverbot = lazyCleverbot.Value;

            var MewdekoId = _client.CurrentUser.Id;
            var normalMention = $"<@{MewdekoId}> ";
            var nickMention = $"<@!{MewdekoId}> ";
            string message;

            if (msg.Content.StartsWith(normalMention, StringComparison.InvariantCulture))
                message = msg.Content.Substring(normalMention.Length).Trim();
            else if (msg.Content.StartsWith(nickMention, StringComparison.InvariantCulture))
                message = msg.Content.Substring(nickMention.Length).Trim();
            else if (msg.Content.StartsWith(_cmd.GetPrefix(channel.Guild)))
                return null;
            else
                message = msg.Content;

            return message;
        }

        public static async Task<bool> TryAsk(IChatterBotSession cleverbot, ITextChannel channel, string message)
        {
            await channel.TriggerTypingAsync().ConfigureAwait(false);

            var response = await cleverbot.Think(message).ConfigureAwait(false);
            try
            {
                await channel.SendConfirmAsync(response.SanitizeMentions(true)).ConfigureAwait(false);
            }
            catch
            {
                await channel.SendConfirmAsync(response.SanitizeMentions(true)).ConfigureAwait(false); // try twice :\
            }

            return true;
        }
    }
}