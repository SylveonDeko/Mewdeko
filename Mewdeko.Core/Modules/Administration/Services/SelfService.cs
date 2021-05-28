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
using NLog;
using StackExchange.Redis;

namespace Mewdeko.Modules.Administration.Services
{
    public class SelfService : ILateExecutor, INService
    {
        private readonly IBotConfigProvider _bc;
        private readonly Mewdeko _bot;
        private readonly IDataCache _cache;
        private readonly DiscordSocketClient _client;
        private readonly CommandHandler _cmdHandler;

        private readonly IBotCredentials _creds;
        private readonly DbService _db;
        private readonly IHttpClientFactory _httpFactory;
        private readonly IImageCache _imgs;
        private readonly ILocalization _localization;
        private readonly Logger _log;

        private readonly ConnectionMultiplexer _redis;
        private readonly IBotStrings _strings;
        private ConcurrentDictionary<ulong?, ConcurrentDictionary<int, Timer>> _autoCommands = new();

        private ImmutableDictionary<ulong, IDMChannel> ownerChannels =
            new Dictionary<ulong, IDMChannel>().ToImmutableDictionary();
        //private readonly Timer _updateTimer;

        public SelfService(DiscordSocketClient client, Mewdeko bot, CommandHandler cmdHandler, DbService db,
            IBotConfigProvider bc, ILocalization localization, IBotStrings strings, IBotCredentials creds,
            IDataCache cache, IHttpClientFactory factory)
        {
            _redis = cache.Redis;
            _bot = bot;
            _cmdHandler = cmdHandler;
            _db = db;
            _log = LogManager.GetCurrentClassLogger();
            _localization = localization;
            _strings = strings;
            _client = client;
            _creds = creds;
            _bc = bc;
            _cache = cache;
            _imgs = cache.LocalImages;
            _httpFactory = factory;
            var sub = _redis.GetSubscriber();
            if (_client.ShardId == 0)
                sub.Subscribe(_creds.RedisKey() + "_reload_images",
                    delegate { _imgs.Reload(); }, CommandFlags.FireAndForget);

            //_updateTimer = new Timer(async _ =>
            //{
            //    try
            //    {
            //        var ch = ownerChannels?.Values.FirstOrDefault();

            //        if (ch == null) // no owner channels
            //            return;

            //        var cfo = _bc.BotConfig.CheckForUpdates;
            //        if (cfo == UpdateCheckType.None)
            //            return;

            //        string data;
            //        if ((cfo == UpdateCheckType.Commit && (data = await GetNewCommit().ConfigureAwait(false)) != null)
            //            || (cfo == UpdateCheckType.Release && (data = await GetNewRelease().ConfigureAwait(false)) != null))
            //        {
            //            await ch.SendConfirmAsync("New Bot Update", data).ConfigureAwait(false);
            //        }
            //    }
            //    catch (Exception ex)
            //    {
            //        _log.Warn(ex);
            //    }
            //}, null, TimeSpan.FromHours(8), TimeSpan.FromHours(8));
            sub.Subscribe(_creds.RedisKey() + "_reload_bot_config",
                delegate { _bc.Reload(); }, CommandFlags.FireAndForget);
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
                        _log.Info($"Left server {server.Name} [{server.Id}]");
                    }
                    else
                    {
                        await server.DeleteAsync().ConfigureAwait(false);
                        _log.Info($"Deleted server {server.Name} [{server.Id}]");
                    }
                }
                catch
                {
                }
            }, CommandFlags.FireAndForget);

            Task.Run(async () =>
            {
                await bot.Ready.Task.ConfigureAwait(false);

                _autoCommands = bc.BotConfig
                    .StartupCommands
                    .Where(x => x.Interval >= 5)
                    .GroupBy(x => x.GuildId)
                    .ToDictionary(
                        x => x.Key,
                        y => y.ToDictionary(x => x.Id,
                                TimerFromStartupCommand)
                            .ToConcurrent())
                    .ToConcurrent();

                foreach (var cmd in bc.BotConfig.StartupCommands.Where(x => x.Interval <= 0))
                    try
                    {
                        await ExecuteCommand(cmd).ConfigureAwait(false);
                    }
                    catch
                    {
                    }
            });

            Task.Run(async () =>
            {
                await bot.Ready.Task.ConfigureAwait(false);

            });
        }

        public bool ForwardDMs => _bc.BotConfig.ForwardMessages;
        public bool ForwardDMsToAllOwners => _bc.BotConfig.ForwardToAllOwners;

        // forwards dms
        public async Task LateExecute(DiscordSocketClient client, IGuild guild, IUserMessage msg)
        {
            if (msg.Channel is IDMChannel && ForwardDMs && ownerChannels.Any())
            {
                var title = _strings.GetText("dm_from",
                                _localization.DefaultCultureInfo,
                                "Administration".ToLowerInvariant()) +
                            $" [{msg.Author}]({msg.Author.Id})";

                var attachamentsTxt = _strings.GetText("attachments",
                    _localization.DefaultCultureInfo,
                    "Administration".ToLowerInvariant());

                var toSend = msg.Content;

                if (msg.Attachments.Count > 0)
                    toSend += $"\n\n{Format.Code(attachamentsTxt)}:\n" +
                              string.Join("\n", msg.Attachments.Select(a => a.ProxyUrl));

                if (ForwardDMsToAllOwners)
                {
                    var allOwnerChannels = ownerChannels.Values;

                    foreach (var ownerCh in allOwnerChannels.Where(ch => ch.Recipient.Id != msg.Author.Id))
                        try
                        {
                            await ownerCh.SendConfirmAsync(title, toSend).ConfigureAwait(false);
                        }
                        catch
                        {
                            _log.Warn("Can't contact owner with id {0}", ownerCh.Recipient.Id);
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

        //private async Task<string> GetNewCommit()
        //{
        //    var client = new GitHubClient(new ProductHeaderValue("Mewdeko"));
        //    var lu = _bc.BotConfig.LastUpdate;
        //    var commits = await client.Repository.Commit.GetAll("Kwoth", "Mewdeko", new CommitRequest()
        //    {
        //        Since = lu,
        //    }).ConfigureAwait(false);

        //    commits = commits.Where(x => x.Commit.Committer.Date.UtcDateTime > lu)
        //        .Take(10)
        //        .ToList();

        //    if (!commits.Any())
        //        return null;

        //    SetNewLastUpdate(commits[0].Commit.Committer.Date.UtcDateTime);

        //    var newCommits = commits
        //        .Select(x => $"[{x.Sha.TrimTo(6, true)}]({x.HtmlUrl})  {x.Commit.Message.TrimTo(50)}");

        //    return string.Join('\n', newCommits);
        //}

        private void SetNewLastUpdate(DateTime dt)
        {
            using (var uow = _db.GetDbContext())
            {
                var bc = uow.BotConfig.GetOrCreate(set => set);
                bc.LastUpdate = dt;
                uow.SaveChanges();
            }

            _bc.BotConfig.LastUpdate = dt;
        }

        //private async Task<string> GetNewRelease()
        //{
        //    var client = new GitHubClient(new ProductHeaderValue("Mewdeko"));
        //    var lu = _bc.BotConfig.LastUpdate;
        //    var release = (await client.Repository.Release.GetAll("Kwoth", "Mewdeko").ConfigureAwait(false)).FirstOrDefault();

        //    if (release == null || release.CreatedAt.UtcDateTime <= lu)
        //        return null;

        //    SetNewLastUpdate(release.CreatedAt.UtcDateTime);

        //    return Format.Bold(release.Name) + "\n\n" + release.Body.TrimTo(1500);
        //}

        public void SetUpdateCheck(UpdateCheckType type)
        {
            using (var uow = _db.GetDbContext())
            {
                var bc = uow.BotConfig.GetOrCreate(set => set);
                _bc.BotConfig.CheckForUpdates = bc.CheckForUpdates = type;
                uow.SaveChanges();
            }

            //if (type == UpdateCheckType.None)
            //{
            //    _updateTimer.Change(Timeout.Infinite, Timeout.Infinite);
            //}
        }

        private Timer TimerFromStartupCommand(StartupCommand x)
        {
            return new(async obj => await ExecuteCommand((StartupCommand) obj).ConfigureAwait(false),
                x,
                x.Interval * 1000,
                x.Interval * 1000);
        }

        private async Task ExecuteCommand(StartupCommand cmd)
        {
            try
            {
                var prefix = _cmdHandler.GetPrefix(cmd.GuildId);
                //if someone already has .die as their startup command, ignore it
                if (cmd.CommandText.StartsWith(prefix + "die", StringComparison.InvariantCulture))
                    return;
                await _cmdHandler.ExecuteExternal(cmd.GuildId, cmd.ChannelId, cmd.CommandText).ConfigureAwait(false);
                await Task.Delay(400).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _log.Warn(ex);
            }
        }

        public void AddNewAutoCommand(StartupCommand cmd)
        {
            using (var uow = _db.GetDbContext())
            {
                uow.BotConfig
                    .GetOrCreate(set => set.Include(x => x.StartupCommands))
                    .StartupCommands
                    .Add(cmd);
                uow.SaveChanges();
            }

            var autos = _autoCommands.GetOrAdd(cmd.GuildId, new ConcurrentDictionary<int, Timer>());
            autos.AddOrUpdate(cmd.Id, key => TimerFromStartupCommand(cmd), (key, old) =>
            {
                old.Change(Timeout.Infinite, Timeout.Infinite);
                return TimerFromStartupCommand(cmd);
            });
        }

        public IEnumerable<StartupCommand> GetStartupCommands()
        {
            using (var uow = _db.GetDbContext())
            {
                return uow.BotConfig
                    .GetOrCreate(set => set.Include(x => x.StartupCommands))
                    .StartupCommands
                    .OrderBy(x => x.Id)
                    .ToArray();
            }
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

        public bool RemoveStartupCommand(int index, out StartupCommand cmd)
        {
            using (var uow = _db.GetDbContext())
            {
                var cmds = uow.BotConfig
                    .GetOrCreate(set => set.Include(x => x.StartupCommands))
                    .StartupCommands;
                cmd = cmds
                    .FirstOrDefault(x => x.Index == index);

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
                uow.BotConfig
                    .GetOrCreate(set => set.Include(x => x.StartupCommands))
                    .StartupCommands
                    .Clear();
                uow.SaveChanges();
            }
        }

        public void ReloadBotConfig()
        {
            var sub = _cache.Redis.GetSubscriber();
            sub.Publish(_creds.RedisKey() + "_reload_bot_config",
                "",
                CommandFlags.FireAndForget);
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

        public void ForwardMessages()
        {
            using (var uow = _db.GetDbContext())
            {
                var config = uow.BotConfig.GetOrCreate(set => set);
                _bc.BotConfig.ForwardMessages = config.ForwardMessages = !config.ForwardMessages;
                uow.SaveChanges();
            }
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

        public void ForwardToAll()
        {
            using (var uow = _db.GetDbContext())
            {
                var config = uow.BotConfig.GetOrCreate(set => set);
                _bc.BotConfig.ForwardToAllOwners = config.ForwardToAllOwners = !config.ForwardToAllOwners;
                uow.SaveChanges();
            }
        }

        public IEnumerable<ShardComMessage> GetAllShardStatuses()
        {
            var db = _cache.Redis.GetDatabase();
            return db.ListRange(_creds.RedisKey() + "_shardstats")
                .Select(x => JsonConvert.DeserializeObject<ShardComMessage>(x));
        }
    }
}