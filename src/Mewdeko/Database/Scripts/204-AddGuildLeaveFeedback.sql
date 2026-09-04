-- Tracks the "why was I removed" prompt sent to a guild owner after the bot leaves
-- a server, along with whatever they answer. One row is created when the prompt is
-- sent, then updated in place when the owner picks a reason or writes a comment.
-- Migration: 204-AddGuildLeaveFeedback.sql

CREATE TABLE IF NOT EXISTS "GuildLeaveFeedbacks"
(
    "Id"              SERIAL PRIMARY KEY,
    "GuildId"         NUMERIC(20, 0)              NOT NULL,
    "GuildName"       TEXT                        NOT NULL DEFAULT '',
    "MemberCount"     INTEGER                     NOT NULL DEFAULT 0,
    "OwnerId"         NUMERIC(20, 0)              NOT NULL,
    "JoinedAt"        TIMESTAMP WITHOUT TIME ZONE NULL,
    "Reason"          TEXT                        NULL,
    "Comment"         TEXT                        NULL,
    "Dismissed"       BOOLEAN                     NOT NULL DEFAULT FALSE,
    "ReportMessageId" NUMERIC(20, 0)              NOT NULL DEFAULT 0,
    "AnsweredAt"      TIMESTAMP WITHOUT TIME ZONE NULL,
    "DateAdded"       TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc')
);

CREATE INDEX IF NOT EXISTS "IX_GuildLeaveFeedbacks_GuildId_DateAdded"
    ON "GuildLeaveFeedbacks" ("GuildId", "DateAdded" DESC);

CREATE INDEX IF NOT EXISTS "IX_GuildLeaveFeedbacks_OwnerId"
    ON "GuildLeaveFeedbacks" ("OwnerId");
