using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Discord.WebSocket;
using Mewdeko.Services;
using Serilog;

namespace Mewdeko.Services
{
    public class SingleProcessCoordinator : ICoordinator
    {
        private readonly IBotCredentials _creds;
        private readonly DiscordSocketClient _client;

        public SingleProcessCoordinator(IBotCredentials creds, DiscordSocketClient client)
        {
            _creds = creds;
            _client = client;
        }
        public bool RestartBot()
        {
            if (string.IsNullOrWhiteSpace(_creds.RestartCommand?.Cmd)
                || string.IsNullOrWhiteSpace(_creds.RestartCommand?.Args))
            {
                Log.Error("You must set RestartCommand.Cmd and RestartCommand.Args in creds.yml");
                return false;
            }

            Process.Start(_creds.RestartCommand.Cmd, _creds.RestartCommand.Args);
            _ = Task.Run(async () =>
            {
                await Task.Delay(2000);
                Die();
            });
            return true;
        }

        public void Die()
        {
            Environment.Exit(5);
        }

        public bool RestartShard(int shardId)
        {
            return RestartBot();
        }

        public IList<ShardStatus> GetAllShardStatuses()
        {
            return new[]
            {
                new ShardStatus()
                {
                    ConnectionState = _client.ConnectionState,
                    GuildCount = _client.Guilds.Count,
                    LastUpdate = DateTime.UtcNow,
                    ShardId = _client.ShardId
                }
            };
        }

        public int GetGuildCount()
        {
            return _client.Guilds.Count;
        }
    }
}
