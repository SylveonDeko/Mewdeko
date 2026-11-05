-- Per-stream options for TikTok video notifications and live messages that are edited in place while a stream runs.
-- Migration: 241-AddTikTokAndLiveMessages.sql

ALTER TABLE "FollowedStreams"
    ADD COLUMN IF NOT EXISTS "VideoMessage" TEXT;

ALTER TABLE "FollowedStreams"
    ADD COLUMN IF NOT EXISTS "UpdateLiveMessage" BOOLEAN NOT NULL DEFAULT FALSE;
