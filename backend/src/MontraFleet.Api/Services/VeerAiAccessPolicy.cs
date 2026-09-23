using MontraFleet.Api.Data;

namespace MontraFleet.Api.Services;

public sealed record VeerAiAccessDecision(bool Allowed, string Message, IReadOnlyList<string> RequiredModules);

/// <summary>
/// Centralizes business-data dependencies for VeerAI. A module can only be read when
/// every module needed to establish its meaning is enabled.
/// </summary>
public static class VeerAiAccessPolicy
{
    private static readonly IReadOnlyDictionary<string, string[]> Dependencies =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["vehicles"] = ["vehicles"],
            ["service-events"] = ["vehicles", "service-events"],
            ["job-cards"] = ["vehicles", "service-events", "job-cards"],
            ["breakdowns"] = ["vehicles", "service-events", "breakdowns"],
            ["appointments"] = ["appointments"],
            ["pm"] = ["vehicles", "pm"],
            ["parts"] = ["parts"],
            ["technicians"] = ["vehicles", "service-events", "job-cards", "technicians"],
            ["documents"] = ["vehicles", "service-events", "job-cards", "documents"],
            ["audit"] = ["audit"]
        };

    public static IReadOnlyList<string> Expand(IEnumerable<string> requestedModules)
        => requestedModules
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .SelectMany(x => Dependencies.TryGetValue(x, out var required) ? required : [x])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    public static VeerAiAccessDecision Check(AppDbContext db, IEnumerable<string> requestedModules)
    {
        var required = Expand(requestedModules);
        var disabled = required
            .Where(module => !AiConfiguration.IsEnabled(db, module))
            .Select(AiConfiguration.NameFor)
            .ToArray();

        if (disabled.Length == 0)
            return new(true, string.Empty, required);

        return new(
            false,
            $"No access to {string.Join(", ", disabled)} data is enabled for VeerAI. Enable the required data areas in AI Configuration.",
            required);
    }
}
