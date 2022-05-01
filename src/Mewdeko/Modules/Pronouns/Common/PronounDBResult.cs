using Newtonsoft.Json;

namespace Mewdeko.Modules.Pronouns.Common;

public class PronounDbResult
{
    [JsonProperty("pronouns")]
    public string Pronouns { get; set; }
}