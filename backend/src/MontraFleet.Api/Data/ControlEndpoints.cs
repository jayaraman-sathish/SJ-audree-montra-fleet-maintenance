using Microsoft.EntityFrameworkCore;
using MontraFleet.Api.Models;
using System.Text.RegularExpressions;
namespace MontraFleet.Api.Data;
public static class ControlEndpoints
{
 public record ActionInput(string User,string Result,string Remarks);
 public record ClaimInput(Guid WarrantyEntitlementId,Guid JobCardId,Guid? PartRequestId,string Description,decimal Amount,string User);
 public record ClockInput(string ClockType,int TargetMinutes,string User);
 public record PauseInput(string Reason,string User);
 public record OffInput(Guid VehicleId,string ReasonCode,DateTime? ExpectedReturnAt,string User);
 public static void Log(AppDbContext db,string action,string type,Guid id,string detail,string user)=>db.AuditEvents.Add(new AuditEvent{Action=action,EntityType=type,EntityId=id,Details=detail,UserName=user,CorrelationId=Guid.NewGuid().ToString("N")});
 static IResult Invalid(string message)=>Results.BadRequest(new{message});
 static bool NoUser(string? s)=>string.IsNullOrWhiteSpace(s)||s.Length>120;
 public static async Task<int> BreachCountAsync(AppDbContext db){var clocks=await db.SlaClocks.AsNoTracking().Where(x=>x.Status!="Stopped").ToListAsync();return clocks.Count(x=>ControlRules.Breached(x,DateTime.UtcNow));}
 public static async Task<double?> UptimeAsync(AppDbContext db,DateTime from,DateTime to){var rows=await db.VehicleAvailabilityLedger.AsNoTracking().ToListAsync();var values=rows.GroupBy(x=>x.VehicleId).Select(g=>ControlRules.ObservedTime(g,from,to)).ToList();var hours=values.Sum(x=>x.observed);return hours==0?null:Math.Round(values.Sum(x=>x.available)*100/hours,1);}
 public static void MapControlEndpoints(this WebApplication app)
 {
  app.MapGet("/api/availability",async(AppDbContext db)=>Results.Ok(await(from a in db.VehicleAvailabilityLedger.AsNoTracking() join v in db.Vehicles on a.VehicleId equals v.Id orderby a.StartAt descending select new{a.Id,a.VehicleId,vehicle=v.RegistrationNumber,a.State,a.StartAt,a.EndAt,a.ReasonCode,a.SourceType,jobCardId=db.JobCards.Where(j=>j.ServiceEventId==a.SourceServiceEventId).Select(j=>(Guid?)j.Id).FirstOrDefault()}).ToListAsync()));
  app.MapGet("/api/availability/report",async(DateTime? from,DateTime? to,Guid? vehicleId,string? model,AppDbContext db)=>{
   var start=from??DateTime.UtcNow.AddDays(-30);var end=to??DateTime.UtcNow;if(end>DateTime.UtcNow)end=DateTime.UtcNow;if(end<=start)return Invalid("Choose a reporting period that has started.");
   var vehicles=await db.Vehicles.AsNoTracking().Where(v=>(vehicleId==null||v.Id==vehicleId)&&(model==null||model==""||v.Model==model)).ToListAsync();var ids=vehicles.Select(v=>v.Id).ToList();
   var ledger=await db.VehicleAvailabilityLedger.AsNoTracking().Where(x=>ids.Contains(x.VehicleId)).ToListAsync();
   var durations=vehicles.Select(v=>ControlRules.ObservedTime(ledger.Where(x=>x.VehicleId==v.Id),start,end)).ToList();var observed=durations.Sum(x=>x.observed);var available=durations.Sum(x=>x.available);var total=(end-start).TotalHours*vehicles.Count;
   var superseded=ledger.Where(x=>x.CorrectsLedgerId.HasValue).Select(x=>x.CorrectsLedgerId!.Value).ToHashSet();
   var rows=ledger.Where(x=>!superseded.Contains(x.Id)&&x.StartAt<end&&(x.EndAt==null||x.EndAt>start)).OrderByDescending(x=>x.StartAt).Select(x=>new{x.Id,x.VehicleId,vehicle=vehicles.First(v=>v.Id==x.VehicleId).RegistrationNumber,x.State,x.StartAt,x.EndAt,x.ReasonCode,x.SourceType,status=x.State});
   return Results.Ok(new{availabilityPct=observed==0?(double?)null:Math.Round(available*100/observed,1),observedHours=Math.Round(observed,1),unknownHours=Math.Round(Math.Max(0,total-observed),1),rows});
  });
  app.MapGet("/api/offhire",async(AppDbContext db)=>Results.Ok(await(from o in db.OffHireRecords.AsNoTracking() join v in db.Vehicles on o.VehicleId equals v.Id orderby o.StartAt descending select new{o.Id,o.VehicleId,vehicle=v.RegistrationNumber,o.StartAt,o.ExpectedReturnAt,o.EndAt,o.ReasonCode,o.RequestedBy,o.ApprovedBy,o.Status}).ToListAsync()));
  app.MapPost("/api/offhire",async(OffInput r,AppDbContext db)=>{
   if(NoUser(r.User)||string.IsNullOrWhiteSpace(r.ReasonCode))return Invalid("Enter operator name and off-hire reason.");
   if(r.ExpectedReturnAt<DateTime.UtcNow.Date)return Invalid("Expected return cannot be in the past.");
   await using var tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
   if(!await db.Vehicles.AnyAsync(x=>x.Id==r.VehicleId))return Invalid("Select a vehicle.");
   if(await db.OffHireRecords.AnyAsync(x=>x.VehicleId==r.VehicleId&&(x.Status=="Approved"||x.Status=="Pending Approval")))return Results.Conflict(new{message="An open off-hire request already exists."});
   var row=new OffHireRecord{VehicleId=r.VehicleId,ReasonCode=r.ReasonCode.Trim(),RequestedBy=r.User.Trim(),ExpectedReturnAt=r.ExpectedReturnAt};db.OffHireRecords.Add(row);Log(db,"REQUEST","OffHire",row.Id,row.ReasonCode,r.User);await db.SaveChangesAsync();await tx.CommitAsync();return Results.Ok(row);
  });
  app.MapPost("/api/offhire/{id:guid}/approve",async(Guid id,ActionInput r,AppDbContext db)=>{
   if(NoUser(r.User)||!(r.Result=="Approve"||r.Result=="Reject")||string.IsNullOrWhiteSpace(r.Remarks))return Invalid("Enter decision, operator and remarks.");
   await using var tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
   var o=await db.OffHireRecords.FindAsync(id);if(o==null)return Results.NotFound();if(o.Status!="Pending Approval")return Results.Conflict(new{message="Only pending requests can be decided."});
   o.Status=r.Result=="Approve"?"Approved":"Rejected";o.ApprovedBy=r.User;
   if(o.Status=="Approved"){var v=await db.Vehicles.FindAsync(o.VehicleId);if(v==null)return Results.NotFound();v.Status="Off-Hire";db.VehicleAvailabilityLedger.Add(new VehicleAvailabilityLedger{VehicleId=v.Id,State="Off-Hire",ReasonCode=o.ReasonCode,SourceType="OffHire",ChangedBy=r.User});}
   Log(db,r.Result.ToUpperInvariant(),"OffHire",o.Id,r.Remarks,r.User);await db.SaveChangesAsync();await tx.CommitAsync();return Results.Ok(o);
  });
  app.MapPost("/api/offhire/{id:guid}/recommission",async(Guid id,ActionInput r,AppDbContext db)=>{
   if(NoUser(r.User)||!(r.Result=="Pass"||r.Result=="Fail")||string.IsNullOrWhiteSpace(r.Remarks))return Invalid("Enter inspector, Pass/Fail and inspection remarks.");
   await using var tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
   var o=await db.OffHireRecords.FindAsync(id);if(o==null)return Results.NotFound();if(o.Status!="Approved")return Results.Conflict(new{message="Only approved off-hire records can be recommissioned."});
   var inspection=new RecommissioningInspection{OffHireRecordId=id,VehicleId=o.VehicleId,Inspector=r.User,Result=r.Result,Remarks=r.Remarks,InspectedAt=DateTime.UtcNow};db.RecommissioningInspections.Add(inspection);
   if(r.Result=="Pass"){
    var v=await db.Vehicles.FindAsync(o.VehicleId);if(v==null)return Results.NotFound();o.Status="Closed";o.EndAt=DateTime.UtcNow;
    var active=await db.ServiceEvents.AnyAsync(x=>x.VehicleId==v.Id&&x.Status!="Closed"&&x.Status!="Cancelled");
    var breakdown=await db.Breakdowns.AnyAsync(x=>x.VehicleId==v.Id&&x.Status=="Reported");
    v.Status=active?"Under Maintenance":breakdown?"Breakdown":"Available";
    db.VehicleAvailabilityLedger.Add(new VehicleAvailabilityLedger{VehicleId=v.Id,State=v.Status,SourceType="Recommission",ReasonCode=r.Remarks,ChangedBy=r.User});
   }
   Log(db,"RECOMMISSION","OffHire",id,r.Result+": "+r.Remarks,r.User);await db.SaveChangesAsync();await tx.CommitAsync();return Results.Ok(inspection);
  });
  app.MapGet("/api/sla",async(AppDbContext db)=>{
   var rows=await(from s in db.SlaClocks.AsNoTracking() join e in db.ServiceEvents on s.ServiceEventId equals e.Id join v in db.Vehicles on e.VehicleId equals v.Id select new{s,e.EventNumber,vehicle=v.RegistrationNumber,jobCardId=db.JobCards.Where(j=>j.ServiceEventId==e.Id).Select(j=>(Guid?)j.Id).FirstOrDefault()}).ToListAsync();var now=DateTime.UtcNow;
   return Results.Ok(rows.OrderByDescending(x=>x.s.StartedAt).Select(x=>new{x.s.Id,x.s.ServiceEventId,eventNo=x.EventNumber,x.vehicle,x.jobCardId,x.s.ClockType,x.s.StartedAt,dueAt=ControlRules.EffectiveDue(x.s,x.s.StoppedAt??now),x.s.StoppedAt,x.s.TotalPausedMinutes,x.s.PauseReason,x.s.Status,breached=ControlRules.Breached(x.s,now)}));
  });
  app.MapPost("/api/sla/{serviceEventId:guid}",async(Guid serviceEventId,ClockInput r,AppDbContext db)=>{
   if(NoUser(r.User)||r.TargetMinutes<=0||r.TargetMinutes>525600||!(r.ClockType=="Response"||r.ClockType=="Resolution"))return Invalid("Choose clock type, positive target minutes (up to one year) and operator.");
   await using var tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
   var e=await db.ServiceEvents.FindAsync(serviceEventId);if(e==null)return Results.NotFound();if(!ControlRules.Active(e.Status))return Results.Conflict(new{message="Service is closed or cancelled."});
   if(await db.SlaClocks.AnyAsync(x=>x.ServiceEventId==serviceEventId&&x.ClockType==r.ClockType&&x.Status!="Stopped"))return Results.Conflict(new{message="This clock is already active."});
   var s=new SlaClock{ServiceEventId=serviceEventId,ClockType=r.ClockType,DueAt=DateTime.UtcNow.AddMinutes(r.TargetMinutes)};db.SlaClocks.Add(s);Log(db,"START","SLA",s.Id,r.ClockType,r.User);await db.SaveChangesAsync();await tx.CommitAsync();return Results.Ok(s);
  });
  foreach(var action in new[]{"pause","resume","stop"}){
   var verb=action;app.MapPost("/api/sla/{id:guid}/"+verb,async(Guid id,PauseInput r,AppDbContext db)=>{
    if(NoUser(r.User))return Invalid("Enter operator name.");await using var tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
    var s=await db.SlaClocks.FindAsync(id);if(s==null)return Results.NotFound();
    try{if(verb=="pause")ControlRules.Pause(s,DateTime.UtcNow,r.Reason);else if(verb=="resume")ControlRules.Resume(s,DateTime.UtcNow);else ControlRules.Stop(s,DateTime.UtcNow);}catch(InvalidOperationException ex){return Results.Conflict(new{message=ex.Message});}
    Log(db,verb.ToUpperInvariant(),"SLA",s.Id,r.Reason??"",r.User);await db.SaveChangesAsync();await tx.CommitAsync();return Results.Ok(s);
   });
  }
  app.MapGet("/api/warranty",async(AppDbContext db)=>{
   var rows=await(from w in db.WarrantyEntitlements.AsNoTracking() join v in db.Vehicles on w.VehicleId equals v.Id select new{w,v.RegistrationNumber,v.OdometerKm}).ToListAsync();return Results.Ok(rows.Select(x=>new{x.w.Id,x.w.VehicleId,vehicle=x.RegistrationNumber,x.w.ReferenceNo,x.w.EntitlementType,x.w.StartDate,x.w.EndDate,x.w.OdometerLimitKm,x.w.CoverageNotes,status=ControlRules.Eligibility(x.w,DateTime.UtcNow,x.OdometerKm)}));
  });
  app.MapPost("/api/warranty",async(WarrantyEntitlement r,HttpRequest request,AppDbContext db)=>{
   var user=request.Headers["X-Operator"].ToString();if(NoUser(user))return Invalid("Enter operator name.");
   if(r.EndDate<r.StartDate||r.StartDate==default||string.IsNullOrWhiteSpace(r.ReferenceNo)||r.OdometerLimitKm<0||!await db.Vehicles.AnyAsync(x=>x.Id==r.VehicleId))return Invalid("Enter a valid vehicle, reference, dates and mileage limit.");
   r.Id=Guid.NewGuid();r.Status="Active";db.WarrantyEntitlements.Add(r);Log(db,"CREATE","Warranty",r.Id,r.ReferenceNo,user);await db.SaveChangesAsync();return Results.Ok(r);
  });
  app.MapGet("/api/warranty/claims",async(AppDbContext db)=>Results.Ok(await(from c in db.WarrantyClaims.AsNoTracking() join w in db.WarrantyEntitlements on c.WarrantyEntitlementId equals w.Id join j in db.JobCards on c.JobCardId equals j.Id join v in db.Vehicles on w.VehicleId equals v.Id orderby c.CreatedAt descending select new{c.Id,c.ClaimNumber,c.JobCardId,jobCard=j.JobCardNumber,vehicle=v.RegistrationNumber,c.Description,c.Amount,c.Status,c.CreatedAt,c.DecisionBy,c.DecisionRemarks,c.PartRequestId,w.ReferenceNo}).ToListAsync()));
  app.MapPost("/api/warranty/claims",async(ClaimInput r,AppDbContext db)=>{
   if(NoUser(r.User)||string.IsNullOrWhiteSpace(r.Description)||r.Amount<0)return Invalid("Enter operator, description and non-negative amount.");
   var w=await db.WarrantyEntitlements.FindAsync(r.WarrantyEntitlementId);var j=await db.JobCards.FindAsync(r.JobCardId);if(w==null||j==null)return Invalid("Select entitlement and Job Card.");var e=await db.ServiceEvents.FindAsync(j.ServiceEventId);if(e==null||e.VehicleId!=w.VehicleId)return Invalid("Entitlement and Job Card must belong to the same vehicle.");
   if(r.PartRequestId.HasValue&&!await db.PartRequests.AnyAsync(p=>p.Id==r.PartRequestId&&p.JobCardId==j.Id))return Invalid("Select a part request from this Job Card.");
   var c=new WarrantyClaim{WarrantyEntitlementId=w.Id,JobCardId=j.Id,PartRequestId=r.PartRequestId,ClaimNumber="CLM-"+Guid.NewGuid().ToString("N")[..12].ToUpperInvariant(),Description=r.Description,Amount=r.Amount,CreatedBy=r.User};db.WarrantyClaims.Add(c);Log(db,"CREATE","WarrantyClaim",c.Id,c.ClaimNumber,r.User);await db.SaveChangesAsync();return Results.Ok(c);
  });
  app.MapPost("/api/warranty/claims/{id:guid}/decision",async(Guid id,ActionInput r,AppDbContext db)=>{
   if(NoUser(r.User)||string.IsNullOrWhiteSpace(r.Remarks))return Invalid("Enter operator and remarks.");await using var tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
   var c=await db.WarrantyClaims.FindAsync(id);if(c==null)return Results.NotFound();var valid=(c.Status=="Draft"&&r.Result=="Submitted")||(c.Status=="Submitted"&&(r.Result=="Approved"||r.Result=="Rejected"))||(c.Status=="Approved"&&r.Result=="Paid");if(!valid)return Results.Conflict(new{message="Invalid claim transition."});
   if(r.Result=="Submitted"||r.Result=="Approved"){var w=await db.WarrantyEntitlements.FindAsync(c.WarrantyEntitlementId);var j=await db.JobCards.FindAsync(c.JobCardId);var e=j==null?null:await db.ServiceEvents.FindAsync(j.ServiceEventId);if(w==null||e==null)return Invalid("Linked records are missing.");var eligible=ControlRules.Eligibility(w,e.OpenedAt,e.OpenedOdometerKm);if(eligible!="Eligible")return Results.Conflict(new{message="Warranty at service intake: "+eligible});}
   c.Status=r.Result;c.DecisionBy=r.User;c.DecisionRemarks=r.Remarks;c.DecidedAt=DateTime.UtcNow;Log(db,r.Result.ToUpperInvariant(),"WarrantyClaim",id,r.Remarks,r.User);await db.SaveChangesAsync();await tx.CommitAsync();return Results.Ok(c);
  });
  app.MapGet("/api/documents/{id:guid}/file",async(Guid id,bool? download,AppDbContext db,HttpResponse response)=>{
   var d=await db.VehicleDocuments.FindAsync(id);if(d==null||d.Content.Length==0)return Results.NotFound();response.Headers["X-Content-Type-Options"]="nosniff";response.Headers["Content-Security-Policy"]="sandbox";return Results.File(d.Content,d.ContentType,download==true?d.FileName:null);
  });
  app.MapGet("/api/control/jobs",async(AppDbContext db)=>{
   var rows=await(from j in db.JobCards.AsNoTracking() join e in db.ServiceEvents on j.ServiceEventId equals e.Id join v in db.Vehicles on e.VehicleId equals v.Id orderby e.OpenedAt descending select new{j,e,v}).ToListAsync();var results=new List<object>();
   foreach(var row in rows){var ready=await ReleaseReadiness.ReadAsync(db,row.j.Id);results.Add(new{row.j.Id,jobCard=row.j.JobCardNumber,row.e.EventNumber,row.e.VehicleId,vehicle=row.v.RegistrationNumber,row.e.AssignedSupervisor,status=row.e.Status,row.e.OpenedAt,row.e.ClosedAt,ready.QcStatus,ready.CanRelease,blockers=ready.Blockers.Select(b=>b.Message)});}return Results.Ok(results);
  });
  app.MapGet("/api/control/audit",async(string? q,string? action,string? user,DateTime? from,DateTime? to,int? page,AppDbContext db)=>{
   if(from.HasValue&&to.HasValue&&to<=from)return Invalid("End must follow start.");
   var query=db.AuditEvents.AsNoTracking().AsQueryable();if(!string.IsNullOrWhiteSpace(q))query=query.Where(x=>x.Details.Contains(q)||x.EntityType.Contains(q));if(!string.IsNullOrWhiteSpace(action))query=query.Where(x=>x.Action==action);if(!string.IsNullOrWhiteSpace(user))query=query.Where(x=>x.UserName.Contains(user));if(from.HasValue)query=query.Where(x=>x.OccurredAt>=from);if(to.HasValue)query=query.Where(x=>x.OccurredAt<to);
   var total=await query.CountAsync();var current=Math.Max(1,page??1);var rows=await query.OrderByDescending(x=>x.OccurredAt).ThenBy(x=>x.Id).Skip((current-1)*50).Take(50).ToListAsync();var output=new List<object>();
   foreach(var x in rows){Guid? job=null;string? path=null;if(x.EntityId is Guid id){
    if(x.EntityType=="JobCard"||x.EntityType=="WorkOrder")job=id;
    else if(x.EntityType=="ServiceEvent")job=await db.JobCards.Where(j=>j.ServiceEventId==id).Select(j=>(Guid?)j.Id).FirstOrDefaultAsync();
    else if(x.EntityType=="Task"||x.EntityType=="WorkItem")job=await db.WorkItems.Where(t=>t.Id==id).Select(t=>(Guid?)t.JobCardId).FirstOrDefaultAsync();
    else if(x.EntityType=="Breakdown")job=await(from e in db.ServiceEvents join j in db.JobCards on e.Id equals j.ServiceEventId where e.BreakdownId==id select (Guid?)j.Id).FirstOrDefaultAsync();
    else if(x.EntityType=="SLA")job=await(from clock in db.SlaClocks join j in db.JobCards on clock.ServiceEventId equals j.ServiceEventId where clock.Id==id select (Guid?)j.Id).FirstOrDefaultAsync();
    else if(x.EntityType=="MaintenanceRequest")job=await db.MaintenanceRequests.Where(t=>t.Id==id).Select(t=>t.JobCardId).FirstOrDefaultAsync();
    else if(x.EntityType=="Issue"||x.EntityType=="Defect")job=await db.Defects.Where(t=>t.Id==id).Select(t=>(Guid?)t.JobCardId).FirstOrDefaultAsync();
    else if(x.EntityType=="PartRequest")job=await db.PartRequests.Where(t=>t.Id==id).Select(t=>(Guid?)t.JobCardId).FirstOrDefaultAsync();
    else if(x.EntityType=="WarrantyClaim")job=await db.WarrantyClaims.Where(c=>c.Id==id).Select(c=>(Guid?)c.JobCardId).FirstOrDefaultAsync();
    else if(x.EntityType=="WorkEvidence")job=await db.WorkEvidence.Where(d=>d.Id==id).Select(d=>(Guid?)d.JobCardId).FirstOrDefaultAsync();
    else if(x.EntityType=="Document")job=await db.VehicleDocuments.Where(d=>d.Id==id).Select(d=>d.JobCardId).FirstOrDefaultAsync();
    path=x.EntityType switch{"SLA"=>"/sla","OffHire"=>"/offhire","Warranty"=>"/warranty","WarrantyClaim"=>"/warranty","Document"=>"/documents",_=>null};
   }output.Add(new{x.Id,x.OccurredAt,x.UserName,x.Action,x.EntityType,x.EntityId,x.Details,jobCardId=job,path});}return Results.Ok(new{total,page=current,pageSize=50,rows=output});
  });
  app.MapGet("/api/quality",async(DateTime? from,DateTime? to,Guid? vehicleId,string? model,string? serviceType,AppDbContext db)=>{if(from.HasValue&&to.HasValue&&to<=from)return Invalid("End must follow start.");return Results.Ok(await QualityAsync(db,from??DateTime.UtcNow.AddDays(-90),to??DateTime.UtcNow,vehicleId,model,serviceType));});
  app.MapGet("/api/analytics/maintenance",async(DateTime? from,DateTime? to,Guid? vehicleId,string? model,string? serviceType,AppDbContext db)=>{
   var start=from??DateTime.UtcNow.AddDays(-30);var end=to??DateTime.UtcNow;if(end<=start)return Invalid("End must follow start.");
   var vehicles=await db.Vehicles.AsNoTracking().Where(v=>(vehicleId==null||v.Id==vehicleId)&&(model==null||model==""||v.Model==model)).ToListAsync();var vehicleIds=vehicles.Select(v=>v.Id).ToList();
   var jobs=await(from j in db.JobCards.AsNoTracking() join e in db.ServiceEvents on j.ServiceEventId equals e.Id where vehicleIds.Contains(e.VehicleId)&&(serviceType==null||serviceType==""||e.EventType==serviceType) select new{j.Id,j.Status,e.VehicleId,e.OpenedAt,e.ClosedAt,e.EventType}).ToListAsync();var ids=jobs.Select(j=>j.Id).ToList();
   var parts=await db.PartTransactions.AsNoTracking().Where(x=>ids.Contains(x.JobCardId)&&x.TransactionAt>=start&&x.TransactionAt<end&&(x.TransactionType=="Issue"||x.TransactionType=="Return")).ToListAsync();
   var labour=await db.LabourEntries.AsNoTracking().Where(x=>ids.Contains(x.JobCardId)&&x.StartAt>=start&&x.StartAt<end).ToListAsync();var costs=await db.WorkOrderCosts.AsNoTracking().Where(x=>ids.Contains(x.JobCardId)&&x.PostedAt>=start&&x.PostedAt<end).ToListAsync();
   var pm=await db.PmObligations.AsNoTracking().Where(x=>vehicleIds.Contains(x.VehicleId)&&x.DueDate>=start&&x.DueDate<end).ToListAsync();var ontime=pm.Count(x=>x.CompletedAt.HasValue&&x.DueDate.HasValue&&x.CompletedAt.Value.Date<=x.DueDate.Value.Date);var closed=jobs.Where(j=>j.ClosedAt>=start&&j.ClosedAt<end).ToList();
   var ledger=await db.VehicleAvailabilityLedger.AsNoTracking().Where(x=>vehicleIds.Contains(x.VehicleId)).ToListAsync();var duration=ledger.GroupBy(x=>x.VehicleId).Select(g=>ControlRules.ObservedTime(g,start,end<DateTime.UtcNow?end:DateTime.UtcNow)).ToList();var observed=duration.Sum(x=>x.observed);
   var partCost=parts.Sum(x=>x.ExtendedCost);var labourCost=labour.Sum(x=>x.CostAmount);var extra=costs.Where(x=>x.CostType=="External").Sum(x=>x.Amount);var other=costs.Where(x=>x.CostType!="External").Sum(x=>x.Amount);
   var detail=jobs.Where(j=>parts.Any(p=>p.JobCardId==j.Id)||labour.Any(l=>l.JobCardId==j.Id)||costs.Any(c=>c.JobCardId==j.Id)||j.OpenedAt>=start&&j.OpenedAt<end||j.ClosedAt>=start&&j.ClosedAt<end).Select(j=>new{jobCardId=j.Id,vehicle=vehicles.First(v=>v.Id==j.VehicleId).RegistrationNumber,j.EventType,j.Status,j.OpenedAt,j.ClosedAt,cost=parts.Where(x=>x.JobCardId==j.Id).Sum(x=>x.ExtendedCost)+labour.Where(x=>x.JobCardId==j.Id).Sum(x=>x.CostAmount)+costs.Where(x=>x.JobCardId==j.Id).Sum(x=>x.Amount)}).ToList();
   return Results.Ok(new{availabilityPct=observed==0?(double?)null:Math.Round(duration.Sum(x=>x.available)*100/observed,1),observedHours=Math.Round(observed,1),pmCompliancePct=pm.Count==0?(double?)null:Math.Round(ontime*100.0/pm.Count,1),pmDue=pm.Count,pmOnTime=ontime,mttrHours=closed.Count==0?(double?)null:Math.Round(closed.Average(j=>(j.ClosedAt!.Value-j.OpenedAt).TotalHours),1),partsCost=partCost,labourCost,externalCost=extra,otherCost=other,totalMaintenanceCost=partCost+labourCost+extra+other,costPerKm=(decimal?)null,openWorkOrders=jobs.Count(j=>ControlRules.Active(j.Status)),details=detail,topVehicles=detail.GroupBy(x=>x.vehicle).Select(g=>new{vehicle=g.Key,cost=g.Sum(x=>x.cost)}).OrderByDescending(x=>x.cost).Take(10),topParts=parts.GroupBy(p=>p.PartNumber).Select(g=>new{partNumber=g.Key,quantity=g.Sum(p=>p.TransactionType=="Return"?-p.Quantity:p.Quantity),cost=g.Sum(p=>p.ExtendedCost)}).OrderByDescending(x=>x.cost).Take(10),technicianProductivity=labour.GroupBy(l=>l.Technician).Select(g=>new{technician=g.Key,hours=g.Sum(l=>l.Hours),cost=g.Sum(l=>l.CostAmount)})});
  });
 }
 public record QualityReport(double? FirstTimeFix,int RepeatFailures,int Eligible,int Observing,int Unknown,int OpenRca,double? QcPass,List<object> Rows);
 public static async Task<QualityReport> QualityAsync(AppDbContext db,DateTime from,DateTime to,Guid? vehicleId,string? model,string? serviceType)
 {
  var visits=await(from e in db.ServiceEvents.AsNoTracking() join j in db.JobCards on e.Id equals j.ServiceEventId join v in db.Vehicles on e.VehicleId equals v.Id where e.Status!="Cancelled"&&(vehicleId==null||v.Id==vehicleId)&&(model==null||model==""||v.Model==model) select new{e,j.Id,vehicle=v.RegistrationNumber}).ToListAsync();
  var defects=await db.Defects.AsNoTracking().ToListAsync();var complaints=await db.Breakdowns.AsNoTracking().ToDictionaryAsync(b=>b.Id,b=>b.Complaint);
  string Key(string text)=>Regex.Replace(text.Trim().ToLowerInvariant(),@"\s+"," ");
  HashSet<string> Keys(Guid job,Guid? breakdown){var codes=defects.Where(d=>d.JobCardId==job&&!string.IsNullOrWhiteSpace(d.FailureCode)).Select(d=>"code:"+Key(d.FailureCode)).ToHashSet();if(breakdown.HasValue&&complaints.TryGetValue(breakdown.Value,out var complaint)&&!string.IsNullOrWhiteSpace(complaint))codes.Add("complaint:"+Key(complaint));return codes;}
  var observedUntil=to<DateTime.UtcNow?to:DateTime.UtcNow;
  var rows=new List<object>();int eligible=0,passed=0,repeats=0,pending=0,unknown=0;
  foreach(var visit in visits.Where(x=>x.e.ClosedAt>=from&&x.e.ClosedAt<to&&x.e.EventType!="PM"&&(string.IsNullOrEmpty(serviceType)||x.e.EventType==serviceType))){
   var keys=Keys(visit.Id,visit.e.BreakdownId);var closed=visit.e.ClosedAt!.Value;var window=closed.AddDays(30);var repeat=visits.OrderBy(x=>x.e.OpenedAt).FirstOrDefault(x=>x.e.VehicleId==visit.e.VehicleId&&x.e.Id!=visit.e.Id&&x.e.OpenedAt>closed&&x.e.OpenedAt<=window&&x.e.OpenedAt<=observedUntil&&Keys(x.Id,x.e.BreakdownId).Overlaps(keys));
   string status;if(keys.Count==0){unknown++;status="Insufficient failure detail";}else if(repeat!=null){eligible++;repeats++;status="Repeat detected";}else if(window>observedUntil){pending++;status="Observing 30-day window";}else{eligible++;passed++;status="First-time fix";}
   rows.Add(new{jobCardId=visit.Id,vehicle=visit.vehicle,eventNo=visit.e.EventNumber,closedAt=closed,windowEnds=window,status,repeatJobCardId=repeat?.Id});
  }
  var jobIds=visits.Where(x=>string.IsNullOrEmpty(serviceType)||x.e.EventType==serviceType).Select(x=>x.Id).ToList();var inspections=await db.QcInspections.AsNoTracking().Where(q=>jobIds.Contains(q.JobCardId)&&q.InspectedAt>=from&&q.InspectedAt<to).ToListAsync();var qc=inspections.GroupBy(q=>q.JobCardId).Select(g=>g.OrderByDescending(q=>q.InspectedAt).First()).ToList();
  return new QualityReport(eligible==0?(double?)null:Math.Round(passed*100.0/eligible,1),repeats,eligible,pending,unknown,defects.Count(d=>jobIds.Contains(d.JobCardId)&&d.Disposition!="Closed"&&string.IsNullOrWhiteSpace(d.RcaSummary)),qc.Count==0?(double?)null:Math.Round(qc.Count(q=>q.Result=="Pass")*100.0/qc.Count,1),rows);
 }
}
