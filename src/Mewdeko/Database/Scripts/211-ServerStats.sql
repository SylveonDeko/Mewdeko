-- Server activity stats: voice time segments with a 90 day window plus all time totals, hourly guild snapshots
-- (member and status counts), per guild tracking settings and exclusions, and a global per user opt out that
-- scrubs and stops message and voice tracking for that user everywhere.
-- Migration: 211-ServerStats.sql

CREATE TABLE IF NOT EXISTS "VoiceSegments"
(
    "Id"        BIGSERIAL PRIMARY KEY,
    "GuildId"   NUMERIC(20, 0)              NOT NULL,
    "ChannelId" NUMERIC(20, 0)              NOT NULL,
    "UserId"    NUMERIC(20, 0)              NOT NULL,
    "StartedAt" TIMESTAMP WITHOUT TIME ZONE NOT NULL,
    "EndedAt"   TIMESTAMP WITHOUT TIME ZONE NOT NULL,
    "Seconds"   INTEGER                     NOT NULL,
    "State"     INTEGER                     NOT NULL DEFAULT 0
);

CREATE INDEX IF NOT EXISTS "IX_VoiceSegments_Guild_EndedAt"
    ON "VoiceSegments" ("GuildId", "EndedAt");

CREATE INDEX IF NOT EXISTS "IX_VoiceSegments_Guild_User_EndedAt"
    ON "VoiceSegments" ("GuildId", "UserId", "EndedAt");

CREATE INDEX IF NOT EXISTS "IX_VoiceSegments_EndedAt"
    ON "VoiceSegments" ("EndedAt");

CREATE TABLE IF NOT EXISTS "VoiceTotals"
(
    "Id"        SERIAL PRIMARY KEY,
    "GuildId"   NUMERIC(20, 0)              NOT NULL,
    "ChannelId" NUMERIC(20, 0)              NOT NULL,
    "UserId"    NUMERIC(20, 0)              NOT NULL,
    "Seconds"   BIGINT                      NOT NULL DEFAULT 0,
    "DateAdded" TIMESTAMP WITHOUT TIME ZONE NULL,
    CONSTRAINT "UQ_VoiceTotals_Guild_Channel_User" UNIQUE ("GuildId", "ChannelId", "UserId")
);

CREATE INDEX IF NOT EXISTS "IX_VoiceTotals_Guild_User"
    ON "VoiceTotals" ("GuildId", "UserId");

CREATE TABLE IF NOT EXISTS "GuildSnapshots"
(
    "Id"        BIGSERIAL PRIMARY KEY,
    "GuildId"   NUMERIC(20, 0)              NOT NULL,
    "Timestamp" TIMESTAMP WITHOUT TIME ZONE NOT NULL,
    "Members"   INTEGER                     NOT NULL DEFAULT 0,
    "Humans"    INTEGER                     NOT NULL DEFAULT 0,
    "Bots"      INTEGER                     NOT NULL DEFAULT 0,
    "Online"    INTEGER                     NOT NULL DEFAULT 0,
    "Idle"      INTEGER                     NOT NULL DEFAULT 0,
    "Dnd"       INTEGER                     NOT NULL DEFAULT 0,
    "Offline"   INTEGER                     NOT NULL DEFAULT 0,
    "InVoice"   INTEGER                     NOT NULL DEFAULT 0
);

CREATE INDEX IF NOT EXISTS "IX_GuildSnapshots_Guild_Timestamp"
    ON "GuildSnapshots" ("GuildId", "Timestamp");

CREATE INDEX IF NOT EXISTS "IX_GuildSnapshots_Timestamp"
    ON "GuildSnapshots" ("Timestamp");

CREATE TABLE IF NOT EXISTS "ServerStatsSettings"
(
    "Id"                     SERIAL PRIMARY KEY,
    "GuildId"                NUMERIC(20, 0)              NOT NULL,
    "TrackVoice"             BOOLEAN                     NOT NULL DEFAULT TRUE,
    "TrackSnapshots"         BOOLEAN                     NOT NULL DEFAULT TRUE,
    "MessageCooldownSeconds" INTEGER                     NOT NULL DEFAULT 0,
    "DefaultLookbackDays"    INTEGER                     NOT NULL DEFAULT 14,
    "CountBots"              BOOLEAN                     NOT NULL DEFAULT FALSE,
    "VoiceStates"            INTEGER                     NOT NULL DEFAULT 0,
    "DateAdded"              TIMESTAMP WITHOUT TIME ZONE NULL,
    CONSTRAINT "UQ_ServerStatsSettings_GuildId" UNIQUE ("GuildId")
);

CREATE TABLE IF NOT EXISTS "ServerStatsExclusions"
(
    "Id"        SERIAL PRIMARY KEY,
    "GuildId"   NUMERIC(20, 0)              NOT NULL,
    "TargetId"  NUMERIC(20, 0)              NOT NULL,
    "Kind"      INTEGER                     NOT NULL,
    "DateAdded" TIMESTAMP WITHOUT TIME ZONE NULL,
    CONSTRAINT "UQ_ServerStatsExclusions_Guild_Target_Kind" UNIQUE ("GuildId", "TargetId", "Kind")
);

CREATE INDEX IF NOT EXISTS "IX_ServerStatsExclusions_GuildId"
    ON "ServerStatsExclusions" ("GuildId");

CREATE TABLE IF NOT EXISTS "StatsPrivacyOptOuts"
(
    "Id"        SERIAL PRIMARY KEY,
    "UserId"    NUMERIC(20, 0)              NOT NULL,
    "DateAdded" TIMESTAMP WITHOUT TIME ZONE NULL,
    CONSTRAINT "UQ_StatsPrivacyOptOuts_UserId" UNIQUE ("UserId")
);

-- Analytics over join and leave events need a time bounded scan per guild.
CREATE INDEX IF NOT EXISTS "IX_JoinLeaveLogs_Guild_DateAdded"
    ON "JoinLeaveLogs" ("GuildId", "DateAdded");
