-- MAINTENANCE WINDOW SCRIPT. Not part of the DbUp lanes; run by hand and confirm before starting.
--
-- The database was created under glibc 2.35 and the host now provides 2.39. Collation changes between
-- those versions alter text sort order, which means every text btree index may be ordered against rules
-- the server no longer follows. Such an index can silently return wrong results: an equality or range
-- lookup that walks the tree can miss rows that are physically present.
--
-- The 56 affected indexes total only about 21 MB, so this is minutes of work, and REINDEX CONCURRENTLY
-- holds no lock that blocks reads or writes. Run every REINDEX before the REFRESH at the bottom, because
-- the refresh only records that the mismatch is resolved; it fixes nothing on its own.
--
-- REINDEX CONCURRENTLY cannot run inside a transaction block. Run with psql directly, not with -1.
-- A failed REINDEX CONCURRENTLY leaves an invalid index named with a _ccnew suffix; find them with
--   SELECT c.relname FROM pg_index i JOIN pg_class c ON c.oid = i.indexrelid WHERE NOT i.indisvalid;
-- and drop them before retrying.

\set ON_ERROR_STOP on

REINDEX INDEX CONCURRENTLY public."BannedImageHashes_GuildId_Hash_key";
REINDEX INDEX CONCURRENTLY public."CurrencyCooldowns_GuildId_UserId_CooldownKey_key";
REINDEX INDEX CONCURRENTLY public."DashboardAccessSection_DashboardAccessId_Section_key";
REINDEX INDEX CONCURRENTLY public."IX_BanPruneSettings_Scope";
REINDEX INDEX CONCURRENTLY public."IX_ChatTriggerCounters_Guild_Name_User";
REINDEX INDEX CONCURRENTLY public."IX_ChatTriggers_Guild_Category";
REINDEX INDEX CONCURRENTLY public."IX_CommandAlias_GuildId_Trigger";
REINDEX INDEX CONCURRENTLY public."IX_CurrencyCooldowns_GuildId_CooldownKey";
REINDEX INDEX CONCURRENTLY public."IX_DiscordPermOverrides_GuildId_Command";
REINDEX INDEX CONCURRENTLY public."IX_EmbedWebhookPersonas_Guild_Name";
REINDEX INDEX CONCURRENTLY public."IX_EmbedWebhookPersonas_User_Name";
REINDEX INDEX CONCURRENTLY public."IX_Embeds_EmbedName_GuildId_IsGuildShared";
REINDEX INDEX CONCURRENTLY public."IX_Embeds_EmbedName_UserId_GuildId";
REINDEX INDEX CONCURRENTLY public."IX_FeedSub_GuildId_Url";
REINDEX INDEX CONCURRENTLY public."IX_MinecraftServers_GuildId_Name";
REINDEX INDEX CONCURRENTLY public."IX_MusicPlaylists_GuildId_Name";
REINDEX INDEX CONCURRENTLY public."IX_PatreonSupporters_GuildId_PatronStatus";
REINDEX INDEX CONCURRENTLY public."IX_Quotes_Keyword";
REINDEX INDEX CONCURRENTLY public."IX_RepCommandRequirements_CommandName";
REINDEX INDEX CONCURRENTLY public."IX_StarboardReactions_MessageId_UserId_Emote";
REINDEX INDEX CONCURRENTLY public."IX_StarboardStats_MessageId_StarboardId_Emote";
REINDEX INDEX CONCURRENTLY public."IX_TicketActionLogs_Action";
REINDEX INDEX CONCURRENTLY public."IX_TodoLists_Name_Unique";
REINDEX INDEX CONCURRENTLY public."IX_TransactionHistory_GuildId_Category_DateAdded";
REINDEX INDEX CONCURRENTLY public."IX_TransactionHistory_GuildId_Source_DateAdded";
REINDEX INDEX CONCURRENTLY public."IX_TwitchAccountLinks_GuildId_TwitchUsername";
REINDEX INDEX CONCURRENTLY public."IX_TwitchCounters_GuildId_Name";
REINDEX INDEX CONCURRENTLY public."IX_TwitchCustomCommands_GuildId_Name";
REINDEX INDEX CONCURRENTLY public."IX_TwitchEventSubSubscriptions_CallbackUrl";
REINDEX INDEX CONCURRENTLY public."IX_TwitchEventSubSubscriptions_GuildId_Type";
REINDEX INDEX CONCURRENTLY public."IX_TwitchLinkCodes_GuildId_Code";
REINDEX INDEX CONCURRENTLY public."IX_TwitchRedemptionActions_GuildId_RewardTitle";
REINDEX INDEX CONCURRENTLY public."IX_TwitchTimers_GuildId_Name";
REINDEX INDEX CONCURRENTLY public."PK___EFMigrationsHistory";
REINDEX INDEX CONCURRENTLY public."PatreonGoals_GuildId_GoalId_key";
REINDEX INDEX CONCURRENTLY public."PatreonSupporters_GuildId_PatreonUserId_key";
REINDEX INDEX CONCURRENTLY public."RepBadges_UserId_GuildId_BadgeType_key";
REINDEX INDEX CONCURRENTLY public."RepCommandRequirements_GuildId_CommandName_key";
REINDEX INDEX CONCURRENTLY public."TwitchAccountLinks_GuildId_TwitchUsername_key";
REINDEX INDEX CONCURRENTLY public."TwitchBotAccounts_TwitchUserId_key";
REINDEX INDEX CONCURRENTLY public."TwitchCounters_GuildId_Name_key";
REINDEX INDEX CONCURRENTLY public."TwitchCustomCommands_GuildId_Name_key";
REINDEX INDEX CONCURRENTLY public."TwitchEventSubSubscriptions_TwitchSubscriptionId_key";
REINDEX INDEX CONCURRENTLY public."TwitchLinkCodes_Code_key";
REINDEX INDEX CONCURRENTLY public."TwitchRaidTargets_GuildId_TwitchLogin_key";
REINDEX INDEX CONCURRENTLY public."TwitchRedemptionActions_GuildId_RewardTitle_key";
REINDEX INDEX CONCURRENTLY public."TwitchTimers_GuildId_Name_key";
REINDEX INDEX CONCURRENTLY public."UX_ShopItems_GuildId_Name";
REINDEX INDEX CONCURRENTLY public."form_response_workflows_status_check_token_unique";
REINDEX INDEX CONCURRENTLY public."form_share_links_share_code_key";
REINDEX INDEX CONCURRENTLY public."idx_coprmonitors_project";
REINDEX INDEX CONCURRENTLY public."idx_form_response_workflows_status_check_token";
REINDEX INDEX CONCURRENTLY public."idx_form_share_links_share_code";
REINDEX INDEX CONCURRENTLY public."idx_repbadges_type";
REINDEX INDEX CONCURRENTLY public."idx_repcustomtype_guild_type";
REINDEX INDEX CONCURRENTLY public."unique_copr_monitor";

-- Only after every index above has been rebuilt.
ALTER DATABASE mewdeko REFRESH COLLATION VERSION;
