-- Per-guild Word of the Day configuration, custom word pools, and a history of posted words.
-- Migration: 216-WordOfTheDay.sql

CREATE TABLE IF NOT EXISTS "WordOfTheDayConfigs"
(
    "Id"              SERIAL PRIMARY KEY,
    "GuildId"         NUMERIC(20, 0)              NOT NULL UNIQUE,
    "ChannelId"       NUMERIC(20, 0)              NULL,
    "Enabled"         BOOLEAN                     NOT NULL DEFAULT FALSE,
    "PostHour"        INTEGER                     NOT NULL DEFAULT 9,
    "Timezone"        TEXT                        NOT NULL DEFAULT 'UTC',
    "PingRoleId"      NUMERIC(20, 0)              NULL,
    "MessageTemplate" TEXT                        NULL,
    "Topic"           TEXT                        NULL,
    "PartOfSpeech"    INTEGER                     NOT NULL DEFAULT 0,
    "Difficulty"      INTEGER                     NOT NULL DEFAULT 0,
    "SourceMode"      INTEGER                     NOT NULL DEFAULT 0,
    "LastPostedDate"  DATE                        NULL,
    "DateAdded"       TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    "DateModified"    TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc')
);

CREATE TABLE IF NOT EXISTS "WordOfTheDayWords"
(
    "Id"           SERIAL PRIMARY KEY,
    "GuildId"      NUMERIC(20, 0)              NOT NULL,
    "Word"         TEXT                        NOT NULL,
    "PartOfSpeech" TEXT                        NULL,
    "Definition"   TEXT                        NULL,
    "Example"      TEXT                        NULL,
    "AddedBy"      NUMERIC(20, 0)              NOT NULL,
    "TimesUsed"    INTEGER                     NOT NULL DEFAULT 0,
    "LastUsed"     TIMESTAMP WITHOUT TIME ZONE NULL,
    "DateAdded"    TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc')
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_WordOfTheDayWords_GuildId_Word"
    ON "WordOfTheDayWords" ("GuildId", LOWER("Word"));

CREATE TABLE IF NOT EXISTS "WordOfTheDayHistory"
(
    "Id"           SERIAL PRIMARY KEY,
    "GuildId"      NUMERIC(20, 0)              NOT NULL,
    "Word"         TEXT                        NOT NULL,
    "PartOfSpeech" TEXT                        NULL,
    "Definition"   TEXT                        NOT NULL,
    "Example"      TEXT                        NULL,
    "Phonetic"     TEXT                        NULL,
    "PostedOn"     DATE                        NOT NULL,
    "DateAdded"    TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc')
);

CREATE INDEX IF NOT EXISTS "IX_WordOfTheDayHistory_GuildId_PostedOn"
    ON "WordOfTheDayHistory" ("GuildId", "PostedOn" DESC);
