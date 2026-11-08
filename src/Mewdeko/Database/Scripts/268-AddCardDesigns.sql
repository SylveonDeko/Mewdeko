-- Card design library: per community designs for the rank card and the achievement card, who owns them, and which
-- one each member has equipped. Shop items can grant a design.
-- Migration: 268-AddCardDesigns.sql

CREATE TABLE IF NOT EXISTS "CardDesigns"
(
    "Id"                   SERIAL PRIMARY KEY,
    "GuildId"              NUMERIC(20, 0) NOT NULL,
    "Name"                 TEXT           NOT NULL,
    "Target"               SMALLINT       NOT NULL DEFAULT 0,
    "Data"                 TEXT           NOT NULL DEFAULT '{}',
    "Availability"         SMALLINT       NOT NULL DEFAULT 0,
    "UnlockType"           SMALLINT       NULL,
    "UnlockLevel"          INT            NULL,
    "UnlockAchievementKey" TEXT           NULL,
    "UnlockRoleId"         NUMERIC(20, 0) NULL,
    "IsDefault"            BOOLEAN        NOT NULL DEFAULT FALSE,
    "DateAdded"            TIMESTAMP      NOT NULL DEFAULT NOW(),
    "DateUpdated"          TIMESTAMP      NOT NULL DEFAULT NOW()
);

CREATE UNIQUE INDEX IF NOT EXISTS "UX_CardDesigns_GuildId_Name" ON "CardDesigns" ("GuildId", LOWER("Name"));
CREATE UNIQUE INDEX IF NOT EXISTS "UX_CardDesigns_Default" ON "CardDesigns" ("GuildId", "Target") WHERE "IsDefault";

CREATE TABLE IF NOT EXISTS "CardDesignOwners"
(
    "Id"        SERIAL PRIMARY KEY,
    "GuildId"   NUMERIC(20, 0) NOT NULL,
    "UserId"    NUMERIC(20, 0) NOT NULL,
    "DesignId"  INT            NOT NULL REFERENCES "CardDesigns" ("Id") ON DELETE CASCADE,
    "Source"    SMALLINT       NOT NULL DEFAULT 0,
    "DateAdded" TIMESTAMP      NOT NULL DEFAULT NOW()
);

CREATE UNIQUE INDEX IF NOT EXISTS "UX_CardDesignOwners_Member_Design" ON "CardDesignOwners" ("GuildId", "UserId", "DesignId");
CREATE INDEX IF NOT EXISTS "IX_CardDesignOwners_DesignId" ON "CardDesignOwners" ("DesignId");

CREATE TABLE IF NOT EXISTS "EquippedCardDesigns"
(
    "Id"       SERIAL PRIMARY KEY,
    "GuildId"  NUMERIC(20, 0) NOT NULL,
    "UserId"   NUMERIC(20, 0) NOT NULL,
    "Target"   SMALLINT       NOT NULL,
    "DesignId" INT            NOT NULL REFERENCES "CardDesigns" ("Id") ON DELETE CASCADE
);

CREATE UNIQUE INDEX IF NOT EXISTS "UX_EquippedCardDesigns_Member_Target" ON "EquippedCardDesigns" ("GuildId", "UserId", "Target");
CREATE INDEX IF NOT EXISTS "IX_EquippedCardDesigns_DesignId" ON "EquippedCardDesigns" ("DesignId");

ALTER TABLE "ShopItems"
    ADD COLUMN IF NOT EXISTS "DesignId" INT NULL REFERENCES "CardDesigns" ("Id") ON DELETE SET NULL;
