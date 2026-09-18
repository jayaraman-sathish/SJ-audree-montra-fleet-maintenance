using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using MontraFleet.Api.Data;
using MontraFleet.Api.Models;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var rawConnection =
    Environment.GetEnvironmentVariable("DATABASE_URL")
    ?? builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Database connection is not configured.");

static string NormalizePostgresConnection(string raw)
{
    if (!raw.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
        && !raw.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        return raw;

    var uri = new Uri(raw);
    var userInfo = Uri.UnescapeDataString(uri.UserInfo).Split(':', 2);
    var cs = new NpgsqlConnectionStringBuilder
    {
        Host = uri.Host,
        Port = uri.IsDefaultPort ? 5432 : uri.Port,
        Username = userInfo[0],
        Password = userInfo.Length > 1 ? userInfo[1] : string.Empty,
        Database = uri.AbsolutePath.TrimStart('/'),
        SslMode = SslMode.Prefer
    };
    return cs.ConnectionString;
}

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(NormalizePostgresConnection(rawConnection)));

var app = builder.Build();
if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }
app.UseDefaultFiles();
app.UseStaticFiles();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();
    await db.Database.ExecuteSqlRawAsync("""
        CREATE TABLE IF NOT EXISTS "MasterOptions" (
          "Id" uuid PRIMARY KEY, "Category" text NOT NULL, "Code" text NOT NULL, "Name" text NOT NULL,
          "Value" text NOT NULL DEFAULT '', "Description" text NOT NULL DEFAULT '', "SortOrder" integer NOT NULL DEFAULT 0, "IsActive" boolean NOT NULL DEFAULT true);
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_MasterOptions_Category_Code" ON "MasterOptions" ("Category","Code");

        CREATE TABLE IF NOT EXISTS "VehicleModelMasters" (
          "Id" uuid PRIMARY KEY, "ModelCode" text NOT NULL, "Name" text NOT NULL, "ManufacturerCode" text NOT NULL DEFAULT '',
          "VehicleTypeCode" text NOT NULL DEFAULT '', "PowertrainCode" text NOT NULL DEFAULT '', "ImageUrl" text NOT NULL DEFAULT '',
          "GvwKg" numeric(18,2) NULL, "BatteryCapacityKwh" numeric(18,2) NULL, "IsActive" boolean NOT NULL DEFAULT true);
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_VehicleModelMasters_ModelCode" ON "VehicleModelMasters" ("ModelCode");

        CREATE TABLE IF NOT EXISTS "VehicleVariantMasters" (
          "Id" uuid PRIMARY KEY, "VehicleModelMasterId" uuid NOT NULL, "VariantCode" text NOT NULL, "Name" text NOT NULL,
          "ImageUrl" text NOT NULL DEFAULT '', "IsActive" boolean NOT NULL DEFAULT true);
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_VehicleVariantMasters_Model_Variant" ON "VehicleVariantMasters" ("VehicleModelMasterId","VariantCode");

        CREATE TABLE IF NOT EXISTS "MaintenancePrograms" (
          "Id" uuid PRIMARY KEY, "ProgramCode" text NOT NULL, "Name" text NOT NULL, "Description" text NOT NULL DEFAULT '',
          "VehicleModelMasterId" uuid NULL, "VehicleVariantMasterId" uuid NULL, "EffectiveFrom" timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
          "EffectiveTo" timestamptz NULL, "IsActive" boolean NOT NULL DEFAULT true);
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_MaintenancePrograms_ProgramCode" ON "MaintenancePrograms" ("ProgramCode");

        CREATE TABLE IF NOT EXISTS "MaintenancePlans" (
          "Id" uuid PRIMARY KEY, "MaintenanceProgramId" uuid NOT NULL, "PlanCode" text NOT NULL, "Name" text NOT NULL,
          "Description" text NOT NULL DEFAULT '', "RecurrenceBasis" text NOT NULL DEFAULT 'Completion', "Sequence" integer NOT NULL DEFAULT 0,
          "IsActive" boolean NOT NULL DEFAULT true);
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_MaintenancePlans_Program_Plan" ON "MaintenancePlans" ("MaintenanceProgramId","PlanCode");

        CREATE TABLE IF NOT EXISTS "MaintenancePlanTriggers" (
          "Id" uuid PRIMARY KEY, "MaintenancePlanId" uuid NOT NULL, "TriggerCode" text NOT NULL DEFAULT 'ODOMETER',
          "IntervalValue" numeric(18,2) NOT NULL DEFAULT 0, "InitialDueValue" numeric(18,2) NULL, "UnitCode" text NOT NULL DEFAULT '',
          "WarningValue" numeric(18,2) NOT NULL DEFAULT 0, "ToleranceValue" numeric(18,2) NOT NULL DEFAULT 0, "IsActive" boolean NOT NULL DEFAULT true);
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_MaintenancePlanTriggers_Plan_Trigger" ON "MaintenancePlanTriggers" ("MaintenancePlanId","TriggerCode");

        CREATE TABLE IF NOT EXISTS "MaintenancePlanTasks" (
          "Id" uuid PRIMARY KEY, "MaintenancePlanId" uuid NOT NULL, "ServiceTaskMasterId" uuid NOT NULL,
          "Sequence" integer NOT NULL DEFAULT 0, "IsMandatory" boolean NOT NULL DEFAULT true);
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_MaintenancePlanTasks_Plan_Task" ON "MaintenancePlanTasks" ("MaintenancePlanId","ServiceTaskMasterId");

        ALTER TABLE "Vehicles" ADD COLUMN IF NOT EXISTS "VehicleTypeCode" text NOT NULL DEFAULT '';
        ALTER TABLE "Vehicles" ADD COLUMN IF NOT EXISTS "ManufacturerCode" text NOT NULL DEFAULT '';
        ALTER TABLE "Vehicles" ADD COLUMN IF NOT EXISTS "ModelMasterId" uuid NULL;
        ALTER TABLE "Vehicles" ADD COLUMN IF NOT EXISTS "VariantMasterId" uuid NULL;
        ALTER TABLE "Vehicles" ADD COLUMN IF NOT EXISTS "ImageUrl" text NOT NULL DEFAULT '';
        ALTER TABLE "Vehicles" ADD COLUMN IF NOT EXISTS "MotorNumber" text NOT NULL DEFAULT '';
        ALTER TABLE "Vehicles" ADD COLUMN IF NOT EXISTS "PurchaseDate" timestamptz NULL;
        ALTER TABLE "Vehicles" ADD COLUMN IF NOT EXISTS "PurchaseCost" numeric(18,2) NULL;
        ALTER TABLE "Vehicles" ADD COLUMN IF NOT EXISTS "InvoiceNumber" text NOT NULL DEFAULT '';
        ALTER TABLE "Vehicles" ADD COLUMN IF NOT EXISTS "DealerName" text NOT NULL DEFAULT '';
        ALTER TABLE "Vehicles" ADD COLUMN IF NOT EXISTS "CommissioningDate" timestamptz NULL;
        ALTER TABLE "Vehicles" ADD COLUMN IF NOT EXISTS "RegistrationDate" timestamptz NULL;
        ALTER TABLE "Vehicles" ADD COLUMN IF NOT EXISTS "RegistrationExpiry" timestamptz NULL;
        ALTER TABLE "Vehicles" ADD COLUMN IF NOT EXISTS "InsuranceNumber" text NOT NULL DEFAULT '';
        ALTER TABLE "Vehicles" ADD COLUMN IF NOT EXISTS "InsuranceStartDate" timestamptz NULL;
        ALTER TABLE "Vehicles" ADD COLUMN IF NOT EXISTS "InsuranceExpiryDate" timestamptz NULL;
        ALTER TABLE "Vehicles" ADD COLUMN IF NOT EXISTS "WarrantyStartDate" timestamptz NULL;
        ALTER TABLE "Vehicles" ADD COLUMN IF NOT EXISTS "WarrantyExpiryDate" timestamptz NULL;
        ALTER TABLE "Vehicles" ADD COLUMN IF NOT EXISTS "BatteryWarrantyStartDate" timestamptz NULL;
        ALTER TABLE "Vehicles" ADD COLUMN IF NOT EXISTS "BatteryWarrantyExpiryDate" timestamptz NULL;
        ALTER TABLE "Vehicles" ADD COLUMN IF NOT EXISTS "DepotCode" text NOT NULL DEFAULT '';
        ALTER TABLE "Vehicles" ADD COLUMN IF NOT EXISTS "ServiceCentreCode" text NOT NULL DEFAULT '';
        ALTER TABLE "Vehicles" ADD COLUMN IF NOT EXISTS "CustomerCode" text NOT NULL DEFAULT '';
        ALTER TABLE "Vehicles" ADD COLUMN IF NOT EXISTS "OwnershipTypeCode" text NOT NULL DEFAULT '';
        ALTER TABLE "Vehicles" ADD COLUMN IF NOT EXISTS "MaintenanceProgramId" uuid NULL;
        ALTER TABLE "Vehicles" ADD COLUMN IF NOT EXISTS "Remarks" text NOT NULL DEFAULT '';
        ALTER TABLE "Vehicles" ADD COLUMN IF NOT EXISTS "EnergyKwh" numeric(18,2) NOT NULL DEFAULT 0;
        ALTER TABLE "Vehicles" ADD COLUMN IF NOT EXISTS "IsActive" boolean NOT NULL DEFAULT true;

        ALTER TABLE "PmObligations" ADD COLUMN IF NOT EXISTS "MaintenancePlanId" uuid NULL;
        ALTER TABLE "PmObligations" ADD COLUMN IF NOT EXISTS "DueOperatingHours" numeric(18,2) NULL;
        ALTER TABLE "PmObligations" ADD COLUMN IF NOT EXISTS "DueEnergyKwh" numeric(18,2) NULL;
        ALTER TABLE "PmObligations" ADD COLUMN IF NOT EXISTS "CompletedAt" timestamptz NULL;
        ALTER TABLE "ServiceEvents" ADD COLUMN IF NOT EXISTS "PmObligationId" uuid NULL;
    """);
    await db.Database.ExecuteSqlRawAsync("""
        CREATE TABLE IF NOT EXISTS "MaintenanceRequests" (
          "Id" uuid PRIMARY KEY, "RequestNumber" text NOT NULL, "VehicleId" uuid NOT NULL, "SourceType" text NOT NULL DEFAULT 'Manual',
          "SourceReference" text NOT NULL DEFAULT '', "RequestType" text NOT NULL DEFAULT 'Repair', "Priority" text NOT NULL DEFAULT 'P3',
          "Description" text NOT NULL DEFAULT '', "Status" text NOT NULL DEFAULT 'Open', "RequestedBy" text NOT NULL DEFAULT 'Fleet User',
          "RequestedAt" timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP, "TargetDate" timestamptz NULL, "JobCardId" uuid NULL);
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_MaintenanceRequests_RequestNumber" ON "MaintenanceRequests" ("RequestNumber");
        CREATE INDEX IF NOT EXISTS "IX_MaintenanceRequests_Vehicle_Status" ON "MaintenanceRequests" ("VehicleId","Status");
        ALTER TABLE "MaintenanceRequests" ADD COLUMN IF NOT EXISTS "ComplaintCategoryCode" text NOT NULL DEFAULT 'GENERAL';
        ALTER TABLE "MaintenanceRequests" ADD COLUMN IF NOT EXISTS "SymptomCode" text NOT NULL DEFAULT 'OTHER';
        ALTER TABLE "MaintenanceRequests" ADD COLUMN IF NOT EXISTS "DiagnosticTemplateCode" text NOT NULL DEFAULT 'DIAG-GENERAL';

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
        ALTER TABLE "Technicians" ADD COLUMN IF NOT EXISTS "HourlyRate" numeric(18,2) NOT NULL DEFAULT 0;
    """);

    await db.Database.ExecuteSqlRawAsync("""
        ALTER TABLE "Appointments" ADD COLUMN IF NOT EXISTS "AppointmentNumber" text NOT NULL DEFAULT '';
        ALTER TABLE "Appointments" ADD COLUMN IF NOT EXISTS "SourceType" text NOT NULL DEFAULT 'Manual';
        ALTER TABLE "Appointments" ADD COLUMN IF NOT EXISTS "SourceReference" text NOT NULL DEFAULT '';
        ALTER TABLE "Appointments" ADD COLUMN IF NOT EXISTS "TechnicianId" uuid NULL;
        ALTER TABLE "Appointments" ADD COLUMN IF NOT EXISTS "Technician" text NOT NULL DEFAULT '';
        ALTER TABLE "Appointments" ADD COLUMN IF NOT EXISTS "Priority" text NOT NULL DEFAULT 'P3';
        ALTER TABLE "Appointments" ADD COLUMN IF NOT EXISTS "Reason" text NOT NULL DEFAULT '';
        ALTER TABLE "Appointments" ADD COLUMN IF NOT EXISTS "CreatedBy" text NOT NULL DEFAULT 'Service User';
        ALTER TABLE "Appointments" ADD COLUMN IF NOT EXISTS "CreatedAt" timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP;

        ALTER TABLE "WorkItems" ADD COLUMN IF NOT EXISTS "TaskCode" text NOT NULL DEFAULT '';
        ALTER TABLE "WorkItems" ADD COLUMN IF NOT EXISTS "AssignedToTechnicianId" uuid NULL;
        ALTER TABLE "WorkItems" ADD COLUMN IF NOT EXISTS "AssignedTo" text NOT NULL DEFAULT '';
        ALTER TABLE "WorkItems" ADD COLUMN IF NOT EXISTS "PlannedStartAt" timestamp with time zone NULL;
        ALTER TABLE "WorkItems" ADD COLUMN IF NOT EXISTS "DueAt" timestamp with time zone NULL;
        ALTER TABLE "WorkItems" ADD COLUMN IF NOT EXISTS "Priority" text NOT NULL DEFAULT 'P3';
        ALTER TABLE "WorkItems" ADD COLUMN IF NOT EXISTS "DependencyTaskId" uuid NULL;
        ALTER TABLE "WorkItems" ADD COLUMN IF NOT EXISTS "EstimatedHours" numeric(18,2) NULL;
        ALTER TABLE "WorkItems" ADD COLUMN IF NOT EXISTS "ActualHours" numeric(18,2) NULL;
        ALTER TABLE "WorkItems" ADD COLUMN IF NOT EXISTS "EvidenceReference" text NOT NULL DEFAULT '';
        ALTER TABLE "WorkItems" ADD COLUMN IF NOT EXISTS "CompletionRemarks" text NOT NULL DEFAULT '';
        ALTER TABLE "WorkItems" ADD COLUMN IF NOT EXISTS "UpdatedAt" timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP;

        CREATE UNIQUE INDEX IF NOT EXISTS "IX_Appointments_AppointmentNumber" ON "Appointments" ("AppointmentNumber") WHERE "AppointmentNumber" <> '';
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_WorkItems_TaskCode" ON "WorkItems" ("TaskCode") WHERE "TaskCode" <> '';
        CREATE TABLE IF NOT EXISTS "PartMasters" (
            "Id" uuid PRIMARY KEY, "PartNumber" text NOT NULL, "Description" text NOT NULL DEFAULT '', "Category" text NOT NULL DEFAULT '',
            "UnitOfMeasure" text NOT NULL DEFAULT 'EA', "ManufacturerPartNumber" text NOT NULL DEFAULT '', "IsSerialized" boolean NOT NULL DEFAULT false,
            "IsWarrantyReturnable" boolean NOT NULL DEFAULT false, "ReorderLevel" numeric(18,3) NOT NULL DEFAULT 0, "ReorderQuantity" numeric(18,3) NOT NULL DEFAULT 0, "IsActive" boolean NOT NULL DEFAULT true
        );
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_PartMaster_PartNumber" ON "PartMasters" ("PartNumber");
        CREATE TABLE IF NOT EXISTS "InventoryLocations" (
            "Id" uuid PRIMARY KEY, "LocationCode" text NOT NULL, "Name" text NOT NULL DEFAULT '', "ServiceCentre" text NOT NULL DEFAULT '', "Bin" text NOT NULL DEFAULT '', "IsActive" boolean NOT NULL DEFAULT true
        );
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_InventoryLocation_Code" ON "InventoryLocations" ("LocationCode");
        CREATE TABLE IF NOT EXISTS "PartStocks" (
            "Id" uuid PRIMARY KEY, "PartMasterId" uuid NOT NULL, "InventoryLocationId" uuid NOT NULL, "OnHandQty" numeric(18,3) NOT NULL DEFAULT 0, "ReservedQty" numeric(18,3) NOT NULL DEFAULT 0, "UpdatedAt" timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP
        );
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_PartStock_PartLocation" ON "PartStocks" ("PartMasterId","InventoryLocationId");
        CREATE TABLE IF NOT EXISTS "PartRequests" (
            "Id" uuid PRIMARY KEY, "RequestNumber" text NOT NULL, "JobCardId" uuid NOT NULL, "WorkItemId" uuid NULL, "PartMasterId" uuid NOT NULL, "InventoryLocationId" uuid NOT NULL,
            "QuantityRequired" numeric(18,3) NOT NULL DEFAULT 0, "QuantityReserved" numeric(18,3) NOT NULL DEFAULT 0, "QuantityIssued" numeric(18,3) NOT NULL DEFAULT 0,
            "QuantityReturned" numeric(18,3) NOT NULL DEFAULT 0, "QuantityConsumed" numeric(18,3) NOT NULL DEFAULT 0, "Status" text NOT NULL DEFAULT 'Requested',
            "WarrantyCandidate" boolean NOT NULL DEFAULT false, "FailedPartDisposition" text NOT NULL DEFAULT '', "RequestedBy" text NOT NULL DEFAULT 'Technician',
            "RequestedAt" timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP, "UpdatedAt" timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP
        );
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_PartRequest_Number" ON "PartRequests" ("RequestNumber");
        ALTER TABLE "PartTransactions" ADD COLUMN IF NOT EXISTS "PartRequestId" uuid NULL;
        ALTER TABLE "PartTransactions" ADD COLUMN IF NOT EXISTS "PartMasterId" uuid NULL;
        ALTER TABLE "PartTransactions" ADD COLUMN IF NOT EXISTS "InventoryLocationId" uuid NULL;
        ALTER TABLE "PartTransactions" ADD COLUMN IF NOT EXISTS "PerformedBy" text NOT NULL DEFAULT 'Store User';

    """);



    await db.Database.ExecuteSqlRawAsync("""
        ALTER TABLE "VehicleVariantMasters" ADD COLUMN IF NOT EXISTS "GvwKg" numeric(18,2) NULL;
        ALTER TABLE "VehicleVariantMasters" ADD COLUMN IF NOT EXISTS "PayloadKg" numeric(18,2) NULL;
        ALTER TABLE "VehicleVariantMasters" ADD COLUMN IF NOT EXISTS "BatteryCapacityKwh" numeric(18,2) NULL;
        ALTER TABLE "VehicleVariantMasters" ADD COLUMN IF NOT EXISTS "MotorPowerKw" numeric(18,2) NULL;
        ALTER TABLE "VehicleVariantMasters" ADD COLUMN IF NOT EXISTS "WheelbaseMm" numeric(18,2) NULL;
        ALTER TABLE "VehicleVariantMasters" ADD COLUMN IF NOT EXISTS "Configuration" text NOT NULL DEFAULT '';
        ALTER TABLE "VehicleVariantMasters" ADD COLUMN IF NOT EXISTS "EffectiveFrom" timestamptz NULL;

        CREATE TABLE IF NOT EXISTS "ManufacturerMasters" (
          "Id" uuid PRIMARY KEY, "ManufacturerCode" text NOT NULL, "Name" text NOT NULL DEFAULT '',
          "Country" text NOT NULL DEFAULT 'India', "WebsiteUrl" text NOT NULL DEFAULT '',
          "ContactPhone" text NOT NULL DEFAULT '', "ContactEmail" text NOT NULL DEFAULT '',
          "IsActive" boolean NOT NULL DEFAULT true);
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_ManufacturerMasters_ManufacturerCode" ON "ManufacturerMasters" ("ManufacturerCode");

        CREATE TABLE IF NOT EXISTS "CustomerMasters" (
          "Id" uuid PRIMARY KEY, "CustomerCode" text NOT NULL, "Name" text NOT NULL DEFAULT '',
          "AddressLine1" text NOT NULL DEFAULT '', "AddressLine2" text NOT NULL DEFAULT '',
          "City" text NOT NULL DEFAULT '', "State" text NOT NULL DEFAULT '', "PostalCode" text NOT NULL DEFAULT '',
          "Country" text NOT NULL DEFAULT 'India', "Gstin" text NOT NULL DEFAULT '',
          "ContactPerson" text NOT NULL DEFAULT '', "Mobile" text NOT NULL DEFAULT '', "Email" text NOT NULL DEFAULT '',
          "IsActive" boolean NOT NULL DEFAULT true);
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_CustomerMasters_CustomerCode" ON "CustomerMasters" ("CustomerCode");

        CREATE TABLE IF NOT EXISTS "DepotMasters" (
          "Id" uuid PRIMARY KEY, "DepotCode" text NOT NULL, "Name" text NOT NULL DEFAULT '',
          "CustomerMasterId" uuid NULL, "AddressLine1" text NOT NULL DEFAULT '', "City" text NOT NULL DEFAULT '',
          "State" text NOT NULL DEFAULT '', "PostalCode" text NOT NULL DEFAULT '',
          "ContactPerson" text NOT NULL DEFAULT '', "Mobile" text NOT NULL DEFAULT '', "IsActive" boolean NOT NULL DEFAULT true);
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_DepotMasters_DepotCode" ON "DepotMasters" ("DepotCode");

        CREATE TABLE IF NOT EXISTS "ServiceCentreMasters" (
          "Id" uuid PRIMARY KEY, "CentreCode" text NOT NULL, "Name" text NOT NULL DEFAULT '',
          "CentreType" text NOT NULL DEFAULT 'Company', "AddressLine1" text NOT NULL DEFAULT '',
          "City" text NOT NULL DEFAULT '', "State" text NOT NULL DEFAULT '', "PostalCode" text NOT NULL DEFAULT '',
          "ContactPerson" text NOT NULL DEFAULT '', "Mobile" text NOT NULL DEFAULT '', "WorkingHours" text NOT NULL DEFAULT '',
          "BayCount" integer NOT NULL DEFAULT 0, "IsActive" boolean NOT NULL DEFAULT true);
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_ServiceCentreMasters_CentreCode" ON "ServiceCentreMasters" ("CentreCode");

        ALTER TABLE "ServiceCentreMasters" ADD COLUMN IF NOT EXISTS "Email" text NOT NULL DEFAULT '';
        CREATE TABLE IF NOT EXISTS "ServiceCentreModelSupports" (
          "Id" uuid PRIMARY KEY,
          "ServiceCentreMasterId" uuid NOT NULL,
          "VehicleModelMasterId" uuid NOT NULL);
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_ServiceCentreModelSupports_Centre_Model"
          ON "ServiceCentreModelSupports" ("ServiceCentreMasterId","VehicleModelMasterId");

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
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_WorkTemplateFields_Template_FieldCode"
          ON "WorkTemplateFields" ("WorkTemplateId","FieldCode");

        CREATE TABLE IF NOT EXISTS "MaintenancePlanTemplates" (
          "Id" uuid PRIMARY KEY, "MaintenancePlanId" uuid NOT NULL, "WorkTemplateId" uuid NOT NULL,
          "Sequence" integer NOT NULL DEFAULT 0, "IsMandatory" boolean NOT NULL DEFAULT true);
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_MaintenancePlanTemplates_Plan_Template"
          ON "MaintenancePlanTemplates" ("MaintenancePlanId","WorkTemplateId");

        CREATE TABLE IF NOT EXISTS "WorkTemplateInstances" (
          "Id" uuid PRIMARY KEY, "JobCardId" uuid NOT NULL, "WorkItemId" uuid NOT NULL, "WorkTemplateId" uuid NOT NULL,
          "TemplateCode" text NOT NULL DEFAULT '', "TemplateName" text NOT NULL DEFAULT '',
          "TemplateVersion" integer NOT NULL DEFAULT 1, "Status" text NOT NULL DEFAULT 'Not Started',
          "CreatedAt" timestamptz NOT NULL DEFAULT now(), "CompletedAt" timestamptz NULL);
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_WorkTemplateInstances_WorkItemId" ON "WorkTemplateInstances" ("WorkItemId");

        CREATE TABLE IF NOT EXISTS "WorkTemplateFieldInstances" (
          "Id" uuid PRIMARY KEY, "WorkTemplateInstanceId" uuid NOT NULL, "SourceTemplateFieldId" uuid NULL,
          "SectionName" text NOT NULL DEFAULT 'General', "Sequence" integer NOT NULL DEFAULT 0,
          "FieldCode" text NOT NULL DEFAULT '', "Label" text NOT NULL DEFAULT '', "FieldType" text NOT NULL DEFAULT 'Text',
          "UnitCode" text NOT NULL DEFAULT '', "IsMandatory" boolean NOT NULL DEFAULT true,
          "MinValue" numeric(18,4) NULL, "MaxValue" numeric(18,4) NULL, "Options" text NOT NULL DEFAULT '',
          "FailureAction" text NOT NULL DEFAULT 'None', "Value" text NOT NULL DEFAULT '',
          "Result" text NOT NULL DEFAULT 'Pending', "Remarks" text NOT NULL DEFAULT '',
          "EvidenceReference" text NOT NULL DEFAULT '', "ExecutedAt" timestamptz NULL,
          "ExecutedBy" text NOT NULL DEFAULT '');
        ALTER TABLE "WorkTemplateFields" ADD COLUMN IF NOT EXISTS "SuggestedIssueCode" text NOT NULL DEFAULT '';
        ALTER TABLE "WorkTemplateFieldInstances" ADD COLUMN IF NOT EXISTS "SuggestedIssueCode" text NOT NULL DEFAULT '';
        ALTER TABLE "Defects" ADD COLUMN IF NOT EXISTS "ChecklistFieldInstanceId" uuid NULL;
        ALTER TABLE "Defects" ADD COLUMN IF NOT EXISTS "CorrectiveWorkItemId" uuid NULL;
        CREATE INDEX IF NOT EXISTS "IX_Defects_ChecklistFieldInstanceId" ON "Defects" ("ChecklistFieldInstanceId");
        ALTER TABLE "WorkTemplateFieldInstances" ADD COLUMN IF NOT EXISTS "ActionCode" text NOT NULL DEFAULT '';
        ALTER TABLE "WorkTemplateFieldInstances" ADD COLUMN IF NOT EXISTS "Specification" text NOT NULL DEFAULT '';
        ALTER TABLE "WorkTemplateFieldInstances" ADD COLUMN IF NOT EXISTS "Severity" text NOT NULL DEFAULT '';

        CREATE TABLE IF NOT EXISTS "MaintenanceTaskDefinitions" (
          "Id" uuid PRIMARY KEY, "SectionName" text NOT NULL DEFAULT '', "TaskCode" text NOT NULL,
          "TaskName" text NOT NULL DEFAULT '', "ActionCode" text NOT NULL DEFAULT 'I', "Specification" text NOT NULL DEFAULT '',
          "Severity" text NOT NULL DEFAULT '', "UnitCode" text NOT NULL DEFAULT '', "SuggestedIssueCode" text NOT NULL DEFAULT '',
          "SortOrder" integer NOT NULL DEFAULT 0, "IsActive" boolean NOT NULL DEFAULT true);
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_MaintenanceTaskDefinitions_TaskCode" ON "MaintenanceTaskDefinitions" ("TaskCode");

        CREATE TABLE IF NOT EXISTS "MaintenancePlanMatrixItems" (
          "Id" uuid PRIMARY KEY, "MaintenancePlanId" uuid NOT NULL, "MaintenanceTaskDefinitionId" uuid NOT NULL,
          "Sequence" integer NOT NULL DEFAULT 0, "IsMandatory" boolean NOT NULL DEFAULT true);
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_MaintenancePlanMatrixItems_Plan_Task" ON "MaintenancePlanMatrixItems" ("MaintenancePlanId","MaintenanceTaskDefinitionId");
        ALTER TABLE "MaintenanceTaskDefinitions" ADD COLUMN IF NOT EXISTS "MaintenanceProgramId" uuid NULL;
        DROP INDEX IF EXISTS "IX_MaintenanceTaskDefinitions_TaskCode";
        CREATE INDEX IF NOT EXISTS "IX_MaintenanceTaskDefinitions_TaskCode" ON "MaintenanceTaskDefinitions" ("TaskCode");
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_MaintenanceTaskDefinitions_Program_Code_Name" ON "MaintenanceTaskDefinitions" ("MaintenanceProgramId","TaskCode","TaskName");
        ALTER TABLE "MaintenancePlanMatrixItems" ADD COLUMN IF NOT EXISTS "ActionCode" text NOT NULL DEFAULT '';

        CREATE TABLE IF NOT EXISTS "MaintenanceReplacementRules" (
          "Id" uuid PRIMARY KEY, "Platform" text NOT NULL DEFAULT '', "SystemName" text NOT NULL DEFAULT '',
          "ItemName" text NOT NULL DEFAULT '', "PartNumber" text NOT NULL DEFAULT '', "ActionCode" text NOT NULL DEFAULT 'R',
          "UsageInterval" numeric NULL, "UsageUnit" text NOT NULL DEFAULT '', "IntervalMonths" integer NULL,
          "Quantity" numeric NULL, "Notes" text NOT NULL DEFAULT '', "IsActive" boolean NOT NULL DEFAULT true);
    """);

    await db.Database.ExecuteSqlRawAsync("""
        CREATE TABLE IF NOT EXISTS "WorkLogEntries" (
          "Id" uuid PRIMARY KEY, "JobCardId" uuid NOT NULL, "WorkItemId" uuid NULL,
          "ChecklistFieldInstanceId" uuid NULL, "EntryType" text NOT NULL DEFAULT 'Work Note',
          "Comment" text NOT NULL DEFAULT '', "CreatedBy" text NOT NULL DEFAULT 'Service User',
          "CreatedRole" text NOT NULL DEFAULT 'Technician', "CreatedAt" timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP);
        CREATE INDEX IF NOT EXISTS "IX_WorkLogEntries_JobCardId_CreatedAt" ON "WorkLogEntries" ("JobCardId","CreatedAt");

        CREATE TABLE IF NOT EXISTS "WorkEvidence" (
          "Id" uuid PRIMARY KEY, "JobCardId" uuid NOT NULL, "WorkItemId" uuid NULL,
          "ChecklistFieldInstanceId" uuid NULL, "WorkLogEntryId" uuid NULL,
          "Stage" text NOT NULL DEFAULT 'General', "FileName" text NOT NULL DEFAULT '',
          "ContentType" text NOT NULL DEFAULT 'application/octet-stream', "FileSize" bigint NOT NULL DEFAULT 0,
          "Content" bytea NOT NULL, "Caption" text NOT NULL DEFAULT '',
          "UploadedBy" text NOT NULL DEFAULT 'Service User', "UploadedAt" timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP);
        CREATE INDEX IF NOT EXISTS "IX_WorkEvidence_JobCardId_UploadedAt" ON "WorkEvidence" ("JobCardId","UploadedAt");
    """);




    // Reference master data from Montra Electric public product/contact pages. Idempotent by code.
    if(!await db.ManufacturerMasters.AnyAsync(x=>x.ManufacturerCode=="MONTRA"))
    {
        db.ManufacturerMasters.Add(new ManufacturerMaster{
            ManufacturerCode="MONTRA",Name="Montra Electric",Country="India",
            WebsiteUrl="https://www.montraelectric.com/",ContactPhone="1800-833-3303",ContactEmail="customercare@montraelectric.com",IsActive=true});
        await db.SaveChangesAsync();
    }

    var seedOptions = new [] {
        new MasterOption{Category="VEHICLE_TYPE",Code="E3W",Name="Three Wheeler",Description="Super Auto / Super Cargo",SortOrder=10},
        new MasterOption{Category="VEHICLE_TYPE",Code="ESCV",Name="Small Commercial Vehicle",Description="EVIATOR electric small commercial vehicle",SortOrder=20},
        new MasterOption{Category="VEHICLE_TYPE",Code="EMHCV",Name="Medium & Heavy Commercial Vehicle",Description="Rhino / Tipper electric heavy commercial vehicles",SortOrder=30},
        new MasterOption{Category="VEHICLE_TYPE",Code="ETRACTOR",Name="Electric Tractor",Description="Montra electric tractor range",SortOrder=40},
        new MasterOption{Category="POWERTRAIN",Code="EV",Name="Battery Electric",Description="Battery electric vehicle",SortOrder=10},
        new MasterOption{Category="OWNERSHIP_TYPE",Code="OWNED",Name="Owned",SortOrder=10},
        new MasterOption{Category="OWNERSHIP_TYPE",Code="LEASED",Name="Leased",SortOrder=20},
        new MasterOption{Category="PM_TRIGGER_TYPE",Code="ODOMETER",Name="Odometer",Value="KM",Description="Distance-based preventive maintenance trigger",SortOrder=10},
        new MasterOption{Category="PM_TRIGGER_TYPE",Code="TIME",Name="Calendar / Time",Value="MONTH",Description="Calendar-based preventive maintenance trigger",SortOrder=20},
        new MasterOption{Category="PM_TRIGGER_TYPE",Code="OPERATING_HOURS",Name="Operating Hours",Value="HOUR",Description="Operating-hour preventive maintenance trigger",SortOrder=30},
        new MasterOption{Category="PM_TRIGGER_TYPE",Code="KWH",Name="Energy Used",Value="KWH",Description="Accumulated energy-used preventive maintenance trigger",SortOrder=40},
        new MasterOption{Category="UNIT_OF_MEASURE",Code="KM",Name="Kilometre",Value="km",Description="Distance",SortOrder=10},
        new MasterOption{Category="UNIT_OF_MEASURE",Code="HOUR",Name="Hour",Value="hr",Description="Operating time",SortOrder=20},
        new MasterOption{Category="UNIT_OF_MEASURE",Code="KWH",Name="Kilowatt-hour",Value="kWh",Description="Energy",SortOrder=30},
        new MasterOption{Category="UNIT_OF_MEASURE",Code="DAY",Name="Day",Value="day",Description="Calendar time",SortOrder=40},
        new MasterOption{Category="UNIT_OF_MEASURE",Code="MONTH",Name="Month",Value="month",Description="Calendar time",SortOrder=50},
        new MasterOption{Category="UNIT_OF_MEASURE",Code="YEAR",Name="Year",Value="year",Description="Calendar time",SortOrder=60},
        new MasterOption{Category="UNIT_OF_MEASURE",Code="EA",Name="Each",Value="EA",Description="Parts quantity",SortOrder=70},
        new MasterOption{Category="PRIORITY",Code="P1",Name="Critical",Value="1",Description="Immediate / vehicle-off-road priority",SortOrder=10},
        new MasterOption{Category="PRIORITY",Code="P2",Name="High",Value="2",Description="High operational impact",SortOrder=20},
        new MasterOption{Category="PRIORITY",Code="P3",Name="Normal",Value="3",Description="Normal workshop priority",SortOrder=30},
        new MasterOption{Category="PRIORITY",Code="P4",Name="Low",Value="4",Description="Low urgency / planned work",SortOrder=40},
        new MasterOption{Category="VEHICLE_STATUS",Code="AVAILABLE",Name="Available",Value="Available",Description="Vehicle available for operation",SortOrder=10},
        new MasterOption{Category="VEHICLE_STATUS",Code="UNDER_MAINTENANCE",Name="Under Maintenance",Value="Downtime",Description="Vehicle currently under maintenance",SortOrder=20},
        new MasterOption{Category="VEHICLE_STATUS",Code="OFF_HIRE",Name="Off-Hire",Value="Downtime",Description="Vehicle unavailable / off-hire",SortOrder=30},
        new MasterOption{Category="VEHICLE_STATUS",Code="INACTIVE",Name="Inactive",Value="Inactive",Description="Vehicle not in active fleet",SortOrder=40},
        new MasterOption{Category="TASK_CATEGORY",Code="INSPECTION",Name="Inspection",Description="Inspection / condition check",SortOrder=10},
        new MasterOption{Category="TASK_CATEGORY",Code="PM_SERVICE",Name="Preventive Service",Description="Scheduled preventive maintenance task",SortOrder=20},
        new MasterOption{Category="TASK_CATEGORY",Code="REPAIR",Name="Repair",Description="Corrective repair task",SortOrder=30},
        new MasterOption{Category="TASK_CATEGORY",Code="DIAGNOSTIC",Name="Diagnostic",Description="Diagnostic / scan task",SortOrder=40},
        new MasterOption{Category="TASK_CATEGORY",Code="ROAD_TEST",Name="Road Test",Description="Road test / functional verification",SortOrder=50},
        new MasterOption{Category="SKILL",Code="EV",Name="EV General",Description="General electric-vehicle service skill",SortOrder=10},
        new MasterOption{Category="SKILL",Code="HV",Name="High Voltage",Description="High-voltage authorized skill",SortOrder=20},
        new MasterOption{Category="SKILL",Code="MECH",Name="Mechanical",Description="Mechanical service skill",SortOrder=30},
        new MasterOption{Category="SKILL",Code="ELEC",Name="Electrical",Description="Electrical service skill",SortOrder=40},
        new MasterOption{Category="SKILL",Code="DIAG",Name="Diagnostics",Description="Vehicle diagnostic skill",SortOrder=50},
        new MasterOption{Category="DOCUMENT_TYPE",Code="REGISTRATION",Name="Registration Certificate",Value="Vehicle",Description="Vehicle registration document",SortOrder=10},
        new MasterOption{Category="DOCUMENT_TYPE",Code="INSURANCE",Name="Insurance",Value="Vehicle",Description="Vehicle insurance document",SortOrder=20},
        new MasterOption{Category="DOCUMENT_TYPE",Code="WARRANTY",Name="Warranty",Value="Vehicle",Description="Vehicle / battery warranty document",SortOrder=30},
        new MasterOption{Category="DOCUMENT_TYPE",Code="PURCHASE_INVOICE",Name="Purchase Invoice",Value="Vehicle",Description="Vehicle purchase invoice",SortOrder=40},
        new MasterOption{Category="DOCUMENT_TYPE",Code="SERVICE_REPORT",Name="Service Report",Value="Work Order",Description="Service completion report",SortOrder=50},
        new MasterOption{Category="DOCUMENT_TYPE",Code="QC_REPORT",Name="QC / Release Report",Value="Work Order",Description="QC and release evidence",SortOrder=60},
        new MasterOption{Category="ISSUE_TYPE",Code="COOL-HOSE",Name="Coolant hose leakage",Value="Major",Description="Replace coolant hose",SortOrder=10},
        new MasterOption{Category="ISSUE_TYPE",Code="COOL-LOW",Name="Coolant level low",Value="Minor",Description="Top up coolant and inspect for leakage",SortOrder=20},
        new MasterOption{Category="ISSUE_TYPE",Code="BRK-PAD",Name="Brake pad worn",Value="Major",Description="Replace brake pad",SortOrder=30},
        new MasterOption{Category="ISSUE_TYPE",Code="BRK-HOSE",Name="Brake hose damaged or leaking",Value="Major",Description="Replace brake hose",SortOrder=40},
        new MasterOption{Category="ISSUE_TYPE",Code="TYRE-DMG",Name="Tyre damaged",Value="Major",Description="Replace tyre",SortOrder=50},
        new MasterOption{Category="ISSUE_TYPE",Code="HV-ISO",Name="HV isolation failed",Value="Critical",Description="Stop work and perform HV diagnosis",SortOrder=60},
        new MasterOption{Category="ISSUE_TYPE",Code="BATT-TEMP",Name="Battery abnormal temperature",Value="Major",Description="Perform battery diagnostic",SortOrder=70},
        new MasterOption{Category="ISSUE_TYPE",Code="CONN-LOOSE",Name="Electrical connector loose",Value="Minor",Description="Secure or repair connector",SortOrder=80},
        new MasterOption{Category="ISSUE_TYPE",Code="OTHER",Name="Other issue",Value="Minor",Description="Assess and define corrective work",SortOrder=90},
        new MasterOption{Category="COMPLAINT_CATEGORY",Code="FLUID_LEAK",Name="Oil / Fluid Leakage",Value="DIAG-LEAK",Description="Oil, coolant, hydraulic or other fluid leakage",SortOrder=10},
        new MasterOption{Category="COMPLAINT_CATEGORY",Code="BRAKE",Name="Brake Complaint",Value="DIAG-BRAKE",Description="Braking performance, noise, warning or leakage",SortOrder=20},
        new MasterOption{Category="COMPLAINT_CATEGORY",Code="CHARGING",Name="Charging Issue",Value="DIAG-CHARGING",Description="Charging failure, slow charging or connector issue",SortOrder=30},
        new MasterOption{Category="COMPLAINT_CATEGORY",Code="NO_START",Name="Vehicle Not Starting",Value="DIAG-NOSTART",Description="Vehicle will not power on or move",SortOrder=40},
        new MasterOption{Category="COMPLAINT_CATEGORY",Code="BATTERY_HV",Name="Battery / HV Warning",Value="DIAG-HV",Description="HV battery warning, isolation or thermal concern",SortOrder=50},
        new MasterOption{Category="COMPLAINT_CATEGORY",Code="STEERING",Name="Steering Complaint",Value="DIAG-STEER",Description="Steering effort, play, pull or vibration",SortOrder=60},
        new MasterOption{Category="COMPLAINT_CATEGORY",Code="NOISE_VIB",Name="Abnormal Noise / Vibration",Value="DIAG-NOISE",Description="Noise or vibration requiring diagnosis",SortOrder=70},
        new MasterOption{Category="COMPLAINT_CATEGORY",Code="TYRE_WHEEL",Name="Tyre / Wheel Complaint",Value="DIAG-TYRE",Description="Tyre pressure, damage, wear, wheel concern",SortOrder=80},
        new MasterOption{Category="COMPLAINT_CATEGORY",Code="GENERAL",Name="Other / General",Value="DIAG-GENERAL",Description="General diagnosis when no specific template applies",SortOrder=90},
        new MasterOption{Category="SYMPTOM",Code="LEAKAGE",Name="Leakage",Value="Leakage",Description="Visible or suspected fluid leakage",SortOrder=10},
        new MasterOption{Category="SYMPTOM",Code="NOISE",Name="Noise",Value="Noise",Description="Abnormal sound",SortOrder=20},
        new MasterOption{Category="SYMPTOM",Code="WARNING",Name="Warning Lamp / DTC",Value="Warning",Description="Warning lamp, fault or DTC",SortOrder=30},
        new MasterOption{Category="SYMPTOM",Code="LOW_PERFORMANCE",Name="Low Performance",Value="Performance",Description="Reduced performance or efficiency",SortOrder=40},
        new MasterOption{Category="SYMPTOM",Code="NO_START",Name="Not Starting",Value="No start",Description="Vehicle does not power on/start",SortOrder=50},
        new MasterOption{Category="SYMPTOM",Code="OVERHEAT",Name="Overheating",Value="Overheating",Description="Abnormal thermal condition",SortOrder=60},
        new MasterOption{Category="SYMPTOM",Code="VIBRATION",Name="Vibration",Value="Vibration",Description="Abnormal vibration",SortOrder=70},
        new MasterOption{Category="SYMPTOM",Code="OTHER",Name="Other",Value="Other",Description="Other symptom",SortOrder=90}
    };
    foreach(var o in seedOptions)
        if(!await db.MasterOptions.AnyAsync(x=>x.Category==o.Category&&x.Code==o.Code)) db.MasterOptions.Add(o);
    await db.SaveChangesAsync();

    async Task<WorkTemplate> EnsureDiagnosticTemplate(string code,string name,string description,params (string code,string label,string type,string options,string failAction)[] fields)
    {
        var wt=await db.WorkTemplates.FirstOrDefaultAsync(x=>x.TemplateCode==code);
        if(wt is null)
        {
            wt=new WorkTemplate{TemplateCode=code,Name=name,Category="Diagnostic",Description=description,Version=1,StandardHours=0.5m,RequiredSkillCode="DIAG",RequiresQc=true,IsActive=true};
            db.WorkTemplates.Add(wt);await db.SaveChangesAsync();
        }
        var seq=0;
        foreach(var item in fields)
        {
            seq+=10;
            if(!await db.WorkTemplateFields.AnyAsync(x=>x.WorkTemplateId==wt.Id&&x.FieldCode==item.code))
                db.WorkTemplateFields.Add(new WorkTemplateField{WorkTemplateId=wt.Id,SectionName=name,Sequence=seq,FieldCode=item.code,Label=item.label,FieldType=item.type,Options=item.options,IsMandatory=true,FailureAction=item.failAction});
        }
        await db.SaveChangesAsync();
        return wt;
    }

    await EnsureDiagnosticTemplate("DIAG-GENERAL","General Diagnosis","Fallback diagnostic checklist for any unplanned repair or breakdown",
        ("GD-01","Confirm customer / driver complaint","OK/Not OK","OK;Not OK","None"),
        ("GD-02","Record warning lamps / DTC / dashboard indication","Text","","None"),
        ("GD-03","Visual vehicle condition and safety check","OK/Not OK","OK;Not OK","Create Defect"),
        ("GD-04","Inspect for visible fluid leakage","OK/Not OK","OK;Not OK","Create Defect"),
        ("GD-05","Diagnosis / finding","Text","","None"),
        ("GD-06","Recommended corrective action","Text","","None"),
        ("GD-07","Parts required?","Yes/No","Yes;No","None"),
        ("GD-08","Photo / evidence reference","Photo","","None"));

    await EnsureDiagnosticTemplate("DIAG-LEAK","Oil / Fluid Leak Diagnosis","Structured leak diagnosis for oil, coolant, hydraulic and other fluid complaints",
        ("LK-01","Confirm leakage complaint","OK/Not OK","OK;Not OK","None"),
        ("LK-02","Identify fluid / leak source","Text","","None"),
        ("LK-03","Check fluid level / loss severity","Text","","None"),
        ("LK-04","Inspect hose / pipe / seal / gasket condition","OK/Not OK","OK;Not OK","Create Defect"),
        ("LK-05","Inspect joints, clamps and connections","OK/Not OK","OK;Not OK","Create Defect"),
        ("LK-06","Check contamination / spread area","Text","","None"),
        ("LK-07","Recommended repair","Text","","None"),
        ("LK-08","Photo / evidence reference","Photo","","None"));

    await EnsureDiagnosticTemplate("DIAG-BRAKE","Brake Diagnosis","Brake complaint diagnostic checklist",
        ("BRD-01","Confirm brake complaint","OK/Not OK","OK;Not OK","None"),
        ("BRD-02","Check warning lamp / DTC","Text","","None"),
        ("BRD-03","Inspect pads / shoes / discs / drums","OK/Not OK","OK;Not OK","Create Defect"),
        ("BRD-04","Inspect brake lines / hoses for leakage or damage","OK/Not OK","OK;Not OK","Create Defect"),
        ("BRD-05","Check pedal / parking brake operation","OK/Not OK","OK;Not OK","Create Defect"),
        ("BRD-06","Functional brake test finding","Text","","None"),
        ("BRD-07","Recommended repair","Text","","None"));

    await EnsureDiagnosticTemplate("DIAG-CHARGING","Charging System Diagnosis","Charging system complaint diagnostic checklist",
        ("CHD-01","Confirm charging complaint","OK/Not OK","OK;Not OK","None"),
        ("CHD-02","Inspect charge inlet and connector","OK/Not OK","OK;Not OK","Create Defect"),
        ("CHD-03","Check connector pins / locking","OK/Not OK","OK;Not OK","Create Defect"),
        ("CHD-04","Record charger / vehicle DTC","Text","","None"),
        ("CHD-05","Charging communication result","Text","","None"),
        ("CHD-06","Charging functional test","Pass/Fail","Pass;Fail","Create Defect"),
        ("CHD-07","Check overheating / burning signs","OK/Not OK","OK;Not OK","Block Completion"));

    await EnsureDiagnosticTemplate("DIAG-NOSTART","No-Start Diagnosis","Vehicle does not power on, start or move",
        ("NS-01","Confirm no-start condition","OK/Not OK","OK;Not OK","None"),
        ("NS-02","Check 12 V supply / terminals","OK/Not OK","OK;Not OK","Create Defect"),
        ("NS-03","Check HV ready indication / interlock","OK/Not OK","OK;Not OK","Create Defect"),
        ("NS-04","Record warning lamp / DTC","Text","","None"),
        ("NS-05","Check key / start enable / safety interlocks","OK/Not OK","OK;Not OK","Create Defect"),
        ("NS-06","Diagnosis / recommended action","Text","","None"));

    await EnsureDiagnosticTemplate("DIAG-HV","Battery / HV Diagnosis","HV battery, isolation and thermal diagnostic checklist",
        ("HV-01","Record battery warning / DTC","Text","","None"),
        ("HV-02","Check SOC / battery status","Text","","None"),
        ("HV-03","Check battery temperature / thermal warning","Text","","None"),
        ("HV-04","Inspect HV cables / connectors","OK/Not OK","OK;Not OK","Create Defect"),
        ("HV-05","Check isolation / insulation status","Pass/Fail","Pass;Fail","Block Completion"),
        ("HV-06","Check coolant leakage / cooling condition","OK/Not OK","OK;Not OK","Create Defect"),
        ("HV-07","Diagnosis / recommended action","Text","","None"));

    await EnsureDiagnosticTemplate("DIAG-STEER","Steering Diagnosis","Steering effort, play, pull and vibration diagnosis",
        ("ST-01","Confirm steering complaint","OK/Not OK","OK;Not OK","None"),
        ("ST-02","Check steering free play / operation","OK/Not OK","OK;Not OK","Create Defect"),
        ("ST-03","Inspect linkage / joints / mounting","OK/Not OK","OK;Not OK","Create Defect"),
        ("ST-04","Inspect suspension components","OK/Not OK","OK;Not OK","Create Defect"),
        ("ST-05","Check tyre pressure / uneven wear","OK/Not OK","OK;Not OK","Create Defect"),
        ("ST-06","Road test / diagnosis finding","Text","","None"));

    await EnsureDiagnosticTemplate("DIAG-NOISE","Noise / Vibration Diagnosis","Abnormal noise or vibration diagnostic checklist",
        ("NV-01","Confirm complaint and operating condition","Text","","None"),
        ("NV-02","Identify source area","Text","","None"),
        ("NV-03","Inspect mounting / fasteners","OK/Not OK","OK;Not OK","Create Defect"),
        ("NV-04","Inspect driveline / bearings / rotating parts","OK/Not OK","OK;Not OK","Create Defect"),
        ("NV-05","Road / functional test finding","Text","","None"),
        ("NV-06","Recommended action","Text","","None"));

    await EnsureDiagnosticTemplate("DIAG-TYRE","Tyre / Wheel Diagnosis","Tyre, wheel, pressure, wear and alignment diagnosis",
        ("TYD-01","Confirm tyre / wheel complaint","OK/Not OK","OK;Not OK","None"),
        ("TYD-02","Check tyre pressure","Text","","None"),
        ("TYD-03","Inspect tread / damage / sidewall","OK/Not OK","OK;Not OK","Create Defect"),
        ("TYD-04","Inspect wheel / fasteners","OK/Not OK","OK;Not OK","Create Defect"),
        ("TYD-05","Check abnormal / uneven wear","OK/Not OK","OK;Not OK","Create Defect"),
        ("TYD-06","Alignment / vibration finding","Text","","None"));

    var montraTasks=new[]{
        new MaintenanceTaskDefinition{SectionName="Air / Brake System",TaskCode="BRK-01",TaskName="Brake lining / pad thickness",ActionCode="M",Specification="5–30 mm",Severity="Critical",UnitCode="mm",SuggestedIssueCode="BRK-PAD",SortOrder=10},
        new MaintenanceTaskDefinition{SectionName="Air / Brake System",TaskCode="BRK-02",TaskName="Brake chamber",ActionCode="I",Specification="",Severity="Critical",UnitCode="",SuggestedIssueCode="",SortOrder=20},
        new MaintenanceTaskDefinition{SectionName="Air / Brake System",TaskCode="BRK-03",TaskName="Air pressure build-up",ActionCode="M",Specification="7–12 bar",Severity="Critical",UnitCode="bar",SuggestedIssueCode="",SortOrder=30},
        new MaintenanceTaskDefinition{SectionName="Air / Brake System",TaskCode="BRK-04",TaskName="Air leakage",ActionCode="I",Specification="",Severity="Critical",UnitCode="",SuggestedIssueCode="",SortOrder=40},
        new MaintenanceTaskDefinition{SectionName="Air / Brake System",TaskCode="BRK-05",TaskName="Hoses and valves",ActionCode="I",Specification="",Severity="Critical",UnitCode="",SuggestedIssueCode="",SortOrder=50},
        new MaintenanceTaskDefinition{SectionName="Air / Brake System",TaskCode="BRK-06",TaskName="Parking brake",ActionCode="I",Specification="",Severity="Critical",UnitCode="",SuggestedIssueCode="",SortOrder=60},
        new MaintenanceTaskDefinition{SectionName="Air / Brake System",TaskCode="BRK-07",TaskName="ABS/EBS warning indication",ActionCode="D",Specification="",Severity="Critical",UnitCode="",SuggestedIssueCode="",SortOrder=70},
        new MaintenanceTaskDefinition{SectionName="Air / Brake System",TaskCode="BRK-08",TaskName="Brake balance functional check",ActionCode="F",Specification="",Severity="Critical",UnitCode="",SuggestedIssueCode="",SortOrder=80},
        new MaintenanceTaskDefinition{SectionName="Axles / Driveline",TaskCode="AXL-01",TaskName="Front axle",ActionCode="I",Specification="",Severity="Major",UnitCode="",SuggestedIssueCode="",SortOrder=90},
        new MaintenanceTaskDefinition{SectionName="Axles / Driveline",TaskCode="AXL-02",TaskName="Rear axle(s)",ActionCode="I",Specification="",Severity="Major",UnitCode="",SuggestedIssueCode="",SortOrder=100},
        new MaintenanceTaskDefinition{SectionName="Axles / Driveline",TaskCode="AXL-03",TaskName="Differential",ActionCode="I",Specification="",Severity="Major",UnitCode="",SuggestedIssueCode="",SortOrder=110},
        new MaintenanceTaskDefinition{SectionName="Axles / Driveline",TaskCode="AXL-04",TaskName="Hub and bearing",ActionCode="I",Specification="",Severity="Critical",UnitCode="",SuggestedIssueCode="",SortOrder=120},
        new MaintenanceTaskDefinition{SectionName="Axles / Driveline",TaskCode="AXL-05",TaskName="Leakage",ActionCode="I",Specification="",Severity="Major",UnitCode="",SuggestedIssueCode="",SortOrder=130},
        new MaintenanceTaskDefinition{SectionName="Axles / Driveline",TaskCode="AXL-06",TaskName="Driveline play",ActionCode="I",Specification="",Severity="Major",UnitCode="",SuggestedIssueCode="",SortOrder=140},
        new MaintenanceTaskDefinition{SectionName="Battery & High-Voltage System",TaskCode="BAT-01",TaskName="Battery pack physical condition",ActionCode="I",Specification="",Severity="Critical",UnitCode="",SuggestedIssueCode="",SortOrder=150},
        new MaintenanceTaskDefinition{SectionName="Battery & High-Voltage System",TaskCode="BAT-02",TaskName="Battery mounting and protection",ActionCode="T",Specification="",Severity="Critical",UnitCode="",SuggestedIssueCode="",SortOrder=160},
        new MaintenanceTaskDefinition{SectionName="Battery & High-Voltage System",TaskCode="BAT-03",TaskName="HV cables and harness condition",ActionCode="I",Specification="",Severity="Critical",UnitCode="",SuggestedIssueCode="",SortOrder=170},
        new MaintenanceTaskDefinition{SectionName="Battery & High-Voltage System",TaskCode="BAT-04",TaskName="HV connectors and locking",ActionCode="I",Specification="",Severity="Critical",UnitCode="",SuggestedIssueCode="",SortOrder=180},
        new MaintenanceTaskDefinition{SectionName="Battery & High-Voltage System",TaskCode="BAT-05",TaskName="Signs of overheating, arcing or damage",ActionCode="I",Specification="",Severity="Critical",UnitCode="",SuggestedIssueCode="",SortOrder=190},
        new MaintenanceTaskDefinition{SectionName="Battery & High-Voltage System",TaskCode="BAT-06",TaskName="Battery temperature reading",ActionCode="M",Specification="5–45 °C",Severity="Major",UnitCode="°C",SuggestedIssueCode="BATT-TEMP",SortOrder=200},
        new MaintenanceTaskDefinition{SectionName="Battery & High-Voltage System",TaskCode="BAT-07",TaskName="State of charge",ActionCode="M",Specification="0–100 %",Severity="",UnitCode="%",SuggestedIssueCode="",SortOrder=210},
        new MaintenanceTaskDefinition{SectionName="Battery & High-Voltage System",TaskCode="BAT-08",TaskName="State of health",ActionCode="M",Specification="80–100 %",Severity="Major",UnitCode="%",SuggestedIssueCode="",SortOrder=220},
        new MaintenanceTaskDefinition{SectionName="Battery & High-Voltage System",TaskCode="BAT-09",TaskName="Cell imbalance indication",ActionCode="D",Specification="",Severity="Major",UnitCode="",SuggestedIssueCode="",SortOrder=230},
        new MaintenanceTaskDefinition{SectionName="Battery & High-Voltage System",TaskCode="BAT-10",TaskName="Isolation / insulation status",ActionCode="D",Specification="",Severity="Critical",UnitCode="",SuggestedIssueCode="HV-ISO",SortOrder=240},
        new MaintenanceTaskDefinition{SectionName="Battery & High-Voltage System",TaskCode="BAT-11",TaskName="Battery cooling system condition",ActionCode="I",Specification="",Severity="Major",UnitCode="",SuggestedIssueCode="",SortOrder=250},
        new MaintenanceTaskDefinition{SectionName="Battery & High-Voltage System",TaskCode="BAT-12",TaskName="Coolant leakage",ActionCode="I",Specification="",Severity="Major",UnitCode="",SuggestedIssueCode="COOL-HOSE",SortOrder=260},
        new MaintenanceTaskDefinition{SectionName="Battery & High-Voltage System",TaskCode="BAT-13",TaskName="Battery warning or fault indication",ActionCode="D",Specification="",Severity="Critical",UnitCode="",SuggestedIssueCode="",SortOrder=270},
        new MaintenanceTaskDefinition{SectionName="Battery & High-Voltage System",TaskCode="BAT-14",TaskName="Battery communication status",ActionCode="D",Specification="",Severity="Major",UnitCode="mm",SuggestedIssueCode="",SortOrder=280},
        new MaintenanceTaskDefinition{SectionName="Body & Safety",TaskCode="BOD-01",TaskName="Driver seat and mounting",ActionCode="T",Specification="",Severity="Major",UnitCode="",SuggestedIssueCode="",SortOrder=290},
        new MaintenanceTaskDefinition{SectionName="Body & Safety",TaskCode="BOD-02",TaskName="Body panels and doors",ActionCode="I",Specification="",Severity="Minor",UnitCode="",SuggestedIssueCode="",SortOrder=300},
        new MaintenanceTaskDefinition{SectionName="Body & Safety",TaskCode="BOD-03",TaskName="Mirrors",ActionCode="I",Specification="",Severity="Major",UnitCode="",SuggestedIssueCode="",SortOrder=310},
        new MaintenanceTaskDefinition{SectionName="Body & Safety",TaskCode="BOD-04",TaskName="Windscreen",ActionCode="I",Specification="",Severity="Major",UnitCode="",SuggestedIssueCode="",SortOrder=320},
        new MaintenanceTaskDefinition{SectionName="Body & Safety",TaskCode="BOD-05",TaskName="Wiper",ActionCode="I",Specification="",Severity="Minor",UnitCode="",SuggestedIssueCode="",SortOrder=330},
        new MaintenanceTaskDefinition{SectionName="Cabin & Safety",TaskCode="CAB-01",TaskName="Steering",ActionCode="I",Specification="",Severity="Critical",UnitCode="",SuggestedIssueCode="",SortOrder=340},
        new MaintenanceTaskDefinition{SectionName="Cabin & Safety",TaskCode="CAB-02",TaskName="Seat and seat belt",ActionCode="I",Specification="",Severity="Critical",UnitCode="",SuggestedIssueCode="",SortOrder=350},
        new MaintenanceTaskDefinition{SectionName="Cabin & Safety",TaskCode="CAB-03",TaskName="Mirrors",ActionCode="I",Specification="",Severity="Major",UnitCode="",SuggestedIssueCode="",SortOrder=360},
        new MaintenanceTaskDefinition{SectionName="Cabin & Safety",TaskCode="CAB-04",TaskName="Windscreen and wipers",ActionCode="I",Specification="",Severity="Major",UnitCode="",SuggestedIssueCode="",SortOrder=370},
        new MaintenanceTaskDefinition{SectionName="Cabin & Safety",TaskCode="CAB-05",TaskName="HVAC",ActionCode="I",Specification="",Severity="Minor",UnitCode="",SuggestedIssueCode="",SortOrder=380},
        new MaintenanceTaskDefinition{SectionName="Cabin & Safety",TaskCode="CAB-06",TaskName="Mandatory emergency equipment",ActionCode="I",Specification="",Severity="Major",UnitCode="",SuggestedIssueCode="",SortOrder=390},
        new MaintenanceTaskDefinition{SectionName="Charging System",TaskCode="CHG-01",TaskName="Charge inlet condition",ActionCode="I",Specification="",Severity="Major",UnitCode="",SuggestedIssueCode="",SortOrder=400},
        new MaintenanceTaskDefinition{SectionName="Charging System",TaskCode="CHG-02",TaskName="Charge connector condition",ActionCode="I",Specification="",Severity="Major",UnitCode="",SuggestedIssueCode="",SortOrder=410},
        new MaintenanceTaskDefinition{SectionName="Charging System",TaskCode="CHG-03",TaskName="Connector pins",ActionCode="T",Specification="",Severity="Major",UnitCode="",SuggestedIssueCode="",SortOrder=420},
        new MaintenanceTaskDefinition{SectionName="Charging System",TaskCode="CHG-04",TaskName="Locking mechanism",ActionCode="I",Specification="",Severity="Major",UnitCode="",SuggestedIssueCode="",SortOrder=430},
        new MaintenanceTaskDefinition{SectionName="Charging System",TaskCode="CHG-05",TaskName="Charging communication",ActionCode="D",Specification="",Severity="Major",UnitCode="mm",SuggestedIssueCode="",SortOrder=440},
        new MaintenanceTaskDefinition{SectionName="Charging System",TaskCode="CHG-06",TaskName="Charging functional check",ActionCode="F",Specification="",Severity="Major",UnitCode="",SuggestedIssueCode="",SortOrder=450},
        new MaintenanceTaskDefinition{SectionName="Charging System",TaskCode="CHG-07",TaskName="Signs of overheating or burning",ActionCode="I",Specification="",Severity="Critical",UnitCode="",SuggestedIssueCode="",SortOrder=460},
        new MaintenanceTaskDefinition{SectionName="Charging System",TaskCode="CHG-08",TaskName="Protective caps and covers",ActionCode="I",Specification="",Severity="Minor",UnitCode="",SuggestedIssueCode="",SortOrder=470},
        new MaintenanceTaskDefinition{SectionName="Hydraulics",TaskCode="HYD-01",TaskName="Hydraulic oil level and condition",ActionCode="I",Specification="",Severity="Major",UnitCode="",SuggestedIssueCode="",SortOrder=480},
        new MaintenanceTaskDefinition{SectionName="Hydraulics",TaskCode="HYD-02",TaskName="Leakage",ActionCode="I",Specification="",Severity="Major",UnitCode="",SuggestedIssueCode="",SortOrder=490},
        new MaintenanceTaskDefinition{SectionName="Hydraulics",TaskCode="HYD-03",TaskName="Hoses",ActionCode="I",Specification="",Severity="Major",UnitCode="",SuggestedIssueCode="",SortOrder=500},
        new MaintenanceTaskDefinition{SectionName="Hydraulics",TaskCode="HYD-04",TaskName="Lift arms",ActionCode="I",Specification="",Severity="Major",UnitCode="",SuggestedIssueCode="",SortOrder=510},
        new MaintenanceTaskDefinition{SectionName="Hydraulics",TaskCode="HYD-05",TaskName="Lifting function",ActionCode="F",Specification="",Severity="Critical",UnitCode="",SuggestedIssueCode="",SortOrder=520},
        new MaintenanceTaskDefinition{SectionName="Implement Attachment",TaskCode="IMP-01",TaskName="Three-point linkage",ActionCode="T",Specification="",Severity="Critical",UnitCode="",SuggestedIssueCode="",SortOrder=530},
        new MaintenanceTaskDefinition{SectionName="Implement Attachment",TaskCode="IMP-02",TaskName="Top link and pins",ActionCode="T",Specification="",Severity="Major",UnitCode="",SuggestedIssueCode="",SortOrder=540},
        new MaintenanceTaskDefinition{SectionName="Implement Attachment",TaskCode="IMP-03",TaskName="Safety locking",ActionCode="I",Specification="",Severity="Critical",UnitCode="",SuggestedIssueCode="",SortOrder=550},
        new MaintenanceTaskDefinition{SectionName="Low-Voltage Electrical",TaskCode="LV-01",TaskName="12 V auxiliary battery condition",ActionCode="I",Specification="",Severity="Major",UnitCode="",SuggestedIssueCode="",SortOrder=560},
        new MaintenanceTaskDefinition{SectionName="Low-Voltage Electrical",TaskCode="LV-02",TaskName="Battery terminals",ActionCode="I",Specification="",Severity="Minor",UnitCode="",SuggestedIssueCode="",SortOrder=570},
        new MaintenanceTaskDefinition{SectionName="Low-Voltage Electrical",TaskCode="LV-03",TaskName="Headlamps",ActionCode="I",Specification="",Severity="Major",UnitCode="",SuggestedIssueCode="",SortOrder=580},
        new MaintenanceTaskDefinition{SectionName="Low-Voltage Electrical",TaskCode="LV-04",TaskName="Tail lamps",ActionCode="I",Specification="",Severity="Major",UnitCode="",SuggestedIssueCode="",SortOrder=590},
        new MaintenanceTaskDefinition{SectionName="Low-Voltage Electrical",TaskCode="LV-05",TaskName="Indicators and hazard lights",ActionCode="I",Specification="",Severity="Major",UnitCode="",SuggestedIssueCode="",SortOrder=600},
        new MaintenanceTaskDefinition{SectionName="Low-Voltage Electrical",TaskCode="LV-06",TaskName="Horn",ActionCode="I",Specification="",Severity="Major",UnitCode="",SuggestedIssueCode="",SortOrder=610},
        new MaintenanceTaskDefinition{SectionName="Low-Voltage Electrical",TaskCode="LV-07",TaskName="Instrument cluster",ActionCode="I",Specification="",Severity="Major",UnitCode="",SuggestedIssueCode="",SortOrder=620},
        new MaintenanceTaskDefinition{SectionName="Low-Voltage Electrical",TaskCode="LV-08",TaskName="Warning lamps",ActionCode="D",Specification="",Severity="Major",UnitCode="",SuggestedIssueCode="",SortOrder=630},
        new MaintenanceTaskDefinition{SectionName="Low-Voltage Electrical",TaskCode="LV-09",TaskName="Visible wiring harness condition",ActionCode="I",Specification="",Severity="Major",UnitCode="",SuggestedIssueCode="",SortOrder=640},
        new MaintenanceTaskDefinition{SectionName="Low-Voltage Electrical",TaskCode="LV-10",TaskName="Fuse and relay condition",ActionCode="I",Specification="",Severity="Minor",UnitCode="",SuggestedIssueCode="",SortOrder=650},
        new MaintenanceTaskDefinition{SectionName="Motor & AMT",TaskCode="AMT-01",TaskName="Gearbox oil level and condition",ActionCode="I",Specification="",Severity="Major",UnitCode="",SuggestedIssueCode="",SortOrder=660},
        new MaintenanceTaskDefinition{SectionName="Motor & AMT",TaskCode="AMT-02",TaskName="Gearbox leakage",ActionCode="I",Specification="",Severity="Major",UnitCode="",SuggestedIssueCode="",SortOrder=670},
        new MaintenanceTaskDefinition{SectionName="Motor & AMT",TaskCode="AMT-03",TaskName="AMT operation",ActionCode="F",Specification="",Severity="Critical",UnitCode="",SuggestedIssueCode="",SortOrder=680},
        new MaintenanceTaskDefinition{SectionName="Motor & AMT",TaskCode="AMT-04",TaskName="Gear-shift function",ActionCode="I",Specification="",Severity="Critical",UnitCode="",SuggestedIssueCode="",SortOrder=690},
        new MaintenanceTaskDefinition{SectionName="Motor & AMT",TaskCode="AMT-05",TaskName="Transmission mounting",ActionCode="T",Specification="",Severity="Major",UnitCode="",SuggestedIssueCode="",SortOrder=700},
        new MaintenanceTaskDefinition{SectionName="Motor / Inverter / Controller",TaskCode="MOT-01",TaskName="Motor mounting",ActionCode="T",Specification="",Severity="Major",UnitCode="",SuggestedIssueCode="",SortOrder=710},
        new MaintenanceTaskDefinition{SectionName="Motor / Inverter / Controller",TaskCode="MOT-02",TaskName="Abnormal motor noise",ActionCode="I",Specification="",Severity="Major",UnitCode="",SuggestedIssueCode="",SortOrder=720},
        new MaintenanceTaskDefinition{SectionName="Motor / Inverter / Controller",TaskCode="MOT-03",TaskName="Motor vibration",ActionCode="I",Specification="",Severity="Major",UnitCode="",SuggestedIssueCode="",SortOrder=730},
        new MaintenanceTaskDefinition{SectionName="Motor / Inverter / Controller",TaskCode="MOT-04",TaskName="Motor temperature",ActionCode="M",Specification="0–90 °C",Severity="Major",UnitCode="°C",SuggestedIssueCode="",SortOrder=740},
        new MaintenanceTaskDefinition{SectionName="Motor / Inverter / Controller",TaskCode="MOT-05",TaskName="Motor electrical connections",ActionCode="I",Specification="",Severity="Critical",UnitCode="",SuggestedIssueCode="",SortOrder=750},
        new MaintenanceTaskDefinition{SectionName="Motor / Inverter / Controller",TaskCode="MOT-06",TaskName="Inverter / controller condition",ActionCode="I",Specification="",Severity="Critical",UnitCode="",SuggestedIssueCode="",SortOrder=760},
        new MaintenanceTaskDefinition{SectionName="Motor / Inverter / Controller",TaskCode="MOT-07",TaskName="Cooling connections and leakage",ActionCode="I",Specification="",Severity="Major",UnitCode="",SuggestedIssueCode="",SortOrder=770},
        new MaintenanceTaskDefinition{SectionName="Motor / Inverter / Controller",TaskCode="MOT-08",TaskName="Fault indication / DTC review",ActionCode="D",Specification="",Severity="Major",UnitCode="",SuggestedIssueCode="",SortOrder=780},
        new MaintenanceTaskDefinition{SectionName="Motor / Inverter / Controller",TaskCode="MOT-09",TaskName="Regenerative braking functional check",ActionCode="F",Specification="",Severity="Critical",UnitCode="",SuggestedIssueCode="",SortOrder=790},
        new MaintenanceTaskDefinition{SectionName="PTO",TaskCode="PTO-01",TaskName="PTO engagement",ActionCode="F",Specification="",Severity="Critical",UnitCode="",SuggestedIssueCode="",SortOrder=800},
        new MaintenanceTaskDefinition{SectionName="PTO",TaskCode="PTO-02",TaskName="540 RPM mode operation",ActionCode="F",Specification="",Severity="Major",UnitCode="",SuggestedIssueCode="",SortOrder=810},
        new MaintenanceTaskDefinition{SectionName="PTO",TaskCode="PTO-03",TaskName="1000 RPM mode operation",ActionCode="F",Specification="",Severity="Major",UnitCode="",SuggestedIssueCode="",SortOrder=820},
        new MaintenanceTaskDefinition{SectionName="PTO",TaskCode="PTO-04",TaskName="PTO shaft condition",ActionCode="I",Specification="",Severity="Critical",UnitCode="",SuggestedIssueCode="",SortOrder=830},
        new MaintenanceTaskDefinition{SectionName="PTO",TaskCode="PTO-05",TaskName="PTO guard",ActionCode="I",Specification="",Severity="Critical",UnitCode="",SuggestedIssueCode="",SortOrder=840},
        new MaintenanceTaskDefinition{SectionName="Replacement — Battery & HV",TaskCode="RPL",TaskName="Battery coolant",ActionCode="R",Specification="interval TBC",Severity="Major",UnitCode="",SuggestedIssueCode="",SortOrder=850},
        new MaintenanceTaskDefinition{SectionName="Steering & Suspension",TaskCode="STR-01",TaskName="Steering free play and operation",ActionCode="F",Specification="",Severity="Critical",UnitCode="",SuggestedIssueCode="",SortOrder=860},
        new MaintenanceTaskDefinition{SectionName="Steering & Suspension",TaskCode="STR-02",TaskName="Front suspension condition",ActionCode="I",Specification="",Severity="Major",UnitCode="",SuggestedIssueCode="",SortOrder=870},
        new MaintenanceTaskDefinition{SectionName="Steering & Suspension",TaskCode="STR-03",TaskName="Rear suspension condition",ActionCode="I",Specification="",Severity="Major",UnitCode="",SuggestedIssueCode="",SortOrder=880},
        new MaintenanceTaskDefinition{SectionName="Steering & Suspension",TaskCode="STR-04",TaskName="Shock absorber condition",ActionCode="I",Specification="",Severity="Major",UnitCode="",SuggestedIssueCode="",SortOrder=890},
        new MaintenanceTaskDefinition{SectionName="Steering & Suspension",TaskCode="STR-05",TaskName="Mounting points",ActionCode="T",Specification="",Severity="Major",UnitCode="",SuggestedIssueCode="",SortOrder=900},
        new MaintenanceTaskDefinition{SectionName="Transmission",TaskCode="TRN-01",TaskName="Gear selection",ActionCode="F",Specification="",Severity="Major",UnitCode="",SuggestedIssueCode="",SortOrder=910},
        new MaintenanceTaskDefinition{SectionName="Transmission",TaskCode="TRN-02",TaskName="8F + 2R operation",ActionCode="F",Specification="",Severity="Major",UnitCode="",SuggestedIssueCode="",SortOrder=920},
        new MaintenanceTaskDefinition{SectionName="Transmission",TaskCode="TRN-03",TaskName="Transmission oil condition",ActionCode="I",Specification="",Severity="Major",UnitCode="",SuggestedIssueCode="",SortOrder=930},
        new MaintenanceTaskDefinition{SectionName="Transmission",TaskCode="TRN-04",TaskName="Leakage",ActionCode="I",Specification="",Severity="Major",UnitCode="",SuggestedIssueCode="",SortOrder=940},
        new MaintenanceTaskDefinition{SectionName="Tyres / Chassis / Coupling",TaskCode="TYR-01",TaskName="Tyre pressure — front left",ActionCode="M",Specification="90–130 psi",Severity="Major",UnitCode="bar",SuggestedIssueCode="",SortOrder=950},
        new MaintenanceTaskDefinition{SectionName="Tyres / Chassis / Coupling",TaskCode="TYR-02",TaskName="Tyre pressure — front right",ActionCode="M",Specification="90–130 psi",Severity="Major",UnitCode="bar",SuggestedIssueCode="",SortOrder=960},
        new MaintenanceTaskDefinition{SectionName="Tyres / Chassis / Coupling",TaskCode="TYR-03",TaskName="Tread depth — minimum across axles",ActionCode="M",Specification="3–20 mm",Severity="Critical",UnitCode="mm",SuggestedIssueCode="",SortOrder=970},
        new MaintenanceTaskDefinition{SectionName="Tyres / Chassis / Coupling",TaskCode="TYR-04",TaskName="Abnormal or uneven wear",ActionCode="I",Specification="",Severity="Major",UnitCode="",SuggestedIssueCode="",SortOrder=980},
        new MaintenanceTaskDefinition{SectionName="Tyres / Chassis / Coupling",TaskCode="TYR-05",TaskName="Wheel alignment and fasteners",ActionCode="T",Specification="",Severity="Critical",UnitCode="Nm",SuggestedIssueCode="",SortOrder=990},
        new MaintenanceTaskDefinition{SectionName="Tyres / Chassis / Coupling",TaskCode="TYR-06",TaskName="Frame cracks or deformation",ActionCode="I",Specification="",Severity="Critical",UnitCode="",SuggestedIssueCode="",SortOrder=1000},
        new MaintenanceTaskDefinition{SectionName="Tyres / Chassis / Coupling",TaskCode="TYR-07",TaskName="Fifth wheel / kingpin coupling",ActionCode="I",Specification="",Severity="Critical",UnitCode="",SuggestedIssueCode="",SortOrder=1010},
        new MaintenanceTaskDefinition{SectionName="Tyres / Chassis / Coupling",TaskCode="TYR-08",TaskName="Trailer electrical and air connections",ActionCode="I",Specification="",Severity="Critical",UnitCode="",SuggestedIssueCode="",SortOrder=1020}
    };
    foreach(var t in montraTasks)
        if(!await db.MaintenanceTaskDefinitions.AnyAsync(x=>x.TaskCode==t.TaskCode)) db.MaintenanceTaskDefinitions.Add(t);
    var montraReplacementRules=new[]{
        new MaintenanceReplacementRule{Platform="All platforms",SystemName="Battery & HV",ItemName="Battery coolant",PartNumber="",ActionCode="R",UsageInterval=null,IntervalMonths=null,Quantity=null,Notes="Closed-loop circuit; check specification before renewal",IsActive=false},
        new MaintenanceReplacementRule{Platform="All platforms",SystemName="Battery & HV",ItemName="HV service disconnect inspection seal",PartNumber="",ActionCode="R",UsageInterval=null,IntervalMonths=null,Quantity=null,Notes="Renew seal after any HV intervention",IsActive=false},
        new MaintenanceReplacementRule{Platform="All platforms",SystemName="Motor / Inverter",ItemName="Motor & inverter coolant",PartNumber="",ActionCode="R",UsageInterval=null,IntervalMonths=null,Quantity=null,Notes="May share the battery circuit on some variants",IsActive=false},
        new MaintenanceReplacementRule{Platform="All platforms",SystemName="Low-Voltage",ItemName="12 V auxiliary battery",PartNumber="",ActionCode="R",UsageInterval=null,IntervalMonths=null,Quantity=null,Notes="Condition-based renewal is common; confirm policy",IsActive=false},
        new MaintenanceReplacementRule{Platform="All platforms",SystemName="Cabin",ItemName="Cabin air filter",PartNumber="",ActionCode="R",UsageInterval=null,IntervalMonths=null,Quantity=null,Notes="Shorter interval in dusty duty cycles",IsActive=false},
        new MaintenanceReplacementRule{Platform="Rhino MHCV",SystemName="Air / Brake",ItemName="Air dryer desiccant cartridge",PartNumber="",ActionCode="R",UsageInterval=null,IntervalMonths=null,Quantity=null,Notes="Critical to brake system moisture control",IsActive=false},
        new MaintenanceReplacementRule{Platform="Rhino MHCV",SystemName="Air / Brake",ItemName="Brake fluid (hydraulic circuits where fitted)",PartNumber="",ActionCode="R",UsageInterval=null,IntervalMonths=null,Quantity=null,Notes="Hygroscopic; time-based not distance-based",IsActive=false},
        new MaintenanceReplacementRule{Platform="Rhino MHCV",SystemName="Motor & AMT",ItemName="AMT gearbox oil and filter",PartNumber="",ActionCode="R",UsageInterval=null,IntervalMonths=null,Quantity=null,Notes="Confirm grade and fill volume with Montra",IsActive=false},
        new MaintenanceReplacementRule{Platform="Rhino MHCV",SystemName="Axles / Driveline",ItemName="Differential oil",PartNumber="",ActionCode="R",UsageInterval=null,IntervalMonths=null,Quantity=null,Notes="",IsActive=false},
        new MaintenanceReplacementRule{Platform="Rhino MHCV",SystemName="Axles / Driveline",ItemName="Wheel bearing grease",PartNumber="",ActionCode="L",UsageInterval=null,IntervalMonths=null,Quantity=null,Notes="Repack at overhaul interval",IsActive=false},
        new MaintenanceReplacementRule{Platform="Rhino MHCV",SystemName="Tyres / Chassis",ItemName="Fifth wheel / kingpin grease",PartNumber="",ActionCode="L",UsageInterval=null,IntervalMonths=null,Quantity=null,Notes="Shorter interval than the service ladder",IsActive=false},
        new MaintenanceReplacementRule{Platform="EVIATOR SCV",SystemName="Brakes",ItemName="Brake fluid",PartNumber="",ActionCode="R",UsageInterval=null,IntervalMonths=null,Quantity=null,Notes="Time-based",IsActive=false},
        new MaintenanceReplacementRule{Platform="EVIATOR SCV",SystemName="Brakes",ItemName="Brake pads / shoes",PartNumber="",ActionCode="R",UsageInterval=null,IntervalMonths=null,Quantity=null,Notes="Condition-based; measure at every service",IsActive=false},
        new MaintenanceReplacementRule{Platform="EVIATOR SCV",SystemName="Steering & Suspension",ItemName="Steering linkage grease",PartNumber="",ActionCode="L",UsageInterval=null,IntervalMonths=null,Quantity=null,Notes="",IsActive=false},
        new MaintenanceReplacementRule{Platform="Super Auto 3W",SystemName="Brakes",ItemName="Brake fluid",PartNumber="",ActionCode="R",UsageInterval=null,IntervalMonths=null,Quantity=null,Notes="Time-based",IsActive=false},
        new MaintenanceReplacementRule{Platform="Super Auto 3W",SystemName="Brakes",ItemName="Brake shoes",PartNumber="",ActionCode="R",UsageInterval=null,IntervalMonths=null,Quantity=null,Notes="Condition-based",IsActive=false},
        new MaintenanceReplacementRule{Platform="E-Tractor",SystemName="Hydraulics",ItemName="Hydraulic oil and filter",PartNumber="",ActionCode="R",UsageInterval=null,IntervalMonths=null,Quantity=null,Notes="Primary wear item on a tractor",IsActive=false},
        new MaintenanceReplacementRule{Platform="E-Tractor",SystemName="Transmission",ItemName="Transmission oil",PartNumber="",ActionCode="R",UsageInterval=null,IntervalMonths=null,Quantity=null,Notes="Confirm grade for 8F + 2R gearbox",IsActive=false},
        new MaintenanceReplacementRule{Platform="E-Tractor",SystemName="PTO",ItemName="PTO shaft grease",PartNumber="",ActionCode="L",UsageInterval=null,IntervalMonths=null,Quantity=null,Notes="Short interval; guard must be refitted",IsActive=false},
        new MaintenanceReplacementRule{Platform="E-Tractor",SystemName="Implement Attachment",ItemName="Three-point linkage pins and bushes",PartNumber="",ActionCode="R",UsageInterval=null,IntervalMonths=null,Quantity=null,Notes="Wear item under load",IsActive=false}
    };
    foreach(var r in montraReplacementRules)
        if(!await db.MaintenanceReplacementRules.AnyAsync(x=>x.Platform==r.Platform&&x.ItemName==r.ItemName)) db.MaintenanceReplacementRules.Add(r);
    await db.SaveChangesAsync();

    async Task<VehicleModelMaster> EnsureModel(string code,string name,string type,string description="")
    {
        var m=await db.VehicleModelMasters.FirstOrDefaultAsync(x=>x.ModelCode==code);
        if(m is null){m=new VehicleModelMaster{ModelCode=code,Name=name,ManufacturerCode="MONTRA",VehicleTypeCode=type,PowertrainCode="EV",IsActive=true};db.VehicleModelMasters.Add(m);await db.SaveChangesAsync();}
        return m;
    }
    async Task EnsureVariant(VehicleModelMaster m,string code,string name,decimal? battery=null,string config="")
    {
        if(!await db.VehicleVariantMasters.AnyAsync(x=>x.VehicleModelMasterId==m.Id&&x.VariantCode==code))
        {db.VehicleVariantMasters.Add(new VehicleVariantMaster{VehicleModelMasterId=m.Id,VariantCode=code,Name=name,BatteryCapacityKwh=battery,Configuration=config,IsActive=true});await db.SaveChangesAsync();}
    }

    var superAuto=await EnsureModel("SUPER_AUTO","Super Auto","E3W");
    var superCargo=await EnsureModel("SUPER_CARGO","Super Cargo","E3W");
    var eviator=await EnsureModel("EVIATOR","EVIATOR","ESCV");
    var rhino=await EnsureModel("RHINO_5538_EV","Rhino 5538 EV","EMHCV");
    var tipper=await EnsureModel("TIPPER_2868_EV","Tipper 2868 EV","EMHCV");
    var tractor27=await EnsureModel("TRACTOR_E27","Tractor e-27","ETRACTOR");
    var tractor45=await EnsureModel("TRACTOR_E45","Tractor e-45","ETRACTOR");

    // Ten public product/variant references from Montra Electric portfolio.
    await EnsureVariant(superCargo,"ECX","eCX",null,"Super Cargo");
    await EnsureVariant(superCargo,"ECX_DPLUS","eCX d+",null,"Super Cargo");
    await EnsureVariant(superCargo,"EQX","eQX",null,"Super Cargo");
    await EnsureVariant(superCargo,"EQX_DPLUS","eQX d+",null,"Super Cargo");
    await EnsureVariant(eviator,"EVIATOR_350_32","EVIATOR 350 (32 kWh)",32,"Urban / short-distance");
    await EnsureVariant(eviator,"EVIATOR_40","EVIATOR (40 kWh)",40,"Core EVIATOR");
    await EnsureVariant(eviator,"EVIATOR_350L_50","EVIATOR 350L+ (50 kWh)",50,"Long-distance / intercity");
    await EnsureVariant(rhino,"RHINO_5538_4X2","Rhino 5538 EV 4x2",null,"4x2");
    await EnsureVariant(rhino,"RHINO_5538_6X4","Rhino 5538 EV 6x4",null,"6x4");
    await EnsureVariant(tipper,"TIPPER_2868_6X4","Tipper 2868 EV 6x4",null,"6x4 Tipper");

    await PmMasterSeedV177.SeedAsync(db);
    await DemoVehicleSeedV179.SeedAsync(db);


    async Task<ServiceCentreMaster> EnsureCentre(string code,string name,string address,string city,string state,string postal,string mobile,string email)
    {
        var c=await db.ServiceCentreMasters.FirstOrDefaultAsync(x=>x.CentreCode==code);
        if(c is null)
        {
            c=new ServiceCentreMaster{CentreCode=code,Name=name,CentreType="Authorized",AddressLine1=address,City=city,State=state,PostalCode=postal,
                Mobile=mobile,Email=email,BayCount=3,IsActive=true};
            db.ServiceCentreMasters.Add(c);await db.SaveChangesAsync();
        }
        else
        {
            c.Name=name;c.CentreType="Authorized";c.AddressLine1=address;c.City=city;c.State=state;c.PostalCode=postal;c.Mobile=mobile;c.Email=email;c.BayCount=3;c.IsActive=true;
            await db.SaveChangesAsync();
        }
        return c;
    }
    async Task EnsureCentreModels(ServiceCentreMaster centre, params VehicleModelMaster[] supported)
    {
        foreach(var m in supported)
            if(!await db.ServiceCentreModelSupports.AnyAsync(x=>x.ServiceCentreMasterId==centre.Id&&x.VehicleModelMasterId==m.Id))
                db.ServiceCentreModelSupports.Add(new ServiceCentreModelSupport{ServiceCentreMasterId=centre.Id,VehicleModelMasterId=m.Id});
        await db.SaveChangesAsync();
    }

    var anjana=await EnsureCentre("ANJANA-ATP","Anjana Electric Vehicles",
        "6-240,241,242,243,244,245, M R S M Complex, Azad Nagar, Bellary Bypass Road","Anantapur","Andhra Pradesh","515004","9110375242","anjanaelectric.atp@montraelectric.com");
    await EnsureCentreModels(anjana,superAuto,superCargo);

    var sriram=await EnsureCentre("SRIRAM-VNS","Sriram Harsha LLP",
        "Opp deer park, beside Bharath benz, Vanasthalipuram","Vanasthalipuram","Telangana","500070","9030196652","vnsnandina@gmail.com");
    await EnsureCentreModels(sriram,eviator,tractor27,tractor45);

    var sol=await EnsureCentre("SOL-GGN","SOL Automotives",
        "Ground floor, khewat no 861, kherki daula, Jaipur Road, opp govt school main road nh 8, Kherki Daula","Gurugram","Haryana","122004","9891333888","RAJESHGULIA@SOLINDIA.NET");
    await EnsureCentreModels(sol,eviator,tractor27,tractor45);

    foreach(var centre in new[]{anjana,sriram,sol})
    {
        for(var i=1;i<=3;i++)
        {
            var bayCode=$"Bay-{i:D2}";
            if(!await db.ServiceBays.AnyAsync(x=>x.ServiceCentre==centre.CentreCode&&x.BayCode==bayCode))
                db.ServiceBays.Add(new ServiceBay{ServiceCentre=centre.CentreCode,BayCode=bayCode,BayType="General",IsActive=true});
        }
    }
    await db.SaveChangesAsync();

    if (!await db.InventoryLocations.AnyAsync())
    {
        db.InventoryLocations.Add(new InventoryLocation { LocationCode="MAIN-STORE", Name="Main Parts Store", ServiceCentre="Chennai Service Centre", Bin="GENERAL" });
        await db.SaveChangesAsync();
    }
    if (!await db.PartMasters.AnyAsync())
    {
        db.PartMasters.AddRange(
            new PartMaster { PartNumber="CL-7T-018", Description="Coolant hose assembly", Category="Cooling", UnitOfMeasure="EA", IsWarrantyReturnable=true, ReorderLevel=2, ReorderQuantity=5 },
            new PartMaster { PartNumber="FLT-7T-002", Description="Cabin filter", Category="Filter", UnitOfMeasure="EA", ReorderLevel=4, ReorderQuantity=10 },
            new PartMaster { PartNumber="CLP-7T-007", Description="Hose clamp", Category="Cooling", UnitOfMeasure="EA", ReorderLevel=5, ReorderQuantity=20 }
        );
        await db.SaveChangesAsync();
    }
    var seedLocation=await db.InventoryLocations.FirstAsync();
    foreach (var p in await db.PartMasters.ToListAsync())
    {
        if (!await db.PartStocks.AnyAsync(x=>x.PartMasterId==p.Id && x.InventoryLocationId==seedLocation.Id))
            db.PartStocks.Add(new PartStock { PartMasterId=p.Id, InventoryLocationId=seedLocation.Id, OnHandQty=p.PartNumber=="CL-7T-018"?4:p.PartNumber=="FLT-7T-002"?8:12, ReservedQty=0 });
    }
    await db.SaveChangesAsync();
    if (!await db.Vehicles.AnyAsync())
    {
        db.Vehicles.AddRange(
            new Vehicle { Vin="MA1AU7EV001234", RegistrationNumber="TN12AB1234", Model="Montra eTruck 7T", Variant="Std", Status="Available", OdometerKm=48520, OperatingHours=3120, BatterySoc=78 },
            new Vehicle { Vin="MA1AU7EV007782", RegistrationNumber="KA05EV7782", Model="Montra eTruck 7T", Variant="Long Range", Status="Available", OdometerKm=36240, OperatingHours=2710, BatterySoc=64 },
            new Vehicle { Vin="MA1AU7EV009088", RegistrationNumber="TN22EV9088", Model="Montra eTruck 7T", Variant="Std", Status="Available", OdometerKm=52850, OperatingHours=3450, BatterySoc=82 }
        );
        db.ServiceBays.AddRange(
            new ServiceBay { ServiceCentre="Chennai Service Centre", BayCode="Bay-01", BayType="General" },
            new ServiceBay { ServiceCentre="Chennai Service Centre", BayCode="Bay-02", BayType="General" },
            new ServiceBay { ServiceCentre="Chennai Service Centre", BayCode="HV-01", BayType="HV" }
        );
        db.Technicians.AddRange(
            new Technician { EmployeeCode="TECH001", Name="Suresh K", ServiceCentre="Chennai Service Centre", SkillCodes="PM,BRAKE", HvAuthorized=false },
            new Technician { EmployeeCode="TECH002", Name="Meena P", ServiceCentre="Chennai Service Centre", SkillCodes="EV,HV,DIAG", HvAuthorized=true, HvAuthorizationValidUntil=DateTime.UtcNow.AddYears(1) }
        );
        await db.SaveChangesAsync();
    }
}

