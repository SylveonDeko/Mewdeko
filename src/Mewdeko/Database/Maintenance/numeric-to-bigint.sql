-- MAINTENANCE WINDOW SCRIPT. Not part of the DbUp lanes; run by hand and confirm before starting.
--
-- Finishes the numeric(20,0) to bigint conversion that script 001 started for the ticket tables.
-- 507 columns across 208 tables remain. numeric(20,0) costs more storage per value than bigint, is
-- slower to compare, and forces every snowflake comparison through numeric arithmetic.
--
-- Before running:
--   1. Run numeric-to-bigint-preflight.sql and confirm it reports the conversion as safe.
--   2. Let the retention sweep in MessageTimestampRetentionService drain first. "MessageTimestamps" is
--      7.5 GB of the 10 GB database and every gigabyte still present is a gigabyte rewritten here.
--   3. Take a backup. This rewrites tables in place and there is no reverse script.
--
-- Each ALTER TABLE converts all of that table's columns in a single statement, so each table is
-- rewritten once rather than once per column. Every rewrite takes an ACCESS EXCLUSIVE lock, blocking
-- all access to that table for its duration, and rebuilds the table's indexes. Expect 30 to 60+ minutes
-- overall, dominated by the few largest tables.
--
-- The whole run is one transaction so a failure leaves the schema untouched. That also means the locks
-- are held until the end, so the bot should be stopped rather than merely idle.

\set ON_ERROR_STOP on

BEGIN;

ALTER TABLE "AFK"
    ALTER COLUMN "GuildId" TYPE BIGINT,
    ALTER COLUMN "UserId" TYPE BIGINT;
ALTER TABLE "AiConversations"
    ALTER COLUMN "GuildId" TYPE BIGINT,
    ALTER COLUMN "UserId" TYPE BIGINT;
ALTER TABLE "AntiAltSetting"
    ALTER COLUMN "GuildId" TYPE BIGINT,
    ALTER COLUMN "RoleId" TYPE BIGINT;
ALTER TABLE "AntiImageHashIgnoredChannels"
    ALTER COLUMN "ChannelId" TYPE BIGINT,
    ALTER COLUMN "GuildId" TYPE BIGINT;
ALTER TABLE "AntiImageHashIgnoredRoles"
    ALTER COLUMN "GuildId" TYPE BIGINT,
    ALTER COLUMN "RoleId" TYPE BIGINT;
ALTER TABLE "AntiImageHashSettings"
    ALTER COLUMN "GuildId" TYPE BIGINT,
    ALTER COLUMN "RoleId" TYPE BIGINT;
ALTER TABLE "AntiMassMentionSetting"
    ALTER COLUMN "GuildId" TYPE BIGINT,
    ALTER COLUMN "RoleId" TYPE BIGINT;
ALTER TABLE "AntiRaidSetting"
    ALTER COLUMN "GuildId" TYPE BIGINT;
ALTER TABLE "AntiSpamIgnore"
    ALTER COLUMN "ChannelId" TYPE BIGINT;
ALTER TABLE "AntiSpamSetting"
    ALTER COLUMN "GuildId" TYPE BIGINT,
    ALTER COLUMN "RoleId" TYPE BIGINT;
ALTER TABLE "AuthCodes"
    ALTER COLUMN "UserId" TYPE BIGINT;
ALTER TABLE "AutoBanRoles"
    ALTER COLUMN "GuildId" TYPE BIGINT,
    ALTER COLUMN "RoleId" TYPE BIGINT;
ALTER TABLE "AutoBanWords"
    ALTER COLUMN "GuildId" TYPE BIGINT;
ALTER TABLE "AutoCommands"
    ALTER COLUMN "ChannelId" TYPE BIGINT,
    ALTER COLUMN "GuildId" TYPE BIGINT,
    ALTER COLUMN "VoiceChannelId" TYPE BIGINT;
ALTER TABLE "AutoPublish"
    ALTER COLUMN "BlacklistedUsers" TYPE BIGINT,
    ALTER COLUMN "ChannelId" TYPE BIGINT,
    ALTER COLUMN "GuildId" TYPE BIGINT;
ALTER TABLE "BanPruneSettings"
    ALTER COLUMN "GuildId" TYPE BIGINT,
    ALTER COLUMN "ScopeId" TYPE BIGINT;
ALTER TABLE "BanTemplates"
    ALTER COLUMN "GuildId" TYPE BIGINT;
ALTER TABLE "BannedImageHashes"
    ALTER COLUMN "AddedBy" TYPE BIGINT,
    ALTER COLUMN "GuildId" TYPE BIGINT,
    ALTER COLUMN "RoleId" TYPE BIGINT;
ALTER TABLE "BirthdayConfigs"
    ALTER COLUMN "BirthdayChannelId" TYPE BIGINT,
    ALTER COLUMN "BirthdayPingRoleId" TYPE BIGINT,
    ALTER COLUMN "BirthdayRoleId" TYPE BIGINT,
    ALTER COLUMN "GuildId" TYPE BIGINT;
ALTER TABLE "Blacklist"
    ALTER COLUMN "ItemId" TYPE BIGINT;
ALTER TABLE "BlacklistedPermissions"
    ALTER COLUMN "GuildId" TYPE BIGINT,
    ALTER COLUMN "Permission" TYPE BIGINT;
ALTER TABLE "BlacklistedRoles"
    ALTER COLUMN "GuildId" TYPE BIGINT,
    ALTER COLUMN "RoleId" TYPE BIGINT;
ALTER TABLE "BotInstances"
    ALTER COLUMN "BotId" TYPE BIGINT;
ALTER TABLE "BotReviews"
    ALTER COLUMN "UserId" TYPE BIGINT;
