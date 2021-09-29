using Newtonsoft.Json;

namespace Mewdeko.Voice.Models
{
    public sealed class VoiceHello
    {
        [JsonProperty("heartbeat_interval")] public int HeartbeatInterval { get; set; }
    }
}