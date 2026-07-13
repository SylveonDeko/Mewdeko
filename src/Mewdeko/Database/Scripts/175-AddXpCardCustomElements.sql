ALTER TABLE "Template"
    ADD COLUMN IF NOT EXISTS "CustomElementsJson" text;

ALTER TABLE "Template"
    ADD COLUMN IF NOT EXISTS "BuiltInOrderJson" text;
