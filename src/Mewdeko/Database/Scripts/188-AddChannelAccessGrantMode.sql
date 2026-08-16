-- Channel access gates can grant access two ways: by handing out a role, or by writing a
-- per-user permission overwrite straight onto the locked channel. Role gates keep working
-- exactly as before, so existing rows default to the role mode.
-- Migration: 188-AddChannelAccessGrantMode.sql

ALTER TABLE "ChannelAccessConfigs"
    ADD COLUMN IF NOT EXISTS "GrantMode" INTEGER NOT NULL DEFAULT 0;
-- 0 = role, 1 = channel permission overwrite

-- Overwrite gates have no role to hand out.
ALTER TABLE "ChannelAccessConfigs"
    ALTER COLUMN "AccessRoleId" DROP NOT NULL;
