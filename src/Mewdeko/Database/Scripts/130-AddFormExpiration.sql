-- Add expiration support to forms
ALTER TABLE forms
    ADD COLUMN IF NOT EXISTS expires_at TIMESTAMP DEFAULT NULL;

-- Add index for efficient expiration checks
CREATE INDEX IF NOT EXISTS idx_forms_expires_at ON forms (expires_at) WHERE expires_at IS NOT NULL;
