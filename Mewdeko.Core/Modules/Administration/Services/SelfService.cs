using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Common.ShardCom;
using Mewdeko.Core.Services;
using Mewdeko.Core.Services.Database.Models;
using Mewdeko.Extensions;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Serilog;
using StackExchange.Redis;

namespace Mewdeko.Modules.Administration.Services
{
    public sealed class SelfService : ILateExecutor, IReadyExecutor, INService
    {
        private readonly BotConfigService _bss;

        private readonly IDataCache _cache;
        private readonly DiscordSocketClient _client;
        private readonly CommandHandler _cmdHandler;

        private readonly IBotCredentials _creds;
        private readonly DbService _db;
        private readonly IHttpClientFactory _httpFactory;
        private readonly IImageCache _imgs;
        private readonly ConnectionMultiplexer _redis;
        private readonly IBotStrings _strings;

        private ConcurrentDictionary<ulong?, ConcurrentDictionary<int, Timer>> _autoCommands =
            new();

        private ImmutableDictionary<ulong, IDMChannel> ownerChannels =
            new Dictionary<ulong, IDMChannel>().ToImmutableDictionary();

        public SelfService(DiscordSocketClient client, CommandHandler cmdHandler, DbService db,
            IBotStrings strings, IBotCredentials creds, IDataCache cache, IHttpClientFactory factory,
            BotConfigService bss)
        {
            _redis = cache.Redis;
            _cmdHandler = cmdHandler;
            _db = db;
            _strings = strings;
            _client = client;
            _creds = creds;
            _cache = cache;
            _imgs = cache.LocalImages;
            _httpFactory = factory;
            _bss = bss;

            var sub = _redis.GetSubscriber();
            if (_client.ShardId == 0)
                sub.Subscribe(_creds.RedisKey() + "_reload_images",
                    delegate { _imgs.Reload(); }, CommandFlags.FireAndForget);

            sub.Subscribe(_creds.RedisKey() + "_leave_guild", async (ch, v) =>
            {
                try
                {
                    var guildStr = v.ToString()?.Trim().ToUpperInvariant();
                    if (string.IsNullOrWhiteSpace(guildStr))
                        return;
                    var server = _client.Guilds.FirstOrDefault(g => g.Id.ToString() == guildStr) ??
                                 _client.Guilds.FirstOrDefault(g => g.Name.Trim().ToUpperInvariant() == guildStr);

                    if (server == null) return;

                    if (server.OwnerId != _client.CurrentUser.Id)
                    {
                        await server.LeaveAsync().ConfigureAwait(false);
                        Log.Information($"Left server {server.Name} [{server.Id}]");
                    }
                    else
                    {
                        await server.DeleteAsync().ConfigureAwait(false);
                        Log.Information($"Deleted server {server.Name} [{server.Id}]");
                    }
                }
                catch
                {
                }
            }, CommandFlags.FireAndForget);
        }

        // forwards dms
        public async Task LateExecute(DiscordSocketClient client, IGuild guild, IUserMessage msg)
        {
            var bs = _bss.Data;
            if (msg.Channel is IDMChannel && _bss.Data.ForwardMessages && ownerChannels.Any())
            {
                var title = _strings.GetText("dm_from") +
                            $" [{msg.Author}]({msg.Author.Id})";

                var attachamentsTxt = _strings.GetText("attachments");

                var toSend = msg.Content;

                if (msg.Attachments.Count > 0)
                    toSend += $"\n\n{Format.Code(attachamentsTxt)}:\n" +
                              string.Join("\n", msg.Attachments.Select(a => a.ProxyUrl));

                if (bs.ForwardToAllOwners)
                {
                    var allOwnerChannels = ownerChannels.Values;

                    foreach (var ownerCh in allOwnerChannels.Where(ch => ch.Recipient.Id != msg.Author.Id))
                        try
                        {
                            await ownerCh.SendConfirmAsync(title, toSend).ConfigureAwait(false);
                        }
                        catch
                        {
                            Log.Warning("Can't contact owner with id {0}", ownerCh.Recipient.Id);
                        }
                }
                else
                {
                    var firstOwnerChannel = ownerChannels.Values.First();
                    if (firstOwnerChannel.Recipient.Id != msg.Author.Id)
                        try
                        {
                            await firstOwnerChannel.SendConfirmAsync(title, toSend).ConfigureAwait(false);
                        }
                        catch
                        {
                            // ignored
                        }
                }
            }
        }

