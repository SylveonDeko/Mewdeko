-- Community extras: starboard autostar channels, per-user rank card backgrounds, suggestion vote colours, role pings on
-- repeaters, the daily dice game and free game alerts. Everything is off until a server turns it on.
-- Migration: 254-AddCommunityExtras.sql

ALTER TABLE "Starboards"
    ADD COLUMN IF NOT EXISTS "AutoStarChannels" TEXT NOT NULL DEFAULT '';

ALTER TABLE "GuildRepeater"
    ADD COLUMN IF NOT EXISTS "PingRoleId" NUMERIC(20, 0);

CREATE TABLE IF NOT EXISTS "UserXpBackgrounds"
(
    "Id"        SERIAL PRIMARY KEY,
    "UserId"    NUMERIC(20, 0) NOT NULL UNIQUE,
    "Image"     TEXT           NOT NULL,
    "DateAdded" TIMESTAMP      NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS "SuggestionVoteColors"
(
    "Id"        SERIAL PRIMARY KEY,
    "GuildId"   NUMERIC(20, 0) NOT NULL UNIQUE,
    "Threshold" INTEGER        NOT NULL DEFAULT 0,
    "DateAdded" TIMESTAMP      NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS "DailyDiceRolls"
(
    "Id"       SERIAL PRIMARY KEY,
    "GuildId"  NUMERIC(20, 0) NOT NULL,
    "UserId"   NUMERIC(20, 0) NOT NULL,
    "RollDate" DATE           NOT NULL,
    "Roll"     INTEGER        NOT NULL,
    "RolledAt" TIMESTAMP      NOT NULL DEFAULT NOW(),
    UNIQUE ("GuildId", "UserId", "RollDate")
);

CREATE INDEX IF NOT EXISTS "IX_DailyDiceRolls_GuildId_RollDate" ON "DailyDiceRolls" ("GuildId", "RollDate");

CREATE TABLE IF NOT EXISTS "FreeGameAlerts"
(
    "Id"        SERIAL PRIMARY KEY,
    "GuildId"   NUMERIC(20, 0) NOT NULL,
    "ChannelId" NUMERIC(20, 0) NOT NULL,
    "RoleId"    NUMERIC(20, 0),
    "Epic"      BOOLEAN        NOT NULL DEFAULT TRUE,
    "Steam"     BOOLEAN        NOT NULL DEFAULT TRUE,
    "Gog"       BOOLEAN        NOT NULL DEFAULT TRUE,
    "DateAdded" TIMESTAMP      NOT NULL DEFAULT NOW(),
    UNIQUE ("GuildId", "ChannelId")
);

CREATE TABLE IF NOT EXISTS "FreeGameAnnounced"
(
    "Id"        SERIAL PRIMARY KEY,
    "Store"     TEXT      NOT NULL,
    "GameKey"   TEXT      NOT NULL,
    "DateAdded" TIMESTAMP NOT NULL DEFAULT NOW(),
    UNIQUE ("Store", "GameKey")
);
