-- Invite milestone roles (roles earned at a number of net invites) and achievement extras: new activity counters, a
-- target role for role achievements, per achievement unlock messages, a role reward that takes a role away, and the
-- one time "almost there" DM.
-- Migration: 260-AddInviteRolesAndAchievementExtras.sql

CREATE TABLE IF NOT EXISTS "InviteRoleSettings"
(
    "Id"           SERIAL PRIMARY KEY,
    "GuildId"      NUMERIC(20, 0) NOT NULL UNIQUE,
    "Mode"         INTEGER        NOT NULL DEFAULT 0,
    "RemoveOnDrop" BOOLEAN        NOT NULL DEFAULT FALSE
);

CREATE TABLE IF NOT EXISTS "InviteRoles"
(
    "Id"              SERIAL PRIMARY KEY,
    "GuildId"         NUMERIC(20, 0) NOT NULL,
    "RoleId"          NUMERIC(20, 0) NOT NULL,
    "RequiredInvites" INTEGER        NOT NULL,
    "DateAdded"       TIMESTAMP      NOT NULL DEFAULT NOW(),
    UNIQUE ("GuildId", "RoleId")
);

CREATE INDEX IF NOT EXISTS "IX_InviteRoles_GuildId_RequiredInvites" ON "InviteRoles" ("GuildId", "RequiredInvites");

ALTER TABLE "AchievementSettings"
    ADD COLUMN IF NOT EXISTS "AlmostThereDm" BOOLEAN NOT NULL DEFAULT FALSE;

ALTER TABLE "CustomAchievements"
    ADD COLUMN IF NOT EXISTS "TargetRoleId" NUMERIC(20, 0);
ALTER TABLE "CustomAchievements"
    ADD COLUMN IF NOT EXISTS "RoleRemoveId" NUMERIC(20, 0);
ALTER TABLE "CustomAchievements"
    ADD COLUMN IF NOT EXISTS "UnlockMessage" TEXT;

ALTER TABLE "AchievementOverrides"
    ADD COLUMN IF NOT EXISTS "RoleRemoveId" NUMERIC(20, 0);
ALTER TABLE "AchievementOverrides"
    ADD COLUMN IF NOT EXISTS "UnlockMessage" TEXT;

ALTER TABLE "AchievementMembers"
    ADD COLUMN IF NOT EXISTS "ThreadsCreated" BIGINT NOT NULL DEFAULT 0;
ALTER TABLE "AchievementMembers"
    ADD COLUMN IF NOT EXISTS "ThreadsJoined" BIGINT NOT NULL DEFAULT 0;
ALTER TABLE "AchievementMembers"
    ADD COLUMN IF NOT EXISTS "GiveawaysEntered" BIGINT NOT NULL DEFAULT 0;
ALTER TABLE "AchievementMembers"
    ADD COLUMN IF NOT EXISTS "ShopPurchases" BIGINT NOT NULL DEFAULT 0;

CREATE TABLE IF NOT EXISTS "AchievementAlmostThere"
(
    "GuildId"        NUMERIC(20, 0) NOT NULL,
    "UserId"         NUMERIC(20, 0) NOT NULL,
    "AchievementKey" TEXT           NOT NULL,
    "NotifiedAt"     TIMESTAMP      NOT NULL DEFAULT NOW(),
    PRIMARY KEY ("GuildId", "UserId", "AchievementKey")
);
