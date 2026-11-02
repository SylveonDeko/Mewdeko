-- Ticket threads: a ticket button or select option with a thread channel opens its tickets as private threads in that
-- text channel, or as posts when it is a forum channel, instead of creating a channel for each ticket.
-- Migration: 227-AddTicketThreads.sql

ALTER TABLE "PanelButtons"
    ADD COLUMN IF NOT EXISTS "ThreadChannelId" NUMERIC(20, 0) NULL;

ALTER TABLE "SelectMenuOptions"
    ADD COLUMN IF NOT EXISTS "ThreadChannelId" NUMERIC(20, 0) NULL;
