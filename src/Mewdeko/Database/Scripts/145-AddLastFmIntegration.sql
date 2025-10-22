-- Add LastFm integration support

CREATE TABLE IF NOT EXISTS "LastFmUsers"
(
    "Id"                SERIAL PRIMARY KEY,
    "UserId"            NUMERIC(20, 0) NOT NULL UNIQUE,
    "SessionKey"        TEXT           NOT NULL,
    "Username"          TEXT           NOT NULL,
    "ScrobblingEnabled" BOOLEAN        NOT NULL        DEFAULT TRUE,
    "DateAdded"         TIMESTAMP(6) WITHOUT TIME ZONE DEFAULT (NOW() AT TIME ZONE 'UTC')
);

CREATE INDEX IF NOT EXISTS "IX_LastFmUsers_UserId" ON "LastFmUsers" ("UserId");
