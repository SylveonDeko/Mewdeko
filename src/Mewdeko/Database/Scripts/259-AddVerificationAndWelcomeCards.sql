-- Verification gate: new members pass a captcha before they get access, and welcome cards: an image attached to greet
-- and bye messages. Both are off until a server turns them on.
-- Migration: 259-AddVerificationAndWelcomeCards.sql

CREATE TABLE IF NOT EXISTS "VerificationSettings"
(
    "Id"               SERIAL PRIMARY KEY,
    "GuildId"          NUMERIC(20, 0) NOT NULL UNIQUE,
    "Enabled"          BOOLEAN        NOT NULL DEFAULT FALSE,
    "ChannelId"        NUMERIC(20, 0),
    "MessageId"        NUMERIC(20, 0),
    "UnverifiedRoleId" NUMERIC(20, 0),
    "VerifiedRoleId"   NUMERIC(20, 0),
    "MaxAttempts"      INTEGER        NOT NULL DEFAULT 3,
    "TimeoutMinutes"   INTEGER        NOT NULL DEFAULT 10,
    "KickOnFail"       BOOLEAN        NOT NULL DEFAULT FALSE,
    "LogChannelId"     NUMERIC(20, 0),
    "DateAdded"        TIMESTAMP      NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS "VerificationPermissionBackups"
(
    "Id"            SERIAL PRIMARY KEY,
    "GuildId"       NUMERIC(20, 0) NOT NULL,
    "ChannelId"     NUMERIC(20, 0) NOT NULL,
    "RoleId"        NUMERIC(20, 0) NOT NULL,
    "HadOverwrite"  BOOLEAN        NOT NULL DEFAULT FALSE,
    "PreviousAllow" NUMERIC(20, 0) NOT NULL DEFAULT 0,
    "PreviousDeny"  NUMERIC(20, 0) NOT NULL DEFAULT 0,
    UNIQUE ("GuildId", "ChannelId", "RoleId")
);

CREATE INDEX IF NOT EXISTS "IX_VerificationPermissionBackups_GuildId" ON "VerificationPermissionBackups" ("GuildId");

CREATE TABLE IF NOT EXISTS "WelcomeCardSettings"
(
    "Id"              SERIAL PRIMARY KEY,
    "GuildId"         NUMERIC(20, 0) NOT NULL,
    "Target"          INTEGER        NOT NULL DEFAULT 0,
    "Enabled"         BOOLEAN        NOT NULL DEFAULT FALSE,
    "BackgroundColor" TEXT           NOT NULL DEFAULT '#23272A',
    "BackgroundData"  BYTEA,
    "OverlayOpacity"  INTEGER        NOT NULL DEFAULT 40,
    "Title"           TEXT           NOT NULL DEFAULT 'Welcome, %user.name%',
    "Subtitle"        TEXT           NOT NULL DEFAULT 'You are member #%server.members% of %server.name%',
    "FontName"        TEXT           NOT NULL DEFAULT 'noto-bold',
    "TextColor"       TEXT           NOT NULL DEFAULT '#FFFFFF',
    "AvatarShape"     INTEGER        NOT NULL DEFAULT 0,
    "DateAdded"       TIMESTAMP      NOT NULL DEFAULT NOW(),
    UNIQUE ("GuildId", "Target")
);