ALTER TABLE "ChannelAccessApplications"
    ALTER COLUMN "GuildId" TYPE BIGINT,
    ALTER COLUMN "MessageChannelId" TYPE BIGINT,
    ALTER COLUMN "MessageId" TYPE BIGINT,
    ALTER COLUMN "ResolvedBy" TYPE BIGINT,
    ALTER COLUMN "ThreadId" TYPE BIGINT,
    ALTER COLUMN "UserId" TYPE BIGINT;
ALTER TABLE "ChannelAccessBlacklists"
    ALTER COLUMN "AddedBy" TYPE BIGINT,
    ALTER COLUMN "GuildId" TYPE BIGINT,
    ALTER COLUMN "UserId" TYPE BIGINT;
ALTER TABLE "ChannelAccessConfigs"
    ALTER COLUMN "AccessRoleId" TYPE BIGINT,
    ALTER COLUMN "ChannelId" TYPE BIGINT,
    ALTER COLUMN "CreatedBy" TYPE BIGINT,
    ALTER COLUMN "GuildId" TYPE BIGINT,
    ALTER COLUMN "LogChannelId" TYPE BIGINT,
    ALTER COLUMN "PanelChannelId" TYPE BIGINT,
    ALTER COLUMN "PanelMessageId" TYPE BIGINT,
    ALTER COLUMN "PingRoleId" TYPE BIGINT,
    ALTER COLUMN "ReviewChannelId" TYPE BIGINT,
    ALTER COLUMN "VoterRoleId" TYPE BIGINT;
ALTER TABLE "ChannelAccessVotes"
    ALTER COLUMN "UserId" TYPE BIGINT;
ALTER TABLE "ChatLogs"
    ALTER COLUMN "ChannelId" TYPE BIGINT,
    ALTER COLUMN "CreatedBy" TYPE BIGINT,
    ALTER COLUMN "GuildId" TYPE BIGINT;
ALTER TABLE "ChatTriggerCounters"
    ALTER COLUMN "GuildId" TYPE BIGINT,
    ALTER COLUMN "UserId" TYPE BIGINT;
ALTER TABLE "ChatTriggers"
    ALTER COLUMN "ApplicationCommandId" TYPE BIGINT,
    ALTER COLUMN "CrosspostingChannelId" TYPE BIGINT,
    ALTER COLUMN "EventChannelId" TYPE BIGINT,
    ALTER COLUMN "GuildId" TYPE BIGINT,
    ALTER COLUMN "UseCount" TYPE BIGINT;
ALTER TABLE "CommandAlias"
    ALTER COLUMN "GuildId" TYPE BIGINT;
ALTER TABLE "CommandCooldown"
    ALTER COLUMN "GuildId" TYPE BIGINT;
ALTER TABLE "CommandStats"
    ALTER COLUMN "ChannelId" TYPE BIGINT,
    ALTER COLUMN "GuildId" TYPE BIGINT,
    ALTER COLUMN "UserId" TYPE BIGINT;
ALTER TABLE "Confessions"
    ALTER COLUMN "ChannelId" TYPE BIGINT,
    ALTER COLUMN "ConfessNumber" TYPE BIGINT,
    ALTER COLUMN "GuildId" TYPE BIGINT,
    ALTER COLUMN "MessageId" TYPE BIGINT,
    ALTER COLUMN "UserId" TYPE BIGINT;
ALTER TABLE "CountingAppliedPunishments"
    ALTER COLUMN "ChannelId" TYPE BIGINT,
    ALTER COLUMN "RoleId" TYPE BIGINT,
    ALTER COLUMN "UserId" TYPE BIGINT;
ALTER TABLE "CountingChannel"
    ALTER COLUMN "ChannelId" TYPE BIGINT,
    ALTER COLUMN "GuildId" TYPE BIGINT,
    ALTER COLUMN "LastMessageId" TYPE BIGINT,
    ALTER COLUMN "LastUserId" TYPE BIGINT;
ALTER TABLE "CountingChannelConfig"
    ALTER COLUMN "ChannelId" TYPE BIGINT,
    ALTER COLUMN "FailureChannelId" TYPE BIGINT,
    ALTER COLUMN "MilestoneChannelId" TYPE BIGINT,
    ALTER COLUMN "PunishmentRoleId" TYPE BIGINT;
ALTER TABLE "CountingEvents"
    ALTER COLUMN "ChannelId" TYPE BIGINT,
    ALTER COLUMN "MessageId" TYPE BIGINT,
    ALTER COLUMN "UserId" TYPE BIGINT;
ALTER TABLE "CountingLeaderboard"
    ALTER COLUMN "ChannelId" TYPE BIGINT,
    ALTER COLUMN "UserId" TYPE BIGINT;
ALTER TABLE "CountingMilestones"
    ALTER COLUMN "ChannelId" TYPE BIGINT,
    ALTER COLUMN "UserId" TYPE BIGINT;
ALTER TABLE "CountingModerationConfig"
    ALTER COLUMN "ChannelId" TYPE BIGINT,
    ALTER COLUMN "PunishmentRoleId" TYPE BIGINT;
ALTER TABLE "CountingModerationDefaults"
    ALTER COLUMN "GuildId" TYPE BIGINT,
    ALTER COLUMN "PunishmentRoleId" TYPE BIGINT;
ALTER TABLE "CountingModerationPunishments"
    ALTER COLUMN "ChannelId" TYPE BIGINT,
    ALTER COLUMN "GuildId" TYPE BIGINT,
    ALTER COLUMN "RoleId" TYPE BIGINT;
ALTER TABLE "CountingSaves"
    ALTER COLUMN "ChannelId" TYPE BIGINT,
    ALTER COLUMN "SavedBy" TYPE BIGINT;
ALTER TABLE "CountingStats"
    ALTER COLUMN "ChannelId" TYPE BIGINT,
    ALTER COLUMN "UserId" TYPE BIGINT;
ALTER TABLE "CountingUserBans"
    ALTER COLUMN "BannedBy" TYPE BIGINT,
    ALTER COLUMN "ChannelId" TYPE BIGINT,
    ALTER COLUMN "UserId" TYPE BIGINT;
