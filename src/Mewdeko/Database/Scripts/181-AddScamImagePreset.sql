-- Lets a guild block the known scam images that ship with the bot, without having to collect them
-- first. The hashes live in a data file rather than in this table, so enabling the preset is a flag
-- rather than 87 rows per guild.

ALTER TABLE "AntiImageHashSettings"
    ADD COLUMN IF NOT EXISTS "UsePresetList" BOOLEAN NOT NULL DEFAULT FALSE;

ALTER TABLE "AntiImageHashSettings"
    ADD COLUMN IF NOT EXISTS "PresetTriggers" INTEGER NOT NULL DEFAULT 0;
