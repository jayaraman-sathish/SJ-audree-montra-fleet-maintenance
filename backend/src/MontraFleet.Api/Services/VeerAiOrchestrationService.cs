using MontraFleet.Api.Data;

namespace MontraFleet.Api.Services;

/// <summary>
/// Coordinates VeerAI requests. This service is the policy boundary between the
/// HTTP endpoint, approved fleet evidence readers and the external AI provider.
/// </summary>
public sealed class VeerAiOrchestrationService : IVeerAiOrchestrationService
{
    private readonly AppDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public VeerAiOrchestrationService(
        AppDbContext db,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    public async Task<VeerAiOrchestrationResult?> AnalyseJobAsync(
        Guid jobCardId,
        string question,
        CancellationToken cancellationToken = default)
    {
        if (!Veerai.Configured(_configuration))
            throw new InvalidOperationException("VeerAI is not configured.");

        if (string.IsNullOrWhiteSpace(question) || question.Length > 1500)
            throw new ArgumentException("Enter a question up to 1500 characters.", nameof(question));

        // Evidence collection is deliberately bounded and read-only.
        var sources = await Veerai.Sources(_db, jobCardId, cancellationToken);
        if (sources is null)
            return null;

        if (System.Text.Json.JsonSerializer.Serialize(sources, Veerai.Json).Length > 100000)
            throw new InvalidOperationException("The job evidence is too large for one analysis.");

        var analysis = await Veerai.Analyse(
            _httpClientFactory.CreateClient("veerai"),
            _configuration["Veerai:ApiKey"]!,
            _configuration["Veerai:Model"]!,
            question,
            sources,
            cancellationToken);

        return new VeerAiOrchestrationResult(
            analysis,
            sources,
            DateTime.UtcNow,
            _configuration["Veerai:Model"]!,
            "AI advisory draft. Check evidence and approved procedures. No service records changed.");
    }
}
