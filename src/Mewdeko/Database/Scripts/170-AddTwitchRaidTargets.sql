CREATE TABLE IF NOT EXISTS "TwitchRaidTargets"
(
    "Id"            SERIAL PRIMARY KEY,
    "GuildId"       NUMERIC(20, 0) NOT NULL,
    "TwitchLogin"   TEXT           NOT NULL,
    "Note"          TEXT           NULL,
    "Enabled"       BOOLEAN        NOT NULL DEFAULT TRUE,
    "DateAdded"     TIMESTAMP      NULL,
    "LastUpdatedAt" TIMESTAMP      NULL,
    UNIQUE ("GuildId", "TwitchLogin")
);

CREATE INDEX IF NOT EXISTS "IX_TwitchRaidTargets_GuildId"
    ON "TwitchRaidTargets" ("GuildId");
