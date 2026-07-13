CREATE TABLE IF NOT EXISTS "TwitchTimers"
(
    "Id"                   SERIAL PRIMARY KEY,
    "GuildId"              NUMERIC(20, 0) NOT NULL,
    "Name"                 TEXT           NOT NULL,
    "Messages"             TEXT           NOT NULL,
    "IntervalMinutes"      INTEGER        NOT NULL DEFAULT 10,
    "MinChatMessages"      INTEGER        NOT NULL DEFAULT 5,
    "OnlineOnly"           BOOLEAN        NOT NULL DEFAULT TRUE,
    "RandomizeMessages"    BOOLEAN        NOT NULL DEFAULT FALSE,
    "Enabled"              BOOLEAN        NOT NULL DEFAULT TRUE,
    "LastMessageIndex"     INTEGER        NOT NULL DEFAULT 0,
    "LastChatMessageCount" INTEGER        NOT NULL DEFAULT 0,
    "LastSentAt"           TIMESTAMP      NULL,
    "DateAdded"            TIMESTAMP      NULL,
    "LastUpdatedAt"        TIMESTAMP      NULL,
    UNIQUE ("GuildId", "Name")
);

CREATE INDEX IF NOT EXISTS "IX_TwitchTimers_GuildId_Name"
    ON "TwitchTimers" ("GuildId", "Name");
