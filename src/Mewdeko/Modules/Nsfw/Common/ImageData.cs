namespace Mewdeko.Modules.Nsfw.Common;

/// <summary>
///     Represents image data.
/// </summary>
public class ImageData : IComparable<ImageData>
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ImageData" /> class with the specified URL, Booru type, tags, and
    ///     rating.
    /// </summary>
    /// <param name="url">The URL of the image file.</param>
    /// <param name="type">The type of Booru the image belongs to.</param>
    /// <param name="tags">The tags associated with the image.</param>
    /// <param name="rating">The rating of the image.</param>
    public ImageData(string url, Booru type, string[] tags, string rating)
    {
        FileUrl = type == Booru.Danbooru && !Uri.IsWellFormedUriString(url, UriKind.Absolute)
            ? $"https://danbooru.donmai.us{url}"
            : url.StartsWith("http", StringComparison.InvariantCulture)
                ? url
                : $"https:{url}";

        SearchType = type;
        Tags = tags.ToHashSet();
        Rating = rating;
    }

    /// <summary>
    ///     Gets the type of Booru for this image.
    /// </summary>
    public Booru SearchType { get; }

    /// <summary>
    ///     Gets the URL of the image file.
    /// </summary>
    public string FileUrl { get; }

    /// <summary>
    ///     Gets the set of tags associated with the image.
    /// </summary>
    public HashSet<string> Tags { get; }

    /// <summary>
    ///     Gets the rating of the image.
    /// </summary>
    public string Rating { get; }

    /// <inheritdoc />
    public int CompareTo(ImageData? other)
    {
        return string.Compare(FileUrl, other?.FileUrl, StringComparison.InvariantCulture);
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return FileUrl;
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return FileUrl.GetHashCode();
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return obj is ImageData ico && ico.FileUrl == FileUrl;
    }
}