-- Indexes for the lookups that dominate sequential-scan time in production.
--
-- "MessageCounts" carried only a primary key on "Id" while every read filters on
-- (GuildId, ChannelId, UserId), producing ~30M sequential scans and 4.14 trillion tuples read.
-- "InvitedBy" and "CustomVoiceConfig" have the same shape at smaller scale.
--
-- The index on "MessageCounts" is unique because script 199 establishes that one row per
-- (GuildId, ChannelId, UserId) is the intended model, and ON CONFLICT in the get-or-create path
-- needs a unique constraint to target. "CustomVoiceConfig" stays non-unique: a guild may legitimately
-- run more than one hub voice channel, so its script 200 dedupe is a cleanup, not an invariant.
--
-- The unique index on "GuildXpSettings" ("GuildId") is what lets the get-or-create path switch to
-- INSERT ... ON CONFLICT DO NOTHING; without it the duplicates cleaned up in script 198 return.
--
-- This script runs outside a transaction (see DatabaseUpgrader.BuildConcurrentUpgrader). A failed
-- CREATE INDEX CONCURRENTLY leaves an INVALID index behind that IF NOT EXISTS would silently
-- accept, so any invalid leftovers are dropped before the build is retried.

DO
$$
    DECLARE
        idx TEXT;
    BEGIN
        FOR idx IN
            SELECT c.relname
            FROM pg_index i
                     JOIN pg_class c ON c.oid = i.indexrelid
                     JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE NOT i.indisvalid
              AND n.nspname = 'public'
              AND c.relname IN (
                                'IX_MessageCounts_GuildId_ChannelId_UserId',
                                'IX_MessageTimestamps_Timestamp',
                                'IX_InvitedBy_GuildId_UserId',
                                'IX_CustomVoiceConfig_GuildId',
                                'IX_GuildXpSettings_GuildId'
                )
            LOOP
                EXECUTE FORMAT('DROP INDEX IF EXISTS public.%I', idx);
            END LOOP;
    END
$$;

CREATE UNIQUE INDEX CONCURRENTLY IF NOT EXISTS "IX_MessageCounts_GuildId_ChannelId_UserId"
    ON "MessageCounts" ("GuildId", "ChannelId", "UserId");

CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_InvitedBy_GuildId_UserId"
    ON "InvitedBy" ("GuildId", "UserId");

CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_CustomVoiceConfig_GuildId"
    ON "CustomVoiceConfig" ("GuildId");

CREATE UNIQUE INDEX CONCURRENTLY IF NOT EXISTS "IX_GuildXpSettings_GuildId"
    ON "GuildXpSettings" ("GuildId");

-- Supports the retention sweep in MessageTimestampRetentionService, which deletes by age.
CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_MessageTimestamps_Timestamp"
    ON "MessageTimestamps" ("Timestamp");
