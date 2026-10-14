-- TTS auto-join: when a member joins a TTS-enabled voice channel with this flag set and the bot is not already
-- in voice for the guild, the bot joins that channel on its own. Off until a server turns it on.
-- Migration: 225-AddTtsAutoJoin.sql

ALTER TABLE "TtsVoiceChannelSettings"
    ADD COLUMN IF NOT EXISTS "AutoJoin" boolean NOT NULL DEFAULT FALSE;
