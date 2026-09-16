-- Invite tracking overhaul: per-inviter breakdown (regular/left/fake/bonus), join metadata on InvitedBy so
-- retention, rejoins and join sources can be computed, fake detection settings, inviter blacklists, hidden
-- leaderboard users, and labelled invite codes with optional auto roles and owners.
-- Migration: 210-InviteTrackingOverhaul.sql

ALTER TABLE "InviteCounts"
    ADD COLUMN IF NOT EXISTS "Regular" INTEGER NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS "Left"    INTEGER NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS "Fake"    INTEGER NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS "Bonus"   INTEGER NOT NULL DEFAULT 0;

-- Existing rows only ever tracked a net total, so seed the regular column from it.
UPDATE "InviteCounts"
SET "Regular" = GREATEST("Count", 0)
WHERE "Regular" = 0
  AND "Count" > 0;

CREATE INDEX IF NOT EXISTS "IX_InviteCounts_Guild_User"
    ON "InviteCounts" ("GuildId", "UserId");

ALTER TABLE "InvitedBy"
    ADD COLUMN IF NOT EXISTS "InviteCode" TEXT                        NULL,
    ADD COLUMN IF NOT EXISTS "JoinType"   INTEGER                     NOT NULL DEFAULT 1,
    ADD COLUMN IF NOT EXISTS "IsFake"     BOOLEAN                     NOT NULL DEFAULT FALSE,
    ADD COLUMN IF NOT EXISTS "FakeReason" INTEGER                     NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS "LeftAt"     TIMESTAMP WITHOUT TIME ZONE NULL;

CREATE INDEX IF NOT EXISTS "IX_InvitedBy_Guild_Inviter"
    ON "InvitedBy" ("GuildId", "InviterId");

CREATE INDEX IF NOT EXISTS "IX_InvitedBy_Guild_User"
    ON "InvitedBy" ("GuildId", "UserId");

CREATE INDEX IF NOT EXISTS "IX_InvitedBy_Guild_DateAdded"
    ON "InvitedBy" ("GuildId", "DateAdded");

ALTER TABLE "InviteCountSettings"
    ADD COLUMN IF NOT EXISTS "CountRejoins"   BOOLEAN        NOT NULL DEFAULT TRUE,
    ADD COLUMN IF NOT EXISTS "FakeOnNoAvatar" BOOLEAN        NOT NULL DEFAULT FALSE,
    ADD COLUMN IF NOT EXISTS "LinkChannelId"  NUMERIC(20, 0) NULL,
    ADD COLUMN IF NOT EXISTS "LogChannelId"   NUMERIC(20, 0) NULL;

CREATE TABLE IF NOT EXISTS "InviteTrackingExclusions"
(
    "Id"        SERIAL PRIMARY KEY,
    "GuildId"   NUMERIC(20, 0)              NOT NULL,
    "TargetId"  NUMERIC(20, 0)              NOT NULL,
    "Kind"      INTEGER                     NOT NULL,
    "DateAdded" TIMESTAMP WITHOUT TIME ZONE NULL,
    CONSTRAINT "UQ_InviteTrackingExclusions_Guild_Target_Kind" UNIQUE ("GuildId", "TargetId", "Kind")
);

CREATE INDEX IF NOT EXISTS "IX_InviteTrackingExclusions_GuildId"
    ON "InviteTrackingExclusions" ("GuildId");

CREATE TABLE IF NOT EXISTS "InviteLabels"
(
    "Id"          SERIAL PRIMARY KEY,
    "GuildId"     NUMERIC(20, 0)              NOT NULL,
    "InviteCode"  TEXT                        NOT NULL,
    "Label"       TEXT                        NOT NULL,
    "RoleId"      NUMERIC(20, 0)              NULL,
    "OwnerUserId" NUMERIC(20, 0)              NULL,
    "DateAdded"   TIMESTAMP WITHOUT TIME ZONE NULL,
    CONSTRAINT "UQ_InviteLabels_Guild_Code" UNIQUE ("GuildId", "InviteCode")
);

CREATE INDEX IF NOT EXISTS "IX_InviteLabels_GuildId"
    ON "InviteLabels" ("GuildId");
