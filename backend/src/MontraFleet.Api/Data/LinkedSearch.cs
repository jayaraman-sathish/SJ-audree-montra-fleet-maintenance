using Microsoft.EntityFrameworkCore;

namespace MontraFleet.Api.Data;

public static class LinkedSearch
{
    public static async Task<object> FindAsync(AppDbContext db, string? q)
    {
        var term = (q ?? "").Trim().ToLowerInvariant();
        var results = new List<object>();
        if (term.Length == 0) return new { query = q ?? "", results };
        var vehicles = await db.Vehicles.AsNoTracking()
            .Where(x => x.RegistrationNumber.ToLower().Contains(term) || x.Vin.ToLower().Contains(term) || x.Model.ToLower().Contains(term))
            .OrderBy(x => x.RegistrationNumber).Take(15).ToListAsync();
        foreach (var v in vehicles)
            results.Add(new { type = "Vehicle", key = v.RegistrationNumber, title = v.Model + " · " + v.Vin,
                status = v.Status, url = "/vehicle?id=" + v.Id });
        var visits = await (from e in db.ServiceEvents.AsNoTracking()
            join v in db.Vehicles.AsNoTracking() on e.VehicleId equals v.Id
            where e.EventNumber.ToLower().Contains(term) || v.RegistrationNumber.ToLower().Contains(term) || v.Vin.ToLower().Contains(term)
                || db.JobCards.Any(j => j.ServiceEventId == e.Id && j.JobCardNumber.ToLower().Contains(term))
                || db.Breakdowns.Any(b => b.Id == e.BreakdownId && (b.BreakdownNumber.ToLower().Contains(term) || b.Complaint.ToLower().Contains(term)))
                || db.MaintenanceRequests.Any(r => r.JobCardId.HasValue &&
                    db.JobCards.Any(j => j.Id == r.JobCardId && j.ServiceEventId == e.Id) &&
                    (r.RequestNumber.ToLower().Contains(term) || r.Description.ToLower().Contains(term)))
            orderby e.OpenedAt descending
            select new { Event = e, Vehicle = v }).Take(30).ToListAsync();
        var eventIds = visits.Select(x => x.Event.Id).ToList();
        var jobs = await db.JobCards.AsNoTracking().Where(x => eventIds.Contains(x.ServiceEventId)).ToListAsync();
        var jobIds = jobs.Select(x => x.Id).ToList();
        var tasks = await db.WorkItems.AsNoTracking().Where(x => jobIds.Contains(x.JobCardId)).ToListAsync();
        var instances = await db.WorkTemplateInstances.AsNoTracking().Where(x => jobIds.Contains(x.JobCardId)).ToListAsync();
        var instanceIds = instances.Select(x => x.Id).ToList();
        var fields = await db.WorkTemplateFieldInstances.AsNoTracking().Where(x => instanceIds.Contains(x.WorkTemplateInstanceId)).ToListAsync();
        var legacyChecks = await db.ChecklistExecutions.AsNoTracking().Where(x => jobIds.Contains(x.JobCardId)).ToListAsync();
        var parts = await db.PartRequests.AsNoTracking().Where(x => jobIds.Contains(x.JobCardId)).ToListAsync();
        var inspections = await db.QcInspections.AsNoTracking().Where(x => jobIds.Contains(x.JobCardId)).ToListAsync();
        var requests = await db.MaintenanceRequests.AsNoTracking().Where(x => x.JobCardId.HasValue && jobIds.Contains(x.JobCardId.Value)).ToListAsync();
        var breakdownIds = visits.Where(x => x.Event.BreakdownId.HasValue).Select(x => x.Event.BreakdownId!.Value).ToList();
        var breakdowns = await db.Breakdowns.AsNoTracking().Where(x => breakdownIds.Contains(x.Id)).ToListAsync();
        foreach (var visit in visits)
        {
            var e = visit.Event;
            var cards = new List<object>();
            foreach (var job in jobs.Where(x => x.ServiceEventId == e.Id))
            {
                var work = tasks.Where(x => x.JobCardId == job.Id && x.Status != "Cancelled").ToList();
                var templateIds = instances.Where(x => x.JobCardId == job.Id).Select(x => x.Id).ToHashSet();
                var checks = fields.Where(x => templateIds.Contains(x.WorkTemplateInstanceId)).ToList();
                var legacy = legacyChecks.Where(x => x.JobCardId == job.Id).ToList();
                var completed = work.Count(x => x.Status == "Completed");
                var waiting = parts.Count(x => x.JobCardId == job.Id && x.Status != "Cancelled" && x.Status != "Returned" &&
                    x.Status != "Consumed" && x.QuantityIssued < x.QuantityRequired);
                var qc = inspections.Where(x => x.JobCardId == job.Id && x.InspectedAt.HasValue).OrderByDescending(x => x.InspectedAt).FirstOrDefault();
                var qcCurrent = qc != null && !work.Any(x => x.UpdatedAt > qc.InspectedAt!.Value);
                var qcStatus = qc == null ? "Pending" : !qcCurrent ? "Review Required" :
                    qc.Result == "Pass" && qc.RoadTestRequired && !qc.RoadTestPassed ? "Road Test Pending" : qc.Result;
                var stage = e.Status == "Closed" ? "Released" : e.Status == "Cancelled" ? "Cancelled" :
                    work.Count == 0 ? "Work Not Generated" : work.Any(x => x.Status == "Pending Approval") ? "Approval Pending" :
                    waiting > 0 ? "Parts Waiting" : work.Any(x => x.Status == "In Progress") ? "Work In Progress" :
                    work.Any(x => x.Status == "On Hold") ? "On Hold" : completed < work.Count ?
                    work.Any(x => x.AssignedToTechnicianId.HasValue) || job.TechnicianId.HasValue ? "Assigned / Work Pending" : "Technician Allocation Pending" :
                    qcStatus == "Pass" ? "Ready for Release Review" : "QC Pending";
                cards.Add(new { id = job.Id, number = job.JobCardNumber, status = job.Status, stage,
                    assignedSupervisor = e.AssignedSupervisor, supervisorAssignedAt = e.SupervisorAssignedAt,
                    engineer = string.IsNullOrWhiteSpace(job.Technician) ? "Unassigned" : job.Technician,
                    taskEngineers = work.Select(x => x.AssignedTo).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToArray(),
                    bay = string.IsNullOrWhiteSpace(job.Bay) ? "Not assigned" : job.Bay,
                    tasksTotal = work.Count, tasksCompleted = completed,
                    checksTotal = checks.Count + legacy.Count,
                    checksRecorded = checks.Count(x => x.Result != "Pending" && !string.IsNullOrWhiteSpace(x.Value)) + legacy.Count(x => x.Result != "Pending"),
                    partsWaiting = waiting, qcStatus, url = "/service-workspace/" + job.Id,
                    additionalWork = work.Where(x => x.WorkType == "Additional Work").Select(x => new { x.TaskCode, x.Description, x.Status }).ToArray() });
            }
            var b = breakdowns.FirstOrDefault(x => x.Id == e.BreakdownId);
            var visitJobs = jobs.Where(x => x.ServiceEventId == e.Id).Select(x => x.Id).ToHashSet();
            var issues = requests.Where(x => x.JobCardId.HasValue && visitJobs.Contains(x.JobCardId.Value))
                .Select(x => new { reference = x.RequestNumber, description = x.Description, reportedAt = (DateTime?)x.RequestedAt }).ToList();
            if (b != null) issues.Insert(0, new { reference = b.BreakdownNumber, description = b.Complaint, reportedAt = (DateTime?)b.ReportedAt });
            results.Add(new { type = "Service Visit", key = e.EventNumber, title = visit.Vehicle.RegistrationNumber + " · " + e.EventType,
                status = e.Status == "Closed" ? "Released" : e.Status, openedAt = e.OpenedAt, jobs = cards, issues,
                issueSummary = issues.Count > 0 ? "" : e.EventType == "PM" ? "Scheduled preventive maintenance; no reported complaint linked." : "No reported issue linked." });
        }
        var unlinked = await db.MaintenanceRequests.AsNoTracking().Where(r => !r.JobCardId.HasValue &&
            (r.RequestNumber.ToLower().Contains(term) || r.Description.ToLower().Contains(term) ||
                db.Vehicles.Any(v => v.Id == r.VehicleId && (v.RegistrationNumber.ToLower().Contains(term) || v.Vin.ToLower().Contains(term)))))
            .OrderByDescending(x => x.RequestedAt).Take(15).ToListAsync();
        foreach (var r in unlinked)
            results.Add(new { type = "Maintenance Request", key = r.RequestNumber, title = r.Description,
                status = r.Status, reportedAt = r.RequestedAt, url = "/maintenance-requests" });
        return new { query = q ?? "", results, visitLimit = 30, visitLimitReached = visits.Count == 30 };
    }
}
