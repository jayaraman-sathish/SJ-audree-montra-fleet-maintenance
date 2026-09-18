-- v1.8.3 Paper-on-Glass Execution
-- Idempotent PostgreSQL upgrade for work logs and evidence captured from tablets/mobile devices.

CREATE TABLE IF NOT EXISTS "WorkLogEntries" (
  "Id" uuid PRIMARY KEY,
  "JobCardId" uuid NOT NULL,
  "WorkItemId" uuid NULL,
  "ChecklistFieldInstanceId" uuid NULL,
  "EntryType" text NOT NULL DEFAULT 'Work Note',
  "Comment" text NOT NULL DEFAULT '',
  "CreatedBy" text NOT NULL DEFAULT 'Service User',
  "CreatedRole" text NOT NULL DEFAULT 'Technician',
  "CreatedAt" timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS "IX_WorkLogEntries_JobCardId_CreatedAt"
  ON "WorkLogEntries" ("JobCardId", "CreatedAt");

CREATE TABLE IF NOT EXISTS "WorkEvidence" (
  "Id" uuid PRIMARY KEY,
  "JobCardId" uuid NOT NULL,
  "WorkItemId" uuid NULL,
  "ChecklistFieldInstanceId" uuid NULL,
  "WorkLogEntryId" uuid NULL,
  "Stage" text NOT NULL DEFAULT 'General',
  "FileName" text NOT NULL DEFAULT '',
  "ContentType" text NOT NULL DEFAULT 'application/octet-stream',
  "FileSize" bigint NOT NULL DEFAULT 0,
  "Content" bytea NOT NULL,
  "Caption" text NOT NULL DEFAULT '',
  "UploadedBy" text NOT NULL DEFAULT 'Service User',
  "UploadedAt" timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS "IX_WorkEvidence_JobCardId_UploadedAt"
  ON "WorkEvidence" ("JobCardId", "UploadedAt");