static void Audit(AppDbContext db, string action, string entityType, Guid? entityId, string details, string user="System")
{
    db.AuditEvents.Add(new AuditEvent {
        UserName=user, Action=action, EntityType=entityType, EntityId=entityId,
        CorrelationId=Guid.NewGuid().ToString("N"), Details=details
    });
}

app.MapGet("/api/health", () => Results.Ok(new { status="ok", service="MontraFleet.Api", version="1.7.7" }));
app.MapGet("/api/db/health", async (AppDbContext db) =>
{
    try { return await db.Database.CanConnectAsync()
        ? Results.Ok(new { status="ok", database="PostgreSQL", connected=true, version="1.7.7" })
        : Results.Problem("Database connection check returned false.", statusCode:503); }
    catch (Exception ex) { return Results.Problem("Database connection failed", ex.Message, statusCode:503); }
});
app.MapGet("/api/ui/health", (IWebHostEnvironment env) =>
{
    var webRoot = env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");
    var indexPath = Path.Combine(webRoot, "index.html");
    return Results.Ok(new { status=File.Exists(indexPath)?"ok":"missing", indexExists=File.Exists(indexPath), webRoot, version="1.7.7" });
});

static decimal NextMetricDue(decimal current, decimal? initialDue, decimal interval)
{
    if(interval<=0)return current;
    var first=initialDue.HasValue && initialDue.Value>0 ? initialDue.Value : interval;
    if(current < first)return first;
    var steps=Math.Floor((current-first)/interval)+1;
    return first+(steps*interval);
}

