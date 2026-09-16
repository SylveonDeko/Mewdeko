-- Stat roles: roles granted and taken away on a schedule based on a member's activity over a window (messages,
-- voice time, invites, time in the server or account age), with threshold, top rank, top percent and daily streak
-- limits, per role filters, optional permanence and notifications.
-- Migration: 212-StatRoles.sql

CREATE TABLE IF NOT EXISTS "StatRoles"
(
    "Id"              SERIAL PRIMARY KEY,
    "GuildId"         NUMERIC(20, 0)              NOT NULL,
    "RoleId"          NUMERIC(20, 0)              NOT NULL,
    "Name"            TEXT                        NOT NULL DEFAULT '',
    "Enabled"         BOOLEAN                     NOT NULL DEFAULT TRUE,
    "StatType"        INTEGER                     NOT NULL DEFAULT 0,
    "LimitType"       INTEGER                     NOT NULL DEFAULT 0,
    "Minimum"         BIGINT                      NOT NULL DEFAULT 1,
    "Maximum"         BIGINT                      NULL,
    "LookbackDays"    INTEGER                     NOT NULL DEFAULT 0,
    "TopStart"        INTEGER                     NOT NULL DEFAULT 1,
    "TopEnd"          INTEGER                     NOT NULL DEFAULT 10,
    "RequiredDays"    INTEGER                     NOT NULL DEFAULT 1,
    "Permanent"       BOOLEAN                     NOT NULL DEFAULT FALSE,
    "Invert"          BOOLEAN                     NOT NULL DEFAULT FALSE,
    "ApplyToBots"     BOOLEAN                     NOT NULL DEFAULT FALSE,
    "GroupName"       TEXT                        NULL,
    "ChannelFilter"   TEXT                        NULL,
    "RoleWhitelist"   TEXT                        NULL,
    "RoleBlacklist"   TEXT                        NULL,
    "IgnoredUsers"    TEXT                        NULL,
    "NotifyChannelId" NUMERIC(20, 0)              NULL,
    "NotifyDm"        BOOLEAN                     NOT NULL DEFAULT FALSE,
    "NotifyMessage"   TEXT                        NULL,
    "IntervalMinutes" INTEGER                     NOT NULL DEFAULT 180,
    "LastRunAt"       TIMESTAMP WITHOUT TIME ZONE NULL,
    "DateAdded"       TIMESTAMP WITHOUT TIME ZONE NULL
);

CREATE INDEX IF NOT EXISTS "IX_StatRoles_GuildId"
    ON "StatRoles" ("GuildId");
