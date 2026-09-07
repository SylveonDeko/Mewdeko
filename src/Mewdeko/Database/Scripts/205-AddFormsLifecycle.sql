-- Migration: Forms lifecycle. Adds scheduling, appeal policy, decision role splits,
-- answer denormalization, versioning, drafts and response revisions.
-- Version: 205
-- Date: 2026-09-07

DO
$$
    BEGIN
        -- Scheduling: when a form starts accepting responses, and the announcement posted then.
        IF NOT EXISTS (SELECT 1
                       FROM information_schema.columns
                       WHERE table_name = 'forms'
                         AND column_name = 'opens_at') THEN
            ALTER TABLE forms
                ADD COLUMN opens_at TIMESTAMP;
            COMMENT ON COLUMN forms.opens_at IS 'When the form begins accepting responses. Null means immediately.';
        END IF;

        IF NOT EXISTS (SELECT 1
                       FROM information_schema.columns
                       WHERE table_name = 'forms'
                         AND column_name = 'announce_channel_id') THEN
            ALTER TABLE forms
                ADD COLUMN announce_channel_id NUMERIC(20, 0);
            COMMENT ON COLUMN forms.announce_channel_id IS 'Channel the launch announcement is posted to.';
        END IF;

        IF NOT EXISTS (SELECT 1
                       FROM information_schema.columns
                       WHERE table_name = 'forms'
                         AND column_name = 'announce_role_id') THEN
            ALTER TABLE forms
                ADD COLUMN announce_role_id NUMERIC(20, 0);
            COMMENT ON COLUMN forms.announce_role_id IS 'Role pinged by the launch announcement.';
        END IF;

        IF NOT EXISTS (SELECT 1
                       FROM information_schema.columns
                       WHERE table_name = 'forms'
                         AND column_name = 'announce_message') THEN
            ALTER TABLE forms
                ADD COLUMN announce_message TEXT;
        END IF;

        IF NOT EXISTS (SELECT 1
                       FROM information_schema.columns
                       WHERE table_name = 'forms'
                         AND column_name = 'announced_at') THEN
            ALTER TABLE forms
                ADD COLUMN announced_at TIMESTAMP;
            COMMENT ON COLUMN forms.announced_at IS 'Stamped before the announcement is sent so a failure is not retried forever.';
        END IF;

        -- Reviewer notification and role handling around submission.
        IF NOT EXISTS (SELECT 1
                       FROM information_schema.columns
                       WHERE table_name = 'forms'
                         AND column_name = 'notify_role_id') THEN
            ALTER TABLE forms
                ADD COLUMN notify_role_id NUMERIC(20, 0);
            COMMENT ON COLUMN forms.notify_role_id IS 'Role pinged when a response arrives in the submit channel.';
        END IF;

        IF NOT EXISTS (SELECT 1
                       FROM information_schema.columns
                       WHERE table_name = 'forms'
                         AND column_name = 'submit_role_ids') THEN
            ALTER TABLE forms
                ADD COLUMN submit_role_ids TEXT;
            COMMENT ON COLUMN forms.submit_role_ids IS 'Comma separated roles granted the moment a response is submitted.';
        END IF;

        IF NOT EXISTS (SELECT 1
                       FROM information_schema.columns
                       WHERE table_name = 'forms'
                         AND column_name = 'pending_role_id') THEN
            ALTER TABLE forms
                ADD COLUMN pending_role_id NUMERIC(20, 0);
            COMMENT ON COLUMN forms.pending_role_id IS 'Role held while a response awaits review, removed on decision.';
        END IF;

        -- Eligibility gates.
        IF NOT EXISTS (SELECT 1
                       FROM information_schema.columns
                       WHERE table_name = 'forms'
                         AND column_name = 'min_account_age_days') THEN
            ALTER TABLE forms
                ADD COLUMN min_account_age_days INT;
        END IF;

        IF NOT EXISTS (SELECT 1
                       FROM information_schema.columns
                       WHERE table_name = 'forms'
                         AND column_name = 'allow_resubmit_after_rejection') THEN
            ALTER TABLE forms
                ADD COLUMN allow_resubmit_after_rejection BOOLEAN DEFAULT FALSE NOT NULL;
            COMMENT ON COLUMN forms.allow_resubmit_after_rejection IS 'Lets a rejected user submit again without opening the form to unlimited submissions.';
        END IF;

        -- Appeal policy.
        IF NOT EXISTS (SELECT 1
                       FROM information_schema.columns
                       WHERE table_name = 'forms'
                         AND column_name = 'block_reappeal_after_rejection') THEN
            ALTER TABLE forms
                ADD COLUMN block_reappeal_after_rejection BOOLEAN DEFAULT FALSE NOT NULL;
        END IF;

        IF NOT EXISTS (SELECT 1
                       FROM information_schema.columns
                       WHERE table_name = 'forms'
                         AND column_name = 'max_appeal_attempts') THEN
            ALTER TABLE forms
                ADD COLUMN max_appeal_attempts INT;
        END IF;

        IF NOT EXISTS (SELECT 1
                       FROM information_schema.columns
                       WHERE table_name = 'forms'
                         AND column_name = 'reappeal_cooldown_days') THEN
            ALTER TABLE forms
                ADD COLUMN reappeal_cooldown_days INT;
        END IF;

        IF NOT EXISTS (SELECT 1
                       FROM information_schema.columns
                       WHERE table_name = 'forms'
                         AND column_name = 'appeal_delay_days') THEN
            ALTER TABLE forms
                ADD COLUMN appeal_delay_days INT;
            COMMENT ON COLUMN forms.appeal_delay_days IS 'Days after the ban before an appeal may be filed.';
        END IF;

        -- Split decision roles so one decision can both add and remove.
        IF NOT EXISTS (SELECT 1
                       FROM information_schema.columns
                       WHERE table_name = 'forms'
                         AND column_name = 'approval_add_role_ids') THEN
            ALTER TABLE forms
                ADD COLUMN approval_add_role_ids TEXT;
        END IF;

        IF NOT EXISTS (SELECT 1
                       FROM information_schema.columns
                       WHERE table_name = 'forms'
                         AND column_name = 'approval_remove_role_ids') THEN
            ALTER TABLE forms
                ADD COLUMN approval_remove_role_ids TEXT;
        END IF;

        IF NOT EXISTS (SELECT 1
                       FROM information_schema.columns
                       WHERE table_name = 'forms'
                         AND column_name = 'rejection_add_role_ids') THEN
            ALTER TABLE forms
                ADD COLUMN rejection_add_role_ids TEXT;
        END IF;

        IF NOT EXISTS (SELECT 1
                       FROM information_schema.columns
                       WHERE table_name = 'forms'
                         AND column_name = 'rejection_remove_role_ids') THEN
            ALTER TABLE forms
                ADD COLUMN rejection_remove_role_ids TEXT;
        END IF;

        -- Illustrated questions.
        IF NOT EXISTS (SELECT 1
                       FROM information_schema.columns
                       WHERE table_name = 'form_questions'
                         AND column_name = 'image_url') THEN
            ALTER TABLE form_questions
                ADD COLUMN image_url VARCHAR(500);
        END IF;

        -- Answer denormalization, so a response stays readable after its question is edited.
        IF NOT EXISTS (SELECT 1
                       FROM information_schema.columns
                       WHERE table_name = 'form_answers'
                         AND column_name = 'question_text') THEN
            ALTER TABLE form_answers
                ADD COLUMN question_text TEXT;
        END IF;

        IF NOT EXISTS (SELECT 1
                       FROM information_schema.columns
                       WHERE table_name = 'form_answers'
                         AND column_name = 'question_type') THEN
            ALTER TABLE form_answers
                ADD COLUMN question_type VARCHAR(50);
        END IF;

        IF NOT EXISTS (SELECT 1
                       FROM information_schema.columns
                       WHERE table_name = 'form_answers'
                         AND column_name = 'answer_display') THEN
            ALTER TABLE form_answers
                ADD COLUMN answer_display TEXT;
            COMMENT ON COLUMN form_answers.answer_display IS 'Answer rendered for reading, with option values resolved to their labels.';
        END IF;

        IF NOT EXISTS (SELECT 1
                       FROM information_schema.columns
                       WHERE table_name = 'form_answers'
                         AND column_name = 'form_version_id') THEN
            ALTER TABLE form_answers
                ADD COLUMN form_version_id INT;
            COMMENT ON COLUMN form_answers.form_version_id IS 'The form version this answer was given against.';
        END IF;

        -- Response editing.
        IF NOT EXISTS (SELECT 1
                       FROM information_schema.columns
                       WHERE table_name = 'form_responses'
                         AND column_name = 'edited_at') THEN
            ALTER TABLE form_responses
                ADD COLUMN edited_at TIMESTAMP;
        END IF;

        IF NOT EXISTS (SELECT 1
                       FROM information_schema.columns
                       WHERE table_name = 'form_responses'
                         AND column_name = 'form_version_id') THEN
            ALTER TABLE form_responses
                ADD COLUMN form_version_id INT;
        END IF;

        -- Delivery failures on a review decision.
        IF NOT EXISTS (SELECT 1
                       FROM information_schema.columns
                       WHERE table_name = 'form_response_workflows'
                         AND column_name = 'dm_failed') THEN
            ALTER TABLE form_response_workflows
                ADD COLUMN dm_failed BOOLEAN DEFAULT FALSE NOT NULL;
        END IF;
    END
