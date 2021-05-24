using System;

namespace Mewdeko.Core.Modules.Searches.Common
{
    public class YtVideoData
    {
        public string VideoId { get; set; }
        public string ChannelName { get; set; }
        public DateTime PublishedAt { get; set; }
        public string Thumbnail { get; set; }

        public string GetVideoUrl()
        {
            return "https://www.youtube.com/watch?v=" + VideoId;
        }
    }
}