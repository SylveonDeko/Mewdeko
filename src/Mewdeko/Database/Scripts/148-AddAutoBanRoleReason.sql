-- Add Reason column to AutoBanRoles table for custom ban reasons in audit log
ALTER TABLE IF EXISTS "AutoBanRoles"
    ADD COLUMN IF NOT EXISTS "Reason" TEXT NULL;
