using System.Collections.Generic;

namespace NadekoBot.Core.Services.Database.Models
{
    public class StreamRoleSettings : DbEntity
    {
        public int GuildConfigId { get; set; }
        public GuildConfig GuildConfig { get; set; }

        /// <summary>
        /// Whether the feature is enabled in the guild.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Id of the role to give to the users in the role 'FromRole' when they start streaming
        /// </summary>
        public ulong AddRoleId { get; set; }

        /// <summary>
        /// Id of the role whose users are eligible to get the 'AddRole'
        /// </summary>
        public ulong FromRoleId { get; set; }

        /// <summary>
        /// If set, feature will only apply to users who have this keyword in their streaming status.
        /// </summary>
        public string Keyword { get; set; }

        /// <summary>
        /// A collection of whitelisted users' IDs. Whitelisted users don't require 'keyword' in
        /// order to get the stream role.
        /// </summary>
        public HashSet<StreamRoleWhitelistedUser> Whitelist { get; set; } = new HashSet<StreamRoleWhitelistedUser>();

        /// <summary>
        /// A collection of blacklisted users' IDs. Blacklisted useres will never get the stream role.
        /// </summary>
        public HashSet<StreamRoleBlacklistedUser> Blacklist { get; set; } = new HashSet<StreamRoleBlacklistedUser>();
    }

    public class StreamRoleBlacklistedUser : DbEntity
    {
        public ulong UserId { get; set; }
        public string Username { get; set; }

        public override bool Equals(object obj)
        {
            if (!(obj is StreamRoleBlacklistedUser x))
                return false;

            return x.UserId == UserId;
        }

        public override int GetHashCode()
        {
            return UserId.GetHashCode();
        }
    }

    public class StreamRoleWhitelistedUser : DbEntity
    {
        public ulong UserId { get; set; }
        public string Username { get; set; }

        public override bool Equals(object obj)
        {
            return obj is StreamRoleWhitelistedUser x
                ? x.UserId == UserId
                : false;
        }

        public override int GetHashCode()
        {
            return UserId.GetHashCode();
        }
    }
}
