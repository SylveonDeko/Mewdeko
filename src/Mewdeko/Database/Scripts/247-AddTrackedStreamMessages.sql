-- Live stream messages that are edited in place, kept so a restart can keep editing them and mark them ended.
-- Migration: 247-AddTrackedStreamMessages.sql

CREATE TABLE IF NOT EXISTS "TrackedStreamMessages"
(
    "Id"               SERIAL PRIMARY KEY,
    "FollowedStreamId" INTEGER        NOT NULL UNIQUE,
    "GuildId"          NUMERIC(20, 0) NOT NULL,
    "ChannelId"        NUMERIC(20, 0) NOT NULL,
    "MessageId"        NUMERIC(20, 0) NOT NULL,
    "StreamKey"        TEXT           NOT NULL,
    "StartedAt"        TIMESTAMP      NOT NULL,
    "LastEdit"         TIMESTAMP      NOT NULL
);

CREATE INDEX IF NOT EXISTS "IX_TrackedStreamMessages_GuildId" ON "TrackedStreamMessages" ("GuildId");
