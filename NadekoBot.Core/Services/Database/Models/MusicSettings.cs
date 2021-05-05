namespace NadekoBot.Core.Services.Database.Models
{
    public class MusicSettings : DbEntity
    {
        public int GuildConfigId { get; set; }
        public GuildConfig GuildConfig { get; set; }

        public bool SongAutoDelete { get; set; } = false;
        public ulong? MusicChannelId { get; set; } = null;
    }
}
