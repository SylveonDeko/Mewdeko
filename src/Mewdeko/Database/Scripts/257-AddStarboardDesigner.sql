-- Starboard designer and extras: per-board post design, auto-react, role and age rules, repost cooldown and an enabled
-- toggle. Every column defaults to the old behaviour. Existing boards count every reaction, as they did before, while
-- new boards count each member once.
-- Migration: 257-AddStarboardDesigner.sql

ALTER TABLE "Starboards"
    ADD COLUMN IF NOT EXISTS "Enabled"                  BOOLEAN NOT NULL DEFAULT TRUE,
    ADD COLUMN IF NOT EXISTS "PostDesign"               TEXT,
    ADD COLUMN IF NOT EXISTS "AutoReact"                INTEGER NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS "CountMultipleReactions"   BOOLEAN NOT NULL DEFAULT TRUE,
    ADD COLUMN IF NOT EXISTS "AllowedRoles"             TEXT    NOT NULL DEFAULT '',
    ADD COLUMN IF NOT EXISTS "DeniedRoles"              TEXT    NOT NULL DEFAULT '',
    ADD COLUMN IF NOT EXISTS "MinMessageAgeMinutes"     INTEGER NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS "MaxMessageAgeMinutes"     INTEGER NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS "RepostCooldownMinutes"    INTEGER NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS "RemoveInvalidStars"       BOOLEAN NOT NULL DEFAULT FALSE;

ALTER TABLE "Starboards"
    ALTER COLUMN "CountMultipleReactions" SET DEFAULT FALSE;

ALTER TABLE "StarboardPosts"
    ADD COLUMN IF NOT EXISTS "LastPostedAt" TIMESTAMP;
