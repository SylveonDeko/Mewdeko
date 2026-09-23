-- Optional discussion thread created under each Word of the Day post.
-- Migration: 218-WordOfTheDayThreads.sql

ALTER TABLE "WordOfTheDayConfigs"
    ADD COLUMN IF NOT EXISTS "CreateThread"             BOOLEAN NOT NULL DEFAULT FALSE,
    ADD COLUMN IF NOT EXISTS "ThreadName"               TEXT    NULL,
    ADD COLUMN IF NOT EXISTS "ThreadAutoArchiveMinutes" INTEGER NOT NULL DEFAULT 1440;
