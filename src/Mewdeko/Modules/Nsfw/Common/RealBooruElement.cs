namespace Mewdeko.Modules.Nsfw.Common;

/// <summary>
///     Represents an element from Realbooru.
/// </summary>
public class RealBooruElement : IImageData
{
    /// <summary>
    ///     Gets or init the image filename.
    /// </summary>
    public string Image { get; init; }

    /// <summary>
    ///     Gets or init the directory where the image is located.
    /// </summary>
    public string Directory { get; init; }

    /// <summary>
    ///     Gets or init the tags associated with the image.
    /// </summary>
    public string Tags { get; init; }

    /// <summary>
    ///     Gets or init the score of the image.
    /// </summary>
    public string? Score { get; init; }

    /// <summary>
    ///     Converts this <see cref="RealBooruElement" /> to <see cref="ImageData" />.
    /// </summary>
    /// <param name="type">The type of Booru for this image.</param>
    /// <returns>An <see cref="ImageData" /> object.</returns>
    public ImageData ToCachedImageData(Booru type)
    {
        return new ImageData($"https://realbooru.com/images/{Directory}/{Image}", Booru.Realbooru, Tags.Split(' '),
            Score);
    }
}