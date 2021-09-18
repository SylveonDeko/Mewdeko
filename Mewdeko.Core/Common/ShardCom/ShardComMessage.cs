using System;
using Discord;

namespace Mewdeko.Common.ShardCom
{
    public class ShardComMessage
    {
        public int ShardId { get; set; }
        public ConnectionState ConnectionState { get; set; }
        public int Guilds { get; set; }
        public DateTime Time { get; set; }

        public ShardComMessage Clone()
        {
            return new ShardComMessage
            {
                ShardId = ShardId,
                ConnectionState = ConnectionState,
                Guilds = Guilds,
                Time = Time
            };
        }
    }
}