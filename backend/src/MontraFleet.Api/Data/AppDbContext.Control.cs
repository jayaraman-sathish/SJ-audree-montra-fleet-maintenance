using Microsoft.EntityFrameworkCore;
using MontraFleet.Api.Models;
namespace MontraFleet.Api.Data;
public partial class AppDbContext
{
 public DbSet<WarrantyClaim> WarrantyClaims=>Set<WarrantyClaim>();
 private async Task MaintainControlStateAsync(CancellationToken ct)
 {
  var now=DateTime.UtcNow;
  foreach(var v in ChangeTracker.Entries<Vehicle>().Where(x=>x.State==EntityState.Modified).Select(x=>x.Entity).ToList()){
   await OffHireRecords.Where(x=>x.VehicleId==v.Id&&x.Status=="Approved").LoadAsync(ct);
   if(OffHireRecords.Local.Any(x=>x.VehicleId==v.Id&&x.Status=="Approved")){
    v.Status="Off-Hire";foreach(var row in ChangeTracker.Entries<VehicleAvailabilityLedger>().Where(x=>x.State==EntityState.Added&&x.Entity.VehicleId==v.Id))row.Entity.State="Off-Hire";
   }
  }
  foreach(var entry in ChangeTracker.Entries<ServiceEvent>().Where(x=>x.State==EntityState.Added||x.State==EntityState.Modified).ToList()){
   var e=entry.Entity;
   if(entry.State==EntityState.Added){var v=await Vehicles.FindAsync(new object[]{e.VehicleId},ct);e.OpenedOdometerKm??=v?.OdometerKm;}
   if(e.Status=="Closed"||e.Status=="Cancelled"){
    var clocks=await SlaClocks.Where(x=>x.ServiceEventId==e.Id&&x.Status!="Stopped").ToListAsync(ct);foreach(var s in clocks){ControlRules.Stop(s,e.ClosedAt??now);ControlEndpoints.Log(this,"STOP","SLA",s.Id,"Clock stopped with service closure.","System");}
   }
  }
  // Normalize newly-created ledger transitions in every intake path.
  foreach(var added in ChangeTracker.Entries<VehicleAvailabilityLedger>().Where(x=>x.State==EntityState.Added&&x.Entity.CorrectsLedgerId==null).Select(x=>x.Entity).OrderBy(x=>x.StartAt).ToList()){
   var previous=await VehicleAvailabilityLedger.Where(x=>x.VehicleId==added.VehicleId&&x.EndAt==null&&x.StartAt<=added.StartAt).ToListAsync(ct);
   foreach(var row in previous.Concat(VehicleAvailabilityLedger.Local.Where(x=>x.VehicleId==added.VehicleId&&x.EndAt==null&&x.StartAt<=added.StartAt)).Distinct().Where(x=>x.Id!=added.Id))row.EndAt=added.StartAt;
  }
 }
 public async Task ReconcileControlsAsync()
 {
  var rows=await VehicleAvailabilityLedger.OrderBy(x=>x.StartAt).ToListAsync();
  foreach(var group in rows.Where(x=>x.CorrectsLedgerId==null).GroupBy(x=>x.VehicleId)){
   var ordered=group.OrderBy(x=>x.StartAt).ToList();
   for(int i=0;i+1<ordered.Count;i++){var a=ordered[i];var next=ordered[i+1];if(a.EndAt==null||a.EndAt>next.StartAt){a.EndAt=next.StartAt;ControlEndpoints.Log(this,"RECONCILE","Availability",a.Id,"Closed overlapping period at next recorded state transition.","System");}}
  }
  var closed=await ServiceEvents.Where(x=>x.Status=="Closed"||x.Status=="Cancelled").ToListAsync();
  foreach(var e in closed){var clocks=await SlaClocks.Where(x=>x.ServiceEventId==e.Id&&x.Status!="Stopped").ToListAsync();foreach(var clock in clocks){ControlRules.Stop(clock,e.ClosedAt??DateTime.UtcNow);ControlEndpoints.Log(this,"STOP","SLA",clock.Id,"Reconciled clock for closed/cancelled service.","System");}}
  await SaveChangesAsync();
 }
}
