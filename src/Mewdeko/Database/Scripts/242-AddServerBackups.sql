-- Server backups: a compressed snapshot of a server's settings, roles, channels and emojis that its owner can restore
-- into the same server or another one.
-- Migration: 242-AddServerBackups.sql

CREATE TABLE IF NOT EXISTS "ServerBackups"
(
    "Id"              SERIAL PRIMARY KEY,
    "UserId"          NUMERIC(20, 0) NOT NULL,
    "SourceGuildId"   NUMERIC(20, 0) NOT NULL,
    "SourceGuildName" TEXT           NOT NULL DEFAULT '',
    "Data"            BYTEA          NOT NULL,
    "SizeBytes"       INTEGER        NOT NULL DEFAULT 0,
    "RoleCount"       INTEGER        NOT NULL DEFAULT 0,
    "ChannelCount"    INTEGER        NOT NULL DEFAULT 0,
    "EmojiCount"      INTEGER        NOT NULL DEFAULT 0,
    "MessageCount"    INTEGER        NOT NULL DEFAULT 0,
    "DateAdded"       TIMESTAMP      NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS "IX_ServerBackups_UserId" ON "ServerBackups" ("UserId", "DateAdded");