static DateTime NextTimeDue(DateTime basis, decimal? initialDue, decimal interval, string unit)
{
    var amount=(int)Math.Ceiling(initialDue.HasValue&&initialDue.Value>0?initialDue.Value:interval);
    if(amount<=0)amount=1;
    return unit.ToUpperInvariant() switch { "DAY" or "DAYS" => basis.AddDays(amount), "YEAR" or "YEARS" => basis.AddYears(amount), _ => basis.AddMonths(amount) };
}

static async Task EnsurePmObligationsAsync(AppDbContext db, Vehicle v)
{
    if(!v.MaintenanceProgramId.HasValue)return;
    var plans=await db.MaintenancePlans.Where(x=>x.MaintenanceProgramId==v.MaintenanceProgramId.Value&&x.IsActive).OrderBy(x=>x.Sequence).ToListAsync();
    foreach(var plan in plans)
    {
        if(await db.PmObligations.AnyAsync(x=>x.VehicleId==v.Id&&x.MaintenancePlanId==plan.Id&&x.Status!="Completed"))continue;
        var triggers=await db.MaintenancePlanTriggers.Where(x=>x.MaintenancePlanId==plan.Id&&x.IsActive).ToListAsync();
        if(triggers.Count==0)continue;
        var o=new PmObligation{VehicleId=v.Id,MaintenancePlanId=plan.Id,PlanCode=plan.PlanCode,TriggerType=string.Join(" / ",triggers.Select(x=>x.TriggerCode)),Status="Upcoming",GeneratedAt=DateTime.UtcNow};
        foreach(var t in triggers)
        {
            switch(t.TriggerCode.ToUpperInvariant())
            {
                case "ODOMETER": o.DueReading=NextMetricDue(v.OdometerKm,t.InitialDueValue,t.IntervalValue);break;
                case "OPERATING_HOURS": o.DueOperatingHours=NextMetricDue(v.OperatingHours,t.InitialDueValue,t.IntervalValue);break;
                case "KWH": o.DueEnergyKwh=NextMetricDue(v.EnergyKwh,t.InitialDueValue,t.IntervalValue);break;
                case "TIME": o.DueDate=NextTimeDue((v.CommissioningDate??v.PurchaseDate??DateTime.UtcNow).Date,t.InitialDueValue,t.IntervalValue,t.UnitCode);break;
            }
        }
        db.PmObligations.Add(o);
    }
    await db.SaveChangesAsync();
}

static async Task GenerateNextPmObligationAsync(AppDbContext db, PmObligation completed, Vehicle v)
{
    if(!completed.MaintenancePlanId.HasValue)return;
    var plan=await db.MaintenancePlans.FindAsync(completed.MaintenancePlanId.Value);if(plan is null||!plan.IsActive)return;
    var triggers=await db.MaintenancePlanTriggers.Where(x=>x.MaintenancePlanId==plan.Id&&x.IsActive).ToListAsync();
    var o=new PmObligation{VehicleId=v.Id,MaintenancePlanId=plan.Id,PlanCode=plan.PlanCode,TriggerType=string.Join(" / ",triggers.Select(x=>x.TriggerCode)),Status="Upcoming",GeneratedAt=DateTime.UtcNow};
    foreach(var t in triggers)
    {
        switch(t.TriggerCode.ToUpperInvariant())
        {
            case "ODOMETER": o.DueReading=(completed.DueReading.HasValue?completed.DueReading.Value:v.OdometerKm)+t.IntervalValue;break;
            case "OPERATING_HOURS": o.DueOperatingHours=(completed.DueOperatingHours.HasValue?completed.DueOperatingHours.Value:v.OperatingHours)+t.IntervalValue;break;
            case "KWH": o.DueEnergyKwh=(completed.DueEnergyKwh.HasValue?completed.DueEnergyKwh.Value:v.EnergyKwh)+t.IntervalValue;break;
            case "TIME":
                var basis=completed.DueDate.HasValue?completed.DueDate.Value:DateTime.UtcNow;
                o.DueDate=NextTimeDue(basis,null,t.IntervalValue,t.UnitCode);break;
        }
    }
    completed.SupersededById=o.Id;db.PmObligations.Add(o);await db.SaveChangesAsync();
}

app.MapGet("/api/dashboard/summary", async (AppDbContext db) =>
{
    var total = await db.Vehicles.CountAsync();
    var available = await db.Vehicles.CountAsync(x => x.Status == "Available");
    var maintenance = await db.Vehicles.CountAsync(x => x.Status == "Under Maintenance");
    var offHire = await db.OffHireRecords.CountAsync(x => x.Status != "Closed");
    var today = DateTime.UtcNow.Date;
    var tomorrow = today.AddDays(1);
    var appointments = await db.Appointments.CountAsync(x => x.StartAt >= today && x.StartAt < tomorrow);
    var breakdowns = await db.Breakdowns.CountAsync(x => x.Status != "Closed");
    var pmOverdue = await db.PmObligations.CountAsync(x => x.Status == "Overdue");
    return Results.Ok(new { totalVehicles=total, available, underMaintenance=maintenance, offHire, appointmentsToday=appointments, breakdownRequests=breakdowns, pmOverdue, slaBreaches=0, firstTimeFix=100.0, uptime30d=99.0 });
});

app.MapGet("/api/vehicles", async (AppDbContext db) =>
    Results.Ok(await db.Vehicles.AsNoTracking().Where(x=>x.IsActive).OrderBy(x=>x.RegistrationNumber).ToListAsync()));

app.MapPost("/api/vehicles", async (Vehicle vehicle, AppDbContext db) =>
{
    if(await db.Vehicles.AnyAsync(x=>x.Vin==vehicle.Vin || x.RegistrationNumber==vehicle.RegistrationNumber))return Results.Conflict(new{message="VIN or registration number already exists."});
    db.Vehicles.Add(vehicle);await db.SaveChangesAsync();await EnsurePmObligationsAsync(db,vehicle);return Results.Created($"/api/vehicles/{vehicle.Id}",vehicle);
});
app.MapPut("/api/vehicles/{id:guid}", async (Guid id, Vehicle input, AppDbContext db) =>
{
    var v=await db.Vehicles.FindAsync(id);if(v is null)return Results.NotFound();
    db.Entry(v).CurrentValues.SetValues(input);v.Id=id;await db.SaveChangesAsync();await EnsurePmObligationsAsync(db,v);return Results.Ok(v);
});

app.MapGet("/api/pm/master-options", async (AppDbContext db) => Results.Ok(await db.MasterOptions.AsNoTracking().OrderBy(x=>x.Category).ThenBy(x=>x.SortOrder).ThenBy(x=>x.Name).ToListAsync()));
app.MapPost("/api/pm/master-options", async (MasterOption r, AppDbContext db) =>
{
    r.Id=Guid.NewGuid();r.Category=r.Category.Trim().ToUpperInvariant();r.Code=r.Code.Trim().ToUpperInvariant();
    if(await db.MasterOptions.AnyAsync(x=>x.Category==r.Category&&x.Code==r.Code))return Results.Conflict(new{message="This code already exists in the selected master."});
    db.MasterOptions.Add(r);Audit(db,"CREATE","MasterOption",r.Id,$"{r.Category}:{r.Code}");await db.SaveChangesAsync();return Results.Ok(r);
});
app.MapPut("/api/pm/master-options/{id:guid}", async (Guid id,MasterOption r,AppDbContext db)=>
{
    var x=await db.MasterOptions.FindAsync(id);if(x is null)return Results.NotFound();x.Name=r.Name;x.Value=r.Value;x.Description=r.Description;x.SortOrder=r.SortOrder;x.IsActive=r.IsActive;await db.SaveChangesAsync();return Results.Ok(x);
});

app.MapGet("/api/pm/vehicle-models", async (AppDbContext db) => Results.Ok(await db.VehicleModelMasters.AsNoTracking().OrderBy(x=>x.Name).ToListAsync()));
app.MapPost("/api/pm/vehicle-models", async (VehicleModelMaster r, AppDbContext db) =>
{
    r.Id=Guid.NewGuid();r.ModelCode=r.ModelCode.Trim().ToUpperInvariant();if(await db.VehicleModelMasters.AnyAsync(x=>x.ModelCode==r.ModelCode))return Results.Conflict(new{message="Model code already exists."});
    db.VehicleModelMasters.Add(r);Audit(db,"CREATE","VehicleModelMaster",r.Id,r.ModelCode);await db.SaveChangesAsync();return Results.Ok(r);
});
app.MapPut("/api/pm/vehicle-models/{id:guid}", async (Guid id,VehicleModelMaster r,AppDbContext db)=>
{
    var x=await db.VehicleModelMasters.FindAsync(id);if(x is null)return Results.NotFound();x.Name=r.Name;x.ManufacturerCode=r.ManufacturerCode;x.VehicleTypeCode=r.VehicleTypeCode;x.PowertrainCode=r.PowertrainCode;x.ImageUrl=r.ImageUrl;x.GvwKg=r.GvwKg;x.BatteryCapacityKwh=r.BatteryCapacityKwh;x.IsActive=r.IsActive;await db.SaveChangesAsync();return Results.Ok(x);
});
app.MapGet("/api/pm/vehicle-variants", async (AppDbContext db) => Results.Ok(await db.VehicleVariantMasters.AsNoTracking().OrderBy(x=>x.Name).ToListAsync()));
app.MapPost("/api/pm/vehicle-variants", async (VehicleVariantMaster r,AppDbContext db)=>
{
    r.Id=Guid.NewGuid();r.VariantCode=r.VariantCode.Trim().ToUpperInvariant();if(!await db.VehicleModelMasters.AnyAsync(x=>x.Id==r.VehicleModelMasterId))return Results.BadRequest(new{message="Vehicle model not found."});
    if(await db.VehicleVariantMasters.AnyAsync(x=>x.VehicleModelMasterId==r.VehicleModelMasterId&&x.VariantCode==r.VariantCode))return Results.Conflict(new{message="Variant code already exists for the model."});
    db.VehicleVariantMasters.Add(r);await db.SaveChangesAsync();return Results.Ok(r);
});

app.MapPut("/api/pm/vehicle-variants/{id:guid}", async (Guid id,VehicleVariantMaster r,AppDbContext db)=>
{
    var x=await db.VehicleVariantMasters.FindAsync(id);if(x is null)return Results.NotFound();
    if(x.VehicleModelMasterId!=r.VehicleModelMasterId && !await db.VehicleModelMasters.AnyAsync(m=>m.Id==r.VehicleModelMasterId))return Results.BadRequest(new{message="Vehicle model not found."});
    x.VehicleModelMasterId=r.VehicleModelMasterId;x.Name=r.Name;x.ImageUrl=r.ImageUrl;x.GvwKg=r.GvwKg;x.PayloadKg=r.PayloadKg;
    x.BatteryCapacityKwh=r.BatteryCapacityKwh;x.MotorPowerKw=r.MotorPowerKw;x.WheelbaseMm=r.WheelbaseMm;x.Configuration=r.Configuration;
    x.EffectiveFrom=r.EffectiveFrom;x.IsActive=r.IsActive;await db.SaveChangesAsync();return Results.Ok(x);
});


app.MapGet("/api/pm/manufacturers", async (AppDbContext db) => Results.Ok(await db.ManufacturerMasters.AsNoTracking().OrderBy(x=>x.Name).ToListAsync()));
app.MapPost("/api/pm/manufacturers", async (ManufacturerMaster r,AppDbContext db)=>
{
    r.Id=Guid.NewGuid();r.ManufacturerCode=r.ManufacturerCode.Trim().ToUpperInvariant();
    if(string.IsNullOrWhiteSpace(r.ManufacturerCode)||string.IsNullOrWhiteSpace(r.Name))return Results.BadRequest(new{message="Manufacturer code and name are required."});
    if(await db.ManufacturerMasters.AnyAsync(x=>x.ManufacturerCode==r.ManufacturerCode))return Results.Conflict(new{message="Manufacturer code already exists."});
    db.ManufacturerMasters.Add(r);Audit(db,"CREATE","ManufacturerMaster",r.Id,r.ManufacturerCode);await db.SaveChangesAsync();return Results.Ok(r);
});
app.MapPut("/api/pm/manufacturers/{id:guid}", async (Guid id,ManufacturerMaster r,AppDbContext db)=>
{
    var x=await db.ManufacturerMasters.FindAsync(id);if(x is null)return Results.NotFound();
    x.Name=r.Name;x.Country=r.Country;x.WebsiteUrl=r.WebsiteUrl;x.ContactPhone=r.ContactPhone;x.ContactEmail=r.ContactEmail;x.IsActive=r.IsActive;
    await db.SaveChangesAsync();return Results.Ok(x);
});

app.MapGet("/api/pm/customers", async (AppDbContext db) => Results.Ok(await db.CustomerMasters.AsNoTracking().OrderBy(x=>x.Name).ToListAsync()));
app.MapPost("/api/pm/customers", async (CustomerMaster r,AppDbContext db)=>
{
    r.Id=Guid.NewGuid();r.CustomerCode=r.CustomerCode.Trim().ToUpperInvariant();
    if(string.IsNullOrWhiteSpace(r.CustomerCode)||string.IsNullOrWhiteSpace(r.Name))return Results.BadRequest(new{message="Customer code and name are required."});
    if(await db.CustomerMasters.AnyAsync(x=>x.CustomerCode==r.CustomerCode))return Results.Conflict(new{message="Customer code already exists."});
    db.CustomerMasters.Add(r);Audit(db,"CREATE","CustomerMaster",r.Id,r.CustomerCode);await db.SaveChangesAsync();return Results.Ok(r);
});
app.MapPut("/api/pm/customers/{id:guid}", async (Guid id,CustomerMaster r,AppDbContext db)=>
{
    var x=await db.CustomerMasters.FindAsync(id);if(x is null)return Results.NotFound();
    x.Name=r.Name;x.AddressLine1=r.AddressLine1;x.AddressLine2=r.AddressLine2;x.City=r.City;x.State=r.State;x.PostalCode=r.PostalCode;x.Country=r.Country;
    x.Gstin=r.Gstin;x.ContactPerson=r.ContactPerson;x.Mobile=r.Mobile;x.Email=r.Email;x.IsActive=r.IsActive;await db.SaveChangesAsync();return Results.Ok(x);
});

app.MapGet("/api/pm/depots", async (AppDbContext db) => Results.Ok(await db.DepotMasters.AsNoTracking().OrderBy(x=>x.Name).ToListAsync()));
app.MapPost("/api/pm/depots", async (DepotMaster r,AppDbContext db)=>
{
    r.Id=Guid.NewGuid();r.DepotCode=r.DepotCode.Trim().ToUpperInvariant();
    if(string.IsNullOrWhiteSpace(r.DepotCode)||string.IsNullOrWhiteSpace(r.Name))return Results.BadRequest(new{message="Depot code and name are required."});
    if(r.CustomerMasterId.HasValue&&!await db.CustomerMasters.AnyAsync(x=>x.Id==r.CustomerMasterId))return Results.BadRequest(new{message="Customer not found."});
    if(await db.DepotMasters.AnyAsync(x=>x.DepotCode==r.DepotCode))return Results.Conflict(new{message="Depot code already exists."});
    db.DepotMasters.Add(r);await db.SaveChangesAsync();return Results.Ok(r);
});
app.MapPut("/api/pm/depots/{id:guid}", async (Guid id,DepotMaster r,AppDbContext db)=>
{
    var x=await db.DepotMasters.FindAsync(id);if(x is null)return Results.NotFound();
    x.Name=r.Name;x.CustomerMasterId=r.CustomerMasterId;x.AddressLine1=r.AddressLine1;x.City=r.City;x.State=r.State;x.PostalCode=r.PostalCode;
    x.ContactPerson=r.ContactPerson;x.Mobile=r.Mobile;x.IsActive=r.IsActive;await db.SaveChangesAsync();return Results.Ok(x);
});

