ALTER TABLE "StatChannels"
    ADD COLUMN IF NOT EXISTS "DisplayStyle"          INTEGER                     NOT NULL DEFAULT 1,
    ADD COLUMN IF NOT EXISTS "StyleOptions"          TEXT                        NULL,
    ADD COLUMN IF NOT EXISTS "UpdateMechanism"       INTEGER                     NOT NULL DEFAULT 2,
    ADD COLUMN IF NOT EXISTS "UpdateIntervalMinutes" INTEGER                     NOT NULL DEFAULT 5,
    ADD COLUMN IF NOT EXISTS "CategoryId"            NUMERIC(20, 0)              NULL,
    ADD COLUMN IF NOT EXISTS "Position"              INTEGER                     NULL,
    ADD COLUMN IF NOT EXISTS "PermissionOverwrites"  TEXT                        NULL,
    ADD COLUMN IF NOT EXISTS "TargetId"              NUMERIC(20, 0)              NULL,
    ADD COLUMN IF NOT EXISTS "TargetName"            TEXT                        NULL,
    ADD COLUMN IF NOT EXISTS "LastValue"             TEXT                        NULL,
    ADD COLUMN IF NOT EXISTS "LastUpdateAt"          TIMESTAMP WITHOUT TIME ZONE NULL;

CREATE TABLE IF NOT EXISTS "StatChannelSettings"
(
    "Id"                     SERIAL PRIMARY KEY,
    "GuildId"                NUMERIC(20, 0)              NOT NULL,
    "DefaultMechanism"       INTEGER                     NOT NULL DEFAULT 2,
    "DefaultIntervalMinutes" INTEGER                     NOT NULL DEFAULT 5,
    "DefaultDisplayStyle"    INTEGER                     NOT NULL DEFAULT 1,
    "DateAdded"              TIMESTAMP WITHOUT TIME ZONE NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_StatChannelSettings_GuildId"
    ON "StatChannelSettings" ("GuildId");
