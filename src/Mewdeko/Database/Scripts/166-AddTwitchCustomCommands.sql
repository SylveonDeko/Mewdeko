CREATE TABLE IF NOT EXISTS "TwitchCustomCommands"
(
    "Id"              SERIAL PRIMARY KEY,
    "GuildId"         NUMERIC(20, 0) NOT NULL,
    "Name"            TEXT           NOT NULL,
    "Response"        TEXT           NOT NULL,
    "PermissionLevel" INTEGER        NOT NULL DEFAULT 0,
    "CooldownSeconds" INTEGER        NOT NULL DEFAULT 0,
    "Enabled"         BOOLEAN        NOT NULL DEFAULT TRUE,
    "UseCount"        INTEGER        NOT NULL DEFAULT 0,
    "LastUsedAt"      TIMESTAMP      NULL,
    "DateAdded"       TIMESTAMP      NULL,
    "LastUpdatedAt"   TIMESTAMP      NULL,
    UNIQUE ("GuildId", "Name")
);

CREATE TABLE IF NOT EXISTS "TwitchLinkCodes"
(
    "Id"             SERIAL PRIMARY KEY,
    "GuildId"        NUMERIC(20, 0) NOT NULL,
    "TwitchUsername" TEXT           NOT NULL,
    "Code"           TEXT           NOT NULL UNIQUE,
    "ExpiresAt"      TIMESTAMP      NOT NULL,
    "ClaimedAt"      TIMESTAMP      NULL,
    "DateAdded"      TIMESTAMP      NULL
);

CREATE INDEX IF NOT EXISTS "IX_TwitchCustomCommands_GuildId_Name"
    ON "TwitchCustomCommands" ("GuildId", "Name");

CREATE INDEX IF NOT EXISTS "IX_TwitchLinkCodes_GuildId_Code"
    ON "TwitchLinkCodes" ("GuildId", "Code");

ALTER TABLE "TwitchGuildConfigs"
    ADD COLUMN IF NOT EXISTS "SubNotificationChannelId"  NUMERIC(20, 0) NULL,
    ADD COLUMN IF NOT EXISTS "SubNotificationMessage"    TEXT           NULL,
    ADD COLUMN IF NOT EXISTS "RaidNotificationChannelId" NUMERIC(20, 0) NULL,
    ADD COLUMN IF NOT EXISTS "RaidNotificationMessage"   TEXT           NULL;
