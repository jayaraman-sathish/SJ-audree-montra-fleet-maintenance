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

// Control workflows: time calculations and invariants shared across intake types.
var instant=new DateTime(2026,9,1,8,0,0,DateTimeKind.Utc);
var clock=new SlaClock{StartedAt=instant,DueAt=instant.AddHours(2)};
ControlRules.Pause(clock,instant.AddMinutes(30),"Waiting for authorization");
Check(!ControlRules.Breached(clock,instant.AddHours(5)),"Paused SLA does not consume remaining target");
ControlRules.Resume(clock,instant.AddHours(1));
Check(clock.DueAt==instant.AddHours(2.5)&&clock.TotalPausedMinutes==30,"Resume extends target by actual pause duration");
ControlRules.Stop(clock,instant.AddHours(2));
Check(!ControlRules.Breached(clock,instant.AddDays(10)),"Stopped clock does not become breached later");
var rejectedPause=false;try{ControlRules.Pause(clock,instant.AddHours(3),"Late pause");}catch(InvalidOperationException){rejectedPause=true;}
Check(rejectedPause,"Cannot pause a stopped clock");
var warranty=new WarrantyEntitlement{StartDate=instant.Date,EndDate=instant.Date.AddYears(1),OdometerLimitKm=50000};
Check(ControlRules.Eligibility(warranty,instant,null)=="Reading required","Mileage warranty does not invent historical odometer");
Check(ControlRules.Eligibility(warranty,instant,50000)=="Eligible"&&ControlRules.Eligibility(warranty,instant,50001)=="Mileage exceeded","Warranty mileage boundary");
Check(ControlRules.Eligibility(warranty,warranty.EndDate.AddDays(1),100)=="Expired","Warranty date boundary");
var periods=new[]{new VehicleAvailabilityLedger{StartAt=instant,State="Available"},new VehicleAvailabilityLedger{StartAt=instant.AddHours(2),EndAt=instant.AddHours(4),State="Under Maintenance"}};
var duration=ControlRules.ObservedTime(periods,instant,instant.AddHours(6));
Check(duration==(4d,2d),"Overlapping availability clips at transition; uncovered gaps stay unknown");
var correction=new VehicleAvailabilityLedger{StartAt=instant,EndAt=instant.AddHours(2),State="Off-Hire",CorrectsLedgerId=periods[0].Id};
Check(ControlRules.ObservedTime(periods.Append(correction),instant,instant.AddHours(6))==(4d,0d),"Availability correction supersedes original interval");
await using(var controlDb=new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options)){
 var v=new Vehicle{RegistrationNumber="CONTROL-TEST",OdometerKm=42000,Status="Under Maintenance"};
 var e=new ServiceEvent{VehicleId=v.Id,AssignedSupervisor="Supervisor"};var j=new JobCard{ServiceEventId=e.Id};
 var work=new WorkItem{JobCardId=j.Id,Status="Completed",UpdatedAt=instant};
 var qc=new QcInspection{JobCardId=j.Id,Result="Pass",InspectedAt=DateTime.UtcNow.AddMinutes(1)};
 var off=new OffHireRecord{VehicleId=v.Id,Status="Approved"};
 var first=new VehicleAvailabilityLedger{VehicleId=v.Id,State="Available",StartAt=instant};
 controlDb.AddRange(v,e,j,work,qc,off,first);await controlDb.SaveChangesAsync();
 Check(e.OpenedOdometerKm==42000,"New service captures intake odometer for eligibility");
 Check((await ReleaseReadiness.ReadAsync(controlDb,j.Id)).Blockers.Any(x=>x.Kind=="Off-Hire"),"Approved off-hire blocks vehicle release");
 v.Status="Available";var transition=new VehicleAvailabilityLedger{VehicleId=v.Id,StartAt=instant.AddHours(1),State="Available"};controlDb.Add(transition);await controlDb.SaveChangesAsync();
 Check(v.Status=="Off-Hire"&&transition.State=="Off-Hire","Other workflows cannot make an approved off-hire vehicle available");
 Check(first.EndAt==transition.StartAt,"New transition closes previous open availability period");
 off.Status="Closed";v.Status="Under Maintenance";await controlDb.SaveChangesAsync();
 Check(v.Status=="Under Maintenance","Closing off-hire allows active service state");
 var running=new SlaClock{ServiceEventId=e.Id,DueAt=DateTime.UtcNow.AddHours(2)};controlDb.Add(running);await controlDb.SaveChangesAsync();
 e.Status="Closed";e.ClosedAt=DateTime.UtcNow;await controlDb.SaveChangesAsync();
 Check(running.Status=="Stopped"&&running.StoppedAt.HasValue,"Closing service stops linked SLA");
 var audits=await controlDb.AuditEvents.CountAsync();await controlDb.ReconcileControlsAsync();await controlDb.ReconcileControlsAsync();
 Check(await controlDb.AuditEvents.CountAsync()==audits,"Control reconciliation is idempotent");
}
Console.WriteLine("Control rule checks passed (in-memory provider; PostgreSQL and browser checks remain separate).");
await using(var qualityDb=new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options)){
 var observed=DateTime.UtcNow.AddDays(-60);var v=new Vehicle{RegistrationNumber="FTF-TEST"};
 var original=new ServiceEvent{VehicleId=v.Id,EventType="Breakdown",OpenedAt=observed.AddDays(-1),ClosedAt=observed,Status="Closed"};
 var repeat=new ServiceEvent{VehicleId=v.Id,EventType="Maintenance",OpenedAt=observed.AddDays(2),Status="In Progress"};
 var a=new JobCard{ServiceEventId=original.Id};var b=new JobCard{ServiceEventId=repeat.Id};
 qualityDb.AddRange(v,original,repeat,a,b,new Defect{JobCardId=a.Id,FailureCode="AC-01"},new Defect{JobCardId=b.Id,FailureCode="ac-01"});qualityDb.SaveChanges();
 var report=await ControlEndpoints.QualityAsync(qualityDb,observed.AddDays(-2),DateTime.UtcNow,v.Id,null,"Breakdown");
 Check(report.RepeatFailures==1&&report.FirstTimeFix==0,"Repeat in another service type still counts against original cohort");
 var empty=await ControlEndpoints.QualityAsync(qualityDb,DateTime.UtcNow.AddDays(-1),DateTime.UtcNow,v.Id,null,null);
 Check(empty.FirstTimeFix==null,"No eligible releases reports unavailable, not 100 percent");
}
