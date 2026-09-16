-- Game and application activity tracking: time members spend in each presence activity (playing, streaming,
-- listening, watching, competing), with a 90 day segment window plus all time totals, a per guild activity
-- name whitelist or blacklist, and an activity condition for stat roles.
-- Migration: 214-ActivityTracking.sql

CREATE TABLE IF NOT EXISTS "ActivitySegments"
(
    "Id"            BIGSERIAL PRIMARY KEY,
    "GuildId"       NUMERIC(20, 0)              NOT NULL,
    "UserId"        NUMERIC(20, 0)              NOT NULL,
    "ApplicationId" NUMERIC(20, 0)              NULL,
    "Name"          TEXT                        NOT NULL,
    "Type"          INTEGER                     NOT NULL DEFAULT 0,
    "StartedAt"     TIMESTAMP WITHOUT TIME ZONE NOT NULL,
    "EndedAt"       TIMESTAMP WITHOUT TIME ZONE NOT NULL,
    "Seconds"       INTEGER                     NOT NULL
);

CREATE INDEX IF NOT EXISTS "IX_ActivitySegments_Guild_EndedAt"
    ON "ActivitySegments" ("GuildId", "EndedAt");

CREATE INDEX IF NOT EXISTS "IX_ActivitySegments_Guild_User_EndedAt"
    ON "ActivitySegments" ("GuildId", "UserId", "EndedAt");

CREATE INDEX IF NOT EXISTS "IX_ActivitySegments_EndedAt"
    ON "ActivitySegments" ("EndedAt");

CREATE TABLE IF NOT EXISTS "ActivityTotals"
(
    "Id"            SERIAL PRIMARY KEY,
    "GuildId"       NUMERIC(20, 0)              NOT NULL,
    "UserId"        NUMERIC(20, 0)              NOT NULL,
    "ApplicationId" NUMERIC(20, 0)              NULL,
    "Name"          TEXT                        NOT NULL,
    "Type"          INTEGER                     NOT NULL DEFAULT 0,
    "Seconds"       BIGINT                      NOT NULL DEFAULT 0,
    "DateAdded"     TIMESTAMP WITHOUT TIME ZONE NULL,
    CONSTRAINT "UQ_ActivityTotals_Guild_User_Name_Type" UNIQUE ("GuildId", "UserId", "Name", "Type")
);

CREATE INDEX IF NOT EXISTS "IX_ActivityTotals_Guild_Name"
    ON "ActivityTotals" ("GuildId", "Name");

CREATE TABLE IF NOT EXISTS "ActivityFilters"
(
    "Id"        SERIAL PRIMARY KEY,
    "GuildId"   NUMERIC(20, 0)              NOT NULL,
    "Name"      TEXT                        NOT NULL,
    "DateAdded" TIMESTAMP WITHOUT TIME ZONE NULL,
    CONSTRAINT "UQ_ActivityFilters_Guild_Name" UNIQUE ("GuildId", "Name")
);

ALTER TABLE "ServerStatsSettings"
    ADD COLUMN IF NOT EXISTS "TrackActivities"    BOOLEAN NOT NULL DEFAULT FALSE,
    ADD COLUMN IF NOT EXISTS "VerifyActivities"   BOOLEAN NOT NULL DEFAULT TRUE,
    ADD COLUMN IF NOT EXISTS "ActivityFilterMode" INTEGER NOT NULL DEFAULT 0;

ALTER TABLE "StatRoles"
    ADD COLUMN IF NOT EXISTS "ActivityName" TEXT NULL;
