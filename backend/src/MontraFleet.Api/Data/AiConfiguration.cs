using Microsoft.EntityFrameworkCore;

namespace MontraFleet.Api.Data;

public static class AiConfiguration
{
    private static readonly (string Code, string Name)[] Modules =
    [
        ("vehicles", "Vehicles"),
        ("job-cards", "Job Cards"),
        ("service-events", "Service Events"),
        ("breakdowns", "Breakdowns"),
        ("appointments", "Appointments"),
        ("pm", "Preventive Maintenance"),
        ("parts", "Parts and Inventory"),
        ("technicians", "Technicians"),
        ("documents", "Documents and Evidence"),
        ("audit", "Audit Records")
    ];

    public static string NameFor(string code) => Modules.FirstOrDefault(x => string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase)).Name ?? code;

    private static bool IsAdmin(HttpRequest request, IConfiguration configuration)
    {
        var expected = configuration["Veerai:AdminKey"];
        return !string.IsNullOrWhiteSpace(expected)
            && string.Equals(request.Headers["X-Veerai-Admin-Key"].FirstOrDefault(), expected, StringComparison.Ordinal);
    }

    public static bool IsEnabled(AppDbContext db, string module)
        => db.MasterOptions.AsNoTracking()
            .Where(x => x.Category == "AI_READ_ACCESS" && x.Code == module)
            .Select(x => (bool?)x.IsActive)
            .FirstOrDefault() ?? true;

    public static async Task EnsureDefaults(AppDbContext db, CancellationToken ct = default)
    {
        foreach (var module in Modules)
        {
            if (!await db.MasterOptions.AnyAsync(x => x.Category == "AI_READ_ACCESS" && x.Code == module.Code, ct))
                db.MasterOptions.Add(new Models.MasterOption
                {
                    Category = "AI_READ_ACCESS",
                    Code = module.Code,
                    Name = module.Name,
                    Description = "Read-only data access for VeerAI",
                    IsActive = true
                });
        }
        await db.SaveChangesAsync(ct);
    }

    public static void Map(this WebApplication app)
    {
        app.MapGet("/api/ai/configuration", async (HttpRequest request, AppDbContext db, IConfiguration configuration, CancellationToken ct) =>
        {
            if (!IsAdmin(request, configuration))
                return Results.Unauthorized();

            await EnsureDefaults(db, ct);
            var values = await db.MasterOptions.AsNoTracking()
                .Where(x => x.Category == "AI_READ_ACCESS")
                .ToDictionaryAsync(x => x.Code, x => new { code = x.Code, name = x.Name, enabled = x.IsActive }, ct);
            return Results.Ok(Modules.Select(x => values.TryGetValue(x.Code, out var value)
                ? value
                : new { code = x.Code, name = x.Name, enabled = true }).ToArray());
        });

        app.MapPut("/api/ai/configuration", async (AiConfigurationRequest input, HttpRequest request, AppDbContext db, IConfiguration configuration, CancellationToken ct) =>
        {
            if (!IsAdmin(request, configuration))
                return Results.Unauthorized();

            await EnsureDefaults(db, ct);
            var allowed = Modules.Select(x => x.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var submitted = input.Modules ?? Array.Empty<AiConfigurationModule>();
            var submittedCodes = submitted.Select(x => x.Code).ToArray();
            if (submitted.Length != Modules.Length
                || submittedCodes.Distinct(StringComparer.OrdinalIgnoreCase).Count() != Modules.Length
                || submitted.Any(x => string.IsNullOrWhiteSpace(x.Code) || !allowed.Contains(x.Code)))
                return Results.BadRequest(new { message = "The AI configuration must contain each approved data area exactly once." });

            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            foreach (var module in Modules)
            {
                var value = input.Modules.FirstOrDefault(x => string.Equals(x.Code, module.Code, StringComparison.OrdinalIgnoreCase));
                var setting = await db.MasterOptions.FirstAsync(x => x.Category == "AI_READ_ACCESS" && x.Code == module.Code, ct);
                setting.IsActive = value?.Enabled ?? true;
            }
            Audit(db, "UPDATE", "AiConfiguration", null, "Updated VeerAI read-only data access", input.UpdatedBy ?? "AI Administrator");
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return Results.Ok(new { saved = true, message = "AI configuration saved." });
        });
    }

    private static void Audit(AppDbContext db, string action, string entityType, Guid? entityId, string details, string user)
        => db.AuditEvents.Add(new Models.AuditEvent
        {
            UserName = user, Action = action, EntityType = entityType, EntityId = entityId,
            CorrelationId = Guid.NewGuid().ToString("N"), Details = details
        });
}

public sealed record AiConfigurationRequest(AiConfigurationModule[] Modules, string? UpdatedBy);
public sealed record AiConfigurationModule(string Code, bool Enabled);
