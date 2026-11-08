-- Members can leave messages for someone who is AFK, read when they return.
-- Migration: 272-AddAfkInbox.sql

ALTER TABLE "GuildConfigs" ADD COLUMN IF NOT EXISTS "AfkMessages" BOOLEAN NOT NULL DEFAULT FALSE;

ALTER TABLE "DiscordUser" ADD COLUMN IF NOT EXISTS "AfkMessagesOptOut" BOOLEAN NOT NULL DEFAULT FALSE;

CREATE TABLE IF NOT EXISTS "AfkInbox"
(
    "Id"          SERIAL PRIMARY KEY,
    "GuildId"     NUMERIC(20, 0) NOT NULL,
    "RecipientId" NUMERIC(20, 0) NOT NULL,
    "SenderId"    NUMERIC(20, 0) NOT NULL,
    "Content"     TEXT           NOT NULL,
    "DateAdded"   TIMESTAMP      NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc')
);

CREATE INDEX IF NOT EXISTS "IX_AfkInbox_GuildId_RecipientId" ON "AfkInbox" ("GuildId", "RecipientId");

CREATE TABLE IF NOT EXISTS "AfkInboxBlocks"
(
    "UserId"    NUMERIC(20, 0) NOT NULL,
    "BlockedId" NUMERIC(20, 0) NOT NULL,
    PRIMARY KEY ("UserId", "BlockedId")
);
