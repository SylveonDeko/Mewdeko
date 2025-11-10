CREATE TABLE IF NOT EXISTS CoprMonitors
(
    Id                SERIAL PRIMARY KEY,
    GuildId           BIGINT    NOT NULL,
    ChannelId         BIGINT    NOT NULL,
    CoprOwner         TEXT      NOT NULL,
    CoprProject       TEXT      NOT NULL,
    IsEnabled         BOOLEAN   NOT NULL DEFAULT TRUE,

    -- Package filtering (NULL = all packages)
    PackageFilter     TEXT,

    -- Status filtering
    NotifyOnSucceeded BOOLEAN   NOT NULL DEFAULT TRUE,
    NotifyOnFailed    BOOLEAN   NOT NULL DEFAULT TRUE,
    NotifyOnCanceled  BOOLEAN   NOT NULL DEFAULT FALSE,
    NotifyOnPending   BOOLEAN   NOT NULL DEFAULT FALSE,
    NotifyOnRunning   BOOLEAN   NOT NULL DEFAULT FALSE,
    NotifyOnSkipped   BOOLEAN   NOT NULL DEFAULT FALSE,

    -- Custom messages per status
    SucceededMessage  TEXT,
    FailedMessage     TEXT,
    CanceledMessage   TEXT,
    PendingMessage    TEXT,
    RunningMessage    TEXT,
    SkippedMessage    TEXT,
    DefaultMessage    TEXT,

    DateAdded         TIMESTAMP NOT NULL DEFAULT NOW(),

    CONSTRAINT unique_copr_monitor UNIQUE (GuildId, CoprOwner, CoprProject, ChannelId, PackageFilter)
);

CREATE INDEX IF NOT EXISTS idx_coprmonitors_guild ON CoprMonitors (GuildId);
CREATE INDEX IF NOT EXISTS idx_coprmonitors_enabled ON CoprMonitors (IsEnabled);
CREATE INDEX IF NOT EXISTS idx_coprmonitors_project ON CoprMonitors (CoprOwner, CoprProject);
