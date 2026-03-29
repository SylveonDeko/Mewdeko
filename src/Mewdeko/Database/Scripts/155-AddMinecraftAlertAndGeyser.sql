ALTER TABLE "MinecraftServers"
    ADD COLUMN IF NOT EXISTS "CustomOnlineMessage"  TEXT NULL,
    ADD COLUMN IF NOT EXISTS "CustomOfflineMessage" TEXT NULL;
