-- Anonymous suggestions: hides the suggester from suggestion messages while staff can still look the author up.
-- Migration: 236-AddAnonymousSuggestions.sql

ALTER TABLE "GuildConfigs" ADD COLUMN IF NOT EXISTS "AnonymousSuggestions" BOOLEAN NOT NULL DEFAULT FALSE;
