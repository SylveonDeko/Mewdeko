-- Game and community options: scheduled economy boosts, shop item messages, giveaway extra entries, prizes and drafts,
-- and birthday announcement hour, age message and birth year rule. Everything is off until a server turns it on.
-- Migration: 261-AddGameAndCommunityOptions.sql

CREATE TABLE IF NOT EXISTS "EconomyBoosts"
(
    "Id"              SERIAL PRIMARY KEY,
    "GuildId"         NUMERIC(20, 0) NOT NULL,
    "Name"            TEXT           NOT NULL,
    "PercentChange"   INTEGER        NOT NULL,
    "Commands"        TEXT           NOT NULL DEFAULT '',
    "ScheduleType"    INTEGER        NOT NULL DEFAULT 0,
    "StartsAt"        TIMESTAMP      NOT NULL,
    "DurationMinutes" INTEGER        NOT NULL DEFAULT 60,
    "Enabled"         BOOLEAN        NOT NULL DEFAULT TRUE,
    "DateAdded"       TIMESTAMP      NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS "IX_EconomyBoosts_GuildId" ON "EconomyBoosts" ("GuildId");

ALTER TABLE "ShopItems"
    ADD COLUMN IF NOT EXISTS "BuyMessage" TEXT,
    ADD COLUMN IF NOT EXISTS "UseMessage" TEXT,
    ADD COLUMN IF NOT EXISTS "UseOnBuy" BOOLEAN NOT NULL DEFAULT FALSE;

ALTER TABLE "Giveaways"
    ADD COLUMN IF NOT EXISTS "ExtraEntryRoles" TEXT,
    ADD COLUMN IF NOT EXISTS "PrizeType" INTEGER NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS "PrizeAmount" BIGINT NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS "DraftDurationSeconds" INTEGER NOT NULL DEFAULT 0;

ALTER TABLE "BirthdayConfigs"
    ADD COLUMN IF NOT EXISTS "AnnouncementHour" INTEGER NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS "AgeMessage" TEXT,
    ADD COLUMN IF NOT EXISTS "BirthYearRule" INTEGER NOT NULL DEFAULT 1;
