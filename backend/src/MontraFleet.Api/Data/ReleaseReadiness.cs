using Microsoft.EntityFrameworkCore;
using MontraFleet.Api.Models;
namespace MontraFleet.Api.Data;

public record ReleaseBlocker(string Kind, string Reference, string Message, Guid? WorkItemId = null);
public record ReleaseReadinessResult(bool Released, bool CanQc, bool CanRelease, string QcStatus,
    List<ReleaseBlocker> Blockers);
public static class ReleaseReadiness
{
    public static Task<bool> TaskIsClosedAsync(AppDbContext db, Guid id) =>
        (from t in db.WorkItems join j in db.JobCards on t.JobCardId equals j.Id
         join e in db.ServiceEvents on j.ServiceEventId equals e.Id
         where t.Id == id && (e.Status == "Closed" || e.Status == "Cancelled" || t.Status == "Cancelled") select t.Id).AnyAsync();

    public static async Task<ReleaseReadinessResult> ReadAsync(AppDbContext db, Guid jobId)
    {
        var job = await db.JobCards.AsNoTracking().SingleAsync(x => x.Id == jobId);
        var visit = await db.ServiceEvents.AsNoTracking().SingleAsync(x => x.Id == job.ServiceEventId);
        var blockers = new List<ReleaseBlocker>();
        var released = visit.Status == "Closed";
        var tasks = await db.WorkItems.AsNoTracking().Where(x => x.JobCardId == jobId && x.Status != "Cancelled").ToListAsync();
        if (visit.Status == "Cancelled" || job.Status == "Cancelled") blockers.Add(new("Service", visit.EventNumber, "This service is cancelled."));
        if (string.IsNullOrWhiteSpace(visit.AssignedSupervisor)) blockers.Add(new("Assignment", job.JobCardNumber, "Assign the supervisor responsible for this service."));
        if (tasks.Count == 0) blockers.Add(new("Task", job.JobCardNumber, "No active work task exists. Add and complete the required work."));
        foreach (var task in tasks.Where(x => x.Status != "Completed"))
            blockers.Add(new("Task", task.TaskCode, $"{task.Description} — {task.Status}. Complete this task after recording its work.", task.Id));
        var ids = tasks.Select(x => x.Id).ToList();
        var instances = await db.WorkTemplateInstances.AsNoTracking().Where(x => ids.Contains(x.WorkItemId)).ToListAsync();
        var instanceIds = instances.Select(x => x.Id).ToList();
        var fields = await db.WorkTemplateFieldInstances.AsNoTracking().Where(x => instanceIds.Contains(x.WorkTemplateInstanceId)).ToListAsync();
        foreach (var field in fields.Where(x => x.IsMandatory && (string.IsNullOrWhiteSpace(x.Value) || x.Result == "Pending") || x.Result == "Fail" || x.Result == "Not OK"))
            blockers.Add(new("Checklist", field.FieldCode, $"{field.Label}: record the required result or complete corrective work and recheck.", instances.Single(x => x.Id == field.WorkTemplateInstanceId).WorkItemId));
        var defects = await db.Defects.AsNoTracking().Where(x => x.JobCardId == jobId && x.Disposition != "Closed").ToListAsync();
        foreach (var defect in defects) blockers.Add(new("Issue", defect.DefectNumber, defect.Description));
        var parts = await db.PartRequests.AsNoTracking().Where(x => x.JobCardId == jobId && x.Status != "Consumed" && x.Status != "Returned" && x.Status != "Cancelled").ToListAsync();
        foreach (var part in parts) blockers.Add(new("Parts", part.RequestNumber, $"Parts request is {part.Status}. Consume, return or cancel it in Parts & Labour."));
        var legacy = await db.ChecklistExecutions.AsNoTracking().Where(x => x.JobCardId == jobId).ToListAsync();
        foreach (var check in legacy.Where(x => (x.IsMandatory && x.Result == "Pending") || x.Result == "Fail" || x.Result == "Not OK"))
            blockers.Add(new("Checklist", check.ItemCode, $"{check.ItemText}: complete or recheck this checklist item.", check.WorkItemId));
        if (await db.ServiceEvents.AnyAsync(x => x.VehicleId == visit.VehicleId && x.Id != visit.Id && x.Status != "Closed" && x.Status != "Cancelled"))
            blockers.Add(new("Service", visit.EventNumber, "Another active service exists for this vehicle. Resolve it before release."));
        if(await db.OffHireRecords.AnyAsync(x=>x.VehicleId==visit.VehicleId&&x.Status=="Approved"))blockers.Add(new("Off-Hire",job.JobCardNumber,"Vehicle is off-hire. Complete recommissioning before release."));
        var canQc = !released && blockers.Count == 0;
        var qc = await db.QcInspections.AsNoTracking().Where(x => x.JobCardId == jobId).OrderByDescending(x => x.InspectedAt).FirstOrDefaultAsync();
        var allParts = await db.PartRequests.AsNoTracking().Where(x => x.JobCardId == jobId).ToListAsync();
        var allDefects = await db.Defects.AsNoTracking().Where(x => x.JobCardId == jobId).ToListAsync();
        var lastChange = tasks.Select(x => (DateTime?)x.UpdatedAt).Concat(fields.Select(x => x.ExecutedAt)).Concat(legacy.Select(x => x.ExecutedAt)).Concat(allParts.Select(x => (DateTime?)x.UpdatedAt)).Concat(allDefects.Select(x => x.ClosedAt)).Max();
        var qcStatus = qc?.Result ?? "Pending";
        if (qc == null || qc.Result != "Pass") blockers.Add(new("QC", job.JobCardNumber, "Record passing QC after completing the work."));
        else if (!qc.InspectedAt.HasValue || (lastChange.HasValue && lastChange.Value > qc.InspectedAt.Value)) { qcStatus = "Recheck required"; blockers.Add(new("QC", job.JobCardNumber, "Work changed after QC. Perform QC again.")); }
        else if (qc.RoadTestRequired && !qc.RoadTestPassed) blockers.Add(new("QC", job.JobCardNumber, "The required road test has not passed."));
        return new(released, canQc, !released && blockers.Count == 0, qcStatus, released ? new() : blockers);
    }
}
