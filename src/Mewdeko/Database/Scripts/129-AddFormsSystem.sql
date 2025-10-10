-- Forms System Schema
-- This migration adds support for custom forms with conditional logic

-- Main forms table
CREATE TABLE IF NOT EXISTS forms
(
    id                         SERIAL PRIMARY KEY,
    guild_id                   BIGINT       NOT NULL,
    name                       VARCHAR(255) NOT NULL,
    description                TEXT,
    submit_channel_id          BIGINT,
    allow_multiple_submissions BOOLEAN   DEFAULT FALSE,
    max_responses              INT       DEFAULT NULL,
    require_captcha            BOOLEAN   DEFAULT FALSE,
    is_active                  BOOLEAN   DEFAULT TRUE,
    expires_at                 TIMESTAMP DEFAULT NULL,
    created_by                 BIGINT       NOT NULL,
    created_at                 TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at                 TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS idx_forms_guild_id ON forms (guild_id);
CREATE INDEX IF NOT EXISTS idx_forms_is_active ON forms (is_active);

-- Form questions table
CREATE TABLE IF NOT EXISTS form_questions
(
    id                             SERIAL PRIMARY KEY,
    form_id                        INT         NOT NULL REFERENCES forms (id) ON DELETE CASCADE,
    question_text                  TEXT        NOT NULL,
    question_type                  VARCHAR(50) NOT NULL, -- 'short_text', 'long_text', 'multiple_choice', 'checkboxes', 'dropdown', 'number', 'email', 'url'
    is_required                    BOOLEAN   DEFAULT FALSE,
    display_order                  INT         NOT NULL,
    placeholder                    TEXT,
    min_value                      INT,
    max_value                      INT,
    min_length                     INT,
    max_length                     INT,
    -- Conditional logic
    conditional_parent_question_id INT         REFERENCES form_questions (id) ON DELETE SET NULL,
    conditional_operator           VARCHAR(20),          -- 'equals', 'contains', 'not_equals', 'greater_than', 'less_than'
    conditional_expected_value     TEXT,
    created_at                     TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS idx_form_questions_form_id ON form_questions (form_id);
CREATE INDEX IF NOT EXISTS idx_form_questions_parent ON form_questions (conditional_parent_question_id);

-- Question options (for multiple choice, checkboxes, dropdown)
CREATE TABLE IF NOT EXISTS form_question_options
(
    id            SERIAL PRIMARY KEY,
    question_id   INT          NOT NULL REFERENCES form_questions (id) ON DELETE CASCADE,
    option_text   VARCHAR(500) NOT NULL,
    option_value  VARCHAR(500) NOT NULL,
    display_order INT          NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_form_question_options_question_id ON form_question_options (question_id);

-- Form responses (submissions)
CREATE TABLE IF NOT EXISTS form_responses
(
    id           SERIAL PRIMARY KEY,
    form_id      INT    NOT NULL REFERENCES forms (id) ON DELETE CASCADE,
    user_id      BIGINT NOT NULL,
    username     VARCHAR(255),
    submitted_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    ip_address   VARCHAR(45), -- Optional for spam prevention
    message_id   BIGINT       -- Discord message ID if logged to channel
);

CREATE INDEX IF NOT EXISTS idx_form_responses_form_id ON form_responses (form_id);
CREATE INDEX IF NOT EXISTS idx_form_responses_user_id ON form_responses (user_id);
CREATE INDEX IF NOT EXISTS idx_form_responses_submitted_at ON form_responses (submitted_at);

-- Individual answers
CREATE TABLE IF NOT EXISTS form_answers
(
    id            SERIAL PRIMARY KEY,
    response_id   INT NOT NULL REFERENCES form_responses (id) ON DELETE CASCADE,
    question_id   INT NOT NULL REFERENCES form_questions (id) ON DELETE CASCADE,
    answer_text   TEXT,
    answer_values TEXT[], -- For multi-select (checkboxes)
    created_at    TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS idx_form_answers_response_id ON form_answers (response_id);
CREATE INDEX IF NOT EXISTS idx_form_answers_question_id ON form_answers (question_id);

COMMENT ON TABLE forms IS 'Custom forms created by guilds';
COMMENT ON TABLE form_questions IS 'Questions belonging to forms with conditional logic support';
COMMENT ON TABLE form_question_options IS 'Options for multiple choice, checkbox, and dropdown questions';
COMMENT ON TABLE form_responses IS 'User submissions to forms';
COMMENT ON TABLE form_answers IS 'Individual answers to questions in a submission';
