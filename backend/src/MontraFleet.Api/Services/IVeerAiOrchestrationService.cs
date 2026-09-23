using MontraFleet.Api.Data;

namespace MontraFleet.Api.Services;

public interface IVeerAiOrchestrationService
{
    Task<VeerAiOrchestrationResult?> AnalyseJobAsync(Guid jobCardId, string question, CancellationToken cancellationToken = default);
    Task<VeerChatContext> ResolveChatContextAsync(string question, Guid? currentJobId, CancellationToken cancellationToken = default);
    Task<List<VeerSource>> GetEvidenceAsync(Guid jobCardId, CancellationToken cancellationToken = default);
    Task<List<VeerSource>> GetVehicleEvidenceAsync(Guid vehicleId, CancellationToken cancellationToken = default);
}

public sealed record VeerAiOrchestrationResult(
    VeerAnalysis Analysis,
    IReadOnlyList<VeerSource> Sources,
    DateTime AnalysedAt,
    string Model,
    string Notice);
