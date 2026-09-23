using MontraFleet.Api.Data;

namespace MontraFleet.Api.Services;

public sealed record VeerAiApiDefinition(
    string Key,
    string Route,
    string Method,
    string Module,
    string[] Dependencies,
    string DataSensitivity,
    bool ReadOnly,
    string Owner,
    string Version);

public static class VeeraiApiCatalog
{
    private static readonly VeerAiApiDefinition[] Definitions =
    [
        new("chat", "/api/veerai/chat", "POST", "Conversation", ["vehicles", "job-cards"], "Controlled operational evidence", true, "Fleet Platform", "v1"),
        new("job-analysis", "/api/job-cards/{id}/veerai/analyse", "POST", "Job Cards", ["vehicles", "service-events", "job-cards"], "Controlled operational evidence", true, "Fleet Platform", "v1"),
        new("vehicle-evidence", "internal:vehicle-evidence-reader", "INTERNAL", "Vehicles", ["vehicles"], "Vehicle enrollment and status", true, "Fleet Platform", "v1"),
        new("job-evidence", "internal:job-evidence-reader", "INTERNAL", "Job Cards", ["vehicles", "service-events", "job-cards"], "Job Card maintenance evidence", true, "Fleet Platform", "v1")
    ];

    public static IReadOnlyList<VeerAiApiDefinition> All => Definitions;

    public static bool IsRegistered(string key)
        => Definitions.Any(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase));

    public static void Map(WebApplication app)
    {
        app.MapGet("/api/ai/api-catalog", (HttpRequest request, IConfiguration configuration) =>
        {
            var expected = configuration["Veerai:AdminKey"];
            if (string.IsNullOrWhiteSpace(expected)
                || !string.Equals(request.Headers["X-Veerai-Admin-Key"].FirstOrDefault(), expected, StringComparison.Ordinal))
                return Results.Unauthorized();

            return Results.Ok(new
            {
                failClosed = true,
                policy = "Only registered, read-only VeerAI APIs may be used. New endpoints require catalog registration.",
                apis = Definitions
            });
        });
    }
}
