-- Per-weekday and per-month overrides for Word of the Day filters.
-- Migration: 217-WordOfTheDaySchedule.sql

CREATE TABLE IF NOT EXISTS "WordOfTheDaySchedules"
(
    "Id"           SERIAL PRIMARY KEY,
    "GuildId"      NUMERIC(20, 0)              NOT NULL,
    "RuleType"     INTEGER                     NOT NULL,
    "RuleKey"      INTEGER                     NOT NULL,
    "Topic"        TEXT                        NULL,
    "PartOfSpeech" INTEGER                     NULL,
    "Difficulty"   INTEGER                     NULL,
    "DateAdded"    TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc')
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_WordOfTheDaySchedules_Guild_Rule"
    ON "WordOfTheDaySchedules" ("GuildId", "RuleType", "RuleKey");
