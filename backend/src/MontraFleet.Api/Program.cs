using Microsoft.EntityFrameworkCore;
using MontraFleet.Api.Data;
using MontraFleet.Api.Models;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(options => options.AddPolicy("web", policy => policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Server=localhost;Database=MontraFleetMaintenance;Trusted_Connection=True;TrustServerCertificate=True";
builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlServer(connectionString));

var app = builder.Build();
app.UseCors("web");
if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }

app.MapGet("/api/health", () => Results.Ok(new { status = "ok", service = "MontraFleet.Api", version = "0.4" }));
app.MapGet("/api/dashboard/summary", () => Results.Ok(new { totalVehicles=1248, inService=1102, underMaintenance=96, offHire=50, appointmentsToday=18, breakdownRequests=7, pmOverdue=12, slaBreaches=3 }));

app.MapGet("/api/appointments/capacity", () => Results.Ok(new {
    date="2026-09-12", centre="Chennai Service Centre", bays=12, technicians=18, plannedHours=72, bookedHours=51.5, utilization=71.5,
    slots=new[]{
        new { time="09:00", bay="Bay-02", vehicle="TN12AB1234", type="PM", hours=2.5, status="Confirmed" },
        new { time="10:00", bay="HV-01", vehicle="KA05EV7782", type="Breakdown", hours=3.0, status="In Progress" },
        new { time="11:30", bay="Bay-05", vehicle="TN22EV9088", type="Inspection", hours=1.0, status="Scheduled" }
    }}));

app.MapGet("/api/technicians", () => Results.Ok(new[]{
    new { code="TECH-014", name="Suresh K", skills="EV, HV, Brakes", hv=true, validUntil="31 Dec 2026", load="5.5 / 8 h" },
    new { code="TECH-021", name="Akhil R", skills="EV, Diagnostics", hv=false, validUntil="-", load="4.0 / 8 h" },
    new { code="TECH-032", name="Meena P", skills="EV, HV, Battery", hv=true, validUntil="30 Jun 2027", load="6.0 / 8 h" }
}));

app.MapGet("/api/checklists/demo", () => Results.Ok(new[]{
    new { item="HV isolation verified", mandatory=true, result="Pass", executedBy="Suresh K" },
    new { item="Brake wear within limit", mandatory=true, result="Pass", executedBy="Suresh K" },
    new { item="Coolant leak inspection", mandatory=true, result="Fail", executedBy="Suresh K" },
    new { item="Road test", mandatory=true, result="Pending", executedBy="" }
}));

app.MapGet("/api/defects/demo", () => Results.Ok(new[]{
    new { no="DF-2026-00411", severity="Major", category="Cooling", description="Coolant hose seepage", failureCode="COOL-HOSE", disposition="Repair in progress", rca="Pending repeat-failure check" },
    new { no="DF-2026-00412", severity="Minor", category="Cabin", description="Cabin filter dirty", failureCode="CAB-FLT", disposition="Replace", rca="Not required" }
}));

app.MapGet("/api/offhire/demo", () => Results.Ok(new[]{
    new { vehicle="TN09CE4421", reason="Accident repair", start="09 Sep 2026 14:20", expectedReturn="16 Sep 2026", status="Approved", approver="Fleet Manager" },
    new { vehicle="KA05EV7782", reason="HV battery investigation", start="12 Sep 2026 10:15", expectedReturn="13 Sep 2026", status="Pending Approval", approver="-" }
}));

app.MapGet("/api/quality/ftf", () => Results.Ok(new {
    firstTimeFix=91.4, repeatFailures30d=8, openRca=3,
    recent=new[]{
        new { vehicle="TN12AB1234", eventNo="SE-2026-001234", matchKey="COOL-HOSE", repeat=false, ftf="Pass" },
        new { vehicle="KA05EV7782", eventNo="SE-2026-001221", matchKey="CHG-FAULT", repeat=true, ftf="Fail" }
    }}));

app.MapGet("/api/audit/recent", () => Results.Ok(new[]{
    new { at="12 Sep 17:20", user="ravi.k", action="QC_RESULT", entity="JC-2026-00987", detail="QC passed; road test passed" },
    new { at="12 Sep 17:12", user="suresh.k", action="CHECKLIST_UPDATE", entity="JC-2026-00987", detail="23/24 mandatory items completed" },
    new { at="12 Sep 16:58", user="system", action="AVAILABILITY_TRANSITION", entity="TN12AB1234", detail="Breakdown -> Under Maintenance" }
}));

app.MapGet("/api/release/readiness", () => Results.Ok(new {
    checklistComplete=true, defectsDispositioned=true, partsAndLabourRecorded=true, qcPassed=true, roadTestPassed=true,
    openWorkItems=0, hvAuthorizationValid=true, rcaComplete=true, approvalComplete=true, availabilityCloseReady=true, releaseAllowed=true
}));

app.MapPost("/api/appointments", (Appointment x) => Results.Created($"/api/appointments/{x.Id}", x));
app.MapPost("/api/technicians", (Technician x) => Results.Created($"/api/technicians/{x.Id}", x));
app.MapPost("/api/checklists", (ChecklistExecution x) => Results.Created($"/api/checklists/{x.Id}", x));
app.MapPost("/api/defects", (Defect x) => Results.Created($"/api/defects/{x.Id}", x));
app.MapPost("/api/service-events", (ServiceEvent x) => Results.Created($"/api/service-events/{x.Id}", x));
app.MapPost("/api/job-cards", (JobCard x) => Results.Created($"/api/job-cards/{x.Id}", x));
app.MapPost("/api/work-items", (WorkItem x) => Results.Created($"/api/work-items/{x.Id}", x));
app.MapPost("/api/breakdowns", (Breakdown x) => Results.Created($"/api/breakdowns/{x.Id}", x));
app.MapPost("/api/parts/transactions", (PartTransaction x) => Results.Created($"/api/parts/transactions/{x.Id}", x));
app.MapPost("/api/labour", (LabourEntry x) => Results.Created($"/api/labour/{x.Id}", x));
app.MapPost("/api/qc", (QcInspection x) => Results.Created($"/api/qc/{x.Id}", x));
app.MapPost("/api/releases", (VehicleRelease x) => Results.Created($"/api/releases/{x.Id}", x));
app.MapPost("/api/availability", (VehicleAvailabilityLedger x) => Results.Created($"/api/availability/{x.Id}", x));
app.MapPost("/api/offhire", (OffHireRecord x) => Results.Created($"/api/offhire/{x.Id}", x));
app.MapPost("/api/recommissioning", (RecommissioningInspection x) => Results.Created($"/api/recommissioning/{x.Id}", x));
app.MapPost("/api/approvals", (ApprovalRecord x) => x.RequestedBy == x.ApprovedBy && !string.IsNullOrWhiteSpace(x.ApprovedBy)
    ? Results.BadRequest(new { error="Maker-checker violation: requester cannot approve own action." })
    : Results.Created($"/api/approvals/{x.Id}", x));
app.MapPost("/api/audit", (AuditEvent x) => Results.Created($"/api/audit/{x.Id}", x));

app.Run();
