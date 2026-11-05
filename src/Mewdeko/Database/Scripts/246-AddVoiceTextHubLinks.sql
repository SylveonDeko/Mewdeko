-- Voice text hub links: temporary voice channels from a join-to-create hub share the hub's text link, and can optionally
-- get their own temporary text channel. Off until a server turns it on.
-- Migration: 246-AddVoiceTextHubLinks.sql

ALTER TABLE "CustomVoiceConfig"
    ADD COLUMN IF NOT EXISTS "CreateTextChannel" BOOLEAN NOT NULL DEFAULT FALSE;

ALTER TABLE "CustomVoiceChannel"
    ADD COLUMN IF NOT EXISTS "TextChannelId" NUMERIC(20, 0);
