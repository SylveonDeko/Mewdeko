-- Guild ownership lets several bot instances share one database without handling the same server twice. A server with
-- a row is handled only by the instance it is assigned to; a server with no row stays with the master instance.
-- Migration: 265-AddGuildOwnership.sql

ALTER TABLE "BotInstances"
    ADD COLUMN IF NOT EXISTS "OwnerUserId" NUMERIC(20, 0) NULL;

ALTER TABLE "BotInstances"
    ADD COLUMN IF NOT EXISTS "IsCustom" BOOLEAN NOT NULL DEFAULT FALSE;

CREATE TABLE IF NOT EXISTS "GuildOwnership"
(
    "Id"         SERIAL PRIMARY KEY,
    "GuildId"    NUMERIC(20, 0) NOT NULL UNIQUE,
    "InstanceId" INT            NOT NULL REFERENCES "BotInstances" ("Id") ON DELETE CASCADE,
    "DateAdded"  TIMESTAMP      NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS "IX_GuildOwnership_InstanceId" ON "GuildOwnership" ("InstanceId");
