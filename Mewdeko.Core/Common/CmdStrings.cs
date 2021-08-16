using Newtonsoft.Json;

namespace Mewdeko.Core.Common
{
    public class CmdStrings
    {
        [JsonConstructor]
        public CmdStrings(
            [JsonProperty("args")] string[] usages,
            [JsonProperty("desc")] string description
        )
        {
            Usages = usages;
            Description = description;
        }

        public string[] Usages { get; }
        public string Description { get; }
    }
}