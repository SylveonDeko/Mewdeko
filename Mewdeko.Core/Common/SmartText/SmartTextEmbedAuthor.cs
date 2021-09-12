using Newtonsoft.Json;

namespace Mewdeko
{
    public class SmartTextEmbedAuthor
    {
        public string Name { get; set; }
        public string IconUrl { get; set; }
        [JsonProperty("icon_url")]
        private string Icon_Url { set => IconUrl = value; }
        public string Url { get; set; }
    }
}