        public async Task OnReadyAsync()
        {
            using var uow = _db.GetDbContext();

            _autoCommands = uow._context
                .AutoCommands
                .AsNoTracking()
                .Where(x => x.Interval >= 5)
                .AsEnumerable()
                .GroupBy(x => x.GuildId)
                .ToDictionary(x => x.Key,
                    y => y.ToDictionary(x => x.Id, TimerFromAutoCommand)
                        .ToConcurrent())
                .ToConcurrent();

            var startupCommands = uow._context.AutoCommands.AsNoTracking().Where(x => x.Interval == 0);
            foreach (var cmd in startupCommands)
                try
                {
                    await ExecuteCommand(cmd).ConfigureAwait(false);
                }
                catch
                {
                }

            if (_client.ShardId == 0) await LoadOwnerChannels().ConfigureAwait(false);
        }

        private Timer TimerFromAutoCommand(AutoCommand x)
        {
            return new(async obj => await ExecuteCommand((AutoCommand) obj).ConfigureAwait(false),
                x,
                x.Interval * 1000,
                x.Interval * 1000);
        }

        private async Task ExecuteCommand(AutoCommand cmd)
        {
            try
            {
                if (cmd.GuildId is null)
                    return;

                var guildShard = (int) ((cmd.GuildId.Value >> 22) % (ulong) _creds.TotalShards);
                if (guildShard != _client.ShardId)
                    return;
                var prefix = _cmdHandler.GetPrefix(cmd.GuildId);
                //if someone already has .die as their startup command, ignore it
                if (cmd.CommandText.StartsWith(prefix + "die", StringComparison.InvariantCulture))
                    return;
                await _cmdHandler.ExecuteExternal(cmd.GuildId, cmd.ChannelId, cmd.CommandText).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error in SelfService ExecuteCommand");
            }
        }

        public void AddNewAutoCommand(AutoCommand cmd)
        {
            using (var uow = _db.GetDbContext())
            {
                uow._context.AutoCommands.Add(cmd);
                uow.SaveChanges();
            }

            if (cmd.Interval >= 5)
            {
                var autos = _autoCommands.GetOrAdd(cmd.GuildId, new ConcurrentDictionary<int, Timer>());
                autos.AddOrUpdate(cmd.Id, key => TimerFromAutoCommand(cmd), (key, old) =>
                {
                    old.Change(Timeout.Infinite, Timeout.Infinite);
                    return TimerFromAutoCommand(cmd);
                });
            }
        }

        public IEnumerable<AutoCommand> GetStartupCommands()
        {
            using var uow = _db.GetDbContext();
            return uow._context
                .AutoCommands
                .AsNoTracking()
                .Where(x => x.Interval == 0)
                .OrderBy(x => x.Id)
                .ToList();
        }

        public IEnumerable<AutoCommand> GetAutoCommands()
        {
            using var uow = _db.GetDbContext();
            return uow._context
                .AutoCommands
                .AsNoTracking()
                .Where(x => x.Interval >= 5)
                .OrderBy(x => x.Id)
                .ToList();
        }

        private async Task LoadOwnerChannels()
        {
            var channels = await Task.WhenAll(_creds.OwnerIds.Select(id =>
            {
                var user = _client.GetUser(id);
                if (user == null)
                    return Task.FromResult<IDMChannel>(null);

                return user.CreateDMChannelAsync();
            })).ConfigureAwait(false);

            ownerChannels = channels.Where(x => x != null)
                .ToDictionary(x => x.Recipient.Id, x => x)
                .ToImmutableDictionary();

            if (!ownerChannels.Any())
                Log.Warning(
                    "No owner channels created! Make sure you've specified the correct OwnerId in the credentials.json file and invited the bot to a Discord server.");
            else
                Log.Information(
                    $"Created {ownerChannels.Count} out of {_creds.OwnerIds.Length} owner message channels.");
        }

        public Task LeaveGuild(string guildStr)
        {
            var sub = _cache.Redis.GetSubscriber();
            return sub.PublishAsync(_creds.RedisKey() + "_leave_guild", guildStr);
        }

