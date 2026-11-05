-- Automations: no-code rules made of one trigger, a list of conditions and an ordered list of actions, plus a log of the
-- latest runs of each one. Conditions and actions are stored as JSON arrays and checked when they are saved.
-- Migration: 258-AddAutomations.sql

CREATE TABLE IF NOT EXISTS "Automations"
(
    "Id"                 SERIAL PRIMARY KEY,
    "GuildId"            NUMERIC(20, 0) NOT NULL,
    "Name"               TEXT           NOT NULL,
    "Enabled"            BOOLEAN        NOT NULL DEFAULT TRUE,
    "Trigger"            INTEGER        NOT NULL DEFAULT 0,
    "Conditions"         TEXT           NOT NULL DEFAULT '[]',
    "Actions"            TEXT           NOT NULL DEFAULT '[]',
    "CooldownSeconds"    INTEGER        NOT NULL DEFAULT 0,
    "CooldownScope"      INTEGER        NOT NULL DEFAULT 0,
    "RetryOnFailure"     BOOLEAN        NOT NULL DEFAULT FALSE,
    "IgnoreBots"         BOOLEAN        NOT NULL DEFAULT TRUE,
    "AllowMassMentions"  BOOLEAN        NOT NULL DEFAULT FALSE,
    "CreatorId"          NUMERIC(20, 0) NOT NULL DEFAULT 0,
    "DateAdded"          TIMESTAMP      NOT NULL DEFAULT NOW(),
    "DateModified"       TIMESTAMP      NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS "IX_Automations_GuildId" ON "Automations" ("GuildId");

CREATE TABLE IF NOT EXISTS "AutomationRuns"
(
    "Id"                SERIAL PRIMARY KEY,
    "AutomationId"      INTEGER        NOT NULL REFERENCES "Automations" ("Id") ON DELETE CASCADE,
    "GuildId"           NUMERIC(20, 0) NOT NULL,
    "Success"           BOOLEAN        NOT NULL,
    "IsTest"            BOOLEAN        NOT NULL DEFAULT FALSE,
    "FailedActionIndex" INTEGER,
    "FailedActionType"  TEXT,
    "Reason"            TEXT,
    "UserId"            NUMERIC(20, 0) NOT NULL DEFAULT 0,
    "ChannelId"         NUMERIC(20, 0) NOT NULL DEFAULT 0,
    "DurationMs"        INTEGER        NOT NULL DEFAULT 0,
    "RanAt"             TIMESTAMP      NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS "IX_AutomationRuns_AutomationId_Id" ON "AutomationRuns" ("AutomationId", "Id" DESC);
