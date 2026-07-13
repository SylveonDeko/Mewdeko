CREATE TABLE IF NOT EXISTS "TwitchEventHistory"
(
    "Id"        SERIAL PRIMARY KEY,
    "GuildId"   NUMERIC(20, 0) NOT NULL,
    "EventType" TEXT           NOT NULL,
    "Source"    TEXT           NOT NULL,
    "Succeeded" BOOLEAN        NOT NULL DEFAULT TRUE,
    "Message"   TEXT           NOT NULL,
    "Error"     TEXT           NULL,
    "DateAdded" TIMESTAMP      NOT NULL
);

CREATE INDEX IF NOT EXISTS "IX_TwitchEventHistory_GuildId_DateAdded"
    ON "TwitchEventHistory" ("GuildId", "DateAdded");
