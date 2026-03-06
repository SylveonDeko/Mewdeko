-- TTS (Text-to-Speech) support for Music Player
-- Per-voice-channel TTS with join/leave announcements, per-user voices, and guild-wide settings

ALTER TABLE "MusicPlayerSettings"
    ADD COLUMN IF NOT EXISTS "TtsChannelId" numeric(20, 0) NULL;

ALTER TABLE "MusicPlayerSettings"
    ADD COLUMN IF NOT EXISTS "TtsVolume" integer NOT NULL DEFAULT 100;

ALTER TABLE "MusicPlayerSettings"
    ADD COLUMN IF NOT EXISTS "TtsDefaultVoice" text NULL;

ALTER TABLE "MusicPlayerSettings"
    ADD COLUMN IF NOT EXISTS "TtsSpeed" real NOT NULL DEFAULT 1.0;

ALTER TABLE "MusicPlayerSettings"
    ADD COLUMN IF NOT EXISTS "TtsReplyContext" boolean NOT NULL DEFAULT TRUE;

ALTER TABLE "MusicPlayerSettings"
    ADD COLUMN IF NOT EXISTS "TtsAttachmentNarration" boolean NOT NULL DEFAULT TRUE;

ALTER TABLE "MusicPlayerSettings"
    ADD COLUMN IF NOT EXISTS "TtsMaxQueueSize" integer NOT NULL DEFAULT 10;

ALTER TABLE "MusicPlayerSettings"
    ADD COLUMN IF NOT EXISTS "TtsConsecutiveGrouping" boolean NOT NULL DEFAULT TRUE;

ALTER TABLE "MusicPlayerSettings"
    ADD COLUMN IF NOT EXISTS "TtsRoleId" numeric(20, 0) NULL;

CREATE TABLE IF NOT EXISTS "TtsUserSettings"
(
    "Id"        serial PRIMARY KEY,
    "GuildId"   numeric(20, 0) NOT NULL,
    "UserId"    numeric(20, 0) NOT NULL,
    "Voice"     text           NULL,
    "IsBlocked" boolean        NOT NULL DEFAULT FALSE,
    UNIQUE ("GuildId", "UserId")
);

CREATE TABLE IF NOT EXISTS "TtsVoiceChannelSettings"
(
    "Id"                  serial PRIMARY KEY,
    "GuildId"             numeric(20, 0) NOT NULL,
    "VoiceChannelId"      numeric(20, 0) NOT NULL,
    "Enabled"             boolean        NOT NULL DEFAULT TRUE,
    "LinkedTextChannelId" numeric(20, 0) NULL,
    "AnnounceJoinLeave"   boolean        NOT NULL DEFAULT FALSE,
    "JoinFormat"          text           NULL,
    "LeaveFormat"         text           NULL,
    UNIQUE ("GuildId", "VoiceChannelId")
);
