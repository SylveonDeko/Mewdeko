namespace Mewdeko.Modules.Nsfw.Common;

public class RealBooruElement : IImageData
{
    public string Image { get; init; }
    public string Directory { get; init; }
    public string Tags { get; init; }
    public string Score { get; init; }

    public ImageData ToCachedImageData(Booru type) =>
        new($"https://realbooru.com//images/{Directory}/{Image}", Booru.Realbooru, Tags.Split(' '), Score);
}