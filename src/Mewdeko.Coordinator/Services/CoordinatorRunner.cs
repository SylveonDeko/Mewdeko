using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Mewdeko.Coordinator.Shared;
using Microsoft.Extensions.Hosting;
using Serilog;
using YamlDotNet.Serialization;

namespace Mewdeko.Coordinator.Services;

public sealed class CoordinatorRunner : BackgroundService
{
    private const string ConfigPath = "coord.yml";

    private const string GracefulStatePath = "graceful.json";
    private const string GracefulStateBackupPath = "graceful_old.json";

    private readonly Serializer serializer;
    private readonly Deserializer deserializer;

    private Config config;
    private ShardStatus[] shardStatuses;

    private readonly object locker = new();
    private readonly Random rng;
    private bool gracefulImminent;

    public CoordinatorRunner()
    {
        serializer = new Serializer();
        deserializer = new Deserializer();
        config = LoadConfig();
        rng = new Random();

        if (!TryRestoreOldState())
            InitAll();
    }

    private Config LoadConfig()
    {
        lock (locker)
        {
            return deserializer.Deserialize<Config>(File.ReadAllText(ConfigPath));
        }
    }

    private void SaveConfig(in Config coordConfig)
    {
        lock (locker)
        {
            var output = serializer.Serialize(coordConfig);
            File.WriteAllText(ConfigPath, output);
        }
    }

