-- More automod rules: emoji spam, spoiler spam, zalgo text, plus channel rules for image-only, video-only and commands-only
-- channels, a lockdown exemption list and a setting for logs to include bots. The new rules are off until a server turns
-- them on.
-- Migration: 256-AddAutomodMoreRules.sql

ALTER TABLE "AutomodRuleSettings"
    ADD COLUMN IF NOT EXISTS "MaxEmojis" INTEGER NOT NULL DEFAULT 10,
    ADD COLUMN IF NOT EXISTS "MaxSpoilers" INTEGER NOT NULL DEFAULT 3,
    ADD COLUMN IF NOT EXISTS "ZalgoThreshold" INTEGER NOT NULL DEFAULT 4;

CREATE TABLE IF NOT EXISTS "LockdownExemptions"
(
    "Id"        SERIAL PRIMARY KEY,
    "GuildId"   NUMERIC(20, 0) NOT NULL,
    "ChannelId" NUMERIC(20, 0) NOT NULL,
    "DateAdded" TIMESTAMP      NOT NULL DEFAULT NOW(),
    UNIQUE ("GuildId", "ChannelId")
);

ALTER TABLE "LoggingV2"
    ADD COLUMN IF NOT EXISTS "IgnoreBots" BOOLEAN NOT NULL DEFAULT TRUE;
