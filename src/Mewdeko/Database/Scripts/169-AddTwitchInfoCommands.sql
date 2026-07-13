ALTER TABLE "TwitchGuildConfigs"
    ADD COLUMN IF NOT EXISTS "ScheduleMessage" TEXT NULL,
    ADD COLUMN IF NOT EXISTS "SocialsMessage"  TEXT NULL;
