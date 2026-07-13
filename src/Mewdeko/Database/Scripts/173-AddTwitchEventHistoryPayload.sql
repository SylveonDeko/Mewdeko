ALTER TABLE "TwitchEventHistory"
    ADD COLUMN IF NOT EXISTS "RawPayload" TEXT NULL;
