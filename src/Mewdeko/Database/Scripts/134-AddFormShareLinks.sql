-- Add share link system for forms
CREATE TABLE IF NOT EXISTS form_share_links
(
    id                  SERIAL PRIMARY KEY,
    share_code          VARCHAR(32) UNIQUE NOT NULL,
    form_id             INT                NOT NULL REFERENCES forms (id) ON DELETE CASCADE,
    instance_identifier VARCHAR(100)       NOT NULL, -- Instance name or identifier
    created_at          TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    expires_at          TIMESTAMP DEFAULT NULL,
    is_active           BOOLEAN   DEFAULT TRUE
);

CREATE INDEX IF NOT EXISTS idx_form_share_links_share_code ON form_share_links (share_code);
CREATE INDEX IF NOT EXISTS idx_form_share_links_form_id ON form_share_links (form_id);
CREATE INDEX IF NOT EXISTS idx_form_share_links_active ON form_share_links (is_active) WHERE is_active = TRUE;

COMMENT ON TABLE form_share_links IS 'Encrypted/encoded share links for forms with instance routing';
COMMENT ON COLUMN form_share_links.share_code IS 'Unique encrypted code used in URL (e.g., /forms/abc123def)';
COMMENT ON COLUMN form_share_links.instance_identifier IS 'Instance identifier (port or name) for multi-instance routing';
