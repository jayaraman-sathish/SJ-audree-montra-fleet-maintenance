using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Text;
namespace MontraFleet.Api.Data;
public class ServiceReportSnapshot { public Guid Id{get;set;}=Guid.NewGuid();public Guid JobCardId{get;set;}public string Html{get;set;}="";public DateTime CreatedAt{get;set;}=DateTime.UtcNow; }
public partial class AppDbContext { public DbSet<ServiceReportSnapshot> ServiceReportSnapshots=>Set<ServiceReportSnapshot>(); }
public static class ServiceReports
{
 public static void MapServiceReportEndpoints(this WebApplication app){app.MapGet("/api/job-cards/{id:guid}/report",async(Guid id,AppDbContext db,HttpResponse response)=>{
  if(!await db.JobCards.AnyAsync(x=>x.Id==id))return Results.NotFound();
  var saved=await db.ServiceReportSnapshots.AsNoTracking().FirstOrDefaultAsync(x=>x.JobCardId==id);
  response.Headers["Cache-Control"]="no-store";response.Headers["X-Content-Type-Options"]="nosniff";
  return Results.Content(saved?.Html??await RenderAsync(db,id,false),"text/html; charset=utf-8");
 });}
 public static async Task CaptureAsync(AppDbContext db,Guid id){if(await db.ServiceReportSnapshots.AnyAsync(x=>x.JobCardId==id))return;db.ServiceReportSnapshots.Add(new ServiceReportSnapshot{JobCardId=id,Html=await RenderAsync(db,id,true)});await db.SaveChangesAsync();}
 static string H(object? value)=>WebUtility.HtmlEncode(value is DateTime time?time.ToUniversalTime().ToString("dd MMM yyyy HH:mm 'UTC'"):Convert.ToString(value,System.Globalization.CultureInfo.InvariantCulture)??"—");
 public static async Task<string> RenderAsync(AppDbContext db,Guid id,bool final)
 {
  var j=await db.JobCards.AsNoTracking().SingleAsync(x=>x.Id==id);var e=await db.ServiceEvents.AsNoTracking().SingleAsync(x=>x.Id==j.ServiceEventId);var v=await db.Vehicles.AsNoTracking().SingleAsync(x=>x.Id==e.VehicleId);
  var b=e.BreakdownId.HasValue?await db.Breakdowns.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==e.BreakdownId):null;
  var tasks=await db.WorkItems.AsNoTracking().Where(x=>x.JobCardId==id).OrderBy(x=>x.TaskCode).ToListAsync();var requests=await db.PartRequests.AsNoTracking().Where(x=>x.JobCardId==id).ToListAsync();var parts=await db.PartMasters.AsNoTracking().Select(x=>new{x.Id,x.PartNumber,x.Description,x.ManufacturerPartNumber}).ToDictionaryAsync(x=>x.Id);
  var instances=await db.WorkTemplateInstances.Where(x=>x.JobCardId==id).Select(x=>x.Id).ToListAsync();var fields=await db.WorkTemplateFieldInstances.AsNoTracking().Where(x=>instances.Contains(x.WorkTemplateInstanceId)).OrderBy(x=>x.SectionName).ThenBy(x=>x.Sequence).ToListAsync();
  var qc=await db.QcInspections.AsNoTracking().Where(x=>x.JobCardId==id).OrderByDescending(x=>x.InspectedAt).FirstOrDefaultAsync();var release=await db.VehicleReleases.AsNoTracking().Where(x=>x.ServiceEventId==e.Id).OrderByDescending(x=>x.ReleasedAt).FirstOrDefaultAsync();var labour=await db.LabourEntries.AsNoTracking().Where(x=>x.JobCardId==id).ToListAsync();var cost=await db.WorkOrderCosts.AsNoTracking().Where(x=>x.JobCardId==id).ToListAsync();var movements=await db.PartTransactions.AsNoTracking().Where(x=>x.JobCardId==id).ToListAsync();var complaints=await db.MaintenanceRequests.AsNoTracking().Where(x=>x.JobCardId==id).Select(x=>x.Description).ToListAsync();
  var output=new StringBuilder("<!doctype html><html><head><meta charset='utf-8'><meta name='viewport' content='width=device-width'><title>Service Work Report</title><style>body{font:14px Arial,sans-serif;color:#162a45;max-width:1100px;margin:30px auto;padding:20px}h1{font-size:25px}h2{font-size:18px;margin-top:26px}table{width:100%;border-collapse:collapse;margin:12px 0}th,td{padding:9px;border:1px solid #ccd5df;text-align:left;vertical-align:top;overflow-wrap:anywhere}th{background:#edf2f8}small{color:#52647b}.banner{padding:12px;background:#edf2f8}.brand{display:flex;align-items:center;gap:16px}.brand img{width:55px}button{padding:10px 18px;cursor:pointer}@media print{button{display:none}body{margin:0;max-width:none;padding:0}tr{break-inside:avoid}thead{display:table-header-group}@page{size:A4;margin:12mm}}</style></head><body><div class='brand'><img src='/assets/audree-logo.png' alt='Audree'><div><b>AUDREE · Montra Fleet Maintenance</b><h1>Service Work Report</h1></div></div><button onclick='window.print()'>Print / Save as PDF</button>");
  output.Append("<p class='banner'>").Append(H(final?"Final report — captured at vehicle release":e.Status=="Closed"?"Historical report — reconstructed from current records; no release-time snapshot exists":"DRAFT — service has not been released")).Append("</p>");
  void Table(string[] heads,IEnumerable<object?[]> rows){output.Append("<table><thead><tr>");foreach(var head in heads)output.Append("<th>").Append(H(head)).Append("</th>");output.Append("</tr></thead><tbody>");foreach(var row in rows){output.Append("<tr>");foreach(var cell in row)output.Append("<td>").Append(H(cell)).Append("</td>");output.Append("</tr>");}output.Append("</tbody></table>");}
  Table(new[]{"Job Card / Service","Vehicle / VIN","Type / Model","Supervisor / Bay"},new[]{new object?[]{j.JobCardNumber+" / "+e.EventNumber,v.RegistrationNumber+" / "+v.Vin,e.EventType+" / "+v.Model,e.AssignedSupervisor+" / "+j.Bay}});
  Table(new[]{"Opened on","Released on","Intake odometer (km)","Status"},new[]{new object?[]{e.OpenedAt,e.ClosedAt,e.OpenedOdometerKm,e.Status}});
  output.Append("<p>Service centre (vehicle record): ").Append(H(v.ServiceCentreCode)).Append(" · Customer: ").Append(H(v.CustomerCode)).Append("</p>");
  output.Append("<h2>Reported work</h2><p>").Append(H(b?.Complaint??(complaints.Count>0?string.Join("; ",complaints):e.EventType=="PM"?"Scheduled preventive maintenance":"See work tasks below"))).Append("</p><h2>Work performed</h2>");
  Table(new[]{"Task","Description","Technician","Status","Completion remarks"},tasks.Select(t=>new object?[]{t.TaskCode,t.Description,t.AssignedTo,t.Status,t.CompletionRemarks}));
  output.Append("<h2>Inspection and diagnosis</h2>");Table(new[]{"Check","Result / Reading","Outcome","Remarks","Recorded by"},fields.Select(f=>new object?[]{f.FieldCode+" · "+f.Label,f.Value,f.Result,f.Remarks,f.ExecutedBy}));
  var legacy=await db.ChecklistExecutions.AsNoTracking().Where(x=>x.JobCardId==id).ToListAsync();if(legacy.Count>0)Table(new[]{"Check","Result","Remarks"},legacy.Select(f=>new object?[]{f.ItemCode+" · "+f.ItemText,f.Result,f.Remarks}));
  output.Append("<h2>Parts supplied and used</h2>");Table(new[]{"Part number / OEM number","Description","Requested","Issued","Returned","Consumed","Unsettled at technician"},requests.Select(r=>new object?[]{parts.TryGetValue(r.PartMasterId,out var p)?p.PartNumber+" / "+p.ManufacturerPartNumber:r.PartMasterId.ToString(),parts.TryGetValue(r.PartMasterId,out var part)?part.Description:"",r.QuantityRequired,r.QuantityIssued,r.QuantityReturned,r.QuantityConsumed,InventoryRules.AtTechnician(r)}));
  output.Append("<h2>Labour and costs</h2>");Table(new[]{"Technician","Hours","Labour cost (INR)"},labour.Select(l=>new object?[]{l.Technician,l.Hours,l.CostAmount}));
  var netParts=movements.Where(x=>x.TransactionType=="Issue"||x.TransactionType=="Return").Sum(x=>x.ExtendedCost);output.Append("<p>").Append(H($"Parts (net issues): INR {netParts:0.00}; Labour: INR {labour.Sum(x=>x.CostAmount):0.00}; Other recorded costs: INR {cost.Sum(x=>x.Amount):0.00}. Recorded service cost, not a tax invoice.")).Append("</p><h2>QC and release</h2>");
  Table(new[]{"QC inspector","QC result","QC on","Road test","QC remarks"},new[]{new object?[]{qc?.Inspector,qc?.Result,qc?.InspectedAt,qc==null?"Not recorded":!qc.RoadTestRequired?"Not required":qc.RoadTestPassed?"Passed":"Not passed",qc?.Remarks}});
  Table(new[]{"Released by","Released on","Release remarks"},new[]{new object?[]{release?.ReleasedBy,release?.ReleasedAt,release?.Remarks}});
  output.Append("<p>Service centre representative: ____________________ &nbsp; Customer acknowledgement: ____________________</p><small>Report reference: ").Append(H(j.JobCardNumber)).Append(" · Generated ").Append(H(DateTime.UtcNow)).Append("</small></body></html>");return output.ToString();
 }
}
