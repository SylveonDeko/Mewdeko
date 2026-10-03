-- Achievements: per server settings, custom categories and achievements, overrides for built in
-- achievements, uploaded icons, unlocks, per member counters and badge slots, emoji use, and per user
-- preferences.
-- Migration: 220-Achievements.sql

CREATE TABLE IF NOT EXISTS "AchievementSettings"
(
    "Id"                 SERIAL PRIMARY KEY,
    "GuildId"            NUMERIC(20, 0)              NOT NULL,
    "Enabled"            BOOLEAN                     NOT NULL DEFAULT FALSE,
    "AnnounceMode"       INTEGER                     NOT NULL DEFAULT 0,
    "LogChannelId"       NUMERIC(20, 0)              NULL,
    "DmByDefault"        BOOLEAN                     NOT NULL DEFAULT FALSE,
    "MentionUsers"       BOOLEAN                     NOT NULL DEFAULT TRUE,
    "UnlockMessage"      TEXT                        NULL,
    "XpPerPoint"         INTEGER                     NOT NULL DEFAULT 0,
    "RevealHidden"       BOOLEAN                     NOT NULL DEFAULT FALSE,
    "UnlockImage"        BOOLEAN                     NOT NULL DEFAULT TRUE,
    "DeleteAfter"        INTEGER                     NOT NULL DEFAULT 0,
    "DefaultCardId"      INTEGER                     NULL,
    "CardAssignments"    TEXT                        NOT NULL DEFAULT '',
    "DisabledCategories" TEXT                        NOT NULL DEFAULT '',
    "CategoryOrder"      TEXT                        NOT NULL DEFAULT '',
    "ExcludedRoleIds"    TEXT                        NOT NULL DEFAULT '',
    "ExcludedChannelIds" TEXT                        NOT NULL DEFAULT '',
    "BackfilledAt"       TIMESTAMP WITHOUT TIME ZONE NULL,
    "DateAdded"          TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc')
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_AchievementSettings_GuildId"
    ON "AchievementSettings" ("GuildId");

CREATE TABLE IF NOT EXISTS "AchievementCategories"
(
    "Id"          SERIAL PRIMARY KEY,
    "GuildId"     NUMERIC(20, 0)              NOT NULL,
    "Name"        TEXT                        NOT NULL,
    "Description" TEXT                        NULL,
    "Icon"        TEXT                        NULL,
    "DateAdded"   TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc')
);

CREATE INDEX IF NOT EXISTS "IX_AchievementCategories_GuildId"
    ON "AchievementCategories" ("GuildId");

CREATE TABLE IF NOT EXISTS "AchievementIconUploads"
(
    "Id"          SERIAL PRIMARY KEY,
    "GuildId"     NUMERIC(20, 0)              NOT NULL,
    "Kind"        SMALLINT                    NOT NULL DEFAULT 0,
    "Data"        BYTEA                       NOT NULL,
    "PublicUrl"   TEXT                        NULL,
    "UploadedBy"  NUMERIC(20, 0)              NOT NULL DEFAULT 0,
    "DateAdded"   TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc')
);

CREATE INDEX IF NOT EXISTS "IX_AchievementIconUploads_GuildId"
    ON "AchievementIconUploads" ("GuildId");

CREATE TABLE IF NOT EXISTS "AchievementCardDesigns"
(
    "Id"          SERIAL PRIMARY KEY,
    "GuildId"     NUMERIC(20, 0)              NOT NULL,
    "Name"        TEXT                        NOT NULL,
    "Template"    TEXT                        NOT NULL,
    "DateAdded"   TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    "DateUpdated" TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc')
);

CREATE INDEX IF NOT EXISTS "IX_AchievementCardDesigns_GuildId"
    ON "AchievementCardDesigns" ("GuildId");

CREATE TABLE IF NOT EXISTS "CustomAchievements"
(
    "Id"             SERIAL PRIMARY KEY,
    "GuildId"        NUMERIC(20, 0)              NOT NULL,
    "CategoryKey"    TEXT                        NOT NULL DEFAULT 'custom',
    "Name"           TEXT                        NOT NULL,
    "Description"    TEXT                        NULL,
    "Icon"           TEXT                        NULL,
    "Grade"          INTEGER                     NOT NULL DEFAULT 0,
    "Points"         INTEGER                     NULL,
    "Hidden"         BOOLEAN                     NOT NULL DEFAULT FALSE,
    "Enabled"        BOOLEAN                     NOT NULL DEFAULT TRUE,
    "Position"       INTEGER                     NOT NULL DEFAULT 0,
    "TriggerType"    INTEGER                     NOT NULL DEFAULT 0,
    "Metric"         INTEGER                     NOT NULL DEFAULT 0,
    "Threshold"      BIGINT                      NOT NULL DEFAULT 0,
    "Keyword"        TEXT                        NULL,
    "ChannelId"      NUMERIC(20, 0)              NULL,
    "RoleRewardId"   NUMERIC(20, 0)              NULL,
    "CurrencyReward" BIGINT                      NOT NULL DEFAULT 0,
    "XpReward"       INTEGER                     NOT NULL DEFAULT 0,
    "CreatedBy"      NUMERIC(20, 0)              NOT NULL DEFAULT 0,
    "DateAdded"      TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    "DateModified"   TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc')
);

CREATE INDEX IF NOT EXISTS "IX_CustomAchievements_GuildId"
    ON "CustomAchievements" ("GuildId", "Position");

CREATE TABLE IF NOT EXISTS "AchievementOverrides"
(
    "Id"             SERIAL PRIMARY KEY,
    "GuildId"        NUMERIC(20, 0)              NOT NULL,
    "AchievementKey" TEXT                        NOT NULL,
    "Disabled"       BOOLEAN                     NOT NULL DEFAULT FALSE,
    "Name"           TEXT                        NULL,
    "Description"    TEXT                        NULL,
    "Icon"           TEXT                        NULL,
    "Points"         INTEGER                     NULL,
    "Hidden"         BOOLEAN                     NULL,
    "RoleRewardId"   NUMERIC(20, 0)              NULL,
    "CurrencyReward" BIGINT                      NOT NULL DEFAULT 0,
    "XpReward"       INTEGER                     NOT NULL DEFAULT 0,
    "DateModified"   TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc')
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_AchievementOverrides_GuildId_Key"
    ON "AchievementOverrides" ("GuildId", "AchievementKey");

CREATE TABLE IF NOT EXISTS "UserAchievements"
(
    "Id"             BIGSERIAL PRIMARY KEY,
    "GuildId"        NUMERIC(20, 0)              NOT NULL,
    "UserId"         NUMERIC(20, 0)              NOT NULL,
    "AchievementKey" TEXT                        NOT NULL,
    "Points"         INTEGER                     NOT NULL DEFAULT 0,
    "GrantedBy"      NUMERIC(20, 0)              NULL,
    "UnlockedAt"     TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc')
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_UserAchievements_Guild_User_Key"
    ON "UserAchievements" ("GuildId", "UserId", "AchievementKey");

CREATE INDEX IF NOT EXISTS "IX_UserAchievements_Guild_Key"
    ON "UserAchievements" ("GuildId", "AchievementKey");

CREATE INDEX IF NOT EXISTS "IX_UserAchievements_User"
    ON "UserAchievements" ("UserId");

CREATE TABLE IF NOT EXISTS "AchievementMembers"
(
    "GuildId"       NUMERIC(20, 0)              NOT NULL,
    "UserId"        NUMERIC(20, 0)              NOT NULL,
    "Points"        INTEGER                     NOT NULL DEFAULT 0,
    "UnlockedCount" INTEGER                     NOT NULL DEFAULT 0,
    "LastUnlockAt"  TIMESTAMP WITHOUT TIME ZONE NULL,
    "VoiceJoins"    BIGINT                      NOT NULL DEFAULT 0,
    "MutedSeconds"  BIGINT                      NOT NULL DEFAULT 0,
    "Badge1"        TEXT                        NULL,
    "Badge2"        TEXT                        NULL,
    "Badge3"        TEXT                        NULL,
    "Badge4"        TEXT                        NULL,
    "DateAdded"     TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    PRIMARY KEY ("GuildId", "UserId")
);

CREATE INDEX IF NOT EXISTS "IX_AchievementMembers_Guild_Points"
    ON "AchievementMembers" ("GuildId", "Points" DESC);

CREATE INDEX IF NOT EXISTS "IX_AchievementMembers_User"
    ON "AchievementMembers" ("UserId");

CREATE TABLE IF NOT EXISTS "AchievementEmojiUses"
(
    "GuildId" NUMERIC(20, 0) NOT NULL,
    "UserId"  NUMERIC(20, 0) NOT NULL,
    "Emoji"   TEXT           NOT NULL,
    "Count"   INTEGER        NOT NULL DEFAULT 0,
    PRIMARY KEY ("GuildId", "UserId", "Emoji")
);

CREATE TABLE IF NOT EXISTS "AchievementUserSettings"
(
    "UserId"                 NUMERIC(20, 0)              NOT NULL PRIMARY KEY,
    "ProfileVisibility"      INTEGER                     NOT NULL DEFAULT 0,
    "AchievementsVisibility" INTEGER                     NOT NULL DEFAULT 0,
    "BadgesVisibility"       INTEGER                     NOT NULL DEFAULT 0,
    "HideFromLeaderboards"   BOOLEAN                     NOT NULL DEFAULT FALSE,
    "DmUnlocks"              INTEGER                     NOT NULL DEFAULT 0,
    "ShowInLog"              BOOLEAN                     NOT NULL DEFAULT TRUE,
    "MentionMe"              BOOLEAN                     NOT NULL DEFAULT TRUE,
    "DateAdded"              TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc')
);
