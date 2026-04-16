CREATE TABLE IF NOT EXISTS "StatChannels"
(
    "Id"            SERIAL PRIMARY KEY,
    "GuildId"       NUMERIC(20, 0)              NOT NULL,
    "ChannelId"     NUMERIC(20, 0)              NOT NULL,
    "StatType"      INTEGER                     NOT NULL DEFAULT 0,
    "Template"      TEXT                        NOT NULL DEFAULT '{count}',
    "RoleId"        NUMERIC(20, 0)              NULL,
    "CountdownDate" TIMESTAMP WITHOUT TIME ZONE NULL,
    "GoalTarget"    INTEGER                     NOT NULL DEFAULT 0,
    "DateAdded"     TIMESTAMP WITHOUT TIME ZONE NULL
);

CREATE INDEX IF NOT EXISTS "IX_StatChannels_GuildId"
    ON "StatChannels" ("GuildId");

CREATE UNIQUE INDEX IF NOT EXISTS "IX_StatChannels_ChannelId"
    ON "StatChannels" ("ChannelId");
