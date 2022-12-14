namespace Mewdeko.Modules.Gambling.Common;

public class Betroll
{
    private readonly Random rng;

    private readonly IOrderedEnumerable<GamblingConfig.BetRollConfig.Pair> thresholdPairs;

    public Betroll(GamblingConfig.BetRollConfig settings)
    {
        thresholdPairs = settings.Pairs.OrderByDescending(x => x.WhenAbove);
        rng = new Random();
    }

    public Result Roll()
    {
        var roll = rng.Next(0, 101);

        var pair = thresholdPairs.FirstOrDefault(x => x.WhenAbove < roll);
        if (pair is null)
        {
            return new Result
            {
                Multiplier = 0, Roll = roll
            };
        }

        return new Result
        {
            Multiplier = pair.MultiplyBy, Roll = roll, Threshold = pair.WhenAbove
        };
    }

    public class Result
    {
        public int Roll { get; set; }
        public float Multiplier { get; set; }
        public int Threshold { get; set; }
    }
}