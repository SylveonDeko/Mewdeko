-- Active member timeouts, so the bot can log when one runs out even though Discord sends no event for it.
-- Migration: 248-AddTrackedTimeouts.sql

CREATE TABLE IF NOT EXISTS "TrackedTimeouts"
(
    "Id"      SERIAL PRIMARY KEY,
    "GuildId" NUMERIC(20, 0) NOT NULL,
    "UserId"  NUMERIC(20, 0) NOT NULL,
    "EndsAt"  TIMESTAMP      NOT NULL,
    UNIQUE ("GuildId", "UserId")
);

CREATE INDEX IF NOT EXISTS "IX_TrackedTimeouts_EndsAt" ON "TrackedTimeouts" ("EndsAt");
