using System.Text.Json.Serialization;

namespace Mewdeko.Modules.Nsfw.Common;

public class Rule34Object : IImageData
{
    public string Image { get; init; }
    public int Directory { get; init; }
    public string Tags { get; init; }
    public int Score { get; init; }

    [JsonPropertyName("file_url")]
    public string FileUrl { get; init; }


    public ImageData ToCachedImageData(Booru type)
        => new(FileUrl, Booru.Rule34, Tags.Split(' '), Score.ToString());
}