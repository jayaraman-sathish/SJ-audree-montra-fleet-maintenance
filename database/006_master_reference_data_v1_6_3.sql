-- AU-Fleet-Ops v1.6.3 - Manufacturer + Montra reference masters + operational master correction
CREATE TABLE IF NOT EXISTS "ManufacturerMasters" (
 "Id" uuid PRIMARY KEY, "ManufacturerCode" text NOT NULL, "Name" text NOT NULL DEFAULT '',
 "Country" text NOT NULL DEFAULT 'India', "WebsiteUrl" text NOT NULL DEFAULT '',
 "ContactPhone" text NOT NULL DEFAULT '', "ContactEmail" text NOT NULL DEFAULT '',
 "IsActive" boolean NOT NULL DEFAULT true);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_ManufacturerMasters_ManufacturerCode" ON "ManufacturerMasters" ("ManufacturerCode");
-- Remaining reference records are seeded idempotently by application startup.
