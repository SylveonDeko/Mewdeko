namespace NadekoBot.Core.Services.Database.Models
{
    public class DelMsgOnCmdChannel : DbEntity
    {
        public ulong ChannelId { get; set; }
        public bool State { get; set; }

        public override int GetHashCode()
        {
            return ChannelId.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            return obj is DelMsgOnCmdChannel x
                && x.ChannelId == ChannelId;
        }
    }
}
