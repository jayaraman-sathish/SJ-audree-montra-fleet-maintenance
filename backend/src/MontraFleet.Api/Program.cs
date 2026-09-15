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
        new MasterOption{Category="DOCUMENT_TYPE",Code="QC_REPORT",Name="QC / Release Report",Value="Work Order",Description="QC and release evidence",SortOrder=60}
    };
    foreach(var o in seedOptions)
        if(!await db.MasterOptions.AnyAsync(x=>x.Category==o.Category&&x.Code==o.Code)) db.MasterOptions.Add(o);
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

app.MapGet("/api/health", () => Results.Ok(new { status="ok", service="MontraFleet.Api", version="1.6.4" }));
app.MapGet("/api/db/health", async (AppDbContext db) =>
{
    try { return await db.Database.CanConnectAsync()
        ? Results.Ok(new { status="ok", database="PostgreSQL", connected=true, version="1.6.4" })
        : Results.Problem("Database connection check returned false.", statusCode:503); }
    catch (Exception ex) { return Results.Problem("Database connection failed", ex.Message, statusCode:503); }
});
app.MapGet("/api/ui/health", (IWebHostEnvironment env) =>
{
    var webRoot = env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");
    var indexPath = Path.Combine(webRoot, "index.html");
    return Results.Ok(new { status=File.Exists(indexPath)?"ok":"missing", indexExists=File.Exists(indexPath), webRoot, version="1.6.4" });
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
            case "ODOMETER": o.DueReading=(plan.RecurrenceBasis=="ScheduledDue"&&completed.DueReading.HasValue?completed.DueReading.Value:v.OdometerKm)+t.IntervalValue;break;
            case "OPERATING_HOURS": o.DueOperatingHours=(plan.RecurrenceBasis=="ScheduledDue"&&completed.DueOperatingHours.HasValue?completed.DueOperatingHours.Value:v.OperatingHours)+t.IntervalValue;break;
            case "KWH": o.DueEnergyKwh=(plan.RecurrenceBasis=="ScheduledDue"&&completed.DueEnergyKwh.HasValue?completed.DueEnergyKwh.Value:v.EnergyKwh)+t.IntervalValue;break;
            case "TIME":
                var basis=plan.RecurrenceBasis=="ScheduledDue"&&completed.DueDate.HasValue?completed.DueDate.Value:DateTime.UtcNow;
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

app.MapGet("/api/pm/service-centres", async (AppDbContext db) => Results.Ok(await db.ServiceCentreMasters.AsNoTracking().OrderBy(x=>x.Name).ToListAsync()));
app.MapPost("/api/pm/service-centres", async (ServiceCentreMaster r,AppDbContext db)=>
{
    r.Id=Guid.NewGuid();r.CentreCode=r.CentreCode.Trim().ToUpperInvariant();
    if(string.IsNullOrWhiteSpace(r.CentreCode)||string.IsNullOrWhiteSpace(r.Name))return Results.BadRequest(new{message="Service centre code and name are required."});
    if(await db.ServiceCentreMasters.AnyAsync(x=>x.CentreCode==r.CentreCode))return Results.Conflict(new{message="Service centre code already exists."});
    db.ServiceCentreMasters.Add(r);await db.SaveChangesAsync();return Results.Ok(r);
});
app.MapPut("/api/pm/service-centres/{id:guid}", async (Guid id,ServiceCentreMaster r,AppDbContext db)=>
{
    var x=await db.ServiceCentreMasters.FindAsync(id);if(x is null)return Results.NotFound();
    x.Name=r.Name;x.CentreType=r.CentreType;x.AddressLine1=r.AddressLine1;x.City=r.City;x.State=r.State;x.PostalCode=r.PostalCode;
    x.ContactPerson=r.ContactPerson;x.Mobile=r.Mobile;x.WorkingHours=r.WorkingHours;x.BayCount=r.BayCount;x.IsActive=r.IsActive;await db.SaveChangesAsync();return Results.Ok(x);
});

app.MapGet("/api/pm/programs", async (AppDbContext db) => Results.Ok(await db.MaintenancePrograms.AsNoTracking().OrderBy(x=>x.Name).ToListAsync()));
app.MapPost("/api/pm/programs", async (MaintenanceProgram r,AppDbContext db)=>
{
    r.Id=Guid.NewGuid();r.ProgramCode=r.ProgramCode.Trim().ToUpperInvariant();if(await db.MaintenancePrograms.AnyAsync(x=>x.ProgramCode==r.ProgramCode))return Results.Conflict(new{message="Program code already exists."});db.MaintenancePrograms.Add(r);await db.SaveChangesAsync();return Results.Ok(r);
});
app.MapPut("/api/pm/programs/{id:guid}",async(Guid id,MaintenanceProgram r,AppDbContext db)=>{var x=await db.MaintenancePrograms.FindAsync(id);if(x is null)return Results.NotFound();x.Name=r.Name;x.Description=r.Description;x.VehicleModelMasterId=r.VehicleModelMasterId;x.VehicleVariantMasterId=r.VehicleVariantMasterId;x.EffectiveFrom=r.EffectiveFrom;x.EffectiveTo=r.EffectiveTo;x.IsActive=r.IsActive;await db.SaveChangesAsync();return Results.Ok(x);});

app.MapGet("/api/pm/plans", async (AppDbContext db) =>
{
    var plans=await db.MaintenancePlans.AsNoTracking().OrderBy(x=>x.Sequence).ThenBy(x=>x.PlanCode).ToListAsync();var triggers=await db.MaintenancePlanTriggers.AsNoTracking().ToListAsync();var tasks=await db.MaintenancePlanTasks.AsNoTracking().ToListAsync();
    return Results.Ok(plans.Select(x=>new{x.Id,x.MaintenanceProgramId,x.PlanCode,x.Name,x.Description,x.RecurrenceBasis,x.Sequence,x.IsActive,triggers=triggers.Where(t=>t.MaintenancePlanId==x.Id),taskCount=tasks.Count(t=>t.MaintenancePlanId==x.Id)}));
});
app.MapPost("/api/pm/plans",async(MaintenancePlan r,AppDbContext db)=>{r.Id=Guid.NewGuid();r.PlanCode=r.PlanCode.Trim().ToUpperInvariant();if(!await db.MaintenancePrograms.AnyAsync(x=>x.Id==r.MaintenanceProgramId))return Results.BadRequest(new{message="Maintenance program not found."});if(await db.MaintenancePlans.AnyAsync(x=>x.MaintenanceProgramId==r.MaintenanceProgramId&&x.PlanCode==r.PlanCode))return Results.Conflict(new{message="Plan code already exists in this program."});db.MaintenancePlans.Add(r);await db.SaveChangesAsync();return Results.Ok(r);});
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

app.MapGet("/api/capacity", async (DateTime? date, AppDbContext db) =>
{
    var d=(date ?? DateTime.UtcNow).Date; var next=d.AddDays(1);
    var bays=await db.ServiceBays.AsNoTracking().Where(x=>x.IsActive).OrderBy(x=>x.BayCode).ToListAsync();
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
            var mapped=await(from m in db.MaintenancePlanTasks where m.MaintenancePlanId==planId join sm in db.ServiceTaskMasters on m.ServiceTaskMasterId equals sm.Id orderby m.Sequence select new{m,sm}).ToListAsync();
            var seq=0;foreach(var x in mapped){seq++;db.WorkItems.Add(new WorkItem{JobCardId=jc.Id,TaskCode=$"TSK-{DateTime.UtcNow:yyyyMMddHHmmss}-{seq:D2}",WorkType="PM",Description=x.sm.Name,Status="Not Started",Priority=a.Priority,EstimatedHours=x.sm.StandardHours,StandardRepairHours=x.sm.StandardHours,RequiresQc=x.sm.RequiresQc,RequiresHvAuthorization=x.sm.RequiresHvAuthorization,UpdatedAt=DateTime.UtcNow});}
        }
    }
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
                      join j in db.JobCards.AsNoTracking() on e.Id equals j.ServiceEventId into jj
                      from j in jj.DefaultIfEmpty()
                      where e.Status!="Closed"
                      orderby e.OpenedAt descending
                      select new { e.Id,eventNo=e.EventNumber,vehicle=v.RegistrationNumber,type=e.EventType,e.Status,jobCard=j!=null?j.JobCardNumber:"",jobCardId=j!=null?j.Id:(Guid?)null,bay=j!=null?j.Bay:"",technician=j!=null?j.Technician:"",sla=e.Priority }).ToListAsync();
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
                        t.EvidenceReference,t.CompletionRemarks,t.RequiresQc,t.RequiresHvAuthorization,t.UpdatedAt}).ToListAsync();
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
    var allowed=new[]{"Not Started","Assigned","In Progress","On Hold","Completed","Cancelled"};if(!allowed.Contains(r.Status))return Results.BadRequest(new{message="Invalid task status."});
    if(r.Status=="In Progress" && t.DependencyTaskId.HasValue){var d=await db.WorkItems.FindAsync(t.DependencyTaskId.Value);if(d!=null&&d.Status!="Completed")return Results.Conflict(new{message=$"Dependency {d.TaskCode} must be completed first."});}
    t.Status=r.Status;t.ActualHours=r.ActualHours??t.ActualHours;t.CompletionRemarks=r.CompletionRemarks??t.CompletionRemarks;t.EvidenceReference=r.EvidenceReference??t.EvidenceReference;t.UpdatedAt=DateTime.UtcNow;
    Audit(db,"STATUS","Task",t.Id,$"{t.TaskCode}:{r.Status}");await db.SaveChangesAsync();return Results.Ok(t);
});