app.MapGet("/api/pm/service-centres", async (AppDbContext db) =>
{
    var centres=await db.ServiceCentreMasters.AsNoTracking().OrderBy(x=>x.Name).ToListAsync();
    var maps=await db.ServiceCentreModelSupports.AsNoTracking().ToListAsync();
    var models=await db.VehicleModelMasters.AsNoTracking().ToListAsync();
    return Results.Ok(centres.Select(x=>new{
        x.Id,x.CentreCode,x.Name,x.CentreType,x.AddressLine1,x.City,x.State,x.PostalCode,x.ContactPerson,x.Mobile,x.Email,x.WorkingHours,x.BayCount,x.IsActive,
        supportedModelIds=maps.Where(m=>m.ServiceCentreMasterId==x.Id).Select(m=>m.VehicleModelMasterId).ToList(),
        supportedModels=maps.Where(m=>m.ServiceCentreMasterId==x.Id).Join(models,m=>m.VehicleModelMasterId,v=>v.Id,(m,v)=>new{v.Id,v.ModelCode,v.Name}).OrderBy(v=>v.Name).ToList()
    }));
});
app.MapPost("/api/pm/service-centres", async (ServiceCentreUpsertRequest r,AppDbContext db)=>
{
    var code=r.CentreCode.Trim().ToUpperInvariant();
    if(string.IsNullOrWhiteSpace(code)||string.IsNullOrWhiteSpace(r.Name))return Results.BadRequest(new{message="Service centre code and name are required."});
    if(await db.ServiceCentreMasters.AnyAsync(x=>x.CentreCode==code))return Results.Conflict(new{message="Service centre code already exists."});
    var validModelIds=await db.VehicleModelMasters.Where(x=>r.SupportedModelIds.Contains(x.Id)).Select(x=>x.Id).ToListAsync();
    if(validModelIds.Count!=r.SupportedModelIds.Distinct().Count())return Results.BadRequest(new{message="One or more supported vehicle models are invalid."});
    var x=new ServiceCentreMaster{
        CentreCode=code,Name=r.Name,CentreType=r.CentreType,AddressLine1=r.AddressLine1,City=r.City,State=r.State,PostalCode=r.PostalCode,
        ContactPerson=r.ContactPerson,Mobile=r.Mobile,Email=r.Email,WorkingHours=r.WorkingHours,BayCount=r.BayCount<=0?3:r.BayCount,IsActive=r.IsActive
    };
    db.ServiceCentreMasters.Add(x);
    foreach(var modelId in validModelIds)db.ServiceCentreModelSupports.Add(new ServiceCentreModelSupport{ServiceCentreMasterId=x.Id,VehicleModelMasterId=modelId});
    await db.SaveChangesAsync();return Results.Ok(x);
});
app.MapPut("/api/pm/service-centres/{id:guid}", async (Guid id,ServiceCentreUpsertRequest r,AppDbContext db)=>
{
    var x=await db.ServiceCentreMasters.FindAsync(id);if(x is null)return Results.NotFound();
    var validModelIds=await db.VehicleModelMasters.Where(m=>r.SupportedModelIds.Contains(m.Id)).Select(m=>m.Id).ToListAsync();
    if(validModelIds.Count!=r.SupportedModelIds.Distinct().Count())return Results.BadRequest(new{message="One or more supported vehicle models are invalid."});
    x.Name=r.Name;x.CentreType=r.CentreType;x.AddressLine1=r.AddressLine1;x.City=r.City;x.State=r.State;x.PostalCode=r.PostalCode;
    x.ContactPerson=r.ContactPerson;x.Mobile=r.Mobile;x.Email=r.Email;x.WorkingHours=r.WorkingHours;x.BayCount=r.BayCount<=0?3:r.BayCount;x.IsActive=r.IsActive;
    var old=await db.ServiceCentreModelSupports.Where(m=>m.ServiceCentreMasterId==id).ToListAsync();db.ServiceCentreModelSupports.RemoveRange(old);
    foreach(var modelId in validModelIds)db.ServiceCentreModelSupports.Add(new ServiceCentreModelSupport{ServiceCentreMasterId=id,VehicleModelMasterId=modelId});
    await db.SaveChangesAsync();return Results.Ok(x);
});


app.MapGet("/api/work-templates", async (AppDbContext db) =>
{
    var rows=await db.WorkTemplates.AsNoTracking().OrderBy(x=>x.TemplateCode).Select(x=>new{
        x.Id,x.TemplateCode,x.Name,x.Category,x.Description,x.Version,x.StandardHours,x.RequiredSkillCode,
        x.RequiresHvAuthorization,x.RequiresQc,x.IsActive,
        fieldCount=db.WorkTemplateFields.Count(f=>f.WorkTemplateId==x.Id)
    }).ToListAsync();
    return Results.Ok(rows);
});
app.MapPost("/api/work-templates", async (WorkTemplate r,AppDbContext db)=>
{
    r.Id=Guid.NewGuid();r.TemplateCode=r.TemplateCode.Trim().ToUpperInvariant();
    if(string.IsNullOrWhiteSpace(r.TemplateCode)||string.IsNullOrWhiteSpace(r.Name))
        return Results.BadRequest(new{message="Template code and name are required."});
    if(await db.WorkTemplates.AnyAsync(x=>x.TemplateCode==r.TemplateCode))
        return Results.Conflict(new{message="Template code already exists."});
    db.WorkTemplates.Add(r);Audit(db,"CREATE","WorkTemplate",r.Id,r.TemplateCode);await db.SaveChangesAsync();return Results.Ok(r);
});
app.MapPut("/api/work-templates/{id:guid}", async (Guid id,WorkTemplate r,AppDbContext db)=>
{
    var x=await db.WorkTemplates.FindAsync(id);if(x is null)return Results.NotFound();
    x.Name=r.Name;x.Category=r.Category;x.Description=r.Description;x.StandardHours=r.StandardHours;
    x.RequiredSkillCode=r.RequiredSkillCode;x.RequiresHvAuthorization=r.RequiresHvAuthorization;
    x.RequiresQc=r.RequiresQc;x.IsActive=r.IsActive;await db.SaveChangesAsync();return Results.Ok(x);
});
app.MapGet("/api/work-templates/{id:guid}/fields", async (Guid id,AppDbContext db) =>
    Results.Ok(await db.WorkTemplateFields.AsNoTracking().Where(x=>x.WorkTemplateId==id).OrderBy(x=>x.Sequence).ToListAsync()));
app.MapPut("/api/work-templates/{id:guid}/fields", async (Guid id,List<WorkTemplateFieldInput> rows,AppDbContext db)=>
{
    var t=await db.WorkTemplates.FindAsync(id);if(t is null)return Results.NotFound();
    var old=await db.WorkTemplateFields.Where(x=>x.WorkTemplateId==id).ToListAsync();db.WorkTemplateFields.RemoveRange(old);
    var seq=0;
    foreach(var r in rows)
    {
        seq++;var code=string.IsNullOrWhiteSpace(r.FieldCode)?$"F{seq:D3}":r.FieldCode.Trim().ToUpperInvariant();
        db.WorkTemplateFields.Add(new WorkTemplateField{
            WorkTemplateId=id,SectionName=string.IsNullOrWhiteSpace(r.SectionName)?"General":r.SectionName,
            Sequence=r.Sequence<=0?seq*10:r.Sequence,FieldCode=code,Label=r.Label,FieldType=r.FieldType,
            UnitCode=r.UnitCode,IsMandatory=r.IsMandatory,MinValue=r.MinValue,MaxValue=r.MaxValue,
            Options=r.Options,FailureAction=r.FailureAction,SuggestedIssueCode=r.SuggestedIssueCode});
    }
    t.Version+=1;await db.SaveChangesAsync();return Results.Ok(new{version=t.Version,fields=rows.Count});
});

app.MapGet("/api/pm/plans/{id:guid}/templates", async (Guid id,AppDbContext db)=>
{
    var rows=await(from m in db.MaintenancePlanTemplates.AsNoTracking()
        join t in db.WorkTemplates.AsNoTracking() on m.WorkTemplateId equals t.Id
        where m.MaintenancePlanId==id orderby m.Sequence
        select new{m.Id,m.Sequence,m.IsMandatory,m.WorkTemplateId,t.TemplateCode,t.Name,t.Category,t.Version,
            t.StandardHours,t.RequiredSkillCode,t.RequiresHvAuthorization,t.RequiresQc,
            fieldCount=db.WorkTemplateFields.Count(f=>f.WorkTemplateId==t.Id)}).ToListAsync();
    return Results.Ok(rows);
});
app.MapPost("/api/pm/plans/{id:guid}/template", async (Guid id,MaintenancePlanTemplate r,AppDbContext db)=>
{
    if(!await db.MaintenancePlans.AnyAsync(x=>x.Id==id)||!await db.WorkTemplates.AnyAsync(x=>x.Id==r.WorkTemplateId))
        return Results.BadRequest(new{message="Plan or work template not found."});
    var x=await db.MaintenancePlanTemplates.FirstOrDefaultAsync(m=>m.MaintenancePlanId==id&&m.WorkTemplateId==r.WorkTemplateId);
    if(x is null){x=new MaintenancePlanTemplate{MaintenancePlanId=id,WorkTemplateId=r.WorkTemplateId};db.MaintenancePlanTemplates.Add(x);}
    x.Sequence=r.Sequence;x.IsMandatory=r.IsMandatory;await db.SaveChangesAsync();return Results.Ok(x);
});
app.MapPut("/api/pm/plans/{id:guid}/templates", async (Guid id,List<MaintenancePlanTemplate> rows,AppDbContext db)=>
{
    if(!await db.MaintenancePlans.AnyAsync(x=>x.Id==id))return Results.NotFound(new{message="Maintenance Plan not found."});
    var requested=rows.Where(x=>x.WorkTemplateId!=Guid.Empty).GroupBy(x=>x.WorkTemplateId).Select(g=>g.First()).ToList();
    var templateIds=requested.Select(x=>x.WorkTemplateId).ToList();
    var validIds=await db.WorkTemplates.Where(x=>templateIds.Contains(x.Id)&&x.IsActive).Select(x=>x.Id).ToListAsync();
    if(validIds.Count!=templateIds.Distinct().Count())return Results.BadRequest(new{message="One or more selected Work Templates are invalid or inactive."});
    var existing=await db.MaintenancePlanTemplates.Where(x=>x.MaintenancePlanId==id).ToListAsync();
    db.MaintenancePlanTemplates.RemoveRange(existing);
    foreach(var r in requested.OrderBy(x=>x.Sequence))db.MaintenancePlanTemplates.Add(new MaintenancePlanTemplate{MaintenancePlanId=id,WorkTemplateId=r.WorkTemplateId,Sequence=r.Sequence,IsMandatory=r.IsMandatory});
    await db.SaveChangesAsync();
    return Results.Ok(new{planId=id,mapped=requested.Count});
});

app.MapDelete("/api/pm/plans/{planId:guid}/template/{mappingId:guid}", async(Guid planId,Guid mappingId,AppDbContext db)=>
{
    var x=await db.MaintenancePlanTemplates.FirstOrDefaultAsync(m=>m.Id==mappingId&&m.MaintenancePlanId==planId);
    if(x is null)return Results.NotFound();db.MaintenancePlanTemplates.Remove(x);await db.SaveChangesAsync();return Results.NoContent();
});

app.MapGet("/api/issues/catalog", async(AppDbContext db) =>
{
    var rows=await db.MasterOptions.AsNoTracking().Where(x=>x.Category=="ISSUE_TYPE"&&x.IsActive).OrderBy(x=>x.SortOrder).ThenBy(x=>x.Name)
        .Select(x=>new{code=x.Code,name=x.Name,severity=x.Value,correctiveAction=x.Description}).ToListAsync();
    return Results.Ok(rows);
});

app.MapGet("/api/tasks/{workItemId:guid}/paper-form", async(Guid workItemId,AppDbContext db)=>
{
    var instance=await db.WorkTemplateInstances.AsNoTracking().FirstOrDefaultAsync(x=>x.WorkItemId==workItemId);
    if(instance is null)return Results.NotFound(new{message="No work template is attached to this task."});
    var raw=await db.WorkTemplateFieldInstances.AsNoTracking().Where(x=>x.WorkTemplateInstanceId==instance.Id).OrderBy(x=>x.Sequence).ToListAsync();
    var fieldIds=raw.Select(x=>x.Id).ToList();
    var issueFieldIds=await db.Defects.AsNoTracking().Where(x=>x.ChecklistFieldInstanceId.HasValue&&fieldIds.Contains(x.ChecklistFieldInstanceId.Value)&&x.Disposition!="Closed").Select(x=>x.ChecklistFieldInstanceId!.Value).ToListAsync();
    var fields=raw.Select(x=>new{x.Id,x.WorkTemplateInstanceId,x.SourceTemplateFieldId,x.SectionName,x.Sequence,x.FieldCode,x.Label,x.FieldType,x.UnitCode,x.IsMandatory,x.MinValue,x.MaxValue,x.Options,x.FailureAction,x.SuggestedIssueCode,x.ActionCode,x.Specification,x.Severity,x.Value,x.Result,x.Remarks,x.EvidenceReference,x.ExecutedAt,x.ExecutedBy,issueRecorded=issueFieldIds.Contains(x.Id)});
    return Results.Ok(new{instance,fields});
});
app.MapPut("/api/tasks/{workItemId:guid}/paper-form/{fieldId:guid}", async(Guid workItemId,Guid fieldId,WorkTemplateFieldResultRequest r,AppDbContext db)=>
{
    var instance=await db.WorkTemplateInstances.FirstOrDefaultAsync(x=>x.WorkItemId==workItemId);if(instance is null)return Results.NotFound();
    var f=await db.WorkTemplateFieldInstances.FirstOrDefaultAsync(x=>x.Id==fieldId&&x.WorkTemplateInstanceId==instance.Id);if(f is null)return Results.NotFound();
    f.Value=r.Value;f.Result=r.Result;f.Remarks=r.Remarks;f.EvidenceReference=r.EvidenceReference;f.ExecutedBy=r.ExecutedBy;f.ExecutedAt=DateTime.UtcNow;
    if(instance.Status=="Not Started")instance.Status="In Progress";
    var failed=string.Equals(r.Result,"Fail",StringComparison.OrdinalIgnoreCase)||string.Equals(r.Result,"Not OK",StringComparison.OrdinalIgnoreCase);
    var issuePrompt=failed&&(string.Equals(f.FailureAction,"Create Defect",StringComparison.OrdinalIgnoreCase)||string.Equals(f.FailureAction,"Block Completion",StringComparison.OrdinalIgnoreCase));
    var issueRecorded=await db.Defects.AnyAsync(d=>d.ChecklistFieldInstanceId==f.Id&&d.Disposition!="Closed");
    await db.SaveChangesAsync();
    return Results.Ok(new{field=f,issuePrompt=issuePrompt&&!issueRecorded,suggestedIssueCode=f.SuggestedIssueCode,issueRecorded});
});

app.MapPost("/api/tasks/{workItemId:guid}/paper-form/{fieldId:guid}/issue", async(Guid workItemId,Guid fieldId,TechnicianIssueRequest r,AppDbContext db)=>
{
    var instance=await db.WorkTemplateInstances.FirstOrDefaultAsync(x=>x.WorkItemId==workItemId);if(instance is null)return Results.NotFound();
    var f=await db.WorkTemplateFieldInstances.FirstOrDefaultAsync(x=>x.Id==fieldId&&x.WorkTemplateInstanceId==instance.Id);if(f is null)return Results.NotFound();
    var existing=await db.Defects.FirstOrDefaultAsync(d=>d.ChecklistFieldInstanceId==fieldId&&d.Disposition!="Closed");
    if(existing is not null)
    {
        var existingTask=existing.CorrectiveWorkItemId.HasValue?await db.WorkItems.FindAsync(existing.CorrectiveWorkItemId.Value):null;
        return Results.Ok(new{message="Issue is already recorded.",issue=existing.Description,severity=existing.Severity,correctiveTask=existingTask?.Description??""});
    }
    var issue=await db.MasterOptions.AsNoTracking().FirstOrDefaultAsync(x=>x.Category=="ISSUE_TYPE"&&x.Code==r.FailureCode&&x.IsActive);
    if(issue is null)return Results.BadRequest(new{message="Select an issue from the list."});
    var wi=await db.WorkItems.FindAsync(workItemId);if(wi is null)return Results.NotFound();
    var job=await db.JobCards.FindAsync(wi.JobCardId);if(job is null)return Results.NotFound();
    var evt=await db.ServiceEvents.FindAsync(job.ServiceEventId);if(evt is null)return Results.NotFound();
    var severity=string.IsNullOrWhiteSpace(issue.Value)?"Minor":issue.Value;
    var correctiveAction=string.IsNullOrWhiteSpace(issue.Description)?"Assess and repair":issue.Description;
    var priority=severity.Equals("Critical",StringComparison.OrdinalIgnoreCase)?"P1":severity.Equals("Major",StringComparison.OrdinalIgnoreCase)?"P2":"P3";
    var corrective=new WorkItem{JobCardId=job.Id,TaskCode=$"TSK-{DateTime.UtcNow:yyyyMMddHHmmss}-C",WorkType="Corrective Repair",Description=correctiveAction,Status="Not Started",Priority=priority,DependencyTaskId=workItemId,RequiresQc=true,UpdatedAt=DateTime.UtcNow};
    db.WorkItems.Add(corrective);
    var defect=new Defect{VehicleId=evt.VehicleId,JobCardId=job.Id,WorkItemId=workItemId,ChecklistFieldInstanceId=fieldId,CorrectiveWorkItemId=corrective.Id,
        DefectNumber=$"DF-{DateTime.UtcNow:yyyy}-{(await db.Defects.CountAsync()+1):D6}",Category="Checklist Finding",Severity=severity,Description=issue.Name,Disposition="Open",FailureCode=issue.Code};
    db.Defects.Add(defect);
    if(!string.IsNullOrWhiteSpace(r.EvidenceReference))f.EvidenceReference=r.EvidenceReference;
    if(!string.IsNullOrWhiteSpace(r.Remarks))f.Remarks=r.Remarks;
    Audit(db,"CREATE","Issue",defect.Id,$"{issue.Name} from {instance.TemplateName} / {f.Label}");
    await db.SaveChangesAsync();
    return Results.Ok(new{message="Issue saved and corrective work created.",issue=issue.Name,severity,correctiveTask=correctiveAction});
});

app.MapPost("/api/tasks/{workItemId:guid}/paper-form/complete", async(Guid workItemId,AppDbContext db)=>
{
    var instance=await db.WorkTemplateInstances.FirstOrDefaultAsync(x=>x.WorkItemId==workItemId);if(instance is null)return Results.NotFound();
    var fields=await db.WorkTemplateFieldInstances.Where(x=>x.WorkTemplateInstanceId==instance.Id).ToListAsync();
    var missing=fields.Where(x=>x.IsMandatory&&(string.IsNullOrWhiteSpace(x.Value)||x.Result=="Pending")).Select(x=>x.Label).ToList();
    if(missing.Count>0)return Results.Conflict(new{message="Complete all mandatory checklist items.",missing});
    if(fields.Any(x=>string.Equals(x.FailureAction,"Block Completion",StringComparison.OrdinalIgnoreCase)
        &&(string.Equals(x.Result,"Fail",StringComparison.OrdinalIgnoreCase)||string.Equals(x.Result,"Not OK",StringComparison.OrdinalIgnoreCase))))
        return Results.Conflict(new{message="A failed checklist item is configured to block task completion."});
    instance.Status="Completed";instance.CompletedAt=DateTime.UtcNow;
    var task=await db.WorkItems.FindAsync(workItemId);if(task!=null){task.Status="Completed";task.UpdatedAt=DateTime.UtcNow;}
    await db.SaveChangesAsync();return Results.Ok(instance);
});


app.MapGet("/api/pm/task-library", async (string? search,string? section,string? action,string? severity,AppDbContext db) =>
{
    var q=db.MaintenanceTaskDefinitions.AsNoTracking().Where(x=>x.IsActive).AsQueryable();
    if(!string.IsNullOrWhiteSpace(search))q=q.Where(x=>x.TaskCode.Contains(search)||x.TaskName.Contains(search));
    if(!string.IsNullOrWhiteSpace(section))q=q.Where(x=>x.SectionName==section);
    if(!string.IsNullOrWhiteSpace(action))q=q.Where(x=>x.ActionCode==action);
    if(!string.IsNullOrWhiteSpace(severity))q=q.Where(x=>x.Severity==severity);
    return Results.Ok(await q.OrderBy(x=>x.SectionName).ThenBy(x=>x.SortOrder).ThenBy(x=>x.TaskCode).ToListAsync());
});
app.MapPost("/api/pm/task-library", async(MaintenanceTaskDefinition r,AppDbContext db)=>
{
    r.Id=Guid.NewGuid();r.TaskCode=r.TaskCode.Trim().ToUpperInvariant();
    if(string.IsNullOrWhiteSpace(r.TaskCode)||string.IsNullOrWhiteSpace(r.TaskName))return Results.BadRequest(new{message="Task code and task name are required."});
    if(await db.MaintenanceTaskDefinitions.AnyAsync(x=>x.TaskCode==r.TaskCode))return Results.Conflict(new{message="Task code already exists."});
    db.MaintenanceTaskDefinitions.Add(r);await db.SaveChangesAsync();return Results.Ok(r);
});
app.MapPut("/api/pm/task-library/{id:guid}", async(Guid id,MaintenanceTaskDefinition r,AppDbContext db)=>
{
    var x=await db.MaintenanceTaskDefinitions.FindAsync(id);if(x is null)return Results.NotFound();
    x.SectionName=r.SectionName;x.TaskName=r.TaskName;x.ActionCode=r.ActionCode;x.Specification=r.Specification;x.Severity=r.Severity;x.UnitCode=r.UnitCode;x.SuggestedIssueCode=r.SuggestedIssueCode;x.SortOrder=r.SortOrder;x.IsActive=r.IsActive;
    await db.SaveChangesAsync();return Results.Ok(x);
});
app.MapPost("/api/pm/task-library/bulk", async(List<MaintenanceTaskDefinition> rows,AppDbContext db)=>
{
    foreach(var r in rows)
    {
        var code=r.TaskCode.Trim().ToUpperInvariant();if(string.IsNullOrWhiteSpace(code)||string.IsNullOrWhiteSpace(r.TaskName))continue;
        var x=await db.MaintenanceTaskDefinitions.FirstOrDefaultAsync(t=>t.TaskCode==code);
        if(x is null){r.Id=Guid.NewGuid();r.TaskCode=code;db.MaintenanceTaskDefinitions.Add(r);}else{x.SectionName=r.SectionName;x.TaskName=r.TaskName;x.ActionCode=r.ActionCode;x.Specification=r.Specification;x.Severity=r.Severity;x.UnitCode=r.UnitCode;x.SuggestedIssueCode=r.SuggestedIssueCode;x.SortOrder=r.SortOrder;x.IsActive=true;}
    }
    await db.SaveChangesAsync();return Results.Ok(await db.MaintenanceTaskDefinitions.AsNoTracking().Where(x=>x.IsActive).OrderBy(x=>x.SectionName).ThenBy(x=>x.SortOrder).ToListAsync());
});
app.MapGet("/api/pm/replacement-rules",async(AppDbContext db)=>Results.Ok(await db.MaintenanceReplacementRules.AsNoTracking().OrderBy(x=>x.Platform).ThenBy(x=>x.SystemName).ThenBy(x=>x.ItemName).ToListAsync()));
app.MapPut("/api/pm/replacement-rules/{id:guid}",async(Guid id,MaintenanceReplacementRule r,AppDbContext db)=>{var x=await db.MaintenanceReplacementRules.FindAsync(id);if(x is null)return Results.NotFound();x.PartNumber=r.PartNumber;x.UsageInterval=r.UsageInterval;x.UsageUnit=r.UsageUnit;x.IntervalMonths=r.IntervalMonths;x.Quantity=r.Quantity;x.Notes=r.Notes;x.IsActive=r.IsActive;await db.SaveChangesAsync();return Results.Ok(x);});


// PM master validation is separate from vehicle due-date calculation.
static async Task<string?> ValidatePmProgramScopeAsync(AppDbContext db, MaintenanceProgram r)
{
    if (!r.VehicleModelMasterId.HasValue || r.VehicleModelMasterId == Guid.Empty)
        return "Select a Vehicle Model from the master. The program name does not assign a model.";
    var model = await db.VehicleModelMasters.AsNoTracking().FirstOrDefaultAsync(x => x.Id == r.VehicleModelMasterId.Value);
    if (model is null || !model.IsActive) return "Select an active vehicle model.";
    if (r.VehicleVariantMasterId.HasValue)
    {
        var variant = await db.VehicleVariantMasters.AsNoTracking().FirstOrDefaultAsync(x => x.Id == r.VehicleVariantMasterId.Value);
        if (variant is null || !variant.IsActive || variant.VehicleModelMasterId != model.Id)
            return "The selected active variant must belong to the selected model.";
    }
    return null;
}

static string? ValidatePmServiceLevels(List<PmServiceLevelRequest>? rows)
{
    if (rows is null || rows.Count == 0) return "Add at least one service level.";
    var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    foreach (var r in rows)
    {
        if (r is null) return "A service level is missing.";
        var code = (r.PlanCode ?? "").Trim();
        if (code.Length == 0 || string.IsNullOrWhiteSpace(r.Name)) return "Each service level needs a code and name.";
        if (!codes.Add(code)) return "Service level codes must be unique within this program.";
        if (r.Sequence < 0) return "Service level order cannot be negative.";
        if (!r.IsActive) continue;
        var usage = (r.UsageTriggerCode ?? "NONE").Trim().ToUpperInvariant();
        var expectedUnit = usage switch { "ODOMETER" => "KM", "OPERATING_HOURS" => "HOUR", "KWH" => "KWH", "NONE" => "", _ => null };
        if (expectedUnit is null) return "Usage basis must be Odometer, Operating Hours, Energy Used or Calendar only.";
        var hasUsage = r.UsageInterval.HasValue;
        if (usage == "NONE" && hasUsage) return "Calendar-only levels cannot have a usage interval.";
        if (hasUsage && (r.UsageInterval <= 0 || !string.Equals(r.UsageUnit, expectedUnit, StringComparison.OrdinalIgnoreCase)))
            return "Usage interval must be positive and its unit must match the usage basis.";
        var calendar = r.CalendarInterval ?? r.CalendarMonths;
        var unit = (r.CalendarUnit ?? "MONTH").Trim().ToUpperInvariant();
        if (calendar.HasValue && (calendar.Value <= 0 || !new[] { "DAY", "MONTH", "YEAR" }.Contains(unit)))
            return "Calendar interval must be a positive whole number in days, months or years.";
        if (!hasUsage && !calendar.HasValue) return "An active service level needs a usage or calendar interval.";
    }
    return null;
}

// Called only inside the service-ladder or import transaction. Preserves existing plan IDs.
static async Task<string?> SavePmServiceLevelsAsync(AppDbContext db, Guid programId, List<PmServiceLevelRequest> rows)
{
    foreach (var r in rows.OrderBy(x => x.Sequence))
    {
        var code = r.PlanCode.Trim().ToUpperInvariant();
        MaintenancePlan? plan;
        if (r.Id.HasValue && r.Id.Value != Guid.Empty)
        {
            plan = await db.MaintenancePlans.FirstOrDefaultAsync(x => x.Id == r.Id.Value && x.MaintenanceProgramId == programId);
            if (plan is null) return "A service level does not belong to the selected program. Refresh and retry.";
            if (!string.Equals(plan.PlanCode, code, StringComparison.OrdinalIgnoreCase))
                return "The code of a saved service level cannot be changed. Its existing references are retained.";
        }
        else
        {
            plan = await db.MaintenancePlans.FirstOrDefaultAsync(x => x.MaintenanceProgramId == programId && x.PlanCode == code);
            if (plan is null)
            {
                plan = new MaintenancePlan { MaintenanceProgramId = programId, PlanCode = code };
                db.MaintenancePlans.Add(plan);
            }
        }
        var triggers = await db.MaintenancePlanTriggers.Where(x => x.MaintenancePlanId == plan.Id).ToListAsync();
        if (triggers.Count(x => x.IsActive && x.TriggerCode != "TIME") > 1 || triggers.Count(x => x.IsActive && x.TriggerCode == "TIME") > 1)
            return "This legacy level contains multiple usage or calendar conditions. Nothing was changed. Review it separately before editing.";
        plan.Name = r.Name.Trim(); plan.Sequence = r.Sequence; plan.IsActive = r.IsActive;
        plan.RecurrenceBasis = "ScheduledDue";
        if (!r.IsActive) { await db.SaveChangesAsync(); continue; }
        var usageCode = (r.UsageTriggerCode ?? "NONE").ToUpperInvariant();
        var calendar = r.CalendarInterval ?? r.CalendarMonths;
        var calendarUnit = (r.CalendarUnit ?? "MONTH").ToUpperInvariant();
        foreach (var trigger in triggers)
        {
            if (trigger.TriggerCode == "TIME") trigger.IsActive = calendar.HasValue;
            else trigger.IsActive = r.UsageInterval.HasValue && trigger.TriggerCode == usageCode;
        }
        if (r.UsageInterval.HasValue)
        {
            var trigger = triggers.FirstOrDefault(x => x.TriggerCode == usageCode);
            if (trigger is null) { trigger = new MaintenancePlanTrigger { MaintenancePlanId = plan.Id, TriggerCode = usageCode }; db.MaintenancePlanTriggers.Add(trigger); }
            var interval = r.UsageInterval.Value;
            if (trigger.IntervalValue != interval || trigger.UnitCode != r.UsageUnit) trigger.WarningValue = 0;
            trigger.IntervalValue = interval; trigger.InitialDueValue = interval;
            trigger.UnitCode = r.UsageUnit.ToUpperInvariant(); trigger.IsActive = true;
        }
        if (calendar.HasValue)
        {
            var time = triggers.FirstOrDefault(x => x.TriggerCode == "TIME");
            if (time is null) { time = new MaintenancePlanTrigger { MaintenancePlanId = plan.Id, TriggerCode = "TIME" }; db.MaintenancePlanTriggers.Add(time); }
            if (time.IntervalValue != calendar.Value || time.UnitCode != calendarUnit) time.WarningValue = 0;
            time.IntervalValue = calendar.Value; time.InitialDueValue = calendar.Value;
            time.UnitCode = calendarUnit; time.IsActive = true;
        }
        await db.SaveChangesAsync();
    }
    return null;
}

app.MapGet("/api/pm/programs/{id:guid}/service-matrix", async(Guid id,AppDbContext db)=>
{
    var program=await db.MaintenancePrograms.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==id);if(program is null)return Results.NotFound();
    var plans=await db.MaintenancePlans.AsNoTracking().Where(x=>x.MaintenanceProgramId==id).OrderBy(x=>x.Sequence).ToListAsync();
    var planIds=plans.Select(x=>x.Id).ToList();
    var triggers=await db.MaintenancePlanTriggers.AsNoTracking().Where(x=>planIds.Contains(x.MaintenancePlanId)&&x.IsActive).ToListAsync();
    var levels=plans.Select(p=>new{p.Id,p.PlanCode,p.Name,p.Sequence,p.IsActive,triggers=triggers.Where(t=>t.MaintenancePlanId==p.Id).Select(t=>new{t.Id,t.TriggerCode,t.IntervalValue,t.UnitCode,t.WarningValue,t.ToleranceValue})});
    var taskRows=await db.MaintenanceTaskDefinitions.AsNoTracking().Where(x=>x.IsActive && x.MaintenanceProgramId==id).OrderBy(x=>x.SectionName).ThenBy(x=>x.SortOrder).ThenBy(x=>x.TaskCode).ToListAsync();
    if(taskRows.Count==0) taskRows=await db.MaintenanceTaskDefinitions.AsNoTracking().Where(x=>x.IsActive && x.MaintenanceProgramId==null).OrderBy(x=>x.SectionName).ThenBy(x=>x.SortOrder).ThenBy(x=>x.TaskCode).ToListAsync();
    var assignments=await db.MaintenancePlanMatrixItems.AsNoTracking().Where(x=>planIds.Contains(x.MaintenancePlanId)).ToListAsync();
    return Results.Ok(new{program,levels,tasks=taskRows,assignments});
});
app.MapPut("/api/pm/programs/{id:guid}/service-ladder", async(Guid id,List<PmServiceLevelRequest> rows,AppDbContext db)=>
{
    if (!await db.MaintenancePrograms.AnyAsync(x => x.Id == id)) return Results.NotFound();
    var problem = ValidatePmServiceLevels(rows);
    if (problem is not null) return Results.BadRequest(new { message = problem });
    await using var transaction = await db.Database.BeginTransactionAsync();
    var error = await SavePmServiceLevelsAsync(db, id, rows);
    if (error is not null) return Results.Conflict(new { message = error });
    Audit(db, "UPDATE", "PMServiceLevels", id, $"Updated {rows.Count} service levels; existing plan IDs retained.");
    await db.SaveChangesAsync();
    await transaction.CommitAsync();
    return Results.Ok(new { saved = rows.Count });
});
app.MapPut("/api/pm/programs/{id:guid}/task-matrix", async(Guid id,List<MaintenancePlanMatrixItem> rows,AppDbContext db)=>
{
    var planIds=await db.MaintenancePlans.Where(x=>x.MaintenanceProgramId==id).Select(x=>x.Id).ToListAsync();if(planIds.Count==0)return Results.BadRequest(new{message="Create the service ladder first."});
    var old=await db.MaintenancePlanMatrixItems.Where(x=>planIds.Contains(x.MaintenancePlanId)).ToListAsync();db.MaintenancePlanMatrixItems.RemoveRange(old);
    foreach(var r in rows.Where(x=>planIds.Contains(x.MaintenancePlanId)&&!string.IsNullOrWhiteSpace(x.ActionCode)).GroupBy(x=>new{x.MaintenancePlanId,x.MaintenanceTaskDefinitionId}).Select(g=>g.First()))db.MaintenancePlanMatrixItems.Add(new MaintenancePlanMatrixItem{MaintenancePlanId=r.MaintenancePlanId,MaintenanceTaskDefinitionId=r.MaintenanceTaskDefinitionId,ActionCode=r.ActionCode.Trim().ToUpperInvariant(),Sequence=r.Sequence,IsMandatory=r.IsMandatory});
    await db.SaveChangesAsync();return Results.Ok(new{saved=rows.Count});
});
app.MapPost("/api/pm/programs/{id:guid}/import-matrix",async(Guid id,PmProgramMatrixImportRequest r,AppDbContext db)=>
{
    if (!await db.MaintenancePrograms.AnyAsync(x => x.Id == id)) return Results.NotFound();
    var problem = ValidatePmServiceLevels(r.Levels);
    if (problem is not null) return Results.BadRequest(new { message = problem });
    if (r.Tasks is null || r.Assignments is null || r.Tasks.Any(x => x is null || string.IsNullOrWhiteSpace(x.TaskCode) || string.IsNullOrWhiteSpace(x.TaskName)))
        return Results.BadRequest(new { message = "The import contains missing task definitions." });
    if (r.Tasks.GroupBy(x => new { Code=x.TaskCode.Trim().ToUpperInvariant(), Name=x.TaskName.Trim() }).Any(g => g.Count() > 1))
        return Results.BadRequest(new { message = "The selected worksheet contains duplicate task rows." });
    await using var transaction = await db.Database.BeginTransactionAsync();
    var ladderError = await SavePmServiceLevelsAsync(db, id, r.Levels);
    if (ladderError is not null) return Results.Conflict(new { message = ladderError });
    foreach(var t in r.Tasks){var code=t.TaskCode.Trim().ToUpperInvariant();var name=t.TaskName.Trim();var x=await db.MaintenanceTaskDefinitions.FirstOrDefaultAsync(z=>z.MaintenanceProgramId==id&&z.TaskCode==code&&z.TaskName==name);if(x is null){t.Id=Guid.NewGuid();t.MaintenanceProgramId=id;t.TaskCode=code;t.TaskName=name;db.MaintenanceTaskDefinitions.Add(t);}else{x.SectionName=t.SectionName;x.ActionCode=t.ActionCode;x.Specification=t.Specification;x.Severity=t.Severity;x.UnitCode=t.UnitCode;x.SuggestedIssueCode=t.SuggestedIssueCode;x.SortOrder=t.SortOrder;x.IsActive=true;}}
    await db.SaveChangesAsync();
    var plans=await db.MaintenancePlans.Where(x=>x.MaintenanceProgramId==id).ToListAsync();var defs=await db.MaintenanceTaskDefinitions.Where(x=>x.MaintenanceProgramId==id).ToListAsync();var planIds=plans.Select(x=>x.Id).ToList();var old=await db.MaintenancePlanMatrixItems.Where(x=>planIds.Contains(x.MaintenancePlanId)).ToListAsync();db.MaintenancePlanMatrixItems.RemoveRange(old);
    foreach(var a in r.Assignments){var p=plans.FirstOrDefault(x=>x.PlanCode==a.PlanCode);var t=defs.FirstOrDefault(x=>x.TaskCode==a.TaskCode);if(p!=null&&t!=null&&!string.IsNullOrWhiteSpace(a.ActionCode))db.MaintenancePlanMatrixItems.Add(new MaintenancePlanMatrixItem{MaintenancePlanId=p.Id,MaintenanceTaskDefinitionId=t.Id,ActionCode=a.ActionCode.Trim().ToUpperInvariant(),Sequence=a.Sequence,IsMandatory=a.IsMandatory});}
    await db.SaveChangesAsync();await transaction.CommitAsync();return Results.Ok(new{levels=plans.Count,tasks=r.Tasks.Count,assignments=r.Assignments.Count});
});

