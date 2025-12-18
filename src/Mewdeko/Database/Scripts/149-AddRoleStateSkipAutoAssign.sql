-- Add Skip Auto Assign Roles Support for Role States
-- This migration adds the ability to skip auto-assign roles for users who have saved role states

-- Add SkipAutoAssignRoles column to RoleStateSettings table
ALTER TABLE "RoleStateSettings"
    ADD COLUMN IF NOT EXISTS "SkipAutoAssignRoles" boolean NOT NULL DEFAULT FALSE;
