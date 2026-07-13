-- The crop flag promised more than it could deliver. PDQ moves about 30 of its 256 bits for a 1%
-- crop, so a fixed set of guessed crops only ever catches the crops it guessed. What can be undone
-- exactly is a solid border, because it can be measured rather than guessed, so the setting now
-- means "strip a border before matching".

ALTER TABLE "AntiImageHashSettings"
    ADD COLUMN IF NOT EXISTS "CheckBorders" BOOLEAN NOT NULL DEFAULT TRUE;

ALTER TABLE "AntiImageHashSettings"
    DROP COLUMN IF EXISTS "CheckCrops";
