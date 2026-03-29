CREATE TABLE IF NOT EXISTS "MinecraftServers"
(
    "Id"                  SERIAL PRIMARY KEY,
    "GuildId"             NUMERIC(20, 0)              NOT NULL,
    "Name"                TEXT                        NOT NULL,
    "Address"             TEXT                        NOT NULL,
    "Port"                INTEGER                     NOT NULL DEFAULT 25565,
    "ServerType"          INTEGER                     NOT NULL DEFAULT 0,
    "QueryPort"           INTEGER                     NOT NULL DEFAULT 0,
    "IsDefault"           BOOLEAN                     NOT NULL DEFAULT FALSE,
    "WatchChannelId"      NUMERIC(20, 0)              NULL,
    "WatchMessageId"      NUMERIC(20, 0)              NULL,
    "WatchInterval"       INTEGER                     NOT NULL DEFAULT 5,
    "CustomEmbedTemplate" TEXT                        NULL,
    "LastOnline"          BOOLEAN                     NULL,
    "DateAdded"           TIMESTAMP WITHOUT TIME ZONE NULL
);

CREATE INDEX IF NOT EXISTS "IX_MinecraftServers_GuildId"
    ON "MinecraftServers" ("GuildId");

CREATE UNIQUE INDEX IF NOT EXISTS "IX_MinecraftServers_GuildId_Name"
    ON "MinecraftServers" ("GuildId", "Name");
