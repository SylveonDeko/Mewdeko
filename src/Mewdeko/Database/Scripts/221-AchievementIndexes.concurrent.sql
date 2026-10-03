-- Achievements count each member's commands in a server, and "CommandStats" had no index on
-- (GuildId, UserId), so every count was a sequential scan of the whole table.
--
-- This script runs outside a transaction (see DatabaseUpgrader.BuildConcurrentUpgrader). A failed
-- CREATE INDEX CONCURRENTLY leaves an INVALID index behind that IF NOT EXISTS would silently
-- accept, so an invalid leftover is dropped before the build is retried.

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
              AND c.relname = 'IX_CommandStats_GuildId_UserId'
            LOOP
                EXECUTE FORMAT('DROP INDEX IF EXISTS public.%I', idx);
            END LOOP;
    END
$$;

CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_CommandStats_GuildId_UserId"
    ON "CommandStats" ("GuildId", "UserId");
