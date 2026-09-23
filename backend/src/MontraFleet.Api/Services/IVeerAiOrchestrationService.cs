using MontraFleet.Api.Data;

namespace MontraFleet.Api.Services;

public interface IVeerAiOrchestrationService
{
    Task<VeerAiOrchestrationResult?> AnalyseJobAsync(Guid jobCardId, string question, CancellationToken cancellationToken = default);
}

public sealed record VeerAiOrchestrationResult(
    VeerAnalysis Analysis,
    IReadOnlyList<VeerSource> Sources,
    DateTime AnalysedAt,
    string Model,
    string Notice);
