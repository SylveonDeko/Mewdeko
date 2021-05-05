using Newtonsoft.Json.Linq;

namespace NadekoBot.Modules.Utility.Common.Patreon
{
    public class PatreonData
    {
        public JObject[] Included { get; set; }
        public JObject[] Data { get; set; }
        public PatreonDataLinks Links { get; set; }
    }

    public class PatreonDataLinks
    {
        public string first { get; set; }
        public string next { get; set; }
    }

    public class PatreonUserAndReward
    {
        public PatreonUser User { get; set; }
        public PatreonPledge Reward { get; set; }
    }
}
