namespace NadekoBot.Modules.Searches.Common
{
    public class E621Object
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
            public string Total { get; set; }
        }

        public FileData File { get; set; }
        public TagData Tags { get; set; }
        public ScoreData Score { get; set; }
    }
}