app.MapGet("/api/pm/programs", async (AppDbContext db) => Results.Ok(await db.MaintenancePrograms.AsNoTracking().OrderBy(x=>x.Name).ToListAsync()));
app.MapPost("/api/pm/programs", async (MaintenanceProgram r,AppDbContext db)=>
{
    r.ProgramCode = (r.ProgramCode ?? "").Trim().ToUpperInvariant();
    r.Name = (r.Name ?? "").Trim(); r.Description = (r.Description ?? "").Trim();
    if (r.ProgramCode.Length == 0 || r.ProgramCode.Length > 64 || r.Name.Length == 0 || r.Name.Length > 200)
        return Results.BadRequest(new { message = "Enter a Program Code (up to 64 characters) and Program Name (up to 200 characters)." });
    var scopeError = await ValidatePmProgramScopeAsync(db, r);
    if (scopeError is not null) return Results.BadRequest(new { message = scopeError });
    if (r.EffectiveTo.HasValue && r.EffectiveTo.Value < r.EffectiveFrom)
        return Results.BadRequest(new { message = "Effective end must not be earlier than effective start." });
    if (await db.MaintenancePrograms.AnyAsync(x => x.ProgramCode == r.ProgramCode))
        return Results.Conflict(new { message = "Program code already exists. Select the existing program and use Edit Program." });
    r.Id = Guid.NewGuid(); db.MaintenancePrograms.Add(r);
    Audit(db, "CREATE", "MaintenanceProgram", r.Id, $"Program {r.ProgramCode}: {r.Name}; model {r.VehicleModelMasterId}; variant {r.VehicleVariantMasterId}");
    try { await db.SaveChangesAsync(); }
    catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg && pg.SqlState == "23505")
    { return Results.Conflict(new { message = "Program code already exists. Refresh and use Edit Program." }); }
    return Results.Ok(r);
});
app.MapPut("/api/pm/programs/{id:guid}", async (Guid id,MaintenanceProgram r,AppDbContext db)=>
{
    var x = await db.MaintenancePrograms.FindAsync(id);
    if (x is null) return Results.NotFound();
    // Immutable identity: never create a replacement or assign x.Id / x.ProgramCode here.
    if (r.Id != id || !string.Equals(r.ProgramCode, x.ProgramCode, StringComparison.Ordinal))
        return Results.BadRequest(new { message = "Program identity cannot be changed. Reload the existing program and edit its details." });
    var name = (r.Name ?? "").Trim();
    if (name.Length == 0 || name.Length > 200) return Results.BadRequest(new { message = "Enter a Program Name up to 200 characters." });
    if (r.EffectiveTo.HasValue && r.EffectiveTo.Value < r.EffectiveFrom)
        return Results.BadRequest(new { message = "Effective end must not be earlier than effective start." });
    var changedScope = x.VehicleModelMasterId != r.VehicleModelMasterId || x.VehicleVariantMasterId != r.VehicleVariantMasterId;
    if (changedScope)
    {
        var hasVehicles = await db.Vehicles.AnyAsync(v => v.MaintenanceProgramId == id);
        var hasPmHistory = await (from obligation in db.PmObligations
                                  join plan in db.MaintenancePlans on obligation.MaintenancePlanId equals (Guid?)plan.Id
                                  where plan.MaintenanceProgramId == id select obligation.Id).AnyAsync();
        if (hasVehicles || hasPmHistory)
            return Results.Conflict(new { message = "This program is already assigned to vehicles or used in PM history. Its model/variant scope cannot be changed here. Keep that scope to rename the program; create a new program for different applicability." });
        var error = await ValidatePmProgramScopeAsync(db, r);
        if (error is not null) return Results.BadRequest(new { message = error });
    }
    var previous = $"Name={x.Name}; Model={x.VehicleModelMasterId}; Variant={x.VehicleVariantMasterId}; Active={x.IsActive}";
    x.Name = name; x.Description = (r.Description ?? "").Trim();
    x.VehicleModelMasterId = r.VehicleModelMasterId; x.VehicleVariantMasterId = r.VehicleVariantMasterId;
    x.EffectiveFrom = r.EffectiveFrom; x.EffectiveTo = r.EffectiveTo; x.IsActive = r.IsActive;
    Audit(db, "UPDATE", "MaintenanceProgram", x.Id,
        $"Before: {previous}. After: Name={x.Name}; Model={x.VehicleModelMasterId}; Variant={x.VehicleVariantMasterId}; Active={x.IsActive}. Levels and mappings retained.");
    // This endpoint intentionally does not touch plans, triggers, matrix mappings or vehicle assignments.
    await db.SaveChangesAsync(); return Results.Ok(x);
});

app.MapGet("/api/pm/plans", async (AppDbContext db) =>
{
    var plans=await db.MaintenancePlans.AsNoTracking().OrderBy(x=>x.Sequence).ThenBy(x=>x.PlanCode).ToListAsync();var triggers=await db.MaintenancePlanTriggers.AsNoTracking().ToListAsync();var tasks=await db.MaintenancePlanTasks.AsNoTracking().ToListAsync();var templates=await db.MaintenancePlanTemplates.AsNoTracking().ToListAsync();
    return Results.Ok(plans.Select(x=>new{x.Id,x.MaintenanceProgramId,x.PlanCode,x.Name,x.Description,x.RecurrenceBasis,x.Sequence,x.IsActive,triggers=triggers.Where(t=>t.MaintenancePlanId==x.Id),taskCount=tasks.Count(t=>t.MaintenancePlanId==x.Id),templateCount=templates.Count(t=>t.MaintenancePlanId==x.Id)}));
});
app.MapPost("/api/pm/plans",async(MaintenancePlan r,AppDbContext db)=>{r.Id=Guid.NewGuid();r.PlanCode=r.PlanCode.Trim().ToUpperInvariant();r.RecurrenceBasis="ScheduledDue";if(!await db.MaintenancePrograms.AnyAsync(x=>x.Id==r.MaintenanceProgramId))return Results.BadRequest(new{message="Maintenance program not found."});if(await db.MaintenancePlans.AnyAsync(x=>x.MaintenanceProgramId==r.MaintenanceProgramId&&x.PlanCode==r.PlanCode))return Results.Conflict(new{message="Plan code already exists in this program."});db.MaintenancePlans.Add(r);await db.SaveChangesAsync();return Results.Ok(r);});
app.MapPost("/api/pm/plans/{id:guid}/trigger",async(Guid id,MaintenancePlanTrigger r,AppDbContext db)=>{if(!await db.MaintenancePlans.AnyAsync(x=>x.Id==id))return Results.NotFound();var code=r.TriggerCode.Trim().ToUpperInvariant();var x=await db.MaintenancePlanTriggers.FirstOrDefaultAsync(t=>t.MaintenancePlanId==id&&t.TriggerCode==code);if(x is null){x=new MaintenancePlanTrigger{MaintenancePlanId=id,TriggerCode=code};db.MaintenancePlanTriggers.Add(x);}x.IntervalValue=r.IntervalValue;x.InitialDueValue=r.InitialDueValue;x.UnitCode=r.UnitCode;x.WarningValue=r.WarningValue;x.ToleranceValue=r.ToleranceValue;x.IsActive=r.IsActive;await db.SaveChangesAsync();return Results.Ok(x);});
app.MapPost("/api/pm/plans/{id:guid}/task",async(Guid id,MaintenancePlanTask r,AppDbContext db)=>{if(!await db.MaintenancePlans.AnyAsync(x=>x.Id==id)||!await db.ServiceTaskMasters.AnyAsync(x=>x.Id==r.ServiceTaskMasterId))return Results.BadRequest();var x=await db.MaintenancePlanTasks.FirstOrDefaultAsync(t=>t.MaintenancePlanId==id&&t.ServiceTaskMasterId==r.ServiceTaskMasterId);if(x is null){x=new MaintenancePlanTask{MaintenancePlanId=id,ServiceTaskMasterId=r.ServiceTaskMasterId};db.MaintenancePlanTasks.Add(x);}x.Sequence=r.Sequence;x.IsMandatory=r.IsMandatory;await db.SaveChangesAsync();return Results.Ok(x);});
app.MapDelete("/api/pm/plans/{planId:guid}/task/{mappingId:guid}",async(Guid planId,Guid mappingId,AppDbContext db)=>{var x=await db.MaintenancePlanTasks.FirstOrDefaultAsync(t=>t.Id==mappingId&&t.MaintenancePlanId==planId);if(x is null)return Results.NotFound();db.MaintenancePlanTasks.Remove(x);await db.SaveChangesAsync();return Results.NoContent();});
app.MapGet("/api/pm/plans/{id:guid}/tasks",async(Guid id,AppDbContext db)=>
{
    var rows=await(from m in db.MaintenancePlanTasks.AsNoTracking() join t in db.ServiceTaskMasters.AsNoTracking() on m.ServiceTaskMasterId equals t.Id where m.MaintenancePlanId==id orderby m.Sequence select new{m.Id,m.Sequence,m.IsMandatory,m.ServiceTaskMasterId,t.TaskCode,t.Name,t.StandardHours,t.RequiredSkillCode,t.RequiresHvAuthorization,t.RequiresQc,t.ChecklistCode}).ToListAsync();return Results.Ok(rows);
});

app.MapGet("/api/pm/enrollments",async(AppDbContext db)=>Results.Ok(await db.Vehicles.AsNoTracking().OrderBy(x=>x.RegistrationNumber).Select(v=>new{v.Id,v.RegistrationNumber,v.Vin,v.Model,v.Variant,v.ImageUrl,v.PurchaseDate,v.CommissioningDate,v.OdometerKm,v.OperatingHours,v.EnergyKwh,v.Status,v.MaintenanceProgramId,v.DepotCode,v.ServiceCentreCode,v.CustomerCode}).ToListAsync()));
app.MapPost("/api/pm/enroll",async(VehicleEnrollmentRequest r,AppDbContext db)=>
{
    if(await db.Vehicles.AnyAsync(x=>x.Vin==r.Vin||x.RegistrationNumber==r.RegistrationNumber))return Results.Conflict(new{message="VIN or registration number already exists."});
    var model=await db.VehicleModelMasters.FindAsync(r.ModelMasterId);if(model is null)return Results.BadRequest(new{message="Vehicle model is required."});
    VehicleVariantMaster? variant=null;if(r.VariantMasterId.HasValue){variant=await db.VehicleVariantMasters.FindAsync(r.VariantMasterId.Value);if(variant is null||variant.VehicleModelMasterId!=model.Id)return Results.BadRequest(new{message="Selected variant does not belong to the model."});}
    if (r.MaintenanceProgramId.HasValue)
    {
        var program = await db.MaintenancePrograms.AsNoTracking().FirstOrDefaultAsync(p => p.Id == r.MaintenanceProgramId.Value);
        if (program is null || !program.IsActive || program.VehicleModelMasterId != model.Id
            || (program.VehicleVariantMasterId.HasValue && program.VehicleVariantMasterId != variant?.Id))
            return Results.BadRequest(new { message = "Select an active PM program applicable to this vehicle model and variant." });
    }
    var v=new Vehicle{Vin=r.Vin.Trim(),RegistrationNumber=r.RegistrationNumber.Trim().ToUpperInvariant(),Model=model.Name,Variant=variant?.Name??"",VehicleTypeCode=model.VehicleTypeCode,ManufacturerCode=model.ManufacturerCode,ModelMasterId=model.Id,VariantMasterId=variant?.Id,ImageUrl=string.IsNullOrWhiteSpace(r.ImageUrl)?(variant?.ImageUrl??model.ImageUrl):r.ImageUrl,MotorNumber=r.MotorNumber,PurchaseDate=r.PurchaseDate,PurchaseCost=r.PurchaseCost,InvoiceNumber=r.InvoiceNumber,DealerName=r.DealerName,CommissioningDate=r.CommissioningDate,RegistrationDate=r.RegistrationDate,RegistrationExpiry=r.RegistrationExpiry,InsuranceNumber=r.InsuranceNumber,InsuranceStartDate=r.InsuranceStartDate,InsuranceExpiryDate=r.InsuranceExpiryDate,WarrantyStartDate=r.WarrantyStartDate,WarrantyExpiryDate=r.WarrantyExpiryDate,BatteryWarrantyStartDate=r.BatteryWarrantyStartDate,BatteryWarrantyExpiryDate=r.BatteryWarrantyExpiryDate,OdometerKm=r.OdometerKm,OperatingHours=r.OperatingHours,EnergyKwh=r.EnergyKwh,BatterySoc=r.BatterySoc,DepotCode=r.DepotCode,ServiceCentreCode=r.ServiceCentreCode,CustomerCode=r.CustomerCode,OwnershipTypeCode=r.OwnershipTypeCode,MaintenanceProgramId=r.MaintenanceProgramId,Remarks=r.Remarks,Status="Available",IsActive=true};
    db.Vehicles.Add(v);Audit(db,"ENROLL","Vehicle",v.Id,v.RegistrationNumber,r.CreatedBy);await db.SaveChangesAsync();await EnsurePmObligationsAsync(db,v);return Results.Created($"/api/vehicles/{v.Id}",v);
});

app.MapPost("/api/pm/recalculate",async(AppDbContext db)=>{var vs=await db.Vehicles.Where(x=>x.IsActive).ToListAsync();foreach(var v in vs)await EnsurePmObligationsAsync(db,v);return Results.Ok(new{status="ok",vehicles=vs.Count});});
app.MapGet("/api/pm/due-board",async(AppDbContext db)=>
{
    var obs=await db.PmObligations.Where(x=>x.Status!="Completed").OrderBy(x=>x.DueDate).ThenBy(x=>x.DueReading).ToListAsync();var vehicles=await db.Vehicles.AsNoTracking().ToDictionaryAsync(x=>x.Id);var plans=await db.MaintenancePlans.AsNoTracking().ToDictionaryAsync(x=>x.Id);var triggers=await db.MaintenancePlanTriggers.AsNoTracking().Where(x=>x.IsActive).ToListAsync();var result=new List<object>();
    foreach(var o in obs){if(!vehicles.TryGetValue(o.VehicleId,out var v))continue;MaintenancePlan? p=null;if(o.MaintenancePlanId.HasValue)plans.TryGetValue(o.MaintenancePlanId.Value,out p);var ts=o.MaintenancePlanId.HasValue?triggers.Where(x=>x.MaintenancePlanId==o.MaintenancePlanId.Value).ToList():new List<MaintenancePlanTrigger>();var pieces=new List<string>();var duePieces=new List<string>();var remPieces=new List<string>();var overdue=false;var due=false;var soon=false;
        foreach(var t in ts){switch(t.TriggerCode.ToUpperInvariant()){case "ODOMETER":if(o.DueReading.HasValue){var r=o.DueReading.Value-v.OdometerKm;pieces.Add("Odometer");duePieces.Add($"{o.DueReading:0} km");remPieces.Add($"{r:0} km");overdue|=r<0;due|=r==0;soon|=r>0&&r<=t.WarningValue;}break;case "OPERATING_HOURS":if(o.DueOperatingHours.HasValue){var r=o.DueOperatingHours.Value-v.OperatingHours;pieces.Add("Hours");duePieces.Add($"{o.DueOperatingHours:0} hr");remPieces.Add($"{r:0} hr");overdue|=r<0;due|=r==0;soon|=r>0&&r<=t.WarningValue;}break;case "KWH":if(o.DueEnergyKwh.HasValue){var r=o.DueEnergyKwh.Value-v.EnergyKwh;pieces.Add("kWh");duePieces.Add($"{o.DueEnergyKwh:0} kWh");remPieces.Add($"{r:0} kWh");overdue|=r<0;due|=r==0;soon|=r>0&&r<=t.WarningValue;}break;case "TIME":if(o.DueDate.HasValue){var r=(o.DueDate.Value.Date-DateTime.UtcNow.Date).Days;pieces.Add("Time");duePieces.Add(o.DueDate.Value.ToString("dd MMM yyyy"));remPieces.Add($"{r} days");overdue|=r<0;due|=r==0;soon|=r>0&&r<=t.WarningValue;}break;}}
        var calc=overdue?"Overdue":due?"Due":soon?"Due Soon":"Upcoming";if(o.Status is "Planned" or "In Service")calc=o.Status;if(o.Status!=calc){o.Status=calc;}
        result.Add(new{o.Id,o.VehicleId,vehicle=v.RegistrationNumber,model=v.Model,variant=v.Variant,imageUrl=v.ImageUrl,plan=p?.PlanCode??o.PlanCode,planName=p?.Name??o.PlanCode,trigger=pieces.Count>0?string.Join(" / ",pieces):o.TriggerType,current=$"{v.OdometerKm:0} km · {v.OperatingHours:0} hr · {v.EnergyKwh:0} kWh",due=string.Join(" · ",duePieces),remaining=string.Join(" · ",remPieces),status=calc});}
    await db.SaveChangesAsync();return Results.Ok(result);
});
app.MapGet("/api/pm/history",async(AppDbContext db)=>
{
    var rows=await(from o in db.PmObligations.AsNoTracking() join v in db.Vehicles.AsNoTracking() on o.VehicleId equals v.Id where o.Status=="Completed" orderby o.CompletedAt descending select new{o.Id,vehicle=v.RegistrationNumber,v.Model,o.PlanCode,o.TriggerType,o.CompletedAt,o.DueDate,o.DueReading,o.DueOperatingHours,o.DueEnergyKwh}).ToListAsync();return Results.Ok(rows);
});
app.MapPost("/api/pm/obligations/{id:guid}/create-request",async(Guid id,AppDbContext db)=>
{
    var o=await db.PmObligations.FindAsync(id);if(o is null)return Results.NotFound();var v=await db.Vehicles.FindAsync(o.VehicleId);if(v is null)return Results.BadRequest();if(await db.MaintenanceRequests.AnyAsync(x=>x.SourceType=="PM"&&x.SourceReference==o.Id.ToString()&&x.Status!="Closed"))return Results.Conflict(new{message="A maintenance request already exists for this PM obligation."});
    var mr=new MaintenanceRequest{RequestNumber=$"MR-{DateTime.UtcNow:yyyy}-{(await db.MaintenanceRequests.CountAsync()+1):D6}",VehicleId=v.Id,SourceType="PM",SourceReference=o.Id.ToString(),RequestType="Preventive Maintenance",Priority=o.Status=="Overdue"?"P2":"P3",Description=$"{o.PlanCode} preventive maintenance",Status="Open",RequestedBy="PM Engine",RequestedAt=DateTime.UtcNow};o.Status="Planned";db.MaintenanceRequests.Add(mr);Audit(db,"CREATE","MaintenanceRequest",mr.Id,$"{mr.RequestNumber} from {o.PlanCode}");await db.SaveChangesAsync();return Results.Ok(mr);
});

// Manual obligation generation remains for authorized exception use only.
app.MapPost("/api/pm/obligations/generate", async (PmRequest r, AppDbContext db) =>
{
    var v=await db.Vehicles.FindAsync(r.VehicleId);if(v is null)return Results.BadRequest(new{message="Vehicle not found."});
    var p=new PmObligation{VehicleId=v.Id,PlanCode=r.PlanCode,TriggerType=r.TriggerType,DueDate=r.DueDate,DueReading=r.DueReading,Status="Upcoming"};db.PmObligations.Add(p);Audit(db,"GENERATE","PmObligation",p.Id,$"{v.RegistrationNumber} {p.PlanCode}");await db.SaveChangesAsync();return Results.Created($"/api/pm/obligations/{p.Id}",p);
});

app.MapGet("/api/appointments", async (AppDbContext db) =>
{
    var rows = await (from a in db.Appointments.AsNoTracking()
                      join v in db.Vehicles.AsNoTracking() on a.VehicleId equals v.Id
                      orderby a.StartAt descending
                      select new { a.Id,a.AppointmentNumber,a.VehicleId,vehicle=v.RegistrationNumber,a.SourceType,a.SourceReference,
                          a.StartAt,a.EndAt,a.ServiceCentre,a.Bay,a.TechnicianId,a.Technician,a.AppointmentType,a.Priority,a.Reason,
                          a.PlannedHours,a.Status,a.PmObligationId,a.CreatedBy,a.CreatedAt }).ToListAsync();
    return Results.Ok(rows);
});

app.MapGet("/api/capacity", async (DateTime? date, string? serviceCentre, AppDbContext db) =>
{
    var d=(date ?? DateTime.UtcNow).Date; var next=d.AddDays(1);
    var bayQuery=db.ServiceBays.AsNoTracking().Where(x=>x.IsActive);
    if(!string.IsNullOrWhiteSpace(serviceCentre)) bayQuery=bayQuery.Where(x=>x.ServiceCentre==serviceCentre);
    var bays=await bayQuery.OrderBy(x=>x.BayCode).ToListAsync();
    var techs=await db.Technicians.AsNoTracking().Where(x=>x.IsActive).OrderBy(x=>x.Name).ToListAsync();
    var appts=await db.Appointments.AsNoTracking().Where(x=>x.StartAt>=d && x.StartAt<next && x.Status!="Cancelled" && x.Status!="No-show").ToListAsync();
    return Results.Ok(new {
        date=d,
        bays=bays.Select(b=>new {b.Id,b.BayCode,b.BayType,b.ServiceCentre,bookedHours=appts.Where(a=>a.Bay==b.BayCode).Sum(a=>a.PlannedHours)}),
        technicians=techs.Select(t=>new {t.Id,t.EmployeeCode,t.Name,t.SkillCodes,t.HvAuthorized,t.HvAuthorizationValidUntil,
            bookedHours=appts.Where(a=>a.TechnicianId==t.Id).Sum(a=>a.PlannedHours)})
    });
});

app.MapPost("/api/appointments", async (AppointmentRequest r, AppDbContext db) =>
{
    var vehicle=await db.Vehicles.FindAsync(r.VehicleId);
    if(vehicle is null) return Results.BadRequest(new {message="Vehicle not found."});
    if(r.StartAt==default || r.PlannedHours<=0) return Results.BadRequest(new {message="Start date/time and planned hours are required."});

    var end=r.StartAt.AddHours((double)r.PlannedHours);
    var bayClash=await db.Appointments.AnyAsync(x=>x.Bay==r.Bay && x.Status!="Cancelled" && x.Status!="No-show" &&
        x.StartAt<end && (x.EndAt ?? x.StartAt.AddHours((double)x.PlannedHours))>r.StartAt);
    if(bayClash) return Results.Conflict(new {message=$"{r.Bay} is already booked for the selected time."});

    Technician? tech=null;
    if(r.TechnicianId.HasValue)
    {
        tech=await db.Technicians.FindAsync(r.TechnicianId.Value);
        if(tech is null || !tech.IsActive) return Results.BadRequest(new {message="Selected technician is not active."});
        var techClash=await db.Appointments.AnyAsync(x=>x.TechnicianId==r.TechnicianId && x.Status!="Cancelled" && x.Status!="No-show" &&
            x.StartAt<end && (x.EndAt ?? x.StartAt.AddHours((double)x.PlannedHours))>r.StartAt);
        if(techClash) return Results.Conflict(new {message=$"{tech.Name} is already assigned during the selected time."});
        if(r.Bay.StartsWith("HV",StringComparison.OrdinalIgnoreCase) && (!tech.HvAuthorized || tech.HvAuthorizationValidUntil<DateTime.UtcNow))
            return Results.Conflict(new {message="An active HV-authorized technician is required for an HV bay."});
    }

    var seq=await db.Appointments.CountAsync()+1;
    var a=new Appointment{
        AppointmentNumber=$"APT-{DateTime.UtcNow:yyyyMMdd}-{seq:D5}",VehicleId=r.VehicleId,PmObligationId=r.PmObligationId,
        SourceType=r.SourceType,SourceReference=r.SourceReference,StartAt=r.StartAt,EndAt=end,ServiceCentre=r.ServiceCentre,
        Bay=r.Bay,TechnicianId=r.TechnicianId,Technician=tech?.Name ?? "",AppointmentType=r.AppointmentType,Priority=r.Priority,
        Reason=r.Reason,PlannedHours=r.PlannedHours,Status="Requested",CreatedBy=r.CreatedBy,CreatedAt=DateTime.UtcNow
    };
    db.Appointments.Add(a); Audit(db,"CREATE","Appointment",a.Id,$"{a.AppointmentNumber} {vehicle.RegistrationNumber}");
    await db.SaveChangesAsync(); return Results.Created($"/api/appointments/{a.Id}",a);
});

app.MapPut("/api/appointments/{id:guid}/status", async (Guid id, StatusRequest r, AppDbContext db) =>
{
    var a=await db.Appointments.FindAsync(id); if(a is null)return Results.NotFound();
    var allowed=new[]{"Requested","Confirmed","Checked-In","In Progress","Completed","Cancelled","No-show"};
    if(!allowed.Contains(r.Status))return Results.BadRequest(new{message="Invalid appointment status."});
    a.Status=r.Status; Audit(db,"STATUS","Appointment",a.Id,r.Status); await db.SaveChangesAsync(); return Results.Ok(a);
});

app.MapPost("/api/appointments/{id:guid}/check-in", async (Guid id, CheckInRequest r, AppDbContext db) =>
{
    var a=await db.Appointments.FindAsync(id); if(a is null)return Results.NotFound();
    var v=await db.Vehicles.FindAsync(a.VehicleId); if(v is null)return Results.BadRequest(new{message="Vehicle not found."});
    if(a.Status=="Cancelled"||a.Status=="Completed"||a.Status=="No-show")return Results.Conflict(new{message=$"Cannot check in a {a.Status} appointment."});
    if(string.IsNullOrWhiteSpace(r.ServiceCentre)||string.IsNullOrWhiteSpace(r.Bay))return Results.BadRequest(new{message="Service centre and bay are required."});
    var bayOk=await db.ServiceBays.AnyAsync(x=>x.IsActive&&x.ServiceCentre==r.ServiceCentre&&x.BayCode==r.Bay);
    if(!bayOk)return Results.BadRequest(new{message="Selected bay does not belong to the selected service centre."});
    a.ServiceCentre=r.ServiceCentre;a.Bay=r.Bay;a.Status="Checked-In";
    if(!string.IsNullOrWhiteSpace(r.AdditionalComplaint))a.Reason=string.IsNullOrWhiteSpace(a.Reason)?r.AdditionalComplaint:$"{a.Reason}; {r.AdditionalComplaint}";
    if(r.OdometerKm.HasValue&&r.OdometerKm.Value>=v.OdometerKm)v.OdometerKm=r.OdometerKm.Value;
    if(r.OperatingHours.HasValue&&r.OperatingHours.Value>=v.OperatingHours)v.OperatingHours=r.OperatingHours.Value;
    if(r.EnergyKwh.HasValue&&r.EnergyKwh.Value>=v.EnergyKwh)v.EnergyKwh=r.EnergyKwh.Value;
    Audit(db,"CHECKIN","Appointment",a.Id,$"{v.RegistrationNumber} @ {r.ServiceCentre}/{r.Bay}; {r.ArrivalRemarks}");
    await db.SaveChangesAsync();return Results.Ok(a);
});

async Task AddDiagnosticWorkAsync(AppDbContext db,JobCard jc,MaintenanceRequest mr,string priority)
{
    if(await db.WorkItems.AnyAsync(x=>x.JobCardId==jc.Id&&x.Description.StartsWith(mr.RequestNumber+" ·"))) return;
    var code=string.IsNullOrWhiteSpace(mr.DiagnosticTemplateCode)?"DIAG-GENERAL":mr.DiagnosticTemplateCode;
    var wt=await db.WorkTemplates.FirstOrDefaultAsync(x=>x.TemplateCode==code&&x.IsActive)
           ?? await db.WorkTemplates.FirstOrDefaultAsync(x=>x.TemplateCode=="DIAG-GENERAL"&&x.IsActive);
    var wi=new WorkItem{JobCardId=jc.Id,TaskCode=$"TSK-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..4]}",
        WorkType="Diagnostic",Description=$"{mr.RequestNumber} · {mr.Description}",Status="Not Started",Priority=priority,
        EstimatedHours=wt?.StandardHours??0.5m,StandardRepairHours=wt?.StandardHours??0.5m,RequiresQc=true,RequiresHvAuthorization=wt?.RequiresHvAuthorization??false,UpdatedAt=DateTime.UtcNow};
    db.WorkItems.Add(wi);
    if(wt!=null)
    {
        var inst=new WorkTemplateInstance{JobCardId=jc.Id,WorkItemId=wi.Id,WorkTemplateId=wt.Id,TemplateCode=wt.TemplateCode,TemplateName=wt.Name,TemplateVersion=wt.Version};
        db.WorkTemplateInstances.Add(inst);
        var fields=await db.WorkTemplateFields.AsNoTracking().Where(x=>x.WorkTemplateId==wt.Id).OrderBy(x=>x.Sequence).ToListAsync();
        foreach(var field in fields)
            db.WorkTemplateFieldInstances.Add(new WorkTemplateFieldInstance{WorkTemplateInstanceId=inst.Id,SourceTemplateFieldId=field.Id,SectionName=field.SectionName,Sequence=field.Sequence,
                FieldCode=field.FieldCode,Label=field.Label,FieldType=field.FieldType,UnitCode=field.UnitCode,IsMandatory=field.IsMandatory,MinValue=field.MinValue,MaxValue=field.MaxValue,
                Options=field.Options,FailureAction=field.FailureAction,SuggestedIssueCode=field.SuggestedIssueCode,
                ActionCode=field.FieldType=="Number"?"M":field.FieldType=="Pass/Fail"?"F":field.FieldType=="OK/Not OK"||field.FieldType=="Yes/No"?"I":"D"});
    }
    mr.JobCardId=jc.Id;
    mr.Status="Converted";
}

app.MapPost("/api/work-orders/{jobCardId:guid}/diagnosis/fallback", async (Guid jobCardId, AppDbContext db) =>
{
    var jc=await db.JobCards.FindAsync(jobCardId);if(jc is null)return Results.NotFound();
    var existing=await db.WorkTemplateInstances.AsNoTracking().Where(x=>x.JobCardId==jobCardId&&x.TemplateCode=="DIAG-GENERAL").Select(x=>x.WorkItemId).FirstOrDefaultAsync();
    if(existing!=Guid.Empty)return Results.Conflict(new{message="General Diagnosis already exists.",workItemId=existing});
    var evt=await db.ServiceEvents.FindAsync(jc.ServiceEventId);if(evt is null)return Results.BadRequest(new{message="Service event not found."});
    var breakdown=evt.BreakdownId.HasValue?await db.Breakdowns.FindAsync(evt.BreakdownId.Value):null;
    var reference=breakdown?.BreakdownNumber??"GENERAL-DIAG";
    var description=breakdown is null?"General diagnosis / technician findings":$"General diagnosis for breakdown: {breakdown.Complaint}";
    var mr=new MaintenanceRequest{RequestNumber=reference,VehicleId=evt.VehicleId,Description=description,ComplaintCategoryCode="GENERAL",SymptomCode="OTHER",DiagnosticTemplateCode="DIAG-GENERAL",Priority=evt.Priority};
    await AddDiagnosticWorkAsync(db,jc,mr,evt.Priority);
    var created=db.ChangeTracker.Entries<WorkItem>().Select(x=>x.Entity).FirstOrDefault(x=>x.JobCardId==jobCardId&&x.Description.StartsWith(reference+" ·"));
    if(created is not null&&jc.TechnicianId.HasValue){created.AssignedToTechnicianId=jc.TechnicianId;created.AssignedTo=jc.Technician;created.Status="Assigned";}
    db.WorkLogEntries.Add(new WorkLogEntry{JobCardId=jobCardId,WorkItemId=created?.Id,EntryType="General Diagnosis Added",Comment=description,CreatedBy="Service Supervisor",CreatedRole="Supervisor"});
    Audit(db,"CREATE","GeneralDiagnosis",created?.Id??jobCardId,$"{reference}: {description}","Service Supervisor");
    await db.SaveChangesAsync();
    return Results.Ok(new{message="General diagnostic checklist generated.",workItemId=created?.Id});
});

