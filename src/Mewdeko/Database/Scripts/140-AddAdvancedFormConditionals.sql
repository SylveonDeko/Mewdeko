-- Migration: Add advanced conditional logic and Discord-specific conditionals to forms
-- Version: 140
-- Date: 2025-10-13

-- Add advanced conditional columns to form_questions
DO
$$
    BEGIN
        -- Conditional type (0=QuestionBased, 1=DiscordRole, 2=ServerTenure, 3=BoostStatus, 4=Permission, 5=MultipleConditions)
        IF NOT EXISTS (SELECT 1
                       FROM information_schema.columns
                       WHERE table_name = 'form_questions'
                         AND column_name = 'conditional_type') THEN
            ALTER TABLE form_questions
                ADD COLUMN conditional_type INT DEFAULT 0 NOT NULL;
            COMMENT ON COLUMN form_questions.conditional_type IS 'Type of conditional logic: 0=QuestionBased, 1=DiscordRole, 2=ServerTenure, 3=BoostStatus, 4=Permission, 5=MultipleConditions';
        END IF;

        -- Discord role-based conditionals
        IF NOT EXISTS (SELECT 1
                       FROM information_schema.columns
                       WHERE table_name = 'form_questions'
                         AND column_name = 'conditional_role_ids') THEN
            ALTER TABLE form_questions
                ADD COLUMN conditional_role_ids TEXT;
            COMMENT ON COLUMN form_questions.conditional_role_ids IS 'Comma-separated role IDs for role-based conditionals';
        END IF;

        IF NOT EXISTS (SELECT 1
                       FROM information_schema.columns
                       WHERE table_name = 'form_questions'
                         AND column_name = 'conditional_role_logic') THEN
            ALTER TABLE form_questions
                ADD COLUMN conditional_role_logic VARCHAR(10);
            COMMENT ON COLUMN form_questions.conditional_role_logic IS 'Role logic type: any, all, none';
        END IF;

        -- Server tenure conditionals
        IF NOT EXISTS (SELECT 1
                       FROM information_schema.columns
                       WHERE table_name = 'form_questions'
                         AND column_name = 'conditional_days_in_server') THEN
            ALTER TABLE form_questions
                ADD COLUMN conditional_days_in_server INT;
            COMMENT ON COLUMN form_questions.conditional_days_in_server IS 'Minimum days user must be in server';
        END IF;

        IF NOT EXISTS (SELECT 1
                       FROM information_schema.columns
                       WHERE table_name = 'form_questions'
                         AND column_name = 'conditional_account_age_days') THEN
            ALTER TABLE form_questions
                ADD COLUMN conditional_account_age_days INT;
            COMMENT ON COLUMN form_questions.conditional_account_age_days IS 'Minimum Discord account age in days';
        END IF;

        -- Boost/Premium conditionals
        IF NOT EXISTS (SELECT 1
                       FROM information_schema.columns
                       WHERE table_name = 'form_questions'
                         AND column_name = 'conditional_requires_boost') THEN
            ALTER TABLE form_questions
                ADD COLUMN conditional_requires_boost BOOLEAN;
            COMMENT ON COLUMN form_questions.conditional_requires_boost IS 'Whether user must be boosting the server';
        END IF;

        IF NOT EXISTS (SELECT 1
                       FROM information_schema.columns
                       WHERE table_name = 'form_questions'
                         AND column_name = 'conditional_requires_nitro') THEN
            ALTER TABLE form_questions
                ADD COLUMN conditional_requires_nitro BOOLEAN;
            COMMENT ON COLUMN form_questions.conditional_requires_nitro IS 'Whether user must have Discord Nitro';
        END IF;

        -- Permission-based conditionals
        IF NOT EXISTS (SELECT 1
                       FROM information_schema.columns
                       WHERE table_name = 'form_questions'
                         AND column_name = 'conditional_permission_flags') THEN
            ALTER TABLE form_questions
                ADD COLUMN conditional_permission_flags BIGINT;
            COMMENT ON COLUMN form_questions.conditional_permission_flags IS 'GuildPermissions flags user must have';
        END IF;

        -- Conditional required (make question required based on conditions)
        IF NOT EXISTS (SELECT 1
                       FROM information_schema.columns
                       WHERE table_name = 'form_questions'
                         AND column_name = 'required_when_parent_question_id') THEN
            ALTER TABLE form_questions
                ADD COLUMN required_when_parent_question_id INT;
            COMMENT ON COLUMN form_questions.required_when_parent_question_id IS 'Question ID that determines if this is required';
        END IF;

        IF NOT EXISTS (SELECT 1
                       FROM information_schema.columns
                       WHERE table_name = 'form_questions'
                         AND column_name = 'required_when_operator') THEN
            ALTER TABLE form_questions
                ADD COLUMN required_when_operator VARCHAR(20);
            COMMENT ON COLUMN form_questions.required_when_operator IS 'Operator for required condition';
        END IF;

        IF NOT EXISTS (SELECT 1
                       FROM information_schema.columns
                       WHERE table_name = 'form_questions'
                         AND column_name = 'required_when_value') THEN
            ALTER TABLE form_questions
                ADD COLUMN required_when_value TEXT;
            COMMENT ON COLUMN form_questions.required_when_value IS 'Expected value for required condition';
        END IF;

        -- Answer piping support
        IF NOT EXISTS (SELECT 1
                       FROM information_schema.columns
                       WHERE table_name = 'form_questions'
                         AND column_name = 'enable_answer_piping') THEN
            ALTER TABLE form_questions
                ADD COLUMN enable_answer_piping BOOLEAN DEFAULT FALSE;
            COMMENT ON COLUMN form_questions.enable_answer_piping IS 'Whether question text contains {{QX}} placeholders for answer piping';
        END IF;
    END
