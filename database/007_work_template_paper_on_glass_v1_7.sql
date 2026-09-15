-- AU-Fleet-Ops v1.7 - Work Template / Paper-on-Glass Engine
CREATE TABLE IF NOT EXISTS "WorkTemplates" (
 "Id" uuid PRIMARY KEY, "TemplateCode" text NOT NULL, "Name" text NOT NULL DEFAULT '',
 "Category" text NOT NULL DEFAULT 'Inspection', "Description" text NOT NULL DEFAULT '',
 "Version" integer NOT NULL DEFAULT 1, "StandardHours" numeric(18,2) NOT NULL DEFAULT 0,
 "RequiredSkillCode" text NOT NULL DEFAULT '', "RequiresHvAuthorization" boolean NOT NULL DEFAULT false,
 "RequiresQc" boolean NOT NULL DEFAULT true, "IsActive" boolean NOT NULL DEFAULT true);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_WorkTemplates_TemplateCode" ON "WorkTemplates" ("TemplateCode");

CREATE TABLE IF NOT EXISTS "WorkTemplateFields" (
 "Id" uuid PRIMARY KEY, "WorkTemplateId" uuid NOT NULL, "SectionName" text NOT NULL DEFAULT 'General',
 "Sequence" integer NOT NULL DEFAULT 0, "FieldCode" text NOT NULL, "Label" text NOT NULL DEFAULT '',
 "FieldType" text NOT NULL DEFAULT 'Text', "UnitCode" text NOT NULL DEFAULT '',
 "IsMandatory" boolean NOT NULL DEFAULT true, "MinValue" numeric(18,4) NULL, "MaxValue" numeric(18,4) NULL,
 "Options" text NOT NULL DEFAULT '', "FailureAction" text NOT NULL DEFAULT 'None');

CREATE TABLE IF NOT EXISTS "MaintenancePlanTemplates" (
 "Id" uuid PRIMARY KEY, "MaintenancePlanId" uuid NOT NULL, "WorkTemplateId" uuid NOT NULL,
 "Sequence" integer NOT NULL DEFAULT 0, "IsMandatory" boolean NOT NULL DEFAULT true);

CREATE TABLE IF NOT EXISTS "WorkTemplateInstances" (
 "Id" uuid PRIMARY KEY, "JobCardId" uuid NOT NULL, "WorkItemId" uuid NOT NULL, "WorkTemplateId" uuid NOT NULL,
 "TemplateCode" text NOT NULL DEFAULT '', "TemplateName" text NOT NULL DEFAULT '',
 "TemplateVersion" integer NOT NULL DEFAULT 1, "Status" text NOT NULL DEFAULT 'Not Started',
 "CreatedAt" timestamptz NOT NULL DEFAULT now(), "CompletedAt" timestamptz NULL);

CREATE TABLE IF NOT EXISTS "WorkTemplateFieldInstances" (
 "Id" uuid PRIMARY KEY, "WorkTemplateInstanceId" uuid NOT NULL, "SourceTemplateFieldId" uuid NULL,
 "SectionName" text NOT NULL DEFAULT 'General', "Sequence" integer NOT NULL DEFAULT 0,
 "FieldCode" text NOT NULL DEFAULT '', "Label" text NOT NULL DEFAULT '', "FieldType" text NOT NULL DEFAULT 'Text',
 "UnitCode" text NOT NULL DEFAULT '', "IsMandatory" boolean NOT NULL DEFAULT true,
 "MinValue" numeric(18,4) NULL, "MaxValue" numeric(18,4) NULL, "Options" text NOT NULL DEFAULT '',
 "FailureAction" text NOT NULL DEFAULT 'None', "Value" text NOT NULL DEFAULT '',
 "Result" text NOT NULL DEFAULT 'Pending', "Remarks" text NOT NULL DEFAULT '',
 "EvidenceReference" text NOT NULL DEFAULT '', "ExecutedAt" timestamptz NULL, "ExecutedBy" text NOT NULL DEFAULT '');
