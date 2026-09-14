
        CREATE TABLE IF NOT EXISTS "MaintenanceRequests" (
          "Id" uuid PRIMARY KEY, "RequestNumber" text NOT NULL, "VehicleId" uuid NOT NULL, "SourceType" text NOT NULL DEFAULT 'Manual',
          "SourceReference" text NOT NULL DEFAULT '', "RequestType" text NOT NULL DEFAULT 'Repair', "Priority" text NOT NULL DEFAULT 'P3',
          "Description" text NOT NULL DEFAULT '', "Status" text NOT NULL DEFAULT 'Open', "RequestedBy" text NOT NULL DEFAULT 'Fleet User',
          "RequestedAt" timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP, "TargetDate" timestamptz NULL, "JobCardId" uuid NULL);
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_MaintenanceRequests_RequestNumber" ON "MaintenanceRequests" ("RequestNumber");
        CREATE INDEX IF NOT EXISTS "IX_MaintenanceRequests_Vehicle_Status" ON "MaintenanceRequests" ("VehicleId","Status");

        CREATE TABLE IF NOT EXISTS "ServiceTaskMasters" (
          "Id" uuid PRIMARY KEY, "TaskCode" text NOT NULL, "Name" text NOT NULL, "Category" text NOT NULL DEFAULT '', "Description" text NOT NULL DEFAULT '',
          "StandardHours" numeric(18,2) NOT NULL DEFAULT 0, "RequiredSkillCode" text NOT NULL DEFAULT '', "RequiresHvAuthorization" boolean NOT NULL DEFAULT false,
          "RequiresQc" boolean NOT NULL DEFAULT true, "ChecklistCode" text NOT NULL DEFAULT '', "IsActive" boolean NOT NULL DEFAULT true);
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_ServiceTaskMasters_TaskCode" ON "ServiceTaskMasters" ("TaskCode");

        CREATE TABLE IF NOT EXISTS "ServiceTaskStandardParts" (
          "Id" uuid PRIMARY KEY, "ServiceTaskMasterId" uuid NOT NULL, "PartMasterId" uuid NOT NULL, "Quantity" numeric(18,3) NOT NULL DEFAULT 0);
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_ServiceTaskStandardParts_Master_Part" ON "ServiceTaskStandardParts" ("ServiceTaskMasterId","PartMasterId");

        CREATE TABLE IF NOT EXISTS "WorkOrderCosts" (
          "Id" uuid PRIMARY KEY, "JobCardId" uuid NOT NULL, "WorkItemId" uuid NULL, "CostType" text NOT NULL DEFAULT 'Other',
          "Description" text NOT NULL DEFAULT '', "Amount" numeric(18,2) NOT NULL DEFAULT 0, "VendorReference" text NOT NULL DEFAULT '',
          "PostedBy" text NOT NULL DEFAULT 'Service User', "PostedAt" timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP);
        CREATE INDEX IF NOT EXISTS "IX_WorkOrderCosts_JobCard_PostedAt" ON "WorkOrderCosts" ("JobCardId","PostedAt");

        ALTER TABLE "PartMasters" ADD COLUMN IF NOT EXISTS "StandardCost" numeric(18,2) NOT NULL DEFAULT 0;
        ALTER TABLE "PartTransactions" ADD COLUMN IF NOT EXISTS "UnitCost" numeric(18,2) NOT NULL DEFAULT 0;
        ALTER TABLE "PartTransactions" ADD COLUMN IF NOT EXISTS "ExtendedCost" numeric(18,2) NOT NULL DEFAULT 0;
        ALTER TABLE "LabourEntries" ADD COLUMN IF NOT EXISTS "HourlyRate" numeric(18,2) NOT NULL DEFAULT 0;
        ALTER TABLE "LabourEntries" ADD COLUMN IF NOT EXISTS "CostAmount" numeric(18,2) NOT NULL DEFAULT 0;
    