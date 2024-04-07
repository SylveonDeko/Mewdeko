namespace Mewdeko.Modules.Searches.Common;

/// <summary>
/// Represents an object that holds information about an image cached from various image searching APIs.
/// </summary>
public class ImageCacherObject : IComparable<ImageCacherObject>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ImageCacherObject"/> class using data from a <see cref="DapiImageObject"/>.
    /// </summary>
    /// <param name="obj">The <see cref="DapiImageObject"/> containing image data.</param>
    /// <param name="type">The source <see cref="DapiSearchType"/> where the image was found.</param>
    public ImageCacherObject(DapiImageObject obj, DapiSearchType type)
    {
        if (type == DapiSearchType.Danbooru && !Uri.IsWellFormedUriString(obj.FileUrl, UriKind.Absolute))
        {
            FileUrl = $"https://danbooru.donmai.us{obj.FileUrl}";
        }
        else
        {
            FileUrl = obj.FileUrl.StartsWith("http", StringComparison.InvariantCulture)
                ? obj.FileUrl
                : $"https:{obj.FileUrl}";
        }

        SearchType = type;
        Rating = obj.Rating;
        Tags = [..(obj?.Tags ?? obj.TagString).Split(' ')];
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ImageCacherObject"/> class using specified image data.
    /// </summary>
    /// <param name="url">The URL of the image.</param>
    /// <param name="type">The source <see cref="DapiSearchType"/> where the image was found.</param>
    /// <param name="tags">A string of space-separated tags associated with the image.</param>
    /// <param name="rating">The rating of the image (e.g., safe, explicit).</param>
    public ImageCacherObject(string url, DapiSearchType type, string tags, string rating)
    {
        SearchType = type;
        FileUrl = url;
        Tags = [..tags.Split(' ')];
        Rating = rating;
    }

    /// <summary>
    /// Gets the type of the source where the image was found.
    /// </summary>
    public DapiSearchType SearchType { get; }

    /// <summary>
    /// Gets the URL of the image.
    /// </summary>
    public string FileUrl { get; }

    /// <summary>
    /// Gets a collection of tags associated with the image.
    /// </summary>
    public HashSet<string> Tags { get; }

    /// <summary>
    /// Gets the rating of the image (e.g., safe, explicit).
    /// </summary>
    public string Rating { get; }


    /// <summary>
    /// Compares the current object with another object of the same type.
    /// </summary>
    /// <param name="other">An object to compare with this object.</param>
    /// <returns>A value that indicates the relative order of the objects being compared.</returns>
    public int CompareTo(ImageCacherObject? other) =>
        string.Compare(FileUrl, other?.FileUrl, StringComparison.InvariantCulture);

    /// <summary>
    /// Returns a string that represents the current object.
    /// </summary>
    /// <returns>A string that represents the current object.</returns>
    public override string ToString() => FileUrl;
}