    public void ReloadConfig()
    {
        lock (locker)
        {
            var oldConfig = config;
            var newConfig = LoadConfig();
            if (oldConfig.TotalShards != newConfig.TotalShards)
            {
                KillAll();
            }

            config = newConfig;
            if (oldConfig.TotalShards != newConfig.TotalShards)
            {
                InitAll();
            }
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Log.Information("Executing");

        var first = true;
        while (!stoppingToken.IsCancellationRequested)
        {
            if (config.ClientId == 0)
            {
                Log.Error("Client ID needs to be set to start the coordinator. Exiting.");
                Environment.Exit(1);
                return;
            }

            try
            {
                var hadAction = false;
                lock (locker)
                {
                    var shardIds = Enumerable.Range(0, 1)
                        .Concat(Enumerable.Range(1, config.TotalShards - 1)
                            .OrderBy(_ => rng.Next())) // then all other shards in a random order
                        .Distinct()
                        .ToList();

                    if (first)
                    {
                        // Log.Information("Startup order: {StartupOrder}",string.Join(' ', shardIds));
                        first = false;
                    }

                    foreach (var shardId in shardIds)
                    {
                        if (stoppingToken.IsCancellationRequested)
                            break;

                        var status = shardStatuses[shardId];

                        if (status.ShouldRestart)
                        {
                            Log.Warning("Shard {ShardId} is restarting (scheduled)...", shardId);
                            hadAction = true;
                            StartShard(shardId);
                            break;
                        }

                        if (status.Process is null or { HasExited: true })
                        {
                            Log.Warning("Shard {ShardId} is starting (process)...", shardId);
                            hadAction = true;
                            StartShard(shardId);
                            break;
                        }

                        if (DateTime.UtcNow - status.LastUpdate >
                            TimeSpan.FromSeconds(config.UnresponsiveSec))
                        {
                            Log.Warning("Shard {ShardId} is restarting (unresponsive)...", shardId);
                            hadAction = true;
                            StartShard(shardId);
                            break;
                        }

                        if (status.StateCounter > 8 && status.State != ConnState.Connected)
                        {
                            Log.Warning("Shard {ShardId} is restarting (stuck)...", shardId);
                            hadAction = true;
                            StartShard(shardId);
                            break;
                        }
                    }
                }

                if (hadAction)
                {
                    await Task.Delay(config.RecheckIntervalMs, stoppingToken).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in coordinator: {Message}", ex.Message);
            }

            await Task.Delay(5000, stoppingToken).ConfigureAwait(false);
        }
    }

    private void StartShard(int shardId)
    {
        var status = shardStatuses[shardId];
        try
        {
            if (status.Process is { HasExited: false } p)
            {
                try
                {
                    p.Kill(true);
                }
                catch
                {
                    // ignored
                }
            }

            status.Process?.Dispose();
        }
        catch
        {
            // ignored
        }

        var proc = StartShardProcess(shardId);
        shardStatuses[shardId] = status with
        {
            Process = proc,
            LastUpdate = DateTime.UtcNow,
            State = ConnState.Disconnected,
            ShouldRestart = false,
            StateCounter = 0
        };
    }

    private Process StartShardProcess(int shardId) =>
        Process.Start(new ProcessStartInfo
        {
            FileName = config.ShardStartCommand,
            Arguments = string.Format(config.ShardStartArgs,
                shardId,
                config.TotalShards),
            EnvironmentVariables =
            {
                {
                    "MEWDEKO_IS_COORDINATED", "1"
                }
            }
            // CreateNoWindow = true,
            // UseShellExecute = false,
        });

    public bool Heartbeat(int shardId, int guildCount, ConnState state, int userCount)
    {
        lock (locker)
        {
            if (shardId >= shardStatuses.Length)
                throw new ArgumentOutOfRangeException(nameof(shardId));

            var status = shardStatuses[shardId];
            status = shardStatuses[shardId] = status with
            {
                GuildCount = guildCount,
                State = state,
                LastUpdate = DateTime.UtcNow,
                StateCounter = status.State == state
                    ? status.StateCounter + 1
                    : 1,
                UserCount = userCount
            };
            if (status.StateCounter > 1 && status.State == ConnState.Disconnected)
            {
                Log.Warning("Shard {ShardId} is in DISCONNECTED state! ({StateCounter})",
                    status.ShardId,
                    status.StateCounter);
            }

            return gracefulImminent;
        }
    }

    public void SetShardCount(int totalShards)
    {
        lock (locker)
        {
            SaveConfig(new Config(
                totalShards,
                config.RecheckIntervalMs,
                config.ShardStartCommand,
                config.ShardStartArgs,
                config.UnresponsiveSec,
                config.ClientId));
        }
    }

    public void RestartShard(int shardId)
    {
        lock (locker)
        {
            if (shardId >= shardStatuses.Length)
                throw new ArgumentOutOfRangeException(nameof(shardId));

            shardStatuses[shardId] = shardStatuses[shardId] with
            {
                ShouldRestart = true, StateCounter = 0
            };
        }
    }

    public void RestartAll(bool nuke)
    {
        lock (locker)
        {
            if (nuke)
            {
                KillAll();
            }

            QueueAll();
        }
    }

    private void KillAll()
    {
        lock (locker)
        {
            for (var shardId = 0; shardId < shardStatuses.Length; shardId++)
            {
                var status = shardStatuses[shardId];
                if (status.Process is { } p)
                {
                    p.Kill();
                    p.Dispose();
                    shardStatuses[shardId] = status with
                    {
                        Process = null,
                        ShouldRestart = true,
                        LastUpdate = DateTime.UtcNow,
                        State = ConnState.Disconnected,
                        StateCounter = 0
                    };
                }
            }
        }
    }

    public void SaveState()
    {
        var coordState = new CoordState
        {
            StatusObjects = shardStatuses
                .Select(x => new JsonStatusObject
                {
                    Pid = x.Process?.Id, ConnectionState = x.State, GuildCount = x.GuildCount, UserCount = x.UserCount
                })
                .ToList()
        };
        var jsonState = JsonSerializer.Serialize(coordState, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        File.WriteAllText(GracefulStatePath, jsonState);
    }

    private bool TryRestoreOldState()
    {
        lock (locker)
        {
            if (!File.Exists(GracefulStatePath))
                return false;

            Log.Information("Restoring old coordinator state...");

            CoordState savedState;
            try
            {
                savedState = JsonSerializer.Deserialize<CoordState>(File.ReadAllText(GracefulStatePath));

                if (savedState is null)
                    throw new ArgumentException("Old state is null?!");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error deserializing old state: {Message}", ex.Message);
                File.Move(GracefulStatePath, GracefulStateBackupPath, overwrite: true);
                return false;
            }

            if (savedState.StatusObjects.Count != config.TotalShards)
            {
                Log.Error("Unable to restore old state because shard count doesn't match");
                File.Move(GracefulStatePath, GracefulStateBackupPath, overwrite: true);
                return false;
            }

            shardStatuses = new ShardStatus[config.TotalShards];

            for (var shardId = 0; shardId < shardStatuses.Length; shardId++)
            {
                var statusObj = savedState.StatusObjects[shardId];
                Process p = null;
                if (statusObj.Pid is { } pid)
                {
                    try
                    {
                        p = Process.GetProcessById(pid);
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Process for shard {ShardId} is not runnning", shardId);
                    }
                }

                shardStatuses[shardId] = new ShardStatus(
                    shardId,
                    DateTime.UtcNow,
                    statusObj.GuildCount,
                    statusObj.ConnectionState,
                    p is null,
                    p,
                    UserCount: statusObj.UserCount);
            }

            File.Move(GracefulStatePath, GracefulStateBackupPath, overwrite: true);
            Log.Information("Old state restored!");
            return true;
        }
    }

    private void InitAll()
    {
        lock (locker)
        {
            shardStatuses = new ShardStatus[config.TotalShards];
            for (var shardId = 0; shardId < shardStatuses.Length; shardId++)
            {
                shardStatuses[shardId] = new ShardStatus(shardId, DateTime.UtcNow);
            }
        }
    }

    private void QueueAll()
    {
        lock (locker)
        {
            for (var shardId = 0; shardId < shardStatuses.Length; shardId++)
            {
                shardStatuses[shardId] = shardStatuses[shardId] with
                {
                    ShouldRestart = true
                };
            }
        }
    }


    public ShardStatus GetShardStatus(int shardId)
    {
        lock (locker)
        {
            if (shardId >= shardStatuses.Length)
                throw new ArgumentOutOfRangeException(nameof(shardId));

            return shardStatuses[shardId];
        }
    }

    public List<ShardStatus> GetAllStatuses()
    {
        lock (locker)
        {
            var toReturn = new List<ShardStatus>(shardStatuses.Length);
            toReturn.AddRange(shardStatuses);
            return toReturn;
        }
    }

    public void PrepareGracefulShutdown()
    {
        lock (locker)
        {
            gracefulImminent = true;
        }
    }

    public static string GetConfigText() =>
        File.ReadAllText(ConfigPath);

    public void SetConfigText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentNullException(nameof(text), "coord.yml can't be empty");
        var coordConfig = deserializer.Deserialize<Config>(text);
        SaveConfig(in coordConfig);
        ReloadConfig();
    }
}