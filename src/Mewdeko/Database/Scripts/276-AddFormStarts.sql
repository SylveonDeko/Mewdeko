-- Migration: Remembers who began filling in a form, so the owner can compare people who started
-- against people who submitted. One row per form and person, written when they first touch the form.
-- Version: 276
-- Date: 2026-11-23

CREATE TABLE IF NOT EXISTS form_starts
(
    form_id    INT            NOT NULL REFERENCES forms (id) ON DELETE CASCADE,
    user_id    NUMERIC(20, 0) NOT NULL,
    started_at TIMESTAMP      NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    PRIMARY KEY (form_id, user_id)
);

CREATE INDEX IF NOT EXISTS idx_form_starts_started_at
    ON form_starts (started_at);

-- Rollback script (commented):
-- DROP TABLE IF EXISTS form_starts;
