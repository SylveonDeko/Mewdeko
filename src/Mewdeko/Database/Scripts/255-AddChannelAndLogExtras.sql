-- Channel and log extras: server templates, auto reaction priority, cleanup and manager role, and more log types.
-- Migration: 255-AddChannelAndLogExtras.sql

CREATE TABLE IF NOT EXISTS "ServerTemplates"
(
    "Id"           SERIAL PRIMARY KEY,
    "UserId"       NUMERIC(20, 0) NOT NULL,
    "Name"         TEXT           NOT NULL,
    "Data"         BYTEA          NOT NULL,
    "SizeBytes"    INTEGER        NOT NULL DEFAULT 0,
    "RoleCount"    INTEGER        NOT NULL DEFAULT 0,
    "ChannelCount" INTEGER        NOT NULL DEFAULT 0,
    "DateAdded"    TIMESTAMP      NOT NULL DEFAULT NOW(),
    UNIQUE ("UserId", "Name")
);

ALTER TABLE "AutoReactions"
    ADD COLUMN IF NOT EXISTS "Priority" INTEGER NOT NULL DEFAULT 0;
ALTER TABLE "AutoReactions"
    ADD COLUMN IF NOT EXISTS "DeleteNonMatching" BOOLEAN NOT NULL DEFAULT FALSE;

CREATE TABLE IF NOT EXISTS "AutoReactionSettings"
(
    "Id"            SERIAL PRIMARY KEY,
    "GuildId"       NUMERIC(20, 0) NOT NULL UNIQUE,
    "ManagerRoleId" NUMERIC(20, 0)
);

ALTER TABLE "LoggingV2"
    ADD COLUMN IF NOT EXISTS "ChannelPermissionsUpdatedId" NUMERIC(20, 0);
ALTER TABLE "LoggingV2"
    ADD COLUMN IF NOT EXISTS "ServerBoostedId" NUMERIC(20, 0);
ALTER TABLE "LoggingV2"
    ADD COLUMN IF NOT EXISTS "IntegrationChangedId" NUMERIC(20, 0);
ALTER TABLE "LoggingV2"
    ADD COLUMN IF NOT EXISTS "WebhookChangedId" NUMERIC(20, 0);
ALTER TABLE "LoggingV2"
    ADD COLUMN IF NOT EXISTS "StageChangedId" NUMERIC(20, 0);
ALTER TABLE "LoggingV2"
    ADD COLUMN IF NOT EXISTS "ScheduledEventChangedId" NUMERIC(20, 0);
ALTER TABLE "LoggingV2"
    ADD COLUMN IF NOT EXISTS "AutoModRuleChangedId" NUMERIC(20, 0);
ALTER TABLE "LoggingV2"
    ADD COLUMN IF NOT EXISTS "SoundboardChangedId" NUMERIC(20, 0);
ALTER TABLE "LoggingV2"
    ADD COLUMN IF NOT EXISTS "ThreadMemberChangedId" NUMERIC(20, 0);
