ALTER TABLE "TwitchGuildConfigs"
    ADD COLUMN IF NOT EXISTS "TwitchUserId"              TEXT           NULL,
    ADD COLUMN IF NOT EXISTS "TwitchDisplayName"         TEXT           NULL,
    ADD COLUMN IF NOT EXISTS "UseEventSub"               BOOLEAN        NOT NULL DEFAULT TRUE,
    ADD COLUMN IF NOT EXISTS "AuthorizedByDiscordUserId" NUMERIC(20, 0) NULL,
    ADD COLUMN IF NOT EXISTS "LastAuthorizedAt"          TIMESTAMP      NULL,
    ADD COLUMN IF NOT EXISTS "LastEventAt"               TIMESTAMP      NULL;

CREATE TABLE IF NOT EXISTS "TwitchBotAccounts"
(
    "Id"              SERIAL PRIMARY KEY,
    "TwitchUserId"    TEXT      NOT NULL UNIQUE,
    "TwitchUsername"  TEXT      NOT NULL,
    "DisplayName"     TEXT      NOT NULL,
    "AccessToken"     TEXT      NOT NULL,
    "RefreshToken"    TEXT      NOT NULL,
    "Scopes"          TEXT      NOT NULL,
    "TokenExpiresAt"  TIMESTAMP NULL,
    "DateAdded"       TIMESTAMP NULL,
    "LastRefreshedAt" TIMESTAMP NULL
);

CREATE TABLE IF NOT EXISTS "TwitchChannelAuthorizations"
(
    "Id"                        SERIAL PRIMARY KEY,
    "GuildId"                   NUMERIC(20, 0) NOT NULL UNIQUE,
    "TwitchUserId"              TEXT           NOT NULL,
    "TwitchUsername"            TEXT           NOT NULL,
    "DisplayName"               TEXT           NOT NULL,
    "AccessToken"               TEXT           NOT NULL,
    "RefreshToken"              TEXT           NOT NULL,
    "Scopes"                    TEXT           NOT NULL,
    "TokenExpiresAt"            TIMESTAMP      NULL,
    "AuthorizedByDiscordUserId" NUMERIC(20, 0) NULL,
    "DateAdded"                 TIMESTAMP      NULL,
    "LastRefreshedAt"           TIMESTAMP      NULL
);

CREATE TABLE IF NOT EXISTS "TwitchEventSubSubscriptions"
(
    "Id"                   SERIAL PRIMARY KEY,
    "GuildId"              NUMERIC(20, 0) NOT NULL,
    "TwitchSubscriptionId" TEXT           NOT NULL UNIQUE,
    "SubscriptionType"     TEXT           NOT NULL,
    "Version"              TEXT           NOT NULL,
    "Status"               TEXT           NOT NULL,
    "TransportMethod"      TEXT           NOT NULL,
    "SessionId"            TEXT           NULL,
    "Cost"                 INTEGER        NOT NULL DEFAULT 0,
    "DateAdded"            TIMESTAMP      NULL,
    "LastUpdatedAt"        TIMESTAMP      NULL
);

CREATE INDEX IF NOT EXISTS "IX_TwitchChannelAuthorizations_GuildId"
    ON "TwitchChannelAuthorizations" ("GuildId");

CREATE INDEX IF NOT EXISTS "IX_TwitchEventSubSubscriptions_GuildId_Type"
    ON "TwitchEventSubSubscriptions" ("GuildId", "SubscriptionType");
