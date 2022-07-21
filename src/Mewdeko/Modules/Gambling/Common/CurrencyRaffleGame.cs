namespace Mewdeko.Modules.Gambling.Common;

public class CurrencyRaffleGame
{
    public enum Type
    {
        Mixed,
        Normal
    }

    private readonly HashSet<User> _users = new();

    public CurrencyRaffleGame(Type type) => GameType = type;

    public IEnumerable<User> Users => _users;
    public Type GameType { get; }

    public bool AddUser(IUser usr, long amount)
    {
        // if game type is normal, and someone already joined the game 
        // (that's the user who created it)
        if (GameType == Type.Normal && _users.Count > 0 &&
            _users.First().Amount != amount)
        {
            return false;
        }

        if (!_users.Add(new User
        {
            DiscordUser = usr,
            Amount = amount
        }))
        {
            return false;
        }

        return true;
    }

    public User GetWinner()
    {
        var rng = new MewdekoRandom();
        if (GameType == Type.Mixed)
        {
            var num = rng.NextLong(0L, Users.Sum(x => x.Amount));
            var sum = 0L;
            foreach (var u in Users)
            {
                sum += u.Amount;
                if (sum > num)
                    return u;
            }
        }

        var usrs = _users.ToArray();
        return usrs[rng.Next(0, usrs.Length)];
    }

    public class User
    {
        public IUser DiscordUser { get; set; }
        public long Amount { get; set; }

        public override int GetHashCode() => DiscordUser.GetHashCode();

        public override bool Equals(object? obj) =>
            obj is User u
&& u.DiscordUser == DiscordUser;
    }
}