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
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/api/health", () => Results.Ok(new { status = "ok", service = "MontraFleet.Api", version = "0.3" }));

app.MapGet("/api/dashboard/summary", () => Results.Ok(new
{
    totalVehicles = 1248,
    inService = 1102,
    underMaintenance = 96,
    offHire = 50,
    appointmentsToday = 18,
    breakdownRequests = 7,
    pmOverdue = 12,
    slaBreaches = 3
}));

app.MapGet("/api/demo/vehicle-360", () => Results.Ok(new
{
    vehicle = new { registrationNumber = "TN12AB1234", vin = "MT7T72036001234", model = "Montra eTruck 7T", variant = "ExT 7T - Std", odometerKm = 48520, operatingHours = 3120, batterySoc = 78, uptime30Days = 96.2 },
    pm = new[]
    {
        new { plan = "PM-5K", trigger = "Odometer", due = "50,000 km", status = "Due Soon" },
        new { plan = "PM-3M", trigger = "Time", due = "01 Oct 2026", status = "Upcoming" }
    }
}));

app.MapGet("/api/pm/obligations", () => Results.Ok(new[]
{
    new { vehicle = "TN12AB1234", plan = "PM-5K", trigger = "Odometer", due = "50,000 km", status = "Due Soon" },
    new { vehicle = "KA05EV7782", plan = "PM-3M", trigger = "Time", due = "10 Sep 2026", status = "Overdue" }
}));

app.MapGet("/api/breakdowns", () => Results.Ok(new[]
{
    new { number = "BD-2026-00128", vehicle = "TN12AB1234", priority = "P1", complaint = "Vehicle stopped - drive system warning", location = "Sriperumbudur", status = "Technician Assigned", elapsed = "00:42" },
    new { number = "BD-2026-00129", vehicle = "KA05EV7782", priority = "P2", complaint = "Charging fault", location = "Chennai ORR", status = "Awaiting Towing", elapsed = "01:18" }
}));

app.MapGet("/api/job-cards/demo", () => Results.Ok(new
{
    jobCardNumber = "JC-2026-00987",
    vehicle = "TN12AB1234",
    eventNumber = "SE-2026-001234",
    status = "In Progress",
    bay = "Bay-04",
    technician = "Suresh K",
    workItems = new[]
    {
        new { description = "5,000 km PM inspection", status = "In Progress", srt = 1.5m },
        new { description = "Brake inspection", status = "Completed", srt = 0.5m },
        new { description = "Rectify coolant leak", status = "Waiting Part", srt = 0.8m }
    }
}));

app.MapGet("/api/parts/demo", () => Results.Ok(new[]
{
    new { partNumber = "CL-7T-018", description = "Coolant hose assembly", required = 1, reserved = 1, issued = 0, warrantyCandidate = true },
    new { partNumber = "FLT-7T-002", description = "Cabin filter", required = 1, reserved = 1, issued = 1, warrantyCandidate = false }
}));

app.MapGet("/api/release/demo", () => Results.Ok(new
{
    checklistComplete = true,
    defectsDispositioned = true,
    partsAndLabourRecorded = true,
    qcResult = "Pending",
    openWorkItems = 1,
    releaseAllowed = false
}));

app.MapGet("/api/availability/demo", () => Results.Ok(new[]
{
    new { state = "Available", startAt = "2026-09-01T06:00:00Z", endAt = "2026-09-11T10:42:00Z", reason = "Normal Operation" },
    new { state = "Breakdown", startAt = "2026-09-11T10:42:00Z", endAt = "2026-09-11T12:10:00Z", reason = "Drive System Warning" },
    new { state = "Under Maintenance", startAt = "2026-09-11T12:10:00Z", endAt = (string?)null, reason = "Service Event SE-2026-001234" }
}));

app.MapPost("/api/appointments", (Appointment entity) => Results.Created($"/api/appointments/{entity.Id}", entity));
app.MapPost("/api/service-events", (ServiceEvent entity) => Results.Created($"/api/service-events/{entity.Id}", entity));
app.MapPost("/api/job-cards", (JobCard entity) => Results.Created($"/api/job-cards/{entity.Id}", entity));
app.MapPost("/api/work-items", (WorkItem entity) => Results.Created($"/api/work-items/{entity.Id}", entity));
app.MapPost("/api/breakdowns", (Breakdown entity) => Results.Created($"/api/breakdowns/{entity.Id}", entity));
app.MapPost("/api/parts/transactions", (PartTransaction entity) => Results.Created($"/api/parts/transactions/{entity.Id}", entity));
app.MapPost("/api/labour", (LabourEntry entity) => Results.Created($"/api/labour/{entity.Id}", entity));
app.MapPost("/api/qc", (QcInspection entity) => Results.Created($"/api/qc/{entity.Id}", entity));
app.MapPost("/api/releases", (VehicleRelease entity) => Results.Created($"/api/releases/{entity.Id}", entity));
app.MapPost("/api/availability", (VehicleAvailabilityLedger entity) => Results.Created($"/api/availability/{entity.Id}", entity));

app.Run();