ALTER TABLE "CountingUserWrongCounts"
    ALTER COLUMN "ChannelId" TYPE BIGINT,
    ALTER COLUMN "UserId" TYPE BIGINT;
ALTER TABLE "CurrencyConfigs"
    ALTER COLUMN "GuildId" TYPE BIGINT;
ALTER TABLE "CurrencyCooldowns"
    ALTER COLUMN "GuildId" TYPE BIGINT,
    ALTER COLUMN "UserId" TYPE BIGINT;
ALTER TABLE "CustomVoiceChannel"
    ALTER COLUMN "ChannelId" TYPE BIGINT,
    ALTER COLUMN "GuildId" TYPE BIGINT,
    ALTER COLUMN "OwnerId" TYPE BIGINT;
ALTER TABLE "CustomVoiceConfig"
    ALTER COLUMN "ChannelCategoryId" TYPE BIGINT,
    ALTER COLUMN "CustomVoiceAdminRoleId" TYPE BIGINT,
    ALTER COLUMN "GuildId" TYPE BIGINT,
    ALTER COLUMN "HubVoiceChannelId" TYPE BIGINT;
ALTER TABLE "DailyChallenges"
    ALTER COLUMN "GuildId" TYPE BIGINT,
    ALTER COLUMN "UserId" TYPE BIGINT;
ALTER TABLE "DashboardAccess"
    ALTER COLUMN "GrantedBy" TYPE BIGINT,
    ALTER COLUMN "GuildId" TYPE BIGINT,
    ALTER COLUMN "TargetId" TYPE BIGINT;
ALTER TABLE "DashboardAccessManagers"
    ALTER COLUMN "GrantedBy" TYPE BIGINT,
    ALTER COLUMN "GuildId" TYPE BIGINT,
    ALTER COLUMN "TargetId" TYPE BIGINT;
ALTER TABLE "DashboardAccessSettings"
    ALTER COLUMN "GuildId" TYPE BIGINT;
ALTER TABLE "DashboardAuditLogs"
    ALTER COLUMN "GuildId" TYPE BIGINT,
    ALTER COLUMN "UserId" TYPE BIGINT;
ALTER TABLE "DelMsgOnCmdChannels"
    ALTER COLUMN "ChannelId" TYPE BIGINT,
    ALTER COLUMN "GuildId" TYPE BIGINT;
ALTER TABLE "DiscordPermOverrides"
    ALTER COLUMN "GuildId" TYPE BIGINT,
    ALTER COLUMN "Perm" TYPE BIGINT;
ALTER TABLE "DiscordUser"
    ALTER COLUMN "UserId" TYPE BIGINT;
ALTER TABLE "EmbedWebhookPersonas"
    ALTER COLUMN "GuildId" TYPE BIGINT,
    ALTER COLUMN "UserId" TYPE BIGINT;
ALTER TABLE "Embeds"
    ALTER COLUMN "GuildId" TYPE BIGINT,
    ALTER COLUMN "UserId" TYPE BIGINT;
ALTER TABLE "FeedSub"
    ALTER COLUMN "ChannelId" TYPE BIGINT,
    ALTER COLUMN "GuildId" TYPE BIGINT;
ALTER TABLE "FilterInvitesChannelIds"
    ALTER COLUMN "ChannelId" TYPE BIGINT,
    ALTER COLUMN "GuildId" TYPE BIGINT;
ALTER TABLE "FilterLinksChannelIds"
    ALTER COLUMN "ChannelId" TYPE BIGINT,
    ALTER COLUMN "GuildId" TYPE BIGINT;
ALTER TABLE "FilterWordsChannelIds"
    ALTER COLUMN "ChannelId" TYPE BIGINT,
    ALTER COLUMN "GuildId" TYPE BIGINT;
ALTER TABLE "FilteredWord"
    ALTER COLUMN "GuildId" TYPE BIGINT;
ALTER TABLE "FollowedStreams"
    ALTER COLUMN "ChannelId" TYPE BIGINT,
    ALTER COLUMN "GuildId" TYPE BIGINT;
ALTER TABLE "GiveawayUsers"
    ALTER COLUMN "UserId" TYPE BIGINT;
ALTER TABLE "Giveaways"
    ALTER COLUMN "ChannelId" TYPE BIGINT,
    ALTER COLUMN "MessageCountReq" TYPE BIGINT,
    ALTER COLUMN "MessageId" TYPE BIGINT,
    ALTER COLUMN "ServerId" TYPE BIGINT,
    ALTER COLUMN "UserId" TYPE BIGINT;
ALTER TABLE "GlobalUserBalance"
    ALTER COLUMN "UserId" TYPE BIGINT;
ALTER TABLE "GroupName"
    ALTER COLUMN "GuildId" TYPE BIGINT;
ALTER TABLE "GuildAiConfig"
    ALTER COLUMN "ChannelId" TYPE BIGINT,
    ALTER COLUMN "GuildId" TYPE BIGINT;
ALTER TABLE "GuildBotProfiles"
    ALTER COLUMN "GuildId" TYPE BIGINT;
