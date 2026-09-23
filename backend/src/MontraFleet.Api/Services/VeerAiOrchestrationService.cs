using System.Text.Json;
using Microsoft.EntityFrameworkCore;
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
        var access = VeerAiAccessPolicy.Check(_db, ["job-cards"]);
        if (!access.Allowed)
            return new(null, "Access controlled", access.Message, []);

        return await VeeraiChat.Resolve(_db, question, currentJobId, cancellationToken);
    }

    public async Task<List<VeerSource>> GetEvidenceAsync(
        Guid jobCardId,
        CancellationToken cancellationToken = default)
    {
        var access = VeerAiAccessPolicy.Check(_db, ["job-cards"]);
        if (!access.Allowed)
            return [];

        return await Veerai.Sources(_db, jobCardId, cancellationToken) ?? [];
    }

    public async Task<List<VeerSource>> GetVehicleEvidenceAsync(
        Guid vehicleId,
        CancellationToken cancellationToken = default)
    {
        var access = VeerAiAccessPolicy.Check(_db, ["vehicles"]);
        if (!access.Allowed)
            return [];

        var vehicle = await _db.Vehicles.AsNoTracking()
            .Where(x => x.Id == vehicleId)
            .Select(x => new
            {
                x.Id,
                x.Vin,
                x.RegistrationNumber,
                x.Model,
                x.Variant,
                x.Status,
                x.DepotCode,
                x.ServiceCentreCode,
                x.OdometerKm,
                x.OperatingHours,
                x.EnergyKwh,
                x.BatterySoc,
                x.IsActive
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (vehicle is null)
            return [];

        return
        [
            new VeerSource(
                "vehicle-1",
                "Vehicle enrollment and current status",
                $"/vehicle-360/{vehicle.Id}",
                JsonSerializer.Serialize(vehicle, Veerai.Json))
        ];
    }

    public async Task<VeerAiOrchestrationResult?> AnalyseJobAsync(
        Guid jobCardId,
        string question,
        CancellationToken cancellationToken = default)
    {
        if (!Veerai.Configured(_configuration))
            throw new InvalidOperationException("VeerAI is not configured.");

        var access = VeerAiAccessPolicy.Check(_db, ["job-cards"]);
        if (!access.Allowed)
            throw new UnauthorizedAccessException(access.Message);

        if (string.IsNullOrWhiteSpace(question) || question.Length > 1500)
            throw new ArgumentException("Enter a question up to 1500 characters.", nameof(question));

        // Evidence collection is deliberately bounded and read-only.
        var sources = await Veerai.Sources(_db, jobCardId, cancellationToken);
        if (sources is null)
            return null;

        if (JsonSerializer.Serialize(sources, Veerai.Json).Length > 100000)
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
