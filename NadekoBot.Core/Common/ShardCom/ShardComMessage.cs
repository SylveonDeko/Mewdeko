using System;
using Discord;

namespace NadekoBot.Common.ShardCom
{
    public class ShardComMessage
    {
        public int ShardId { get; set; }
        public ConnectionState ConnectionState { get; set; }
        public int Guilds { get; set; }
        public DateTime Time { get; set; }

        public ShardComMessage Clone() =>
            new ShardComMessage
            {
                ShardId = ShardId,
                ConnectionState = ConnectionState,
                Guilds = Guilds,
                Time = Time,
            };
    }
}