ALTER TABLE "GuildConfigs"
    ALTER COLUMN "AcceptChannel" TYPE BIGINT,
    ALTER COLUMN "BoostMessageChannelId" TYPE BIGINT,
    ALTER COLUMN "ByeMessageChannelId" TYPE BIGINT,
    ALTER COLUMN "CommandLogChannel" TYPE BIGINT,
    ALTER COLUMN "ConfessionChannel" TYPE BIGINT,
    ALTER COLUMN "ConfessionLogChannel" TYPE BIGINT,
    ALTER COLUMN "ConsiderChannel" TYPE BIGINT,
    ALTER COLUMN "DenyChannel" TYPE BIGINT,
    ALTER COLUMN "GameVoiceChannel" TYPE BIGINT,
    ALTER COLUMN "GiveawayPingRole" TYPE BIGINT,
    ALTER COLUMN "GuildId" TYPE BIGINT,
    ALTER COLUMN "ImplementChannel" TYPE BIGINT,
    ALTER COLUMN "MemberRole" TYPE BIGINT,
    ALTER COLUMN "MiniWarnlogChannelId" TYPE BIGINT,
    ALTER COLUMN "PatreonChannelId" TYPE BIGINT,
    ALTER COLUMN "PatreonGoalChannel" TYPE BIGINT,
    ALTER COLUMN "PatreonStatsChannel" TYPE BIGINT,
    ALTER COLUMN "StaffRole" TYPE BIGINT,
    ALTER COLUMN "SuggestButtonChannel" TYPE BIGINT,
    ALTER COLUMN "SuggestButtonMessageId" TYPE BIGINT,
    ALTER COLUMN "TicketCategory" TYPE BIGINT,
    ALTER COLUMN "TicketChannel" TYPE BIGINT,
    ALTER COLUMN "VotesChannel" TYPE BIGINT,
    ALTER COLUMN "WarnlogChannelId" TYPE BIGINT,
    ALTER COLUMN "WizardCompletedByUserId" TYPE BIGINT,
    ALTER COLUMN "sugchan" TYPE BIGINT,
    ALTER COLUMN "sugnum" TYPE BIGINT;
ALTER TABLE "GuildEmoteUsage"
    ALTER COLUMN "GuildId" TYPE BIGINT;
ALTER TABLE "GuildRepeater"
    ALTER COLUMN "ChannelId" TYPE BIGINT,
    ALTER COLUMN "GuildId" TYPE BIGINT,
    ALTER COLUMN "LastMessageId" TYPE BIGINT;
ALTER TABLE "GuildTicketSettings"
    ALTER COLUMN "GuildId" TYPE BIGINT,
    ALTER COLUMN "LogChannelId" TYPE BIGINT,
    ALTER COLUMN "TranscriptChannelId" TYPE BIGINT;
ALTER TABLE "GuildUserBalance"
    ALTER COLUMN "GuildId" TYPE BIGINT,
    ALTER COLUMN "UserId" TYPE BIGINT;
ALTER TABLE "GuildUserXp"
    ALTER COLUMN "GuildId" TYPE BIGINT,
    ALTER COLUMN "UserId" TYPE BIGINT;
ALTER TABLE "GuildXpSettings"
    ALTER COLUMN "GuildId" TYPE BIGINT,
    ALTER COLUMN "LevelUpChannel" TYPE BIGINT;
ALTER TABLE "HighlightSettings"
    ALTER COLUMN "GuildId" TYPE BIGINT,
    ALTER COLUMN "UserId" TYPE BIGINT;
ALTER TABLE "Highlights"
    ALTER COLUMN "GuildId" TYPE BIGINT,
    ALTER COLUMN "UserId" TYPE BIGINT;
ALTER TABLE "InviteCountSettings"
    ALTER COLUMN "GuildId" TYPE BIGINT;
ALTER TABLE "InviteCounts"
    ALTER COLUMN "GuildId" TYPE BIGINT,
    ALTER COLUMN "UserId" TYPE BIGINT;
ALTER TABLE "InvitedBy"
    ALTER COLUMN "GuildId" TYPE BIGINT,
    ALTER COLUMN "InviterId" TYPE BIGINT,
    ALTER COLUMN "UserId" TYPE BIGINT;
ALTER TABLE "JoinLeaveLogs"
    ALTER COLUMN "GuildId" TYPE BIGINT,
    ALTER COLUMN "UserId" TYPE BIGINT;
ALTER TABLE "LastFmUsers"
    ALTER COLUMN "UserId" TYPE BIGINT;
ALTER TABLE "LockdownChannelPermissions"
    ALTER COLUMN "AllowPermissions" TYPE BIGINT,
    ALTER COLUMN "ChannelId" TYPE BIGINT,
    ALTER COLUMN "DenyPermissions" TYPE BIGINT,
    ALTER COLUMN "GuildId" TYPE BIGINT,
    ALTER COLUMN "TargetId" TYPE BIGINT;
ALTER TABLE "LockdownJoinSettings"
    ALTER COLUMN "GuildId" TYPE BIGINT;
ALTER TABLE "LogIgnoredChannels"
    ALTER COLUMN "ChannelId" TYPE BIGINT,
    ALTER COLUMN "GuildId" TYPE BIGINT;
ALTER TABLE "LoggingV2"
    ALTER COLUMN "AvatarUpdatedId" TYPE BIGINT,
    ALTER COLUMN "ChannelCreatedId" TYPE BIGINT,
    ALTER COLUMN "ChannelDestroyedId" TYPE BIGINT,
    ALTER COLUMN "ChannelUpdatedId" TYPE BIGINT,
    ALTER COLUMN "EventCreatedId" TYPE BIGINT,
    ALTER COLUMN "GuildId" TYPE BIGINT,
    ALTER COLUMN "InviteCreatedId" TYPE BIGINT,
    ALTER COLUMN "InviteDeletedId" TYPE BIGINT,
    ALTER COLUMN "LogOtherId" TYPE BIGINT,
    ALTER COLUMN "LogUserPresenceId" TYPE BIGINT,
    ALTER COLUMN "LogVoicePresenceId" TYPE BIGINT,
    ALTER COLUMN "LogVoicePresenceTtsId" TYPE BIGINT,
    ALTER COLUMN "MessageDeletedId" TYPE BIGINT,
    ALTER COLUMN "MessageUpdatedId" TYPE BIGINT,
    ALTER COLUMN "MessagesBulkDeletedId" TYPE BIGINT,
    ALTER COLUMN "NicknameUpdatedId" TYPE BIGINT,
    ALTER COLUMN "ReactionEventsId" TYPE BIGINT,
    ALTER COLUMN "RoleCreatedId" TYPE BIGINT,
    ALTER COLUMN "RoleDeletedId" TYPE BIGINT,
    ALTER COLUMN "RoleUpdatedId" TYPE BIGINT,
    ALTER COLUMN "ServerUpdatedId" TYPE BIGINT,
    ALTER COLUMN "ThreadCreatedId" TYPE BIGINT,
    ALTER COLUMN "ThreadDeletedId" TYPE BIGINT,
    ALTER COLUMN "ThreadUpdatedId" TYPE BIGINT,
    ALTER COLUMN "UserBannedId" TYPE BIGINT,
    ALTER COLUMN "UserJoinedId" TYPE BIGINT,
    ALTER COLUMN "UserLeftId" TYPE BIGINT,
    ALTER COLUMN "UserMutedId" TYPE BIGINT,
    ALTER COLUMN "UserRoleAddedId" TYPE BIGINT,
    ALTER COLUMN "UserRoleRemovedId" TYPE BIGINT,
    ALTER COLUMN "UserUnbannedId" TYPE BIGINT,
    ALTER COLUMN "UserUpdatedId" TYPE BIGINT,
    ALTER COLUMN "UsernameUpdatedId" TYPE BIGINT;
