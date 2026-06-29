CREATE TABLE IF NOT EXISTS "TwitchGuildConfigs"
(
    "Id"              SERIAL PRIMARY KEY,
    "GuildId"         NUMERIC(20, 0) NOT NULL UNIQUE,
    "TwitchChannel"   TEXT           NOT NULL,
    "CommandPrefix"   TEXT           NOT NULL DEFAULT '!',
    "Enabled"         BOOLEAN        NOT NULL DEFAULT TRUE,
    "GoLiveChannelId" NUMERIC(20, 0) NULL,
    "GoLiveMessage"   TEXT           NULL,
    "Language"        TEXT           NULL,
    "DateAdded"       TIMESTAMP      NULL
);

CREATE TABLE IF NOT EXISTS "TwitchAccountLinks"
(
    "Id"             SERIAL PRIMARY KEY,
    "GuildId"        NUMERIC(20, 0) NOT NULL,
    "DiscordUserId"  NUMERIC(20, 0) NOT NULL,
    "TwitchUsername" TEXT           NOT NULL,
    "DateAdded"      TIMESTAMP      NULL,
    UNIQUE ("GuildId", "DiscordUserId"),
    UNIQUE ("GuildId", "TwitchUsername")
);

CREATE INDEX IF NOT EXISTS "IX_TwitchAccountLinks_GuildId_TwitchUsername"
    ON "TwitchAccountLinks" ("GuildId", "TwitchUsername");
