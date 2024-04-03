using System.Text.Json.Serialization;

namespace Mewdeko.Modules.Nsfw.Common
{
    /// <summary>
    /// Represents a container for Derpibooru image objects.
    /// </summary>
    public class DerpiContainer
    {
        /// <summary>
        /// Gets or sets the array of Derpibooru image objects.
        /// </summary>
        public DerpiImageObject[] Images { get; set; }
    }

    /// <summary>
    /// Represents an image object retrieved from Derpibooru.
    /// </summary>
    public class DerpiImageObject : IImageData
    {
        /// <summary>
        /// Gets or sets the view URL of the image.
        /// </summary>
        [JsonPropertyName("view_url")]
        public string ViewUrl { get; set; }

        /// <summary>
        /// Gets or sets the array of tags associated with the image.
        /// </summary>
        public string[] Tags { get; set; }

        /// <summary>
        /// Gets or sets the score of the image.
        /// </summary>
        public int Score { get; set; }

        /// <summary>
        /// Converts the Derpibooru image object to a cached image data object.
        /// </summary>
        /// <param name="type">The type of Booru the image belongs to.</param>
        /// <returns>The cached image data object.</returns>
        public ImageData ToCachedImageData(Booru type)
            => new(ViewUrl, type, Tags, Score.ToString("F1"));
    }
}