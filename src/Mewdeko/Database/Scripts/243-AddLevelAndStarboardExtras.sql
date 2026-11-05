-- Level-up announcement filters, a level-up log, and starboard self-star, NSFW and lock options.
-- Migration: 243-AddLevelAndStarboardExtras.sql

ALTER TABLE "GuildXpSettings"
    ADD COLUMN IF NOT EXISTS "LevelUpInterval" INTEGER NOT NULL DEFAULT 1;
ALTER TABLE "GuildXpSettings"
    ADD COLUMN IF NOT EXISTS "LevelUpMinLevel" INTEGER NOT NULL DEFAULT 0;
ALTER TABLE "GuildXpSettings"
    ADD COLUMN IF NOT EXISTS "LevelUpRewardsOnly" BOOLEAN NOT NULL DEFAULT FALSE;

CREATE TABLE IF NOT EXISTS "XpLevelUpLog"
(
    "Id"        SERIAL PRIMARY KEY,
    "GuildId"   NUMERIC(20, 0) NOT NULL,
    "UserId"    NUMERIC(20, 0) NOT NULL,
    "OldLevel"  INTEGER        NOT NULL,
    "NewLevel"  INTEGER        NOT NULL,
    "DateAdded" TIMESTAMP      NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS "IX_XpLevelUpLog_GuildId_Id" ON "XpLevelUpLog" ("GuildId", "Id");

ALTER TABLE "Starboards"
    ADD COLUMN IF NOT EXISTS "AllowSelfStar" BOOLEAN NOT NULL DEFAULT TRUE;
ALTER TABLE "Starboards"
    ADD COLUMN IF NOT EXISTS "AllowNsfw" BOOLEAN NOT NULL DEFAULT TRUE;
ALTER TABLE "Starboards"
    ADD COLUMN IF NOT EXISTS "LockedUntil" TIMESTAMP;
