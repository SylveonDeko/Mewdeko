namespace Mewdeko.Modules.Nsfw.Common;

/// <summary>
///     Represents an object from Safebooru.
/// </summary>
public class SafebooruElement : IImageData
{
    /// <summary>
    ///     Gets or sets the directory where the image is located.
    /// </summary>
    public string Directory { get; set; }

    /// <summary>
    ///     Gets or sets the image filename.
    /// </summary>
    public string Image { get; set; }

    /// <summary>
    ///     Gets the URL of the image file.
    /// </summary>
    public string FileUrl
    {
        get
        {
            return $"https://safebooru.org/images/{Directory}/{Image}";
        }
    }

    /// <summary>
    ///     Gets or sets the rating of the image.
    /// </summary>
    public string Rating { get; set; }

    /// <summary>
    ///     Gets or sets the tags associated with the image.
    /// </summary>
    public string Tags { get; set; }

    /// <summary>
    ///     Converts this <see cref="SafebooruElement" /> to <see cref="ImageData" />.
    /// </summary>
    /// <param name="type">The type of Booru for this image.</param>
    /// <returns>An <see cref="ImageData" /> object.</returns>
    public ImageData ToCachedImageData(Booru type)
    {
        return new ImageData(FileUrl, Booru.Safebooru, Tags.Split(' '), Rating);
    }
}