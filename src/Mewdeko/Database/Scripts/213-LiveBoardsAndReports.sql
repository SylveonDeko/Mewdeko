-- Live boards: pinned messages the bot keeps refreshing with a leaderboard, chart or overview. Server reports:
-- a scheduled digest of growth and activity posted to a channel daily, weekly or monthly.
-- Migration: 213-LiveBoardsAndReports.sql

CREATE TABLE IF NOT EXISTS "LiveBoards"
(
    "Id"              SERIAL PRIMARY KEY,
    "GuildId"         NUMERIC(20, 0)              NOT NULL,
    "ChannelId"       NUMERIC(20, 0)              NOT NULL,
    "MessageId"       NUMERIC(20, 0)              NOT NULL DEFAULT 0,
    "Kind"            INTEGER                     NOT NULL,
    "Range"           INTEGER                     NOT NULL DEFAULT 0,
    "Pin"             BOOLEAN                     NOT NULL DEFAULT TRUE,
    "Entries"         INTEGER                     NOT NULL DEFAULT 10,
    "IntervalMinutes" INTEGER                     NOT NULL DEFAULT 15,
    "LastUpdateAt"    TIMESTAMP WITHOUT TIME ZONE NULL,
    "DateAdded"       TIMESTAMP WITHOUT TIME ZONE NULL
);

CREATE INDEX IF NOT EXISTS "IX_LiveBoards_GuildId"
    ON "LiveBoards" ("GuildId");

CREATE TABLE IF NOT EXISTS "ServerReportSettings"
(
    "Id"         SERIAL PRIMARY KEY,
    "GuildId"    NUMERIC(20, 0)              NOT NULL,
    "ChannelId"  NUMERIC(20, 0)              NULL,
    "Frequency"  INTEGER                     NOT NULL DEFAULT 1,
    "Enabled"    BOOLEAN                     NOT NULL DEFAULT FALSE,
    "LastSentAt" TIMESTAMP WITHOUT TIME ZONE NULL,
    "DateAdded"  TIMESTAMP WITHOUT TIME ZONE NULL,
    CONSTRAINT "UQ_ServerReportSettings_GuildId" UNIQUE ("GuildId")
);
