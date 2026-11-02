-- Link fixing: replies to Twitter/X, Instagram, TikTok and similar links with a copy that goes through an embed fixing
-- service. Off until a server turns it on.
-- Migration: 226-AddLinkFixes.sql

CREATE TABLE IF NOT EXISTS "LinkFixSettings"
(
    "Id"                SERIAL PRIMARY KEY,
    "GuildId"           NUMERIC(20, 0) NOT NULL UNIQUE,
    "Enabled"           BOOLEAN        NOT NULL DEFAULT FALSE,
    "DisabledPlatforms" TEXT           NOT NULL DEFAULT '',
    "Hosts"             TEXT           NOT NULL DEFAULT '',
    "DateAdded"         TIMESTAMP      NOT NULL DEFAULT NOW()
);
