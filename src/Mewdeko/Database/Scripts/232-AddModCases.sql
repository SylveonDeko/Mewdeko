-- Numbered moderation cases, staff notes on members, and the channels cases and reports are posted to.
-- Migration: 232-AddModCases.sql

CREATE TABLE IF NOT EXISTS "ModCases"
(
    "Id"              SERIAL PRIMARY KEY,
    "GuildId"         NUMERIC(20, 0) NOT NULL,
    "CaseNumber"      INTEGER        NOT NULL,
    "UserId"          NUMERIC(20, 0) NOT NULL,
    "ModeratorId"     NUMERIC(20, 0) NOT NULL,
    "Action"          INTEGER        NOT NULL,
    "Reason"          TEXT,
    "DurationSeconds" BIGINT,
    "WarningId"       INTEGER,
    "DateAdded"       TIMESTAMP      NOT NULL DEFAULT NOW(),
    UNIQUE ("GuildId", "CaseNumber")
);

CREATE INDEX IF NOT EXISTS "IX_ModCases_GuildId_UserId" ON "ModCases" ("GuildId", "UserId");
CREATE INDEX IF NOT EXISTS "IX_ModCases_GuildId_ModeratorId" ON "ModCases" ("GuildId", "ModeratorId");

CREATE TABLE IF NOT EXISTS "ModCaseSettings"
(
    "Id"              SERIAL PRIMARY KEY,
    "GuildId"         NUMERIC(20, 0) NOT NULL UNIQUE,
    "LogChannelId"    NUMERIC(20, 0),
    "ReportChannelId" NUMERIC(20, 0)
);

CREATE TABLE IF NOT EXISTS "ModNotes"
(
    "Id"          SERIAL PRIMARY KEY,
    "GuildId"     NUMERIC(20, 0) NOT NULL,
    "UserId"      NUMERIC(20, 0) NOT NULL,
    "ModeratorId" NUMERIC(20, 0) NOT NULL,
    "Note"        TEXT           NOT NULL,
    "DateAdded"   TIMESTAMP      NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS "IX_ModNotes_GuildId_UserId" ON "ModNotes" ("GuildId", "UserId");