app.MapPost("/api/appointments/{id:guid}/start-service", async (Guid id, AppDbContext db) =>
{
    var a=await db.Appointments.FindAsync(id); if(a is null)return Results.NotFound();
    var v=await db.Vehicles.FindAsync(a.VehicleId); if(v is null)return Results.BadRequest();
    if(a.Status=="In Progress")return Results.Conflict(new{message="Appointment already started."});
    if(a.Status=="Cancelled"||a.Status=="No-show"||a.Status=="Completed")return Results.Conflict(new{message=$"Cannot start a {a.Status} appointment."});

    var e=new ServiceEvent{VehicleId=v.Id,PmObligationId=a.PmObligationId,EventNumber=$"SE-{DateTime.UtcNow:yyyy}-{(await db.ServiceEvents.CountAsync()+1):D6}",
        EventType=a.AppointmentType,Priority=a.Priority,Status="In Progress"};
    var jc=new JobCard{ServiceEventId=e.Id,JobCardNumber=$"JC-{DateTime.UtcNow:yyyy}-{(await db.JobCards.CountAsync()+1):D6}",
        Status="Open",Bay=a.Bay,TechnicianId=a.TechnicianId,Technician=a.Technician,StartedAt=DateTime.UtcNow};
    a.Status="In Progress"; v.Status="Under Maintenance"; db.ServiceEvents.Add(e); db.JobCards.Add(jc);
    if(a.PmObligationId.HasValue)
    {
        var po=await db.PmObligations.FindAsync(a.PmObligationId.Value);
        if(po?.MaintenancePlanId is Guid planId)
        {
            var matrix=await(from m in db.MaintenancePlanMatrixItems where m.MaintenancePlanId==planId
                join t in db.MaintenanceTaskDefinitions on m.MaintenanceTaskDefinitionId equals t.Id where t.IsActive orderby m.Sequence,t.SortOrder select new{m,t}).ToListAsync();
            if(matrix.Count>0)
            {
                var plan=await db.MaintenancePlans.FindAsync(planId);var sectionSeq=0;
                foreach(var section in matrix.GroupBy(x=>x.t.SectionName))
                {
                    sectionSeq++;var wi=new WorkItem{JobCardId=jc.Id,TaskCode=$"TSK-{DateTime.UtcNow:yyyyMMddHHmmss}-{sectionSeq:D2}",WorkType="PM Section",Description=section.Key,Status="Not Started",Priority=a.Priority,RequiresQc=true,UpdatedAt=DateTime.UtcNow};db.WorkItems.Add(wi);
                    var inst=new WorkTemplateInstance{JobCardId=jc.Id,WorkItemId=wi.Id,WorkTemplateId=Guid.Empty,TemplateCode=$"MATRIX-{plan?.PlanCode}-{sectionSeq:D2}",TemplateName=section.Key,TemplateVersion=1};db.WorkTemplateInstances.Add(inst);
                    foreach(var x in section)
                    {
                        decimal? min=null,max=null;var match=Regex.Match(x.t.Specification??"",@"(-?\d+(?:\.\d+)?)\s*[–-]\s*(-?\d+(?:\.\d+)?)");if(match.Success){if(decimal.TryParse(match.Groups[1].Value,out var mn))min=mn;if(decimal.TryParse(match.Groups[2].Value,out var mx))max=mx;}
                        var serviceAction=string.IsNullOrWhiteSpace(x.m.ActionCode)?x.t.ActionCode:x.m.ActionCode;
                        var fieldType=serviceAction switch{"M"=>"Number","F"=>"Pass/Fail","D"=>"Text","L"=>"OK/Not OK","R"=>"OK/Not OK","T"=>"OK/Not OK",_=>"OK/Not OK"};
                        var fail=string.IsNullOrWhiteSpace(x.t.Severity)?"None":x.t.Severity.Equals("Critical",StringComparison.OrdinalIgnoreCase)?"Block Completion":"Create Defect";
                        db.WorkTemplateFieldInstances.Add(new WorkTemplateFieldInstance{WorkTemplateInstanceId=inst.Id,SectionName=x.t.SectionName,Sequence=x.m.Sequence>0?x.m.Sequence:x.t.SortOrder,FieldCode=x.t.TaskCode,Label=x.t.TaskName,FieldType=fieldType,UnitCode=x.t.UnitCode,IsMandatory=x.m.IsMandatory,MinValue=min,MaxValue=max,Options=fieldType=="OK/Not OK"?"OK;Not OK":fieldType=="Pass/Fail"?"Pass;Fail":"",FailureAction=fail,SuggestedIssueCode=x.t.SuggestedIssueCode,ActionCode=serviceAction,Specification=x.t.Specification,Severity=x.t.Severity});
                    }
                }
            }
            else
            {
                var templateMappings=await(from m in db.MaintenancePlanTemplates where m.MaintenancePlanId==planId join wt in db.WorkTemplates on m.WorkTemplateId equals wt.Id where wt.IsActive orderby m.Sequence select new{m,wt}).ToListAsync();
                if(templateMappings.Count>0)
                {
                    var seq=0;foreach(var x in templateMappings){seq++;var wi=new WorkItem{JobCardId=jc.Id,TaskCode=$"TSK-{DateTime.UtcNow:yyyyMMddHHmmss}-{seq:D2}",WorkType=x.wt.Category,Description=x.wt.Name,Status="Not Started",Priority=a.Priority,EstimatedHours=x.wt.StandardHours,StandardRepairHours=x.wt.StandardHours,RequiresQc=x.wt.RequiresQc,RequiresHvAuthorization=x.wt.RequiresHvAuthorization,UpdatedAt=DateTime.UtcNow};db.WorkItems.Add(wi);var inst=new WorkTemplateInstance{JobCardId=jc.Id,WorkItemId=wi.Id,WorkTemplateId=x.wt.Id,TemplateCode=x.wt.TemplateCode,TemplateName=x.wt.Name,TemplateVersion=x.wt.Version};db.WorkTemplateInstances.Add(inst);var fields=await db.WorkTemplateFields.AsNoTracking().Where(f=>f.WorkTemplateId==x.wt.Id).OrderBy(f=>f.Sequence).ToListAsync();foreach(var f in fields)db.WorkTemplateFieldInstances.Add(new WorkTemplateFieldInstance{WorkTemplateInstanceId=inst.Id,SourceTemplateFieldId=f.Id,SectionName=f.SectionName,Sequence=f.Sequence,FieldCode=f.FieldCode,Label=f.Label,FieldType=f.FieldType,UnitCode=f.UnitCode,IsMandatory=f.IsMandatory,MinValue=f.MinValue,MaxValue=f.MaxValue,Options=f.Options,FailureAction=f.FailureAction,SuggestedIssueCode=f.SuggestedIssueCode});}
                }
                else{var mapped=await(from m in db.MaintenancePlanTasks where m.MaintenancePlanId==planId join sm in db.ServiceTaskMasters on m.ServiceTaskMasterId equals sm.Id orderby m.Sequence select new{m,sm}).ToListAsync();var seq=0;foreach(var x in mapped){seq++;db.WorkItems.Add(new WorkItem{JobCardId=jc.Id,TaskCode=$"TSK-{DateTime.UtcNow:yyyyMMddHHmmss}-{seq:D2}",WorkType="PM",Description=x.sm.Name,Status="Not Started",Priority=a.Priority,EstimatedHours=x.sm.StandardHours,StandardRepairHours=x.sm.StandardHours,RequiresQc=x.sm.RequiresQc,RequiresHvAuthorization=x.sm.RequiresHvAuthorization,UpdatedAt=DateTime.UtcNow});}}
            }
        }
    }
    var linkedRequests=await db.MaintenanceRequests.Where(x=>x.VehicleId==v.Id && x.Status!="Cancelled" && x.Status!="Converted" &&
        ((a.SourceType=="Maintenance Request" && x.Id.ToString()==a.SourceReference) || (a.PmObligationId.HasValue && (x.Status=="Open"||x.Status=="Vehicle Arrived")))).OrderBy(x=>x.RequestedAt).ToListAsync();
    foreach(var mr in linkedRequests) await AddDiagnosticWorkAsync(db,jc,mr,a.Priority);

    db.VehicleAvailabilityLedger.Add(new VehicleAvailabilityLedger{VehicleId=v.Id,State="Under Maintenance",StartAt=DateTime.UtcNow,
        ReasonCode=a.AppointmentType,SourceType="ServiceEvent",SourceServiceEventId=e.Id});
    if(a.PmObligationId.HasValue){var p=await db.PmObligations.FindAsync(a.PmObligationId.Value);if(p!=null)p.Status="In Service";}
    Audit(db,"START","ServiceEvent",e.Id,$"{v.RegistrationNumber} via {a.AppointmentNumber}");
    await db.SaveChangesAsync(); return Results.Ok(new{serviceEvent=e,jobCard=jc});
});

app.MapGet("/api/service-events/active", async (AppDbContext db) =>
{
    var rows = await (from e in db.ServiceEvents.AsNoTracking()
                      join v in db.Vehicles.AsNoTracking() on e.VehicleId equals v.Id
                      join b in db.Breakdowns.AsNoTracking() on e.BreakdownId equals (Guid?)b.Id into bb
                      from b in bb.DefaultIfEmpty()
                      join j in db.JobCards.AsNoTracking() on e.Id equals j.ServiceEventId into jj
                      from j in jj.DefaultIfEmpty()
                      where e.Status!="Closed"
                      orderby e.OpenedAt descending
                      select new { e.Id,e.VehicleId,eventNo=e.EventNumber,vehicle=v.RegistrationNumber,type=e.EventType,e.Status,
                          breakdownId=e.BreakdownId,breakdownNumber=b!=null?b.BreakdownNumber:"",
                          complaint=b!=null?b.Complaint:"",reportedAt=b!=null?b.ReportedAt:(DateTime?)null,
                          breakdownLocation=b!=null?b.Location:"",dispatchMode=b!=null?b.DispatchMode:"",
                          jobCard=j!=null?j.JobCardNumber:"",jobCardId=j!=null?j.Id:(Guid?)null,bay=j!=null?j.Bay:"",
                          technician=j!=null?j.Technician:"",technicianId=j!=null?j.TechnicianId:null,sla=e.Priority }).ToListAsync();
    return Results.Ok(rows);
});

app.MapGet("/api/job-cards", async (AppDbContext db) =>
{
    var rows = await (from j in db.JobCards.AsNoTracking()
                      join e in db.ServiceEvents.AsNoTracking() on j.ServiceEventId equals e.Id
                      join v in db.Vehicles.AsNoTracking() on e.VehicleId equals v.Id
                      orderby j.StartedAt descending
                      select new { j.Id,j.JobCardNumber,j.Status,j.Bay,j.Technician,j.TechnicianId,j.StartedAt,j.CompletedAt,serviceEventId=e.Id,e.EventNumber,vehicle=v.RegistrationNumber }).ToListAsync();
    return Results.Ok(rows);
});


app.MapGet("/api/job-cards/{id:guid}/work-items", async (Guid id, AppDbContext db) =>
    Results.Ok(await db.WorkItems.AsNoTracking().Where(x=>x.JobCardId==id).OrderBy(x=>x.DueAt).ThenBy(x=>x.TaskCode).ToListAsync()));

app.MapGet("/api/tasks", async (Guid? jobCardId, AppDbContext db) =>
{
    var q=db.WorkItems.AsNoTracking().AsQueryable();
    if(jobCardId.HasValue)q=q.Where(x=>x.JobCardId==jobCardId.Value);
    var rows=await (from t in q join j in db.JobCards.AsNoTracking() on t.JobCardId equals j.Id
                    join e in db.ServiceEvents.AsNoTracking() on j.ServiceEventId equals e.Id
                    join v in db.Vehicles.AsNoTracking() on e.VehicleId equals v.Id
                    orderby t.DueAt,t.TaskCode
                    select new{t.Id,t.TaskCode,t.JobCardId,jobCard=j.JobCardNumber,vehicle=v.RegistrationNumber,t.WorkType,t.Description,t.Status,
                        t.AssignedToTechnicianId,t.AssignedTo,t.PlannedStartAt,t.DueAt,t.Priority,t.DependencyTaskId,t.EstimatedHours,t.ActualHours,
                        t.EvidenceReference,t.CompletionRemarks,t.RequiresQc,t.RequiresHvAuthorization,t.UpdatedAt,
                        hasPaperForm=db.WorkTemplateInstances.Any(w=>w.WorkItemId==t.Id)}).ToListAsync();
    return Results.Ok(rows);
});

app.MapPost("/api/tasks", async (TaskRequest r, AppDbContext db) =>
{
    var jc=await db.JobCards.FindAsync(r.JobCardId);if(jc is null)return Results.BadRequest(new{message="Job card not found."});
    Technician? tech=null;if(r.AssignedToTechnicianId.HasValue){tech=await db.Technicians.FindAsync(r.AssignedToTechnicianId.Value);if(tech is null)return Results.BadRequest(new{message="Technician not found."});}
    if(r.DependencyTaskId.HasValue && !await db.WorkItems.AnyAsync(x=>x.Id==r.DependencyTaskId.Value && x.JobCardId==r.JobCardId))
        return Results.BadRequest(new{message="Dependency must belong to the same job card."});
    var t=new WorkItem{JobCardId=r.JobCardId,TaskCode=$"TSK-{DateTime.UtcNow:yyyyMMdd}-{(await db.WorkItems.CountAsync()+1):D5}",
        WorkType=r.WorkType,Description=r.Description,Status="Not Started",AssignedToTechnicianId=r.AssignedToTechnicianId,AssignedTo=tech?.Name??"",
        PlannedStartAt=r.PlannedStartAt,DueAt=r.DueAt,Priority=r.Priority,DependencyTaskId=r.DependencyTaskId,EstimatedHours=r.EstimatedHours,
        RequiresQc=r.RequiresQc,RequiresHvAuthorization=r.RequiresHvAuthorization,UpdatedAt=DateTime.UtcNow};
    db.WorkItems.Add(t);Audit(db,"CREATE","Task",t.Id,$"{t.TaskCode} {t.Description}");await db.SaveChangesAsync();return Results.Created($"/api/tasks/{t.Id}",t);
});

app.MapPut("/api/tasks/{id:guid}/status", async (Guid id, TaskStatusRequest r, AppDbContext db) =>
{
    var t=await db.WorkItems.FindAsync(id);if(t is null)return Results.NotFound();
    var allowed=new[]{"Pending Approval","Not Started","Assigned","In Progress","On Hold","Completed","Cancelled"};if(!allowed.Contains(r.Status))return Results.BadRequest(new{message="Invalid task status."});
    if(r.Status=="In Progress"&&!t.AssignedToTechnicianId.HasValue)return Results.Conflict(new{message="Assign a technician before starting this work."});
    if(r.Status=="In Progress" && t.DependencyTaskId.HasValue && t.WorkType!="Corrective Repair"){var d=await db.WorkItems.FindAsync(t.DependencyTaskId.Value);if(d!=null&&d.Status!="Completed")return Results.Conflict(new{message=$"Dependency {d.TaskCode} must be completed first."});}
    if(r.Status=="Completed"&&(t.WorkType=="Corrective Repair"||t.WorkType=="Additional Work")&&string.IsNullOrWhiteSpace(r.CompletionRemarks))
        return Results.Conflict(new{message="Record the work performed before completing this task."});
    t.Status=r.Status;t.ActualHours=r.ActualHours??t.ActualHours;t.CompletionRemarks=r.CompletionRemarks??t.CompletionRemarks;t.EvidenceReference=r.EvidenceReference??t.EvidenceReference;t.UpdatedAt=DateTime.UtcNow;
    if(r.Status=="In Progress")
    {
        var jc=await db.JobCards.FindAsync(t.JobCardId);
        if(jc is not null){jc.Status="In Progress";var evt=await db.ServiceEvents.FindAsync(jc.ServiceEventId);if(evt is not null)evt.Status="In Progress";}
    }
    if(r.Status=="Completed")db.WorkLogEntries.Add(new WorkLogEntry{JobCardId=t.JobCardId,WorkItemId=t.Id,EntryType="Task Completed",Comment=t.CompletionRemarks,CreatedBy=string.IsNullOrWhiteSpace(t.AssignedTo)?"Service User":t.AssignedTo,CreatedRole="Technician"});
    Audit(db,"STATUS","Task",t.Id,$"{t.TaskCode}:{r.Status}");await db.SaveChangesAsync();return Results.Ok(t);
});

app.MapPut("/api/tasks/{id:guid}/assign", async (Guid id, TaskAssignRequest r, AppDbContext db) =>
{
    var t=await db.WorkItems.FindAsync(id);if(t is null)return Results.NotFound();var tech=await db.Technicians.FindAsync(r.TechnicianId);
    if(t.Status=="Pending Approval")return Results.Conflict(new{message="Supervisor approval is required before assignment."});
    if(tech is null||!tech.IsActive)return Results.BadRequest(new{message="Technician not active."});
    if(t.RequiresHvAuthorization && (!tech.HvAuthorized || tech.HvAuthorizationValidUntil<DateTime.UtcNow))return Results.Conflict(new{message="Task requires active HV authorization."});
    t.AssignedToTechnicianId=tech.Id;t.AssignedTo=tech.Name;if(t.Status=="Not Started")t.Status="Assigned";t.UpdatedAt=DateTime.UtcNow;
    Audit(db,"ASSIGN","Task",t.Id,$"{t.TaskCode}->{tech.Name}");await db.SaveChangesAsync();return Results.Ok(t);
});

app.MapPut("/api/job-cards/{id:guid}/assign", async (Guid id, JobCardAssignRequest r, AppDbContext db) =>
{
    var jc=await db.JobCards.FindAsync(id);if(jc is null)return Results.NotFound();
    Technician? tech=null;
    if(r.TechnicianId.HasValue)
    {
        tech=await db.Technicians.FindAsync(r.TechnicianId.Value);
        if(tech is null||!tech.IsActive)return Results.BadRequest(new{message="Select an active technician."});
        jc.TechnicianId=tech.Id;jc.Technician=tech.Name;
    }
    if(!string.IsNullOrWhiteSpace(r.Bay))jc.Bay=r.Bay.Trim();
    if(tech is null&&string.IsNullOrWhiteSpace(r.Bay))return Results.BadRequest(new{message="Select a technician or bay."});
    if(tech is not null&&r.AssignOpenTasks)
    {
        var tasks=await db.WorkItems.Where(x=>x.JobCardId==id&&x.Status!="Completed"&&x.Status!="Cancelled"&&x.Status!="Pending Approval").ToListAsync();
        foreach(var task in tasks){task.AssignedToTechnicianId=tech.Id;task.AssignedTo=tech.Name;if(task.Status=="Not Started")task.Status="Assigned";task.UpdatedAt=DateTime.UtcNow;}
    }
    var evt=await db.ServiceEvents.FindAsync(jc.ServiceEventId);if(evt is not null&&tech is not null&&evt.Status=="Awaiting Assignment")evt.Status="Assigned";
    var by=string.IsNullOrWhiteSpace(r.AssignedBy)?"Service Supervisor":r.AssignedBy.Trim();
    db.WorkLogEntries.Add(new WorkLogEntry{JobCardId=id,EntryType="Work Order Assigned",Comment=$"Technician: {jc.Technician}; Bay: {jc.Bay}",CreatedBy=by,CreatedRole="Supervisor"});
    Audit(db,"ASSIGN","JobCard",jc.Id,$"{jc.JobCardNumber}->{jc.Technician}; bay={jc.Bay}",by);await db.SaveChangesAsync();return Results.Ok(jc);
});

app.MapPost("/api/tasks/{id:guid}/approve", async (Guid id, WorkApprovalRequest r, AppDbContext db) =>
{
    var t=await db.WorkItems.FindAsync(id);if(t is null)return Results.NotFound();
    if(t.WorkType!="Additional Work"||t.Status!="Pending Approval")return Results.Conflict(new{message="Only pending additional work can be approved."});
    t.Status="Not Started";t.UpdatedAt=DateTime.UtcNow;
    db.WorkLogEntries.Add(new WorkLogEntry{JobCardId=t.JobCardId,WorkItemId=t.Id,EntryType="Additional Work Approved",Comment=r.Remarks,CreatedBy=r.ApprovedBy,CreatedRole="Supervisor"});
    Audit(db,"APPROVE","Task",t.Id,$"{t.TaskCode}: {r.Remarks}",r.ApprovedBy);await db.SaveChangesAsync();return Results.Ok(t);
});

app.MapPost("/api/job-cards/{id:guid}/work-items", async (Guid id, WorkItemRequest r, AppDbContext db) =>
{
    if(!await db.JobCards.AnyAsync(x=>x.Id==id))return Results.NotFound();
    var w=new WorkItem{JobCardId=id,TaskCode=$"TSK-{DateTime.UtcNow:yyyyMMdd}-{(await db.WorkItems.CountAsync()+1):D5}",WorkType=r.WorkType,
        Description=r.Description,Status="Not Started",EstimatedHours=r.StandardRepairHours,StandardRepairHours=r.StandardRepairHours,
        RequiresQc=r.RequiresQc,RequiresHvAuthorization=r.RequiresHvAuthorization,UpdatedAt=DateTime.UtcNow};
    db.WorkItems.Add(w);Audit(db,"CREATE","Task",w.Id,w.Description);await db.SaveChangesAsync();return Results.Created($"/api/tasks/{w.Id}",w);
});
app.MapPut("/api/work-items/{id:guid}/status", async (Guid id, StatusRequest r, AppDbContext db) =>
{
    var w=await db.WorkItems.FindAsync(id);if(w is null)return Results.NotFound();w.Status=r.Status;w.UpdatedAt=DateTime.UtcNow;Audit(db,"STATUS","Task",w.Id,r.Status);await db.SaveChangesAsync();return Results.Ok(w);
});

app.MapGet("/api/labour", async (Guid? jobCardId, AppDbContext db) =>
{
    var q=db.LabourEntries.AsNoTracking().AsQueryable();if(jobCardId.HasValue)q=q.Where(x=>x.JobCardId==jobCardId.Value);
    return Results.Ok(await q.OrderByDescending(x=>x.StartAt).ToListAsync());
});

app.MapGet("/api/service-workspace/{jobCardId:guid}", async (Guid jobCardId, AppDbContext db) =>
{
    var data=await (from j in db.JobCards.AsNoTracking() join e in db.ServiceEvents.AsNoTracking() on j.ServiceEventId equals e.Id
                    join v in db.Vehicles.AsNoTracking() on e.VehicleId equals v.Id where j.Id==jobCardId
                    select new{jobCardId=j.Id,j.JobCardNumber,j.Status,j.Bay,j.Technician,j.TechnicianId,serviceEventId=e.Id,e.EventNumber,e.EventType,e.Priority,e.BreakdownId,
                        vehicleId=v.Id,vehicle=v.RegistrationNumber,v.Vin,v.Model,v.OdometerKm,v.OperatingHours}).FirstOrDefaultAsync();
    if(data is null)return Results.NotFound();
    var breakdown=data.BreakdownId.HasValue?await db.Breakdowns.AsNoTracking().Where(x=>x.Id==data.BreakdownId.Value)
        .Select(x=>new{x.Id,x.BreakdownNumber,x.Complaint,x.Location,x.DispatchMode,x.TriageDecision,x.ReportedAt,x.ResponseAt,x.RestoredAt,x.Status}).FirstOrDefaultAsync():null;
    var tasks=await db.WorkItems.CountAsync(x=>x.JobCardId==jobCardId);var completed=await db.WorkItems.CountAsync(x=>x.JobCardId==jobCardId&&x.Status=="Completed");
    var defects=await db.Defects.CountAsync(x=>x.JobCardId==jobCardId&&x.Disposition!="Closed");
    var instanceIds=await db.WorkTemplateInstances.Where(x=>x.JobCardId==jobCardId).Select(x=>x.Id).ToListAsync();var checks=await db.WorkTemplateFieldInstances.CountAsync(x=>instanceIds.Contains(x.WorkTemplateInstanceId));var checksDone=await db.WorkTemplateFieldInstances.CountAsync(x=>instanceIds.Contains(x.WorkTemplateInstanceId)&&x.Result!="Pending"&&x.Value!="");
    var parts=await db.PartRequests.CountAsync(x=>x.JobCardId==jobCardId);var partsWaiting=await db.PartRequests.CountAsync(x=>x.JobCardId==jobCardId&&(x.Status=="Requested"||x.Status=="Awaiting Stock"));var qc=await db.QcInspections.Where(x=>x.JobCardId==jobCardId).OrderByDescending(x=>x.InspectedAt).Select(x=>x.Result).FirstOrDefaultAsync();
    return Results.Ok(new{data,breakdown,tasks,completedTasks=completed,checksTotal=checks,checksCompleted=checksDone,openDefects=defects,partTransactions=parts,partsWaiting,qcStatus=qc??"Pending"});
});
app.MapGet("/api/service-workspace/{jobCardId:guid}/execution",async(Guid jobCardId,AppDbContext db)=>
{
    var items=await db.WorkItems.AsNoTracking().Where(x=>x.JobCardId==jobCardId).OrderBy(x=>x.TaskCode).ToListAsync();var ids=items.Select(x=>x.Id).ToList();
    var inst=await db.WorkTemplateInstances.AsNoTracking().Where(x=>x.JobCardId==jobCardId).ToListAsync();var instIds=inst.Select(x=>x.Id).ToList();
    var fields=await db.WorkTemplateFieldInstances.AsNoTracking().Where(x=>instIds.Contains(x.WorkTemplateInstanceId)).OrderBy(x=>x.Sequence).ToListAsync();var fieldIds=fields.Select(x=>x.Id).ToList();var defects=await db.Defects.AsNoTracking().Where(x=>x.JobCardId==jobCardId).ToListAsync();var issueIds=defects.Where(x=>x.ChecklistFieldInstanceId.HasValue&&fieldIds.Contains(x.ChecklistFieldInstanceId.Value)&&x.Disposition!="Closed").Select(x=>x.ChecklistFieldInstanceId!.Value).ToList();
    var sections=inst.Select(i=>{var wi=items.FirstOrDefault(x=>x.Id==i.WorkItemId);var fs=fields.Where(f=>f.WorkTemplateInstanceId==i.Id).Select(f=>new{f.Id,f.Sequence,f.FieldCode,f.Label,f.FieldType,f.ActionCode,f.Specification,f.Severity,f.UnitCode,f.IsMandatory,f.MinValue,f.MaxValue,f.Options,f.FailureAction,f.SuggestedIssueCode,f.Value,f.Result,f.Remarks,f.EvidenceReference,f.ExecutedAt,f.ExecutedBy,issueRecorded=issueIds.Contains(f.Id)});return new{workItemId=i.WorkItemId,taskCode=wi?.TaskCode??"",workType=wi?.WorkType??"",description=wi?.Description??"",templateCode=i.TemplateCode,name=i.TemplateName,status=wi?.Status??i.Status,completed=fs.Count(x=>x.Result!="Pending"&&x.Value!=""),total=fs.Count(),fields=fs};});
    var corrective=items.Where(x=>x.WorkType=="Corrective Repair").Select(x=>{var d=defects.FirstOrDefault(z=>z.CorrectiveWorkItemId==x.Id);var f=d?.ChecklistFieldInstanceId is Guid fieldKey?fields.FirstOrDefault(z=>z.Id==fieldKey):null;return new{x.Id,x.TaskCode,x.Description,x.Status,x.Priority,x.DependencyTaskId,x.AssignedToTechnicianId,x.AssignedTo,x.ActualHours,x.CompletionRemarks,defectId=d?.Id,originFieldId=d?.ChecklistFieldInstanceId,originCode=f?.FieldCode??"",originLabel=f?.Label??"",issue=d?.Description??"",severity=d?.Severity??""};});return Results.Ok(new{sections,corrective});
});
app.MapPost("/api/tasks/{workItemId:guid}/paper-form/{fieldId:guid}/recheck-pass",async(Guid workItemId,Guid fieldId,AppDbContext db)=>{var d=await db.Defects.FirstOrDefaultAsync(x=>x.ChecklistFieldInstanceId==fieldId&&x.Disposition!="Closed");if(d is null)return Results.NotFound(new{message="No open issue found."});if(d.CorrectiveWorkItemId.HasValue){var c=await db.WorkItems.FindAsync(d.CorrectiveWorkItemId.Value);if(c!=null&&c.Status!="Completed")return Results.Conflict(new{message="Complete the corrective work before recheck."});}d.Disposition="Closed";d.ClosedAt=DateTime.UtcNow;var f=await db.WorkTemplateFieldInstances.FindAsync(fieldId);if(f!=null){f.Result="Pass";f.Value=f.FieldType=="OK/Not OK"?"OK":"Pass";f.ExecutedAt=DateTime.UtcNow;}await db.SaveChangesAsync();return Results.Ok();});

app.MapPost("/api/tasks/{workItemId:guid}/paper-form/{fieldId:guid}/recheck",async(Guid workItemId,Guid fieldId,RecheckRequest r,AppDbContext db)=>
{
    var d=await db.Defects.FirstOrDefaultAsync(x=>x.ChecklistFieldInstanceId==fieldId&&x.Disposition!="Closed");
    if(d is null)return Results.NotFound(new{message="No open issue found."});
    if(d.CorrectiveWorkItemId.HasValue){var c=await db.WorkItems.FindAsync(d.CorrectiveWorkItemId.Value);if(c!=null&&c.Status!="Completed")return Results.Conflict(new{message="Complete the corrective work before recheck."});}
    var f=await db.WorkTemplateFieldInstances.FindAsync(fieldId);if(f is null)return Results.NotFound(new{message="Checkpoint not found."});
    var passed=string.Equals(r.Result,"Pass",StringComparison.OrdinalIgnoreCase)||string.Equals(r.Result,"OK",StringComparison.OrdinalIgnoreCase);
    f.Result=passed?"Pass":"Fail";f.Value=f.FieldType=="OK/Not OK"?(passed?"OK":"Not OK"):(passed?"Pass":"Fail");
    f.Remarks=string.IsNullOrWhiteSpace(r.Remarks)?f.Remarks:r.Remarks;f.ExecutedAt=DateTime.UtcNow;f.ExecutedBy=r.RecheckedBy;
    if(passed){d.Disposition="Closed";d.ClosedAt=DateTime.UtcNow;}
    db.WorkLogEntries.Add(new WorkLogEntry{JobCardId=d.JobCardId,WorkItemId=workItemId,ChecklistFieldInstanceId=fieldId,EntryType="Recheck",Comment=$"{f.FieldCode} {r.Result}: {r.Remarks}",CreatedBy=r.RecheckedBy,CreatedRole=r.RecheckedRole});
    Audit(db,"RECHECK","Checkpoint",fieldId,$"{f.FieldCode}:{r.Result}; {r.Remarks}",r.RecheckedBy);await db.SaveChangesAsync();return Results.Ok(new{passed,status=passed?"Closed":"Open"});
});

app.MapGet("/api/work-orders/{jobCardId:guid}/work-log",async(Guid jobCardId,AppDbContext db)=>
    Results.Ok(await db.WorkLogEntries.AsNoTracking().Where(x=>x.JobCardId==jobCardId).OrderByDescending(x=>x.CreatedAt).ToListAsync()));

app.MapPost("/api/work-orders/{jobCardId:guid}/work-log",async(Guid jobCardId,WorkLogCreate r,AppDbContext db)=>
{
    if(!await db.JobCards.AnyAsync(x=>x.Id==jobCardId))return Results.NotFound();
    if(string.IsNullOrWhiteSpace(r.Comment))return Results.BadRequest(new{message="Enter a work note or comment."});
    if(r.WorkItemId.HasValue&&!await db.WorkItems.AnyAsync(x=>x.Id==r.WorkItemId.Value&&x.JobCardId==jobCardId))return Results.BadRequest(new{message="Task does not belong to this Work Order."});
    var x=new WorkLogEntry{JobCardId=jobCardId,WorkItemId=r.WorkItemId,ChecklistFieldInstanceId=r.ChecklistFieldInstanceId,EntryType=string.IsNullOrWhiteSpace(r.EntryType)?"Work Note":r.EntryType.Trim(),Comment=r.Comment.Trim(),CreatedBy=r.CreatedBy,CreatedRole=r.CreatedRole};
    db.WorkLogEntries.Add(x);Audit(db,"NOTE","WorkOrder",jobCardId,$"{x.EntryType}: {x.Comment}",x.CreatedBy);await db.SaveChangesAsync();return Results.Ok(x);
});

app.MapPost("/api/work-orders/{jobCardId:guid}/additional-work",async(Guid jobCardId,AdditionalWorkCreate r,AppDbContext db)=>
{
    if(!await db.JobCards.AnyAsync(x=>x.Id==jobCardId))return Results.NotFound();
    if(string.IsNullOrWhiteSpace(r.Description)||string.IsNullOrWhiteSpace(r.Reason))return Results.BadRequest(new{message="Work description and reason are required."});
    Technician? tech=null;if(r.TechnicianId.HasValue){tech=await db.Technicians.FindAsync(r.TechnicianId.Value);if(tech is null||!tech.IsActive)return Results.BadRequest(new{message="Select an active technician."});}
    var addedByTechnician=string.Equals(r.CreatedRole,"Technician",StringComparison.OrdinalIgnoreCase);
    var task=new WorkItem{JobCardId=jobCardId,TaskCode=$"TSK-{DateTime.UtcNow:yyyyMMdd}-{(await db.WorkItems.CountAsync()+1):D5}",WorkType="Additional Work",Description=r.Description.Trim(),Status=addedByTechnician?"Pending Approval":tech is null?"Not Started":"Assigned",AssignedToTechnicianId=addedByTechnician?null:tech?.Id,AssignedTo=addedByTechnician?"":tech?.Name??"",Priority=string.IsNullOrWhiteSpace(r.Priority)?"P3":r.Priority,EstimatedHours=r.EstimatedHours,RequiresQc=r.RequiresQc,UpdatedAt=DateTime.UtcNow};
    db.WorkItems.Add(task);
    db.WorkLogEntries.Add(new WorkLogEntry{JobCardId=jobCardId,WorkItemId=task.Id,EntryType="Additional Work Added",Comment=$"{r.Description.Trim()} · Reason: {r.Reason.Trim()}",CreatedBy=r.CreatedBy,CreatedRole=r.CreatedRole});
    Audit(db,"CREATE","AdditionalWork",task.Id,$"{task.Description}; reason={r.Reason}",r.CreatedBy);await db.SaveChangesAsync();return Results.Ok(task);
});

app.MapGet("/api/work-orders/{jobCardId:guid}/evidence",async(Guid jobCardId,AppDbContext db)=>
{
    var rows=await db.WorkEvidence.AsNoTracking().Where(x=>x.JobCardId==jobCardId).OrderByDescending(x=>x.UploadedAt)
        .Select(x=>new{x.Id,x.JobCardId,x.WorkItemId,x.ChecklistFieldInstanceId,x.WorkLogEntryId,x.Stage,x.FileName,x.ContentType,x.FileSize,x.Caption,x.UploadedBy,x.UploadedAt,url=$"/api/work-evidence/{x.Id}/content"}).ToListAsync();
    return Results.Ok(rows);
});

app.MapPost("/api/work-orders/{jobCardId:guid}/evidence",async(Guid jobCardId,HttpRequest request,AppDbContext db)=>
{
    if(!request.HasFormContentType)return Results.BadRequest(new{message="Upload a photo or video file."});
    if(!await db.JobCards.AnyAsync(x=>x.Id==jobCardId))return Results.NotFound();
    var form=await request.ReadFormAsync();var file=form.Files.FirstOrDefault();if(file is null||file.Length==0)return Results.BadRequest(new{message="Choose a file to upload."});
    const long maxBytes=25L*1024*1024;if(file.Length>maxBytes)return Results.BadRequest(new{message="Evidence files must be 25 MB or smaller."});
    var allowed=file.ContentType.StartsWith("image/",StringComparison.OrdinalIgnoreCase)||file.ContentType.StartsWith("video/",StringComparison.OrdinalIgnoreCase)||file.ContentType=="application/pdf";
    if(!allowed)return Results.BadRequest(new{message="Only images, short videos and PDF files are supported."});
    Guid? workItemId=Guid.TryParse(form["workItemId"].FirstOrDefault(),out var wi)?wi:null;Guid? fieldId=Guid.TryParse(form["checklistFieldInstanceId"].FirstOrDefault(),out var fi)?fi:null;Guid? logId=Guid.TryParse(form["workLogEntryId"].FirstOrDefault(),out var li)?li:null;
    if(workItemId.HasValue&&!await db.WorkItems.AnyAsync(x=>x.Id==workItemId.Value&&x.JobCardId==jobCardId))return Results.BadRequest(new{message="Task does not belong to this Work Order."});
    await using var input=file.OpenReadStream();using var buffer=new MemoryStream();await input.CopyToAsync(buffer);
    var x=new WorkEvidence{JobCardId=jobCardId,WorkItemId=workItemId,ChecklistFieldInstanceId=fieldId,WorkLogEntryId=logId,Stage=form["stage"].FirstOrDefault()??"General",FileName=Path.GetFileName(file.FileName),ContentType=file.ContentType,FileSize=file.Length,Content=buffer.ToArray(),Caption=form["caption"].FirstOrDefault()??"",UploadedBy=form["uploadedBy"].FirstOrDefault()??"Service User"};
    db.WorkEvidence.Add(x);db.WorkLogEntries.Add(new WorkLogEntry{JobCardId=jobCardId,WorkItemId=workItemId,ChecklistFieldInstanceId=fieldId,EntryType="Evidence Added",Comment=$"{x.Stage}: {x.FileName}",CreatedBy=x.UploadedBy,CreatedRole=form["uploadedRole"].FirstOrDefault()??"Technician"});
    Audit(db,"UPLOAD","WorkEvidence",x.Id,$"{x.Stage}:{x.FileName}",x.UploadedBy);await db.SaveChangesAsync();return Results.Ok(new{x.Id,x.FileName,x.ContentType,x.Stage,url=$"/api/work-evidence/{x.Id}/content"});
}).DisableAntiforgery();

app.MapGet("/api/work-evidence/{id:guid}/content",async(Guid id,AppDbContext db)=>
{
    var x=await db.WorkEvidence.AsNoTracking().FirstOrDefaultAsync(e=>e.Id==id);return x is null?Results.NotFound():Results.File(x.Content,x.ContentType,x.FileName,enableRangeProcessing:true);
});

app.MapGet("/api/parts/master", async (AppDbContext db) => Results.Ok(await db.PartMasters.AsNoTracking().Where(x=>x.IsActive).OrderBy(x=>x.PartNumber).ToListAsync()));
app.MapPost("/api/parts/master", async (PartMasterRequest r, AppDbContext db) =>
{
    if(string.IsNullOrWhiteSpace(r.PartNumber))return Results.BadRequest(new{message="Part number is required."});
    if(await db.PartMasters.AnyAsync(x=>x.PartNumber==r.PartNumber))return Results.Conflict(new{message="Part number already exists."});
    var p=new PartMaster{PartNumber=r.PartNumber.Trim(),Description=r.Description,Category=r.Category,UnitOfMeasure=r.UnitOfMeasure,ManufacturerPartNumber=r.ManufacturerPartNumber,
        IsSerialized=r.IsSerialized,IsWarrantyReturnable=r.IsWarrantyReturnable,ReorderLevel=r.ReorderLevel,ReorderQuantity=r.ReorderQuantity,StandardCost=r.StandardCost};
    db.PartMasters.Add(p);Audit(db,"CREATE","PartMaster",p.Id,p.PartNumber);await db.SaveChangesAsync();return Results.Created($"/api/parts/master/{p.Id}",p);
});

