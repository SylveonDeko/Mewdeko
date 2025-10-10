-- Add anonymous submission support to forms
ALTER TABLE forms
    ADD COLUMN IF NOT EXISTS allow_anonymous BOOLEAN DEFAULT FALSE;

-- Modify user_id to be nullable for anonymous submissions
ALTER TABLE form_responses
    ALTER COLUMN user_id DROP NOT NULL;
ALTER TABLE form_responses
    ALTER COLUMN user_id SET DEFAULT NULL;

-- Add index for filtering anonymous responses
CREATE INDEX IF NOT EXISTS idx_form_responses_anonymous ON form_responses (form_id) WHERE user_id IS NULL;
