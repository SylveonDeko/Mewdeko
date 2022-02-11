using System;
using System.Collections.Generic;
using System.Linq;

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
            this.FileUrl = "https://danbooru.donmai.us" + url;
        }
        else
        {
            this.FileUrl = url.StartsWith("http", StringComparison.InvariantCulture) ? url : "https:" + url;
        }
            
        this.SearchType = type;
        this.FileUrl = url;
        this.Tags = tags.ToHashSet();
        this.Rating = rating;
    }

    public override string ToString()
    {
        return FileUrl;
    }

    public override int GetHashCode() => FileUrl.GetHashCode();
    public override bool Equals(object obj)
        => obj is ImageData ico && ico.FileUrl == this.FileUrl;

    public int CompareTo(ImageData other)
        => string.Compare(FileUrl, other.FileUrl, StringComparison.InvariantCulture);
}