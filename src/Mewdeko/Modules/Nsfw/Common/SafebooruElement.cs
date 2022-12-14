namespace Mewdeko.Modules.Nsfw.Common;

public class SafebooruElement : IImageData
{
    public string Directory { get; set; }
    public string Image { get; set; }

    public string FileUrl => $"https://safebooru.org/images/{Directory}/{Image}";
    public string Rating { get; set; }
    public string Tags { get; set; }

    public ImageData ToCachedImageData(Booru type)
        => new(FileUrl, Booru.Safebooru, Tags.Split(' '), Rating);
}