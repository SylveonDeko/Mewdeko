-- Form Workflow System
-- This migration adds support for ban appeals, join applications, and response workflows
-- Using integers for enum types for better linq2db compatibility

-- Add new columns to forms table
ALTER TABLE forms
    ADD COLUMN IF NOT EXISTS form_type                INT     DEFAULT 0     NOT NULL,
    ADD COLUMN IF NOT EXISTS allow_external_users     BOOLEAN DEFAULT FALSE NOT NULL,
    ADD COLUMN IF NOT EXISTS auto_approve_role_ids    TEXT,
    ADD COLUMN IF NOT EXISTS invite_max_uses          INT     DEFAULT 1,
    ADD COLUMN IF NOT EXISTS invite_max_age           INT     DEFAULT 86400,
    ADD COLUMN IF NOT EXISTS notification_webhook_url TEXT;

-- Create index on form_type for filtering
CREATE INDEX IF NOT EXISTS idx_forms_form_type ON forms (form_type);

-- Create form response workflow table
CREATE TABLE IF NOT EXISTS form_response_workflows
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

-- Create indexes for workflow queries
CREATE INDEX IF NOT EXISTS idx_form_response_workflows_response_id ON form_response_workflows (response_id);
CREATE INDEX IF NOT EXISTS idx_form_response_workflows_status ON form_response_workflows (status);
CREATE INDEX IF NOT EXISTS idx_form_response_workflows_reviewed_by ON form_response_workflows (reviewed_by);
CREATE INDEX IF NOT EXISTS idx_form_response_workflows_created_at ON form_response_workflows (created_at);

-- Update form_responses to allow nullable user_id for external submissions
ALTER TABLE form_responses
    ALTER COLUMN user_id DROP NOT NULL;

-- Add comments
COMMENT ON COLUMN forms.form_type IS 'Type of form: 0=Regular, 1=BanAppeal, 2=JoinApplication';
COMMENT ON COLUMN forms.allow_external_users IS 'Allow users not in the guild to submit (for ban appeals and join applications)';
COMMENT ON COLUMN forms.auto_approve_role_ids IS 'Comma-separated role IDs to pre-assign on join (for join applications)';
COMMENT ON COLUMN forms.invite_max_uses IS 'Maximum uses for generated invite links (join applications)';
COMMENT ON COLUMN forms.invite_max_age IS 'Invite expiry time in seconds (join applications)';
COMMENT ON COLUMN forms.notification_webhook_url IS 'Optional webhook URL for status update notifications';

COMMENT ON TABLE form_response_workflows IS 'Tracks approval/rejection workflow for form responses';
COMMENT ON COLUMN form_response_workflows.status IS 'Current status: 0=Pending, 1=UnderReview, 2=Approved, 3=Rejected';
COMMENT ON COLUMN form_response_workflows.reviewed_by IS 'Discord user ID of the moderator who reviewed';
COMMENT ON COLUMN form_response_workflows.review_notes IS 'Moderator notes explaining the decision';
COMMENT ON COLUMN form_response_workflows.action_taken IS 'Action performed: 0=None, 1=Unbanned, 2=InviteSent, 3=RolesPreassigned';
COMMENT ON COLUMN form_response_workflows.invite_code IS 'Generated invite code for approved join applications';
COMMENT ON COLUMN form_response_workflows.invite_expires_at IS 'Expiration date for the invite code';
