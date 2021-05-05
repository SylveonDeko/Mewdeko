namespace NadekoBot.Modules.Utility.Common.Patreon
{
    public class Attributes
    {
        public int amount_cents { get; set; }
        public string created_at { get; set; }
        public object declined_since { get; set; }
        public bool is_twitch_pledge { get; set; }
        public bool patron_pays_fees { get; set; }
        public int? pledge_cap_cents { get; set; }
    }

    public class Address
    {
        public object data { get; set; }
    }

    public class Data
    {
        public string id { get; set; }
        public string type { get; set; }
    }

    public class Links
    {
        public string related { get; set; }
    }

    public class Creator
    {
        public Data data { get; set; }
        public Links links { get; set; }
    }

    public class Patron
    {
        public Data data { get; set; }
        public Links links { get; set; }
    }

    public class Reward
    {
        public Data data { get; set; }
        public Links links { get; set; }
    }

    public class Relationships
    {
        public Address address { get; set; }
        public Creator creator { get; set; }
        public Patron patron { get; set; }
        public Reward reward { get; set; }
    }

    public class PatreonPledge
    {
        public Attributes attributes { get; set; }
        public string id { get; set; }
        public Relationships relationships { get; set; }
        public string type { get; set; }
    }
}
