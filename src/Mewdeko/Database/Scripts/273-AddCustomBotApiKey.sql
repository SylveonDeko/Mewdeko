-- Each custom bot gets its own API key instead of the master's. The key is kept encrypted next to the bot's token.
-- Migration: 273-AddCustomBotApiKey.sql

ALTER TABLE "CustomBotSecrets"
    ADD COLUMN IF NOT EXISTS "ApiKeyEncrypted" TEXT NULL;
