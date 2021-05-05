namespace NadekoBot.Modules.Utility.Common.Patreon
{
    public class DiscordConnection
    {
        public string user_id { get; set; }
    }

    public class SocialConnections
    {
        public object deviantart { get; set; }
        public DiscordConnection discord { get; set; }
        public object facebook { get; set; }
        public object spotify { get; set; }
        public object twitch { get; set; }
        public object twitter { get; set; }
        public object youtube { get; set; }
    }

    public class UserAttributes
    {
        public string about { get; set; }
        public string created { get; set; }
        public object discord_id { get; set; }
        public string email { get; set; }
        public object facebook { get; set; }
        public object facebook_id { get; set; }
        public string first_name { get; set; }
        public string full_name { get; set; }
        public int gender { get; set; }
        public bool has_password { get; set; }
        public string image_url { get; set; }
        public bool is_deleted { get; set; }
        public bool is_nuked { get; set; }
        public bool is_suspended { get; set; }
        public string last_name { get; set; }
        public SocialConnections social_connections { get; set; }
        public int status { get; set; }
        public string thumb_url { get; set; }
        public object twitch { get; set; }
        public string twitter { get; set; }
        public string url { get; set; }
        public string vanity { get; set; }
        public object youtube { get; set; }
    }

    public class Campaign
    {
        public Data data { get; set; }
        public Links links { get; set; }
    }

    public class UserRelationships
    {
        public Campaign campaign { get; set; }
    }

    public class PatreonUser
    {
        public UserAttributes attributes { get; set; }
        public string id { get; set; }
        public UserRelationships relationships { get; set; }
        public string type { get; set; }
    }
}