ALTER TABLE "MessageCounts"
    ALTER COLUMN "ChannelId" TYPE BIGINT,
    ALTER COLUMN "Count" TYPE BIGINT,
    ALTER COLUMN "GuildId" TYPE BIGINT,
    ALTER COLUMN "UserId" TYPE BIGINT;
ALTER TABLE "MessageTimestamps"
    ALTER COLUMN "ChannelId" TYPE BIGINT,
    ALTER COLUMN "GuildId" TYPE BIGINT,
    ALTER COLUMN "UserId" TYPE BIGINT;
ALTER TABLE "MinecraftServers"
    ALTER COLUMN "AdvancementChannelId" TYPE BIGINT,
    ALTER COLUMN "ChatChannelId" TYPE BIGINT,
    ALTER COLUMN "DeathChannelId" TYPE BIGINT,
    ALTER COLUMN "GuildId" TYPE BIGINT,
    ALTER COLUMN "JoinLeaveChannelId" TYPE BIGINT,
    ALTER COLUMN "WatchChannelId" TYPE BIGINT,
    ALTER COLUMN "WatchMessageId" TYPE BIGINT;
ALTER TABLE "MostActiveStarChannels"
    ALTER COLUMN "ChannelId" TYPE BIGINT,
    ALTER COLUMN "GuildId" TYPE BIGINT;
ALTER TABLE "MostActiveStarrers"
    ALTER COLUMN "GuildId" TYPE BIGINT,
    ALTER COLUMN "UserId" TYPE BIGINT;
ALTER TABLE "MostStarredUsers"
    ALTER COLUMN "AuthorId" TYPE BIGINT,
    ALTER COLUMN "GuildId" TYPE BIGINT;
ALTER TABLE "MultiGreets"
    ALTER COLUMN "ChannelId" TYPE BIGINT,
    ALTER COLUMN "GuildId" TYPE BIGINT;
ALTER TABLE "MusicLinkChannels"
    ALTER COLUMN "ChannelId" TYPE BIGINT,
    ALTER COLUMN "GuildId" TYPE BIGINT;
ALTER TABLE "MusicPlayerSettings"
    ALTER COLUMN "DjRoleId" TYPE BIGINT,
    ALTER COLUMN "GuildId" TYPE BIGINT,
    ALTER COLUMN "MusicChannelId" TYPE BIGINT,
    ALTER COLUMN "TtsChannelId" TYPE BIGINT,
    ALTER COLUMN "TtsRoleId" TYPE BIGINT;
ALTER TABLE "MusicPlaylists"
    ALTER COLUMN "AuthorId" TYPE BIGINT,
    ALTER COLUMN "GuildId" TYPE BIGINT;
ALTER TABLE "MutedUserId"
    ALTER COLUMN "GuildId" TYPE BIGINT,
    ALTER COLUMN "UserId" TYPE BIGINT;
ALTER TABLE "NsfwBlacklistedTags"
    ALTER COLUMN "GuildId" TYPE BIGINT;
ALTER TABLE "PatreonGoals"
    ALTER COLUMN "GuildId" TYPE BIGINT;
ALTER TABLE "PatreonSupporters"
    ALTER COLUMN "DiscordUserId" TYPE BIGINT,
    ALTER COLUMN "GuildId" TYPE BIGINT;
ALTER TABLE "PatreonTiers"
    ALTER COLUMN "DiscordRoleId" TYPE BIGINT,
    ALTER COLUMN "GuildId" TYPE BIGINT;
ALTER TABLE "Permission"
    ALTER COLUMN "PrimaryTargetId" TYPE BIGINT;
ALTER TABLE "Permissions"
    ALTER COLUMN "GuildId" TYPE BIGINT,
    ALTER COLUMN "PrimaryTargetId" TYPE BIGINT;
ALTER TABLE "Poll"
    ALTER COLUMN "ChannelId" TYPE BIGINT,
    ALTER COLUMN "GuildId" TYPE BIGINT;
ALTER TABLE "PollTemplates"
    ALTER COLUMN "CreatorId" TYPE BIGINT,
    ALTER COLUMN "GuildId" TYPE BIGINT;
ALTER TABLE "PollVote"
    ALTER COLUMN "UserId" TYPE BIGINT;
ALTER TABLE "PollVotes"
    ALTER COLUMN "UserId" TYPE BIGINT;
ALTER TABLE "Polls"
    ALTER COLUMN "ChannelId" TYPE BIGINT,
    ALTER COLUMN "CreatorId" TYPE BIGINT,
    ALTER COLUMN "GuildId" TYPE BIGINT,
    ALTER COLUMN "MessageId" TYPE BIGINT;
