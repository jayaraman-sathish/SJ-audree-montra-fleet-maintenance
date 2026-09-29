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
            var summary = $"<div class='summary-grid'>{Field("VIN", vehicle.Vin)}{Field("Model", vehicle.Model)}{Field("Variant", vehicle.Variant)}{Field("Status", vehicle.Status)}{Field("Odometer", vehicle.OdometerKm.ToString("N0") + " km")}{Field("Operating hours", vehicle.OperatingHours.ToString("N0") + " hr")}{Field("Energy", vehicle.EnergyKwh.ToString("N0") + " kWh")}{Field("Depot", vehicle.DepotCode)}{Field("Service centre", vehicle.ServiceCentreCode)}</div>";
            var rows = string.Join("", history.Select(x => $"<tr><td>{E(x.EventNumber)}</td><td>{E(x.EventType)}</td><td>{E(x.Status)}</td><td>{x.OpenedAt:dd-MMM-yyyy}</td><td>{(x.ClosedAt.HasValue ? x.ClosedAt.Value.ToString("dd-MMM-yyyy") : "Open")}</td></tr>"));
            var historyHtml = $"<h2>Service history</h2><table><thead><tr><th>Event</th><th>Type</th><th>Status</th><th>Opened</th><th>Closed</th></tr></thead><tbody>{rows}</tbody></table>";
            return Results.Content(PrintHtml("Vehicle Maintenance Report", vehicle.RegistrationNumber, summary + historyHtml), "text/html", Encoding.UTF8);
        });

        app.MapGet("/api/reports/job-cards/{id:guid}", async (Guid id, HttpRequest request, AppDbContext db, IConfiguration configuration, CancellationToken ct) =>
        {
            if (!IsAdmin(request, configuration)) return Results.Unauthorized();
            var job = await db.JobCards.AsNoTracking().Where(x => x.Id == id)
                .Select(x => new { x.JobCardNumber, x.Status, x.StartedAt, x.CompletedAt, x.ServiceEventId }).SingleOrDefaultAsync(ct);
            if (job is null) return Results.NotFound();
            var tasks = await db.WorkItems.AsNoTracking().Where(x => x.JobCardId == id)
                .Select(x => new { x.TaskCode, x.Description, x.Status, x.AssignedTo, x.CompletionRemarks }).ToListAsync(ct);
            var summary = $"<div class='summary-grid'>{Field("Job card", job.JobCardNumber)}{Field("Status", job.Status)}{Field("Started", job.StartedAt?.ToString("dd-MMM-yyyy") ?? "—")}{Field("Completed", job.CompletedAt?.ToString("dd-MMM-yyyy") ?? "—")}</div>";
            var rows = string.Join("", tasks.Select(x => $"<tr><td>{E(x.TaskCode)}</td><td>{E(x.Description)}</td><td>{E(x.Status)}</td><td>{E(x.AssignedTo)}</td><td>{E(x.CompletionRemarks)}</td></tr>"));
            var taskHtml = $"<h2>Completed work</h2><table><thead><tr><th>Task</th><th>Description</th><th>Status</th><th>Assigned to</th><th>Remarks</th></tr></thead><tbody>{rows}</tbody></table>";
            return Results.Content(PrintHtml("Job Card Completion Report", job.JobCardNumber, summary + taskHtml), "text/html", Encoding.UTF8);
        });
    }

    private static string PrintHtml(string title, string subject, string body)
    {
        return $"<!doctype html><html><head><meta charset='utf-8'><meta name='viewport' content='width=device-width'><title>{E(title)}</title><style>body{{font-family:Arial,sans-serif;margin:32px;color:#14213d;max-width:1100px}}button{{padding:10px 18px;border:0;border-radius:6px;background:#1266d5;color:white;font-weight:700}}h1{{margin-bottom:4px}}h2{{margin-top:28px;border-bottom:1px solid #dbe3ef;padding-bottom:8px}}.summary-grid{{display:grid;grid-template-columns:repeat(3,1fr);gap:12px;margin:20px 0}}.field{{border:1px solid #dbe3ef;border-radius:8px;padding:12px}}.field b{{display:block;color:#64748b;font-size:12px;margin-bottom:6px}}table{{border-collapse:collapse;width:100%;margin-top:12px}}th,td{{border:1px solid #dbe3ef;padding:10px;text-align:left;vertical-align:top}}th{{background:#f5f7fb}}@media print{{button{{display:none}}body{{margin:12mm}}}}</style></head><body><button onclick='print()'>Print / Save PDF</button><h1>{E(title)}</h1><p><b>Record:</b> {E(subject)} · Generated {DateTime.UtcNow:dd-MMM-yyyy HH:mm} UTC</p>{body}</body></html>";
    }
    private static string E(object? value) => WebUtility.HtmlEncode(value?.ToString() ?? "—");
    private static string Field(string label, string? value) => $"<div class='field'><b>{E(label)}</b>{E(value)}</div>";
}

public sealed record VehicleEnrollmentRequest(
    string Vin, string RegistrationNumber, string Model, string? Variant, string? Status,
    DateTime? PurchaseDate, DateTime? CommissioningDate, decimal? OdometerKm, decimal? OperatingHours,
    decimal? EnergyKwh, decimal? BatterySoc, string? DepotCode, string? ServiceCentreCode);
