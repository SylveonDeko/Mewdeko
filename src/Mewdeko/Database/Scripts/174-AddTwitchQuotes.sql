CREATE TABLE IF NOT EXISTS "TwitchQuotes"
(
    "Id"        SERIAL PRIMARY KEY,
    "GuildId"   NUMERIC(20, 0) NOT NULL,
    "Text"      TEXT           NOT NULL,
    "Author"    TEXT           NULL,
    "AddedBy"   TEXT           NULL,
    "DateAdded" TIMESTAMP      NOT NULL
);

CREATE INDEX IF NOT EXISTS "IX_TwitchQuotes_GuildId"
    ON "TwitchQuotes" ("GuildId");
