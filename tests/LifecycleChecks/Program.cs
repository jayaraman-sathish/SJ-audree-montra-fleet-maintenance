using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MontraFleet.Api.Data;
using MontraFleet.Api.Models;

void Check(bool ok, string name) { if (!ok) throw new Exception("FAILED: " + name); Console.WriteLine("PASS: " + name); }
var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
await using var db = new AppDbContext(options);
var vehicle = new Vehicle { RegistrationNumber = "AP39RH4004", Vin = "TEST-VIN", Model = "Rhino", Status = "Under Maintenance" };
var service = new ServiceEvent { VehicleId = vehicle.Id, EventNumber = "SE-TEST-1", EventType = "PM", Status = "In Progress" };
var job = new JobCard { ServiceEventId = service.Id, JobCardNumber = "JC-TEST-1", Status = "Open" };
var task = new WorkItem { JobCardId = job.Id, TaskCode = "T-1", Status = "Not Started" };
db.AddRange(vehicle, service, job, task);
db.SaveChanges(); // Deliberately seed the legacy mismatch without the async lifecycle hook.
await db.ReconcileVisitStatusesAsync();
Check(job.Status == "In Progress" && service.Status == "In Progress", "Existing mismatch reconciled");
var logCount = await db.WorkLogEntries.CountAsync();
await db.ReconcileVisitStatusesAsync();
Check(await db.WorkLogEntries.CountAsync() == logCount, "Reconciliation is idempotent");
task.Status = "On Hold"; await db.SaveChangesAsync();
Check(job.Status == "On Hold" && service.Status == "On Hold", "Hold updates both records");
task.Status = "In Progress"; await db.SaveChangesAsync();
Check(job.Status == "In Progress" && service.Status == "In Progress", "Resume clears hold on both records");
task.Status = "Completed"; await db.SaveChangesAsync();
Check(service.Status != "Closed" && job.Status != "Completed", "Task completion does not release vehicle");
var extra = new WorkItem { JobCardId = job.Id, TaskCode = "T-EXTRA", WorkType = "Additional Work", Description = "Replace lamp", Status = "Pending Approval" };
db.Add(extra); await db.SaveChangesAsync();
Check(await db.JobCards.CountAsync() == 1 && await db.ServiceEvents.CountAsync() == 1, "Additional work remains under same job");
db.PartRequests.Add(new PartRequest { JobCardId = job.Id, RequestNumber = "PR-TEST", QuantityRequired = 2, QuantityIssued = 1, Status = "Partially Issued" });
await db.SaveChangesAsync();
var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
using var search = JsonDocument.Parse(JsonSerializer.Serialize(await LinkedSearch.FindAsync(db, "AP39RH4004"), jsonOptions));
var visits = search.RootElement.GetProperty("results").EnumerateArray().Where(x => x.GetProperty("type").GetString() == "Service Visit").ToList();
Check(visits.Count == 1, "Search groups event and job in one visit");
var summary = visits[0].GetProperty("jobs")[0];
Check(summary.GetProperty("tasksTotal").GetInt32() == 2 && summary.GetProperty("tasksCompleted").GetInt32() == 1, "Task counts include additional work");
Check(summary.GetProperty("partsWaiting").GetInt32() == 1, "Partially issued parts remain waiting");
Check(summary.GetProperty("additionalWork").GetArrayLength() == 1, "Additional work exposed beneath job");
using var byJob = JsonDocument.Parse(JsonSerializer.Serialize(await LinkedSearch.FindAsync(db, "JC-TEST-1"), jsonOptions));
Check(byJob.RootElement.GetProperty("results").GetArrayLength() == 1, "Job reference finds linked visit without duplicates");
service.Status = "Closed"; service.ClosedAt = DateTime.UtcNow; job.Status = "Completed";
await db.SaveChangesAsync();
task.Status = "In Progress"; await db.SaveChangesAsync();
Check(service.Status == "Closed" && job.Status == "Completed", "Task change cannot reopen a released visit");
Check(LifecycleRules.Resolve("Open", "Open", new[] { "Assigned" }, true, true) == ("Assigned", "Assigned"), "Assignment synchronization");
Check(await db.WorkLogEntries.AnyAsync(x => x.EntryType == "Task Update"), "Task transitions logged in timeline");
Console.WriteLine("Lifecycle and grouped-search checks passed (in-memory provider; not PostgreSQL integration tests).");

