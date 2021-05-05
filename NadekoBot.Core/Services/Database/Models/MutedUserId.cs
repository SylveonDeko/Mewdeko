namespace NadekoBot.Core.Services.Database.Models
{
    public class MutedUserId : DbEntity
    {
        public ulong UserId { get; set; }

        public override int GetHashCode()
        {
            return UserId.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            return obj is MutedUserId mui
                ? mui.UserId == UserId
                : false;
        }
    }
}
