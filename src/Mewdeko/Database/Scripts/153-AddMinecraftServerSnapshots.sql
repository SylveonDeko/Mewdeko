CREATE TABLE IF NOT EXISTS "MinecraftServerSnapshots"
(
    "Id"            SERIAL PRIMARY KEY,
    "ServerId"      INTEGER                     NOT NULL REFERENCES "MinecraftServers" ("Id") ON DELETE CASCADE,
    "IsOnline"      BOOLEAN                     NOT NULL,
    "PlayersOnline" INTEGER                     NOT NULL DEFAULT 0,
    "PlayersMax"    INTEGER                     NOT NULL DEFAULT 0,
    "Latency"       INTEGER                     NOT NULL DEFAULT 0,
    "Version"       TEXT                        NULL,
    "Timestamp"     TIMESTAMP WITHOUT TIME ZONE NOT NULL
);

CREATE INDEX IF NOT EXISTS "IX_MinecraftServerSnapshots_ServerId_Timestamp"
    ON "MinecraftServerSnapshots" ("ServerId", "Timestamp" DESC);

ALTER TABLE "MinecraftServers"
    ADD COLUMN IF NOT EXISTS "LastStatusJson" TEXT NULL;
