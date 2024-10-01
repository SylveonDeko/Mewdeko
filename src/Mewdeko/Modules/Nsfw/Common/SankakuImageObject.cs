using System.Text.Json.Serialization;

namespace Mewdeko.Modules.Nsfw.Common;

/// <summary>
///     Represents an object from Sankaku.
/// </summary>
public class SankakuImageObject : IImageData
{
    /// <summary>
    ///     Gets or sets the URL of the image file.
    /// </summary>
    [JsonPropertyName("file_url")]
    public string FileUrl { get; set; }

    /// <summary>
    ///     Gets or sets the type of the image file.
    /// </summary>
    [JsonPropertyName("file_type")]
    public string FileType { get; set; }

    /// <summary>
    ///     Gets or sets the tags associated with the image.
    /// </summary>
    public Tag[] Tags { get; set; }

    /// <summary>
    ///     Gets or sets the total score of the image.
    /// </summary>
    [JsonPropertyName("total_score")]
    public int Score { get; set; }

    /// <summary>
    ///     Converts this <see cref="SankakuImageObject" /> to <see cref="ImageData" />.
    /// </summary>
    /// <param name="type">The type of Booru for this image.</param>
    /// <returns>An <see cref="ImageData" /> object.</returns>
    public ImageData ToCachedImageData(Booru type)
    {
        return new ImageData(FileUrl, Booru.Sankaku, Tags.Select(x => x.Name).ToArray(), Score.ToString());
    }

    /// <summary>
    ///     Represents a tag associated with the image.
    /// </summary>
    public class Tag
    {
        /// <summary>
        ///     Gets or sets the name of the tag.
        /// </summary>
        public string Name { get; set; }
    }
}