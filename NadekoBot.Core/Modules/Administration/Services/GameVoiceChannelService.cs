using System;
using System.Linq;
using System.Threading.Tasks;
using Discord.WebSocket;
using NadekoBot.Common.Collections;
using NadekoBot.Extensions;
using NadekoBot.Core.Services;
using NLog;

namespace NadekoBot.Modules.Administration.Services
{
    public class GameVoiceChannelService : INService
    {
        public ConcurrentHashSet<ulong> GameVoiceChannels { get; } = new ConcurrentHashSet<ulong>();

        private readonly Logger _log;
        private readonly DbService _db;
        private readonly DiscordSocketClient _client;

        public GameVoiceChannelService(DiscordSocketClient client, DbService db, NadekoBot bot)
        {
            _log = LogManager.GetCurrentClassLogger();
            _db = db;
            _client = client;

            GameVoiceChannels = new ConcurrentHashSet<ulong>(
                bot.AllGuildConfigs.Where(gc => gc.GameVoiceChannel != null)
                                         .Select(gc => gc.GameVoiceChannel.Value));

            _client.UserVoiceStateUpdated += Client_UserVoiceStateUpdated;
            _client.GuildMemberUpdated += _client_GuildMemberUpdated;
        }

        private Task _client_GuildMemberUpdated(SocketGuildUser before, SocketGuildUser after)
        {
            var _ = Task.Run(async () =>
            {
                try
                {
                    //if the user is in the voice channel and that voice channel is gvc
                    var vc = after.VoiceChannel;
                    if (vc == null || !GameVoiceChannels.Contains(vc.Id))
                        return;

                    //if the activity has changed, and is a playing activity
                    if (before.Activity != after.Activity
                        && after.Activity != null
                        && after.Activity.Type == Discord.ActivityType.Playing)
                    {
                        //trigger gvc
                        await TriggerGvc(after, after.Activity.Name);
                    }

                }
                catch (Exception ex)
                {
                    _log.Warn(ex);
                }
            });
            return Task.CompletedTask;
        }

        public ulong? ToggleGameVoiceChannel(ulong guildId, ulong vchId)
        {
            ulong? id;
            using (var uow = _db.GetDbContext())
            {
                var gc = uow.GuildConfigs.ForId(guildId, set => set);

                if (gc.GameVoiceChannel == vchId)
                {
                    GameVoiceChannels.TryRemove(vchId);
                    id = gc.GameVoiceChannel = null;
                }
                else
                {
                    if (gc.GameVoiceChannel != null)
                        GameVoiceChannels.TryRemove(gc.GameVoiceChannel.Value);
                    GameVoiceChannels.Add(vchId);
                    id = gc.GameVoiceChannel = vchId;
                }

                uow.SaveChanges();
            }
            return id;
        }

        private Task Client_UserVoiceStateUpdated(SocketUser usr, SocketVoiceState oldState, SocketVoiceState newState)
        {
            var _ = Task.Run(async () =>
            {
                try
                {
                    if (!(usr is SocketGuildUser gUser))
                        return;

                    var game = gUser.Activity?.Name;

                    if (oldState.VoiceChannel == newState.VoiceChannel ||
                        newState.VoiceChannel == null)
                        return;

                    if (!GameVoiceChannels.Contains(newState.VoiceChannel.Id) ||
                        string.IsNullOrWhiteSpace(game))
                        return;

                    await TriggerGvc(gUser, game);
                }
                catch (Exception ex)
                {
                    _log.Warn(ex);
                }
            });

            return Task.CompletedTask;
        }

        private async Task TriggerGvc(SocketGuildUser gUser, string game)
        {
            if (string.IsNullOrWhiteSpace(game))
                return;

            game = game.TrimTo(50).ToLowerInvariant();
            var vch = gUser.Guild.VoiceChannels
                .FirstOrDefault(x => x.Name.ToLowerInvariant() == game);

            if (vch == null)
                return;

            await Task.Delay(1000).ConfigureAwait(false);
            await gUser.ModifyAsync(gu => gu.Channel = vch).ConfigureAwait(false);
        }
    }
}
