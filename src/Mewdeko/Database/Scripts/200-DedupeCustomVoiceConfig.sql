-- Removes duplicate "CustomVoiceConfig" rows, one config per guild. The highest "Id" wins so the
-- most recently written config survives. Discarded rows are copied to
-- "CustomVoiceConfig_DuplicateBackup" first.

CREATE TABLE IF NOT EXISTS "CustomVoiceConfig_DuplicateBackup"
(
    LIKE "CustomVoiceConfig"
);

ALTER TABLE "CustomVoiceConfig_DuplicateBackup"
    ADD COLUMN IF NOT EXISTS "BackedUpAt" TIMESTAMP NOT NULL DEFAULT NOW();

WITH ranked AS (SELECT "Id",
                       ROW_NUMBER() OVER (PARTITION BY "GuildId" ORDER BY "Id" DESC) AS rn
                FROM "CustomVoiceConfig")
INSERT
INTO "CustomVoiceConfig_DuplicateBackup"
SELECT c.*, NOW()
FROM "CustomVoiceConfig" c
         JOIN ranked r ON r."Id" = c."Id"
WHERE r.rn > 1;

DELETE
FROM "CustomVoiceConfig"
WHERE "Id" IN (SELECT "Id" FROM "CustomVoiceConfig_DuplicateBackup");
