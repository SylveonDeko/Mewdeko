using System.Collections.Generic;
using Mewdeko.Common.Collections;

namespace Mewdeko.Core.Services.Database.Models
{
    public class GuildConfig : DbEntity
    {
        public ulong GuildId { get; set; }

        public string Prefix { get; set; } = null;
        public ulong StaffRole { get; set; }
        public bool DeleteMessageOnCommand { get; set; }
        public HashSet<DelMsgOnCmdChannel> DelMsgOnCmdChannels { get; set; } = new();
        public string AutoAssignRoleId { get; set; }
        public string AutoBotRoleIds { get; set; }
        public ulong MemberRole { get; set; } = 0;
        public ulong WarnlogChannelId { get; set; } = 0;
        public ulong MiniWarnlogChannelId { get; set; } = 0;
        public ulong TicketCategory { get; set; } = 0;
        public ulong snipeset { get; set; }
        public string SuggestEmotes { get; set; }
        public int AfkLength { get; set; } = 250;
        public string SuggestMessage { get; set; } = "";
        public string AcceptSuggestDmMessage { get; set; } = "";
        public string DenySuggestDmMessage { get; set; } = "";
        public string ImplementSuggestDmMessage { get; set; } = "";
        public string ConsiderSuggestDmMessage { get; set; } = "";
        public string DenyMessage { get; set; } = "";
        public string AcceptMessage { get; set; } = "";
        public string ImplementMessage { get; set; } = "";
        public string ConsiderMessage { get; set; } = "";
        public int AfkMessageType { get; set; }
        public int XpTxtTimeout { get; set; }
        public int XpTxtRate { get; set; }
        public int XpVoiceRate { get; set; }
        public int XpVoiceTimeout { get; set; }
        public ulong SuggestRole { get; set; }
        public List<WarningPunishment2> WarnPunishments2 { get; set; } = new();
        public ulong Stars { get; set; } = 3;
        public int AfkType { get; set; } = 2;
        public AntiAltSetting AntiAltSetting { get; set; }
        public string AfkDisabledChannels { get; set; }

        public int AfkTimeout { get; set; } = 5;
        //public int TLogType { get; set; }
        //public ulong TLogChan { get; set; }
        //public ulong TPingRole { get; set; }
        //public string TText { get; set; }

        //public string TEmote { get; set; }
        //public ulong TCreatChan { get; set; }
        //public ulong TCreatMsgId { get; set; }
        public ulong Joins { get; set; }
        public ulong Leaves { get; set; }
        public ulong Star { get; set; }
        public ulong StarboardChannel { get; set; }
        public int PreviewLinks { get; set; }
        public ulong ReactChannel { get; set; }
        public ulong sugnum { get; set; }
        public ulong sugchan { get; set; }
        public int fwarn { get; set; }
        public int invwarn { get; set; }

        public int removeroles { get; set; }

        //greet stuff
        public bool AutoDeleteGreetMessages { get; set; } //unused
        public bool AutoDeleteByeMessages { get; set; } // unused
        public int AutoDeleteGreetMessagesTimer { get; set; } = 30;
        public int AutoDeleteByeMessagesTimer { get; set; } = 30;

        public ulong GreetMessageChannelId { get; set; }
        public ulong ByeMessageChannelId { get; set; }
        public int WebhookGreet { get; set; } = 0;
        public string WebhookURL { get; set; } = "";

        public bool SendDmGreetMessage { get; set; }
        public string DmGreetMessageText { get; set; } = "Welcome to the %server% server, %user%!";

        public bool SendChannelGreetMessage { get; set; }
        public string ChannelGreetMessageText { get; set; } = "Welcome to the %server% server, %user%!";

        public bool SendChannelByeMessage { get; set; }
        public string ChannelByeMessageText { get; set; } = "%user% has left!";

        public LogSetting LogSetting { get; set; } = new();

        //self assignable roles
        public bool ExclusiveSelfAssignedRoles { get; set; }
        public bool AutoDeleteSelfAssignedRoleMessages { get; set; }
        public float DefaultMusicVolume { get; set; } = 1.0f;
        public bool VoicePlusTextEnabled { get; set; }

        //stream notifications
        public HashSet<FollowedStream> FollowedStreams { get; set; } = new();

        //currencyGeneration
        public HashSet<GCChannelId> GenerateCurrencyChannelIds { get; set; } = new();

        //permissions
        public Permission RootPermission { get; set; } = null;
        public List<Permissionv2> Permissions { get; set; }
        public bool VerbosePermissions { get; set; } = true;
        public string PermissionRole { get; set; } = null;

        public HashSet<CommandCooldown> CommandCooldowns { get; set; } = new();

        //filtering
        public bool FilterInvites { get; set; }
        public bool FilterLinks { get; set; }
        public HashSet<FilterChannelId> FilterInvitesChannelIds { get; set; } = new();
        public HashSet<FilterLinksChannelId> FilterLinksChannelIds { get; set; } = new();

        //public bool FilterLinks { get; set; }
        //public HashSet<FilterLinksChannelId> FilterLinksChannels { get; set; } = new HashSet<FilterLinksChannelId>();

        public bool FilterWords { get; set; }
        public HashSet<FilteredWord> FilteredWords { get; set; } = new();
        public HashSet<FilterChannelId> FilterWordsChannelIds { get; set; } = new();

        public HashSet<MutedUserId> MutedUsers { get; set; } = new();

        public string MuteRoleName { get; set; }
        public bool CleverbotEnabled { get; set; }
        public List<Repeater> GuildRepeaters { get; set; } = new();

        public AntiRaidSetting AntiRaidSetting { get; set; }
        public AntiSpamSetting AntiSpamSetting { get; set; }

        public string Locale { get; set; } = null;
        public string TimeZoneId { get; set; } = null;

        public HashSet<UnmuteTimer> UnmuteTimers { get; set; } = new();
        public HashSet<UnbanTimer> UnbanTimer { get; set; } = new();
        public HashSet<UnroleTimer> UnroleTimer { get; set; } = new();
        public HashSet<VcRoleInfo> VcRoleInfos { get; set; }
        public HashSet<CommandAlias> CommandAliases { get; set; } = new();
        public List<WarningPunishment> WarnPunishments { get; set; } = new();
        public bool WarningsInitialized { get; set; }
        public HashSet<SlowmodeIgnoredUser> SlowmodeIgnoredUsers { get; set; }
        public HashSet<SlowmodeIgnoredRole> SlowmodeIgnoredRoles { get; set; }
        public HashSet<NsfwBlacklitedTag> NsfwBlacklistedTags { get; set; } = new();

        public List<ShopEntry> ShopEntries { get; set; }
        public ulong? GameVoiceChannel { get; set; } = null;
        public bool VerboseErrors { get; set; } = false;

        public StreamRoleSettings StreamRole { get; set; }

        public XpSettings XpSettings { get; set; }
        public List<FeedSub> FeedSubs { get; set; } = new();
        public bool AutoDcFromVc { get; set; }
        public IndexedCollection<ReactionRoleMessage> ReactionRoleMessages { get; set; } = new();
        public bool NotifyStreamOffline { get; set; }
        public List<GroupName> SelfAssignableRoleGroupNames { get; set; }
        public int WarnExpireHours { get; set; } = 0;
        public WarnExpireAction WarnExpireAction { get; set; } = WarnExpireAction.Clear;
    }
}