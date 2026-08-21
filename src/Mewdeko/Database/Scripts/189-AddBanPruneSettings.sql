-- Stores how many days of messages a ban purges. Rows are keyed by scope (guild default,
-- category override, channel override) and by action, so each moderation action that bans
-- can carry its own purge. An empty ActionKey applies to every action in that scope.
-- Migration: 189-AddBanPruneSettings.sql

CREATE TABLE IF NOT EXISTS "BanPruneSettings"
(
    "Id"        SERIAL PRIMARY KEY,
    "GuildId"   NUMERIC(20, 0) NOT NULL,
    "ScopeType" INTEGER        NOT NULL     DEFAULT 0,
    "ScopeId"   NUMERIC(20, 0) NOT NULL     DEFAULT 0,
    "ActionKey" TEXT           NOT NULL     DEFAULT '',
    "PruneDays" INTEGER        NOT NULL     DEFAULT 0,
    "DateAdded" TIMESTAMP WITHOUT TIME ZONE DEFAULT NOW()
);
-- ScopeType: 0 = guild default, 1 = category, 2 = channel. ScopeId is 0 for the guild default.

CREATE UNIQUE INDEX IF NOT EXISTS "IX_BanPruneSettings_Scope"
    ON "BanPruneSettings" ("GuildId", "ScopeType", "ScopeId", "ActionKey");
