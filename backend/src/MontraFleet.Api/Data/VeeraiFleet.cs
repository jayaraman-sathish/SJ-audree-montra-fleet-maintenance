using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
namespace MontraFleet.Api.Data;
public record PendingServiceRow(Guid VehicleId,string Vehicle,string Reference,string Status,string Url);
public static class VeeraiFleet
{
 public static bool IsPendingList(string text){
  var q=text.ToLowerInvariant().Replace("vechile","vehicle").Replace("vehical","vehicle").Replace("pensing","pending");
  return Regex.IsMatch(q,@"\b(which|what|list|show|how many)\b")&&Regex.IsMatch(q,@"\b(pending|awaiting|open|overdue|due)\b")&&Regex.IsMatch(q,@"\b(service|maintenance|pm)\b")&&Regex.IsMatch(q,@"\b(vehicles?|fleet|all|jobs?)\b")
   &&!Regex.IsMatch(q,@"\b(?:jc|wo|se|bd)[\s-]*\d|\b[a-z]{2}[\s-]*\d");
 }
 public static async Task<List<PendingServiceRow>> Pending(AppDbContext db,CancellationToken ct){
  var rows=await(from e in db.ServiceEvents.AsNoTracking() join v in db.Vehicles.AsNoTracking() on e.VehicleId equals v.Id
   where e.ClosedAt==null&&e.Status!="Closed"&&e.Status!="Cancelled"&&e.Status!="Released"
   orderby e.OpenedAt select new PendingServiceRow(v.Id,v.RegistrationNumber,e.EventNumber,e.EventType+" · "+e.Status,"/service")).ToListAsync(ct);
  rows.AddRange(await(from b in db.Breakdowns.AsNoTracking() join v in db.Vehicles.AsNoTracking() on b.VehicleId equals v.Id
   where b.RestoredAt==null&&b.Status!="Closed"&&b.Status!="Cancelled"&&b.Status!="Restored"&&!db.ServiceEvents.Any(e=>e.BreakdownId==b.Id)
   select new PendingServiceRow(v.Id,v.RegistrationNumber,b.BreakdownNumber,"Breakdown · "+b.Status,"/breakdown")).ToListAsync(ct));
  rows.AddRange(await(from m in db.MaintenanceRequests.AsNoTracking() join v in db.Vehicles.AsNoTracking() on m.VehicleId equals v.Id
   where m.JobCardId==null&&m.Status!="Closed"&&m.Status!="Cancelled"&&m.Status!="Completed"&&m.Status!="Rejected"
   select new PendingServiceRow(v.Id,v.RegistrationNumber,m.RequestNumber,"Maintenance request · "+m.Status,"/maintenance-requests")).ToListAsync(ct));
  var today=DateTime.UtcNow.Date;
  rows.AddRange(await(from p in db.PmObligations.AsNoTracking() join v in db.Vehicles.AsNoTracking() on p.VehicleId equals v.Id
   where p.CompletedAt==null&&p.SupersededById==null&&p.Status!="Completed"&&p.Status!="Cancelled"&&p.Status!="Superseded"
    &&((p.DueDate!=null&&p.DueDate<today.AddDays(1))||(p.DueReading!=null&&p.DueReading<=v.OdometerKm)||(p.DueOperatingHours!=null&&p.DueOperatingHours<=v.OperatingHours)||(p.DueEnergyKwh!=null&&p.DueEnergyKwh<=v.EnergyKwh))
    &&!db.ServiceEvents.Any(e=>e.PmObligationId==p.Id&&e.ClosedAt==null&&e.Status!="Cancelled"&&e.Status!="Closed"&&e.Status!="Released")
   select new PendingServiceRow(v.Id,v.RegistrationNumber,p.PlanCode,"PM due / overdue · not in an open service","/pm-obligations")).ToListAsync(ct));
  return rows.OrderBy(x=>x.Vehicle).ThenBy(x=>x.Reference).ToList();
 }
 public static object Reply(List<PendingServiceRow> rows){
  var sources=rows.Take(50).Select((x,i)=>new VeerSource("F"+(i+1),x.Vehicle+" · "+x.Reference,x.Url,x.Status)).ToArray();
  var count=rows.Select(x=>x.VehicleId).Distinct().Count();
  var reply=rows.Count==0?"No pending service records found. I checked open service visits, unlinked breakdowns and maintenance requests, and PM due against recorded dates and meter readings.":
   $"{count} vehicles have pending service across {rows.Count} records. This includes work already in progress and PM due against recorded readings.\n\n"+string.Join("\n",rows.Take(50).Select(x=>$"{x.Vehicle} — {x.Reference} — {x.Status}"))+(rows.Count>50?"\n\nShowing the first 50 records. Open the service and PM screens for the full lists.":"");
  return new{reply,jobId=(Guid?)null,context="Current fleet service records",choices=Array.Empty<VeerChatChoice>(),sources};
 }
}
