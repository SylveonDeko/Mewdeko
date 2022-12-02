namespace Mewdeko.Modules.Nsfw.Common;

public class E621Object : IImageData
{
    public class FileData
    {
        public string Url { get; set; }
    }

    public class TagData
    {
        public string[] General { get; set; }
    }

    public class ScoreData
    {
        public int Total { get; set; }
    }

    public FileData? File { get; set; }
    public TagData? Tags { get; set; }
    public ScoreData? Score { get; set; }

    public ImageData ToCachedImageData(Booru type)
        => new(File.Url, Booru.E621, Tags.General, Score.Total.ToString());
}