        public bool RestartBot()
        {
            var cmd = _creds.RestartCommand;
            if (string.IsNullOrWhiteSpace(cmd?.Cmd)) return false;

            Restart();
            return true;
        }

        public bool RemoveStartupCommand(int index, out AutoCommand cmd)
        {
            using (var uow = _db.GetDbContext())
            {
                cmd = uow._context.AutoCommands
                    .AsNoTracking()
                    .Where(x => x.Interval == 0)
                    .Skip(index)
                    .FirstOrDefault();

                if (cmd != null)
                {
                    uow._context.Remove(cmd);
                    uow.SaveChanges();
                    return true;
                }
            }

            return false;
        }

        public bool RemoveAutoCommand(int index, out AutoCommand cmd)
        {
            using (var uow = _db.GetDbContext())
            {
                cmd = uow._context.AutoCommands
                    .AsNoTracking()
                    .Where(x => x.Interval >= 5)
                    .Skip(index)
                    .FirstOrDefault();

                if (cmd != null)
                {
                    uow._context.Remove(cmd);
                    if (_autoCommands.TryGetValue(cmd.GuildId, out var autos))
                        if (autos.TryRemove(cmd.Id, out var timer))
                            timer.Change(Timeout.Infinite, Timeout.Infinite);
                    uow.SaveChanges();
                    return true;
                }
            }

            return false;
        }

        public async Task<bool> SetAvatar(string img)
        {
            if (string.IsNullOrWhiteSpace(img))
                return false;

            if (!Uri.IsWellFormedUriString(img, UriKind.Absolute))
                return false;

            var uri = new Uri(img);

            using (var http = _httpFactory.CreateClient())
            using (var sr = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false))
            {
                if (!sr.IsImage())
                    return false;

                // i can't just do ReadAsStreamAsync because dicord.net's image poops itself
                var imgData = await sr.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                using (var imgStream = imgData.ToStream())
                {
                    await _client.CurrentUser.ModifyAsync(u => u.Avatar = new Image(imgStream)).ConfigureAwait(false);
                }
            }

            return true;
        }

        public void ClearStartupCommands()
        {
            using (var uow = _db.GetDbContext())
            {
                var toRemove = uow._context
                    .AutoCommands
                    .AsNoTracking()
                    .Where(x => x.Interval == 0);

                uow._context.AutoCommands.RemoveRange(toRemove);
                uow.SaveChanges();
            }
        }

        public void ReloadImages()
        {
            var sub = _cache.Redis.GetSubscriber();
            sub.Publish(_creds.RedisKey() + "_reload_images", "");
        }

        public void Die()
        {
            var sub = _cache.Redis.GetSubscriber();
            sub.Publish(_creds.RedisKey() + "_die", "", CommandFlags.FireAndForget);
        }

        public void Restart()
        {
            Process.Start(_creds.RestartCommand.Cmd, _creds.RestartCommand.Args);
            var sub = _cache.Redis.GetSubscriber();
            sub.Publish(_creds.RedisKey() + "_die", "", CommandFlags.FireAndForget);
        }

        public bool RestartShard(int shardId)
        {
            if (shardId < 0 || shardId >= _creds.TotalShards)
                return false;

            var pub = _cache.Redis.GetSubscriber();
            pub.Publish(_creds.RedisKey() + "_shardcoord_stop",
                JsonConvert.SerializeObject(shardId),
                CommandFlags.FireAndForget);

            return true;
        }

        public bool ForwardMessages()
        {
            var isForwarding = false;
            _bss.ModifyConfig(config => { isForwarding = config.ForwardMessages = !config.ForwardMessages; });

            return isForwarding;
        }

        public bool ForwardToAll()
        {
            var isToAll = false;
            _bss.ModifyConfig(config => { isToAll = config.ForwardToAllOwners = !config.ForwardToAllOwners; });
            return isToAll;
        }

        public IEnumerable<ShardComMessage> GetAllShardStatuses()
        {
            var db = _cache.Redis.GetDatabase();
            return db.ListRange(_creds.RedisKey() + "_shardstats")
                .Select(x => JsonConvert.DeserializeObject<ShardComMessage>(x));
        }
    }
}