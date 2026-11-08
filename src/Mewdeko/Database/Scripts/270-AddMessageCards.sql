-- Message cards: every member event message keeps its own optional card. A card has a design and a placement (0 none,
-- 1 embed image, 2 attached). Existing welcome cards move onto the messages they were attached to, as attached cards.
-- The old WelcomeCardSettings table is kept and still read for cards saved before designs existed.
-- Target: 0 greet, 1 bye, 2 DM greet, 3 boost, 4 role greet. OwnerId is the multi-greet or role greet id, or 0.
-- Migration: 270-AddMessageCards.sql

CREATE TABLE IF NOT EXISTS "MessageCards"
(
    "Id"        SERIAL PRIMARY KEY,
    "GuildId"   NUMERIC(20, 0) NOT NULL,
    "Target"    INTEGER        NOT NULL DEFAULT 0,
    "OwnerId"   INTEGER        NOT NULL DEFAULT 0,
    "Placement" INTEGER        NOT NULL DEFAULT 0,
    "Template"  TEXT,
    "DateAdded" TIMESTAMP      NOT NULL DEFAULT NOW(),
    UNIQUE ("GuildId", "Target", "OwnerId")
);

INSERT INTO "MessageCards" ("GuildId", "Target", "OwnerId", "Placement", "Template", "DateAdded")
SELECT m."GuildId", 0, m."Id", CASE WHEN s."Enabled" THEN 2 ELSE 0 END, s."Template", s."DateAdded"
FROM "MultiGreets" m
         JOIN "WelcomeCardSettings" s ON s."GuildId" = m."GuildId" AND s."Target" = 0
ON CONFLICT DO NOTHING;

INSERT INTO "MessageCards" ("GuildId", "Target", "OwnerId", "Placement", "Template", "DateAdded")
SELECT s."GuildId", s."Target", 0, CASE WHEN s."Enabled" THEN 2 ELSE 0 END, s."Template", s."DateAdded"
FROM "WelcomeCardSettings" s
WHERE s."Target" IN (1, 2, 3)
ON CONFLICT DO NOTHING;
