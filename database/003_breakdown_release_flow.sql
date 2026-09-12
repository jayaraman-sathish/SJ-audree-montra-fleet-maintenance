USE MontraFleetMaintenance;
GO

IF OBJECT_ID('dbo.Breakdowns','U') IS NULL
CREATE TABLE dbo.Breakdowns (
  Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
  VehicleId UNIQUEIDENTIFIER NOT NULL,
  BreakdownNumber NVARCHAR(40) NOT NULL UNIQUE,
  Priority NVARCHAR(10) NOT NULL,
  Location NVARCHAR(250) NULL,
  Complaint NVARCHAR(1000) NULL,
  TriageDecision NVARCHAR(100) NULL,
  DispatchMode NVARCHAR(50) NULL,
  Status NVARCHAR(40) NOT NULL,
  ReportedAt DATETIME2 NOT NULL,
  ResponseAt DATETIME2 NULL,
  RestoredAt DATETIME2 NULL
);
GO

IF OBJECT_ID('dbo.PartTransactions','U') IS NULL
CREATE TABLE dbo.PartTransactions (
  Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
  JobCardId UNIQUEIDENTIFIER NOT NULL,
  WorkItemId UNIQUEIDENTIFIER NULL,
  PartNumber NVARCHAR(80) NOT NULL,
  PartDescription NVARCHAR(250) NULL,
  TransactionType NVARCHAR(20) NOT NULL,
  Quantity DECIMAL(18,3) NOT NULL,
  SerialNumber NVARCHAR(120) NULL,
  WarrantyCandidate BIT NOT NULL DEFAULT 0,
  FailedPartDisposition NVARCHAR(100) NULL,
  TransactionAt DATETIME2 NOT NULL
);
GO

IF OBJECT_ID('dbo.LabourEntries','U') IS NULL
CREATE TABLE dbo.LabourEntries (
  Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
  JobCardId UNIQUEIDENTIFIER NOT NULL,
  WorkItemId UNIQUEIDENTIFIER NULL,
  Technician NVARCHAR(150) NOT NULL,
  StartAt DATETIME2 NOT NULL,
  EndAt DATETIME2 NULL,
  Hours DECIMAL(18,2) NOT NULL DEFAULT 0,
  SkillCode NVARCHAR(50) NULL
);
GO

IF OBJECT_ID('dbo.QcInspections','U') IS NULL
CREATE TABLE dbo.QcInspections (
  Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
  JobCardId UNIQUEIDENTIFIER NOT NULL,
  Inspector NVARCHAR(150) NULL,
  Result NVARCHAR(30) NOT NULL DEFAULT 'Pending',
  Remarks NVARCHAR(1000) NULL,
  InspectedAt DATETIME2 NULL
);
GO

IF OBJECT_ID('dbo.VehicleReleases','U') IS NULL
CREATE TABLE dbo.VehicleReleases (
  Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
  ServiceEventId UNIQUEIDENTIFIER NOT NULL,
  VehicleId UNIQUEIDENTIFIER NOT NULL,
  ReleaseStatus NVARCHAR(30) NOT NULL DEFAULT 'Pending',
  ReleasedBy NVARCHAR(150) NULL,
  ReleasedAt DATETIME2 NULL,
  Remarks NVARCHAR(1000) NULL
);
GO

IF COL_LENGTH('dbo.ServiceEvents','BreakdownId') IS NULL
  ALTER TABLE dbo.ServiceEvents ADD BreakdownId UNIQUEIDENTIFIER NULL;
GO
IF COL_LENGTH('dbo.ServiceEvents','Priority') IS NULL
  ALTER TABLE dbo.ServiceEvents ADD Priority NVARCHAR(10) NOT NULL CONSTRAINT DF_ServiceEvents_Priority DEFAULT 'P3';
GO
IF COL_LENGTH('dbo.ServiceEvents','ClosedAt') IS NULL
  ALTER TABLE dbo.ServiceEvents ADD ClosedAt DATETIME2 NULL;
GO
IF COL_LENGTH('dbo.JobCards','StandardRepairHours') IS NULL
  ALTER TABLE dbo.JobCards ADD StandardRepairHours DECIMAL(18,2) NULL;
GO
IF COL_LENGTH('dbo.JobCards','StartedAt') IS NULL
  ALTER TABLE dbo.JobCards ADD StartedAt DATETIME2 NULL;
GO
IF COL_LENGTH('dbo.JobCards','CompletedAt') IS NULL
  ALTER TABLE dbo.JobCards ADD CompletedAt DATETIME2 NULL;
GO
IF COL_LENGTH('dbo.WorkItems','WorkType') IS NULL
  ALTER TABLE dbo.WorkItems ADD WorkType NVARCHAR(30) NOT NULL CONSTRAINT DF_WorkItems_WorkType DEFAULT 'Repair';
GO
IF COL_LENGTH('dbo.WorkItems','RequiresQc') IS NULL
  ALTER TABLE dbo.WorkItems ADD RequiresQc BIT NOT NULL CONSTRAINT DF_WorkItems_RequiresQc DEFAULT 1;
GO
IF COL_LENGTH('dbo.VehicleAvailabilityLedger','SourceType') IS NULL
  ALTER TABLE dbo.VehicleAvailabilityLedger ADD SourceType NVARCHAR(40) NULL;
GO
IF COL_LENGTH('dbo.VehicleAvailabilityLedger','SourceBreakdownId') IS NULL
  ALTER TABLE dbo.VehicleAvailabilityLedger ADD SourceBreakdownId UNIQUEIDENTIFIER NULL;
GO
IF COL_LENGTH('dbo.VehicleAvailabilityLedger','ChangedBy') IS NULL
  ALTER TABLE dbo.VehicleAvailabilityLedger ADD ChangedBy NVARCHAR(150) NOT NULL CONSTRAINT DF_Availability_ChangedBy DEFAULT 'System';
GO
