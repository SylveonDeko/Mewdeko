-- Convert Form Enums to Integers
-- This migration converts PostgreSQL enum columns to integers for linq2db compatibility

-- Drop existing columns and recreate as integers
ALTER TABLE forms
    DROP COLUMN IF EXISTS form_type CASCADE;
ALTER TABLE forms
    ADD COLUMN form_type INT DEFAULT 0 NOT NULL;

-- Drop and recreate workflow table with integer columns
DROP TABLE IF EXISTS form_response_workflows CASCADE;
CREATE TABLE form_response_workflows
(
    id                SERIAL PRIMARY KEY,
    response_id       INT                 NOT NULL REFERENCES form_responses (id) ON DELETE CASCADE,
    status            INT       DEFAULT 0 NOT NULL,
    reviewed_by       BIGINT,
    reviewed_at       TIMESTAMP,
    review_notes      TEXT,
    action_taken      INT       DEFAULT 0 NOT NULL,
    invite_code       VARCHAR(50),
    invite_expires_at TIMESTAMP,
    created_at        TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at        TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Recreate indexes
CREATE INDEX IF NOT EXISTS idx_forms_form_type ON forms (form_type);
CREATE INDEX IF NOT EXISTS idx_form_response_workflows_response_id ON form_response_workflows (response_id);
CREATE INDEX IF NOT EXISTS idx_form_response_workflows_status ON form_response_workflows (status);
CREATE INDEX IF NOT EXISTS idx_form_response_workflows_reviewed_by ON form_response_workflows (reviewed_by);
CREATE INDEX IF NOT EXISTS idx_form_response_workflows_created_at ON form_response_workflows (created_at);

-- Drop the enum types
DROP TYPE IF EXISTS form_type CASCADE;
DROP TYPE IF EXISTS response_status CASCADE;
DROP TYPE IF EXISTS workflow_action CASCADE;

-- Update comments
COMMENT ON COLUMN forms.form_type IS 'Type of form: 0=Regular, 1=BanAppeal, 2=JoinApplication';
COMMENT ON TABLE form_response_workflows IS 'Tracks approval/rejection workflow for form responses';
COMMENT ON COLUMN form_response_workflows.status IS 'Current status: 0=Pending, 1=UnderReview, 2=Approved, 3=Rejected';
COMMENT ON COLUMN form_response_workflows.action_taken IS 'Action performed: 0=None, 1=Unbanned, 2=InviteSent, 3=RolesPreassigned';
