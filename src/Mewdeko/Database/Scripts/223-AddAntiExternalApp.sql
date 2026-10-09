-- Anti-external-app protection: catches messages sent through apps members have installed on their own account, which
-- post as the app and so slip past every check that looks at the message author. Off until a server turns it on.
-- Migration: 223-AddAntiExternalApp.sql

CREATE TABLE IF NOT EXISTS "AntiExternalAppSettings"
(
    "Id"                SERIAL PRIMARY KEY,
    "GuildId"           NUMERIC(20, 0) NOT NULL UNIQUE,
    "Action"            INTEGER        NOT NULL DEFAULT 10,
    "PunishDuration"    INTEGER        NOT NULL DEFAULT 60,
    "RoleId"            NUMERIC(20, 0) NULL,
    "MentionThreshold"  INTEGER        NOT NULL DEFAULT 5,
    "BlockInvites"      BOOLEAN        NOT NULL DEFAULT TRUE,
    "MaxMessages"       INTEGER        NOT NULL DEFAULT 5,
    "TimeWindowSeconds" INTEGER        NOT NULL DEFAULT 10,
    "DeleteMessages"    BOOLEAN        NOT NULL DEFAULT TRUE,
    "NotifyUser"        BOOLEAN        NOT NULL DEFAULT TRUE,
    "TotalTriggers"     INTEGER        NOT NULL DEFAULT 0,
    "DateAdded"         TIMESTAMP      NOT NULL DEFAULT NOW()
);
