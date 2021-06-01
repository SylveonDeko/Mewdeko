using Newtonsoft.Json;

namespace Ayu.Discord.Voice.Models
{
    public sealed class VoiceHello
    {
        [JsonProperty("heartbeat_interval")]
        public int HeartbeatInterval { get; set; }
    }
}