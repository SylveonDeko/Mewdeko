CREATE TABLE IF NOT EXISTS "ChatTriggerCounters"
(
    "Id"        SERIAL PRIMARY KEY,
    "GuildId"   NUMERIC(20, 0)              NOT NULL,
    "Name"      TEXT                        NOT NULL,
    "UserId"    NUMERIC(20, 0)              NOT NULL DEFAULT 0,
    "Value"     BIGINT                      NOT NULL DEFAULT 0,
    "DateAdded" TIMESTAMP WITHOUT TIME ZONE NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_ChatTriggerCounters_Guild_Name_User"
    ON "ChatTriggerCounters" ("GuildId", "Name", "UserId");
