namespace Mewdeko.Services
{
    /// <summary>
    /// Interface for managing and accessing cached images.
    /// </summary>
    public interface IImageCache
    {
        /// <summary>
        /// Gets the image URLs.
        /// </summary>
        ImageUrls ImageUrls { get; }

        /// <summary>
        /// Gets a read-only list of cached images for heads.
        /// </summary>
        IReadOnlyList<byte[]> Heads { get; }

        /// <summary>
        /// Gets a read-only list of cached images for tails.
        /// </summary>
        IReadOnlyList<byte[]> Tails { get; }

        /// <summary>
        /// Gets a read-only list of cached images for dice.
        /// </summary>
        IReadOnlyList<byte[]> Dice { get; }

        /// <summary>
        /// Gets a read-only list of cached images for slot machine emojis.
        /// </summary>
        IReadOnlyList<byte[]> SlotEmojis { get; }

        /// <summary>
        /// Gets a read-only list of cached currency images.
        /// </summary>
        IReadOnlyList<byte[]> Currency { get; }

        /// <summary>
        /// Gets the background image for the slot machine.
        /// </summary>
        byte[] SlotBackground { get; }

        /// <summary>
        /// Gets the background image for XP.
        /// </summary>
        byte[] XpBackground { get; }

        /// <summary>
        /// Gets the image for RIP (rest in peace).
        /// </summary>
        byte[] Rip { get; }

        /// <summary>
        /// Gets the overlay image for RIP.
        /// </summary>
        byte[] RipOverlay { get; }

        /// <summary>
        /// Gets a cached image by key.
        /// </summary>
        /// <param name="key">The key associated with the cached image.</param>
        /// <returns>The cached image as a byte array.</returns>
        byte[] GetCard(string key);

        /// <summary>
        /// Reloads the image cache.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task Reload();
    }
}