using System;
using System.Collections.Generic;
using YamlDotNet.Serialization;

namespace NadekoBot.Core.Common.Configs
{
    public class GamblingConfig
    {
        [YamlMember(Description = @"Currency settings")]
        public CurrencyConfig Currency { get; set; }

        [YamlMember(Description = @"Minimum amount people can bet. >=0")]
        public long MinBet { get; set; }

        [YamlMember(Description = @"Maximum amount people can bet. Set 0 for unlimited.")]
        public long MaxBet { get; set; }

        [YamlMember(Description = @"Multipliers for gambling.")]
        public MultiplierConfig Multipliers { get; set; }

        [YamlMember(Description = @"Automatic currency generation settings.")]
        public GenerationConfig Generation { get; set; }

        [YamlMember(Description = @"Settings for timely command. (letting people claim X amount of currency every Y hours)")]
        public TimelyConfig Timely { get; set; }

        // // todo FUTURE decay
        // [YamlMember(Description = @"How much will each user's owned currency decay over time.")]
        // public DecayConfig Decay { get; set; }

        [YamlMember(Description = @"Settings for Wheel Of Fortune command.")]
        public WheelOfFortuneSettings WheelOfFortune { get; set; }

        [YamlMember(Description = @"Settings for BetRoll command.")]
        public BetRollConfig BetRoll { get; set; }

        [YamlMember(Description = @"Settings related to waifus")]
        public WaifuConfig Waifu { get; set; }
        
        
        public GamblingConfig()
        {
            this.BetRoll = new BetRollConfig()
            {
                Pairs = new BetRollConfig.Pair[]
                {
                    new BetRollConfig.Pair(99, 10),
                    new BetRollConfig.Pair(90, 4),
                    new BetRollConfig.Pair(66, 2)
                }
            };

            this.WheelOfFortune = new WheelOfFortuneSettings
            {
                Multipliers = new float[]
                {
                    1.7f,
                    1.5f,
                    0.2f,
                    0.1f,
                    0.3f,
                    0.5f,
                    1.2f,
                    2.4f,
                }
            };

            this.Waifu = new WaifuConfig
            {
                Items = new List<WaifuItem>()
                {
                    new WaifuItem("🥔", 5, "Potato"),
                    new WaifuItem("🍪", 10, "Cookie"),
                    new WaifuItem("🥖", 20, "Bread"),
                    new WaifuItem("🍭", 30, "Lollipop"),
                    new WaifuItem("🌹", 50, "Rose"),
                    new WaifuItem("🍺", 70, "Beer"),
                    new WaifuItem("🌮", 85, "Taco"),
                    new WaifuItem("💌", 100, "LoveLetter"),
                    new WaifuItem("🥛", 125, "Milk"),
                    new WaifuItem("🍕", 150, "Pizza"),
                    new WaifuItem("🍫", 200, "Chocolate"),
                    new WaifuItem("🍦", 250, "Icecream"),
                    new WaifuItem("🍣", 300, "Sushi"),
                    new WaifuItem("🍚", 400, "Rice"),
                    new WaifuItem("🍉", 500, "Watermelon"),
                    new WaifuItem("🍱", 600, "Bento"),
                    new WaifuItem("🎟", 800, "MovieTicket"),
                    new WaifuItem("🍰", 1000, "Cake"),
                    new WaifuItem("📔", 1500, "Book"),
                    new WaifuItem("🐱", 2000, "Cat"),
                    new WaifuItem("🐶", 2001, "Dog"),
                    new WaifuItem("🐼", 2500, "Panda"),
                    new WaifuItem("💄", 3000, "Lipstick"),
                    new WaifuItem("👛", 3500, "Purse"),
                    new WaifuItem("📱", 4000, "iPhone"),
                    new WaifuItem("👗", 4500, "Dress"),
                    new WaifuItem("💻", 5000, "Laptop"),
                    new WaifuItem("🎻", 7500, "Violin"),
                    new WaifuItem("🎹", 8000, "Piano"),
                    new WaifuItem("🚗", 9000, "Car"),
                    new WaifuItem("💍", 10000, "Ring"),
                    new WaifuItem("🛳", 12000, "Ship"),
                    new WaifuItem("🏠", 15000, "House"),
                    new WaifuItem("🚁", 20000, "Helicopter"),
                    new WaifuItem("🚀", 30000, "Spaceship"),
                    new WaifuItem("🌕", 50000, "Moon")
                }
            };

            Currency = new CurrencyConfig();
            MinBet = 0;
            MaxBet = 0;
            Multipliers = new MultiplierConfig();
            Generation = new GenerationConfig();
            Timely = new TimelyConfig();
            // Decay = new DecayConfig();
        }


        public class CurrencyConfig
        {
            [YamlMember(Description = @"What is the emoji/character which represents the currency")]
            public string Sign { get; set; } = "🌸";

            [YamlMember(Description = @"What is the name of the currency")]
            public string Name { get; set; } = "Nadeko Flower";
        }


