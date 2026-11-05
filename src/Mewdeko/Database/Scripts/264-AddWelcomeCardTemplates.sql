-- Welcome cards move to the shared card designer: each card keeps its design as a template, and the images a design
-- uses are stored per server. Cards with no template keep drawing from their old columns until they are first saved.
-- Migration: 264-AddWelcomeCardTemplates.sql

ALTER TABLE "WelcomeCardSettings"
    ADD COLUMN IF NOT EXISTS "Template" TEXT;

CREATE TABLE IF NOT EXISTS "WelcomeCardImages"
(
    "Id"        SERIAL PRIMARY KEY,
    "GuildId"   NUMERIC(20, 0) NOT NULL,
    "Data"      BYTEA          NOT NULL,
    "Thumbnail" BYTEA          NOT NULL,
    "DateAdded" TIMESTAMP      NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS "IX_WelcomeCardImages_GuildId" ON "WelcomeCardImages" ("GuildId");
