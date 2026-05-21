CREATE TABLE IF NOT EXISTS "DashboardAuditLogs"
(
    "Id"         SERIAL PRIMARY KEY,
    "GuildId"    NUMERIC(20, 0)              NOT NULL,
    "UserId"     NUMERIC(20, 0)              NOT NULL,
    "UserName"   TEXT                        NOT NULL DEFAULT '',
    "Action"     INTEGER                     NOT NULL DEFAULT 0,
    "Section"    TEXT                        NOT NULL DEFAULT '',
    "Endpoint"   TEXT                        NOT NULL DEFAULT '',
    "HttpMethod" TEXT                        NOT NULL DEFAULT '',
    "Changes"    JSONB                       NULL,
    "UserAgent"  TEXT                        NULL,
    "DateAdded"  TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc')
);

CREATE INDEX IF NOT EXISTS "IX_DashboardAuditLogs_GuildId_DateAdded"
    ON "DashboardAuditLogs" ("GuildId", "DateAdded" DESC);

CREATE INDEX IF NOT EXISTS "IX_DashboardAuditLogs_UserId"
    ON "DashboardAuditLogs" ("UserId");
