CREATE TABLE IF NOT EXISTS "AnalyticsBucket"
(
    "Id"         bigserial PRIMARY KEY,
    "Resolution" smallint         NOT NULL,
    "Bucket"     timestamp        NOT NULL,
    "Metric"     text             NOT NULL,
    "Labels"     text             NOT NULL DEFAULT '',
    "Count"      double precision NOT NULL DEFAULT 0,
    "Sum"        double precision NOT NULL DEFAULT 0,
    "Min"        double precision,
    "Max"        double precision,
    "Last"       double precision,
    "Hist"       text
);
CREATE UNIQUE INDEX IF NOT EXISTS "UX_AnalyticsBucket_Key" ON "AnalyticsBucket" ("Resolution", "Metric", "Labels", "Bucket");
CREATE INDEX IF NOT EXISTS "IX_AnalyticsBucket_Metric_Bucket" ON "AnalyticsBucket" ("Resolution", "Metric", "Bucket");

CREATE TABLE IF NOT EXISTS "AnalyticsCommandInvocation"
(
    "Id"           bigserial PRIMARY KEY,
    "At"           timestamp NOT NULL,
    "Bot"          text      NOT NULL,
    "Shard"        integer,
    "GuildId"      numeric(20, 0),
    "GuildSize"    integer,
    "Kind"         text      NOT NULL,
    "Module"       text,
    "Command"      text      NOT NULL,
    "Ok"           boolean   NOT NULL,
    "ErrorClass"   text,
    "ErrorMessage" text,
    "DurationMs"   integer   NOT NULL,
    "AckMs"        integer,
    "Language"     text
);
CREATE INDEX IF NOT EXISTS "IX_AnalyticsCommandInvocation_At" ON "AnalyticsCommandInvocation" ("At");
CREATE INDEX IF NOT EXISTS "IX_AnalyticsCommandInvocation_Guild_At" ON "AnalyticsCommandInvocation" ("GuildId", "At");
CREATE INDEX IF NOT EXISTS "IX_AnalyticsCommandInvocation_Command_At" ON "AnalyticsCommandInvocation" ("Command", "At");
CREATE INDEX IF NOT EXISTS "IX_AnalyticsCommandInvocation_Ok_At" ON "AnalyticsCommandInvocation" ("Ok", "At");

CREATE TABLE IF NOT EXISTS "AnalyticsFeatureActivity"
(
    "HourUtc" timestamp      NOT NULL,
    "GuildId" numeric(20, 0) NOT NULL,
    "Feature" text           NOT NULL,
    "Bot"     text           NOT NULL,
    "Count"   integer        NOT NULL DEFAULT 0,
    "Errors"  integer        NOT NULL DEFAULT 0,
    PRIMARY KEY ("HourUtc", "GuildId", "Feature")
);
CREATE INDEX IF NOT EXISTS "IX_AnalyticsFeatureActivity_Feature_Hour" ON "AnalyticsFeatureActivity" ("Feature", "HourUtc");

CREATE TABLE IF NOT EXISTS "AnalyticsGuildActivity"
(
    "Hour"      timestamp      NOT NULL,
    "GuildId"   numeric(20, 0) NOT NULL,
    "EventType" text           NOT NULL,
    "Bot"       text           NOT NULL,
    "Count"     integer        NOT NULL DEFAULT 0,
    PRIMARY KEY ("GuildId", "EventType", "Hour")
);
CREATE INDEX IF NOT EXISTS "IX_AnalyticsGuildActivity_Hour" ON "AnalyticsGuildActivity" ("Hour");
CREATE INDEX IF NOT EXISTS "IX_AnalyticsGuildActivity_Type_Hour" ON "AnalyticsGuildActivity" ("EventType", "Hour");

CREATE TABLE IF NOT EXISTS "AnalyticsGuildEventLog"
(
    "Id"        bigserial PRIMARY KEY,
    "At"        timestamp      NOT NULL,
    "GuildId"   numeric(20, 0) NOT NULL,
    "EventType" text           NOT NULL,
    "Bot"       text           NOT NULL,
    "Shard"     integer
);
CREATE INDEX IF NOT EXISTS "IX_AnalyticsGuildEventLog_Guild_At" ON "AnalyticsGuildEventLog" ("GuildId", "At");
CREATE INDEX IF NOT EXISTS "IX_AnalyticsGuildEventLog_Type_At" ON "AnalyticsGuildEventLog" ("EventType", "At");
CREATE INDEX IF NOT EXISTS "IX_AnalyticsGuildEventLog_At" ON "AnalyticsGuildEventLog" ("At");

CREATE TABLE IF NOT EXISTS "AnalyticsErrorSample"
(
    "Id"          bigserial PRIMARY KEY,
    "Hour"        timestamp NOT NULL,
    "Bot"         text      NOT NULL,
    "Shard"       integer,
    "Type"        text      NOT NULL,
    "Module"      text,
    "Location"    text,
    "MessageHash" text      NOT NULL,
    "Message"     text,
    "Count"       integer   NOT NULL DEFAULT 0,
    "FirstSeen"   timestamp NOT NULL,
    "LastSeen"    timestamp NOT NULL,
    "LastGuildId" numeric(20, 0)
);
CREATE UNIQUE INDEX IF NOT EXISTS "UX_AnalyticsErrorSample_Key" ON "AnalyticsErrorSample" ("Hour", "Bot", "Type", COALESCE("Module", ''), "MessageHash");
CREATE INDEX IF NOT EXISTS "IX_AnalyticsErrorSample_LastSeen" ON "AnalyticsErrorSample" ("LastSeen");