app.MapPut("/api/parts/master/{id:guid}", async (Guid id, PartMasterRequest r, AppDbContext db) =>
{
    var p=await db.PartMasters.FindAsync(id); if(p is null)return Results.NotFound();
    p.Description=r.Description;p.Category=r.Category;p.UnitOfMeasure=r.UnitOfMeasure;p.ManufacturerPartNumber=r.ManufacturerPartNumber;
    p.IsSerialized=r.IsSerialized;p.IsWarrantyReturnable=r.IsWarrantyReturnable;p.ReorderLevel=r.ReorderLevel;p.ReorderQuantity=r.ReorderQuantity;p.StandardCost=r.StandardCost;
    Audit(db,"UPDATE","PartMaster",p.Id,$"{p.PartNumber} cost={p.StandardCost}");await db.SaveChangesAsync();return Results.Ok(p);
});

app.MapGet("/api/parts/locations", async (AppDbContext db) => Results.Ok(await db.InventoryLocations.AsNoTracking().Where(x=>x.IsActive).OrderBy(x=>x.LocationCode).ToListAsync()));
app.MapPost("/api/parts/locations", async (InventoryLocation r, AppDbContext db) =>
{
    if(string.IsNullOrWhiteSpace(r.LocationCode))return Results.BadRequest(new{message="Location code is required."});
    if(await db.InventoryLocations.AnyAsync(x=>x.LocationCode==r.LocationCode))return Results.Conflict(new{message="Location already exists."});
    r.Id=Guid.NewGuid();db.InventoryLocations.Add(r);Audit(db,"CREATE","InventoryLocation",r.Id,r.LocationCode);await db.SaveChangesAsync();return Results.Ok(r);
});
app.MapGet("/api/parts/stock", async (AppDbContext db) =>
{
    var rows=await (from s in db.PartStocks.AsNoTracking() join p in db.PartMasters.AsNoTracking() on s.PartMasterId equals p.Id
                    join l in db.InventoryLocations.AsNoTracking() on s.InventoryLocationId equals l.Id orderby p.PartNumber
                    select new{s.Id,s.PartMasterId,p.PartNumber,p.Description,p.Category,p.UnitOfMeasure,s.InventoryLocationId,l.LocationCode,location=l.Name,l.Bin,
                        s.OnHandQty,s.ReservedQty,availableQty=s.OnHandQty-s.ReservedQty,p.StandardCost,stockValue=s.OnHandQty*p.StandardCost,p.ReorderLevel,p.ReorderQuantity,reorderRequired=(s.OnHandQty-s.ReservedQty)<=p.ReorderLevel,s.UpdatedAt}).ToListAsync();
    return Results.Ok(rows);
});
app.MapPost("/api/parts/receive", async (StockMovementRequest r, AppDbContext db) =>
{
    if(r.Quantity<=0)return Results.BadRequest(new{message="Quantity must be greater than zero."});
    var p=await db.PartMasters.FindAsync(r.PartMasterId);
    var l=await db.InventoryLocations.FindAsync(r.InventoryLocationId);
    if(p is null||l is null)return Results.BadRequest(new{message="Part or location not found."});
    var stock=await db.PartStocks.FirstOrDefaultAsync(x=>x.PartMasterId==p.Id&&x.InventoryLocationId==l.Id);
    if(stock is null){stock=new PartStock{PartMasterId=p.Id,InventoryLocationId=l.Id};db.PartStocks.Add(stock);}
    stock.OnHandQty+=r.Quantity;stock.UpdatedAt=DateTime.UtcNow;
    var unitCost=p.StandardCost;
    var tx=new PartTransaction{
        PartMasterId=p.Id,InventoryLocationId=l.Id,JobCardId=Guid.Empty,PartNumber=p.PartNumber,PartDescription=p.Description,
        TransactionType="Receipt",Quantity=r.Quantity,PerformedBy=r.User,UnitCost=unitCost,ExtendedCost=Math.Round(r.Quantity*unitCost,2)
    };
    db.PartTransactions.Add(tx);
    Audit(db,"RECEIPT","PartStock",stock.Id,$"{p.PartNumber} +{r.Quantity} @ {unitCost}",r.User);
    await db.SaveChangesAsync();return Results.Ok(stock);
});
app.MapGet("/api/part-requests", async (Guid? jobCardId, AppDbContext db) =>
{
    var q=db.PartRequests.AsNoTracking().AsQueryable();if(jobCardId.HasValue)q=q.Where(x=>x.JobCardId==jobCardId.Value);
    var rows=await (from r in q join p in db.PartMasters.AsNoTracking() on r.PartMasterId equals p.Id join l in db.InventoryLocations.AsNoTracking() on r.InventoryLocationId equals l.Id
                    join j in db.JobCards.AsNoTracking() on r.JobCardId equals j.Id
                    from t in db.WorkItems.AsNoTracking().Where(x=>x.Id==r.WorkItemId).DefaultIfEmpty()
                    orderby r.RequestedAt descending select new{r.Id,r.RequestNumber,r.JobCardId,jobCard=j.JobCardNumber,r.WorkItemId,task=t==null?"":t.TaskCode,r.PartMasterId,
                        p.PartNumber,p.Description,p.UnitOfMeasure,r.InventoryLocationId,l.LocationCode,r.QuantityRequired,r.QuantityReserved,r.QuantityIssued,r.QuantityReturned,r.QuantityConsumed,
                        r.Status,r.WarrantyCandidate,r.FailedPartDisposition,r.RequestedBy,r.RequestedAt,r.UpdatedAt}).ToListAsync();return Results.Ok(rows);
});
app.MapPost("/api/part-requests", async (PartRequestCreate r, AppDbContext db) =>
{
    if(r.Quantity<=0)return Results.BadRequest(new{message="Quantity must be greater than zero."});
    if(!await db.JobCards.AnyAsync(x=>x.Id==r.JobCardId))return Results.BadRequest(new{message="Job card not found."});
    if(r.WorkItemId.HasValue&&!await db.WorkItems.AnyAsync(x=>x.Id==r.WorkItemId&&x.JobCardId==r.JobCardId))return Results.BadRequest(new{message="Task must belong to the job card."});
    var p=await db.PartMasters.FindAsync(r.PartMasterId);var l=await db.InventoryLocations.FindAsync(r.InventoryLocationId);if(p is null||l is null)return Results.BadRequest(new{message="Part or location not found."});
    var s=await db.PartStocks.FirstOrDefaultAsync(x=>x.PartMasterId==p.Id&&x.InventoryLocationId==l.Id);var available=s==null?0:s.OnHandQty-s.ReservedQty;
    var pr=new PartRequest{RequestNumber=$"PR-{DateTime.UtcNow:yyyyMMdd}-{(await db.PartRequests.CountAsync()+1):D5}",JobCardId=r.JobCardId,WorkItemId=r.WorkItemId,PartMasterId=p.Id,InventoryLocationId=l.Id,
        QuantityRequired=r.Quantity,Status=available>=r.Quantity?"Requested":"Awaiting Stock",WarrantyCandidate=r.WarrantyCandidate,FailedPartDisposition=r.FailedPartDisposition,RequestedBy=r.RequestedBy};
    db.PartRequests.Add(pr);Audit(db,"REQUEST","PartRequest",pr.Id,$"{pr.RequestNumber} {p.PartNumber} x {r.Quantity}",r.RequestedBy);await db.SaveChangesAsync();return Results.Ok(pr);
});
app.MapPost("/api/part-requests/{id:guid}/reserve", async (Guid id, QuantityAction r, AppDbContext db) =>
{
    var pr=await db.PartRequests.FindAsync(id);if(pr is null)return Results.NotFound();
    var p=await db.PartMasters.FindAsync(pr.PartMasterId);
    var stock=await db.PartStocks.FirstOrDefaultAsync(x=>x.PartMasterId==pr.PartMasterId&&x.InventoryLocationId==pr.InventoryLocationId);
    var qty=r.Quantity<=0?pr.QuantityRequired-pr.QuantityReserved:r.Quantity;
    if(qty<=0)return Results.BadRequest(new{message="Nothing to reserve."});
    if(stock is null||stock.OnHandQty-stock.ReservedQty<qty)return Results.Conflict(new{message="Insufficient available stock. Receive/replenish stock first."});
    stock.ReservedQty+=qty;pr.QuantityReserved+=qty;pr.Status=pr.QuantityReserved>=pr.QuantityRequired?"Reserved":"Partially Reserved";pr.UpdatedAt=DateTime.UtcNow;
    db.PartTransactions.Add(new PartTransaction{
        PartRequestId=pr.Id,PartMasterId=pr.PartMasterId,InventoryLocationId=pr.InventoryLocationId,JobCardId=pr.JobCardId,WorkItemId=pr.WorkItemId,
        PartNumber=p!.PartNumber,PartDescription=p.Description,TransactionType="Reserve",Quantity=qty,PerformedBy=r.User,UnitCost=p.StandardCost,ExtendedCost=0
    });
    Audit(db,"RESERVE","PartRequest",pr.Id,$"{p.PartNumber} x {qty}",r.User);await db.SaveChangesAsync();return Results.Ok(pr);
});

app.MapPost("/api/part-requests/{id:guid}/issue", async (Guid id, QuantityAction r, AppDbContext db) =>
{
    var pr=await db.PartRequests.FindAsync(id);if(pr is null)return Results.NotFound();
    var p=await db.PartMasters.FindAsync(pr.PartMasterId);
    var stock=await db.PartStocks.FirstOrDefaultAsync(x=>x.PartMasterId==pr.PartMasterId&&x.InventoryLocationId==pr.InventoryLocationId);
    if(stock is null)return Results.Conflict(new{message="No stock record."});
    var remaining=pr.QuantityRequired-pr.QuantityIssued;var qty=r.Quantity<=0?remaining:r.Quantity;
    if(qty<=0||qty>remaining)return Results.BadRequest(new{message="Invalid issue quantity."});
    if(pr.QuantityReserved-pr.QuantityIssued<qty)return Results.Conflict(new{message="Reserve the quantity before issue."});
    if(stock.OnHandQty<qty)return Results.Conflict(new{message="Insufficient on-hand stock."});
    stock.OnHandQty-=qty;stock.ReservedQty=Math.Max(0,stock.ReservedQty-qty);stock.UpdatedAt=DateTime.UtcNow;
    pr.QuantityIssued+=qty;pr.Status=pr.QuantityIssued>=pr.QuantityRequired?"Issued":"Partially Issued";pr.UpdatedAt=DateTime.UtcNow;
    var unitCost=p?.StandardCost??0;
    db.PartTransactions.Add(new PartTransaction{
        PartRequestId=pr.Id,PartMasterId=pr.PartMasterId,InventoryLocationId=pr.InventoryLocationId,JobCardId=pr.JobCardId,WorkItemId=pr.WorkItemId,
        PartNumber=p!.PartNumber,PartDescription=p.Description,TransactionType="Issue",Quantity=qty,PerformedBy=r.User,
        UnitCost=unitCost,ExtendedCost=Math.Round(qty*unitCost,2)
    });
    Audit(db,"ISSUE","PartRequest",pr.Id,$"{p.PartNumber} x {qty} @ {unitCost}",r.User);await db.SaveChangesAsync();return Results.Ok(pr);
});

app.MapPost("/api/part-requests/{id:guid}/return", async (Guid id, QuantityAction r, AppDbContext db) =>
{
    var pr=await db.PartRequests.FindAsync(id);if(pr is null)return Results.NotFound();
    var p=await db.PartMasters.FindAsync(pr.PartMasterId);
    var stock=await db.PartStocks.FirstOrDefaultAsync(x=>x.PartMasterId==pr.PartMasterId&&x.InventoryLocationId==pr.InventoryLocationId);
    if(stock is null)return Results.Conflict(new{message="No stock record."});
    var availableToReturn=pr.QuantityIssued-pr.QuantityReturned-pr.QuantityConsumed;var qty=r.Quantity<=0?availableToReturn:r.Quantity;
    if(qty<=0||qty>availableToReturn)return Results.BadRequest(new{message="Invalid return quantity."});
    var issueUnitCost=await db.PartTransactions.AsNoTracking()
        .Where(x=>x.PartRequestId==pr.Id&&x.TransactionType=="Issue")
        .OrderByDescending(x=>x.TransactionAt).Select(x=>(decimal?)x.UnitCost).FirstOrDefaultAsync() ?? (p?.StandardCost??0);
    stock.OnHandQty+=qty;stock.UpdatedAt=DateTime.UtcNow;pr.QuantityReturned+=qty;
    pr.Status=(pr.QuantityReturned+pr.QuantityConsumed)>=pr.QuantityIssued?"Returned":"Partially Returned";pr.UpdatedAt=DateTime.UtcNow;
    db.PartTransactions.Add(new PartTransaction{
        PartRequestId=pr.Id,PartMasterId=pr.PartMasterId,InventoryLocationId=pr.InventoryLocationId,JobCardId=pr.JobCardId,WorkItemId=pr.WorkItemId,
        PartNumber=p!.PartNumber,PartDescription=p.Description,TransactionType="Return",Quantity=qty,PerformedBy=r.User,
        UnitCost=issueUnitCost,ExtendedCost=-Math.Round(qty*issueUnitCost,2)
    });
    Audit(db,"RETURN","PartRequest",pr.Id,$"{p.PartNumber} x {qty} @ {issueUnitCost}",r.User);await db.SaveChangesAsync();return Results.Ok(pr);
});

app.MapPost("/api/part-requests/{id:guid}/consume", async (Guid id, QuantityAction r, AppDbContext db) =>
{
    var pr=await db.PartRequests.FindAsync(id);if(pr is null)return Results.NotFound();
    var p=await db.PartMasters.FindAsync(pr.PartMasterId);
    var available=pr.QuantityIssued-pr.QuantityReturned-pr.QuantityConsumed;var qty=r.Quantity<=0?available:r.Quantity;
    if(qty<=0||qty>available)return Results.BadRequest(new{message="Invalid consume quantity."});
    var issueUnitCost=await db.PartTransactions.AsNoTracking()
        .Where(x=>x.PartRequestId==pr.Id&&x.TransactionType=="Issue")
        .OrderByDescending(x=>x.TransactionAt).Select(x=>(decimal?)x.UnitCost).FirstOrDefaultAsync() ?? (p?.StandardCost??0);
    pr.QuantityConsumed+=qty;
    pr.Status=(pr.QuantityReturned+pr.QuantityConsumed)>=pr.QuantityIssued&&pr.QuantityIssued>=pr.QuantityRequired?"Consumed":"Partially Consumed";pr.UpdatedAt=DateTime.UtcNow;
    db.PartTransactions.Add(new PartTransaction{
        PartRequestId=pr.Id,PartMasterId=pr.PartMasterId,InventoryLocationId=pr.InventoryLocationId,JobCardId=pr.JobCardId,WorkItemId=pr.WorkItemId,
        PartNumber=p!.PartNumber,PartDescription=p.Description,TransactionType="Consume",Quantity=qty,PerformedBy=r.User,
        UnitCost=issueUnitCost,ExtendedCost=0
    });
    Audit(db,"CONSUME","PartRequest",pr.Id,$"{p.PartNumber} x {qty}",r.User);await db.SaveChangesAsync();return Results.Ok(pr);
});
app.MapGet("/api/parts/transactions", async (AppDbContext db) => Results.Ok(await db.PartTransactions.AsNoTracking().OrderByDescending(x=>x.TransactionAt).Take(500).ToListAsync()));
app.MapGet("/api/parts", async (AppDbContext db) => Results.Ok(await db.PartTransactions.AsNoTracking().OrderByDescending(x=>x.TransactionAt).Take(200).ToListAsync()));

app.MapPost("/api/labour", async (LabourEntry l, AppDbContext db) =>
{
    if (!await db.JobCards.AnyAsync(x=>x.Id==l.JobCardId)) return Results.BadRequest(new { message="Job card not found." });
    if(l.Hours<=0)return Results.BadRequest(new{message="Labour hours must be greater than zero."});
    if(l.TechnicianId.HasValue)
    {
        var tech=await db.Technicians.FindAsync(l.TechnicianId.Value);
        if(tech!=null){l.Technician=tech.Name;if(l.HourlyRate<=0)l.HourlyRate=tech.HourlyRate;}
    }
    l.CostAmount=Math.Round(l.Hours*l.HourlyRate,2);
    l.Id=Guid.NewGuid(); db.LabourEntries.Add(l); Audit(db,"CREATE","LabourEntry",l.Id,$"{l.Technician} {l.Hours}h @ {l.HourlyRate} = {l.CostAmount}");
    await db.SaveChangesAsync(); return Results.Created($"/api/labour/{l.Id}", l);
});

app.MapGet("/api/breakdowns", async (AppDbContext db) =>
{
    var rows=await (from b in db.Breakdowns.AsNoTracking()
                    join v in db.Vehicles.AsNoTracking() on b.VehicleId equals v.Id
                    join e in db.ServiceEvents.AsNoTracking() on (Guid?)b.Id equals e.BreakdownId into ee
                    from e in ee.DefaultIfEmpty()
                    join j in db.JobCards.AsNoTracking() on e.Id equals j.ServiceEventId into jj
                    from j in jj.DefaultIfEmpty()
                    orderby b.ReportedAt descending
                    select new { b.Id,b.BreakdownNumber,b.VehicleId,vehicle=v.RegistrationNumber,b.Priority,b.Location,b.Complaint,b.TriageDecision,b.DispatchMode,b.Status,b.ReportedAt,
                        serviceEventId=e!=null?e.Id:(Guid?)null,serviceEventNumber=e!=null?e.EventNumber:"",serviceStatus=e!=null?e.Status:"",
                        jobCardId=j!=null?j.Id:(Guid?)null,jobCardNumber=j!=null?j.JobCardNumber:"",bay=j!=null?j.Bay:"",technician=j!=null?j.Technician:"",technicianId=j!=null?j.TechnicianId:null }).ToListAsync();
    return Results.Ok(rows);
});
app.MapPost("/api/breakdowns", async (BreakdownRequest r, AppDbContext db) =>
{
    var v=await db.Vehicles.FindAsync(r.VehicleId); if(v is null) return Results.BadRequest(new { message="Vehicle not found." });
    var b=new Breakdown { VehicleId=v.Id, BreakdownNumber=$"BD-{DateTime.UtcNow:yyyy}-{(await db.Breakdowns.CountAsync()+1):D6}", Priority=r.Priority, Location=r.Location, Complaint=r.Complaint, TriageDecision=r.TriageDecision, DispatchMode=r.DispatchMode, Status="Reported" };
    v.Status="Breakdown"; db.Breakdowns.Add(b); db.VehicleAvailabilityLedger.Add(new VehicleAvailabilityLedger { VehicleId=v.Id, State="Breakdown", ReasonCode="Breakdown", SourceType="Breakdown", SourceBreakdownId=b.Id });
    Audit(db,"CREATE","Breakdown",b.Id,b.BreakdownNumber); await db.SaveChangesAsync(); return Results.Created($"/api/breakdowns/{b.Id}",b);
});
app.MapPost("/api/breakdowns/{id:guid}/convert", async (Guid id, BreakdownConvertRequest r, AppDbContext db) =>
{
    var b=await db.Breakdowns.FindAsync(id); if(b is null) return Results.NotFound();
    if (await db.ServiceEvents.AnyAsync(x=>x.BreakdownId==id)) return Results.Conflict(new { message="Already converted." });
    Technician? tech=null;if(r.TechnicianId.HasValue){tech=await db.Technicians.FindAsync(r.TechnicianId.Value);if(tech is null||!tech.IsActive)return Results.BadRequest(new{message="Select an active technician."});}
    var e=new ServiceEvent { VehicleId=b.VehicleId, BreakdownId=b.Id, EventNumber=$"SE-{DateTime.UtcNow:yyyy}-{(await db.ServiceEvents.CountAsync()+1):D6}", EventType="Breakdown", Priority=b.Priority, Status=tech is null?"Awaiting Assignment":"Assigned" };
    var j=new JobCard { ServiceEventId=e.Id, JobCardNumber=$"JC-{DateTime.UtcNow:yyyy}-{(await db.JobCards.CountAsync()+1):D6}", Status="Open",Bay=r.Bay?.Trim()??"",TechnicianId=tech?.Id,Technician=tech?.Name??"",StartedAt=null };
    b.Status="Converted";b.ResponseAt??=DateTime.UtcNow;db.ServiceEvents.Add(e);db.JobCards.Add(j);
    if(r.GenerateGeneralDiagnosis!=false)
    {
        var mr=new MaintenanceRequest{RequestNumber=b.BreakdownNumber,VehicleId=b.VehicleId,Description=$"General diagnosis for breakdown: {b.Complaint}",ComplaintCategoryCode="GENERAL",SymptomCode="OTHER",DiagnosticTemplateCode="DIAG-GENERAL",Priority=b.Priority};
        await AddDiagnosticWorkAsync(db,j,mr,b.Priority);
        var created=db.ChangeTracker.Entries<WorkItem>().Select(x=>x.Entity).Where(x=>x.JobCardId==j.Id).ToList();
        foreach(var task in created)if(tech is not null){task.AssignedToTechnicianId=tech.Id;task.AssignedTo=tech.Name;task.Status="Assigned";}
        db.WorkLogEntries.Add(new WorkLogEntry{JobCardId=j.Id,WorkItemId=created.FirstOrDefault()?.Id,EntryType="General Diagnosis Added",Comment=$"Created from {b.BreakdownNumber}: {b.Complaint}",CreatedBy=r.ConvertedBy??"Service Supervisor",CreatedRole="Supervisor"});
    }
    var by=string.IsNullOrWhiteSpace(r.ConvertedBy)?"Service Supervisor":r.ConvertedBy.Trim();
    db.WorkLogEntries.Add(new WorkLogEntry{JobCardId=j.Id,EntryType="Breakdown Converted",Comment=$"{b.BreakdownNumber} → {e.EventNumber} → {j.JobCardNumber}",CreatedBy=by,CreatedRole="Supervisor"});
    Audit(db,"CONVERT","Breakdown",b.Id,$"{b.BreakdownNumber}->{e.EventNumber}->{j.JobCardNumber}",by);await db.SaveChangesAsync();return Results.Ok(new { breakdownNumber=b.BreakdownNumber,serviceEvent=e,jobCard=j });
});

app.MapPost("/api/job-cards/{id:guid}/qc", async (Guid id, QcRequest r, AppDbContext db) =>
{
    if (!await db.JobCards.AnyAsync(x=>x.Id==id)) return Results.NotFound();
    var qc=new QcInspection { JobCardId=id, Inspector=r.Inspector, Result=r.Result, RoadTestRequired=r.RoadTestRequired, RoadTestPassed=r.RoadTestPassed, Remarks=r.Remarks, InspectedAt=DateTime.UtcNow };
    db.QcInspections.Add(qc); Audit(db,"QC","JobCard",id,r.Result); await db.SaveChangesAsync(); return Results.Ok(qc);
});

app.MapPost("/api/service-events/{id:guid}/release", async (Guid id, ReleaseRequest r, AppDbContext db) =>
{
    var e=await db.ServiceEvents.FindAsync(id); if(e is null) return Results.NotFound();
    var j=await db.JobCards.FirstOrDefaultAsync(x=>x.ServiceEventId==id); if(j is null) return Results.BadRequest(new { message="Job card not found." });
    if (await db.WorkItems.AnyAsync(x=>x.JobCardId==j.Id && x.Status!="Completed")) return Results.Conflict(new { message="All work items must be completed before release." });
    if (await db.PartRequests.AnyAsync(x=>x.JobCardId==j.Id && x.Status!="Consumed" && x.Status!="Returned" && x.Status!="Cancelled")) return Results.Conflict(new { message="Open parts requests must be resolved before release." });
    var latestQc=await db.QcInspections.Where(x=>x.JobCardId==j.Id).OrderByDescending(x=>x.InspectedAt).FirstOrDefaultAsync();
    if (latestQc is null || latestQc.Result!="Pass" || (latestQc.RoadTestRequired && !latestQc.RoadTestPassed))
        return Results.Conflict(new { message="Passing QC is required before release." });
    var v=await db.Vehicles.FindAsync(e.VehicleId); if(v is null) return Results.BadRequest();
    var rel=new VehicleRelease { ServiceEventId=e.Id, VehicleId=v.Id, ReleaseStatus="Released", ReleasedBy=r.ReleasedBy, ReleasedAt=DateTime.UtcNow, Remarks=r.Remarks };
    e.Status="Closed"; e.ClosedAt=DateTime.UtcNow; j.Status="Completed"; j.CompletedAt=DateTime.UtcNow; v.Status="Available";
    var openLedger=await db.VehicleAvailabilityLedger.Where(x=>x.VehicleId==v.Id && x.EndAt==null).OrderByDescending(x=>x.StartAt).FirstOrDefaultAsync();
    if(openLedger!=null) openLedger.EndAt=DateTime.UtcNow;
    db.VehicleAvailabilityLedger.Add(new VehicleAvailabilityLedger { VehicleId=v.Id, State="Available", StartAt=DateTime.UtcNow, ReasonCode="Released", SourceType="ServiceEvent", SourceServiceEventId=e.Id });
    db.VehicleReleases.Add(rel); Audit(db,"RELEASE","Vehicle",v.Id,e.EventNumber,r.ReleasedBy);
    if(e.PmObligationId.HasValue)
    {
        var po=await db.PmObligations.FindAsync(e.PmObligationId.Value);
        if(po is not null){po.Status="Completed";po.CompletedAt=DateTime.UtcNow;await db.SaveChangesAsync();await GenerateNextPmObligationAsync(db,po,v);}
    }
    await db.SaveChangesAsync(); return Results.Ok(rel);
});

app.MapGet("/api/audit", async (AppDbContext db) => Results.Ok(await db.AuditEvents.AsNoTracking().OrderByDescending(x=>x.OccurredAt).Take(250).ToListAsync()));

app.MapGet("/api/warranty/{vin}", async (string vin, AppDbContext db) =>
{
    var v=await db.Vehicles.FirstOrDefaultAsync(x=>x.Vin==vin || x.RegistrationNumber==vin);
    if(v is null) return Results.NotFound();
    return Results.Ok(await db.WarrantyEntitlements.AsNoTracking().Where(x=>x.VehicleId==v.Id).ToListAsync());
});
app.MapGet("/api/campaigns/open", async (AppDbContext db) => Results.Ok(await db.Campaigns.AsNoTracking().Where(x=>x.Status=="Active").ToListAsync()));
app.MapGet("/api/documents/{vin}", async (string vin, AppDbContext db) =>
{
    var v=await db.Vehicles.FirstOrDefaultAsync(x=>x.Vin==vin || x.RegistrationNumber==vin); if(v is null) return Results.NotFound();
    return Results.Ok(await db.VehicleDocuments.AsNoTracking().Where(x=>x.VehicleId==v.Id).OrderByDescending(x=>x.UploadedAt).ToListAsync());
});
app.MapGet("/api/search", async (string? q, AppDbContext db) =>
{
    var term=(q??"").Trim().ToLower();
    if(string.IsNullOrWhiteSpace(term)) return Results.Ok(new { query=q??"", results=Array.Empty<object>() });

    var vehicles=await db.Vehicles.AsNoTracking()
        .Where(x=>x.Vin.ToLower().Contains(term)||x.RegistrationNumber.ToLower().Contains(term)||x.Model.ToLower().Contains(term))
        .Take(15)
        .Select(x=>new { type="Vehicle",key=x.RegistrationNumber,title=x.Model+" · "+x.Vin,status=x.Status,url="/vehicle?id="+x.Id }).ToListAsync();

    var workOrders=await (from j in db.JobCards.AsNoTracking()
                          join e in db.ServiceEvents.AsNoTracking() on j.ServiceEventId equals e.Id
                          join v in db.Vehicles.AsNoTracking() on e.VehicleId equals v.Id
                          where j.JobCardNumber.ToLower().Contains(term)||v.RegistrationNumber.ToLower().Contains(term)||v.Vin.ToLower().Contains(term)
                          orderby j.StartedAt descending
                          select new {type="Work Order",key=j.JobCardNumber,title=v.RegistrationNumber+" · "+e.EventType,status=j.Status,url="/service-workspace/"+j.Id}).Take(15).ToListAsync();

    var events=await (from e in db.ServiceEvents.AsNoTracking()
                      join v in db.Vehicles.AsNoTracking() on e.VehicleId equals v.Id
                      where e.EventNumber.ToLower().Contains(term)||v.RegistrationNumber.ToLower().Contains(term)||v.Vin.ToLower().Contains(term)
                      orderby e.OpenedAt descending
                      select new {type="Service Event",key=e.EventNumber,title=v.RegistrationNumber+" · "+e.EventType,status=e.Status,url="/service"}).Take(10).ToListAsync();

    var requests=await (from r in db.MaintenanceRequests.AsNoTracking()
                        join v in db.Vehicles.AsNoTracking() on r.VehicleId equals v.Id
                        where r.RequestNumber.ToLower().Contains(term)||v.RegistrationNumber.ToLower().Contains(term)||v.Vin.ToLower().Contains(term)||r.Description.ToLower().Contains(term)
                        orderby r.RequestedAt descending
                        select new {type="Maintenance Request",key=r.RequestNumber,title=v.RegistrationNumber+" · "+r.Description,status=r.Status,url="/maintenance-requests"}).Take(10).ToListAsync();

    var results=new List<object>();
    results.AddRange(vehicles);results.AddRange(workOrders);results.AddRange(events);results.AddRange(requests);
    return Results.Ok(new { query=q??"", results });
});


app.MapGet("/api/vehicle360/{id:guid}", async (Guid id, AppDbContext db) =>
{
    var v=await db.Vehicles.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==id); if(v is null) return Results.NotFound();
    var nextPm=await db.PmObligations.AsNoTracking().Where(x=>x.VehicleId==id && x.Status!="Completed").OrderBy(x=>x.DueDate).ThenBy(x=>x.DueReading).FirstOrDefaultAsync();
    var defects=await db.Defects.CountAsync(x=>x.VehicleId==id && x.Disposition!="Closed");
    var warranty=await db.WarrantyEntitlements.AnyAsync(x=>x.VehicleId==id && x.Status=="Active" && x.EndDate>=DateTime.UtcNow);
    var campaigns=await db.VehicleCampaigns.CountAsync(x=>x.VehicleId==id && x.Status!="Completed");
    var docs=await db.VehicleDocuments.CountAsync(x=>x.VehicleId==id && x.Status=="Active");
    var lastService=await db.ServiceEvents.AsNoTracking().Where(x=>x.VehicleId==id && x.ClosedAt!=null).OrderByDescending(x=>x.ClosedAt).Select(x=>x.ClosedAt).FirstOrDefaultAsync();
    var appointment=await db.Appointments.AsNoTracking().Where(x=>x.VehicleId==id&&!new[]{"Completed","Cancelled","No-show"}.Contains(x.Status)).OrderByDescending(x=>x.StartAt).Select(x=>new{x.Id,x.AppointmentNumber,x.StartAt,x.ServiceCentre,x.Bay,x.Technician,x.Status,x.AppointmentType,x.Priority,x.Reason,x.PmObligationId}).FirstOrDefaultAsync();
    var currentService=await(from e in db.ServiceEvents.AsNoTracking()
        join j in db.JobCards.AsNoTracking() on e.Id equals j.ServiceEventId into jj from j in jj.DefaultIfEmpty()
        join b in db.Breakdowns.AsNoTracking() on e.BreakdownId equals (Guid?)b.Id into bb from b in bb.DefaultIfEmpty()
        where e.VehicleId==id&&e.Status!="Closed" orderby e.OpenedAt descending
        select new{serviceEventId=e.Id,e.EventNumber,e.EventType,eventStatus=e.Status,e.OpenedAt,e.Priority,
            jobCardId=j!=null?j.Id:(Guid?)null,jobCardNumber=j!=null?j.JobCardNumber:"",jobStatus=j!=null?j.Status:"",
            bay=j!=null?j.Bay:"",technician=j!=null?j.Technician:"",complaint=b!=null?b.Complaint:"",
            breakdownNumber=b!=null?b.BreakdownNumber:""}).FirstOrDefaultAsync();
    object? serviceProgress=null;
    if(currentService?.jobCardId is Guid currentJobId)
    {
        var tasksTotal=await db.WorkItems.CountAsync(x=>x.JobCardId==currentJobId);
        var tasksCompleted=await db.WorkItems.CountAsync(x=>x.JobCardId==currentJobId&&x.Status=="Completed");
        var openTaskIds=await db.WorkTemplateInstances.Where(x=>x.JobCardId==currentJobId).Select(x=>x.Id).ToListAsync();
        var checksTotal=await db.WorkTemplateFieldInstances.CountAsync(x=>openTaskIds.Contains(x.WorkTemplateInstanceId));
        var checksCompleted=await db.WorkTemplateFieldInstances.CountAsync(x=>openTaskIds.Contains(x.WorkTemplateInstanceId)&&x.Result!="Pending"&&x.Value!="");
        var partsWaiting=await db.PartRequests.CountAsync(x=>x.JobCardId==currentJobId&&(x.Status=="Requested"||x.Status=="Awaiting Stock"));
        var qc=await db.QcInspections.Where(x=>x.JobCardId==currentJobId).OrderByDescending(x=>x.InspectedAt).Select(x=>x.Result).FirstOrDefaultAsync()??"Pending";
        var stage=string.IsNullOrWhiteSpace(currentService.technician)?"Awaiting Assignment":partsWaiting>0?"Parts Waiting":tasksCompleted<tasksTotal?"Work In Progress":checksCompleted<checksTotal?"Checks In Progress":qc!="Pass"?"QC Pending":"Ready for Release";
        serviceProgress=new{currentService.serviceEventId,currentService.EventNumber,currentService.EventType,currentService.eventStatus,currentService.OpenedAt,currentService.Priority,currentService.jobCardId,currentService.jobCardNumber,currentService.jobStatus,currentService.bay,currentService.technician,currentService.complaint,currentService.breakdownNumber,stage,tasksTotal,tasksCompleted,checksTotal,checksCompleted,partsWaiting,qcStatus=qc};
    }
    var openRequests=await db.MaintenanceRequests.AsNoTracking().Where(x=>x.VehicleId==id&&x.Status!="Closed"&&x.Status!="Cancelled").OrderByDescending(x=>x.RequestedAt).Select(x=>new{x.Id,x.RequestNumber,x.RequestType,x.Description,x.Priority,x.Status,x.RequestedAt}).Take(10).ToListAsync();
    var history=await(from e in db.ServiceEvents.AsNoTracking() join j in db.JobCards.AsNoTracking() on e.Id equals j.ServiceEventId into jj from j in jj.DefaultIfEmpty() where e.VehicleId==id orderby e.OpenedAt descending select new{e.Id,e.EventNumber,e.EventType,e.Status,e.OpenedAt,e.ClosedAt,jobCardId=j!=null?j.Id:(Guid?)null,jobCardNumber=j!=null?j.JobCardNumber:""}).Take(10).ToListAsync();
    return Results.Ok(new { v.Id,v.Vin,registration=v.RegistrationNumber,v.Model,v.Variant,availability=v.Status,v.OdometerKm,v.OperatingHours,v.BatterySoc,
        nextPm=nextPm==null?"-":(nextPm.DueDate.HasValue?nextPm.DueDate.Value.ToString("dd MMM yyyy"):(nextPm.DueReading?.ToString("0")+" km")),
        openDefects=defects,activeWarranty=warranty,openCampaigns=campaigns,lastService=lastService,documents=docs,appointment,currentService=serviceProgress,openRequests,history });
});

app.MapGet("/api/checklists/{jobCardId:guid}", async (Guid jobCardId, AppDbContext db) =>
    Results.Ok(await db.ChecklistExecutions.AsNoTracking().Where(x=>x.JobCardId==jobCardId).OrderBy(x=>x.ItemCode).ToListAsync()));

