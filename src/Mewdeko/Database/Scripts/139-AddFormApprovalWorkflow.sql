-- Form Approval Workflow
-- This migration adds approval workflow columns to the forms table
-- Allows forms to require manual approval with automatic role assignment on approval/rejection

-- Add approval workflow columns to forms table
ALTER TABLE forms
    ADD COLUMN IF NOT EXISTS require_approval      BOOLEAN DEFAULT FALSE NOT NULL,
    ADD COLUMN IF NOT EXISTS approval_action_type  INT     DEFAULT 0     NOT NULL,
    ADD COLUMN IF NOT EXISTS approval_role_ids     TEXT,
    ADD COLUMN IF NOT EXISTS rejection_action_type INT     DEFAULT 0     NOT NULL,
    ADD COLUMN IF NOT EXISTS rejection_role_ids    TEXT;

-- Create index for filtering forms that require approval
CREATE INDEX IF NOT EXISTS idx_forms_require_approval ON forms (require_approval);

-- Add comments
COMMENT ON COLUMN forms.require_approval IS 'Whether form submissions require manual approval before processing';
COMMENT ON COLUMN forms.approval_action_type IS 'Action to take on approval: 0=None, 1=AddRoles, 2=RemoveRoles';
COMMENT ON COLUMN forms.approval_role_ids IS 'Comma-separated role IDs to add/remove when response is approved';
COMMENT ON COLUMN forms.rejection_action_type IS 'Action to take on rejection: 0=None, 1=AddRoles, 2=RemoveRoles';
COMMENT ON COLUMN forms.rejection_role_ids IS 'Comma-separated role IDs to add/remove when response is rejected';

-- Rollback script (run manually if needed):
-- ALTER TABLE forms DROP COLUMN IF EXISTS require_approval;
-- ALTER TABLE forms DROP COLUMN IF EXISTS approval_action_type;
-- ALTER TABLE forms DROP COLUMN IF EXISTS approval_role_ids;
-- ALTER TABLE forms DROP COLUMN IF EXISTS rejection_action_type;
-- ALTER TABLE forms DROP COLUMN IF EXISTS rejection_role_ids;
-- DROP INDEX IF EXISTS idx_forms_require_approval;
