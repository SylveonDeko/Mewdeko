-- Bridge pairs: a community linked once with a community on the other platform, so either side can see the other's
-- shared channels and bridge them without a code per channel. Proposals are the pending channel bridges inside a pair.
-- Migration: 231-AddBridgePairs.sql

CREATE TABLE IF NOT EXISTS "BridgePairs"
(
    "Id"               SERIAL PRIMARY KEY,
    "PairId"           UUID           NOT NULL UNIQUE,
    "GuildId"          NUMERIC(20, 0) NOT NULL,
    "RemoteGuildId"    NUMERIC(20, 0) NOT NULL,
    "RemoteGuildName"  TEXT           NOT NULL DEFAULT '',
    "ApprovalMode"     INTEGER        NOT NULL DEFAULT 0,
    "HiddenChannelIds" TEXT           NOT NULL DEFAULT '',
    "CreatedBy"        NUMERIC(20, 0) NOT NULL,
    "DateAdded"        TIMESTAMP      NOT NULL DEFAULT NOW(),
    CONSTRAINT "UQ_BridgePairs_Guilds" UNIQUE ("GuildId", "RemoteGuildId")
);

CREATE INDEX IF NOT EXISTS "IX_BridgePairs_GuildId" ON "BridgePairs" ("GuildId");

CREATE TABLE IF NOT EXISTS "BridgeProposals"
(
    "Id"                SERIAL PRIMARY KEY,
    "ProposalId"        UUID           NOT NULL UNIQUE,
    "PairId"            UUID           NOT NULL REFERENCES "BridgePairs" ("PairId") ON DELETE CASCADE,
    "Incoming"          BOOLEAN        NOT NULL,
    "LocalChannelId"    NUMERIC(20, 0) NOT NULL,
    "RemoteChannelId"   NUMERIC(20, 0) NOT NULL,
    "RemoteChannelName" TEXT           NOT NULL DEFAULT '',
    "ProposedBy"        NUMERIC(20, 0) NOT NULL,
    "DateAdded"         TIMESTAMP      NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS "IX_BridgeProposals_PairId" ON "BridgeProposals" ("PairId");

ALTER TABLE "ChannelBridges"
    ADD COLUMN IF NOT EXISTS "PairId" UUID NULL;

CREATE INDEX IF NOT EXISTS "IX_ChannelBridges_PairId" ON "ChannelBridges" ("PairId");