foreach(var kind in new[] { "PM", "Breakdown", "Maintenance" })
{
    var visit = new ServiceEvent { VehicleId = vehicle.Id, EventNumber = "SE-" + kind, EventType = kind, Status = "In Progress" };
    var card = new JobCard { ServiceEventId = visit.Id, JobCardNumber = "JC-" + kind };
    var work = new WorkItem { JobCardId = card.Id, TaskCode = "T-" + kind, Status = "Assigned",
        AssignedToTechnicianId = Guid.NewGuid(), AssignedTo = "Technician" };
    db.AddRange(visit, card, work); await db.SaveChangesAsync();
    Check(visit.Status == "Awaiting Assignment" && card.Status == visit.Status, kind + ": technician is not main assignee");
    visit.AssignedSupervisor = "Supervisor A"; visit.SupervisorAssignedAt = DateTime.UtcNow;
    await db.SaveChangesAsync();
    Check(visit.Status == "Assigned" && card.Status == "Assigned", kind + ": supervisor assigns main request");
    db.WorkLogEntries.Add(new WorkLogEntry { JobCardId = card.Id, CreatedBy = "Other person", Comment = "A note" });
    work.AssignedTo = "Another technician"; await db.SaveChangesAsync();
    Check(visit.AssignedSupervisor == "Supervisor A" && visit.Status == "Assigned", kind + ": note and allocation preserve assignment");
    work.Status = "In Progress"; await db.SaveChangesAsync();
    Check(visit.Status == "In Progress" && card.Status == "In Progress", kind + ": work starts linked lifecycle");
}

// Release gate regression cases apply equally to PM, Breakdown and Maintenance.
foreach(var eventType in new[]{"PM","Breakdown","Maintenance"})
{
    await using var releaseDb = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    var v = new Vehicle { RegistrationNumber="RELEASE-TEST" };
    var e = new ServiceEvent { VehicleId=v.Id, EventType=eventType, AssignedSupervisor="Supervisor" };
    var j = new JobCard { ServiceEventId=e.Id };
    var t = new WorkItem { JobCardId=j.Id, Status="Assigned", UpdatedAt=DateTime.UtcNow.AddMinutes(-10) };
    var i = new WorkTemplateInstance { JobCardId=j.Id, WorkItemId=t.Id };
    var f = new WorkTemplateFieldInstance { WorkTemplateInstanceId=i.Id, IsMandatory=true, Value="No", Result="Pass", ExecutedAt=DateTime.UtcNow.AddMinutes(-10) };
    releaseDb.AddRange(v,e,j,t,i,f);releaseDb.SaveChanges();
    var gate=await ReleaseReadiness.ReadAsync(releaseDb,j.Id);
    Check(!gate.CanQc && gate.Blockers.Any(b=>b.Kind=="Task"),eventType+": completed checks do not complete task");
    t.Status="Completed";releaseDb.SaveChanges();
    gate=await ReleaseReadiness.ReadAsync(releaseDb,j.Id);
    Check(gate.CanQc&&!gate.CanRelease,eventType+": completed work awaits QC");
    var qc=new QcInspection { JobCardId=j.Id, Result="Pass", InspectedAt=DateTime.UtcNow };
    releaseDb.Add(qc);releaseDb.SaveChanges();
    Check((await ReleaseReadiness.ReadAsync(releaseDb,j.Id)).CanRelease,eventType+": ready after passing QC; No is valid");
    var cancelled=new WorkItem { JobCardId=j.Id, Status="Cancelled" };releaseDb.Add(cancelled);releaseDb.SaveChanges();
    Check((await ReleaseReadiness.ReadAsync(releaseDb,j.Id)).CanRelease,eventType+": cancelled task does not block");
    f.Result="Fail";releaseDb.SaveChanges();
    Check(!(await ReleaseReadiness.ReadAsync(releaseDb,j.Id)).CanQc,eventType+": failed check blocks QC");
    f.Result="Pass";f.ExecutedAt=DateTime.UtcNow.AddMinutes(1);releaseDb.SaveChanges();
    Check((await ReleaseReadiness.ReadAsync(releaseDb,j.Id)).QcStatus=="Recheck required",eventType+": changed work invalidates old QC");
    qc.InspectedAt=DateTime.UtcNow.AddMinutes(2);qc.RoadTestRequired=true;releaseDb.SaveChanges();
    Check(!(await ReleaseReadiness.ReadAsync(releaseDb,j.Id)).CanRelease,eventType+": required road test blocks release");
    qc.RoadTestPassed=true;var part=new PartRequest { JobCardId=j.Id, Status="Issued" };releaseDb.Add(part);releaseDb.SaveChanges();
    Check(!(await ReleaseReadiness.ReadAsync(releaseDb,j.Id)).CanQc,eventType+": issued parts must be resolved");
    part.Status="Consumed";releaseDb.SaveChanges();
    Check((await ReleaseReadiness.ReadAsync(releaseDb,j.Id)).CanRelease,eventType+": consumed parts clear gate");
    var defect=new Defect { JobCardId=j.Id, Disposition="Open", Description="Unresolved issue" };releaseDb.Add(defect);releaseDb.SaveChanges();
    Check(!(await ReleaseReadiness.ReadAsync(releaseDb,j.Id)).CanRelease,eventType+": open issue blocks release");
    defect.Disposition="Closed";e.AssignedSupervisor="";releaseDb.SaveChanges();
    Check(!(await ReleaseReadiness.ReadAsync(releaseDb,j.Id)).CanQc,eventType+": supervisor assignment required");
    e.AssignedSupervisor="Supervisor";
    e.Status="Closed";releaseDb.SaveChanges();
    gate=await ReleaseReadiness.ReadAsync(releaseDb,j.Id);
    Check(gate.Released&&!gate.CanRelease&&await ReleaseReadiness.TaskIsClosedAsync(releaseDb,t.Id),eventType+": released service cannot release or execute again");
}
