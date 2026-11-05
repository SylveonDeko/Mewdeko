-- Per-bridge options: which way messages flow, who is relayed outward, what is dropped and how incoming authors are named.
-- Migration: 230-AddChannelBridgeOptions.sql

ALTER TABLE "ChannelBridges"
    ADD COLUMN IF NOT EXISTS "Direction" INTEGER NOT NULL DEFAULT 0;

ALTER TABLE "ChannelBridges"
    ADD COLUMN IF NOT EXISTS "AllowedRoleIds" TEXT NOT NULL DEFAULT '';

ALTER TABLE "ChannelBridges"
    ADD COLUMN IF NOT EXISTS "BlockedRoleIds" TEXT NOT NULL DEFAULT '';

ALTER TABLE "ChannelBridges"
    ADD COLUMN IF NOT EXISTS "BlockedUserIds" TEXT NOT NULL DEFAULT '';

ALTER TABLE "ChannelBridges"
    ADD COLUMN IF NOT EXISTS "MinMemberAgeMinutes" INTEGER NOT NULL DEFAULT 0;

ALTER TABLE "ChannelBridges"
    ADD COLUMN IF NOT EXISTS "BlockInvites" BOOLEAN NOT NULL DEFAULT TRUE;

ALTER TABLE "ChannelBridges"
    ADD COLUMN IF NOT EXISTS "UseWordFilter" BOOLEAN NOT NULL DEFAULT TRUE;

ALTER TABLE "ChannelBridges"
    ADD COLUMN IF NOT EXISTS "NameFormat" TEXT NOT NULL DEFAULT '{name}';
