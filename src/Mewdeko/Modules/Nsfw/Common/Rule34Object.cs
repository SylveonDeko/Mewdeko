namespace Mewdeko.Modules.Nsfw.Common;

public class Rule34Object : IImageData
{
    public string Image { get; init; }
    public int Directory { get; init; }
    public string Tags { get; init; }
    public int Score { get; init; }

    public ImageData ToCachedImageData(Booru type) =>
        new(
            $"https://img.rule34.xxx//images/{Directory}/{Image}",
            Booru.Rule34,
            Tags.Split(' '),
            Score.ToString());
}