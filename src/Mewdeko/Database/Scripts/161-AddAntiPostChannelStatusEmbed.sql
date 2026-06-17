ALTER TABLE "antipostchannelsettings"
    ADD COLUMN IF NOT EXISTS "statusmessageid" NUMERIC(20, 0) NULL,
    ADD COLUMN IF NOT EXISTS "statuschannelid" NUMERIC(20, 0) NULL;
