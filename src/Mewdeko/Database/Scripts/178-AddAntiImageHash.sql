CREATE TABLE IF NOT EXISTS "AntiImageHashSettings"
(
    "Id"             SERIAL PRIMARY KEY,
    "GuildId"        NUMERIC(20, 0) NOT NULL UNIQUE,
    "Action"         INTEGER        NOT NULL DEFAULT 2,
    "PunishDuration" INTEGER        NOT NULL DEFAULT 0,
    "RoleId"         NUMERIC(20, 0) NULL,
    "HashThreshold"  INTEGER        NOT NULL DEFAULT 6,
    "DeleteMessages" BOOLEAN        NOT NULL DEFAULT TRUE,
    "NotifyUser"     BOOLEAN        NOT NULL DEFAULT TRUE,
    "IgnoreBots"     BOOLEAN        NOT NULL DEFAULT TRUE,
    "CheckEmbeds"    BOOLEAN        NOT NULL DEFAULT TRUE,
    "MaxImageSizeMb" INTEGER        NOT NULL DEFAULT 8,
    "TotalTriggers"  INTEGER        NOT NULL DEFAULT 0,
    "DateAdded"      TIMESTAMP      NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS "BannedImageHashes"
(
    "Id"              SERIAL PRIMARY KEY,
    "GuildId"         NUMERIC(20, 0) NOT NULL,
    "Hash"            VARCHAR(16)    NOT NULL,
    "Name"            TEXT           NULL,
    "SourceUrl"       TEXT           NULL,
    "Action"          INTEGER        NULL,
    "PunishDuration"  INTEGER        NULL,
    "RoleId"          NUMERIC(20, 0) NULL,
    "HitCount"        INTEGER        NOT NULL DEFAULT 0,
    "LastTriggeredAt" TIMESTAMP      NULL,
    "AddedBy"         NUMERIC(20, 0) NULL,
    "DateAdded"       TIMESTAMP      NOT NULL DEFAULT NOW(),
    UNIQUE ("GuildId", "Hash")
);

CREATE TABLE IF NOT EXISTS "AntiImageHashIgnoredRoles"
(
    "Id"        SERIAL PRIMARY KEY,
    "GuildId"   NUMERIC(20, 0) NOT NULL,
    "RoleId"    NUMERIC(20, 0) NOT NULL,
    "DateAdded" TIMESTAMP      NOT NULL DEFAULT NOW(),
    UNIQUE ("GuildId", "RoleId")
);

CREATE TABLE IF NOT EXISTS "AntiImageHashIgnoredChannels"
(
    "Id"        SERIAL PRIMARY KEY,
    "GuildId"   NUMERIC(20, 0) NOT NULL,
    "ChannelId" NUMERIC(20, 0) NOT NULL,
    "DateAdded" TIMESTAMP      NOT NULL DEFAULT NOW(),
    UNIQUE ("GuildId", "ChannelId")
);

CREATE INDEX IF NOT EXISTS "IX_AntiImageHashSettings_GuildId"
    ON "AntiImageHashSettings" ("GuildId");
CREATE INDEX IF NOT EXISTS "IX_BannedImageHashes_GuildId"
    ON "BannedImageHashes" ("GuildId");
CREATE INDEX IF NOT EXISTS "IX_AntiImageHashIgnoredRoles_GuildId"
    ON "AntiImageHashIgnoredRoles" ("GuildId");
CREATE INDEX IF NOT EXISTS "IX_AntiImageHashIgnoredChannels_GuildId"
    ON "AntiImageHashIgnoredChannels" ("GuildId");
