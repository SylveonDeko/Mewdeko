-- Drops indexes that production statistics show have never been scanned (idx_scan = 0), reclaiming
-- roughly 1.1 GB and removing their write amplification from the insert paths.
--
-- "PK_MessageTimestamps" is by far the largest at 1018 MB. Nothing references
-- "MessageTimestamps"."Id" and no query looks a row up by it; the column stays an identity column
-- and LinqToDB still treats it as the key via its mapping attributes, so only the database-side
-- constraint goes away. Because it backs a constraint it cannot be dropped concurrently and takes a
-- brief ACCESS EXCLUSIVE lock, but the work is a catalogue update plus a file unlink, not a scan.

ALTER TABLE "MessageTimestamps"
    DROP CONSTRAINT IF EXISTS "PK_MessageTimestamps";

DROP INDEX CONCURRENTLY IF EXISTS "IX_StarboardReactions_MessageId_UserId_Emote";
DROP INDEX CONCURRENTLY IF EXISTS "IX_StarboardReactions_UserId";
DROP INDEX CONCURRENTLY IF EXISTS "IX_StarboardStats_MessageId_StarboardId_Emote";
DROP INDEX CONCURRENTLY IF EXISTS "IX_DiscordUser_TotalXp";
DROP INDEX CONCURRENTLY IF EXISTS "IX_DiscordUser_HasCompletedAnyWizard";
DROP INDEX CONCURRENTLY IF EXISTS "IX_DiscordUser_DashboardExperienceLevel";
