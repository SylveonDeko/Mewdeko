-- Role menu groups link several menus so a member holds one role, or a shared maximum, across all of them. Menus also
-- get an accent colour for the default embed and a default button colour.
-- Migration: 251-AddRoleMenuGroups.sql

CREATE TABLE IF NOT EXISTS "RoleMenuGroups"
(
    "Id"        SERIAL PRIMARY KEY,
    "GuildId"   NUMERIC(20, 0) NOT NULL,
    "Name"      TEXT           NOT NULL,
    "MaxRoles"  INTEGER        NOT NULL DEFAULT 1,
    "DateAdded" TIMESTAMP      NOT NULL DEFAULT NOW(),
    UNIQUE ("GuildId", "Name")
);

ALTER TABLE "RoleMenus"
    ADD COLUMN IF NOT EXISTS "GroupId" INTEGER REFERENCES "RoleMenuGroups" ("Id") ON DELETE SET NULL;
ALTER TABLE "RoleMenus"
    ADD COLUMN IF NOT EXISTS "AccentColor" INTEGER;
ALTER TABLE "RoleMenus"
    ADD COLUMN IF NOT EXISTS "ButtonColor" INTEGER NOT NULL DEFAULT 0;

CREATE INDEX IF NOT EXISTS "IX_RoleMenus_GroupId" ON "RoleMenus" ("GroupId") WHERE "GroupId" IS NOT NULL;
