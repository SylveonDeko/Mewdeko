-- Role menus: posted messages with a dropdown or buttons that give and take roles, plus their options.
-- Migration: 219-RoleMenus.sql

CREATE TABLE IF NOT EXISTS "RoleMenus"
(
    "Id"             SERIAL PRIMARY KEY,
    "GuildId"        NUMERIC(20, 0)              NOT NULL,
    "Name"           TEXT                        NOT NULL,
    "ChannelId"      NUMERIC(20, 0)              NOT NULL,
    "MessageId"      NUMERIC(20, 0)              NULL,
    "Message"        TEXT                        NULL,
    "Style"          INTEGER                     NOT NULL DEFAULT 0,
    "Placeholder"    TEXT                        NULL,
    "Mode"           INTEGER                     NOT NULL DEFAULT 0,
    "MinRoles"       INTEGER                     NOT NULL DEFAULT 0,
    "MaxRoles"       INTEGER                     NOT NULL DEFAULT 0,
    "RequiredRoleId" NUMERIC(20, 0)              NULL,
    "ReplyMode"      INTEGER                     NOT NULL DEFAULT 0,
    "Enabled"        BOOLEAN                     NOT NULL DEFAULT TRUE,
    "CreatedBy"      NUMERIC(20, 0)              NOT NULL DEFAULT 0,
    "DateAdded"      TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    "DateModified"   TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc')
);

CREATE INDEX IF NOT EXISTS "IX_RoleMenus_GuildId"
    ON "RoleMenus" ("GuildId");

CREATE INDEX IF NOT EXISTS "IX_RoleMenus_ChannelId"
    ON "RoleMenus" ("ChannelId");

CREATE UNIQUE INDEX IF NOT EXISTS "IX_RoleMenus_MessageId"
    ON "RoleMenus" ("MessageId")
    WHERE "MessageId" IS NOT NULL;

CREATE TABLE IF NOT EXISTS "RoleMenuOptions"
(
    "Id"          SERIAL PRIMARY KEY,
    "RoleMenuId"  INTEGER                     NOT NULL REFERENCES "RoleMenus" ("Id") ON DELETE CASCADE,
    "RoleId"      NUMERIC(20, 0)              NOT NULL,
    "Label"       TEXT                        NOT NULL,
    "Emoji"       TEXT                        NULL,
    "Description" TEXT                        NULL,
    "ButtonStyle" INTEGER                     NOT NULL DEFAULT 2,
    "Position"    INTEGER                     NOT NULL DEFAULT 0,
    "DateAdded"   TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc')
);

CREATE INDEX IF NOT EXISTS "IX_RoleMenuOptions_RoleMenuId"
    ON "RoleMenuOptions" ("RoleMenuId", "Position");

CREATE INDEX IF NOT EXISTS "IX_RoleMenuOptions_RoleId"
    ON "RoleMenuOptions" ("RoleId");
