using System.Threading.Tasks;

namespace Mewdeko.Modules.Gambling.Common.WheelOfFortune;

public class WheelOfFortuneGame
{
    private readonly long bet;
    private readonly GamblingConfig config;
    private readonly ICurrencyService cs;

    private readonly MewdekoRandom rng;
    private readonly ulong userId;

    public WheelOfFortuneGame(ulong userId, long bet, GamblingConfig config, ICurrencyService cs)
    {
        rng = new MewdekoRandom();
        this.cs = cs;
        this.bet = bet;
        this.config = config;
        this.userId = userId;
    }

    public async Task<Result> SpinAsync()
    {
        var result = rng.Next(0, config.WheelOfFortune.Multipliers.Length);

        var amount = (long)(bet * config.WheelOfFortune.Multipliers[result]);

        if (amount > 0)
            await cs.AddAsync(userId, "Wheel Of Fortune - won", amount, true).ConfigureAwait(false);

        return new Result
        {
            Index = result, Amount = amount
        };
    }

    public class Result
    {
        public int Index { get; set; }
        public long Amount { get; set; }
    }
}