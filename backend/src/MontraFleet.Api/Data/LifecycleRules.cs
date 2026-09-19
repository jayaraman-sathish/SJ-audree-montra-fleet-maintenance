namespace MontraFleet.Api.Data;

public static class LifecycleRules
{
    // Closed is a service release, not merely the completion of its tasks.
    public static (string Service, string Job) Resolve(string service, string job,
        IReadOnlyCollection<string> tasks, bool assigned, bool taskTransition)
    {
        if (service == "Closed") return ("Closed", "Completed");
        if (service == "Cancelled" || job == "Cancelled") return ("Cancelled", "Cancelled");
        string status;
        if (taskTransition && tasks.Contains("In Progress")) status = "In Progress";
        else if (taskTransition && tasks.Contains("On Hold")) status = "On Hold";
        else if (taskTransition && tasks.Contains("Completed")) status = "In Progress";
        else if (service == "On Hold" || job == "On Hold") status = "On Hold";
        else if (service == "In Progress" || job == "In Progress" || job == "Completed") status = "In Progress";
        else if (assigned) status = "Assigned";
        else status = "Awaiting Assignment";
        return (status, status);
    }
}
