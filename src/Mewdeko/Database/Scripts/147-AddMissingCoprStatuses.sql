-- Add missing COPR build status notification columns
ALTER TABLE CoprMonitors
    ADD COLUMN IF NOT EXISTS NotifyOnStarting  BOOLEAN NOT NULL DEFAULT FALSE,
    ADD COLUMN IF NOT EXISTS NotifyOnImporting BOOLEAN NOT NULL DEFAULT FALSE,
    ADD COLUMN IF NOT EXISTS NotifyOnForked    BOOLEAN NOT NULL DEFAULT FALSE,
    ADD COLUMN IF NOT EXISTS NotifyOnWaiting   BOOLEAN NOT NULL DEFAULT FALSE;

-- Add custom message columns for the new statuses
ALTER TABLE CoprMonitors
    ADD COLUMN IF NOT EXISTS StartingMessage  TEXT,
    ADD COLUMN IF NOT EXISTS ImportingMessage TEXT,
    ADD COLUMN IF NOT EXISTS ForkedMessage    TEXT,
    ADD COLUMN IF NOT EXISTS WaitingMessage   TEXT;
