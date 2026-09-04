-- Removes duplicate "GuildXpSettings" rows left behind by an unsynchronised get-or-create
-- in XpCacheManager.GetGuildXpSettingsAsync. Discarded rows are copied to
-- "GuildXpSettings_DuplicateBackup" first so the merge can be audited or reversed.
--
-- Survivor rule: the most-customised row wins, scored as the number of columns deviating
-- from the defaults the create path writes. Ties break on the highest "Id".

CREATE TABLE IF NOT EXISTS "GuildXpSettings_DuplicateBackup"
(
    LIKE "GuildXpSettings"
);

ALTER TABLE "GuildXpSettings_DuplicateBackup"
    ADD COLUMN IF NOT EXISTS "BackedUpAt" TIMESTAMP NOT NULL DEFAULT NOW();

WITH ranked AS (SELECT "Id",
                       ROW_NUMBER() OVER (
                           PARTITION BY "GuildId"
                           ORDER BY (CASE WHEN "XpPerMessage" <> 3 THEN 1 ELSE 0 END) +
                                    (CASE WHEN "MessageXpCooldown" <> 60 THEN 1 ELSE 0 END) +
                                    (CASE WHEN "VoiceXpPerMinute" <> 2 THEN 1 ELSE 0 END) +
                                    (CASE WHEN "VoiceXpTimeout" <> 60 THEN 1 ELSE 0 END) +
                                    (CASE WHEN "XpMultiplier" <> 1.0 THEN 1 ELSE 0 END) +
                                    (CASE WHEN "XpCurveType" <> 0 THEN 1 ELSE 0 END) +
                                    (CASE WHEN "FirstMessageBonus" <> 0 THEN 1 ELSE 0 END) DESC,
                               "Id" DESC
                           ) AS rn
                FROM "GuildXpSettings")
INSERT
INTO "GuildXpSettings_DuplicateBackup"
SELECT g.*, NOW()
FROM "GuildXpSettings" g
         JOIN ranked r ON r."Id" = g."Id"
WHERE r.rn > 1;

DELETE
FROM "GuildXpSettings"
WHERE "Id" IN (SELECT "Id" FROM "GuildXpSettings_DuplicateBackup");
