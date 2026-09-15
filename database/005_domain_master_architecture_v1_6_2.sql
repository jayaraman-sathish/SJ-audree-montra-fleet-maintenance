-- AU-Fleet-Ops v1.6.2 - Domain Master Architecture Correction
ALTER TABLE "VehicleVariantMasters" ADD COLUMN IF NOT EXISTS "GvwKg" numeric(18,2) NULL;
ALTER TABLE "VehicleVariantMasters" ADD COLUMN IF NOT EXISTS "PayloadKg" numeric(18,2) NULL;
ALTER TABLE "VehicleVariantMasters" ADD COLUMN IF NOT EXISTS "BatteryCapacityKwh" numeric(18,2) NULL;
ALTER TABLE "VehicleVariantMasters" ADD COLUMN IF NOT EXISTS "MotorPowerKw" numeric(18,2) NULL;
ALTER TABLE "VehicleVariantMasters" ADD COLUMN IF NOT EXISTS "WheelbaseMm" numeric(18,2) NULL;
ALTER TABLE "VehicleVariantMasters" ADD COLUMN IF NOT EXISTS "Configuration" text NOT NULL DEFAULT '';
ALTER TABLE "VehicleVariantMasters" ADD COLUMN IF NOT EXISTS "EffectiveFrom" timestamptz NULL;

CREATE TABLE IF NOT EXISTS "CustomerMasters" (
 "Id" uuid PRIMARY KEY, "CustomerCode" text NOT NULL, "Name" text NOT NULL DEFAULT '',
 "AddressLine1" text NOT NULL DEFAULT '', "AddressLine2" text NOT NULL DEFAULT '', "City" text NOT NULL DEFAULT '',
 "State" text NOT NULL DEFAULT '', "PostalCode" text NOT NULL DEFAULT '', "Country" text NOT NULL DEFAULT 'India',
 "Gstin" text NOT NULL DEFAULT '', "ContactPerson" text NOT NULL DEFAULT '', "Mobile" text NOT NULL DEFAULT '',
 "Email" text NOT NULL DEFAULT '', "IsActive" boolean NOT NULL DEFAULT true);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_CustomerMasters_CustomerCode" ON "CustomerMasters" ("CustomerCode");

CREATE TABLE IF NOT EXISTS "DepotMasters" (
 "Id" uuid PRIMARY KEY, "DepotCode" text NOT NULL, "Name" text NOT NULL DEFAULT '', "CustomerMasterId" uuid NULL,
 "AddressLine1" text NOT NULL DEFAULT '', "City" text NOT NULL DEFAULT '', "State" text NOT NULL DEFAULT '',
 "PostalCode" text NOT NULL DEFAULT '', "ContactPerson" text NOT NULL DEFAULT '', "Mobile" text NOT NULL DEFAULT '',
 "IsActive" boolean NOT NULL DEFAULT true);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_DepotMasters_DepotCode" ON "DepotMasters" ("DepotCode");

CREATE TABLE IF NOT EXISTS "ServiceCentreMasters" (
 "Id" uuid PRIMARY KEY, "CentreCode" text NOT NULL, "Name" text NOT NULL DEFAULT '', "CentreType" text NOT NULL DEFAULT 'Company',
 "AddressLine1" text NOT NULL DEFAULT '', "City" text NOT NULL DEFAULT '', "State" text NOT NULL DEFAULT '',
 "PostalCode" text NOT NULL DEFAULT '', "ContactPerson" text NOT NULL DEFAULT '', "Mobile" text NOT NULL DEFAULT '',
 "WorkingHours" text NOT NULL DEFAULT '', "BayCount" integer NOT NULL DEFAULT 0, "IsActive" boolean NOT NULL DEFAULT true);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_ServiceCentreMasters_CentreCode" ON "ServiceCentreMasters" ("CentreCode");