ALTER TABLE "PublishUserBlacklist"
    ALTER COLUMN "ChannelId" TYPE BIGINT,
    ALTER COLUMN "User" TYPE BIGINT;
ALTER TABLE "PublishWordBlacklist"
    ALTER COLUMN "ChannelId" TYPE BIGINT;
ALTER TABLE "Quotes"
    ALTER COLUMN "AuthorId" TYPE BIGINT,
    ALTER COLUMN "GuildId" TYPE BIGINT,
    ALTER COLUMN "UseCount" TYPE BIGINT;
ALTER TABLE "ReactionRole"
    ALTER COLUMN "RoleId" TYPE BIGINT;
ALTER TABLE "ReactionRoleMessages"
    ALTER COLUMN "ChannelId" TYPE BIGINT,
    ALTER COLUMN "GuildId" TYPE BIGINT,
    ALTER COLUMN "MessageId" TYPE BIGINT;
ALTER TABLE "Reminders"
    ALTER COLUMN "ChannelId" TYPE BIGINT,
    ALTER COLUMN "ServerId" TYPE BIGINT,
    ALTER COLUMN "UserId" TYPE BIGINT;
ALTER TABLE "RepBadges"
    ALTER COLUMN "GuildId" TYPE BIGINT,
    ALTER COLUMN "UserId" TYPE BIGINT;
ALTER TABLE "RepChannelConfig"
    ALTER COLUMN "ChannelId" TYPE BIGINT,
    ALTER COLUMN "GuildId" TYPE BIGINT,
    ALTER COLUMN "Multiplier" TYPE BIGINT;
ALTER TABLE "RepChannelMetrics"
    ALTER COLUMN "AverageRep" TYPE BIGINT;
ALTER TABLE "RepCommandRequirements"
    ALTER COLUMN "GuildId" TYPE BIGINT;
ALTER TABLE "RepConfig"
    ALTER COLUMN "GuildId" TYPE BIGINT,
    ALTER COLUMN "NotificationChannel" TYPE BIGINT;
ALTER TABLE "RepCooldowns"
    ALTER COLUMN "GiverId" TYPE BIGINT,
    ALTER COLUMN "GuildId" TYPE BIGINT,
    ALTER COLUMN "ReceiverId" TYPE BIGINT;
ALTER TABLE "RepCustomType"
    ALTER COLUMN "GuildId" TYPE BIGINT,
    ALTER COLUMN "Multiplier" TYPE BIGINT;
ALTER TABLE "RepEvent"
    ALTER COLUMN "Multiplier" TYPE BIGINT;
ALTER TABLE "RepHistory"
    ALTER COLUMN "ChannelId" TYPE BIGINT,
    ALTER COLUMN "GiverId" TYPE BIGINT,
    ALTER COLUMN "GuildId" TYPE BIGINT,
    ALTER COLUMN "ReceiverId" TYPE BIGINT;
ALTER TABLE "RepMetrics"
    ALTER COLUMN "AverageRepPerUser" TYPE BIGINT,
    ALTER COLUMN "RetentionRate" TYPE BIGINT;
ALTER TABLE "RepReactionConfig"
    ALTER COLUMN "EmojiId" TYPE BIGINT,
    ALTER COLUMN "GuildId" TYPE BIGINT,
    ALTER COLUMN "RequiredRoleId" TYPE BIGINT;
ALTER TABLE "RepRoleRewards"
    ALTER COLUMN "AnnounceChannel" TYPE BIGINT,
    ALTER COLUMN "GuildId" TYPE BIGINT,
    ALTER COLUMN "RoleId" TYPE BIGINT;
ALTER TABLE "RepUserSettings"
    ALTER COLUMN "GuildId" TYPE BIGINT,
    ALTER COLUMN "UserId" TYPE BIGINT;
ALTER TABLE "RepWeightedVoteRecord"
    ALTER COLUMN "VoteWeight" TYPE BIGINT;
ALTER TABLE "RoleGreets"
    ALTER COLUMN "ChannelId" TYPE BIGINT,
    ALTER COLUMN "GuildId" TYPE BIGINT,
    ALTER COLUMN "RoleId" TYPE BIGINT;
ALTER TABLE "RoleMonitoringSettings"
    ALTER COLUMN "GuildId" TYPE BIGINT;
ALTER TABLE "RoleStateSettings"
    ALTER COLUMN "GuildId" TYPE BIGINT;
ALTER TABLE "ScheduledTicketDeletions"
    ALTER COLUMN "ChannelId" TYPE BIGINT,
    ALTER COLUMN "GuildId" TYPE BIGINT;
ALTER TABLE "SelfAssignableRoles"
    ALTER COLUMN "GuildId" TYPE BIGINT,
    ALTER COLUMN "RoleId" TYPE BIGINT;
ALTER TABLE "ServerRecoveryStore"
    ALTER COLUMN "GuildId" TYPE BIGINT;
ALTER TABLE "ShopItems"
    ALTER COLUMN "GuildId" TYPE BIGINT,
    ALTER COLUMN "RequiredRoleId" TYPE BIGINT,
    ALTER COLUMN "RoleId" TYPE BIGINT;
ALTER TABLE "StarboardPosts"
    ALTER COLUMN "MessageId" TYPE BIGINT,
    ALTER COLUMN "PostId" TYPE BIGINT;
ALTER TABLE "StarboardReactions"
    ALTER COLUMN "MessageId" TYPE BIGINT,
    ALTER COLUMN "UserId" TYPE BIGINT;
ALTER TABLE "StarboardStats"
    ALTER COLUMN "AuthorId" TYPE BIGINT,
    ALTER COLUMN "ChannelId" TYPE BIGINT,
    ALTER COLUMN "MessageId" TYPE BIGINT;
ALTER TABLE "Starboards"
    ALTER COLUMN "GuildId" TYPE BIGINT,
    ALTER COLUMN "StarboardChannelId" TYPE BIGINT;
