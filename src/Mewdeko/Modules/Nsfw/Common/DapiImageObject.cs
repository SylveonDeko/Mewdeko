using System.Text.Json.Serialization;

namespace Mewdeko.Modules.Nsfw.Common
{
    /// <summary>
    /// Represents an image object retrieved from a DAPI (Danbooru API) endpoint.
    /// </summary>
    public class DapiImageObject : IImageData
    {
        /// <summary>
        /// Gets or sets the URL of the image file.
        /// </summary>
        [JsonPropertyName("File_Url")]
        public string FileUrl { get; set; }

        /// <summary>
        /// Gets or sets the tags associated with the image.
        /// </summary>
        public string Tags { get; set; }

        /// <summary>
        /// Gets or sets the tag string associated with the image.
        /// </summary>
        [JsonPropertyName("Tag_String")]
        public string TagString { get; set; }

        /// <summary>
        /// Gets or sets the score of the image.
        /// </summary>
        public int Score { get; set; }

        /// <summary>
        /// Gets or sets the rating of the image.
        /// </summary>
        public string Rating { get; set; }

        /// <summary>
        /// Converts the <see cref="DapiImageObject"/> to a <see cref="ImageData"/> object suitable for caching.
        /// </summary>
        /// <param name="type">The type of booru platform from which the image was retrieved.</param>
        /// <returns>An <see cref="ImageData"/> object representing the image.</returns>
        public ImageData ToCachedImageData(Booru type)
            => new ImageData(FileUrl, type, Tags?.Split(' ') ?? TagString?.Split(' '), Score.ToString() ?? Rating);
    }
}