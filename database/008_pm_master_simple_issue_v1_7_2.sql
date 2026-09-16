-- AU-Fleet-Ops v1.7.2 - PM Master + Simple Issue Workflow
ALTER TABLE "WorkTemplateFields" ADD COLUMN IF NOT EXISTS "SuggestedIssueCode" text NOT NULL DEFAULT '';
ALTER TABLE "WorkTemplateFieldInstances" ADD COLUMN IF NOT EXISTS "SuggestedIssueCode" text NOT NULL DEFAULT '';
ALTER TABLE "Defects" ADD COLUMN IF NOT EXISTS "ChecklistFieldInstanceId" uuid NULL;
ALTER TABLE "Defects" ADD COLUMN IF NOT EXISTS "CorrectiveWorkItemId" uuid NULL;
CREATE INDEX IF NOT EXISTS "IX_Defects_ChecklistFieldInstanceId" ON "Defects" ("ChecklistFieldInstanceId");
-- ISSUE_TYPE values are seeded idempotently by application startup.
