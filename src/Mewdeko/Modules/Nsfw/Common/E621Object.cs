namespace Mewdeko.Modules.Nsfw.Common
{
    /// <summary>
    /// Represents an image object retrieved from the E621 website.
    /// </summary>
    public class E621Object : IImageData
    {
        /// <summary>
        /// Gets or sets the file data of the image.
        /// </summary>
        public FileData File { get; set; }

        /// <summary>
        /// Gets or sets the tag data of the image.
        /// </summary>
        public TagData Tags { get; set; }

        /// <summary>
        /// Gets or sets the score data of the image.
        /// </summary>
        public ScoreData Score { get; set; }

        /// <summary>
        /// Converts the E621 image object to a cached image data object.
        /// </summary>
        /// <param name="type">The type of Booru the image belongs to.</param>
        /// <returns>The cached image data object.</returns>
        public ImageData ToCachedImageData(Booru type)
            => new(File.Url, Booru.E621, Tags.General, Score.Total.ToString());

        /// <summary>
        /// Represents the file data of an E621 image.
        /// </summary>
        public class FileData
        {
            /// <summary>
            /// Gets or sets the URL of the image file.
            /// </summary>
            public string Url { get; set; }
        }

        /// <summary>
        /// Represents the tag data of an E621 image.
        /// </summary>
        public class TagData
        {
            /// <summary>
            /// Gets or sets the general tags associated with the image.
            /// </summary>
            public string[] General { get; set; }
        }

        /// <summary>
        /// Represents the score data of an E621 image.
        /// </summary>
        public class ScoreData
        {
            /// <summary>
            /// Gets or sets the total score of the image.
            /// </summary>
            public int Total { get; set; }
        }
    }
}