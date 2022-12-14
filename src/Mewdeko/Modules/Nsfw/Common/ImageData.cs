namespace Mewdeko.Modules.Nsfw.Common;

public class ImageData : IComparable<ImageData>
{
    public Booru SearchType { get; }
    public string FileUrl { get; }
    public HashSet<string> Tags { get; }
    public string Rating { get; }

    public ImageData(string url, Booru type, string[] tags, string rating)
    {
        if (type == Booru.Danbooru && !Uri.IsWellFormedUriString(url, UriKind.Absolute))
        {
            FileUrl = $"https://danbooru.donmai.us{url}";
        }
        else
        {
            FileUrl = url.StartsWith("http", StringComparison.InvariantCulture) ? url : $"https:{url}";
        }

        SearchType = type;
        FileUrl = url;
        Tags = tags.ToHashSet();
        Rating = rating;
    }

    public override string ToString()
        => FileUrl;

    public override int GetHashCode() => FileUrl.GetHashCode();

    public override bool Equals(object? obj)
        => obj is ImageData ico && ico.FileUrl == FileUrl;

    public int CompareTo(ImageData? other)
        => string.Compare(FileUrl, other.FileUrl, StringComparison.InvariantCulture);
}