$$;

-- Create table for multiple conditions (for AND/OR logic)
CREATE TABLE IF NOT EXISTS form_question_conditions
(
    id                 SERIAL PRIMARY KEY,
    question_id        INT                   NOT NULL REFERENCES form_questions (id) ON DELETE CASCADE,
    condition_group    INT         DEFAULT 0,          -- For grouping AND conditions
    condition_type     INT         DEFAULT 0 NOT NULL, -- 0=Question, 1=Role, 2=Tenure, etc.
    target_question_id INT,
    target_role_ids    TEXT,
    operator           VARCHAR(20),
    expected_value     TEXT,
    days_threshold     INT,
    requires_boost     BOOLEAN,
    requires_nitro     BOOLEAN,
    permission_flags   BIGINT,
    logic_type         VARCHAR(10) DEFAULT 'AND',      -- "AND" or "OR"
    created_at         TIMESTAMP   DEFAULT CURRENT_TIMESTAMP
);

COMMENT ON TABLE form_question_conditions IS 'Multiple conditions for advanced question visibility logic';
COMMENT ON COLUMN form_question_conditions.condition_group IS 'Group number for OR logic (conditions in same group are ANDed, different groups are ORed)';
COMMENT ON COLUMN form_question_conditions.logic_type IS 'How to combine conditions: AND or OR';

-- Create index for efficient condition lookups
CREATE INDEX IF NOT EXISTS idx_form_question_conditions_question_id ON form_question_conditions (question_id);

-- Rollback script (commented):
-- DROP INDEX IF EXISTS idx_form_question_conditions_question_id;
-- DROP TABLE IF EXISTS form_question_conditions;
-- ALTER TABLE form_questions DROP COLUMN IF EXISTS conditional_type;
-- ALTER TABLE form_questions DROP COLUMN IF EXISTS conditional_role_ids;
-- ALTER TABLE form_questions DROP COLUMN IF EXISTS conditional_role_logic;
-- ALTER TABLE form_questions DROP COLUMN IF EXISTS conditional_days_in_server;
-- ALTER TABLE form_questions DROP COLUMN IF EXISTS conditional_account_age_days;
-- ALTER TABLE form_questions DROP COLUMN IF EXISTS conditional_requires_boost;
-- ALTER TABLE form_questions DROP COLUMN IF EXISTS conditional_requires_nitro;
-- ALTER TABLE form_questions DROP COLUMN IF EXISTS conditional_permission_flags;
-- ALTER TABLE form_questions DROP COLUMN IF EXISTS required_when_parent_question_id;
-- ALTER TABLE form_questions DROP COLUMN IF EXISTS required_when_operator;
-- ALTER TABLE form_questions DROP COLUMN IF EXISTS required_when_value;
-- ALTER TABLE form_questions DROP COLUMN IF EXISTS enable_answer_piping;
