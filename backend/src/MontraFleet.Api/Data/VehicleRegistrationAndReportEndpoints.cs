using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace MontraFleet.Api.Data;

public static class VehicleRegistrationAndReportEndpoints
{
    private static bool IsAdmin(HttpRequest request, IConfiguration configuration)
        => !string.IsNullOrWhiteSpace(configuration["Veerai:AdminKey"])
            && string.Equals(request.Headers["X-Veerai-Admin-Key"].FirstOrDefault(),
                configuration["Veerai:AdminKey"], StringComparison.Ordinal);

    public static void Map(WebApplication app)
    {
        app.MapPost("/api/vehicle-enrollment", async (VehicleEnrollmentRequest input, HttpRequest request, AppDbContext db, IConfiguration configuration, CancellationToken ct) =>
        {
            if (!IsAdmin(request, configuration)) return Results.Unauthorized();
            if (string.IsNullOrWhiteSpace(input.Vin) || string.IsNullOrWhiteSpace(input.RegistrationNumber) || string.IsNullOrWhiteSpace(input.Model))
                return Results.BadRequest(new { message = "VIN, registration number and model are required." });
            if (await db.Vehicles.AnyAsync(x => x.Vin == input.Vin || x.RegistrationNumber == input.RegistrationNumber, ct))
                return Results.Conflict(new { message = "VIN or registration number already exists." });

            var vehicle = new Models.Vehicle
            {
                Id = Guid.NewGuid(), Vin = input.Vin.Trim(), RegistrationNumber = input.RegistrationNumber.Trim(),
                Model = input.Model.Trim(), Variant = input.Variant ?? "", Status = input.Status ?? "Available",
                PurchaseDate = input.PurchaseDate, CommissioningDate = input.CommissioningDate,
                OdometerKm = input.OdometerKm ?? 0, OperatingHours = input.OperatingHours ?? 0,
                EnergyKwh = input.EnergyKwh ?? 0, BatterySoc = input.BatterySoc,
                DepotCode = input.DepotCode ?? "", ServiceCentreCode = input.ServiceCentreCode ?? "",
                IsActive = true
            };
            db.Vehicles.Add(vehicle);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/vehicle-enrollment/{vehicle.Id}", new { vehicle.Id, vehicle.RegistrationNumber, vehicle.Vin });
        });

        app.MapPut("/api/vehicle-enrollment/{id:guid}", async (Guid id, VehicleEnrollmentRequest input, HttpRequest request, AppDbContext db, IConfiguration configuration, CancellationToken ct) =>
        {
            if (!IsAdmin(request, configuration)) return Results.Unauthorized();
            var vehicle = await db.Vehicles.SingleOrDefaultAsync(x => x.Id == id, ct);
            if (vehicle is null) return Results.NotFound();
            if (await db.Vehicles.AnyAsync(x => x.Id != id && (x.Vin == input.Vin || x.RegistrationNumber == input.RegistrationNumber), ct))
                return Results.Conflict(new { message = "VIN or registration number already exists." });

            vehicle.Vin = input.Vin.Trim(); vehicle.RegistrationNumber = input.RegistrationNumber.Trim();
            vehicle.Model = input.Model.Trim(); vehicle.Variant = input.Variant ?? "";
            vehicle.Status = input.Status ?? vehicle.Status; vehicle.PurchaseDate = input.PurchaseDate;
            vehicle.CommissioningDate = input.CommissioningDate; vehicle.OdometerKm = input.OdometerKm ?? vehicle.OdometerKm;
            vehicle.OperatingHours = input.OperatingHours ?? vehicle.OperatingHours; vehicle.EnergyKwh = input.EnergyKwh ?? vehicle.EnergyKwh;
            vehicle.BatterySoc = input.BatterySoc; vehicle.DepotCode = input.DepotCode ?? "";
            vehicle.ServiceCentreCode = input.ServiceCentreCode ?? "";
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { vehicle.Id, vehicle.RegistrationNumber, vehicle.Vin });
        });

        app.MapGet("/api/reports/vehicles/{id:guid}", async (Guid id, HttpRequest request, AppDbContext db, IConfiguration configuration, CancellationToken ct) =>
        {
            if (!IsAdmin(request, configuration)) return Results.Unauthorized();
            var vehicle = await db.Vehicles.AsNoTracking().Where(x => x.Id == id)
                .Select(x => new { x.RegistrationNumber, x.Vin, x.Model, x.Variant, x.Status, x.OdometerKm, x.OperatingHours, x.EnergyKwh, x.DepotCode, x.ServiceCentreCode })
                .SingleOrDefaultAsync(ct);
            if (vehicle is null) return Results.NotFound();
            var history = await db.ServiceEvents.AsNoTracking().Where(x => x.VehicleId == id)
                .OrderByDescending(x => x.OpenedAt).Select(x => new { x.EventNumber, x.EventType, x.Status, x.OpenedAt, x.ClosedAt }).ToListAsync(ct);
            return Results.Content(PrintHtml("Vehicle Maintenance Report", vehicle.RegistrationNumber, JsonSerializer.Serialize(new { vehicle, history })), "text/html", Encoding.UTF8);
        });

        app.MapGet("/api/reports/job-cards/{id:guid}", async (Guid id, HttpRequest request, AppDbContext db, IConfiguration configuration, CancellationToken ct) =>
        {
            if (!IsAdmin(request, configuration)) return Results.Unauthorized();
            var job = await db.JobCards.AsNoTracking().Where(x => x.Id == id)
                .Select(x => new { x.JobCardNumber, x.Status, x.StartedAt, x.CompletedAt, x.ServiceEventId }).SingleOrDefaultAsync(ct);
            if (job is null) return Results.NotFound();
            var tasks = await db.WorkItems.AsNoTracking().Where(x => x.JobCardId == id)
                .Select(x => new { x.TaskCode, x.Description, x.Status, x.AssignedTo, x.CompletionRemarks }).ToListAsync(ct);
            return Results.Content(PrintHtml("Job Card Completion Report", job.JobCardNumber, JsonSerializer.Serialize(new { job, tasks })), "text/html", Encoding.UTF8);
        });
    }

    private static string PrintHtml(string title, string subject, string json)
    {
        var safe = WebUtility.HtmlEncode(json);
        return $"<!doctype html><html><head><meta charset='utf-8'><title>{WebUtility.HtmlEncode(title)}</title><style>body{{font-family:Arial;margin:32px;color:#14213d}}button{{padding:10px 18px}}pre{{white-space:pre-wrap;background:#f5f7fb;padding:18px}}</style></head><body><button onclick='print()'>Print / Save PDF</button><h1>{WebUtility.HtmlEncode(title)}</h1><h2>{WebUtility.HtmlEncode(subject)}</h2><pre>{safe}</pre></body></html>";
    }
}

public sealed record VehicleEnrollmentRequest(
    string Vin, string RegistrationNumber, string Model, string? Variant, string? Status,
    DateTime? PurchaseDate, DateTime? CommissioningDate, decimal? OdometerKm, decimal? OperatingHours,
    decimal? EnergyKwh, decimal? BatterySoc, string? DepotCode, string? ServiceCentreCode);
