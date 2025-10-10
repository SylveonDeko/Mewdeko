-- Add required role and custom success message to forms
ALTER TABLE forms
    ADD COLUMN IF NOT EXISTS required_role_id BIGINT DEFAULT NULL;
ALTER TABLE forms
    ADD COLUMN IF NOT EXISTS success_message TEXT DEFAULT NULL;

-- Add index for role-restricted forms
CREATE INDEX IF NOT EXISTS idx_forms_required_role ON forms (required_role_id) WHERE required_role_id IS NOT NULL;