app.MapPost("/api/checklists/{jobCardId:guid}", async (Guid jobCardId, ChecklistRequest r, AppDbContext db) =>
{
    var job=await db.JobCards.FindAsync(jobCardId);if(job is null)return Results.NotFound();
    var c=new ChecklistExecution{JobCardId=jobCardId,WorkItemId=r.WorkItemId,ChecklistCode=r.ChecklistCode,ItemCode=r.ItemCode,ItemText=r.ItemText,
        Result=r.Result,Remarks=r.Remarks,ExecutedBy=r.ExecutedBy,ExecutedAt=DateTime.UtcNow,IsMandatory=r.IsMandatory};
    db.ChecklistExecutions.Add(c);Audit(db,"EXECUTE","Checklist",c.Id,$"{r.ItemCode}:{r.Result}",r.ExecutedBy);
    Defect? defect=null;
    if(string.Equals(r.Result,"Fail",StringComparison.OrdinalIgnoreCase))
    {
        var evt=await db.ServiceEvents.FindAsync(job.ServiceEventId);
        if(evt!=null){defect=new Defect{VehicleId=evt.VehicleId,JobCardId=jobCardId,WorkItemId=r.WorkItemId,DefectNumber=$"DF-{DateTime.UtcNow:yyyy}-{(await db.Defects.CountAsync()+1):D6}",
            Category="Checklist",Severity=r.DefectSeverity??"Major",Description=r.DefectDescription??r.ItemText,Disposition="Open",FailureCode=r.FailureCode??""};
            db.Defects.Add(defect);Audit(db,"CREATE","Defect",defect.Id,$"{defect.DefectNumber} from checklist");}
    }
    await db.SaveChangesAsync();return Results.Ok(new{checklist=c,defect});
});
app.MapGet("/api/defects", async (AppDbContext db) =>
{
    var rows=await (from d in db.Defects.AsNoTracking() join v in db.Vehicles.AsNoTracking() on d.VehicleId equals v.Id orderby d.ReportedAt descending
                    select new { d.Id,d.DefectNumber,d.VehicleId,vehicle=v.RegistrationNumber,d.JobCardId,d.WorkItemId,d.Category,d.Severity,d.Description,d.Disposition,d.FailureCode,d.RcaSummary,d.ReportedAt,d.ClosedAt }).ToListAsync();
    return Results.Ok(rows);
});
app.MapPost("/api/defects", async (DefectRequest r, AppDbContext db) =>
{
    var j=await db.JobCards.FindAsync(r.JobCardId); if(j is null) return Results.BadRequest(new{message="Job card not found."});
    var e=await db.ServiceEvents.FindAsync(j.ServiceEventId); if(e is null) return Results.BadRequest();
    var d=new Defect{VehicleId=e.VehicleId,JobCardId=j.Id,WorkItemId=r.WorkItemId,DefectNumber=$"DF-{DateTime.UtcNow:yyyy}-{(await db.Defects.CountAsync()+1):D6}",Category=r.Category,Severity=r.Severity,Description=r.Description,Disposition="Open",FailureCode=r.FailureCode};
    db.Defects.Add(d); Audit(db,"CREATE","Defect",d.Id,d.DefectNumber); await db.SaveChangesAsync(); return Results.Created($"/api/defects/{d.Id}",d);
});
app.MapPut("/api/defects/{id:guid}/close", async (Guid id, DefectCloseRequest r, AppDbContext db) =>
{
    var d=await db.Defects.FindAsync(id); if(d is null)return Results.NotFound(); d.Disposition="Closed";d.RcaSummary=r.RcaSummary;d.ClosedAt=DateTime.UtcNow;Audit(db,"CLOSE","Defect",d.Id,r.RcaSummary);await db.SaveChangesAsync();return Results.Ok(d);
});


app.MapPut("/api/technicians/{id:guid}", async (Guid id, Technician r, AppDbContext db) =>
{
    var t=await db.Technicians.FindAsync(id);if(t is null)return Results.NotFound();
    t.Name=r.Name;t.ServiceCentre=r.ServiceCentre;t.SkillCodes=r.SkillCodes;t.HvAuthorized=r.HvAuthorized;t.HvAuthorizationValidUntil=r.HvAuthorizationValidUntil;t.HourlyRate=r.HourlyRate;t.IsActive=r.IsActive;
    Audit(db,"UPDATE","Technician",t.Id,$"{t.EmployeeCode} rate={t.HourlyRate}");await db.SaveChangesAsync();return Results.Ok(t);
});
app.MapGet("/api/technicians", async (AppDbContext db)=>Results.Ok(await db.Technicians.AsNoTracking().OrderBy(x=>x.EmployeeCode).ToListAsync()));
app.MapPost("/api/technicians", async (Technician t, AppDbContext db)=>{t.Id=Guid.NewGuid();db.Technicians.Add(t);Audit(db,"CREATE","Technician",t.Id,t.EmployeeCode);await db.SaveChangesAsync();return Results.Created($"/api/technicians/{t.Id}",t);});

app.MapGet("/api/availability", async (AppDbContext db)=>
{
    var rows=await (from a in db.VehicleAvailabilityLedger.AsNoTracking() join v in db.Vehicles.AsNoTracking() on a.VehicleId equals v.Id orderby a.StartAt descending
                    select new {a.Id,a.VehicleId,vehicle=v.RegistrationNumber,a.State,a.StartAt,a.EndAt,a.ReasonCode,a.SourceType,a.RuleVersion}).Take(300).ToListAsync();
    return Results.Ok(rows);
});
app.MapPost("/api/availability/correct", async (AvailabilityCorrection r, AppDbContext db)=>
{
    var prev=await db.VehicleAvailabilityLedger.FindAsync(r.CorrectsLedgerId); if(prev is null)return Results.NotFound();
    var a=new VehicleAvailabilityLedger{VehicleId=prev.VehicleId,State=r.State,StartAt=r.StartAt,EndAt=r.EndAt,ReasonCode=r.ReasonCode,SourceType="Correction",ChangedBy=r.ChangedBy,RuleVersion="AVL-1.0",CorrectsLedgerId=prev.Id};
    db.VehicleAvailabilityLedger.Add(a);Audit(db,"CORRECT","Availability",a.Id,r.ReasonCode,r.ChangedBy);await db.SaveChangesAsync();return Results.Ok(a);
});

app.MapGet("/api/offhire", async (AppDbContext db)=>
{
    var rows=await (from o in db.OffHireRecords.AsNoTracking() join v in db.Vehicles.AsNoTracking() on o.VehicleId equals v.Id orderby o.StartAt descending
                    select new{o.Id,o.VehicleId,vehicle=v.RegistrationNumber,o.StartAt,o.ExpectedReturnAt,o.EndAt,o.ReasonCode,o.RequestedBy,o.ApprovedBy,o.Status}).ToListAsync();
    return Results.Ok(rows);
});
app.MapPost("/api/offhire", async (OffHireRequest r, AppDbContext db)=>
{
    var v=await db.Vehicles.FindAsync(r.VehicleId);if(v is null)return Results.BadRequest(new{message="Vehicle not found."});
    var o=new OffHireRecord{VehicleId=v.Id,StartAt=r.StartAt,ExpectedReturnAt=r.ExpectedReturnAt,ReasonCode=r.ReasonCode,RequestedBy=r.RequestedBy,Status="Pending Approval"};
    db.OffHireRecords.Add(o);Audit(db,"REQUEST","OffHire",o.Id,r.ReasonCode,r.RequestedBy);await db.SaveChangesAsync();return Results.Ok(o);
});
app.MapPost("/api/offhire/{id:guid}/approve", async (Guid id, ApprovalRequest r, AppDbContext db)=>
{
    var o=await db.OffHireRecords.FindAsync(id);if(o is null)return Results.NotFound();o.Status="Approved";o.ApprovedBy=r.User;
    var v=await db.Vehicles.FindAsync(o.VehicleId);if(v!=null)v.Status="Off-Hire";Audit(db,"APPROVE","OffHire",o.Id,o.ReasonCode,r.User);await db.SaveChangesAsync();return Results.Ok(o);
});
app.MapPost("/api/offhire/{id:guid}/recommission", async (Guid id, RecommissionRequest r, AppDbContext db)=>
{
    var o=await db.OffHireRecords.FindAsync(id);if(o is null)return Results.NotFound();
    var ri=new RecommissioningInspection{OffHireRecordId=o.Id,VehicleId=o.VehicleId,Inspector=r.Inspector,Result=r.Result,Remarks=r.Remarks,InspectedAt=DateTime.UtcNow};
    db.RecommissioningInspections.Add(ri);
    if(r.Result=="Pass"){o.Status="Closed";o.EndAt=DateTime.UtcNow;var v=await db.Vehicles.FindAsync(o.VehicleId);if(v!=null)v.Status="Available";}
    Audit(db,"RECOMMISSION","OffHire",o.Id,r.Result,r.Inspector);await db.SaveChangesAsync();return Results.Ok(ri);
});

app.MapGet("/api/warranty", async (AppDbContext db)=>
{
    var rows=await (from w in db.WarrantyEntitlements.AsNoTracking() join v in db.Vehicles.AsNoTracking() on w.VehicleId equals v.Id orderby w.EndDate
                    select new{w.Id,w.VehicleId,vehicle=v.RegistrationNumber,w.EntitlementType,w.ReferenceNo,w.StartDate,w.EndDate,w.OdometerLimitKm,w.Status,w.CoverageNotes}).ToListAsync();
    return Results.Ok(rows);
});
app.MapPost("/api/warranty", async (WarrantyEntitlement w, AppDbContext db)=>{w.Id=Guid.NewGuid();db.WarrantyEntitlements.Add(w);Audit(db,"CREATE","Warranty",w.Id,w.ReferenceNo);await db.SaveChangesAsync();return Results.Ok(w);});

app.MapGet("/api/campaigns", async (AppDbContext db)=>Results.Ok(await db.Campaigns.AsNoTracking().OrderByDescending(x=>x.EffectiveFrom).ToListAsync()));
app.MapPost("/api/campaigns", async (Campaign c, AppDbContext db)=>{c.Id=Guid.NewGuid();db.Campaigns.Add(c);Audit(db,"CREATE","Campaign",c.Id,c.CampaignCode);await db.SaveChangesAsync();return Results.Ok(c);});
app.MapPost("/api/campaigns/{campaignId:guid}/vehicles/{vehicleId:guid}", async (Guid campaignId,Guid vehicleId,AppDbContext db)=>
{
    if(!await db.Campaigns.AnyAsync(x=>x.Id==campaignId)||!await db.Vehicles.AnyAsync(x=>x.Id==vehicleId))return Results.BadRequest();
    if(await db.VehicleCampaigns.AnyAsync(x=>x.CampaignId==campaignId&&x.VehicleId==vehicleId))return Results.Conflict(new{message="Vehicle already assigned."});
    var vc=new VehicleCampaign{CampaignId=campaignId,VehicleId=vehicleId};db.VehicleCampaigns.Add(vc);Audit(db,"ASSIGN","CampaignVehicle",vc.Id,$"{campaignId}/{vehicleId}");await db.SaveChangesAsync();return Results.Ok(vc);
});

app.MapGet("/api/documents", async (AppDbContext db)=>
{
    var rows=await (from d in db.VehicleDocuments.AsNoTracking() join v in db.Vehicles.AsNoTracking() on d.VehicleId equals v.Id orderby d.UploadedAt descending
                    select new{d.Id,d.VehicleId,vehicle=v.RegistrationNumber,d.DocumentType,d.FileName,d.StorageReference,d.UploadedBy,d.UploadedAt,d.Status}).ToListAsync();
    return Results.Ok(rows);
});
app.MapPost("/api/documents", async (VehicleDocument d, AppDbContext db)=>{d.Id=Guid.NewGuid();d.UploadedAt=DateTime.UtcNow;db.VehicleDocuments.Add(d);Audit(db,"UPLOAD","Document",d.Id,d.FileName,d.UploadedBy);await db.SaveChangesAsync();return Results.Ok(d);});

app.MapGet("/api/sla", async (AppDbContext db)=>
{
    var rows=await (from s in db.SlaClocks.AsNoTracking() join e in db.ServiceEvents.AsNoTracking() on s.ServiceEventId equals e.Id orderby s.StartedAt descending
                    select new{s.Id,s.ServiceEventId,eventNo=e.EventNumber,e.Priority,s.ClockType,s.StartedAt,s.DueAt,s.PausedAt,s.TotalPausedMinutes,s.PauseReason,s.Status}).ToListAsync();
    return Results.Ok(rows);
});
app.MapPost("/api/sla/{serviceEventId:guid}", async (Guid serviceEventId,SlaStartRequest r,AppDbContext db)=>
{
    if(!await db.ServiceEvents.AnyAsync(x=>x.Id==serviceEventId))return Results.NotFound();
    var s=new SlaClock{ServiceEventId=serviceEventId,ClockType=r.ClockType,StartedAt=DateTime.UtcNow,DueAt=DateTime.UtcNow.AddMinutes(r.TargetMinutes),Status="Running"};
    db.SlaClocks.Add(s);Audit(db,"START","SLA",s.Id,r.ClockType);await db.SaveChangesAsync();return Results.Ok(s);
});
app.MapPost("/api/sla/{id:guid}/pause", async (Guid id,PauseRequest r,AppDbContext db)=>{var s=await db.SlaClocks.FindAsync(id);if(s is null)return Results.NotFound();s.PausedAt=DateTime.UtcNow;s.PauseReason=r.Reason;s.Status="Paused";Audit(db,"PAUSE","SLA",s.Id,r.Reason);await db.SaveChangesAsync();return Results.Ok(s);});
app.MapPost("/api/sla/{id:guid}/resume", async (Guid id,AppDbContext db)=>{var s=await db.SlaClocks.FindAsync(id);if(s is null)return Results.NotFound();if(s.PausedAt.HasValue){s.TotalPausedMinutes+=(int)(DateTime.UtcNow-s.PausedAt.Value).TotalMinutes;s.PausedAt=null;}s.Status="Running";Audit(db,"RESUME","SLA",s.Id,"");await db.SaveChangesAsync();return Results.Ok(s);});

app.MapGet("/api/quality", async (AppDbContext db)=>
{
    var qcs=await db.QcInspections.AsNoTracking().ToListAsync();var total=qcs.Count;var pass=qcs.Count(x=>x.Result=="Pass");
    var repeats=await db.RepeatFailureMatches.CountAsync(x=>x.IsRepeat);var openRca=await db.Defects.CountAsync(x=>x.Disposition!="Closed"&&x.RcaSummary=="");
    var ftf=await db.FirstTimeFixResults.AsNoTracking().ToListAsync();var eligible=ftf.Count(x=>x.Eligible);var ftfPct=eligible==0?100:Math.Round(ftf.Count(x=>x.Eligible&&x.Passed)*100.0/eligible,1);
    return Results.Ok(new{firstTimeFix=ftfPct,repeatFailures=repeats,openRca,qcPass=total==0?100:Math.Round(pass*100.0/total,1)});
});


// ---------------- v1.5 Functional Fleet Maintenance Baseline ----------------
app.MapGet("/api/maintenance-requests/config", async (AppDbContext db) =>
{
    var categories=await db.MasterOptions.AsNoTracking().Where(x=>x.Category=="COMPLAINT_CATEGORY"&&x.IsActive).OrderBy(x=>x.SortOrder).Select(x=>new{x.Code,x.Name,templateCode=x.Value,x.Description}).ToListAsync();
    var symptoms=await db.MasterOptions.AsNoTracking().Where(x=>x.Category=="SYMPTOM"&&x.IsActive).OrderBy(x=>x.SortOrder).Select(x=>new{x.Code,x.Name,x.Description}).ToListAsync();
    var centres=await db.ServiceCentreMasters.AsNoTracking().Where(x=>x.IsActive).OrderBy(x=>x.Name).Select(x=>new{x.Id,x.CentreCode,x.Name,x.City,x.State,x.BayCount}).ToListAsync();
    return Results.Ok(new{categories,symptoms,centres});
});

app.MapGet("/api/maintenance-requests", async (string? status, Guid? vehicleId, AppDbContext db) =>
{
    var q=db.MaintenanceRequests.AsNoTracking().AsQueryable();
    if(!string.IsNullOrWhiteSpace(status))q=q.Where(x=>x.Status==status);
    if(vehicleId.HasValue)q=q.Where(x=>x.VehicleId==vehicleId.Value);
    var rows=await (from r in q join v in db.Vehicles.AsNoTracking() on r.VehicleId equals v.Id
        orderby r.RequestedAt descending select new{r.Id,r.RequestNumber,r.VehicleId,vehicle=v.RegistrationNumber,r.SourceType,r.SourceReference,
        r.RequestType,r.ComplaintCategoryCode,r.SymptomCode,r.DiagnosticTemplateCode,r.Priority,r.Description,r.Status,r.RequestedBy,r.RequestedAt,r.TargetDate,r.JobCardId}).ToListAsync();
    return Results.Ok(rows);
});
app.MapPost("/api/maintenance-requests", async (MaintenanceRequestCreate r, AppDbContext db) =>
{
    if(!await db.Vehicles.AnyAsync(x=>x.Id==r.VehicleId))return Results.BadRequest(new{message="Vehicle not found."});
    var category=(r.ComplaintCategoryCode??"GENERAL").Trim().ToUpperInvariant();
    var mapping=await db.MasterOptions.AsNoTracking().FirstOrDefaultAsync(x=>x.Category=="COMPLAINT_CATEGORY"&&x.Code==category&&x.IsActive);
    var templateCode=string.IsNullOrWhiteSpace(mapping?.Value)?"DIAG-GENERAL":mapping!.Value;
    var mr=new MaintenanceRequest{RequestNumber=$"MR-{DateTime.UtcNow:yyyyMMdd}-{(await db.MaintenanceRequests.CountAsync()+1):D5}",VehicleId=r.VehicleId,
        SourceType=r.SourceType,SourceReference=r.SourceReference,RequestType=r.RequestType,ComplaintCategoryCode=category,
        SymptomCode=string.IsNullOrWhiteSpace(r.SymptomCode)?"OTHER":r.SymptomCode.Trim().ToUpperInvariant(),DiagnosticTemplateCode=templateCode,
        Priority=r.Priority,Description=r.Description,Status="Open",RequestedBy=r.RequestedBy,RequestedAt=DateTime.UtcNow,TargetDate=r.TargetDate};
    db.MaintenanceRequests.Add(mr);Audit(db,"CREATE","MaintenanceRequest",mr.Id,$"{mr.RequestNumber}:{mr.Description}",r.RequestedBy);await db.SaveChangesAsync();return Results.Created($"/api/maintenance-requests/{mr.Id}",mr);
});
app.MapPut("/api/maintenance-requests/{id:guid}/status", async (Guid id,StatusRequest r,AppDbContext db)=>{var x=await db.MaintenanceRequests.FindAsync(id);if(x is null)return Results.NotFound();x.Status=r.Status;Audit(db,"STATUS","MaintenanceRequest",x.Id,r.Status);await db.SaveChangesAsync();return Results.Ok(x);});

app.MapPost("/api/work-orders/from-requests", async (CreateWorkOrderRequest r,AppDbContext db)=>
{
    if(r.RequestIds is null||r.RequestIds.Length==0)return Results.BadRequest(new{message="Select at least one maintenance request."});
    var reqs=await db.MaintenanceRequests.Where(x=>r.RequestIds.Contains(x.Id)).ToListAsync();
    if(reqs.Count!=r.RequestIds.Length)return Results.BadRequest(new{message="One or more requests were not found."});
    if(reqs.Any(x=>x.JobCardId!=null))return Results.Conflict(new{message="One or more requests are already linked to a work order."});
    var vehicleId=reqs[0].VehicleId;if(reqs.Any(x=>x.VehicleId!=vehicleId))return Results.BadRequest(new{message="All requests grouped into a work order must belong to the same vehicle."});
    var vehicle=await db.Vehicles.FindAsync(vehicleId);if(vehicle is null)return Results.BadRequest();
    var e=new ServiceEvent{VehicleId=vehicleId,EventNumber=$"SE-{DateTime.UtcNow:yyyy}-{(await db.ServiceEvents.CountAsync()+1):D6}",EventType="Maintenance",Priority=r.Priority,Status="Open"};
    var j=new JobCard{ServiceEventId=e.Id,JobCardNumber=$"WO-{DateTime.UtcNow:yyyy}-{(await db.JobCards.CountAsync()+1):D6}",Status="Open",Bay=r.Bay,TechnicianId=r.TechnicianId,Technician=r.Technician,StartedAt=null};
    db.ServiceEvents.Add(e);db.JobCards.Add(j);foreach(var x in reqs){x.JobCardId=j.Id;x.Status="Converted";}
    Audit(db,"CREATE","WorkOrder",j.Id,$"{j.JobCardNumber} from {reqs.Count} request(s)",r.CreatedBy);await db.SaveChangesAsync();return Results.Ok(new{workOrder=j,serviceEvent=e});
});

app.MapGet("/api/work-orders", async (AppDbContext db)=>
{
    var rows=await (from j in db.JobCards.AsNoTracking() join e in db.ServiceEvents.AsNoTracking() on j.ServiceEventId equals e.Id join v in db.Vehicles.AsNoTracking() on e.VehicleId equals v.Id
        orderby j.StartedAt descending select new{j.Id,workOrderNumber=j.JobCardNumber,vehicle=v.RegistrationNumber,e.VehicleId,e.EventNumber,e.EventType,e.Priority,j.Status,j.Bay,j.Technician,j.StartedAt,j.CompletedAt,
        requestCount=db.MaintenanceRequests.Count(r=>r.JobCardId==j.Id),taskCount=db.WorkItems.Count(t=>t.JobCardId==j.Id),openDefects=db.Defects.Count(d=>d.JobCardId==j.Id&&d.Disposition!="Closed")}).ToListAsync();return Results.Ok(rows);
});

app.MapGet("/api/service-task-master", async (AppDbContext db)=>Results.Ok(await db.ServiceTaskMasters.AsNoTracking().OrderBy(x=>x.TaskCode).ToListAsync()));
app.MapPost("/api/service-task-master", async (ServiceTaskMasterCreate r,AppDbContext db)=>
{
    if(await db.ServiceTaskMasters.AnyAsync(x=>x.TaskCode==r.TaskCode))return Results.Conflict(new{message="Task code already exists."});
    var x=new ServiceTaskMaster{TaskCode=r.TaskCode,Name=r.Name,Category=r.Category,Description=r.Description,StandardHours=r.StandardHours,RequiredSkillCode=r.RequiredSkillCode,
        RequiresHvAuthorization=r.RequiresHvAuthorization,RequiresQc=r.RequiresQc,ChecklistCode=r.ChecklistCode,IsActive=true};db.ServiceTaskMasters.Add(x);Audit(db,"CREATE","ServiceTaskMaster",x.Id,x.TaskCode);await db.SaveChangesAsync();return Results.Ok(x);
});
app.MapPost("/api/service-task-master/{id:guid}/parts", async (Guid id,StandardPartCreate r,AppDbContext db)=>
{
    if(!await db.ServiceTaskMasters.AnyAsync(x=>x.Id==id)||!await db.PartMasters.AnyAsync(x=>x.Id==r.PartMasterId))return Results.BadRequest();
    var x=await db.ServiceTaskStandardParts.FirstOrDefaultAsync(p=>p.ServiceTaskMasterId==id&&p.PartMasterId==r.PartMasterId);if(x==null){x=new ServiceTaskStandardPart{ServiceTaskMasterId=id,PartMasterId=r.PartMasterId,Quantity=r.Quantity};db.ServiceTaskStandardParts.Add(x);}else x.Quantity=r.Quantity;await db.SaveChangesAsync();return Results.Ok(x);
});
app.MapPost("/api/work-orders/{jobCardId:guid}/tasks/from-master/{masterId:guid}", async (Guid jobCardId,Guid masterId,TaskFromMasterRequest r,AppDbContext db)=>
{
    var j=await db.JobCards.FindAsync(jobCardId);var m=await db.ServiceTaskMasters.FindAsync(masterId);if(j is null||m is null)return Results.NotFound();
    Technician? tech=null;if(r.TechnicianId.HasValue)tech=await db.Technicians.FindAsync(r.TechnicianId.Value);
    if(m.RequiresHvAuthorization&&tech!=null&&(!tech.HvAuthorized||tech.HvAuthorizationValidUntil<DateTime.UtcNow))return Results.Conflict(new{message="Selected technician does not have active HV authorization."});
    var t=new WorkItem{JobCardId=j.Id,TaskCode=$"TSK-{DateTime.UtcNow:yyyyMMdd}-{(await db.WorkItems.CountAsync()+1):D5}",WorkType=m.Category,Description=m.Name,Status=tech==null?"Not Started":"Assigned",
        AssignedToTechnicianId=tech?.Id,AssignedTo=tech?.Name??"",Priority=r.Priority,EstimatedHours=m.StandardHours,StandardRepairHours=m.StandardHours,RequiresQc=m.RequiresQc,RequiresHvAuthorization=m.RequiresHvAuthorization,UpdatedAt=DateTime.UtcNow};
    db.WorkItems.Add(t);await db.SaveChangesAsync();
    var parts=await db.ServiceTaskStandardParts.AsNoTracking().Where(x=>x.ServiceTaskMasterId==m.Id).ToListAsync();
    return Results.Ok(new{task=t,standardParts=parts});
});

app.MapGet("/api/work-orders/{jobCardId:guid}/costs", async (Guid jobCardId,AppDbContext db)=>
{
    var parts=await db.PartTransactions.Where(x=>x.JobCardId==jobCardId&&(x.TransactionType=="Issue"||x.TransactionType=="Return"))
        .SumAsync(x=>(decimal?)x.ExtendedCost)??0;
    var labour=await db.LabourEntries.Where(x=>x.JobCardId==jobCardId).SumAsync(x=>(decimal?)x.CostAmount)??0;
    var entries=await db.WorkOrderCosts.AsNoTracking().Where(x=>x.JobCardId==jobCardId).OrderByDescending(x=>x.PostedAt).ToListAsync();
    var external=entries.Where(x=>x.CostType=="External").Sum(x=>x.Amount);
    var other=entries.Where(x=>x.CostType=="Other").Sum(x=>x.Amount);
    return Results.Ok(new{parts,labour,external,other,total=parts+labour+external+other,entries});
});
app.MapPost("/api/work-orders/{jobCardId:guid}/costs", async (Guid jobCardId,WorkOrderCostCreate r,AppDbContext db)=>
{
    if(!await db.JobCards.AnyAsync(x=>x.Id==jobCardId))return Results.NotFound();
    if(r.Amount<=0)return Results.BadRequest(new{message="Cost amount must be greater than zero."});
    var costType=r.CostType=="External"?"External":"Other";
    var x=new WorkOrderCost{JobCardId=jobCardId,WorkItemId=r.WorkItemId,CostType=costType,Description=r.Description,
        Amount=Math.Round(r.Amount,2),VendorReference=r.VendorReference,PostedBy=r.PostedBy};
    db.WorkOrderCosts.Add(x);Audit(db,"CREATE","WorkOrderCost",x.Id,$"{costType}:{x.Amount}",r.PostedBy);
    await db.SaveChangesAsync();return Results.Ok(x);
});
app.MapGet("/api/analytics/maintenance", async (AppDbContext db)=>
{
    var vehicles=await db.Vehicles.AsNoTracking().ToListAsync();var vehicleCount=vehicles.Count;
    var pmTotal=await db.PmObligations.CountAsync();var pmOverdue=await db.PmObligations.CountAsync(x=>x.Status=="Overdue");
    var closed=await db.ServiceEvents.AsNoTracking().Where(x=>x.ClosedAt!=null).ToListAsync();var mttr=closed.Count==0?0:Math.Round(closed.Average(x=>(x.ClosedAt!.Value-x.OpenedAt).TotalHours),1);
    var ftf=await db.FirstTimeFixResults.AsNoTracking().Where(x=>x.Eligible).ToListAsync();var ftfPct=ftf.Count==0?100:Math.Round(ftf.Count(x=>x.Passed)*100.0/ftf.Count,1);
    var repeat=await db.RepeatFailureMatches.CountAsync(x=>x.IsRepeat);var avail=vehicles.Count(x=>x.Status=="Available");
    var partCost=await db.PartTransactions.Where(x=>x.TransactionType=="Issue"||x.TransactionType=="Return").SumAsync(x=>(decimal?)x.ExtendedCost)??0;
    var labourCost=await db.LabourEntries.SumAsync(x=>(decimal?)x.CostAmount)??0;
    var externalCost=await db.WorkOrderCosts.Where(x=>x.CostType=="External").SumAsync(x=>(decimal?)x.Amount)??0;
    var otherCost=await db.WorkOrderCosts.Where(x=>x.CostType!="External").SumAsync(x=>(decimal?)x.Amount)??0;
    var totalCost=partCost+labourCost+externalCost+otherCost;var odo=vehicles.Sum(x=>x.OdometerKm);
    var tasks=await db.WorkItems.AsNoTracking().ToListAsync();var completedTasks=tasks.Count(x=>x.Status=="Completed");
    var topParts=await db.PartTransactions.AsNoTracking().Where(x=>x.TransactionType=="Issue"||x.TransactionType=="Return").GroupBy(x=>new{x.PartNumber,x.PartDescription})
        .Select(g=>new{g.Key.PartNumber,g.Key.PartDescription,quantity=g.Sum(x=>x.Quantity),cost=g.Sum(x=>x.ExtendedCost)}).OrderByDescending(x=>x.cost).Take(10).ToListAsync();
    var topVehicles=await (from j in db.JobCards.AsNoTracking() join e in db.ServiceEvents.AsNoTracking() on j.ServiceEventId equals e.Id
                           join v in db.Vehicles.AsNoTracking() on e.VehicleId equals v.Id
                           let parts=db.PartTransactions.Where(x=>x.JobCardId==j.Id&&(x.TransactionType=="Issue"||x.TransactionType=="Return")).Sum(x=>(decimal?)x.ExtendedCost)??0
                           let labour=db.LabourEntries.Where(x=>x.JobCardId==j.Id).Sum(x=>(decimal?)x.CostAmount)??0
                           let extra=db.WorkOrderCosts.Where(x=>x.JobCardId==j.Id).Sum(x=>(decimal?)x.Amount)??0
                           group new{parts,labour,extra} by new{v.Id,v.RegistrationNumber} into g
                           select new{vehicle=g.Key.RegistrationNumber,cost=g.Sum(x=>x.parts+x.labour+x.extra)}).OrderByDescending(x=>x.cost).Take(10).ToListAsync();
    var techProductivity=await (from l in db.LabourEntries.AsNoTracking() group l by l.Technician into g select new{technician=g.Key,hours=g.Sum(x=>x.Hours),cost=g.Sum(x=>x.CostAmount)}).OrderByDescending(x=>x.hours).Take(10).ToListAsync();
    return Results.Ok(new{
      vehicles=vehicleCount,availabilityPct=vehicleCount==0?100:Math.Round(avail*100.0/vehicleCount,1),
      pmCompliancePct=pmTotal==0?100:Math.Round((pmTotal-pmOverdue)*100.0/pmTotal,1),mttrHours=mttr,firstTimeFixPct=ftfPct,repeatFailures=repeat,
      taskCompletionPct=tasks.Count==0?100:Math.Round(completedTasks*100.0/tasks.Count,1),
      partsCost=partCost,labourCost,externalCost,otherCost,totalMaintenanceCost=totalCost,costPerKm=odo==0?0:Math.Round(totalCost/odo,2),
      openRequests=await db.MaintenanceRequests.CountAsync(x=>x.Status=="Open"),openWorkOrders=await db.JobCards.CountAsync(x=>x.Status!="Completed"&&x.Status!="Closed"),
      topParts,topVehicles,technicianProductivity=techProductivity
    });
});

app.MapFallback(async context =>
{
    var env=context.RequestServices.GetRequiredService<IWebHostEnvironment>();
    var webRoot=env.WebRootPath ?? Path.Combine(env.ContentRootPath,"wwwroot");
    var indexPath=Path.Combine(webRoot,"index.html");
    if(!File.Exists(indexPath)){ context.Response.StatusCode=500; await context.Response.WriteAsync("Angular UI missing."); return; }
    context.Response.ContentType="text/html; charset=utf-8"; await context.Response.SendFileAsync(indexPath);
});
app.Run();

record PmRequest(Guid VehicleId, string PlanCode, string TriggerType, DateTime? DueDate, decimal? DueReading);
record VehicleEnrollmentRequest(string Vin,string RegistrationNumber,Guid ModelMasterId,Guid? VariantMasterId,string ImageUrl,string MotorNumber,DateTime? PurchaseDate,decimal? PurchaseCost,string InvoiceNumber,string DealerName,DateTime? CommissioningDate,DateTime? RegistrationDate,DateTime? RegistrationExpiry,string InsuranceNumber,DateTime? InsuranceStartDate,DateTime? InsuranceExpiryDate,DateTime? WarrantyStartDate,DateTime? WarrantyExpiryDate,DateTime? BatteryWarrantyStartDate,DateTime? BatteryWarrantyExpiryDate,decimal OdometerKm,decimal OperatingHours,decimal EnergyKwh,decimal? BatterySoc,string DepotCode,string ServiceCentreCode,string CustomerCode,string OwnershipTypeCode,Guid? MaintenanceProgramId,string Remarks,string CreatedBy);
record AppointmentRequest(Guid VehicleId, Guid? PmObligationId, string SourceType, string SourceReference, DateTime StartAt, string ServiceCentre, string Bay,
    Guid? TechnicianId, string AppointmentType, string Priority, string Reason, decimal PlannedHours, string CreatedBy);
record WorkItemRequest(string WorkType, string Description, decimal? StandardRepairHours, bool RequiresQc, bool RequiresHvAuthorization);
record StatusRequest(string Status);
record CheckInRequest(string ServiceCentre,string Bay,decimal? OdometerKm,decimal? OperatingHours,decimal? EnergyKwh,string AdditionalComplaint,string ArrivalRemarks);
record BreakdownRequest(Guid VehicleId, string Priority, string Location, string Complaint, string TriageDecision, string DispatchMode);
record BreakdownConvertRequest(bool? GenerateGeneralDiagnosis,Guid? TechnicianId,string? Bay,string? ConvertedBy);
record QcRequest(string Inspector, string Result, bool RoadTestRequired, bool RoadTestPassed, string Remarks);
record ReleaseRequest(string ReleasedBy, string Remarks);

record ChecklistRequest(Guid? WorkItemId,string ChecklistCode,string ItemCode,string ItemText,string Result,string Remarks,string ExecutedBy,bool IsMandatory,
    string? DefectSeverity,string? DefectDescription,string? FailureCode);
record DefectRequest(Guid JobCardId,Guid? WorkItemId,string Category,string Severity,string Description,string FailureCode);
record DefectCloseRequest(string RcaSummary);
record AvailabilityCorrection(Guid CorrectsLedgerId,string State,DateTime StartAt,DateTime? EndAt,string ReasonCode,string ChangedBy);
record OffHireRequest(Guid VehicleId,DateTime StartAt,DateTime? ExpectedReturnAt,string ReasonCode,string RequestedBy);
record ApprovalRequest(string User);
record RecommissionRequest(string Inspector,string Result,string Remarks);
record SlaStartRequest(string ClockType,int TargetMinutes);
record PauseRequest(string Reason);

record TaskRequest(Guid JobCardId,string WorkType,string Description,Guid? AssignedToTechnicianId,DateTime? PlannedStartAt,DateTime? DueAt,string Priority,
    Guid? DependencyTaskId,decimal? EstimatedHours,bool RequiresQc,bool RequiresHvAuthorization);
record TaskStatusRequest(string Status,decimal? ActualHours,string? CompletionRemarks,string? EvidenceReference);
record TaskAssignRequest(Guid TechnicianId);
record JobCardAssignRequest(Guid? TechnicianId,string? Bay,bool AssignOpenTasks,string AssignedBy);
record WorkApprovalRequest(string ApprovedBy,string Remarks);
record WorkLogCreate(Guid? WorkItemId,Guid? ChecklistFieldInstanceId,string EntryType,string Comment,string CreatedBy,string CreatedRole);
record AdditionalWorkCreate(string Description,string Reason,Guid? TechnicianId,decimal? EstimatedHours,string Priority,bool RequiresQc,string CreatedBy,string CreatedRole);
record RecheckRequest(string Result,string Remarks,string RecheckedBy,string RecheckedRole);

record PartMasterRequest(string PartNumber,string Description,string Category,string UnitOfMeasure,string ManufacturerPartNumber,bool IsSerialized,bool IsWarrantyReturnable,decimal ReorderLevel,decimal ReorderQuantity,decimal StandardCost);
record StockMovementRequest(Guid PartMasterId,Guid InventoryLocationId,decimal Quantity,string User);
record PartRequestCreate(Guid JobCardId,Guid? WorkItemId,Guid PartMasterId,Guid InventoryLocationId,decimal Quantity,bool WarrantyCandidate,string FailedPartDisposition,string RequestedBy);
record QuantityAction(decimal Quantity,string User);


record MaintenanceRequestCreate(Guid VehicleId,string SourceType,string SourceReference,string RequestType,string ComplaintCategoryCode,string SymptomCode,string Priority,string Description,string RequestedBy,DateTime? TargetDate);
record CreateWorkOrderRequest(Guid[] RequestIds,string Priority,string Bay,Guid? TechnicianId,string Technician,string CreatedBy);
record ServiceTaskMasterCreate(string TaskCode,string Name,string Category,string Description,decimal StandardHours,string RequiredSkillCode,bool RequiresHvAuthorization,bool RequiresQc,string ChecklistCode);
record StandardPartCreate(Guid PartMasterId,decimal Quantity);
record TaskFromMasterRequest(Guid? TechnicianId,string Priority);
record WorkOrderCostCreate(Guid? WorkItemId,string CostType,string Description,decimal Amount,string VendorReference,string PostedBy);
