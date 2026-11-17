-- Blackjack tables are saved between steps so a restart cannot lose a round after its bets were taken. One table per
-- channel and one seat per member per community; both are removed when the table is paid out.
-- Migration: 274-AddBlackjackGames.sql

CREATE TABLE IF NOT EXISTS "BlackjackTables"
(
    "GuildId"      NUMERIC(20, 0) NOT NULL,
    "ChannelId"    NUMERIC(20, 0) NOT NULL,
    "MessageId"    NUMERIC(20, 0) NOT NULL DEFAULT 0,
    "OpenerId"     NUMERIC(20, 0) NOT NULL,
    "Phase"        SMALLINT       NOT NULL DEFAULT 0,
    "Seats"        TEXT           NOT NULL,
    "DealerCards"  TEXT           NOT NULL DEFAULT '',
    "Turn"         INT            NOT NULL DEFAULT 0,
    "Version"      INT            NOT NULL DEFAULT 1,
    "StartedAt"    TIMESTAMP      NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    "LastActionAt" TIMESTAMP      NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    PRIMARY KEY ("GuildId", "ChannelId")
);

CREATE INDEX IF NOT EXISTS "IX_BlackjackTables_LastActionAt" ON "BlackjackTables" ("LastActionAt");
CREATE INDEX IF NOT EXISTS "IX_BlackjackTables_StartedAt" ON "BlackjackTables" ("StartedAt");

CREATE TABLE IF NOT EXISTS "BlackjackSeats"
(
    "GuildId"   NUMERIC(20, 0) NOT NULL,
    "UserId"    NUMERIC(20, 0) NOT NULL,
    "ChannelId" NUMERIC(20, 0) NOT NULL,
    PRIMARY KEY ("GuildId", "UserId")
);

CREATE INDEX IF NOT EXISTS "IX_BlackjackSeats_GuildId_ChannelId" ON "BlackjackSeats" ("GuildId", "ChannelId");
