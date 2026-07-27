-- Channels where music links (Apple Music, Spotify, YouTube Music, etc.) get
-- auto-converted into a cross-platform embed via the song.link/Odesli API.
-- Migration: 183-AddMusicLinkChannels.sql

CREATE TABLE IF NOT EXISTS "MusicLinkChannels"
(
    "Id"        SERIAL PRIMARY KEY,
    "GuildId"   NUMERIC(20, 0) NOT NULL,
    "ChannelId" NUMERIC(20, 0) NOT NULL,
    "DateAdded" TIMESTAMP(6) WITHOUT TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    UNIQUE ("GuildId", "ChannelId")
);

CREATE INDEX IF NOT EXISTS "IX_MusicLinkChannels_GuildId" ON "MusicLinkChannels" ("GuildId");
