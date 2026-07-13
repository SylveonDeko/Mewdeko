-- Anti-Image-Hash moves from a 64 bit dHash to PDQ, Meta's 256 bit DCT hash, with mirrored and
-- cropped variant hashes stored alongside each blocked image so crops and flips still match.
-- Hashes from the two algorithms are not comparable, so any dHash era rows (16 hex characters)
-- are dropped rather than migrated.

ALTER TABLE "BannedImageHashes"
    ALTER COLUMN "Hash" TYPE VARCHAR(64);

ALTER TABLE "BannedImageHashes"
    ADD COLUMN IF NOT EXISTS "Variants" TEXT NULL;

ALTER TABLE "BannedImageHashes"
    ADD COLUMN IF NOT EXISTS "Quality" INTEGER NOT NULL DEFAULT 100;

DELETE
FROM "BannedImageHashes"
WHERE LENGTH("Hash") <> 64;

ALTER TABLE "AntiImageHashSettings"
    ADD COLUMN IF NOT EXISTS "CheckCrops" BOOLEAN NOT NULL DEFAULT TRUE;

-- Tolerance is now out of 256 bits rather than 64. PDQ's standard "same image" threshold is 31,
-- so anything still carrying a dHash era tolerance is moved onto that.
UPDATE "AntiImageHashSettings"
SET "HashThreshold" = 31
WHERE "HashThreshold" <= 20;

ALTER TABLE "AntiImageHashSettings"
    ALTER COLUMN "HashThreshold" SET DEFAULT 31;