$$;

-- Backfill the denormalized answer columns for responses stored before this migration.
UPDATE form_answers a
SET question_text = q.question_text,
    question_type = q.question_type
FROM form_questions q
WHERE a.question_id = q.id
  AND a.question_text IS NULL;

UPDATE form_answers
SET answer_display = COALESCE(answer_text, array_to_string(answer_values, ', '))
WHERE answer_display IS NULL;

-- A saved snapshot of a form, taken every time it is saved, so edits can be reviewed and undone.
CREATE TABLE IF NOT EXISTS form_versions
(
    id             SERIAL PRIMARY KEY,
    form_id        INT       NOT NULL REFERENCES forms (id) ON DELETE CASCADE,
    version_number INT       NOT NULL,
    snapshot       TEXT      NOT NULL,
    question_count INT       NOT NULL DEFAULT 0,
    created_by     NUMERIC(20, 0),
    created_at     TIMESTAMP NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc')
);

CREATE UNIQUE INDEX IF NOT EXISTS idx_form_versions_form_number ON form_versions (form_id, version_number);
CREATE INDEX IF NOT EXISTS idx_form_versions_form_id ON form_versions (form_id, created_at DESC);

-- A snapshot of a response's answers taken before it is edited, so the original survives.
CREATE TABLE IF NOT EXISTS form_response_revisions
(
    id          SERIAL PRIMARY KEY,
    response_id INT       NOT NULL REFERENCES form_responses (id) ON DELETE CASCADE,
    snapshot    TEXT      NOT NULL,
    edited_by   NUMERIC(20, 0),
    created_at  TIMESTAMP NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc')
);

