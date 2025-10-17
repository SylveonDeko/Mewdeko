-- Make NotificationRoles column nullable in GuildTicketSettings
ALTER TABLE "GuildTicketSettings"
    ALTER COLUMN "NotificationRoles" DROP NOT NULL;
