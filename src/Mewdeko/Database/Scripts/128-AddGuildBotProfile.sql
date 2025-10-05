-- Migration: Add GuildBotProfiles table
-- Date: 2025-10-04
-- Description: Stores bot's guild-specific profile customizations (avatar, banner, bio)

CREATE TABLE IF NOT EXISTS public."GuildBotProfiles"
(
    "Id"
    SERIAL
    PRIMARY
    KEY,
    "GuildId"
    NUMERIC
(
    20,
    0
) NOT NULL UNIQUE,
    "AvatarUrl" TEXT NULL,
    "BannerUrl" TEXT NULL,
    "Bio" TEXT NULL,
    "DateAdded" TIMESTAMP
(
    6
) WITHOUT TIME ZONE NULL,
    "DateUpdated" TIMESTAMP
(
    6
)
  WITHOUT TIME ZONE NULL
    );

CREATE INDEX IF NOT EXISTS "IX_GuildBotProfiles_GuildId" ON public."GuildBotProfiles" ("GuildId");
