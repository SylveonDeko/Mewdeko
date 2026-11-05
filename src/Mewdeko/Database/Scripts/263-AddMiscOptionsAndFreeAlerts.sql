-- Peak online tracking, a per-server embed colour, repeater failure reasons, chat trigger deny lists and anonymous
-- responses, delete-on-end stream messages, and the source URL free feed alerts follow.
-- Migration: 263-AddMiscOptionsAndFreeAlerts.sql

CREATE TABLE IF NOT EXISTS "GuildPeakOnline"
(
    "Id"        SERIAL PRIMARY KEY,
    "GuildId"   NUMERIC(20, 0) NOT NULL UNIQUE,
    "PeakCount" INTEGER        NOT NULL DEFAULT 0,
    "PeakAt"    TIMESTAMP      NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS "GuildEmbedColors"
(
    "Id"        SERIAL PRIMARY KEY,
    "GuildId"   NUMERIC(20, 0) NOT NULL UNIQUE,
    "OkColor"   INTEGER        NOT NULL,
    "DateAdded" TIMESTAMP      NOT NULL DEFAULT NOW()
);

ALTER TABLE "GuildRepeater"
    ADD COLUMN IF NOT EXISTS "DisabledReason" TEXT;

ALTER TABLE "ChatTriggers"
    ADD COLUMN IF NOT EXISTS "DeniedChannelIds" TEXT,
    ADD COLUMN IF NOT EXISTS "DeniedRoleIds"    TEXT,
    ADD COLUMN IF NOT EXISTS "Anonymous"        BOOLEAN NOT NULL DEFAULT FALSE;

ALTER TABLE "FollowedStreams"
    ADD COLUMN IF NOT EXISTS "DeleteOnEnd" BOOLEAN NOT NULL DEFAULT FALSE,
    ADD COLUMN IF NOT EXISTS "SourceUrl"   TEXT;
