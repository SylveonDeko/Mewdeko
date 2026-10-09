-- Data imported from other bots (MEE6, Lurkr, Polaris, Arcane, Amari, Tatsu, UnbelievaBoat). Each row keeps a snapshot of
-- what the import replaced so it can be undone for a day.
-- Migration: 224-AddDataImports.sql

CREATE TABLE IF NOT EXISTS "DataImports"
(
    "Id"              SERIAL PRIMARY KEY,
    "GuildId"         NUMERIC(20, 0) NOT NULL,
    "UserId"          NUMERIC(20, 0) NOT NULL,
    "Source"          INTEGER        NOT NULL,
    "Kind"            INTEGER        NOT NULL,
    "MemberCount"     INTEGER        NOT NULL DEFAULT 0,
    "RoleRewardCount" INTEGER        NOT NULL DEFAULT 0,
    "Snapshot"        TEXT           NOT NULL DEFAULT '',
    "UndoneAt"        TIMESTAMP      NULL,
    "DateAdded"       TIMESTAMP      NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS "IX_DataImports_GuildId_DateAdded" ON "DataImports" ("GuildId", "DateAdded" DESC);
