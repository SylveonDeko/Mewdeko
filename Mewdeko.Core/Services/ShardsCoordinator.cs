using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Mewdeko.Common.Collections;
using Mewdeko.Common.ShardCom;
using Mewdeko.Core.Common;
using Mewdeko.Core.Services.Impl;
using Mewdeko.Extensions;
using Newtonsoft.Json;
using NLog;
using StackExchange.Redis;

namespace Mewdeko.Core.Services
{
    public class ShardsCoordinator
    {
        private readonly BotCredentials _creds;
        private readonly int _curProcessId;
        private readonly string _key;

        private readonly Logger _log;
        private readonly ConnectionMultiplexer _redis;
        private readonly Process[] _shardProcesses;
        private readonly ShardComMessage _defaultShardState;

        private readonly ConcurrentHashSet<int> _shardRestartWaitingList =
            new();

        private readonly ShardsCoordinatorQueue _shardStartQueue =
            new();

        public ShardsCoordinator()
        {
            //load main stuff
            LogSetup.SetupLogger(-1);
            _log = LogManager.GetCurrentClassLogger();
            _creds = new BotCredentials();

            _log.Info("Starting Mewdeko v" + StatsService.BotVersion);

            _key = _creds.RedisKey();

            var conf = ConfigurationOptions.Parse(_creds.RedisOptions);
            try
            {
                _redis = ConnectionMultiplexer.Connect(conf);
            }
            catch (RedisConnectionException ex)
            {
                _log.Error("Redis error. Make sure Redis is installed and running as a service.");
                _log.Fatal(ex.ToString());
                Helpers.ReadErrorAndExit(11);
            }

            var imgCache = new RedisImagesCache(_redis, _creds); //reload images into redis
            if (!imgCache.AllKeysExist().GetAwaiter()
                .GetResult()) // but only if the keys don't exist. If images exist, you have to reload them manually
                imgCache.Reload().GetAwaiter().GetResult();
            else
                _log.Info("Images are already present in redis. Use .imagesreload to force update if needed.");

            //setup initial shard statuses
            _defaultShardState = new ShardComMessage
            {
                ConnectionState = ConnectionState.Disconnected,
                Guilds = 0,
                Time = DateTime.UtcNow
            };
            var db = _redis.GetDatabase();
            //clear previous statuses
            db.KeyDelete(_key + "_shardstats");

            _shardProcesses = new Process[_creds.TotalShards];

#if GLOBAL_Mewdeko
            var shardIdsEnum = Enumerable.Range(1, 31)
                .Concat(Enumerable.Range(33, _creds.TotalShards - 33))
                .Shuffle()
                .Prepend(32)
                .Prepend(0);
#else
            var shardIdsEnum = Enumerable.Range(1, _creds.TotalShards - 1)
                .Shuffle()
                .Prepend(0);
#endif

            var shardIds = shardIdsEnum
                .ToArray();
            for (var i = 0; i < shardIds.Length; i++)
            {
                var id = shardIds[i];
                //add it to the list of shards which should be started
#if DEBUG
                if (id > 0)
                    _shardStartQueue.Enqueue(id);
                else
                    _shardProcesses[id] = Process.GetCurrentProcess();
#else
                _shardStartQueue.Enqueue(id);
#endif
                //set the shard's initial state in redis cache
                var msg = _defaultShardState.Clone();
                msg.ShardId = id;
                //this is to avoid the shard coordinator thinking that
                //the shard is unresponsive while starting up
                var delay = 45;
#if GLOBAL_Mewdeko
                delay = 180;
#endif
                msg.Time = DateTime.UtcNow + TimeSpan.FromSeconds(delay * (id + 1));
                db.ListRightPush(_key + "_shardstats",
                    JsonConvert.SerializeObject(msg),
                    flags: CommandFlags.FireAndForget);
            }

            _curProcessId = Process.GetCurrentProcess().Id;

            //subscribe to shardcoord events
            var sub = _redis.GetSubscriber();

            //send is called when shard status is updated. Every 7.5 seconds atm
            sub.Subscribe(_key + "_shardcoord_send",
                OnDataReceived,
                CommandFlags.FireAndForget);

            //called to stop the shard, although the shard will start again when it finds out it's dead
            sub.Subscribe(_key + "_shardcoord_stop",
                OnStop,
                CommandFlags.FireAndForget);

            //called kill the bot
            sub.Subscribe(_key + "_die",
                (ch, x) => Environment.Exit(0),
                CommandFlags.FireAndForget);
        }

        private void OnStop(RedisChannel ch, RedisValue data)
        {
            var shardId = JsonConvert.DeserializeObject<int>(data);
            OnStop(shardId);
        }

        private void OnStop(int shardId)
        {
            var db = _redis.GetDatabase();
            var msg = _defaultShardState.Clone();
            msg.ShardId = shardId;
            db.ListSetByIndex(_key + "_shardstats",
                shardId,
                JsonConvert.SerializeObject(msg),
                CommandFlags.FireAndForget);
            var p = _shardProcesses[shardId];
            if (p == null)
                return; // ignore
            _shardProcesses[shardId] = null;
            try
            {
                p.KillTree();
                p.Dispose();
            }
            catch
            {
            }
        }

        private void OnDataReceived(RedisChannel ch, RedisValue data)
        {
            var msg = JsonConvert.DeserializeObject<ShardComMessage>(data);
            if (msg == null)
                return;
            var db = _redis.GetDatabase();
            //sets the shard state
            db.ListSetByIndex(_key + "_shardstats",
                msg.ShardId,
                data,
                CommandFlags.FireAndForget);
            if (msg.ConnectionState == ConnectionState.Disconnected
                || msg.ConnectionState == ConnectionState.Disconnecting)
            {
                _log.Error("!!! SHARD {0} IS IN {1} STATE !!!", msg.ShardId, msg.ConnectionState.ToString());

                OnShardUnavailable(msg.ShardId);
            }
            else
            {
                // remove the shard from the waiting list if it's on it,
                // because it's connected/connecting now
                _shardRestartWaitingList.TryRemove(msg.ShardId);
            }
        }

