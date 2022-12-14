using Mewdeko.Modules.Games.Common;

namespace Mewdeko.Modules.Gambling.Common.AnimalRacing;

public class AnimalRacingUser
{
    public AnimalRacingUser(string username, ulong userId, long bet)
    {
        Bet = bet;
        Username = username;
        UserId = userId;
    }

    public long Bet { get; }
    public string Username { get; }
    public ulong UserId { get; }
    public RaceAnimal? Animal { get; set; }
    public int Progress { get; set; }

    public override bool Equals(object? obj) =>
        obj is AnimalRacingUser x
        && x.UserId == UserId;

    public override int GetHashCode() => UserId.GetHashCode();
}