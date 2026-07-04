CREATE TABLE IF NOT EXISTS "LockdownJoinSettings"
(
    "Id"               SERIAL PRIMARY KEY,
    "GuildId"          NUMERIC(20, 0) NOT NULL UNIQUE,
    "PunishmentAction" INTEGER        NOT NULL,
    "DateAdded"        TIMESTAMP      NULL,
    "DateUpdated"      TIMESTAMP      NULL
);
