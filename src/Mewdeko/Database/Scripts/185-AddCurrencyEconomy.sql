-- Adds the wallet/bank split, a cooldown table, per-guild economy tuning, a shop with
-- inventory, and the unique indexes the atomic balance operations depend on. Existing
-- duplicate and negative balance rows are repaired first.
-- Migration: 185-AddCurrencyEconomy.sql

UPDATE "GuildUserBalance" t
SET "Balance" = s.total
FROM (SELECT MIN("Id") AS keep_id, "GuildId", "UserId", SUM("Balance") AS total
      FROM "GuildUserBalance"
      GROUP BY "GuildId", "UserId"
      HAVING COUNT(*) > 1) s
WHERE t."Id" = s.keep_id;

DELETE
FROM "GuildUserBalance" a USING (SELECT MIN("Id") AS keep_id, "GuildId", "UserId"
                                 FROM "GuildUserBalance"
                                 GROUP BY "GuildId", "UserId"
                                 HAVING COUNT(*) > 1) s
WHERE a."GuildId" = s."GuildId"
  AND a."UserId" = s."UserId"
  AND a."Id" <> s.keep_id;

UPDATE "GlobalUserBalance" t
SET "Balance" = s.total
FROM (SELECT MIN("Id") AS keep_id, "UserId", SUM("Balance") AS total
      FROM "GlobalUserBalance"
      GROUP BY "UserId"
      HAVING COUNT(*) > 1) s
WHERE t."Id" = s.keep_id;

DELETE
FROM "GlobalUserBalance" a USING (SELECT MIN("Id") AS keep_id, "UserId"
                                  FROM "GlobalUserBalance"
                                  GROUP BY "UserId"
                                  HAVING COUNT(*) > 1) s
WHERE a."UserId" = s."UserId"
  AND a."Id" <> s.keep_id;

UPDATE "GuildUserBalance"
SET "Balance" = 0
WHERE "Balance" < 0;
UPDATE "GlobalUserBalance"
SET "Balance" = 0
WHERE "Balance" < 0;

CREATE UNIQUE INDEX IF NOT EXISTS "UX_GuildUserBalance_GuildId_UserId"
    ON "GuildUserBalance" ("GuildId", "UserId");

CREATE UNIQUE INDEX IF NOT EXISTS "UX_GlobalUserBalance_UserId"
    ON "GlobalUserBalance" ("UserId");

ALTER TABLE "GuildUserBalance"
    ADD COLUMN IF NOT EXISTS "Bank" BIGINT NOT NULL DEFAULT 0;
ALTER TABLE "GlobalUserBalance"
    ADD COLUMN IF NOT EXISTS "Bank" BIGINT NOT NULL DEFAULT 0;

ALTER TABLE "TransactionHistory"
    ADD COLUMN IF NOT EXISTS "Category" TEXT NOT NULL DEFAULT 'Legacy';

ALTER TABLE "TransactionHistory"
    ADD COLUMN IF NOT EXISTS "Source" TEXT;

CREATE INDEX IF NOT EXISTS "IX_TransactionHistory_GuildId_Source_DateAdded"
    ON "TransactionHistory" ("GuildId", "Source", "DateAdded");

CREATE INDEX IF NOT EXISTS "IX_TransactionHistory_GuildId_DateAdded"
    ON "TransactionHistory" ("GuildId", "DateAdded");

CREATE INDEX IF NOT EXISTS "IX_TransactionHistory_GuildId_UserId_DateAdded"
    ON "TransactionHistory" ("GuildId", "UserId", "DateAdded");

CREATE INDEX IF NOT EXISTS "IX_TransactionHistory_GuildId_Category_DateAdded"
    ON "TransactionHistory" ("GuildId", "Category", "DateAdded");

CREATE TABLE IF NOT EXISTS "CurrencyCooldowns"
(
    "Id"          SERIAL PRIMARY KEY,
    "GuildId"     NUMERIC(20, 0)                 NOT NULL,
    "UserId"      NUMERIC(20, 0)                 NOT NULL,
    "CooldownKey" TEXT                           NOT NULL,
    "LastUsed"    TIMESTAMP(6) WITHOUT TIME ZONE NOT NULL,
    "StreakCount" INTEGER                        NOT NULL DEFAULT 0,
    "DateAdded"   TIMESTAMP(6) WITHOUT TIME ZONE          DEFAULT CURRENT_TIMESTAMP,
    UNIQUE ("GuildId", "UserId", "CooldownKey")
);

CREATE INDEX IF NOT EXISTS "IX_CurrencyCooldowns_GuildId_CooldownKey"
    ON "CurrencyCooldowns" ("GuildId", "CooldownKey");

