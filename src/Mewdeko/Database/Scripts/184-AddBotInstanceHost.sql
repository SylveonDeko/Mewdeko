-- Hostname other services use to reach a registered instance's API. Instance URLs
-- were previously hardcoded to localhost, which breaks whenever the dashboard and
-- the bot are not on the same host (for example separate Docker containers).
-- Existing rows keep the old behaviour via the default.
-- Migration: 184-AddBotInstanceHost.sql

ALTER TABLE "BotInstances"
    ADD COLUMN IF NOT EXISTS "Host" TEXT NOT NULL DEFAULT 'localhost';
