-- Channel tools: timed channel locks and hides, voice and text channel links, and scheduled message purges.
-- Migration: 240-AddChannelTools.sql

CREATE TABLE IF NOT EXISTS "ChannelTimedActions"
(
    "Id"        SERIAL PRIMARY KEY,
    "GuildId"   NUMERIC(20, 0) NOT NULL,
    "ChannelId" NUMERIC(20, 0) NOT NULL,
    "Kind"      INTEGER        NOT NULL DEFAULT 0,
    "Previous"  TEXT           NOT NULL DEFAULT '[]',
    "EndsAt"    TIMESTAMP,
    "CreatedBy" NUMERIC(20, 0) NOT NULL DEFAULT 0,
    "DateAdded" TIMESTAMP      NOT NULL DEFAULT NOW(),
    UNIQUE ("ChannelId", "Kind")
);

CREATE INDEX IF NOT EXISTS "IX_ChannelTimedActions_GuildId" ON "ChannelTimedActions" ("GuildId");
CREATE INDEX IF NOT EXISTS "IX_ChannelTimedActions_EndsAt" ON "ChannelTimedActions" ("EndsAt");

CREATE TABLE IF NOT EXISTS "VoiceTextLinks"
(
    "Id"             SERIAL PRIMARY KEY,
    "GuildId"        NUMERIC(20, 0) NOT NULL,
    "VoiceChannelId" NUMERIC(20, 0) NOT NULL UNIQUE,
    "TextChannelId"  NUMERIC(20, 0) NOT NULL,
    "DateAdded"      TIMESTAMP      NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS "IX_VoiceTextLinks_GuildId" ON "VoiceTextLinks" ("GuildId");

CREATE TABLE IF NOT EXISTS "AutoPurgeSettings"
(
    "Id"             SERIAL PRIMARY KEY,
    "GuildId"        NUMERIC(20, 0) NOT NULL,
    "ChannelId"      NUMERIC(20, 0) NOT NULL UNIQUE,
    "MaxAgeMinutes"  INTEGER        NOT NULL DEFAULT 1440,
    "IntervalMinutes" INTEGER       NOT NULL DEFAULT 10,
    "Filter"         INTEGER        NOT NULL DEFAULT 0,
    "KeepPinned"     BOOLEAN        NOT NULL DEFAULT TRUE,
    "LastRunAt"      TIMESTAMP,
    "DateAdded"      TIMESTAMP      NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS "IX_AutoPurgeSettings_GuildId" ON "AutoPurgeSettings" ("GuildId");
