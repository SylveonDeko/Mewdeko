-- Automod extras: a log channel, a review channel, server-wide exemptions, word filter normalisation, no-role-only
-- modes for the invite and link filters, link domain lists and per-channel link rate limits. Everything is off until a
-- server turns it on, except word normalisation.
-- Migration: 250-AddAutomodExtras.sql

CREATE TABLE IF NOT EXISTS "AutomodExtraSettings"
(
    "Id"                SERIAL PRIMARY KEY,
    "GuildId"           NUMERIC(20, 0) NOT NULL UNIQUE,
    "LogChannelId"      NUMERIC(20, 0),
    "ReviewChannelId"   NUMERIC(20, 0),
    "ExemptRoleIds"     TEXT           NOT NULL DEFAULT '',
    "ExemptChannelIds"  TEXT           NOT NULL DEFAULT '',
    "NormalizeWords"    BOOLEAN        NOT NULL DEFAULT TRUE,
    "InviteNoRolesOnly" BOOLEAN        NOT NULL DEFAULT FALSE,
    "LinkNoRolesOnly"   BOOLEAN        NOT NULL DEFAULT FALSE,
    "LinkAllowedDomains" TEXT          NOT NULL DEFAULT '',
    "LinkBlockedDomains" TEXT          NOT NULL DEFAULT '',
    "DateAdded"         TIMESTAMP      NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS "AutomodLinkLimits"
(
    "Id"            SERIAL PRIMARY KEY,
    "GuildId"       NUMERIC(20, 0) NOT NULL,
    "ChannelId"     NUMERIC(20, 0) NOT NULL,
    "MaxLinks"      INTEGER        NOT NULL DEFAULT 3,
    "WindowSeconds" INTEGER        NOT NULL DEFAULT 30,
    "DateAdded"     TIMESTAMP      NOT NULL DEFAULT NOW(),
    UNIQUE ("GuildId", "ChannelId")
);
