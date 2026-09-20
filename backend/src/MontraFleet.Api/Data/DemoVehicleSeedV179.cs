using Microsoft.EntityFrameworkCore;
using MontraFleet.Api.Models;

namespace MontraFleet.Api.Data;

/// <summary>
/// Idempotent non-production demo fleet used to exercise Vehicle Master, PM due,
/// appointment and service execution flows. VINs/registrations are fictional.
/// Existing vehicles are never modified or deleted.
/// </summary>
public static class DemoVehicleSeedV179
{
    private sealed record VehicleSeed(
        string ModelCode,
        string VariantCode,
        string Vin,
        string Registration,
        decimal OdometerKm,
        decimal OperatingHours,
        decimal EnergyKwh,
        decimal BatterySoc,
        int CommissioningDaysAgo);

    public static async Task SeedAsync(AppDbContext db)
    {
        var seeds = new[]
        {
            // Last-mile passenger - Super Auto (4 vehicles)
            new VehicleSeed("SUPER_AUTO","EPL20-STD","DEMOAUTO000000001","TS09DA1001",  4200,  180,  2100, 82, 160),
            new VehicleSeed("SUPER_AUTO","EPL20R-STD","DEMOAUTO000000002","TS09DA1002",  8900,  360,  4600, 67, 310),
            new VehicleSeed("SUPER_AUTO","EPL20-STD","DEMOAUTO000000003","KA01DA1003", 17800,  690,  8900, 74, 520),
            new VehicleSeed("SUPER_AUTO","EPL20R-STD","DEMOAUTO000000004","AP39DA1004", 36200, 1310, 18100, 59, 760),

            // Last-mile cargo - Super Cargo (4 vehicles)
            new VehicleSeed("SUPER_CARGO","SCARGO-CGO","DEMOCARGO00000001","TS09DC2001",  4600,  205,  2500, 79, 175),
            new VehicleSeed("SUPER_CARGO","SCARGO-CGO","DEMOCARGO00000002","TS09DC2002",  9700,  410,  5200, 71, 335),
            new VehicleSeed("SUPER_CARGO","SCARGO-CGO","DEMOCARGO00000003","KA01DC2003", 19300,  780, 10300, 63, 560),
            new VehicleSeed("SUPER_CARGO","SCARGO-CGO","DEMOCARGO00000004","AP39DC2004", 38800, 1450, 20500, 55, 810),

            // Small commercial vehicle - EVIATOR (4 vehicles)
            new VehicleSeed("EVIATOR","EVI350-32","DEMOEVIATOR000001","TS09EV3001",  9300,  410,  7600, 86, 190),
            new VehicleSeed("EVIATOR","EVICORE-40","DEMOEVIATOR000002","TS09EV3002", 18750,  770, 15200, 73, 370),
            new VehicleSeed("EVIATOR","EVI350LP-50","DEMOEVIATOR000003","KA01EV3003", 39200, 1510, 30900, 64, 620),
            new VehicleSeed("EVIATOR","EVICORE-40","DEMOEVIATOR000004","AP39EV3004", 78100, 2870, 60600, 58, 980),

            // Medium & heavy commercial vehicle - Rhino (4 vehicles)
            new VehicleSeed("RHINO_5538_EV","RH5538-4X2","DEMORHINO00000001","TS09RH4001", 19400,  720, 19000, 81, 210),
            new VehicleSeed("RHINO_5538_EV","RH5538-6X4","DEMORHINO00000002","TS09RH4002", 39700, 1420, 38100, 72, 420),
            new VehicleSeed("RHINO_5538_EV","RH5538-4X2","DEMORHINO00000003","KA01RH4003", 79600, 2810, 74200, 65, 720),
            new VehicleSeed("RHINO_5538_EV","RH5538-6X4","DEMORHINO00000004","AP39RH4004",159200, 5660,148900, 53,1320),

            // Tractor - E-27 (4 vehicles; operating-hours driven)
            new VehicleSeed("TRACTOR_E27","E27-2WD","DEMOTRACTOR000001","TS09TR5001",  1850,  235,  4100, 88, 150),
            new VehicleSeed("TRACTOR_E27","E27-4WD","DEMOTRACTOR000002","TS09TR5002",  3720,  485,  7900, 76, 290),
            new VehicleSeed("TRACTOR_E27","E27-2WD","DEMOTRACTOR000003","KA01TR5003",  7450,  980, 15400, 68, 540),
            new VehicleSeed("TRACTOR_E27","E27-4WD","DEMOTRACTOR000004","AP39TR5004", 14900, 1970, 30200, 61, 910),
        };

        var customer = await db.CustomerMasters.Where(x => x.IsActive).OrderBy(x => x.CustomerCode).FirstOrDefaultAsync();
        var depot = await db.DepotMasters.Where(x => x.IsActive).OrderBy(x => x.DepotCode).FirstOrDefaultAsync();

        foreach (var s in seeds)
        {
            if (await db.Vehicles.AnyAsync(x => x.Vin == s.Vin)) continue;

            var model = await db.VehicleModelMasters.FirstOrDefaultAsync(x => x.ModelCode == s.ModelCode && x.IsActive);
            if (model is null) continue; // PM/model seed must exist first.

            var variant = await db.VehicleVariantMasters.FirstOrDefaultAsync(x =>
                x.VehicleModelMasterId == model.Id && x.VariantCode == s.VariantCode && x.IsActive);
            if (variant is null) continue;

            var program = await db.MaintenancePrograms
                .Where(x => x.IsActive && x.VehicleModelMasterId == model.Id &&
                            (x.VehicleVariantMasterId == null || x.VehicleVariantMasterId == variant.Id))
                .OrderByDescending(x => x.VehicleVariantMasterId != null)
                .ThenBy(x => x.ProgramCode)
                .FirstOrDefaultAsync();

            var serviceCentreCode = string.Empty;
            var support = await db.ServiceCentreModelSupports
                .FirstOrDefaultAsync(x => x.VehicleModelMasterId == model.Id);
            if (support is not null)
            {
                var centre = await db.ServiceCentreMasters.FirstOrDefaultAsync(x => x.Id == support.ServiceCentreMasterId && x.IsActive);
                serviceCentreCode = centre?.CentreCode ?? string.Empty;
            }
            if (string.IsNullOrWhiteSpace(serviceCentreCode))
                serviceCentreCode = (await db.ServiceCentreMasters.Where(x => x.IsActive).OrderBy(x => x.CentreCode).FirstOrDefaultAsync())?.CentreCode ?? string.Empty;

            var commissioned = DateTime.UtcNow.Date.AddDays(-s.CommissioningDaysAgo);
            var purchase = commissioned.AddDays(-14);

            db.Vehicles.Add(new Vehicle
            {
                Vin = s.Vin,
                RegistrationNumber = s.Registration,
                Model = model.Name,
                Variant = variant.Name,
                VehicleTypeCode = model.VehicleTypeCode,
                ManufacturerCode = model.ManufacturerCode,
                ModelMasterId = model.Id,
                VariantMasterId = variant.Id,
                ImageUrl = "", // Inherit current model/variant reference at read time.
                MotorNumber = $"DEMO-MTR-{s.Registration}",
                PurchaseDate = purchase,
                InvoiceNumber = $"DEMO-INV-{s.Registration}",
                DealerName = "Montra Demo Fleet",
                CommissioningDate = commissioned,
                RegistrationDate = commissioned.AddDays(-3),
                WarrantyStartDate = commissioned,
                BatteryWarrantyStartDate = commissioned,
                DepotCode = depot?.DepotCode ?? string.Empty,
                ServiceCentreCode = serviceCentreCode,
                CustomerCode = customer?.CustomerCode ?? string.Empty,
                OwnershipTypeCode = "OWNED",
                MaintenanceProgramId = program?.Id,
                Remarks = "DEMO DATA - fictional vehicle seeded for product workflow testing; not an actual Montra VIN.",
                Status = "Available",
                OdometerKm = s.OdometerKm,
                OperatingHours = s.OperatingHours,
                EnergyKwh = s.EnergyKwh,
                BatterySoc = s.BatterySoc,
                IsActive = true
            });
        }

        await db.SaveChangesAsync();
    }
}
