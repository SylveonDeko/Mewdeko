using System.Text.Json.Serialization;

namespace Mewdeko.Modules.Nsfw.Common;

public class DerpiContainer
{
    public DerpiImageObject[] Images { get; set; }
}

public class DerpiImageObject : IImageData
{
    [JsonPropertyName("view_url")]
    public string ViewUrl { get; set; }

    public string[] Tags { get; set; }
    public int Score { get; set; }

    public ImageData ToCachedImageData(Booru type)
        => new(ViewUrl, type, Tags, Score.ToString("F1"));
}