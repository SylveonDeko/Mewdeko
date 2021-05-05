using System;

namespace NadekoBot.Core.Common
{
    public class ImageUrls
    {
        public int Version { get; set; } = 2;

        public CoinData Coins { get; set; }
        public Uri[] Currency { get; set; }
        public Uri[] Dice { get; set; }
        public RategirlData Rategirl { get; set; }
        public XpData Xp { get; set; }

        //new
        public RipData Rip { get; set; }
        public SlotData Slots { get; set; }

        public class RipData
        {
            public Uri Bg { get; set; }
            public Uri Overlay { get; set; }
        }

        public class SlotData
        {
            public Uri[] Emojis { get; set; }
            public Uri[] Numbers { get; set; }
            public Uri Bg { get; set; }
        }

        public class CoinData
        {
            public Uri[] Heads { get; set; }
            public Uri[] Tails { get; set; }
        }

        public class RategirlData
        {
            public Uri Matrix { get; set; }
            public Uri Dot { get; set; }
        }

        public class XpData
        {
            public Uri Bg { get; set; }
        }
    }
}
