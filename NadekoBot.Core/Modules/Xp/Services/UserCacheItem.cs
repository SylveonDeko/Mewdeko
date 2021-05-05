using Discord;

namespace NadekoBot.Modules.Xp.Services
{
    public class UserCacheItem
    {
        public IGuildUser User { get; set; }
        public IGuild Guild { get; set; }
        public IMessageChannel Channel { get; set; }
        public int XpAmount { get; set; }

        public override int GetHashCode()
        {
            return User.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            return obj is UserCacheItem uci && uci.User == User;
        }
    }
}
