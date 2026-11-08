-- AFK replies can carry a button that sends whoever presses it a DM when the away member returns.
-- Migration: 271-AddAfkNotifyButton.sql

ALTER TABLE "GuildConfigs" ADD COLUMN IF NOT EXISTS "AfkNotifyButton" BOOLEAN NOT NULL DEFAULT FALSE;