CREATE TABLE IF NOT EXISTS "CurrencyConfigs"
(
    "Id"                   SERIAL PRIMARY KEY,
    "GuildId"              NUMERIC(20, 0)   NOT NULL UNIQUE,

    "MinBet"               BIGINT           NOT NULL      DEFAULT 1,
    "MaxBet"               BIGINT           NOT NULL      DEFAULT 0,
    "GamblingEnabled"      BOOLEAN          NOT NULL      DEFAULT TRUE,
    "PayoutMultiplier"     DOUBLE PRECISION NOT NULL      DEFAULT 1.0,
    "GameCooldownSeconds"  INTEGER          NOT NULL      DEFAULT 0,
    "LossLimitPerDay"      BIGINT           NOT NULL      DEFAULT 0,

    "PayEnabled"           BOOLEAN          NOT NULL      DEFAULT TRUE,
    "PayTaxPercent"        INTEGER          NOT NULL      DEFAULT 0,
    "PayCooldownSeconds"   INTEGER          NOT NULL      DEFAULT 0,
    "PayMinimum"           BIGINT           NOT NULL      DEFAULT 1,

    "BankEnabled"          BOOLEAN          NOT NULL      DEFAULT TRUE,
    "BankCapacity"         BIGINT           NOT NULL      DEFAULT 0,
    "BankInterestPercent"  DOUBLE PRECISION NOT NULL      DEFAULT 0,
    "BankInterestHours"    INTEGER          NOT NULL      DEFAULT 24,

    "RobEnabled"           BOOLEAN          NOT NULL      DEFAULT FALSE,
    "RobSuccessChance"     INTEGER          NOT NULL      DEFAULT 35,
    "RobMaxStealPercent"   INTEGER          NOT NULL      DEFAULT 20,
    "RobFinePercent"       INTEGER          NOT NULL      DEFAULT 15,
    "RobMinimumWallet"     BIGINT           NOT NULL      DEFAULT 100,
    "RobCooldownSeconds"   INTEGER          NOT NULL      DEFAULT 3600,

    "WorkEnabled"          BOOLEAN          NOT NULL      DEFAULT TRUE,
    "WorkMinReward"        BIGINT           NOT NULL      DEFAULT 50,
    "WorkMaxReward"        BIGINT           NOT NULL      DEFAULT 250,
    "WorkCooldownSeconds"  INTEGER          NOT NULL      DEFAULT 1800,

    "CrimeEnabled"         BOOLEAN          NOT NULL      DEFAULT TRUE,
    "CrimeMinReward"       BIGINT           NOT NULL      DEFAULT 200,
    "CrimeMaxReward"       BIGINT           NOT NULL      DEFAULT 800,
    "CrimeSuccessChance"   INTEGER          NOT NULL      DEFAULT 45,
    "CrimeFineMin"         BIGINT           NOT NULL      DEFAULT 100,
    "CrimeFineMax"         BIGINT           NOT NULL      DEFAULT 500,
    "CrimeCooldownSeconds" INTEGER          NOT NULL      DEFAULT 3600,

    "DailyStreakEnabled"   BOOLEAN          NOT NULL      DEFAULT TRUE,
    "DailyStreakBonus"     BIGINT           NOT NULL      DEFAULT 0,
    "DailyStreakMaxBonus"  BIGINT           NOT NULL      DEFAULT 0,

    "DateAdded"            TIMESTAMP(6) WITHOUT TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS "ShopItems"
(
    "Id"             SERIAL PRIMARY KEY,
    "GuildId"        NUMERIC(20, 0) NOT NULL,
    "Name"           TEXT           NOT NULL,
    "Description"    TEXT,
    "Price"          BIGINT         NOT NULL,
    "ItemType"       INTEGER        NOT NULL        DEFAULT 0,
    "RoleId"         NUMERIC(20, 0),
    "TextContent"    TEXT,
    "Stock"          INTEGER        NOT NULL        DEFAULT -1,
    "MaxPerUser"     INTEGER        NOT NULL        DEFAULT 0,
    "RequiredRoleId" NUMERIC(20, 0),
    "Consumable"     BOOLEAN        NOT NULL        DEFAULT FALSE,
    "Enabled"        BOOLEAN        NOT NULL        DEFAULT TRUE,
    "SortOrder"      INTEGER        NOT NULL        DEFAULT 0,
    "DateAdded"      TIMESTAMP(6) WITHOUT TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

CREATE UNIQUE INDEX IF NOT EXISTS "UX_ShopItems_GuildId_Name"
    ON "ShopItems" ("GuildId", LOWER("Name"));

CREATE TABLE IF NOT EXISTS "UserInventoryItems"
(
    "Id"         SERIAL PRIMARY KEY,
    "GuildId"    NUMERIC(20, 0) NOT NULL,
    "UserId"     NUMERIC(20, 0) NOT NULL,
    "ShopItemId" INTEGER        NOT NULL REFERENCES "ShopItems" ("Id") ON DELETE CASCADE,
    "Quantity"   INTEGER        NOT NULL        DEFAULT 0,
    "TotalPaid"  BIGINT         NOT NULL        DEFAULT 0,
    "DateAdded"  TIMESTAMP(6) WITHOUT TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    UNIQUE ("GuildId", "UserId", "ShopItemId")
);

CREATE INDEX IF NOT EXISTS "IX_UserInventoryItems_GuildId_UserId"
    ON "UserInventoryItems" ("GuildId", "UserId");
