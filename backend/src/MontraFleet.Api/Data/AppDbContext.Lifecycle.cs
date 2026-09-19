using Microsoft.EntityFrameworkCore;
using MontraFleet.Api.Models;

namespace MontraFleet.Api.Data;

public partial class AppDbContext
{
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => SaveChangesAsync(true, cancellationToken);

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        await SynchronizeChangedVisitsAsync(cancellationToken);
        return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private async Task SynchronizeChangedVisitsAsync(CancellationToken ct)
    {
        ChangeTracker.DetectChanges();
        var changedTasks = ChangeTracker.Entries<WorkItem>()
            .Where(x => x.State == EntityState.Added || x.State == EntityState.Modified).ToList();
        var ids = changedTasks.Select(x => x.Entity.JobCardId).ToHashSet();
        foreach (var entry in ChangeTracker.Entries<JobCard>().Where(x => x.State == EntityState.Added || x.State == EntityState.Modified))
            ids.Add(entry.Entity.Id);
        var eventIds = ChangeTracker.Entries<ServiceEvent>().Where(x => x.State == EntityState.Added || x.State == EntityState.Modified)
            .Select(x => x.Entity.Id).ToList();
        if (eventIds.Count > 0)
        {
            var persistedIds = await JobCards.Where(x => eventIds.Contains(x.ServiceEventId)).Select(x => x.Id).ToListAsync(ct);
            ids.UnionWith(persistedIds);
            ids.UnionWith(JobCards.Local.Where(x => eventIds.Contains(x.ServiceEventId)).Select(x => x.Id));
        }
        foreach (var id in ids)
        {
            var job = await JobCards.FindAsync(new object[] { id }, ct);
            if (job is null) continue;
            var service = await ServiceEvents.FindAsync(new object[] { job.ServiceEventId }, ct);
            if (service is null) continue;
            var taskTransition = changedTasks.Any(x => x.Entity.JobCardId == id && x.Property(t => t.Status).IsModified);
            await WorkItems.Where(x => x.JobCardId == id).LoadAsync(ct);
            var tasks = WorkItems.Local.Where(x => x.JobCardId == id && Entry(x).State != EntityState.Deleted).ToList();
            var state = LifecycleRules.Resolve(Entry(service).State == EntityState.Added ? "Open" : service.Status,
                Entry(job).State == EntityState.Added ? "Open" : job.Status, tasks.Select(x => x.Status).ToArray(),
                !string.IsNullOrWhiteSpace(service.AssignedSupervisor), taskTransition);
            var oldEvent = Entry(service).State == EntityState.Added ? "New" : Entry(service).Property(x => x.Status).OriginalValue;
            var oldJob = Entry(job).State == EntityState.Added ? "New" : Entry(job).Property(x => x.Status).OriginalValue;
            service.Status = state.Service;
            job.Status = state.Job;
            if (oldEvent != service.Status || oldJob != job.Status)
                WorkLogEntries.Add(new WorkLogEntry { JobCardId = id, EntryType = "Service Status",
                    Comment = $"{service.EventNumber}: {oldEvent} -> {service.Status}; {job.JobCardNumber}: {oldJob} -> {job.Status}",
                    CreatedBy = "System", CreatedRole = "System" });
        }
        foreach (var entry in changedTasks.Where(x => x.State == EntityState.Modified))
        {
            var status = entry.Property(x => x.Status);
            var engineer = entry.Property(x => x.AssignedTo);
            if (!status.IsModified && !engineer.IsModified) continue;
            WorkLogEntries.Add(new WorkLogEntry { JobCardId = entry.Entity.JobCardId, WorkItemId = entry.Entity.Id,
                EntryType = "Task Update", Comment = $"{entry.Entity.TaskCode}: {status.OriginalValue} -> {status.CurrentValue}; Engineer: {engineer.CurrentValue}",
                CreatedBy = "System", CreatedRole = "System" });
        }
    }

    // Runs after schema initialization. It is idempotent and never fabricates
    // dates or a vehicle release. The original statuses remain in the work log.
    public async Task ReconcileVisitStatusesAsync(CancellationToken ct = default)
    {
        var pairs = await (from j in JobCards join e in ServiceEvents on j.ServiceEventId equals e.Id
            where e.Status != "Closed" && e.Status != "Cancelled"
            select new { Job = j, Service = e }).ToListAsync(ct);
        foreach (var pair in pairs)
        {
            var state = LifecycleRules.Resolve(pair.Service.Status, pair.Job.Status, Array.Empty<string>(), !string.IsNullOrWhiteSpace(pair.Service.AssignedSupervisor), false);
            pair.Service.Status = state.Service;
            pair.Job.Status = state.Job;
        }
        if (pairs.Count > 0) await SaveChangesAsync(ct);
    }
}