ALTER TABLE "StatChannelSettings"
    ALTER COLUMN "GuildId" TYPE BIGINT;
ALTER TABLE "StatChannels"
    ALTER COLUMN "CategoryId" TYPE BIGINT,
    ALTER COLUMN "ChannelId" TYPE BIGINT,
    ALTER COLUMN "GuildId" TYPE BIGINT,
    ALTER COLUMN "RoleId" TYPE BIGINT,
    ALTER COLUMN "TargetId" TYPE BIGINT;
ALTER TABLE "StatusRoles"
    ALTER COLUMN "GuildId" TYPE BIGINT,
    ALTER COLUMN "StatusChannelId" TYPE BIGINT;
ALTER TABLE "StreamRoleBlacklistedUser"
    ALTER COLUMN "UserId" TYPE BIGINT;
ALTER TABLE "StreamRoleSettings"
    ALTER COLUMN "AddRoleId" TYPE BIGINT,
    ALTER COLUMN "FromRoleId" TYPE BIGINT,
    ALTER COLUMN "GuildId" TYPE BIGINT;
ALTER TABLE "StreamRoleWhitelistedUser"
    ALTER COLUMN "UserId" TYPE BIGINT;
ALTER TABLE "SuggestThreads"
    ALTER COLUMN "MessageId" TYPE BIGINT,
    ALTER COLUMN "ThreadChannelId" TYPE BIGINT;
ALTER TABLE "SuggestVotes"
    ALTER COLUMN "MessageId" TYPE BIGINT,
    ALTER COLUMN "UserId" TYPE BIGINT;
ALTER TABLE "Suggestions"
    ALTER COLUMN "GuildId" TYPE BIGINT,
    ALTER COLUMN "MessageId" TYPE BIGINT,
    ALTER COLUMN "StateChangeCount" TYPE BIGINT,
    ALTER COLUMN "StateChangeMessageId" TYPE BIGINT,
    ALTER COLUMN "StateChangeUser" TYPE BIGINT,
    ALTER COLUMN "SuggestionId" TYPE BIGINT,
    ALTER COLUMN "UserId" TYPE BIGINT;
ALTER TABLE "Template"
    ALTER COLUMN "GuildId" TYPE BIGINT;
ALTER TABLE "TicketActionLogs"
    ALTER COLUMN "ChannelId" TYPE BIGINT,
    ALTER COLUMN "GuildId" TYPE BIGINT,
    ALTER COLUMN "UserId" TYPE BIGINT;
ALTER TABLE "TicketPriorities"
    ALTER COLUMN "GuildId" TYPE BIGINT;
ALTER TABLE "TicketTags"
    ALTER COLUMN "GuildId" TYPE BIGINT;
ALTER TABLE "TodoItems"
    ALTER COLUMN "CompletedBy" TYPE BIGINT,
    ALTER COLUMN "CreatedBy" TYPE BIGINT;
ALTER TABLE "TodoListPermissions"
    ALTER COLUMN "GrantedBy" TYPE BIGINT,
    ALTER COLUMN "UserId" TYPE BIGINT;
ALTER TABLE "TodoLists"
    ALTER COLUMN "GuildId" TYPE BIGINT,
    ALTER COLUMN "OwnerId" TYPE BIGINT;
ALTER TABLE "TransactionHistory"
    ALTER COLUMN "GuildId" TYPE BIGINT,
    ALTER COLUMN "UserId" TYPE BIGINT;
ALTER TABLE "TtsUserSettings"
    ALTER COLUMN "GuildId" TYPE BIGINT,
    ALTER COLUMN "UserId" TYPE BIGINT;
ALTER TABLE "TtsVoiceChannelSettings"
    ALTER COLUMN "GuildId" TYPE BIGINT,
    ALTER COLUMN "LinkedTextChannelId" TYPE BIGINT,
    ALTER COLUMN "VoiceChannelId" TYPE BIGINT;
ALTER TABLE "TwitchAccountLinks"
    ALTER COLUMN "DiscordUserId" TYPE BIGINT,
    ALTER COLUMN "GuildId" TYPE BIGINT;
ALTER TABLE "TwitchChannelAuthorizations"
    ALTER COLUMN "AuthorizedByDiscordUserId" TYPE BIGINT,
    ALTER COLUMN "GuildId" TYPE BIGINT;
ALTER TABLE "TwitchCounters"
    ALTER COLUMN "GuildId" TYPE BIGINT;
ALTER TABLE "TwitchCustomCommands"
    ALTER COLUMN "GuildId" TYPE BIGINT;
ALTER TABLE "TwitchEventHistory"
    ALTER COLUMN "GuildId" TYPE BIGINT;
ALTER TABLE "TwitchEventSubSubscriptions"
    ALTER COLUMN "GuildId" TYPE BIGINT;
ALTER TABLE "TwitchGuildConfigs"
    ALTER COLUMN "AuthorizedByDiscordUserId" TYPE BIGINT,
    ALTER COLUMN "GoLiveChannelId" TYPE BIGINT,
    ALTER COLUMN "GuildId" TYPE BIGINT,
    ALTER COLUMN "RaidNotificationChannelId" TYPE BIGINT,
    ALTER COLUMN "StreamRecapChannelId" TYPE BIGINT,
    ALTER COLUMN "SubNotificationChannelId" TYPE BIGINT;
ALTER TABLE "TwitchLinkCodes"
    ALTER COLUMN "GuildId" TYPE BIGINT;
ALTER TABLE "TwitchQuotes"
    ALTER COLUMN "GuildId" TYPE BIGINT;
ALTER TABLE "TwitchRaidTargets"
    ALTER COLUMN "GuildId" TYPE BIGINT;
ALTER TABLE "TwitchRedemptionActions"
    ALTER COLUMN "DiscordChannelId" TYPE BIGINT,
    ALTER COLUMN "GuildId" TYPE BIGINT;
