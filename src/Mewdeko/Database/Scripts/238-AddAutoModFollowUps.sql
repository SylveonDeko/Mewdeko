-- AutoMod follow-ups: what Mewdeko does on top of a native Discord AutoMod rule when it fires, kept per rule.
-- Migration: 238-AddAutoModFollowUps.sql

CREATE TABLE IF NOT EXISTS "AutoModFollowUps"
(
    "Id"                   SERIAL PRIMARY KEY,
    "GuildId"              NUMERIC(20, 0) NOT NULL,
    "RuleId"               NUMERIC(20, 0) NOT NULL,
    "Warn"                 BOOLEAN        NOT NULL DEFAULT FALSE,
    "WarnReason"           TEXT,
    "PunishAction"         INTEGER        NOT NULL DEFAULT 0,
    "PunishMinutes"        INTEGER        NOT NULL DEFAULT 0,
    "EscalateAction"       INTEGER        NOT NULL DEFAULT 0,
    "EscalateHits"         INTEGER        NOT NULL DEFAULT 0,
    "EscalateWindowMinutes" INTEGER       NOT NULL DEFAULT 0,
    "LogChannelId"         NUMERIC(20, 0),
    "DateAdded"            TIMESTAMP      NOT NULL DEFAULT NOW(),
    UNIQUE ("GuildId", "RuleId")
);

CREATE TABLE IF NOT EXISTS "AutoModHits"
(
    "Id"      SERIAL PRIMARY KEY,
    "GuildId" NUMERIC(20, 0) NOT NULL,
    "RuleId"  NUMERIC(20, 0) NOT NULL,
    "UserId"  NUMERIC(20, 0) NOT NULL,
    "HitAt"   TIMESTAMP      NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS "IX_AutoModHits_GuildId_RuleId_UserId_HitAt" ON "AutoModHits" ("GuildId", "RuleId", "UserId", "HitAt");
