-- Queue of servers the bot has left whose data is due to be purged once a grace period passes.
-- A row is inserted when the bot leaves a server and removed again if the bot is re-added before
-- the purge runs, so a quick kick-and-reinvite keeps its data. Purged rows stay behind with
-- "PurgedAt" set so the last few sweeps can be audited from the owner commands.
-- Migration: 215-AddGuildDataRetention.sql

CREATE TABLE IF NOT EXISTS "GuildDataRetention"
(
    "GuildId"     NUMERIC(20, 0)              NOT NULL PRIMARY KEY,
    "GuildName"   TEXT                        NOT NULL DEFAULT '',
    "LeftAt"      TIMESTAMP WITHOUT TIME ZONE NOT NULL,
    "PurgeAfter"  TIMESTAMP WITHOUT TIME ZONE NOT NULL,
    "PurgedAt"    TIMESTAMP WITHOUT TIME ZONE NULL,
    "RowsDeleted" BIGINT                      NOT NULL DEFAULT 0,
    "Source"      TEXT                        NOT NULL DEFAULT 'left',
    "DateAdded"   TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc')
);

CREATE INDEX IF NOT EXISTS "IX_GuildDataRetention_PurgeAfter"
    ON "GuildDataRetention" ("PurgeAfter")
    WHERE "PurgedAt" IS NULL;
