CREATE TABLE IF NOT EXISTS "TwitchRedemptionActions"
(
    "Id"               SERIAL PRIMARY KEY,
    "GuildId"          NUMERIC(20, 0) NOT NULL,
    "RewardTitle"      TEXT           NOT NULL,
    "TwitchResponse"   TEXT           NULL,
    "DiscordChannelId" NUMERIC(20, 0) NULL,
    "DiscordMessage"   TEXT           NULL,
    "Enabled"          BOOLEAN        NOT NULL DEFAULT TRUE,
    "DateAdded"        TIMESTAMP      NULL,
    "LastUpdatedAt"    TIMESTAMP      NULL,
    UNIQUE ("GuildId", "RewardTitle")
);

CREATE INDEX IF NOT EXISTS "IX_TwitchRedemptionActions_GuildId_RewardTitle"
    ON "TwitchRedemptionActions" ("GuildId", "RewardTitle");
