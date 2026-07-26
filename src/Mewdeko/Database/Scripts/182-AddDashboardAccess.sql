-- Add support for restricted dashboard access (per-user/role, per-section grants)
-- Migration: 182-AddDashboardAccess.sql

-- Per-guild settings for the dashboard access feature
CREATE TABLE IF NOT EXISTS "DashboardAccessSettings"
(
    "Id"                    SERIAL PRIMARY KEY,
    "GuildId"               NUMERIC(20, 0) NOT NULL UNIQUE,
    "AdminsCanManageAccess" BOOLEAN        NOT NULL        DEFAULT FALSE,
    "DateAdded"             TIMESTAMP(6) WITHOUT TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- Users/roles explicitly allowed to manage the dashboard access list for a guild,
-- independent of the AdminsCanManageAccess toggle (mirrors the command permission
-- system's PermRole concept, but supports both users and roles).
CREATE TABLE IF NOT EXISTS "DashboardAccessManagers"
(
    "Id"         SERIAL PRIMARY KEY,
    "GuildId"    NUMERIC(20, 0) NOT NULL,
    "TargetType" INTEGER        NOT NULL, -- 0 = User, 1 = Role
    "TargetId"   NUMERIC(20, 0) NOT NULL,
    "GrantedBy"  NUMERIC(20, 0) NOT NULL,
    "DateAdded"  TIMESTAMP(6) WITHOUT TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    UNIQUE ("GuildId", "TargetType", "TargetId")
);

CREATE INDEX IF NOT EXISTS "IX_DashboardAccessManagers_GuildId" ON "DashboardAccessManagers" ("GuildId");

-- Restricted dashboard access grants: a user or role granted access to some
-- section(s) of the dashboard for a guild, without needing real Administrator
-- permission on Discord.
CREATE TABLE IF NOT EXISTS "DashboardAccess"
(
    "Id"         SERIAL PRIMARY KEY,
    "GuildId"    NUMERIC(20, 0) NOT NULL,
    "TargetType" INTEGER        NOT NULL, -- 0 = User, 1 = Role
    "TargetId"   NUMERIC(20, 0) NOT NULL,
    "GrantedBy"  NUMERIC(20, 0) NOT NULL,
    "DateAdded"  TIMESTAMP(6) WITHOUT TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    UNIQUE ("GuildId", "TargetType", "TargetId")
);

CREATE INDEX IF NOT EXISTS "IX_DashboardAccess_GuildId" ON "DashboardAccess" ("GuildId");

-- Per-section access level for a DashboardAccess grant.
CREATE TABLE IF NOT EXISTS "DashboardAccessSection"
(
    "Id"                SERIAL PRIMARY KEY,
    "DashboardAccessId" INTEGER NOT NULL REFERENCES "DashboardAccess" ("Id") ON DELETE CASCADE,
    "Section"           TEXT    NOT NULL,
    "Level"             INTEGER NOT NULL, -- 1 = View, 2 = Manage
    UNIQUE ("DashboardAccessId", "Section")
);

CREATE INDEX IF NOT EXISTS "IX_DashboardAccessSection_DashboardAccessId" ON "DashboardAccessSection" ("DashboardAccessId");
