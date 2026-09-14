-- Feature requests submitted from the dashboard. One row per request, plus one row per
-- user upvote so a user can only vote once and can take the vote back.
-- Migration: 209-AddFeatureRequests.sql

CREATE TABLE IF NOT EXISTS "FeatureRequests"
(
    "Id"              SERIAL PRIMARY KEY,
    "UserId"          NUMERIC(20, 0)              NOT NULL,
    "UserName"        TEXT                        NOT NULL DEFAULT '',
    "GuildId"         NUMERIC(20, 0)              NULL,
    "GuildName"       TEXT                        NULL,
    "Category"        TEXT                        NOT NULL DEFAULT 'feature',
    "Title"           TEXT                        NOT NULL,
    "Body"            TEXT                        NOT NULL,
    "Status"          TEXT                        NOT NULL DEFAULT 'open',
    "OwnerNote"       TEXT                        NULL,
    "Votes"           INTEGER                     NOT NULL DEFAULT 0,
    "ReportMessageId" NUMERIC(20, 0)              NOT NULL DEFAULT 0,
    "UpdatedAt"       TIMESTAMP WITHOUT TIME ZONE NULL,
    "DateAdded"       TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc')
);

CREATE INDEX IF NOT EXISTS "IX_FeatureRequests_Status_Votes"
    ON "FeatureRequests" ("Status", "Votes" DESC, "DateAdded" DESC);

CREATE INDEX IF NOT EXISTS "IX_FeatureRequests_UserId"
    ON "FeatureRequests" ("UserId");

CREATE TABLE IF NOT EXISTS "FeatureRequestVotes"
(
    "Id"        SERIAL PRIMARY KEY,
    "RequestId" INTEGER                     NOT NULL REFERENCES "FeatureRequests" ("Id") ON DELETE CASCADE,
    "UserId"    NUMERIC(20, 0)              NOT NULL,
    "DateAdded" TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    CONSTRAINT "UQ_FeatureRequestVotes_Request_User" UNIQUE ("RequestId", "UserId")
);
