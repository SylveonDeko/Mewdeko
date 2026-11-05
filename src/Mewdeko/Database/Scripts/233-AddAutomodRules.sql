-- Automod rules: caps filter, attachment spam, media-only channels and unsafe files, each with a list of actions,
-- plus message deletion, DMs and exemptions for the existing protections. Everything is off until a server turns it on.
-- Migration: 233-AddAutomodRules.sql

CREATE TABLE IF NOT EXISTS "AutomodRuleSettings"
(
    "Id"                SERIAL PRIMARY KEY,
    "GuildId"           NUMERIC(20, 0) NOT NULL,
    "Rule"              INTEGER        NOT NULL,
    "Enabled"           BOOLEAN        NOT NULL DEFAULT FALSE,
    "Actions"           INTEGER        NOT NULL DEFAULT 1,
    "ActionMinutes"     INTEGER        NOT NULL DEFAULT 10,
    "ActionMessage"     TEXT,
    "ExemptRoleIds"     TEXT           NOT NULL DEFAULT '',
    "ExemptChannelIds"  TEXT           NOT NULL DEFAULT '',
    "MinLength"         INTEGER        NOT NULL DEFAULT 20,
    "CapsPercent"       INTEGER        NOT NULL DEFAULT 70,
    "MaxAttachments"    INTEGER        NOT NULL DEFAULT 5,
    "WindowSeconds"     INTEGER        NOT NULL DEFAULT 10,
    "MediaChannelIds"   TEXT           NOT NULL DEFAULT '',
    "AllowThreads"      BOOLEAN        NOT NULL DEFAULT FALSE,
    "Extensions"        TEXT,
    "DateAdded"         TIMESTAMP      NOT NULL DEFAULT NOW(),
    UNIQUE ("GuildId", "Rule")
);

ALTER TABLE "AntiSpamSetting"
    ADD COLUMN IF NOT EXISTS "DeleteMessage" BOOLEAN NOT NULL DEFAULT FALSE,
    ADD COLUMN IF NOT EXISTS "NotifyUser" BOOLEAN NOT NULL DEFAULT FALSE;

ALTER TABLE "AntiMassMentionSetting"
    ADD COLUMN IF NOT EXISTS "DeleteMessage" BOOLEAN NOT NULL DEFAULT FALSE,
    ADD COLUMN IF NOT EXISTS "NotifyUser" BOOLEAN NOT NULL DEFAULT FALSE,
    ADD COLUMN IF NOT EXISTS "ExemptRoleIds" TEXT NOT NULL DEFAULT '',
    ADD COLUMN IF NOT EXISTS "ExemptChannelIds" TEXT NOT NULL DEFAULT '';

ALTER TABLE "AntiPatternSetting"
    ADD COLUMN IF NOT EXISTS "NotifyUser" BOOLEAN NOT NULL DEFAULT FALSE,
    ADD COLUMN IF NOT EXISTS "ExemptRoleIds" TEXT NOT NULL DEFAULT '';
