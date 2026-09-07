-- Migration: The role allowed to approve or reject a form's responses from the review buttons
-- posted alongside the submission in Discord.
-- Version: 206
-- Date: 2026-09-07

DO
$$
    BEGIN
        IF NOT EXISTS (SELECT 1
                       FROM information_schema.columns
                       WHERE table_name = 'forms'
                         AND column_name = 'reviewer_role_id') THEN
            ALTER TABLE forms
                ADD COLUMN reviewer_role_id NUMERIC(20, 0);
            COMMENT ON COLUMN forms.reviewer_role_id IS 'Role permitted to decide this form''s responses from Discord. Null falls back to Manage Server.';
        END IF;

        -- The identifier of the Discord message carrying the review buttons, so a decision made
        -- anywhere can go back and settle that message.
        IF NOT EXISTS (SELECT 1
                       FROM information_schema.columns
                       WHERE table_name = 'form_response_workflows'
                         AND column_name = 'review_message_id') THEN
            ALTER TABLE form_response_workflows
                ADD COLUMN review_message_id NUMERIC(20, 0);
        END IF;

        -- Per-form override for the review button emotes. Null falls back to the guild default.
        IF NOT EXISTS (SELECT 1
                       FROM information_schema.columns
                       WHERE table_name = 'forms'
                         AND column_name = 'approve_emote') THEN
            ALTER TABLE forms
                ADD COLUMN approve_emote TEXT;
        END IF;

        IF NOT EXISTS (SELECT 1
                       FROM information_schema.columns
                       WHERE table_name = 'forms'
                         AND column_name = 'reject_emote') THEN
            ALTER TABLE forms
                ADD COLUMN reject_emote TEXT;
        END IF;

        -- Guild-wide default for those emotes, so a server sets its own pair once rather than on
        -- every form it builds.
        IF NOT EXISTS (SELECT 1
                       FROM information_schema.columns
                       WHERE table_name = 'GuildConfigs'
                         AND column_name = 'FormApproveEmote') THEN
            ALTER TABLE "GuildConfigs"
                ADD COLUMN "FormApproveEmote" TEXT;
        END IF;

        IF NOT EXISTS (SELECT 1
                       FROM information_schema.columns
                       WHERE table_name = 'GuildConfigs'
                         AND column_name = 'FormRejectEmote') THEN
            ALTER TABLE "GuildConfigs"
                ADD COLUMN "FormRejectEmote" TEXT;
        END IF;
    END
$$;

-- Rollback script (commented):
-- ALTER TABLE forms DROP COLUMN IF EXISTS reviewer_role_id, DROP COLUMN IF EXISTS approve_emote,
--     DROP COLUMN IF EXISTS reject_emote;
-- ALTER TABLE form_response_workflows DROP COLUMN IF EXISTS review_message_id;
-- ALTER TABLE "GuildConfigs" DROP COLUMN IF EXISTS "FormApproveEmote", DROP COLUMN IF EXISTS "FormRejectEmote";
