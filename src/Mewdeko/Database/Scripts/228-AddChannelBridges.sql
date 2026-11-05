-- Channel bridges: a channel relayed to a channel in a Fluxer community (or the other way round) by the two bots
-- through a shared Redis. One bridge per channel.
-- Migration: 228-AddChannelBridges.sql

CREATE TABLE IF NOT EXISTS "ChannelBridges"
(
    "Id"                SERIAL PRIMARY KEY,
    "BridgeId"          UUID           NOT NULL UNIQUE,
    "GuildId"           NUMERIC(20, 0) NOT NULL,
    "ChannelId"         NUMERIC(20, 0) NOT NULL UNIQUE,
    "WebhookUrl"        TEXT           NOT NULL,
    "RemoteGuildId"     NUMERIC(20, 0) NOT NULL,
    "RemoteChannelId"   NUMERIC(20, 0) NOT NULL,
    "RemoteGuildName"   TEXT           NOT NULL DEFAULT '',
    "RemoteChannelName" TEXT           NOT NULL DEFAULT '',
    "Paused"            BOOLEAN        NOT NULL DEFAULT FALSE,
    "AllowBots"         BOOLEAN        NOT NULL DEFAULT FALSE,
    "CreatedBy"         NUMERIC(20, 0) NOT NULL,
    "DateAdded"         TIMESTAMP      NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS "IX_ChannelBridges_GuildId" ON "ChannelBridges" ("GuildId");
