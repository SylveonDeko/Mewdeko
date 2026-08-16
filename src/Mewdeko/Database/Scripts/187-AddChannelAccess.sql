-- Adds the channel access system: users apply for locked channels and the members
-- who already have access vote on whether to let them in.
-- Migration: 187-AddChannelAccess.sql

-- One "gate" per locked channel. Holds every knob for how applications and voting behave.
CREATE TABLE IF NOT EXISTS "ChannelAccessConfigs"
(
    "Id"                   SERIAL PRIMARY KEY,
    "GuildId"              NUMERIC(20, 0) NOT NULL,
    "ChannelId"            NUMERIC(20, 0) NOT NULL,
    "AccessRoleId"         NUMERIC(20, 0) NOT NULL,
    "ReviewChannelId"      NUMERIC(20, 0),
    "LogChannelId"         NUMERIC(20, 0),
    "PanelChannelId"       NUMERIC(20, 0),
    "PanelMessageId"       NUMERIC(20, 0),
    "VoterRoleId"          NUMERIC(20, 0),
    "PingRoleId"           NUMERIC(20, 0),
    "Enabled"              BOOLEAN        NOT NULL        DEFAULT TRUE,
    "RequiredApprovals"    INTEGER        NOT NULL        DEFAULT 3,
    "RequiredDenials"      INTEGER        NOT NULL        DEFAULT 3,
    "VoteDurationHours"    INTEGER        NOT NULL        DEFAULT 72,
    "OnExpiry"             INTEGER        NOT NULL        DEFAULT 0, -- 0 = deny, 1 = majority wins, 2 = stay pending
    "AllowAbstain"         BOOLEAN        NOT NULL        DEFAULT TRUE,
    "AnonymousVotes"       BOOLEAN        NOT NULL        DEFAULT FALSE,
    "AnonymousApplicant"   BOOLEAN        NOT NULL        DEFAULT FALSE,
    "MinAccountAgeDays"    INTEGER        NOT NULL        DEFAULT 0,
    "MinServerAgeDays"     INTEGER        NOT NULL        DEFAULT 0,
    "ReapplyCooldownHours" INTEGER        NOT NULL        DEFAULT 168,
    "DmOnDecision"         BOOLEAN        NOT NULL        DEFAULT TRUE,
    "CreatedBy"            NUMERIC(20, 0) NOT NULL,
    "DateAdded"            TIMESTAMP(6) WITHOUT TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    UNIQUE ("GuildId", "ChannelId")
);

CREATE INDEX IF NOT EXISTS "IX_ChannelAccessConfigs_GuildId" ON "ChannelAccessConfigs" ("GuildId");

-- Custom application questions shown in the apply modal, in Position order (max 5, Discord limit).
CREATE TABLE IF NOT EXISTS "ChannelAccessQuestions"
(
    "Id"          SERIAL PRIMARY KEY,
    "ConfigId"    INTEGER NOT NULL REFERENCES "ChannelAccessConfigs" ("Id") ON DELETE CASCADE,
    "Position"    INTEGER NOT NULL               DEFAULT 0,
    "Question"    TEXT    NOT NULL,
    "Placeholder" TEXT,
    "Required"    BOOLEAN NOT NULL               DEFAULT TRUE,
    "Paragraph"   BOOLEAN NOT NULL               DEFAULT TRUE,
    "MinLength"   INTEGER NOT NULL               DEFAULT 0,
    "MaxLength"   INTEGER NOT NULL               DEFAULT 1000,
    "DateAdded"   TIMESTAMP(6) WITHOUT TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS "IX_ChannelAccessQuestions_ConfigId" ON "ChannelAccessQuestions" ("ConfigId");

-- A single user's application to a gate.
CREATE TABLE IF NOT EXISTS "ChannelAccessApplications"
(
    "Id"               SERIAL PRIMARY KEY,
    "ConfigId"         INTEGER        NOT NULL REFERENCES "ChannelAccessConfigs" ("Id") ON DELETE CASCADE,
    "GuildId"          NUMERIC(20, 0) NOT NULL,
    "UserId"           NUMERIC(20, 0) NOT NULL,
    "Status"           INTEGER        NOT NULL        DEFAULT 0, -- 0 pending, 1 approved, 2 denied, 3 withdrawn, 4 expired
    "MessageChannelId" NUMERIC(20, 0),
    "MessageId"        NUMERIC(20, 0),
    "ThreadId"         NUMERIC(20, 0),
    "ExpiresAt"        TIMESTAMP(6) WITHOUT TIME ZONE,
    "ResolvedAt"       TIMESTAMP(6) WITHOUT TIME ZONE,
    "ResolvedBy"       NUMERIC(20, 0),
    "ResolutionReason" TEXT,
    "DateAdded"        TIMESTAMP(6) WITHOUT TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS "IX_ChannelAccessApplications_ConfigId" ON "ChannelAccessApplications" ("ConfigId");
CREATE INDEX IF NOT EXISTS "IX_ChannelAccessApplications_GuildUser" ON "ChannelAccessApplications" ("GuildId", "UserId");
CREATE INDEX IF NOT EXISTS "IX_ChannelAccessApplications_Status" ON "ChannelAccessApplications" ("Status");

-- The applicant's answers, snapshotted so editing a question later does not rewrite history.
CREATE TABLE IF NOT EXISTS "ChannelAccessAnswers"
(
    "Id"            SERIAL PRIMARY KEY,
    "ApplicationId" INTEGER NOT NULL REFERENCES "ChannelAccessApplications" ("Id") ON DELETE CASCADE,
    "QuestionId"    INTEGER,
    "Position"      INTEGER NOT NULL DEFAULT 0,
    "Question"      TEXT    NOT NULL,
    "Answer"        TEXT    NOT NULL
);

CREATE INDEX IF NOT EXISTS "IX_ChannelAccessAnswers_ApplicationId" ON "ChannelAccessAnswers" ("ApplicationId");

-- One row per voter per application. Voting again updates the existing row.
CREATE TABLE IF NOT EXISTS "ChannelAccessVotes"
(
    "Id"            SERIAL PRIMARY KEY,
    "ApplicationId" INTEGER        NOT NULL REFERENCES "ChannelAccessApplications" ("Id") ON DELETE CASCADE,
    "UserId"        NUMERIC(20, 0) NOT NULL,
    "Vote"          INTEGER        NOT NULL, -- 1 approve, -1 deny, 0 abstain
    "Comment"       TEXT,
    "DateAdded"     TIMESTAMP(6) WITHOUT TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    UNIQUE ("ApplicationId", "UserId")
);

CREATE INDEX IF NOT EXISTS "IX_ChannelAccessVotes_ApplicationId" ON "ChannelAccessVotes" ("ApplicationId");

-- Users barred from applying, either to one gate or (ConfigId NULL) every gate in the guild.
CREATE TABLE IF NOT EXISTS "ChannelAccessBlacklists"
(
    "Id"        SERIAL PRIMARY KEY,
    "GuildId"   NUMERIC(20, 0) NOT NULL,
    "ConfigId"  INTEGER REFERENCES "ChannelAccessConfigs" ("Id") ON DELETE CASCADE,
    "UserId"    NUMERIC(20, 0) NOT NULL,
    "Reason"    TEXT,
    "AddedBy"   NUMERIC(20, 0) NOT NULL,
    "DateAdded" TIMESTAMP(6) WITHOUT TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS "IX_ChannelAccessBlacklists_GuildId" ON "ChannelAccessBlacklists" ("GuildId", "UserId");
