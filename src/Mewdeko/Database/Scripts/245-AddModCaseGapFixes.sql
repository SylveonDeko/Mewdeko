-- When each member last filed a report, so the report cooldown survives restarts.
-- Migration: 245-AddModCaseGapFixes.sql

CREATE TABLE IF NOT EXISTS "ModReportCooldowns"
(
    "GuildId"      NUMERIC(20, 0) NOT NULL,
    "UserId"       NUMERIC(20, 0) NOT NULL,
    "LastReportAt" TIMESTAMP      NOT NULL,
    PRIMARY KEY ("GuildId", "UserId")
);
