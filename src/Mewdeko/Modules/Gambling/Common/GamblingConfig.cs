using Mewdeko.Common.Yml;

namespace Mewdeko.Modules.Gambling.Common;

public sealed class GamblingConfig
{
    public GamblingConfig()
    {
        BetRoll = new BetRollConfig();
        WheelOfFortune = new WheelOfFortuneSettings();
        Waifu = new WaifuConfig();
        Currency = new CurrencyConfig();
        BetFlip = new BetFlipConfig();
        Generation = new GenerationConfig();
        Timely = new TimelyConfig();
        Decay = new DecayConfig();
    }

    [Comment(@"DO NOT CHANGE")]
    public int Version { get; set; } = 1;

    [Comment(@"Currency settings")]
    public CurrencyConfig Currency { get; set; }

    [Comment(@"Minimum amount users can bet (>=0)")]
    public int MinBet { get; set; } = 0;

    [Comment(@"Maximum amount users can bet
Set 0 for unlimited")]
    public int MaxBet { get; set; } = 0;

    [Comment(@"Settings for betflip command")]
    public BetFlipConfig BetFlip { get; set; }

    [Comment(@"Settings for betroll command")]
    public BetRollConfig BetRoll { get; set; }

    [Comment(@"Automatic currency generation settings.")]
    public GenerationConfig Generation { get; set; }

    [Comment(@"Settings for timely command
(letting people claim X amount of currency every Y hours)")]
    public TimelyConfig Timely { get; set; }

    [Comment(@"How much will each user's owned currency decay over time.")]
    public DecayConfig Decay { get; set; }

    [Comment(@"Settings for Wheel Of Fortune command.")]
    public WheelOfFortuneSettings WheelOfFortune { get; set; }

    [Comment(@"Settings related to waifus")]
    public WaifuConfig Waifu { get; set; }

    [Comment(@"Amount of currency selfhosters will get PER pledged dollar CENT.
1 = 100 currency per $. Used almost exclusively on public Mewdeko.")]
    public decimal PatreonCurrencyPerCent { get; set; } = 1;

    public class CurrencyConfig
    {
        [Comment(@"What is the emoji/character which represents the currency")]
        public string? Sign { get; set; } = "🌸";

        [Comment(@"What is the name of the currency")]
        public string? Name { get; set; } = "Mewdeko Flower";
    }

    public class TimelyConfig
    {
        [Comment(@"How much currency will the users get every time they run .timely command
setting to 0 or less will disable this feature")]
        public int Amount { get; set; }

        [Comment(@"How often (in hours) can users claim currency with .timely command
setting to 0 or less will disable this feature")]
        public int Cooldown { get; set; } = 24;
    }

    public class BetFlipConfig
    {
        [Comment(@"Bet multiplier if user guesses correctly")]
        public decimal Multiplier { get; set; } = 1.95M;
    }

    public class BetRollConfig
    {
        public BetRollConfig() =>
            Pairs = new[]
            {
                new(99, 10), new Pair(90, 4), new Pair(66, 2)
            };

        [Comment(@"When betroll is played, user will roll a number 0-100.
This setting will describe which multiplier is used for when the roll is higher than the given number.
Doesn't have to be ordered.")]
        public Pair[] Pairs { get; set; }

        public class Pair
        {
            public Pair()
            {
            }

            public Pair(int threshold, int multiplier)
            {
                WhenAbove = threshold;
                MultiplyBy = multiplier;
            }

            public int WhenAbove { get; set; }

            public float MultiplyBy { get; set; }
        }
    }

    public class GenerationConfig
    {
        [Comment(@"when currency is generated, should it also have a random password
associated with it which users have to type after the .pick command
in order to get it")]
        public bool HasPassword { get; set; } = true;

        [Comment(@"Every message sent has a certain % chance to generate the currency
specify the percentage here (1 being 100%, 0 being 0% - for example
default is 0.02, which is 2%")]
        public decimal Chance { get; set; } = 0.02M;

        [Comment(@"How many seconds have to pass for the next message to have a chance to spawn currency")]
        public int GenCooldown { get; set; } = 10;

        [Comment(@"Minimum amount of currency that can spawn")]
        public int MinAmount { get; set; } = 1;

        [Comment(@"Maximum amount of currency that can spawn.
 Set to the same value as MinAmount to always spawn the same amount")]
        public int MaxAmount { get; set; } = 1;
    }

    public class DecayConfig
    {
        [Comment(@"Percentage of user's current currency which will be deducted every 24h.
0 - 1 (1 is 100%, 0.5 50%, 0 disabled)")]
        public decimal Percent { get; set; } = 0;

        [Comment(@"Maximum amount of user's currency that can decay at each interval. 0 for unlimited.")]
        public int MaxDecay { get; set; } = 0;

        [Comment(@"Only users who have more than this amount will have their currency decay.")]
        public int MinThreshold { get; set; } = 99;

        [Comment(@"How often, in hours, does the decay run. Default is 24 hours")]
        public int HourInterval { get; set; } = 24;
    }

    public class WheelOfFortuneSettings
    {
        public WheelOfFortuneSettings() =>
            Multipliers = new[]
            {
                1.7M, 1.5M, 0.2M, 0.1M, 0.3M, 0.5M, 1.2M, 2.4M
            };

        [Comment(@"Self-Explanatory. Has to have 8 values, otherwise the command won't work.")]
        public decimal[] Multipliers { get; set; }
    }

    public class WaifuConfig
    {
        public WaifuConfig() =>
            Items = new List<WaifuItemModel>
            {
                new("🥔", 5, "Potato"),
                new("🍪", 10, "Cookie"),
                new("🥖", 20, "Bread"),
                new("🍭", 30, "Lollipop"),
                new("🌹", 50, "Rose"),
                new("🍺", 70, "Beer"),
                new("🌮", 85, "Taco"),
                new("💌", 100, "LoveLetter"),
                new("🥛", 125, "Milk"),
                new("🍕", 150, "Pizza"),
                new("🍫", 200, "Chocolate"),
                new("🍦", 250, "Icecream"),
                new("🍣", 300, "Sushi"),
                new("🍚", 400, "Rice"),
                new("🍉", 500, "Watermelon"),
                new("🍱", 600, "Bento"),
                new("🎟", 800, "MovieTicket"),
                new("🍰", 1000, "Cake"),
                new("📔", 1500, "Book"),
                new("🐱", 2000, "Cat"),
                new("🐶", 2001, "Dog"),
                new("🐼", 2500, "Panda"),
                new("💄", 3000, "Lipstick"),
                new("👛", 3500, "Purse"),
                new("📱", 4000, "iPhone"),
                new("👗", 4500, "Dress"),
                new("💻", 5000, "Laptop"),
                new("🎻", 7500, "Violin"),
                new("🎹", 8000, "Piano"),
                new("🚗", 9000, "Car"),
                new("💍", 10000, "Ring"),
                new("🛳", 12000, "Ship"),
                new("🏠", 15000, "House"),
                new("🚁", 20000, "Helicopter"),
                new("🚀", 30000, "Spaceship"),
                new("🌕", 50000, "Moon")
            };

        [Comment(@"Minimum price a waifu can have")]
        public int MinPrice { get; set; } = 50;

        public MultipliersData Multipliers { get; set; } = new();

        [Comment(@"List of items available for gifting.")]
        public List<WaifuItemModel> Items { get; set; }


        public class MultipliersData
        {
            [Comment(@"Multiplier for waifureset. Default 150.
Formula (at the time of writing this):
price = (waifu_price * 1.25f) + ((number_of_divorces + changes_of_heart + 2) * WaifuReset) rounded up")]
            public int WaifuReset { get; set; } = 150;

            [Comment(@"The minimum amount of currency that you have to pay
in order to buy a waifu who doesn't have a crush on you.
Default is 1.1
Example: If a waifu is worth 100, you will have to pay at least 100 * NormalClaim currency to claim her.
(100 * 1.1 = 110)")]
            public decimal NormalClaim { get; set; } = 1.1m;

            [Comment(@"The minimum amount of currency that you have to pay
in order to buy a waifu that has a crush on you.
Default is 0.88
Example: If a waifu is worth 100, you will have to pay at least 100 * CrushClaim currency to claim her.
(100 * 0.88 = 88)")]
            public decimal CrushClaim { get; set; } = 0.88M;

            [Comment(@"When divorcing a waifu, her new value will be her current value multiplied by this number.
Default 0.75 (meaning will lose 25% of her value)")]
            public decimal DivorceNewValue { get; set; } = 0.75M;

            [Comment(@"All gift prices will be multiplied by this number.
Default 1 (meaning no effect)")]
            public decimal AllGiftPrices { get; set; } = 1.0M;

            [Comment(@"What percentage of the value of the gift will a waifu gain when she's gifted.
Default 0.95 (meaning 95%)
Example: If a waifu is worth 1000, and she receives a gift worth 100, her new value will be 1095)")]
            public decimal GiftEffect { get; set; } = 0.95M;
        }
    }
}

public class WaifuItemModel
{
    public WaifuItemModel()
    {
    }

    public WaifuItemModel(string itemEmoji, int price, string name)
    {
        ItemEmoji = itemEmoji;
        Price = price;
        Name = name;
    }

    public string ItemEmoji { get; set; }

    public int Price { get; set; }

    public string Name { get; set; }

    public override string ToString() => Name;
}