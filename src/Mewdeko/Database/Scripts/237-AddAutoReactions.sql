-- Auto reactions: per-channel rules that add reactions to new messages, or to the first post of forum threads.
-- Migration: 237-AddAutoReactions.sql

CREATE TABLE IF NOT EXISTS "AutoReactions"
(
    "Id"             SERIAL PRIMARY KEY,
    "GuildId"        NUMERIC(20, 0) NOT NULL,
    "ChannelId"      NUMERIC(20, 0) NOT NULL,
    "Emojis"         TEXT           NOT NULL DEFAULT '',
    "MessageTypes"   INTEGER        NOT NULL DEFAULT 0,
    "IgnoreBots"     BOOLEAN        NOT NULL DEFAULT FALSE,
    "IgnoreWebhooks" BOOLEAN        NOT NULL DEFAULT FALSE,
    "IgnoreReplies"  BOOLEAN        NOT NULL DEFAULT FALSE,
    "AllowedRoleIds" TEXT           NOT NULL DEFAULT '',
    "IgnoredRoleIds" TEXT           NOT NULL DEFAULT '',
    "IgnoredUserIds" TEXT           NOT NULL DEFAULT '',
    "SkipWords"      TEXT           NOT NULL DEFAULT '',
    "RequiredWords"  TEXT           NOT NULL DEFAULT '',
    "Enabled"        BOOLEAN        NOT NULL DEFAULT TRUE,
    "DateAdded"      TIMESTAMP      NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS "IX_AutoReactions_GuildId" ON "AutoReactions" ("GuildId");
