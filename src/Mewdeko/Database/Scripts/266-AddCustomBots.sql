-- Self-service paid custom bots. A slot is the right to run one custom bot, granted by a Ko-fi purchase or by an
-- owner; a custom bot is a BotInstances row with IsCustom set whose token and container details are kept here.
-- Migration: 266-AddCustomBots.sql

CREATE TABLE IF NOT EXISTS "CustomBotSlots"
(
    "Id"                SERIAL PRIMARY KEY,
    "UserId"            NUMERIC(20, 0) NULL,
    "KofiEmail"         TEXT           NULL,
    "Source"            TEXT           NOT NULL DEFAULT 'kofi',
    "KofiTransactionId" TEXT           NULL UNIQUE,
    "ExpiresAt"         TIMESTAMP      NULL,
    "InstanceId"        INT            NULL REFERENCES "BotInstances" ("Id") ON DELETE SET NULL,
    "StoppedAt"         TIMESTAMP      NULL,
    "DateAdded"         TIMESTAMP      NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS "IX_CustomBotSlots_UserId" ON "CustomBotSlots" ("UserId");
CREATE INDEX IF NOT EXISTS "IX_CustomBotSlots_KofiEmail" ON "CustomBotSlots" (LOWER("KofiEmail"));

CREATE TABLE IF NOT EXISTS "CustomBotSecrets"
(
    "InstanceId"     INT PRIMARY KEY REFERENCES "BotInstances" ("Id") ON DELETE CASCADE,
    "EncryptedToken" TEXT      NOT NULL,
    "ApiPort"        INT       NOT NULL,
    "ContainerName"  TEXT      NOT NULL,
    "DateAdded"      TIMESTAMP NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS "KofiPayments"
(
    "Id"                      SERIAL PRIMARY KEY,
    "KofiTransactionId"       TEXT      NOT NULL UNIQUE,
    "Email"                   TEXT      NOT NULL,
    "Type"                    TEXT      NOT NULL,
    "Amount"                  TEXT      NULL,
    "IsSubscriptionPayment"   BOOLEAN   NOT NULL DEFAULT FALSE,
    "IsFirstSubscriptionPayment" BOOLEAN NOT NULL DEFAULT FALSE,
    "SlotId"                  INT       NULL REFERENCES "CustomBotSlots" ("Id") ON DELETE SET NULL,
    "PaidAt"                  TIMESTAMP NOT NULL,
    "DateAdded"               TIMESTAMP NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS "IX_KofiPayments_Email" ON "KofiPayments" (LOWER("Email"));
