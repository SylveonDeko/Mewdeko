-- Repeating reminders and reminder subscribers. RepeatMinutes of 0 means the reminder fires once; Subscribers holds
-- comma separated user ids that get pinged along with the owner.
-- Migration: 244-AddRepeatingReminders.sql

ALTER TABLE "Reminders"
    ADD COLUMN IF NOT EXISTS "RepeatMinutes" INTEGER NOT NULL DEFAULT 0;

ALTER TABLE "Reminders"
    ADD COLUMN IF NOT EXISTS "Subscribers" TEXT NOT NULL DEFAULT '';
