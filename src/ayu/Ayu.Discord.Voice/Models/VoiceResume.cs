using Newtonsoft.Json;

namespace Ayu.Discord.Voice.Models
{
    public sealed class VoiceResume
    {
        [JsonProperty("server_id")]
        public string ServerId { get; set; }

        [JsonProperty("session_id")]
        public string SessionId { get; set; }

        [JsonProperty("token")]
        public string Token { get; set; }
    }
}