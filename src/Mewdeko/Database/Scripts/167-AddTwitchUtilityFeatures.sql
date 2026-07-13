CREATE TABLE IF NOT EXISTS "TwitchCounters"
(
    "Id"            SERIAL PRIMARY KEY,
    "GuildId"       NUMERIC(20, 0) NOT NULL,
    "Name"          TEXT           NOT NULL,
    "Value"         INTEGER        NOT NULL DEFAULT 0,
    "DateAdded"     TIMESTAMP      NULL,
    "LastUpdatedAt" TIMESTAMP      NULL,
    UNIQUE ("GuildId", "Name")
);

CREATE TABLE IF NOT EXISTS "TwitchRoleSyncMappings"
(
    "Id"              SERIAL PRIMARY KEY,
    "GuildId"         NUMERIC(20, 0) NOT NULL,
    "PermissionLevel" INTEGER        NOT NULL,
    "RoleId"          NUMERIC(20, 0) NOT NULL,
    "Enabled"         BOOLEAN        NOT NULL DEFAULT TRUE,
    "DateAdded"       TIMESTAMP      NULL,
    "LastUpdatedAt"   TIMESTAMP      NULL,
    UNIQUE ("GuildId", "PermissionLevel", "RoleId")
);

CREATE INDEX IF NOT EXISTS "IX_TwitchCounters_GuildId_Name"
    ON "TwitchCounters" ("GuildId", "Name");

CREATE INDEX IF NOT EXISTS "IX_TwitchRoleSyncMappings_GuildId"
    ON "TwitchRoleSyncMappings" ("GuildId");

ALTER TABLE "TwitchGuildConfigs"
    ADD COLUMN IF NOT EXISTS "StreamRecapChannelId" NUMERIC(20, 0) NULL,
    ADD COLUMN IF NOT EXISTS "StreamRecapEnabled"   BOOLEAN        NOT NULL DEFAULT FALSE;