ALTER TABLE "TwitchRoleSyncMappings"
    ALTER COLUMN "GuildId" TYPE BIGINT,
    ALTER COLUMN "RoleId" TYPE BIGINT;
ALTER TABLE "TwitchTimers"
    ALTER COLUMN "GuildId" TYPE BIGINT;
ALTER TABLE "UnbanTimer"
    ALTER COLUMN "GuildId" TYPE BIGINT,
    ALTER COLUMN "UserId" TYPE BIGINT;
ALTER TABLE "UnmuteTimer"
    ALTER COLUMN "GuildId" TYPE BIGINT,
    ALTER COLUMN "UserId" TYPE BIGINT;
ALTER TABLE "UnroleTimer"
    ALTER COLUMN "GuildId" TYPE BIGINT,
    ALTER COLUMN "RoleId" TYPE BIGINT,
    ALTER COLUMN "UserId" TYPE BIGINT;
ALTER TABLE "UserCustomReputation"
    ALTER COLUMN "GuildId" TYPE BIGINT,
    ALTER COLUMN "UserId" TYPE BIGINT;
ALTER TABLE "UserInventoryItems"
    ALTER COLUMN "GuildId" TYPE BIGINT,
    ALTER COLUMN "UserId" TYPE BIGINT;
ALTER TABLE "UserReputation"
    ALTER COLUMN "GuildId" TYPE BIGINT,
    ALTER COLUMN "UserId" TYPE BIGINT;
ALTER TABLE "UserRoleStates"
    ALTER COLUMN "GuildId" TYPE BIGINT,
    ALTER COLUMN "UserId" TYPE BIGINT;
ALTER TABLE "UserVoicePreference"
    ALTER COLUMN "GuildId" TYPE BIGINT,
    ALTER COLUMN "UserId" TYPE BIGINT;
ALTER TABLE "UserXpStats"
    ALTER COLUMN "GuildId" TYPE BIGINT,
    ALTER COLUMN "UserId" TYPE BIGINT;
ALTER TABLE "VcRoles"
    ALTER COLUMN "GuildId" TYPE BIGINT,
    ALTER COLUMN "RoleId" TYPE BIGINT,
    ALTER COLUMN "VoiceChannelId" TYPE BIGINT;
ALTER TABLE "VoteRoles"
    ALTER COLUMN "GuildId" TYPE BIGINT,
    ALTER COLUMN "RoleId" TYPE BIGINT;
ALTER TABLE "Votes"
    ALTER COLUMN "BotId" TYPE BIGINT,
    ALTER COLUMN "GuildId" TYPE BIGINT,
    ALTER COLUMN "UserId" TYPE BIGINT;
ALTER TABLE "WarningPunishment"
    ALTER COLUMN "GuildId" TYPE BIGINT,
    ALTER COLUMN "RoleId" TYPE BIGINT;
ALTER TABLE "WarningPunishment2"
    ALTER COLUMN "GuildId" TYPE BIGINT,
    ALTER COLUMN "RoleId" TYPE BIGINT;
ALTER TABLE "Warnings"
    ALTER COLUMN "GuildId" TYPE BIGINT,
    ALTER COLUMN "UserId" TYPE BIGINT;
ALTER TABLE "Warnings2"
    ALTER COLUMN "GuildId" TYPE BIGINT,
    ALTER COLUMN "UserId" TYPE BIGINT;
ALTER TABLE "WhitelistedRoles"
    ALTER COLUMN "GuildId" TYPE BIGINT,
    ALTER COLUMN "RoleId" TYPE BIGINT;
ALTER TABLE "WhitelistedUsers"
    ALTER COLUMN "GuildId" TYPE BIGINT,
    ALTER COLUMN "UserId" TYPE BIGINT;
ALTER TABLE "XpBoostEvents"
    ALTER COLUMN "GuildId" TYPE BIGINT;
ALTER TABLE "XpChannelMultipliers"
    ALTER COLUMN "ChannelId" TYPE BIGINT,
    ALTER COLUMN "GuildId" TYPE BIGINT;
ALTER TABLE "XpCompetitionEntries"
    ALTER COLUMN "UserId" TYPE BIGINT;
ALTER TABLE "XpCompetitionRewards"
    ALTER COLUMN "RoleId" TYPE BIGINT;
ALTER TABLE "XpCompetitions"
    ALTER COLUMN "AnnouncementChannelId" TYPE BIGINT,
    ALTER COLUMN "GuildId" TYPE BIGINT;
ALTER TABLE "XpCurrencyRewards"
    ALTER COLUMN "GuildId" TYPE BIGINT;
ALTER TABLE "XpExcludedItems"
    ALTER COLUMN "GuildId" TYPE BIGINT,
    ALTER COLUMN "ItemId" TYPE BIGINT;
ALTER TABLE "XpLevelUpMessages"
    ALTER COLUMN "GuildId" TYPE BIGINT;
ALTER TABLE "XpRoleMultipliers"
    ALTER COLUMN "GuildId" TYPE BIGINT,
    ALTER COLUMN "RoleId" TYPE BIGINT;
ALTER TABLE "XpRoleRewards"
    ALTER COLUMN "GuildId" TYPE BIGINT,
    ALTER COLUMN "RoleId" TYPE BIGINT;
ALTER TABLE "XpRoleTracking"
    ALTER COLUMN "GuildId" TYPE BIGINT,
    ALTER COLUMN "RoleId" TYPE BIGINT;
ALTER TABLE "XpUserSnapshots"
    ALTER COLUMN "GuildId" TYPE BIGINT,
    ALTER COLUMN "UserId" TYPE BIGINT;
ALTER TABLE "antipostchannelsettings"
    ALTER COLUMN "statuschannelid" TYPE BIGINT,
    ALTER COLUMN "statusmessageid" TYPE BIGINT;

COMMIT;

-- Statistics on the rewritten tables are stale until this runs.
ANALYZE;
