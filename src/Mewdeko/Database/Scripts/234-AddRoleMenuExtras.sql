-- Role menu extras: temporary roles, blocked roles, self-destructing menus, per-role holder caps, and the grants that
-- remove temporary roles when they run out.
-- Migration: 234-AddRoleMenuExtras.sql

ALTER TABLE "RoleMenus"
    ADD COLUMN IF NOT EXISTS "TempRoleSeconds" INTEGER NOT NULL DEFAULT 0;
ALTER TABLE "RoleMenus"
    ADD COLUMN IF NOT EXISTS "BlockedRoleIds" TEXT NOT NULL DEFAULT '';
ALTER TABLE "RoleMenus"
    ADD COLUMN IF NOT EXISTS "ExpiresAt" TIMESTAMP;

ALTER TABLE "RoleMenuOptions"
    ADD COLUMN IF NOT EXISTS "TempRoleSeconds" INTEGER NOT NULL DEFAULT 0;
ALTER TABLE "RoleMenuOptions"
    ADD COLUMN IF NOT EXISTS "MaxHolders" INTEGER NOT NULL DEFAULT 0;

CREATE TABLE IF NOT EXISTS "RoleMenuTempGrants"
(
    "Id"        SERIAL PRIMARY KEY,
    "GuildId"   NUMERIC(20, 0) NOT NULL,
    "UserId"    NUMERIC(20, 0) NOT NULL,
    "RoleId"    NUMERIC(20, 0) NOT NULL,
    "MenuId"    INTEGER        NOT NULL,
    "ExpiresAt" TIMESTAMP      NOT NULL,
    UNIQUE ("GuildId", "UserId", "RoleId")
);

CREATE INDEX IF NOT EXISTS "IX_RoleMenuTempGrants_ExpiresAt" ON "RoleMenuTempGrants" ("ExpiresAt");

CREATE INDEX IF NOT EXISTS "IX_RoleMenus_ExpiresAt" ON "RoleMenus" ("ExpiresAt") WHERE "ExpiresAt" IS NOT NULL;
