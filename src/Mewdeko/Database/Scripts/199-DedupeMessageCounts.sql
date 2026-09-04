-- Merges duplicate "MessageCounts" rows left behind by an unsynchronised get-or-create in
-- MessageCountService.GetOrCreateMessageCountAsync. Unlike the XP settings dedupe, no counter
-- is discarded: the survivor absorbs the sum of its duplicates and adopts their child
-- "MessageTimestamps" rows before the losers are deleted.
--
-- The survivor is the lowest "Id" in each (GuildId, ChannelId, UserId) group, so the oldest
-- record keeps its identity and any external reference to it stays valid.

CREATE TEMP TABLE message_count_merge ON COMMIT DROP AS
WITH ranked AS (SELECT "Id",
                       "Count",
                       FIRST_VALUE("Id") OVER (
                           PARTITION BY "GuildId", "ChannelId", "UserId"
                           ORDER BY "Id"
                           ) AS keeper_id
                FROM "MessageCounts")
SELECT "Id" AS loser_id, keeper_id, "Count" AS loser_count
FROM ranked
WHERE "Id" <> keeper_id;

UPDATE "MessageCounts" m
SET "Count" = m."Count" + agg.total
FROM (SELECT keeper_id, SUM(loser_count) AS total
      FROM message_count_merge
      GROUP BY keeper_id) agg
WHERE m."Id" = agg.keeper_id;

UPDATE "MessageTimestamps" t
SET "MessageCountId" = merge.keeper_id
FROM message_count_merge merge
WHERE t."MessageCountId" = merge.loser_id;

DELETE
FROM "MessageCounts"
WHERE "Id" IN (SELECT loser_id FROM message_count_merge);
