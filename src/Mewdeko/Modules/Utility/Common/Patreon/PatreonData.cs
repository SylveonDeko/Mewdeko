using Newtonsoft.Json.Linq;

namespace Mewdeko.Modules.Utility.Common.Patreon;

public class PatreonData
{
    public JObject[] Included { get; set; }
    public JObject[] Data { get; set; }
    public PatreonDataLinks Links { get; set; }
}

public class PatreonDataLinks
{
    public string First { get; set; }
    public string Next { get; set; }
}

public class PatreonUserAndReward
{
    public PatreonUser User { get; set; }
    public PatreonPledge Reward { get; set; }
}