app.MapPut("/api/tasks/{id:guid}/assign", async (Guid id, TaskAssignRequest r, AppDbContext db) =>
{
    var t=await db.WorkItems.FindAsync(id);if(t is null)return Results.NotFound();var tech=await db.Technicians.FindAsync(r.TechnicianId);
    if(tech is null||!tech.IsActive)return Results.BadRequest(new{message="Technician not active."});
    if(t.RequiresHvAuthorization && (!tech.HvAuthorized || tech.HvAuthorizationValidUntil<DateTime.UtcNow))return Results.Conflict(new{message="Task requires active HV authorization."});
    t.AssignedToTechnicianId=tech.Id;t.AssignedTo=tech.Name;if(t.Status=="Not Started")t.Status="Assigned";t.UpdatedAt=DateTime.UtcNow;
    Audit(db,"ASSIGN","Task",t.Id,$"{t.TaskCode}->{tech.Name}");await db.SaveChangesAsync();return Results.Ok(t);
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
                    select new{jobCardId=j.Id,j.JobCardNumber,j.Status,j.Bay,j.Technician,j.TechnicianId,serviceEventId=e.Id,e.EventNumber,e.EventType,e.Priority,
                        vehicleId=v.Id,vehicle=v.RegistrationNumber,v.Vin,v.Model,v.OdometerKm,v.OperatingHours}).FirstOrDefaultAsync();
    if(data is null)return Results.NotFound();
    var tasks=await db.WorkItems.CountAsync(x=>x.JobCardId==jobCardId);var completed=await db.WorkItems.CountAsync(x=>x.JobCardId==jobCardId&&x.Status=="Completed");
    var defects=await db.Defects.CountAsync(x=>x.JobCardId==jobCardId&&x.Disposition!="Closed");
    var parts=await db.PartRequests.CountAsync(x=>x.JobCardId==jobCardId);var qc=await db.QcInspections.Where(x=>x.JobCardId==jobCardId).OrderByDescending(x=>x.InspectedAt).Select(x=>x.Result).FirstOrDefaultAsync();
    return Results.Ok(new{data,tasks,completedTasks=completed,openDefects=defects,partTransactions=parts,qcStatus=qc??"Pending"});
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
    var rows=await (from b in db.Breakdowns.AsNoTracking() join v in db.Vehicles.AsNoTracking() on b.VehicleId equals v.Id orderby b.ReportedAt descending
                    select new { b.Id,b.BreakdownNumber,b.VehicleId,vehicle=v.RegistrationNumber,b.Priority,b.Location,b.Complaint,b.TriageDecision,b.DispatchMode,b.Status,b.ReportedAt }).ToListAsync();
    return Results.Ok(rows);
});
app.MapPost("/api/breakdowns", async (BreakdownRequest r, AppDbContext db) =>
{
    var v=await db.Vehicles.FindAsync(r.VehicleId); if(v is null) return Results.BadRequest(new { message="Vehicle not found." });
    var b=new Breakdown { VehicleId=v.Id, BreakdownNumber=$"BD-{DateTime.UtcNow:yyyy}-{(await db.Breakdowns.CountAsync()+1):D6}", Priority=r.Priority, Location=r.Location, Complaint=r.Complaint, TriageDecision=r.TriageDecision, DispatchMode=r.DispatchMode, Status="Reported" };
    v.Status="Breakdown"; db.Breakdowns.Add(b); db.VehicleAvailabilityLedger.Add(new VehicleAvailabilityLedger { VehicleId=v.Id, State="Breakdown", ReasonCode="Breakdown", SourceType="Breakdown", SourceBreakdownId=b.Id });
    Audit(db,"CREATE","Breakdown",b.Id,b.BreakdownNumber); await db.SaveChangesAsync(); return Results.Created($"/api/breakdowns/{b.Id}",b);
});
app.MapPost("/api/breakdowns/{id:guid}/convert", async (Guid id, AppDbContext db) =>
{
    var b=await db.Breakdowns.FindAsync(id); if(b is null) return Results.NotFound();
    if (await db.ServiceEvents.AnyAsync(x=>x.BreakdownId==id)) return Results.Conflict(new { message="Already converted." });
    var e=new ServiceEvent { VehicleId=b.VehicleId, BreakdownId=b.Id, EventNumber=$"SE-{DateTime.UtcNow:yyyy}-{(await db.ServiceEvents.CountAsync()+1):D6}", EventType="Breakdown", Priority=b.Priority, Status="In Progress" };
    var j=new JobCard { ServiceEventId=e.Id, JobCardNumber=$"JC-{DateTime.UtcNow:yyyy}-{(await db.JobCards.CountAsync()+1):D6}", Status="Open", StartedAt=DateTime.UtcNow };
    b.Status="Converted"; db.ServiceEvents.Add(e); db.JobCards.Add(j); Audit(db,"CONVERT","Breakdown",b.Id,e.EventNumber); await db.SaveChangesAsync(); return Results.Ok(new { serviceEvent=e,jobCard=j });
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
    var vehicles=await db.Vehicles.AsNoTracking().Where(x=>term=="" || x.Vin.ToLower().Contains(term) || x.RegistrationNumber.ToLower().Contains(term)).Take(10)
        .Select(x=>new { type="Vehicle",key=x.RegistrationNumber,title=x.Model+" · "+x.Vin,status=x.Status }).ToListAsync();
    return Results.Ok(new { query=q??"", results=vehicles });
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
    return Results.Ok(new { v.Id,v.Vin,registration=v.RegistrationNumber,v.Model,v.Variant,availability=v.Status,v.OdometerKm,v.OperatingHours,v.BatterySoc,
        nextPm=nextPm==null?"-":(nextPm.DueDate.HasValue?nextPm.DueDate.Value.ToString("dd MMM yyyy"):(nextPm.DueReading?.ToString("0")+" km")),
        openDefects=defects,activeWarranty=warranty,openCampaigns=campaigns,lastService=lastService,documents=docs });
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
app.MapGet("/api/maintenance-requests", async (string? status, Guid? vehicleId, AppDbContext db) =>
{
    var q=db.MaintenanceRequests.AsNoTracking().AsQueryable();
    if(!string.IsNullOrWhiteSpace(status))q=q.Where(x=>x.Status==status);
    if(vehicleId.HasValue)q=q.Where(x=>x.VehicleId==vehicleId.Value);
    var rows=await (from r in q join v in db.Vehicles.AsNoTracking() on r.VehicleId equals v.Id
        orderby r.RequestedAt descending select new{r.Id,r.RequestNumber,r.VehicleId,vehicle=v.RegistrationNumber,r.SourceType,r.SourceReference,
        r.RequestType,r.Priority,r.Description,r.Status,r.RequestedBy,r.RequestedAt,r.TargetDate,r.JobCardId}).ToListAsync();
    return Results.Ok(rows);
});
app.MapPost("/api/maintenance-requests", async (MaintenanceRequestCreate r, AppDbContext db) =>
{
    if(!await db.Vehicles.AnyAsync(x=>x.Id==r.VehicleId))return Results.BadRequest(new{message="Vehicle not found."});
    var mr=new MaintenanceRequest{RequestNumber=$"MR-{DateTime.UtcNow:yyyyMMdd}-{(await db.MaintenanceRequests.CountAsync()+1):D5}",VehicleId=r.VehicleId,
        SourceType=r.SourceType,SourceReference=r.SourceReference,RequestType=r.RequestType,Priority=r.Priority,Description=r.Description,
        Status="Open",RequestedBy=r.RequestedBy,RequestedAt=DateTime.UtcNow,TargetDate=r.TargetDate};
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
record BreakdownRequest(Guid VehicleId, string Priority, string Location, string Complaint, string TriageDecision, string DispatchMode);
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

record PartMasterRequest(string PartNumber,string Description,string Category,string UnitOfMeasure,string ManufacturerPartNumber,bool IsSerialized,bool IsWarrantyReturnable,decimal ReorderLevel,decimal ReorderQuantity,decimal StandardCost);
record StockMovementRequest(Guid PartMasterId,Guid InventoryLocationId,decimal Quantity,string User);
record PartRequestCreate(Guid JobCardId,Guid? WorkItemId,Guid PartMasterId,Guid InventoryLocationId,decimal Quantity,bool WarrantyCandidate,string FailedPartDisposition,string RequestedBy);
record QuantityAction(decimal Quantity,string User);


record MaintenanceRequestCreate(Guid VehicleId,string SourceType,string SourceReference,string RequestType,string Priority,string Description,string RequestedBy,DateTime? TargetDate);
record CreateWorkOrderRequest(Guid[] RequestIds,string Priority,string Bay,Guid? TechnicianId,string Technician,string CreatedBy);
record ServiceTaskMasterCreate(string TaskCode,string Name,string Category,string Description,decimal StandardHours,string RequiredSkillCode,bool RequiresHvAuthorization,bool RequiresQc,string ChecklistCode);
record StandardPartCreate(Guid PartMasterId,decimal Quantity);
record TaskFromMasterRequest(Guid? TechnicianId,string Priority);
record WorkOrderCostCreate(Guid? WorkItemId,string CostType,string Description,decimal Amount,string VendorReference,string PostedBy);