        private void OnShardUnavailable(int shardId)
        {
            //if the shard is dc'd, add it to the restart waiting list
            if (!_shardRestartWaitingList.Add(shardId))
            {
                //if it's already on the waiting list
                //stop the shard
                OnStop(shardId);
                //add it to the start queue (start the shard)
                _shardStartQueue.Enqueue(shardId);
                //remove it from the waiting list
                _shardRestartWaitingList.TryRemove(shardId);
            }
        }

        public async Task RunAsync()
        {
            //this task will complete when the initial start of the shards 
            //is complete, but will keep running in order to restart shards
            //which are disconnected for too long
            var tsc = new TaskCompletionSource<bool>();
            var _ = Task.Run(async () =>
            {
                do
                {
                    //start a shard which is scheduled for start every 6 seconds 
                    while (_shardStartQueue.TryPeek(out var id))
                    {
                        // if the shard is on the waiting list again
                        // remove it since it's starting up now

                        _shardRestartWaitingList.TryRemove(id);
                        //if the task is already completed,
                        //it means the initial shard starting is done,
                        //and this is an auto-restart
                        if (tsc.Task.IsCompleted)
                            _log.Warn("Auto-restarting shard {0}, {1} more in queue.", id, _shardStartQueue.Count);
                        else
                            _log.Warn("Starting shard {0}, {1} more in queue.", id, _shardStartQueue.Count - 1);
                        var rem = _shardProcesses[id];
                        if (rem != null)
                            try
                            {
                                rem.KillTree();
                                rem.Dispose();
                            }
                            catch
                            {
                            }

                        _shardProcesses[id] = StartShard(id);
                        _shardStartQueue.TryDequeue(out var __);
                        await Task.Delay(10000).ConfigureAwait(false);
                    }

                    tsc.TrySetResult(true);
                    await Task.Delay(6000).ConfigureAwait(false);
                } while (true);
                // ^ keep checking for shards which need to be restarted
            });

            //restart unresponsive shards
            _ = Task.Run(async () =>
            {
                //after all shards have started initially
                await tsc.Task.ConfigureAwait(false);
                while (true)
                {
                    await Task.Delay(15000).ConfigureAwait(false);
                    try
                    {
                        var db = _redis.GetDatabase();
                        //get all shards which didn't communicate their status in the last 30 seconds
                        var all = db.ListRange(_creds.RedisKey() + "_shardstats")
                            .Select(x => JsonConvert.DeserializeObject<ShardComMessage>(x));
                        var statuses = all
                            .Where(x => x.Time < DateTime.UtcNow - TimeSpan.FromSeconds(30))
                            .ToArray();

                        if (!statuses.Any())
                        {
#if DEBUG
                            for (var i = 0; i < _shardProcesses.Length; i++)
                            {
                                var p = _shardProcesses[i];
                                if (p == null || p.HasExited)
                                {
                                    _log.Warn("Scheduling shard {0} for restart because it's process is stopped.", i);
                                    _shardStartQueue.Enqueue(i);
                                }
                            }
#endif
                        }
                        else
                        {
                            for (var i = 0; i < statuses.Length; i++)
                            {
                                var s = statuses[i];
                                OnStop(s.ShardId);
                                _shardStartQueue.Enqueue(s.ShardId);

                                //to prevent shards which are already scheduled for restart to be scheduled again
                                s.Time = DateTime.UtcNow + TimeSpan.FromSeconds(60 * _shardStartQueue.Count);
                                db.ListSetByIndex(_key + "_shardstats", s.ShardId,
                                    JsonConvert.SerializeObject(s), CommandFlags.FireAndForget);
                                _log.Warn("Shard {0} is scheduled for a restart because it's unresponsive.", s.ShardId);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _log.Error(ex);
                        throw;
                    }
                }
            });

            await tsc.Task.ConfigureAwait(false);
        }

        private Process StartShard(int shardId)
        {
            return Process.Start(new ProcessStartInfo
            {
                FileName = _creds.ShardRunCommand,
                Arguments = string.Format(_creds.ShardRunArguments, shardId, _curProcessId, "")
            });
            // last "" in format is for backwards compatibility
            // because current startup commands have {2} in them probably
        }

        public async Task RunAndBlockAsync()
        {
            try
            {
                await RunAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _log.Error(ex);
                foreach (var p in _shardProcesses)
                {
                    if (p == null)
                        continue;
                    try
                    {
                        p.KillTree();
                        p.Dispose();
                    }
                    catch
                    {
                    }
                }

                return;
            }

            await Task.Delay(-1).ConfigureAwait(false);
        }

        private class ShardsCoordinatorQueue
        {
            private readonly object _locker = new();
            private readonly Queue<int> _queue = new();
            private readonly HashSet<int> _set = new();
            public int Count => _queue.Count;

            public void Enqueue(int i)
            {
                lock (_locker)
                {
                    if (_set.Add(i))
                        _queue.Enqueue(i);
                }
            }

            public bool TryPeek(out int id)
            {
                lock (_locker)
                {
                    return _queue.TryPeek(out id);
                }
            }

            public bool TryDequeue(out int id)
            {
                lock (_locker)
                {
                    if (_queue.TryDequeue(out id))
                    {
                        _set.Remove(id);
                        return true;
                    }
                }

                return false;
            }
        }
    }
}