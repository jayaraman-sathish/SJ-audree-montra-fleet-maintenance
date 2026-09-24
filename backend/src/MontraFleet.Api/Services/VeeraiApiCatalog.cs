using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MontraFleet.Api.Data;

namespace MontraFleet.Api.Services;

public sealed record VeerAiApiDefinition(
    string Key, string Route, string Method, string Module, string[] Dependencies,
    string DataSensitivity, bool ReadOnly, string Owner, string Version, bool Enabled = true, bool Custom = false);

public static class VeeraiApiCatalog
{
    private static readonly VeerAiApiDefinition[] Definitions =
    [
        new("chat", "/api/veerai/chat", "POST", "Conversation", ["vehicles", "job-cards"], "Controlled operational evidence", true, "Fleet Platform", "v1"),
        new("job-analysis", "/api/job-cards/{id}/veerai/analyse", "POST", "Job Cards", ["vehicles", "service-events", "job-cards"], "Controlled operational evidence", true, "Fleet Platform", "v1"),
        new("vehicle-evidence", "internal:vehicle-evidence-reader", "INTERNAL", "Vehicles", ["vehicles"], "Vehicle enrollment and status", true, "Fleet Platform", "v1"),
        new("job-evidence", "internal:job-evidence-reader", "INTERNAL", "Job Cards", ["vehicles", "service-events", "job-cards"], "Job Card maintenance evidence", true, "Fleet Platform", "v1")
    ];

    static bool Admin(HttpRequest r, IConfiguration c) => !string.IsNullOrWhiteSpace(c["Veerai:AdminKey"])
        && string.Equals(r.Headers["X-Veerai-Admin-Key"].FirstOrDefault(), c["Veerai:AdminKey"], StringComparison.Ordinal);

    public static bool IsRegistered(string key) => Definitions.Any(x => x.Key.Equals(key, StringComparison.OrdinalIgnoreCase));

    public static void Map(WebApplication app)
    {
        app.MapGet("/api/ai/api-catalog", async (HttpRequest request, IConfiguration configuration, AppDbContext db, CancellationToken ct) =>
        {
            if (!Admin(request, configuration)) return Results.Unauthorized();
            var custom = await db.MasterOptions.AsNoTracking().Where(x => x.Category == "VEERAI_API_CATALOG" && x.IsActive).ToListAsync(ct);
            var registered = custom.Select(x => JsonSerializer.Deserialize<VeerAiApiDefinition>(x.Value)).Where(x => x is not null).Cast<VeerAiApiDefinition>();
            return Results.Ok(new { failClosed = true, policy = "Only registered APIs may be used by VeerAI. New endpoints require administrator registration and review.", apis = Definitions.Concat(registered) });
        });

        app.MapPost("/api/ai/api-catalog", async (VeerAiApiDefinition input, HttpRequest request, IConfiguration configuration, AppDbContext db, CancellationToken ct) =>
        {
            if (!Admin(request, configuration)) return Results.Unauthorized();
            if (string.IsNullOrWhiteSpace(input.Key) || string.IsNullOrWhiteSpace(input.Route) || string.IsNullOrWhiteSpace(input.Method) || input.Dependencies is null)
                return Results.BadRequest(new { message = "API ID, route, method and dependencies are required." });
            if (Definitions.Any(x => x.Key.Equals(input.Key, StringComparison.OrdinalIgnoreCase)) || await db.MasterOptions.AnyAsync(x => x.Category == "VEERAI_API_CATALOG" && x.Code == input.Key, ct))
                return Results.Conflict(new { message = "API ID already exists." });
            db.MasterOptions.Add(new Models.MasterOption { Category = "VEERAI_API_CATALOG", Code = input.Key.Trim(), Name = input.Route.Trim(), Value = JsonSerializer.Serialize(input with { Key = input.Key.Trim(), Enabled = true, Custom = true }), Description = "Administrator-registered VeerAI API", IsActive = true });
            await db.SaveChangesAsync(ct);
            return Results.Created("/api/ai/api-catalog/" + input.Key, input with { Key = input.Key.Trim(), Enabled = true });
        });

        app.MapPut("/api/ai/api-catalog/{key}", async (string key, VeerAiApiDefinition input, HttpRequest request, IConfiguration configuration, AppDbContext db, CancellationToken ct) =>
        {
            if (!Admin(request, configuration)) return Results.Unauthorized();
            var row = await db.MasterOptions.SingleOrDefaultAsync(x => x.Category == "VEERAI_API_CATALOG" && x.Code == key, ct);
            if (row is null) return Results.NotFound(new { message = "Only administrator-created API registrations can be edited." });
            row.Value = JsonSerializer.Serialize(input with { Key = key });
            row.Name = input.Route; row.IsActive = input.Enabled;
            await db.SaveChangesAsync(ct);
            return Results.Ok(input with { Key = key });
        });
    }
}
