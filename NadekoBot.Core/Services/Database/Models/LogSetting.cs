using System.Collections.Generic;

namespace NadekoBot.Core.Services.Database.Models
{

    public class LogSetting : DbEntity
    {
        public HashSet<IgnoredLogChannel> IgnoredChannels { get; set; } = new HashSet<IgnoredLogChannel>();
        public HashSet<IgnoredVoicePresenceChannel> IgnoredVoicePresenceChannelIds { get; set; } = new HashSet<IgnoredVoicePresenceChannel>();

        public ulong? LogOtherId { get; set; } = null;
        public ulong? MessageUpdatedId { get; set; } = null;
        public ulong? MessageDeletedId { get; set; } = null;

        public ulong? UserJoinedId { get; set; } = null;
        public ulong? UserLeftId { get; set; } = null;
        public ulong? UserBannedId { get; set; } = null;
        public ulong? UserUnbannedId { get; set; } = null;
        public ulong? UserUpdatedId { get; set; } = null;

        public ulong? ChannelCreatedId { get; set; } = null;
        public ulong? ChannelDestroyedId { get; set; } = null;
        public ulong? ChannelUpdatedId { get; set; } = null;

        public ulong? UserMutedId { get; set; }

        //userpresence
        public ulong? LogUserPresenceId { get; set; } = null;

        //voicepresence

        public ulong? LogVoicePresenceId { get; set; } = null;
        public ulong? LogVoicePresenceTTSId { get; set; } = null;



        //-------------------DO NOT USE----------------
        // these old fields are here because sqlite doesn't support drop column operation
        // will be removed after bot moves to another database provider
        /// <summary>
        /// DON'T USE
        /// </summary>
        public bool IsLogging { get; set; }
        /// <summary>
        /// DON'T USE
        /// </summary>
        public ulong ChannelId { get; set; }
        /// <summary>
        /// DON'T USE
        /// </summary>
        public bool MessageUpdated { get; set; } = true;
        /// <summary>
        /// DON'T USE
        /// </summary>
        public bool MessageDeleted { get; set; } = true;
        /// <summary>
        /// DON'T USE
        /// </summary>
        public bool UserJoined { get; set; } = true;
        /// <summary>
        /// DON'T USE
        /// </summary>
        public bool UserLeft { get; set; } = true;
        /// <summary>
        /// DON'T USE
        /// </summary>
        public bool UserBanned { get; set; } = true;
        /// <summary>
        /// DON'T USE
        /// </summary>
        public bool UserUnbanned { get; set; } = true;
        /// <summary>
        /// DON'T USE
        /// </summary>
        public bool UserUpdated { get; set; } = true;
        /// <summary>
        /// DON'T USE
        /// </summary>
        public bool ChannelCreated { get; set; } = true;
        /// <summary>
        /// DON'T USE
        /// </summary>
        public bool ChannelDestroyed { get; set; } = true;
        /// <summary>
        /// DON'T USE
        /// </summary>
        public bool ChannelUpdated { get; set; } = true;
        /// <summary>
        /// DON'T USE
        /// </summary>
        public bool LogUserPresence { get; set; } = false;
        /// <summary>
        /// DON'T USE
        /// </summary>
        public ulong UserPresenceChannelId { get; set; }
        /// <summary>
        /// DON'T USE
        /// </summary>
        public bool LogVoicePresence { get; set; } = false;
        /// <summary>
        /// DON'T USE
        /// </summary>
        public ulong VoicePresenceChannelId { get; set; }
    }
}
