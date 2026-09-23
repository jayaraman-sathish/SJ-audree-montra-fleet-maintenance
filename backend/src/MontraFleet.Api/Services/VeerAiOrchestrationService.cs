using MontraFleet.Api.Data;

namespace MontraFleet.Api.Services;

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

    public async Task<VeerChatContext> ResolveChatContextAsync(
        string question,
        Guid? currentJobId,
        CancellationToken cancellationToken = default)
    {
        // Do not read or resolve Job Cards when disabled.
        if (!AiConfiguration.IsEnabled(_db, "job-cards"))
        {
            return new VeerChatContext(
                null,
                "General Montra guidance",
                null,
                Array.Empty<VeerChatChoice>());
        }

        return await VeeraiChat.Resolve(
            _db,
            question,
            currentJobId,
            cancellationToken);
    }

    public async Task<List<VeerSource>> GetEvidenceAsync(
        Guid jobCardId,
        CancellationToken cancellationToken = default)
    {
        // Enforce the module permission at the orchestration boundary.
        if (!AiConfiguration.IsEnabled(_db, "job-cards"))
            return new List<VeerSource>();

        return await Veerai.Sources(_db, jobCardId, cancellationToken)
            ?? new List<VeerSource>();
    }

    public async Task<VeerAiOrchestrationResult?> AnalyseJobAsync(
        Guid jobCardId,
        string question,
        CancellationToken cancellationToken = default)
    {
        if (!AiConfiguration.IsEnabled(_db, "job-cards"))
            throw new InvalidOperationException(
                "Job Card access is disabled for VeerAI.");

        if (!Veerai.Configured(_configuration))
            throw new InvalidOperationException("VeerAI is not configured.");

        if (string.IsNullOrWhiteSpace(question) || question.Length > 1500)
            throw new ArgumentException(
                "Enter a question up to 1500 characters.",
                nameof(question));

        var sources = await Veerai.Sources(
            _db,
            jobCardId,
            cancellationToken);

        if (sources is null)
            return null;

        if (System.Text.Json.JsonSerializer.Serialize(
                sources,
                Veerai.Json).Length > 100000)
        {
            throw new InvalidOperationException(
                "The job evidence is too large for one analysis.");
        }

        var model = _configuration["Veerai:Model"]!;
        var analysis = await Veerai.Analyse(
            _httpClientFactory.CreateClient("veerai"),
            _configuration["Veerai:ApiKey"]!,
            model,
            question,
            sources,
            cancellationToken);

        return new VeerAiOrchestrationResult(
            analysis,
            sources,
            DateTime.UtcNow,
            model,
            "AI advisory draft. Check evidence and approved procedures. No service records changed.");
    }
}