CREATE TABLE IF NOT EXISTS "AnalyticsDailySnapshot"
(
    "Day"          date    NOT NULL,
    "Bot"          text    NOT NULL,
    "Guilds"       integer NOT NULL DEFAULT 0,
    "Users"        bigint  NOT NULL DEFAULT 0,
    "FeaturesJson" text,
    PRIMARY KEY ("Day", "Bot")
);

CREATE TABLE IF NOT EXISTS "AnalyticsDailyTotal"
(
    "Day"    date             NOT NULL,
    "Bot"    text             NOT NULL,
    "Metric" text             NOT NULL,
    "Value"  double precision NOT NULL DEFAULT 0,
    PRIMARY KEY ("Day", "Bot", "Metric")
);
CREATE INDEX IF NOT EXISTS "IX_AnalyticsDailyTotal_Metric_Day" ON "AnalyticsDailyTotal" ("Metric", "Day");

CREATE TABLE IF NOT EXISTS "AnalyticsConfigCensus"
(
    "Day"    date             NOT NULL,
    "Metric" text             NOT NULL,
    "Value"  double precision NOT NULL DEFAULT 0,
    PRIMARY KEY ("Day", "Metric")
);
CREATE INDEX IF NOT EXISTS "IX_AnalyticsConfigCensus_Metric_Day" ON "AnalyticsConfigCensus" ("Metric", "Day");

CREATE TABLE IF NOT EXISTS "AnalyticsPageView"
(
    "Id"          bigserial PRIMARY KEY,
    "At"          timestamp NOT NULL,
    "Route"       text      NOT NULL,
    "Method"      text      NOT NULL,
    "Status"      smallint  NOT NULL,
    "DurationMs"  integer   NOT NULL DEFAULT 0,
    "VisitorHash" text,
    "Locale"      text,
    "Device"      text,
    "GuildSize"   integer,
    "IsOwner"     boolean   NOT NULL DEFAULT false
);
CREATE INDEX IF NOT EXISTS "IX_AnalyticsPageView_At" ON "AnalyticsPageView" ("At");
CREATE INDEX IF NOT EXISTS "IX_AnalyticsPageView_Route_At" ON "AnalyticsPageView" ("Route", "At");

CREATE TABLE IF NOT EXISTS "AnalyticsMetricRegistry"
(
    "Metric"     text PRIMARY KEY,
    "Kind"       text      NOT NULL,
    "LabelsJson" text      NOT NULL DEFAULT '{}',
    "LastSeen"   timestamp NOT NULL
);

CREATE TABLE IF NOT EXISTS "AnalyticsAlertRule"
(
    "Id"               serial PRIMARY KEY,
    "Name"             text             NOT NULL,
    "Description"      text,
    "Enabled"          boolean          NOT NULL DEFAULT true,
    "Severity"         text             NOT NULL,
    "Metric"           text             NOT NULL,
    "FiltersJson"      text,
    "GroupBy"          text,
    "Aggregation"      text             NOT NULL,
    "WindowSeconds"    integer          NOT NULL,
    "Comparator"       text             NOT NULL,
    "BaselineDays"     smallint,
    "Direction"        text,
    "Threshold"        double precision NOT NULL,
    "ThresholdHigh"    double precision,
    "ForSeconds"       integer          NOT NULL DEFAULT 0,
    "CooldownSeconds"  integer          NOT NULL DEFAULT 900,
    "RepeatSeconds"    integer,
    "WebhookUrl"       text             NOT NULL,
    "MentionRoleId"    numeric(20, 0),
    "ThreadId"         numeric(20, 0),
    "NotifyOnResolve"  boolean          NOT NULL DEFAULT true,
    "QuietStartMinute" smallint,
    "QuietEndMinute"   smallint,
    "MutedUntil"       timestamp,
    "MinSamples"       integer,
    "CreatedAt"        timestamp        NOT NULL,
    "UpdatedAt"        timestamp        NOT NULL,
    "CreatedBy"        numeric(20, 0)   NOT NULL
);

CREATE TABLE IF NOT EXISTS "AnalyticsAlertState"
(
    "RuleId"         integer   NOT NULL,
    "GroupKey"       text      NOT NULL,
    "State"          text      NOT NULL,
    "Since"          timestamp NOT NULL,
    "LastValue"      double precision,
    "LastNotifiedAt" timestamp,
    "BreachingSince" timestamp,
    PRIMARY KEY ("RuleId", "GroupKey")
);

CREATE TABLE IF NOT EXISTS "AnalyticsAlertEvent"
(
    "Id"        bigserial PRIMARY KEY,
    "RuleId"    integer          NOT NULL,
    "GroupKey"  text             NOT NULL,
    "At"        timestamp        NOT NULL,
    "FromState" text             NOT NULL,
    "ToState"   text             NOT NULL,
    "Value"     double precision,
    "Threshold" double precision NOT NULL,
    "Notified"  boolean          NOT NULL DEFAULT false
);
CREATE INDEX IF NOT EXISTS "IX_AnalyticsAlertEvent_Rule_At" ON "AnalyticsAlertEvent" ("RuleId", "At");
CREATE INDEX IF NOT EXISTS "IX_AnalyticsAlertEvent_At" ON "AnalyticsAlertEvent" ("At");
