ALTER TABLE "ChatTriggers"
    ADD COLUMN IF NOT EXISTS "ReplyToTrigger"      BOOLEAN NOT NULL DEFAULT FALSE,
    ADD COLUMN IF NOT EXISTS "DeleteResponseAfter" INTEGER NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS "CooldownSeconds"     INTEGER NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS "CooldownScope"       INTEGER NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS "CounterName"         TEXT    NULL,
    ADD COLUMN IF NOT EXISTS "CounterMin"          BIGINT  NULL,
    ADD COLUMN IF NOT EXISTS "CounterMax"          BIGINT  NULL,
    ADD COLUMN IF NOT EXISTS "Category"            TEXT    NULL;

CREATE INDEX IF NOT EXISTS "IX_ChatTriggers_Guild_Category"
    ON "ChatTriggers" ("GuildId", "Category");
