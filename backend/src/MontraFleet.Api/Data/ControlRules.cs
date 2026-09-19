using MontraFleet.Api.Models;
namespace MontraFleet.Api.Data;
public static class ControlRules
{
 public static bool Active(string status)=>status!="Closed"&&status!="Cancelled"&&status!="Completed"&&status!="Rejected";
 public static DateTime? EffectiveDue(SlaClock s,DateTime now)=>s.DueAt?.Add(s.PausedAt.HasValue?now-s.PausedAt.Value:TimeSpan.Zero);
 public static bool Breached(SlaClock s,DateTime now)=>EffectiveDue(s,s.StoppedAt??now) is DateTime due&&(s.StoppedAt??now)>due;
 public static void Pause(SlaClock s,DateTime now,string reason){if(s.Status!="Running"||string.IsNullOrWhiteSpace(reason))throw new InvalidOperationException("Only a running clock can be paused; enter a reason.");s.Status="Paused";s.PausedAt=now;s.PauseReason=reason;}
 public static void Resume(SlaClock s,DateTime now){if(s.Status!="Paused"||s.PausedAt==null)throw new InvalidOperationException("Only a paused clock can resume.");var elapsed=now>s.PausedAt.Value?now-s.PausedAt.Value:TimeSpan.Zero;s.TotalPausedMinutes+=(int)elapsed.TotalMinutes;s.DueAt=s.DueAt?.Add(elapsed);s.PausedAt=null;s.Status="Running";}
 public static void Stop(SlaClock s,DateTime now){if(s.Status=="Stopped")return;if(s.Status=="Paused")Resume(s,now);s.StoppedAt=now;s.Status="Stopped";}
 public static string Eligibility(WarrantyEntitlement w,DateTime at,decimal? reading){if(w.Status!="Active")return "Inactive";if(at.Date<w.StartDate.Date)return "Not started";if(at.Date>w.EndDate.Date)return "Expired";if(w.OdometerLimitKm.HasValue&&!reading.HasValue)return "Reading required";if(w.OdometerLimitKm.HasValue&&reading>w.OdometerLimitKm)return "Mileage exceeded";return "Eligible";}
 // Later state transitions supersede overlapping legacy intervals; gaps stay unknown.
 public static (double observed,double available) ObservedTime(IEnumerable<VehicleAvailabilityLedger> source,DateTime from,DateTime to)
 {
  var all=source.ToList();var superseded=all.Where(x=>x.CorrectsLedgerId.HasValue).Select(x=>x.CorrectsLedgerId!.Value).ToHashSet();
  var rows=all.Where(x=>!superseded.Contains(x.Id)).OrderBy(x=>x.StartAt).ThenBy(x=>x.Id).ToList();double observed=0,available=0;
  for(int i=0;i<rows.Count;i++){var x=rows[i];var start=x.StartAt>from?x.StartAt:from;var end=x.EndAt??to;if(end>to)end=to;if(i+1<rows.Count&&rows[i+1].StartAt<end)end=rows[i+1].StartAt;if(end<=start)continue;var hours=(end-start).TotalHours;observed+=hours;if(x.State=="Available")available+=hours;}
  return(observed,available);
 }
}
