namespace Mewdeko.Modules.Searches.Common;

public class ImageCacherObject : IComparable<ImageCacherObject>
{
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
        Tags = new HashSet<string>((obj?.Tags ?? obj.TagString).Split(' '));
    }

    public ImageCacherObject(string url, DapiSearchType type, string tags, string rating)
    {
        SearchType = type;
        FileUrl = url;
        Tags = new HashSet<string>(tags.Split(' '));
        Rating = rating;
    }

    public DapiSearchType SearchType { get; }
    public string FileUrl { get; }
    public HashSet<string> Tags { get; }
    public string Rating { get; }

    public int CompareTo(ImageCacherObject? other) => string.Compare(FileUrl, other?.FileUrl, StringComparison.InvariantCulture);

    public override string ToString() => FileUrl;
}