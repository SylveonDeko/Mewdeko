-- Reason aliases staff can use in place of a full punishment reason, and custom DMs sent to members per punishment action.
-- Migration: 249-AddModerationExtras.sql

CREATE TABLE IF NOT EXISTS "ModReasonAliases"
(
    "Id"        SERIAL PRIMARY KEY,
    "GuildId"   NUMERIC(20, 0) NOT NULL,
    "Name"      TEXT           NOT NULL,
    "Reason"    TEXT           NOT NULL,
    "DateAdded" TIMESTAMP      NOT NULL DEFAULT NOW(),
    UNIQUE ("GuildId", "Name")
);

CREATE TABLE IF NOT EXISTS "PunishDmTemplates"
(
    "Id"       SERIAL PRIMARY KEY,
    "GuildId"  NUMERIC(20, 0) NOT NULL,
    "Action"   INTEGER        NOT NULL,
    "Template" TEXT           NOT NULL,
    UNIQUE ("GuildId", "Action")
);