CREATE INDEX IF NOT EXISTS idx_form_response_revisions_response_id
    ON form_response_revisions (response_id, created_at DESC);

-- A partly filled form, kept so a long submission survives a closed tab.
CREATE TABLE IF NOT EXISTS form_response_drafts
(
    id         SERIAL PRIMARY KEY,
    form_id    INT            NOT NULL REFERENCES forms (id) ON DELETE CASCADE,
    user_id    NUMERIC(20, 0) NOT NULL,
    answers    TEXT           NOT NULL,
    page       INT            NOT NULL DEFAULT 0,
    updated_at TIMESTAMP      NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc')
);

CREATE UNIQUE INDEX IF NOT EXISTS idx_form_response_drafts_form_user
    ON form_response_drafts (form_id, user_id);

CREATE INDEX IF NOT EXISTS idx_form_responses_form_user
    ON form_responses (form_id, user_id);

-- Rollback script (commented):
-- DROP TABLE IF EXISTS form_response_drafts;
-- DROP TABLE IF EXISTS form_response_revisions;
-- DROP TABLE IF EXISTS form_versions;
-- ALTER TABLE form_response_workflows DROP COLUMN IF EXISTS dm_failed;
-- ALTER TABLE form_responses DROP COLUMN IF EXISTS edited_at, DROP COLUMN IF EXISTS form_version_id;
-- ALTER TABLE form_answers DROP COLUMN IF EXISTS question_text, DROP COLUMN IF EXISTS question_type,
--     DROP COLUMN IF EXISTS answer_display, DROP COLUMN IF EXISTS form_version_id;
-- ALTER TABLE form_questions DROP COLUMN IF EXISTS image_url;
