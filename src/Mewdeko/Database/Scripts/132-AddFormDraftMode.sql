-- Add draft mode support to forms
ALTER TABLE forms
    ADD COLUMN IF NOT EXISTS is_draft BOOLEAN DEFAULT FALSE;

-- Add index for querying non-draft forms
CREATE INDEX IF NOT EXISTS idx_forms_is_draft ON forms (is_draft) WHERE is_draft = FALSE;
