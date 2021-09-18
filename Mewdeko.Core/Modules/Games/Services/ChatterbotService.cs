using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Core.Services;
using Mewdeko.Extensions;
using Mewdeko.Modules.Games.Common.ChatterBot;
using Mewdeko.Modules.Permissions.Common;
using Mewdeko.Modules.Permissions.Services;
using Serilog;

namespace Mewdeko.Modules.Games.Services
{
    public class ChatterBotService : IEarlyBehavior, INService
    {
        private readonly DiscordSocketClient _client;
        private readonly CommandHandler _cmd;
        private readonly IBotCredentials _creds;
        private readonly IHttpClientFactory _httpFactory;
        private readonly PermissionService _perms;
        private readonly IBotStrings _strings;

        public ChatterBotService(DiscordSocketClient client, PermissionService perms,
            Mewdeko bot, CommandHandler cmd, IBotStrings strings, IHttpClientFactory factory,
            IBotCredentials creds)
        {
            _client = client;
            _perms = perms;
            _cmd = cmd;
            _strings = strings;
            _creds = creds;
            _httpFactory = factory;

            ChatterBotGuilds = new ConcurrentDictionary<ulong, Lazy<IChatterBotSession>>(
                bot.AllGuildConfigs
                    .Where(gc => gc.CleverbotEnabled)
                    .ToDictionary(gc => gc.GuildId, gc => new Lazy<IChatterBotSession>(() => CreateSession(), true)));
        }

        public ConcurrentDictionary<ulong, Lazy<IChatterBotSession>> ChatterBotGuilds { get; }

        public int Priority => -1;
        public ModuleBehaviorType BehaviorType => ModuleBehaviorType.Executor;

        public async Task<bool> RunBehavior(DiscordSocketClient client, IGuild guild, IUserMessage usrMsg)
        {
            if (!(guild is SocketGuild sg))
                return false;
            try
            {
                var message = PrepareMessage(usrMsg, out var cbs);
                if (message == null || cbs == null)
                    return false;

                var pc = _perms.GetCacheFor(guild.Id);
                if (!pc.Permissions.CheckPermissions(usrMsg,
                    "cleverbot",
                    "Games".ToLowerInvariant(),
                    out var index))
                {
                    if (pc.Verbose)
                    {
                        var returnMsg = _strings.GetText("trigger", guild.Id, index + 1,
                            Format.Bold(pc.Permissions[index].GetCommand(_cmd.GetPrefix(guild), (SocketGuild)guild)));
                        try
                        {
                            await usrMsg.Channel.SendErrorAsync(returnMsg).ConfigureAwait(false);
                        }
                        catch
                        {
                        }

                        Log.Information(returnMsg);
                    }

                    return true;
                }

                var cleverbotExecuted = await TryAsk(cbs, (ITextChannel)usrMsg.Channel, message).ConfigureAwait(false);
                if (cleverbotExecuted)
                {
                    Log.Information($@"CleverBot Executed
Server: {guild.Name} [{guild.Id}]
Channel: {usrMsg.Channel?.Name} [{usrMsg.Channel?.Id}]
UserId: {usrMsg.Author} [{usrMsg.Author.Id}]
Message: {usrMsg.Content}");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error in cleverbot");
            }

            return false;
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

            if (!ChatterBotGuilds.TryGetValue(channel.Guild.Id, out var lazyCleverbot))
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
            else
                return null;

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