        public class TimelyConfig
        {
            [YamlMember(Description = @"How much currency will the users get every time they run .timely command
setting to 0 or less will disable this feature")]
            public long Amount { get; set; } = 0;

            [YamlMember(Description = @"How often (in hours) can users claim currency with .timely command
setting to 0 or less will disable this feature")]
            public int Cooldown { get; set; } = 24;
        }


        public class MultiplierConfig
        {
            public float BetFlip { get; set; } = 1.95f;
        }


        public class BetRollConfig
        {
            [YamlMember(Description = @"When betroll is played, user will roll a number 0-100.
This setting will describe which multiplier is used for when the roll is higher than the given number.
Doesn't have to be ordered.")]
            public Pair[] Pairs { get; set; } = Array.Empty<Pair>();


            public class Pair
            {
                public int WhenAbove { get; set; }
                public float MultiplyBy { get; set; }

                public Pair()
                {
                }

                public Pair(int threshold, int multiplier)
                {
                    WhenAbove = threshold;
                    MultiplyBy = multiplier;
                }
            }
        }


        public class GenerationConfig
        {
            [YamlMember(Description = @"when currency is generated, should it also have a random password
associated with it which users have to type after the .pick command
in order to get it")]
            public bool HasPassword { get; set; } = true;

            [YamlMember(Description = @"Every message sent has a certain % chance to generate the currency
specify the percentage here (1 being 100%, 0 being 0% - for example
default is 0.02, which is 2%")]
            public float Chance { get; set; } = 0.02f;

            [YamlMember(Description = @"cooldown setting disables any chance a message can spawn currency
for a specified number of seconds after currency is generated")]
            public int GenCooldown { get; set; } = 10;

            [YamlMember(Description =
                @"Amount of currency that spawns is a random number between Min and Max value specified here.
Set them to the same number to always generate a constant number")]
            public long AmountMin { get; set; } = 1;

            [YamlMember(Description = @"")] public long AmountMax { get; set; } = 1;
        }


        public class DecayConfig
        {
            [YamlMember(Description =
                @"Percentage of user's current currency which will be deducted every 24h. 0 - 1 (1 is 100%, 0.5 50%, 0 disabled)")]
            public float Percent { get; set; } = 0;

            [YamlMember(Description = @"Maximum amount user's currency can decay. 0 for unlimited.")]
            public int MaxDecay { get; set; } = 0;

            [YamlMember(Description = @"Only users who have more than this amount will have decay.")]
            public int MinThreshold { get; set; } = 100;
        }


        public class WheelOfFortuneSettings
        {
            [YamlMember(Description = @"Self-Explanatory. Has to have 8 values, otherwise the command won't work.")]
            public float[] Multipliers { get; set; } = Array.Empty<float>();
        }
        
        public class WaifuItem
        {
            public string ItemEmoji { get; set; }
            public int Price { get; set; }
            public string Name { get; set; }

            public WaifuItem(string itemEmoji, int price, string item)
            {
                ItemEmoji = itemEmoji;
                Price = price;
                Name = item;
            }
        }

        public class WaifuConfig
        {
            public MultipliersData Multipliers { get; set; } = new MultipliersData();

            [YamlMember(Description = @"List of items available for gifting.")]
            public List<WaifuItem> Items { get; set; } = new List<WaifuItem>();
            
           public class MultipliersData
            {
                [YamlMember(Description = @"Multiplier for waifureset. Default 150.
Formula (at the time of writing this): 
price = (waifu_price * 1.25f) + ((number_of_divorces + changes_of_heart + 2) * WaifuReset) rounded up")]
                public double WaifuReset { get; set; } = 150;

                [YamlMember(Description = @"The minimum amount of currency that you have to pay 
in order to buy a waifu that does have a crush on you.
Default is 1.1
Example: If a waifu is worth 100, you will have to pay at least 100 * NormalClaim currency to claim her.
(100 * 1.1 = 110)")]
                public double NormalClaim { get; set; } = 1.1;

                [YamlMember(Description = @"The minimum amount of currency that you have to pay 
in order to buy a waifu that has a crush on you.
Default is 0.88
Example: If a waifu is worth 100, you will have to pay at least 100 * CrushClaim currency to claim her.
(100 * 0.88 = 88)")]
                public double CrushClaim { get; set; } = 0.88;

                [YamlMember(Description =
                    @"When divorcing a waifu, her new value will be her current value multiplied by this number.
Default 0.75 (meaning will lose 25% of her value)")]
                public double DivorceNewValue { get; set; } = 0.75;

                [YamlMember(Description = @"All gift prices will be multiplied by this number.
Default 1 (meaning no effect)")]
                public double AllGiftPrices { get; set; } = 1.0;

                [YamlMember(Description =
                    @"What percentage of the value of the gift will a waifu gain when she's gifted.
Default 0.95 (meaning 95%)
Example: if a waifu is worth 1000, and she receives a gift worth 100, her new value will be 1095)")]
                public double GiftEffect { get; set; } = 0.95;
            }
        }
    }
}