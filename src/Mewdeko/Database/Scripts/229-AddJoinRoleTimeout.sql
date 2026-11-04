-- Join role timeout: acts on members who still lack a required role some time after joining, such as kicking people who
-- never accepted the rules. Off until a server turns it on.
-- Migration: 229-AddJoinRoleTimeout.sql

CREATE TABLE IF NOT EXISTS "JoinRoleTimeoutSettings"
(
    "Id"                     SERIAL PRIMARY KEY,
    "GuildId"                NUMERIC(20, 0) NOT NULL UNIQUE,
    "Enabled"                BOOLEAN        NOT NULL DEFAULT FALSE,
    "TimeoutMinutes"         INTEGER        NOT NULL DEFAULT 1440,
    "RequiredRoleIds"        TEXT           NOT NULL DEFAULT '',
    "ExemptRoleIds"          TEXT           NOT NULL DEFAULT '',
    "Action"                 INTEGER        NOT NULL DEFAULT 0,
    "ActionRoleId"           NUMERIC(20, 0),
    "WarnBeforeMinutes"      INTEGER,
    "WarnMessage"            TEXT,
    "ActionMessage"          TEXT,
    "LogChannelId"           NUMERIC(20, 0),
    "IncludeExistingMembers" BOOLEAN        NOT NULL DEFAULT FALSE,
    "DateAdded"              TIMESTAMP      NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS "JoinRoleTimeoutPending"
(
    "Id"       SERIAL PRIMARY KEY,
    "GuildId"  NUMERIC(20, 0) NOT NULL,
    "UserId"   NUMERIC(20, 0) NOT NULL,
    "JoinedAt" TIMESTAMP      NOT NULL,
    "WarnedAt" TIMESTAMP,
    UNIQUE ("GuildId", "UserId")
);

CREATE INDEX IF NOT EXISTS "IX_JoinRoleTimeoutPending_GuildId_JoinedAt" ON "JoinRoleTimeoutPending" ("GuildId", "JoinedAt");
