using NadekoBot.Core.Services.Database.Models;

namespace NadekoBot.Modules.Gambling.Common.AnimalRacing
{
    public class AnimalRacingUser
    {
        public long Bet { get; }
        public string Username { get; }
        public ulong UserId { get; }
        public RaceAnimal Animal { get; set; }
        public int Progress { get; set; }

        public AnimalRacingUser(string username, ulong userId, long bet)
        {
            this.Bet = bet;
            this.Username = username;
            this.UserId = userId;
        }

        public override bool Equals(object obj)
        {
            return obj is AnimalRacingUser x
                ? x.UserId == this.UserId
                : false;
        }

        public override int GetHashCode()
        {
            return this.UserId.GetHashCode();
        }
    }
}
