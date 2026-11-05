-- Sticky templates and command reply redirects.
-- Migration: 253-AddUtilityExtras.sql

CREATE TABLE IF NOT EXISTS "StickyTemplates"
(
    "Id"        SERIAL PRIMARY KEY,
    "GuildId"   NUMERIC(20, 0) NOT NULL,
    "Name"      TEXT           NOT NULL,
    "Content"   TEXT           NOT NULL,
    "DateAdded" TIMESTAMP      NOT NULL DEFAULT NOW()
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_StickyTemplates_GuildId_Name" ON "StickyTemplates" ("GuildId", LOWER("Name"));

CREATE TABLE IF NOT EXISTS "CommandReplyRedirects"
(
    "Id"            SERIAL PRIMARY KEY,
    "GuildId"       NUMERIC(20, 0) NOT NULL,
    "Target"        TEXT           NOT NULL,
    "IsModule"      BOOLEAN        NOT NULL DEFAULT FALSE,
    "ChannelId"     NUMERIC(20, 0) NOT NULL,
    "DeleteInvoker" BOOLEAN        NOT NULL DEFAULT FALSE,
    "DateAdded"     TIMESTAMP      NOT NULL DEFAULT NOW(),
    UNIQUE ("GuildId", "IsModule", "Target")
);
