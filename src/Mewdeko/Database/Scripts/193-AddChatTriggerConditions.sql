ALTER TABLE "ChatTriggers"
    ADD COLUMN IF NOT EXISTS "TimeConditions"             TEXT                        NULL,
    ADD COLUMN IF NOT EXISTS "ExpiresAt"                  TIMESTAMP WITHOUT TIME ZONE NULL,
    ADD COLUMN IF NOT EXISTS "MaxUses"                    INTEGER                     NULL,
    ADD COLUMN IF NOT EXISTS "MinAccountAgeMinutes"       INTEGER                     NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS "MinServerMembershipMinutes" INTEGER                     NOT NULL DEFAULT 0;
