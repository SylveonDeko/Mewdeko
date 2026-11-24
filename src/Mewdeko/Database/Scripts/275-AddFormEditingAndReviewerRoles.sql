-- Migration: Lets a form switch off editing after submission, and names several reviewer roles
-- instead of one. The single reviewer role is copied into the list and left in place so older form
-- versions still restore.
-- Version: 275
-- Date: 2026-11-23

DO
$$
    BEGIN
        IF NOT EXISTS (SELECT 1
                       FROM information_schema.columns
                       WHERE table_name = 'forms'
                         AND column_name = 'allow_editing') THEN
            ALTER TABLE forms
                ADD COLUMN allow_editing BOOLEAN NOT NULL DEFAULT TRUE;
            COMMENT ON COLUMN forms.allow_editing IS 'Whether a submitter may change their answers after sending, until the response is decided.';
        END IF;

        IF NOT EXISTS (SELECT 1
                       FROM information_schema.columns
                       WHERE table_name = 'forms'
                         AND column_name = 'reviewer_role_ids') THEN
            ALTER TABLE forms
                ADD COLUMN reviewer_role_ids TEXT;
            COMMENT ON COLUMN forms.reviewer_role_ids IS 'Comma separated roles permitted to decide this form''s responses from Discord. Null falls back to Manage Server.';

            UPDATE forms
            SET reviewer_role_ids = reviewer_role_id::TEXT
            WHERE reviewer_role_id IS NOT NULL
              AND reviewer_role_id > 0;
        END IF;
    END
$$;

-- Rollback script (commented):
-- ALTER TABLE forms DROP COLUMN IF EXISTS allow_editing, DROP COLUMN IF EXISTS reviewer_role_ids;
