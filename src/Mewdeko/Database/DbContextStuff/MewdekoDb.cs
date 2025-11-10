using DataModel;
using LinqToDB;
using LinqToDB.Data;
using Mewdeko.Database.L2DB;
using Embed = DataModel.Embed;
using Poll = DataModel.Poll;
using SelectMenuOption = DataModel.SelectMenuOption;

namespace Mewdeko.Database.DbContextStuff;

/// <summary>
///     Main data access class for Mewdeko using LinqToDB.
///     Provides access to all database tables.
/// </summary>
public class MewdekoDb : DataConnection
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="MewdekoDb" /> class using pre-configured options.
    /// </summary>
    /// <param name="options">The configured LinqToDB data options.</param>
    public MewdekoDb(DataOptions options)
        : base(options)
    {
        // Set connection to close after each operation for better connection pooling
        (this as IDataContext).CloseAfterUse = true;
    }

    /// <summary>
    ///     Table for accessing scheduled ticket deletions
    /// </summary>
    public ITable<ScheduledTicketDeletion> ScheduledTicketDeletions => this.GetTable<ScheduledTicketDeletion>();

    /// <summary>
    ///     Gets the global user balances table.
    /// </summary>
    public ITable<GlobalUserBalance> GlobalUserBalances => this.GetTable<GlobalUserBalance>();

    /// <summary>
    ///     Gets the invite counts table.
    /// </summary>
    public ITable<InviteCount> InviteCounts => this.GetTable<InviteCount>();

    /// <summary>
    ///     Gets the tickets table.
    /// </summary>
    public ITable<Ticket> Tickets => this.GetTable<Ticket>();

    /// <summary>
    ///     Gets the custom voice channels table.
    /// </summary>
    public ITable<CustomVoiceChannel> CustomVoiceChannels => this.GetTable<CustomVoiceChannel>();

    /// <summary>
    ///     Gets the custom voice configs table.
    /// </summary>
    public ITable<CustomVoiceConfig> CustomVoiceConfigs => this.GetTable<CustomVoiceConfig>();

    /// <summary>
    ///     Gets the user voice preferences table.
    /// </summary>
    public ITable<UserVoicePreference> UserVoicePreferences => this.GetTable<UserVoicePreference>();

    /// <summary>
    ///     Gets the case notes table.
    /// </summary>
    public ITable<CaseNote> CaseNotes => this.GetTable<CaseNote>();

    /// <summary>
    ///     Gets the chat logs table.
    /// </summary>
    public ITable<ChatLog> ChatLogs => this.GetTable<ChatLog>();

    /// <summary>
    ///     Gets the ticket cases table.
    /// </summary>
    public ITable<TicketCase> TicketCases => this.GetTable<TicketCase>();

    /// <summary>
    ///     Gets the guild user XP data table.
    /// </summary>
    public ITable<GuildUserXp> GuildUserXps => this.GetTable<GuildUserXp>();

    /// <summary>
    ///     Gets the guild XP settings table.
    /// </summary>
    public ITable<GuildXpSetting> GuildXpSettings => this.GetTable<GuildXpSetting>();

    /// <summary>
    ///     Gets the XP boost events table.
    /// </summary>
    public ITable<XpBoostEvent> XpBoostEvents => this.GetTable<XpBoostEvent>();

    /// <summary>
    ///     Gets the XP channel multipliers table.
    /// </summary>
    public ITable<XpChannelMultiplier> XpChannelMultipliers => this.GetTable<XpChannelMultiplier>();

    /// <summary>
    ///     Gets the XP competition entries table.
    /// </summary>
    public ITable<XpCompetitionEntry> XpCompetitionEntries => this.GetTable<XpCompetitionEntry>();

    /// <summary>
    ///     Gets the XP competition rewards table.
    /// </summary>
    public ITable<XpCompetitionReward> XpCompetitionRewards => this.GetTable<XpCompetitionReward>();

    /// <summary>
    ///     Gets the XP competitions table.
    /// </summary>
    public ITable<XpCompetition> XpCompetitions => this.GetTable<XpCompetition>();

    /// <summary>
    ///     Gets the guild configurations table.
    /// </summary>
    public ITable<GuildConfig> GuildConfigs => this.GetTable<GuildConfig>();

    /// <summary>
    ///     Gets the guild bot profiles table.
    /// </summary>
    public ITable<GuildBotProfile> GuildBotProfiles
    {
        get
        {
            return this.GetTable<GuildBotProfile>();
        }
    }

    /// <summary>
    ///     Gets the AFK table.
    /// </summary>
    public ITable<Afk> Afks => this.GetTable<Afk>();

    /// <summary>
    ///     Gets the AI conversations table.
    /// </summary>
    public ITable<AiConversation> AiConversations => this.GetTable<AiConversation>();

    /// <summary>
    ///     Gets the AI messages table.
    /// </summary>
    public ITable<AiMessage> AiMessages => this.GetTable<AiMessage>();

    /// <summary>
    ///     Gets the anti-alt settings table.
    /// </summary>
    public ITable<AntiAltSetting> AntiAltSettings => this.GetTable<AntiAltSetting>();

    /// <summary>
    ///     Gets the anti-mass mention settings table.
    /// </summary>
    public ITable<AntiMassMentionSetting> AntiMassMentionSettings => this.GetTable<AntiMassMentionSetting>();

    /// <summary>
    ///     Gets the anti-mass-post settings table.
    /// </summary>
    public ITable<AntiMassPostSetting> AntiMassPostSettings
    {
        get
        {
            return this.GetTable<AntiMassPostSetting>();
        }
    }

    /// <summary>
    ///     Gets the anti-mass-post ignored roles table.
    /// </summary>
    public ITable<AntiMassPostIgnoredRole> AntiMassPostIgnoredRoles
    {
        get
        {
            return this.GetTable<AntiMassPostIgnoredRole>();
        }
    }

    /// <summary>
    ///     Gets the anti-mass-post ignored users table.
    /// </summary>
    public ITable<AntiMassPostIgnoredUser> AntiMassPostIgnoredUsers
    {
        get
        {
            return this.GetTable<AntiMassPostIgnoredUser>();
        }
    }

    /// <summary>
    ///     Gets the anti-mass-post ignored channels table.
    /// </summary>
    public ITable<AntiMassPostIgnoredChannel> AntiMassPostIgnoredChannels
    {
        get
        {
            return this.GetTable<AntiMassPostIgnoredChannel>();
        }
    }

    /// <summary>
    ///     Gets the anti-mass-post link whitelist table.
    /// </summary>
    public ITable<AntiMassPostLinkWhitelist> AntiMassPostLinkWhitelists
    {
        get
        {
            return this.GetTable<AntiMassPostLinkWhitelist>();
        }
    }

    /// <summary>
    ///     Gets the anti-mass-post link blacklist table.
    /// </summary>
    public ITable<AntiMassPostLinkBlacklist> AntiMassPostLinkBlacklists
    {
        get
        {
            return this.GetTable<AntiMassPostLinkBlacklist>();
        }
    }

    /// <summary>
    ///     Gets the anti-post-channel settings table.
    /// </summary>
    public ITable<AntiPostChannelSetting> AntiPostChannelSettings
    {
        get
        {
            return this.GetTable<AntiPostChannelSetting>();
        }
    }

    /// <summary>
    ///     Gets the anti-post-channel channels table.
    /// </summary>
    public ITable<AntiPostChannelChannel> AntiPostChannelChannels
    {
        get
        {
            return this.GetTable<AntiPostChannelChannel>();
        }
    }

    /// <summary>
    ///     Gets the anti-post-channel ignored roles table.
    /// </summary>
    public ITable<AntiPostChannelIgnoredRole> AntiPostChannelIgnoredRoles
    {
        get
        {
            return this.GetTable<AntiPostChannelIgnoredRole>();
        }
    }

    /// <summary>
    ///     Gets the anti-post-channel ignored users table.
    /// </summary>
    public ITable<AntiPostChannelIgnoredUser> AntiPostChannelIgnoredUsers
    {
        get
        {
            return this.GetTable<AntiPostChannelIgnoredUser>();
        }
    }

    /// <summary>
    ///     Gets the anti-raid settings table.
    /// </summary>
    public ITable<AntiRaidSetting> AntiRaidSettings => this.GetTable<AntiRaidSetting>();

    /// <summary>
    ///     Gets the anti-spam ignores table.
    /// </summary>
    public ITable<AntiSpamIgnore> AntiSpamIgnores => this.GetTable<AntiSpamIgnore>();

    /// <summary>
    ///     Gets the anti-spam settings table.
    /// </summary>
    public ITable<AntiSpamSetting> AntiSpamSettings => this.GetTable<AntiSpamSetting>();

    /// <summary>
    ///     Gets the auth codes table.
    /// </summary>
    public ITable<AuthCode> AuthCodes => this.GetTable<AuthCode>();

    /// <summary>
    ///     Gets the auto-ban roles table.
    /// </summary>
    public ITable<AutoBanRole> AutoBanRoles => this.GetTable<AutoBanRole>();

    /// <summary>
    ///     Gets the auto-ban words table.
    /// </summary>
    public ITable<AutoBanWord> AutoBanWords => this.GetTable<AutoBanWord>();

    /// <summary>
    ///     Gets the auto commands table.
    /// </summary>
    public ITable<AutoCommand> AutoCommands => this.GetTable<AutoCommand>();

    /// <summary>
    ///     Gets the auto publish table.
    /// </summary>
    public ITable<AutoPublish> AutoPublishes => this.GetTable<AutoPublish>();

    /// <summary>
    ///     Gets the ban templates table.
    /// </summary>
    public ITable<BanTemplate> BanTemplates => this.GetTable<BanTemplate>();

    /// <summary>
    ///     Gets the birthday configurations table.
    /// </summary>
    public ITable<BirthdayConfig> BirthdayConfigs => this.GetTable<BirthdayConfig>();

    /// <summary>
    ///     Gets the blacklist table.
    /// </summary>
    public ITable<Blacklist> Blacklists => this.GetTable<Blacklist>();

    /// <summary>
    ///     Gets the blacklisted permissions table.
    /// </summary>
    public ITable<BlacklistedPermission> BlacklistedPermissions => this.GetTable<BlacklistedPermission>();

    /// <summary>
    ///     Gets the blacklisted roles table.
    /// </summary>
    public ITable<BlacklistedRole> BlacklistedRoles => this.GetTable<BlacklistedRole>();

    /// <summary>
    ///     Gets the bot instances table.
    /// </summary>
    public ITable<BotInstance> BotInstances => this.GetTable<BotInstance>();

    /// <summary>
    ///     Gets the bot reviews table.
    /// </summary>
    public ITable<BotReview> BotReviews => this.GetTable<BotReview>();

    /// <summary>
    ///     Gets the chat triggers table.
    /// </summary>
    public ITable<ChatTrigger> ChatTriggers => this.GetTable<ChatTrigger>();

    /// <summary>
    ///     Gets the command aliases table.
    /// </summary>
    public ITable<CommandAlias> CommandAliases => this.GetTable<CommandAlias>();

    /// <summary>
    ///     Gets the command cooldowns table.
    /// </summary>
    public ITable<CommandCooldown> CommandCooldowns => this.GetTable<CommandCooldown>();

    /// <summary>
    ///     Gets the command stats table.
    /// </summary>
    public ITable<CommandStat> CommandStats => this.GetTable<CommandStat>();

    /// <summary>
    ///     Gets the confessions table.
    /// </summary>
    public ITable<Confession> Confessions => this.GetTable<Confession>();

    /// <summary>
    ///     Gets the COPR monitors table.
    /// </summary>
    public ITable<CoprMonitor> CoprMonitors
    {
        get
        {
            return this.GetTable<CoprMonitor>();
        }
    }

    /// <summary>
    ///     Gets the delete message on command channels table.
    /// </summary>
    public ITable<DelMsgOnCmdChannel> DelMsgOnCmdChannels => this.GetTable<DelMsgOnCmdChannel>();

    /// <summary>
    ///     Gets the Discord permission overrides table.
    /// </summary>
    public ITable<DiscordPermOverride> DiscordPermOverrides => this.GetTable<DiscordPermOverride>();

    /// <summary>
    ///     Gets the Discord users table.
    /// </summary>
    public ITable<DiscordUser> DiscordUsers => this.GetTable<DiscordUser>();

    /// <summary>
    ///     Gets the embeds table.
    /// </summary>
    public ITable<Embed> Embeds => this.GetTable<Embed>();

    /// <summary>
    ///     Gets the feed subscriptions table.
    /// </summary>
    public ITable<FeedSub> FeedSubs => this.GetTable<FeedSub>();

    /// <summary>
    ///     Gets the filter invites channel IDs table.
    /// </summary>
    public ITable<FilterInvitesChannelId> FilterInvitesChannelIds => this.GetTable<FilterInvitesChannelId>();

    /// <summary>
    ///     Gets the filter links channel IDs table.
    /// </summary>
    public ITable<FilterLinksChannelId> FilterLinksChannelIds => this.GetTable<FilterLinksChannelId>();

    /// <summary>
    ///     Gets the filter words channel IDs table.
    /// </summary>
    public ITable<FilterWordsChannelId> FilterWordsChannelIds => this.GetTable<FilterWordsChannelId>();

    /// <summary>
    ///     Gets the filtered words table.
    /// </summary>
    public ITable<FilteredWord> FilteredWords => this.GetTable<FilteredWord>();

    /// <summary>
    ///     Gets the followed streams table.
    /// </summary>
    public ITable<FollowedStream> FollowedStreams => this.GetTable<FollowedStream>();

    /// <summary>
    ///     Gets the giveaway users table.
    /// </summary>
    public ITable<GiveawayUser> GiveawayUsers => this.GetTable<GiveawayUser>();

    /// <summary>
    ///     Gets the giveaways table.
    /// </summary>
    public ITable<Giveaway> Giveaways => this.GetTable<Giveaway>();

    /// <summary>
    ///     Gets the group names table.
    /// </summary>
    public ITable<GroupName> GroupNames => this.GetTable<GroupName>();

    /// <summary>
    ///     Gets the guild AI configs table.
    /// </summary>
    public ITable<GuildAiConfig> GuildAiConfigs => this.GetTable<GuildAiConfig>();

    /// <summary>
    ///     Gets the guild repeaters table.
    /// </summary>
    public ITable<GuildRepeater> GuildRepeaters => this.GetTable<GuildRepeater>();

    /// <summary>
    ///     Gets the guild ticket settings table.
    /// </summary>
    public ITable<GuildTicketSetting> GuildTicketSettings => this.GetTable<GuildTicketSetting>();

    /// <summary>
    ///     Gets the guild user balances table.
    /// </summary>
    public ITable<GuildUserBalance> GuildUserBalances => this.GetTable<GuildUserBalance>();

    /// <summary>
    ///     Gets the highlight settings table.
    /// </summary>
    public ITable<HighlightSetting> HighlightSettings => this.GetTable<HighlightSetting>();

    /// <summary>
    ///     Gets the highlights table.
    /// </summary>
    public ITable<Highlight> Highlights => this.GetTable<Highlight>();

    /// <summary>
    ///     Gets the ignored log channels table.
    /// </summary>
    public ITable<IgnoredLogChannel> IgnoredLogChannels => this.GetTable<IgnoredLogChannel>();

    /// <summary>
    ///     Gets the invite count settings table.
    /// </summary>
    public ITable<InviteCountSetting> InviteCountSettings => this.GetTable<InviteCountSetting>();

    /// <summary>
    ///     Gets the invited by table.
    /// </summary>
    public ITable<InvitedBy> InvitedBies => this.GetTable<InvitedBy>();

    /// <summary>
    ///     Gets the join/leave logs table.
    /// </summary>
    public ITable<JoinLeaveLog> JoinLeaveLogs => this.GetTable<JoinLeaveLog>();

    /// <summary>
    ///     Gets the lockdown channel permissions table.
    /// </summary>
    public ITable<LockdownChannelPermission> LockdownChannelPermissions => this.GetTable<LockdownChannelPermission>();

    /// <summary>
    ///     Gets the LoggingV2 table.
    /// </summary>
    public ITable<LoggingV2> LoggingV2 => this.GetTable<LoggingV2>();

    /// <summary>
    ///     Gets the message counts table.
    /// </summary>
    public ITable<MessageCount> MessageCounts => this.GetTable<MessageCount>();

    /// <summary>
    ///     Gets the message timestamps table.
    /// </summary>
    public ITable<MessageTimestamp> MessageTimestamps => this.GetTable<MessageTimestamp>();

    /// <summary>
    ///     Gets the multi-greets table.
    /// </summary>
    public ITable<MultiGreet> MultiGreets => this.GetTable<MultiGreet>();

    /// <summary>
    ///     Gets the music player settings table.
    /// </summary>
    public ITable<MusicPlayerSetting> MusicPlayerSettings => this.GetTable<MusicPlayerSetting>();

    /// <summary>
    ///     Gets the music playlist tracks table.
    /// </summary>
    public ITable<MusicPlaylistTrack> MusicPlaylistTracks => this.GetTable<MusicPlaylistTrack>();

    /// <summary>
    ///     Gets the music playlists table.
    /// </summary>
    public ITable<MusicPlaylist> MusicPlaylists => this.GetTable<MusicPlaylist>();

    /// <summary>
    ///     Gets the muted user IDs table.
    /// </summary>
    public ITable<MutedUserId> MutedUserIds => this.GetTable<MutedUserId>();

    /// <summary>
    ///     Gets the note edits table.
    /// </summary>
    public ITable<NoteEdit> NoteEdits => this.GetTable<NoteEdit>();

    /// <summary>
    ///     Gets the NSFW blacklisted tags table.
    /// </summary>
    public ITable<NsfwBlacklistedTag> NsfwBlacklistedTags => this.GetTable<NsfwBlacklistedTag>();

    /// <summary>
    ///     Gets the owner only settings table.
    /// </summary>
    public ITable<OwnerOnly> OwnerOnlies => this.GetTable<OwnerOnly>();

    /// <summary>
    ///     Gets the panel buttons table.
    /// </summary>
    public ITable<PanelButton> PanelButtons => this.GetTable<PanelButton>();

    /// <summary>
    ///     Gets the panel select menus table.
    /// </summary>
    public ITable<PanelSelectMenu> PanelSelectMenus => this.GetTable<PanelSelectMenu>();

    /// <summary>
    ///     Gets the Permission table.
    /// </summary>
    public ITable<Permission> Permissions => this.GetTable<Permission>();

    /// <summary>
    ///     Gets the Permissions1 table.
    /// </summary>
    public ITable<Permission1> Permissions1 => this.GetTable<Permission1>();

    /// <summary>
    ///     Gets the playlist songs table.
    /// </summary>
    public ITable<PlaylistSong> PlaylistSongs => this.GetTable<PlaylistSong>();

    /// <summary>
    ///     Gets the polls table.
    /// </summary>
    public ITable<Poll> Polls => this.GetTable<Poll>();

    /// <summary>
    ///     Gets the poll votes table.
    /// </summary>
    public ITable<PollVote> PollVotes => this.GetTable<PollVote>();

    /// <summary>
    ///     Gets the poll options table.
    /// </summary>
    public ITable<PollOption> PollOptions => this.GetTable<PollOption>();

    /// <summary>
    ///     Gets the scheduled polls table.
    /// </summary>
    public ITable<ScheduledPoll> ScheduledPolls => this.GetTable<ScheduledPoll>();

    /// <summary>
    ///     Gets the publish user blacklist table.
    /// </summary>
    public ITable<PublishUserBlacklist> PublishUserBlacklists => this.GetTable<PublishUserBlacklist>();

    /// <summary>
    ///     Gets the publish word blacklist table.
    /// </summary>
    public ITable<PublishWordBlacklist> PublishWordBlacklists => this.GetTable<PublishWordBlacklist>();

    /// <summary>
    ///     Gets the quotes table.
    /// </summary>
    public ITable<Quote> Quotes => this.GetTable<Quote>();

    /// <summary>
    ///     Gets the reaction roles table.
    /// </summary>
    public ITable<ReactionRole> ReactionRoles => this.GetTable<ReactionRole>();

    /// <summary>
    ///     Gets the reaction role messages table.
    /// </summary>
    public ITable<ReactionRoleMessage> ReactionRoleMessages => this.GetTable<ReactionRoleMessage>();

    /// <summary>
    ///     Gets the reminders table.
    /// </summary>
    public ITable<Reminder> Reminders => this.GetTable<Reminder>();

    /// <summary>
    ///     Gets the role greets table.
    /// </summary>
    public ITable<RoleGreet> RoleGreets => this.GetTable<RoleGreet>();

    /// <summary>
    ///     Gets the role monitoring settings table.
    /// </summary>
    public ITable<RoleMonitoringSetting> RoleMonitoringSettings => this.GetTable<RoleMonitoringSetting>();

    /// <summary>
    ///     Gets the role state settings table.
    /// </summary>
    public ITable<RoleStateSetting> RoleStateSettings => this.GetTable<RoleStateSetting>();

    /// <summary>
    ///     Gets the rotating statuses table.
    /// </summary>
    public ITable<RotatingStatus> RotatingStatuses => this.GetTable<RotatingStatus>();

    /// <summary>
    ///     Gets the select menu options table.
    /// </summary>
    public ITable<SelectMenuOption> SelectMenuOptions => this.GetTable<SelectMenuOption>();

    /// <summary>
    ///     Gets the self-assignable roles table.
    /// </summary>
    public ITable<SelfAssignableRole> SelfAssignableRoles => this.GetTable<SelfAssignableRole>();

    /// <summary>
    ///     Gets the server recovery stores table.
    /// </summary>
    public ITable<ServerRecoveryStore> ServerRecoveryStores => this.GetTable<ServerRecoveryStore>();

    /// <summary>
    ///     Gets the starboard posts table.
    /// </summary>
    public ITable<StarboardPost> StarboardPosts => this.GetTable<StarboardPost>();

    /// <summary>
    ///     Gets the starboards table.
    /// </summary>
    public ITable<Starboard> Starboards => this.GetTable<Starboard>();

    /// <summary>
    ///     Gets the starboard statistics table.
    /// </summary>
    public ITable<StarboardStats> StarboardStats
    {
        get
        {
            return this.GetTable<StarboardStats>();
        }
    }

    /// <summary>
    ///     Gets the starboard reactions table.
    /// </summary>
    public ITable<StarboardReaction> StarboardReactions
    {
        get
        {
            return this.GetTable<StarboardReaction>();
        }
    }

    /// <summary>
    ///     Gets the status roles table.
    /// </summary>
    public ITable<StatusRole> StatusRoles => this.GetTable<StatusRole>();

    /// <summary>
    ///     Gets the stream role blacklisted users table.
    /// </summary>
    public ITable<StreamRoleBlacklistedUser> StreamRoleBlacklistedUsers => this.GetTable<StreamRoleBlacklistedUser>();

    /// <summary>
    ///     Gets the stream role settings table.
    /// </summary>
    public ITable<StreamRoleSetting> StreamRoleSettings => this.GetTable<StreamRoleSetting>();

    /// <summary>
    ///     Gets the stream role whitelisted users table.
    /// </summary>
    public ITable<StreamRoleWhitelistedUser> StreamRoleWhitelistedUsers => this.GetTable<StreamRoleWhitelistedUser>();

    /// <summary>
    ///     Gets the suggest threads table.
    /// </summary>
    public ITable<SuggestThread> SuggestThreads => this.GetTable<SuggestThread>();

    /// <summary>
    ///     Gets the suggest votes table.
    /// </summary>
    public ITable<SuggestVote> SuggestVotes => this.GetTable<SuggestVote>();

    /// <summary>
    ///     Gets the suggestions table.
    /// </summary>
    public ITable<Suggestion> Suggestions => this.GetTable<Suggestion>();

    /// <summary>
    ///     Gets the template table.
    /// </summary>
    public ITable<Template> Templates => this.GetTable<Template>();

    /// <summary>
    ///     Gets the template bars table.
    /// </summary>
    public ITable<TemplateBar> TemplateBars => this.GetTable<TemplateBar>();

    /// <summary>
    ///     Gets the template clubs table.
    /// </summary>
    public ITable<TemplateClub> TemplateClubs => this.GetTable<TemplateClub>();

    /// <summary>
    ///     Gets the template guilds table.
    /// </summary>
    public ITable<TemplateGuild> TemplateGuilds => this.GetTable<TemplateGuild>();

    /// <summary>
    ///     Gets the template users table.
    /// </summary>
    public ITable<TemplateUser> TemplateUsers => this.GetTable<TemplateUser>();

    /// <summary>
    ///     Gets the ticket notes table.
    /// </summary>
    public ITable<TicketNote> TicketNotes => this.GetTable<TicketNote>();

    /// <summary>
    ///     Gets the ticket panels table.
    /// </summary>
    public ITable<TicketPanel> TicketPanels => this.GetTable<TicketPanel>();

    /// <summary>
    ///     Gets the ticket priorities table.
    /// </summary>
    public ITable<TicketPriority> TicketPriorities => this.GetTable<TicketPriority>();

    /// <summary>
    ///     Gets the ticket tags table.
    /// </summary>
    public ITable<TicketTag> TicketTags => this.GetTable<TicketTag>();

    /// <summary>
    ///     Gets the transaction history table.
    /// </summary>
    public ITable<TransactionHistory> TransactionHistories => this.GetTable<TransactionHistory>();

    /// <summary>
    ///     Gets the daily challenges table.
    /// </summary>
    public ITable<DailyChallenge> DailyChallenges => this.GetTable<DailyChallenge>();

    /// <summary>
    ///     Gets the unban timers table.
    /// </summary>
    public ITable<UnbanTimer> UnbanTimers => this.GetTable<UnbanTimer>();

    /// <summary>
    ///     Gets the unmute timers table.
    /// </summary>
    public ITable<UnmuteTimer> UnmuteTimers => this.GetTable<UnmuteTimer>();

    /// <summary>
    ///     Gets the unrole timers table.
    /// </summary>
    public ITable<UnroleTimer> UnroleTimers => this.GetTable<UnroleTimer>();

    /// <summary>
    ///     Gets the user role states table.
    /// </summary>
    public ITable<UserRoleState> UserRoleStates => this.GetTable<UserRoleState>();

    /// <summary>
    ///     Gets the VC roles table.
    /// </summary>
    public ITable<VcRole> VcRoles => this.GetTable<VcRole>();

    /// <summary>
    ///     Gets the vote roles table.
    /// </summary>
    public ITable<VoteRole> VoteRoles => this.GetTable<VoteRole>();

    /// <summary>
    ///     Gets the votes table.
    /// </summary>
    public ITable<Vote> Votes => this.GetTable<Vote>();

    /// <summary>
    ///     Gets the warning punishments table.
    /// </summary>
    public ITable<WarningPunishment> WarningPunishments => this.GetTable<WarningPunishment>();

    /// <summary>
    ///     Gets the warning punishments 2 table.
    /// </summary>
    public ITable<WarningPunishment2> WarningPunishment2s => this.GetTable<WarningPunishment2>();

    /// <summary>
    ///     Gets the warnings table.
    /// </summary>
    public ITable<Warning> Warnings => this.GetTable<Warning>();

    /// <summary>
    ///     Gets the warnings 2 table.
    /// </summary>
    public ITable<Warnings2> Warnings2s => this.GetTable<Warnings2>();

    /// <summary>
    ///     Gets the whitelisted roles table.
    /// </summary>
    public ITable<WhitelistedRole> WhitelistedRoles => this.GetTable<WhitelistedRole>();

    /// <summary>
    ///     Gets the whitelisted users table.
    /// </summary>
    public ITable<WhitelistedUser> WhitelistedUsers => this.GetTable<WhitelistedUser>();

    /// <summary>
    ///     Gets the XP currency rewards table.
    /// </summary>
    public ITable<XpCurrencyReward> XpCurrencyRewards => this.GetTable<XpCurrencyReward>();

    /// <summary>
    ///     Gets the XP excluded items table.
    /// </summary>
    public ITable<XpExcludedItem> XpExcludedItems => this.GetTable<XpExcludedItem>();

    /// <summary>
    ///     Gets the XP role multipliers table.
    /// </summary>
    public ITable<XpRoleMultiplier> XpRoleMultipliers => this.GetTable<XpRoleMultiplier>();

    /// <summary>
    ///     Gets the XP role rewards table.
    /// </summary>
    public ITable<XpRoleReward> XpRoleRewards => this.GetTable<XpRoleReward>();

    /// <summary>
    ///     Gets the XP level-up messages table.
    /// </summary>
    public ITable<XpLevelUpMessage> XpLevelUpMessages
    {
        get
        {
            return this.GetTable<XpLevelUpMessage>();
        }
    }

    /// <summary>
    ///     Gets the XP role trackings table.
    /// </summary>
    public ITable<XpRoleTracking> XpRoleTrackings => this.GetTable<XpRoleTracking>();

    /// <summary>
    ///     Gets the XP user snapshots table.
    /// </summary>
    public ITable<XpUserSnapshot> XpUserSnapshots => this.GetTable<XpUserSnapshot>();

    /// <summary>
    ///     Gets the EF migrations history table.
    /// </summary>
    public ITable<EfMigrationsHistory> EfMigrationsHistories => this.GetTable<EfMigrationsHistory>();

    /// <summary>
    ///     Gets the Patreon supporters table.
    /// </summary>
    public ITable<PatreonSupporter> PatreonSupporters => this.GetTable<PatreonSupporter>();

    /// <summary>
    ///     Gets the Patreon tiers table.
    /// </summary>
    public ITable<PatreonTier> PatreonTiers => this.GetTable<PatreonTier>();

    /// <summary>
    ///     Gets the Patreon goals table.
    /// </summary>
    public ITable<PatreonGoal> PatreonGoals => this.GetTable<PatreonGoal>();

    /// <summary>
    ///     Gets the todo lists table.
    /// </summary>
    public ITable<TodoList> TodoLists => this.GetTable<TodoList>();

    /// <summary>
    ///     Gets the todo items table.
    /// </summary>
    public ITable<TodoItem> TodoItems => this.GetTable<TodoItem>();

    /// <summary>
    ///     Gets the todo list permissions table.
    /// </summary>
    public ITable<TodoListPermission> TodoListPermissions => this.GetTable<TodoListPermission>();

    /// <summary>
    ///     Gets the user reputation table.
    /// </summary>
    public ITable<UserReputation> UserReputations
    {
        get
        {
            return this.GetTable<UserReputation>();
        }
    }

    /// <summary>
    ///     Gets the reputation history table.
    /// </summary>
    public ITable<RepHistory> RepHistories
    {
        get
        {
            return this.GetTable<RepHistory>();
        }
    }

    /// <summary>
    ///     Gets the reputation configuration table.
    /// </summary>
    public ITable<RepConfig> RepConfigs
    {
        get
        {
            return this.GetTable<RepConfig>();
        }
    }

    /// <summary>
    ///     Gets the reputation channel configuration table.
    /// </summary>
    public ITable<RepChannelConfig> RepChannelConfigs
    {
        get
        {
            return this.GetTable<RepChannelConfig>();
        }
    }

    /// <summary>
    ///     Gets the reputation role rewards table.
    /// </summary>
    public ITable<RepRoleRewards> RepRoleRewards
    {
        get
        {
            return this.GetTable<RepRoleRewards>();
        }
    }

    /// <summary>
    ///     Gets the reputation user settings table.
    /// </summary>
    public ITable<RepUserSettings> RepUserSettings
    {
        get
        {
            return this.GetTable<RepUserSettings>();
        }
    }

    /// <summary>
    ///     Gets the reputation cooldowns table.
    /// </summary>
    public ITable<RepCooldowns> RepCooldowns
    {
        get
        {
            return this.GetTable<RepCooldowns>();
        }
    }

    /// <summary>
    ///     Gets the reputation badges table.
    /// </summary>
    public ITable<RepBadges> RepBadges
    {
        get
        {
            return this.GetTable<RepBadges>();
        }
    }

    /// <summary>
    ///     Gets the reputation reaction configurations table.
    /// </summary>
    public ITable<RepReactionConfig> RepReactionConfigs
    {
        get
        {
            return this.GetTable<RepReactionConfig>();
        }
    }

    /// <summary>
    ///     Gets the custom reputation types table.
    /// </summary>
    public ITable<RepCustomType> RepCustomTypes
    {
        get
        {
            return this.GetTable<RepCustomType>();
        }
    }

    /// <summary>
    ///     Gets the user custom reputation table.
    /// </summary>
    public ITable<UserCustomReputation> UserCustomReputations
    {
        get
        {
            return this.GetTable<UserCustomReputation>();
        }
    }

    /// <summary>
    ///     Gets the reputation command requirements table.
    /// </summary>
    public ITable<RepCommandRequirement> RepCommandRequirements
    {
        get
        {
            return this.GetTable<RepCommandRequirement>();
        }
    }

    /// <summary>
    ///     Gets the reputation events table.
    /// </summary>
    public ITable<RepEvent> RepEvents
    {
        get
        {
            return this.GetTable<RepEvent>();
        }
    }

    /// <summary>
    ///     Gets the reputation challenges table.
    /// </summary>
    public ITable<RepChallenge> RepChallenges
    {
        get
        {
            return this.GetTable<RepChallenge>();
        }
    }

    /// <summary>
    ///     Gets the reputation challenge progress table.
    /// </summary>
    public ITable<RepChallengeProgress> RepChallengeProgresses
    {
        get
        {
            return this.GetTable<RepChallengeProgress>();
        }
    }

    /// <summary>
    ///     Gets the reputation weighted votes table.
    /// </summary>
    public ITable<RepWeightedVote> RepWeightedVotes
    {
        get
        {
            return this.GetTable<RepWeightedVote>();
        }
    }

    /// <summary>
    ///     Gets the reputation weighted vote records table.
    /// </summary>
    public ITable<RepWeightedVoteRecord> RepWeightedVoteRecords
    {
        get
        {
            return this.GetTable<RepWeightedVoteRecord>();
        }
    }

    /// <summary>
    ///     Gets the reputation metrics table.
    /// </summary>
    public ITable<RepMetrics> RepMetrics
    {
        get
        {
            return this.GetTable<RepMetrics>();
        }
    }

    /// <summary>
    ///     Gets the reputation hourly activity table.
    /// </summary>
    public ITable<RepHourlyActivity> RepHourlyActivities
    {
        get
        {
            return this.GetTable<RepHourlyActivity>();
        }
    }

    /// <summary>
    ///     Gets the reputation channel metrics table.
    /// </summary>
    public ITable<RepChannelMetrics> RepChannelMetrics
    {
        get
        {
            return this.GetTable<RepChannelMetrics>();
        }
    }

    /// <summary>
    ///     Gets the counting channels table.
    /// </summary>
    public ITable<CountingChannel> CountingChannels => this.GetTable<CountingChannel>();

    /// <summary>
    ///     Gets the counting channel configurations table.
    /// </summary>
    public ITable<CountingChannelConfig> CountingChannelConfigs => this.GetTable<CountingChannelConfig>();

    /// <summary>
    ///     Gets the counting statistics table.
    /// </summary>
    public ITable<CountingStats> CountingStats => this.GetTable<CountingStats>();

    /// <summary>
    ///     Gets the counting milestones table.
    /// </summary>
    public ITable<CountingMilestones> CountingMilestones => this.GetTable<CountingMilestones>();

    /// <summary>
    ///     Gets the counting events table.
    /// </summary>
    public ITable<CountingEvents> CountingEvents => this.GetTable<CountingEvents>();

    /// <summary>
    ///     Gets the counting user bans table.
    /// </summary>
    public ITable<CountingUserBans> CountingUserBans
    {
        get
        {
            return this.GetTable<CountingUserBans>();
        }
    }

    /// <summary>
    ///     Gets the counting saves table.
    /// </summary>
    public ITable<CountingSaves> CountingSaves => this.GetTable<CountingSaves>();

    /// <summary>
    ///     Gets the counting leaderboard table.
    /// </summary>
    public ITable<CountingLeaderboard> CountingLeaderboard => this.GetTable<CountingLeaderboard>();

    /// <summary>
    ///     Gets the counting moderation defaults table.
    /// </summary>
    public ITable<CountingModerationDefaults> CountingModerationDefaults
    {
        get
        {
            return this.GetTable<CountingModerationDefaults>();
        }
    }

    /// <summary>
    ///     Gets the counting moderation config table.
    /// </summary>
    public ITable<CountingModerationConfig> CountingModerationConfig
    {
        get
        {
            return this.GetTable<CountingModerationConfig>();
        }
    }

    /// <summary>
    ///     Gets the counting user wrong counts table.
    /// </summary>
    public ITable<CountingUserWrongCounts> CountingUserWrongCounts
    {
        get
        {
            return this.GetTable<CountingUserWrongCounts>();
        }
    }

    /// <summary>
    ///     Gets the counting applied punishments table.
    /// </summary>
    public ITable<CountingAppliedPunishments> CountingAppliedPunishments
    {
        get
        {
            return this.GetTable<CountingAppliedPunishments>();
        }
    }

    /// <summary>
    ///     Gets the counting moderation punishments table.
    /// </summary>
    public ITable<CountingModerationPunishments> CountingModerationPunishments
    {
        get
        {
            return this.GetTable<CountingModerationPunishments>();
        }
    }

    /// <summary>
    ///     Gets the forms table.
    /// </summary>
    public ITable<Form> Forms
    {
        get
        {
            return this.GetTable<Form>();
        }
    }

    /// <summary>
    ///     Gets the form questions table.
    /// </summary>
    public ITable<FormQuestion> FormQuestions
    {
        get
        {
            return this.GetTable<FormQuestion>();
        }
    }

    /// <summary>
    ///     Gets the form question options table.
    /// </summary>
    public ITable<FormQuestionOption> FormQuestionOptions
    {
        get
        {
            return this.GetTable<FormQuestionOption>();
        }
    }

    /// <summary>
    ///     Gets the form responses table.
    /// </summary>
    public ITable<FormResponse> FormResponses
    {
        get
        {
            return this.GetTable<FormResponse>();
        }
    }

    /// <summary>
    ///     Gets the form answers table.
    /// </summary>
    public ITable<FormAnswer> FormAnswers
    {
        get
        {
            return this.GetTable<FormAnswer>();
        }
    }

    /// <summary>
    ///     Gets the form share links table.
    /// </summary>
    public ITable<FormShareLink> FormShareLinks
    {
        get
        {
            return this.GetTable<FormShareLink>();
        }
    }

    /// <summary>
    ///     Gets the form response workflows table.
    /// </summary>
    public ITable<FormResponseWorkflow> FormResponseWorkflows
    {
        get
        {
            return this.GetTable<FormResponseWorkflow>();
        }
    }

    /// <summary>
    ///     Gets the form question conditions table for advanced conditional logic.
    /// </summary>
    public ITable<FormQuestionCondition> FormQuestionConditions
    {
        get
        {
            return this.GetTable<FormQuestionCondition>();
        }
    }
}