-- Member card designs: members make their own rank and achievement card design when a community allows it. A member
-- design belongs to its creator, is never sold or shared, and may carry one uploaded background kept in a separate pool
-- until a moderator approves it.
-- Migration: 269-AddMemberCardDesigns.sql

ALTER TABLE "CardDesigns"
    ADD COLUMN IF NOT EXISTS "OwnerUserId" NUMERIC(20, 0) NULL,
    ADD COLUMN IF NOT EXISTS "ReviewStatus" SMALLINT NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS "UploadId" INT NULL,
    ADD COLUMN IF NOT EXISTS "ReviewNote" TEXT NULL;

DROP INDEX IF EXISTS "UX_CardDesigns_GuildId_Name";
CREATE UNIQUE INDEX IF NOT EXISTS "UX_CardDesigns_Community_Name" ON "CardDesigns" ("GuildId", LOWER("Name")) WHERE "OwnerUserId" IS NULL;
CREATE UNIQUE INDEX IF NOT EXISTS "UX_CardDesigns_Member_Target" ON "CardDesigns" ("GuildId", "OwnerUserId", "Target") WHERE "OwnerUserId" IS NOT NULL;
CREATE INDEX IF NOT EXISTS "IX_CardDesigns_UploadId" ON "CardDesigns" ("UploadId") WHERE "UploadId" IS NOT NULL;

CREATE TABLE IF NOT EXISTS "CardDesignSettings"
(
    "GuildId"       NUMERIC(20, 0) PRIMARY KEY,
    "MemberEnabled" BOOLEAN        NOT NULL DEFAULT FALSE,
    "MemberWho"     SMALLINT       NOT NULL DEFAULT 0,
    "MemberRoleIds" TEXT           NOT NULL DEFAULT '',
    "MemberMinLevel" INT           NOT NULL DEFAULT 1,
    "MemberUploads" BOOLEAN        NOT NULL DEFAULT FALSE,
    "MemberReview"  BOOLEAN        NOT NULL DEFAULT TRUE,
    "MemberCost"    BIGINT         NOT NULL DEFAULT 0
);
