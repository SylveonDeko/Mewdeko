-- Add Status Check Token to Form Response Workflows
-- This migration adds a secure token for checking response status (prevents ID enumeration)

-- Clean up existing workflows and responses (since we're adding a required field)
DELETE
FROM form_response_workflows;
DELETE
FROM form_responses;

-- Add status_check_token column as NOT NULL with unique constraint
ALTER TABLE form_response_workflows
    ADD COLUMN IF NOT EXISTS status_check_token VARCHAR(64) NOT NULL DEFAULT 'MIGRATION_PLACEHOLDER',
    ADD CONSTRAINT IF NOT EXISTS form_response_workflows_status_check_token_unique UNIQUE (status_check_token);

-- Remove default now that constraint is added
ALTER TABLE form_response_workflows
    ALTER COLUMN status_check_token DROP DEFAULT;

-- Create index for fast lookups
CREATE INDEX IF NOT EXISTS idx_form_response_workflows_status_check_token ON form_response_workflows (status_check_token);

-- Update comment
COMMENT ON COLUMN form_response_workflows.status_check_token IS 'Unique token for checking response status securely (prevents ID enumeration)';
