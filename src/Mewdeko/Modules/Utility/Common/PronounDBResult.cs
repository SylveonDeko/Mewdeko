using Newtonsoft.Json;

namespace Mewdeko.Modules.Utility.Common;

public class PronounDbResult
{
    [JsonProperty("pronouns")]
    public string Pronouns { get; set; }
}