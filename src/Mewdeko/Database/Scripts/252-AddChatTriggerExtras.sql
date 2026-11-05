-- Chat trigger extras: staff and NSFW gates, an allowed channel list, a fixed reply channel, the member who created the
-- trigger, a prompt asked before responding, and the guild switch that lets members create triggers.
-- Migration: 252-AddChatTriggerExtras.sql

ALTER TABLE "ChatTriggers"
    ADD COLUMN IF NOT EXISTS "StaffOnly"            BOOLEAN        NOT NULL DEFAULT FALSE,
    ADD COLUMN IF NOT EXISTS "NsfwOnly"             BOOLEAN        NOT NULL DEFAULT FALSE,
    ADD COLUMN IF NOT EXISTS "AllowedChannelIds"    TEXT           NULL,
    ADD COLUMN IF NOT EXISTS "ReplyChannelId"       NUMERIC(20, 0) NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS "CreatorId"            NUMERIC(20, 0) NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS "PromptQuestion"       TEXT           NULL,
    ADD COLUMN IF NOT EXISTS "PromptTimeoutSeconds" INTEGER        NOT NULL DEFAULT 30;

ALTER TABLE "GuildConfigs"
    ADD COLUMN IF NOT EXISTS "ChatTriggerMemberCreate" BOOLEAN NOT NULL DEFAULT FALSE;
