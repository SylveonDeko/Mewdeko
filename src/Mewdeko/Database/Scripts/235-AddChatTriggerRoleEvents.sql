-- Role filter for the RoleAdded and RoleRemoved chat trigger events. Zero matches every role.
-- Migration: 235-AddChatTriggerRoleEvents.sql

ALTER TABLE "ChatTriggers"
    ADD COLUMN IF NOT EXISTS "EventRoleId" NUMERIC(20, 0) NOT NULL DEFAULT 0;
