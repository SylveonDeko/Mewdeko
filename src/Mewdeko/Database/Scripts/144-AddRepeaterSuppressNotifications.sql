-- Add SuppressNotifications column to GuildRepeater table for notification suppression support
ALTER TABLE IF EXISTS "GuildRepeater"
    ADD COLUMN IF NOT EXISTS "SuppressNotifications" BOOLEAN NOT NULL DEFAULT FALSE;
