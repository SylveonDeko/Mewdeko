using System.Text.Json.Serialization;

namespace Mewdeko.Modules.Nsfw.Common;

/// <summary>
///     Represents an object from Rule34.
/// </summary>
public class Rule34Object : IImageData
{
    /// <summary>
    ///     Gets or init the image filename.
    /// </summary>
    public string Image { get; init; }

    /// <summary>
    ///     Gets or init the directory where the image is located.
    /// </summary>
    public int Directory { get; init; }

    /// <summary>
    ///     Gets or init the tags associated with the image.
    /// </summary>
    public string Tags { get; init; }

    /// <summary>
    ///     Gets or init the score of the image.
    /// </summary>
    public int Score { get; init; }

    /// <summary>
    ///     Gets or init the URL of the image file.
    /// </summary>
    [JsonPropertyName("file_url")]
    public string FileUrl { get; init; }

    /// <summary>
    ///     Converts this <see cref="Rule34Object" /> to <see cref="ImageData" />.
    /// </summary>
    /// <param name="type">The type of Booru for this image.</param>
    /// <returns>An <see cref="ImageData" /> object.</returns>
    public ImageData ToCachedImageData(Booru type)
    {
        return new ImageData(FileUrl, Booru.Rule34, Tags.Split(' '), Score.ToString());
    }
}