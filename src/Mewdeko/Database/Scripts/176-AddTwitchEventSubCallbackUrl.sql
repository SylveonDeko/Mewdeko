ALTER TABLE "TwitchEventSubSubscriptions"
    ADD COLUMN IF NOT EXISTS "CallbackUrl" TEXT NULL;

CREATE INDEX IF NOT EXISTS "IX_TwitchEventSubSubscriptions_CallbackUrl"
    ON "TwitchEventSubSubscriptions" ("CallbackUrl");
