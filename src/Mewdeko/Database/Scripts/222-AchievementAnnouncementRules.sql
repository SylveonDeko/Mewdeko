-- Achievements: channels where unlocks are earned but never announced, a rule that keeps announcements out of
-- channels the member cannot send messages in, and unlock messages that delete themselves after 5 seconds
-- unless the server chose otherwise. Servers still on 0 had never changed the setting.
-- Migration: 222-AchievementAnnouncementRules.sql

ALTER TABLE "AchievementSettings"
    ADD COLUMN IF NOT EXISTS "QuietChannelIds"       TEXT    NOT NULL DEFAULT '',
    ADD COLUMN IF NOT EXISTS "RequireSendPermission" BOOLEAN NOT NULL DEFAULT TRUE;

ALTER TABLE "AchievementSettings"
    ALTER COLUMN "DeleteAfter" SET DEFAULT 5;

UPDATE "AchievementSettings"
SET "DeleteAfter" = 5
WHERE "DeleteAfter" = 0;
