-- Add saved "send as" personas for the embed builder: a reusable display name and
-- avatar that a message can be delivered under via webhook.
-- Migration: 186-AddEmbedWebhookPersonas.sql

-- A persona is either personal (GuildId is null, visible only to its owner) or
-- guild-shared, mirroring how saved embed templates work. Avatars may be a URL
-- (used as a per-message override) or uploaded bytes (baked onto the persona's
-- per-channel webhook). AvatarVersion is bumped whenever the avatar changes so
-- already-materialized webhooks can be refreshed lazily on next use.
CREATE TABLE IF NOT EXISTS "EmbedWebhookPersonas"
(
    "Id"            SERIAL PRIMARY KEY,
    "GuildId"       NUMERIC(20, 0),
    "UserId"        NUMERIC(20, 0) NOT NULL,
    "Name"          TEXT           NOT NULL,
    "AvatarUrl"     TEXT,
    "AvatarData"    BYTEA,
    "AvatarVersion" INTEGER        NOT NULL        DEFAULT 1,
    "IsGuildShared" BOOLEAN        NOT NULL        DEFAULT FALSE,
    "DateAdded"     TIMESTAMP(6) WITHOUT TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS "IX_EmbedWebhookPersonas_GuildId" ON "EmbedWebhookPersonas" ("GuildId");
CREATE INDEX IF NOT EXISTS "IX_EmbedWebhookPersonas_UserId" ON "EmbedWebhookPersonas" ("UserId");

-- Names must be unique within their own scope so a persona can be referred to
-- unambiguously, matching the uniqueness rules on saved embed templates.
CREATE UNIQUE INDEX IF NOT EXISTS "IX_EmbedWebhookPersonas_Guild_Name"
    ON "EmbedWebhookPersonas" ("GuildId", LOWER("Name"))
    WHERE "GuildId" IS NOT NULL;

CREATE UNIQUE INDEX IF NOT EXISTS "IX_EmbedWebhookPersonas_User_Name"
    ON "EmbedWebhookPersonas" ("UserId", LOWER("Name"))
    WHERE "GuildId" IS NULL;
