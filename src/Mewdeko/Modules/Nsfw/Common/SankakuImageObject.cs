using System.Text.Json.Serialization;

namespace Mewdeko.Modules.Nsfw.Common;

public class SankakuImageObject : IImageData
{
    public class Tag
    {
        public string Name { get; set; }
    }

    [JsonPropertyName("file_url")]
    public string FileUrl { get; set; }

    [JsonPropertyName("file_type")]
    public string FileType { get; set; }

    public Tag[] Tags { get; set; }

    [JsonPropertyName("total_score")]
    public int Score { get; set; }

    public ImageData ToCachedImageData(Booru type)
        => new(FileUrl, Booru.Sankaku, Tags.Select(x => x.Name).ToArray(), Score.ToString());
}