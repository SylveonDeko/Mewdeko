-- Ticket claimed categories and transcript DMs, role menu reverse states, and temporary voice hubs with their own settings.
-- Existing voice configs become the first hub of their server, and existing temporary channels point at it.
-- Migration: 262-AddTicketRoleMenuVoiceOptions.sql

ALTER TABLE "PanelButtons"
    ADD COLUMN IF NOT EXISTS "ClaimedCategoryId" NUMERIC(20, 0);
ALTER TABLE "PanelButtons"
    ADD COLUMN IF NOT EXISTS "DmTranscript" BOOLEAN NOT NULL DEFAULT FALSE;

ALTER TABLE "SelectMenuOptions"
    ADD COLUMN IF NOT EXISTS "ClaimedCategoryId" NUMERIC(20, 0);
ALTER TABLE "SelectMenuOptions"
    ADD COLUMN IF NOT EXISTS "DmTranscript" BOOLEAN NOT NULL DEFAULT FALSE;

ALTER TABLE "GuildTicketSettings"
    ADD COLUMN IF NOT EXISTS "DmTranscriptOnClose" BOOLEAN NOT NULL DEFAULT FALSE;

CREATE TABLE IF NOT EXISTS "RoleMenuReverseStates"
(
    "Id"        SERIAL PRIMARY KEY,
    "GuildId"   NUMERIC(20, 0) NOT NULL,
    "UserId"    NUMERIC(20, 0) NOT NULL,
    "RoleId"    NUMERIC(20, 0) NOT NULL,
    "MenuId"    INTEGER        NOT NULL REFERENCES "RoleMenus" ("Id") ON DELETE CASCADE,
    "DateAdded" TIMESTAMP      NOT NULL DEFAULT NOW(),
    UNIQUE ("MenuId", "UserId", "RoleId")
);

CREATE INDEX IF NOT EXISTS "IX_RoleMenuReverseStates_GuildId_RoleId" ON "RoleMenuReverseStates" ("GuildId", "RoleId");

ALTER TABLE "CustomVoiceConfig"
    ADD COLUMN IF NOT EXISTS "HubName" TEXT;
ALTER TABLE "CustomVoiceConfig"
    ADD COLUMN IF NOT EXISTS "OwnerLockMinutes" INTEGER NOT NULL DEFAULT 0;
ALTER TABLE "CustomVoiceConfig"
    ADD COLUMN IF NOT EXISTS "OwnerManageChannel" BOOLEAN NOT NULL DEFAULT TRUE;
ALTER TABLE "CustomVoiceConfig"
    ADD COLUMN IF NOT EXISTS "OwnerManageRoles" BOOLEAN NOT NULL DEFAULT FALSE;
ALTER TABLE "CustomVoiceConfig"
    ADD COLUMN IF NOT EXISTS "OwnerMoveMembers" BOOLEAN NOT NULL DEFAULT TRUE;
ALTER TABLE "CustomVoiceConfig"
    ADD COLUMN IF NOT EXISTS "OwnerPrioritySpeaker" BOOLEAN NOT NULL DEFAULT FALSE;
ALTER TABLE "CustomVoiceConfig"
    ADD COLUMN IF NOT EXISTS "SyncPermissions" INTEGER NOT NULL DEFAULT 0;
ALTER TABLE "CustomVoiceConfig"
    ADD COLUMN IF NOT EXISTS "ModeratorRoleIds" TEXT NOT NULL DEFAULT '';
ALTER TABLE "CustomVoiceConfig"
    ADD COLUMN IF NOT EXISTS "CleanupLeftovers" BOOLEAN NOT NULL DEFAULT TRUE;

ALTER TABLE "CustomVoiceChannel"
    ADD COLUMN IF NOT EXISTS "HubVoiceChannelId" NUMERIC(20, 0);
ALTER TABLE "CustomVoiceChannel"
    ADD COLUMN IF NOT EXISTS "IsHidden" BOOLEAN NOT NULL DEFAULT FALSE;
ALTER TABLE "CustomVoiceChannel"
    ADD COLUMN IF NOT EXISTS "OwnerLeftAt" TIMESTAMP;

UPDATE "CustomVoiceChannel" c
SET "HubVoiceChannelId" = (SELECT f."HubVoiceChannelId"
                           FROM "CustomVoiceConfig" f
                           WHERE f."GuildId" = c."GuildId"
                             AND f."HubVoiceChannelId" <> 0
                           ORDER BY f."Id"
                           LIMIT 1)
WHERE c."HubVoiceChannelId" IS NULL;

UPDATE "CustomVoiceConfig"
SET "HubName" = 'Hub 1'
WHERE "HubName" IS NULL
  AND "HubVoiceChannelId" <> 0;

DELETE
FROM "CustomVoiceConfig" a
    USING "CustomVoiceConfig" b
WHERE a."GuildId" = b."GuildId"
  AND a."HubVoiceChannelId" = b."HubVoiceChannelId"
  AND a."HubVoiceChannelId" <> 0
  AND a."Id" > b."Id";

CREATE UNIQUE INDEX IF NOT EXISTS "UX_CustomVoiceConfig_GuildId_HubVoiceChannelId" ON "CustomVoiceConfig" ("GuildId", "HubVoiceChannelId") WHERE "HubVoiceChannelId" <> 0;
