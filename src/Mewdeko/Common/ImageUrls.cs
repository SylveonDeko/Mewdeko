namespace Mewdeko.Common
{
    /// <summary>
    /// Represents URLs for various images used in the application.
    /// </summary>
    public class ImageUrls
    {
        /// <summary>
        /// Gets or sets the version of the image URLs.
        /// </summary>
        public int Version { get; set; } = 2;

        /// <summary>
        /// Gets or sets the URLs for coin images.
        /// </summary>
        public CoinData? Coins { get; set; }

        /// <summary>
        /// Gets or sets the URLs for currency images.
        /// </summary>
        public Uri[]? Currency { get; set; }

        /// <summary>
        /// Gets or sets the URLs for dice images.
        /// </summary>
        public Uri[]? Dice { get; set; }

        /// <summary>
        /// Gets or sets the URLs for XP-related images.
        /// </summary>
        public XpData? Xp { get; set; }

        /// <summary>
        /// Gets or sets the URLs for RIP-related images.
        /// </summary>
        public RipData? Rip { get; set; }

        /// <summary>
        /// Gets or sets the URLs for slot machine-related images.
        /// </summary>
        public SlotData? Slots { get; set; }

        /// <summary>
        /// Represents URLs for coin images.
        /// </summary>
        public class CoinData
        {
            /// <summary>
            /// Gets or sets the URLs for heads images.
            /// </summary>
            public Uri[]? Heads { get; set; }

            /// <summary>
            /// Gets or sets the URLs for tails images.
            /// </summary>
            public Uri[]? Tails { get; set; }
        }

        /// <summary>
        /// Represents URLs for XP-related images.
        /// </summary>
        public class XpData
        {
            /// <summary>
            /// Gets or sets the URL for the background image.
            /// </summary>
            public Uri? Bg { get; set; }
        }

        /// <summary>
        /// Represents URLs for RIP-related images.
        /// </summary>
        public class RipData
        {
            /// <summary>
            /// Gets or sets the URL for the background image.
            /// </summary>
            public Uri? Bg { get; set; }

            /// <summary>
            /// Gets or sets the URL for the overlay image.
            /// </summary>
            public Uri? Overlay { get; set; }
        }

        /// <summary>
        /// Represents URLs for slot machine-related images.
        /// </summary>
        public class SlotData
        {
            /// <summary>
            /// Gets or sets the URLs for emoji images.
            /// </summary>
            public Uri[]? Emojis { get; set; }

            /// <summary>
            /// Gets or sets the URLs for number images.
            /// </summary>
            public Uri[]? Numbers { get; set; }

            /// <summary>
            /// Gets or sets the URL for the background image.
            /// </summary>
            public Uri? Bg { get; set; }
        }
    }
}