ALTER TABLE "MinecraftServers"
    ADD COLUMN IF NOT EXISTS "ChatChannelId"        NUMERIC(20, 0) NULL,
    ADD COLUMN IF NOT EXISTS "JoinLeaveChannelId"   NUMERIC(20, 0) NULL,
    ADD COLUMN IF NOT EXISTS "DeathChannelId"       NUMERIC(20, 0) NULL,
    ADD COLUMN IF NOT EXISTS "AdvancementChannelId" NUMERIC(20, 0) NULL;
