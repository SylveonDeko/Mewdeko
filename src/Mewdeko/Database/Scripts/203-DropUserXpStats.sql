-- Drops the dead "UserXpStats" table, superseded by "GuildUserXp". Production shows 1.85M rows
-- across 235 MB with idx_scan = 0 on every index and no autovacuum since April 2025, so nothing has
-- read or written it since the migration to "GuildUserXp".

DROP TABLE IF EXISTS "UserXpStats";
