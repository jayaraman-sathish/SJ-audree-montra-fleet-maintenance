using Microsoft.EntityFrameworkCore;
using MontraFleet.Api.Data;
using MontraFleet.Api.Models;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var rawConnection =
    Environment.GetEnvironmentVariable("DATABASE_URL")
    ?? builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Database connection is not configured.");

static string NormalizePostgresConnection(string raw)
{
    if (!raw.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
        && !raw.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        return raw;

    var uri = new Uri(raw);
    var userInfo = Uri.UnescapeDataString(uri.UserInfo).Split(':', 2);
    var builder = new NpgsqlConnectionStringBuilder
    {
        Host = uri.Host,
        Port = uri.IsDefaultPort ? 5432 : uri.Port,
        Username = userInfo[0],
        Password = userInfo.Length > 1 ? userInfo[1] : string.Empty,
        Database = uri.AbsolutePath.TrimStart('/'),
        SslMode = SslMode.Prefer
    };
    return builder.ConnectionString;
}

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(NormalizePostgresConnection(rawConnection)));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseDefaultFiles();
app.UseStaticFiles();

var autoCreate = builder.Configuration.GetValue("Database:AutoCreate", true)
                 || string.Equals(Environment.GetEnvironmentVariable("AUTO_CREATE_DB"), "true", StringComparison.OrdinalIgnoreCase);

if (autoCreate)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();
}

app.MapGet("/api/health", () => Results.Ok(new { status="ok", service="MontraFleet.Api", version="0.8" }));
app.MapGet("/api/db/health", async (AppDbContext db) =>
{
    try
    {
        var canConnect = await db.Database.CanConnectAsync();
        return canConnect
            ? Results.Ok(new { status="ok", database="PostgreSQL", connected=true })
            : Results.Problem("Database connection check returned false.", statusCode:503);
    }
    catch (Exception ex)
    {
        return Results.Problem(
            title:"Database connection failed",
            detail:ex.Message,
            statusCode:503);
    }
});
app.MapGet("/api/ui/health", (IWebHostEnvironment env) =>
{
    var webRoot = env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");
    var indexPath = Path.Combine(webRoot, "index.html");
    return Results.Ok(new {
        status = File.Exists(indexPath) ? "ok" : "missing",
        indexExists = File.Exists(indexPath),
        webRoot,
        version = "0.8"
    });
});

app.MapGet("/api/dashboard/summary", () => Results.Ok(new { totalVehicles=1248, available=1086, underMaintenance=96, offHire=50, appointmentsToday=18, breakdownRequests=7, pmOverdue=12, slaBreaches=3, firstTimeFix=91.4, uptime30d=96.2 }));
app.MapGet("/api/pm/obligations", () => Results.Ok(new[]{ new { id="PMO-1001", vehicle="TN12AB1234", plan="PM-5K", trigger="Odometer", remaining="1,480 km", status="Due Soon" }, new { id="PMO-1002", vehicle="KA05EV7782", plan="PM-3M", trigger="Time", remaining="Overdue by 2 days", status="Overdue" }, new { id="PMO-1003", vehicle="TN22EV9088", plan="PM-10K", trigger="Odometer", remaining="7,150 km", status="Upcoming" } }));
app.MapGet("/api/service-events/active", () => Results.Ok(new[]{ new { eventNo="SE-2026-001234", vehicle="TN12AB1234", type="PM Service", status="In Progress", jobCard="JC-2026-00987", bay="Bay-04", technician="Suresh K", sla="01:42 / 04:00" }, new { eventNo="SE-2026-001241", vehicle="KA05EV7782", type="Breakdown", status="Diagnosis", jobCard="JC-2026-00991", bay="HV-01", technician="Meena P", sla="00:58 / 02:00" } }));
app.MapGet("/api/service-events/{eventNo}/work-items", (string eventNo) => Results.Ok(new[]{ new { no=1, item="5,000 km PM inspection", checklist="18 / 24", parts="2 reserved", status="In Progress" }, new { no=2, item="Brake inspection", checklist="6 / 6", parts="0", status="Completed" }, new { no=3, item="Rectify coolant leak", checklist="3 / 5", parts="1 issued", status="Waiting Part" } }));
app.MapGet("/api/sla/active", () => Results.Ok(new[]{ new { eventNo="SE-2026-001234", priority="P2", clock="Resolution", elapsed="01:42", target="04:00", paused="00:00", state="Running", breach=false }, new { eventNo="SE-2026-001241", priority="P1", clock="Response", elapsed="00:58", target="01:00", paused="00:12", state="Running", breach=false }, new { eventNo="SE-2026-001198", priority="P2", clock="Resolution", elapsed="04:18", target="04:00", paused="00:00", state="Breached", breach=true } }));
app.MapGet("/api/vehicles/{vin}/360", (string vin) => Results.Ok(new { vin, registration="TN12AB1234", model="Montra eTruck 7T", variant="ExT 7T - Std", customer="ABC Logistics Ltd.", depot="Chennai Depot", odometerKm=48520, operatingHours=3120, batterySoc=78, availability="Available", uptime30d=96.2, nextPm="01 Oct 2026 / 1,480 km", openDefects=2, activeWarranty=true, openCampaigns=1, lastService="12 Aug 2026", documents=8 }));
app.MapGet("/api/warranty/{vin}", (string vin) => Results.Ok(new[]{ new { type="Vehicle Warranty", reference="WAR-VEH-2025-0041", start="01 Jan 2025", end="31 Dec 2027", limit="150,000 km", status="Active" }, new { type="Battery Warranty", reference="WAR-BAT-2025-0041", start="01 Jan 2025", end="31 Dec 2030", limit="300,000 km", status="Active" } }));
app.MapGet("/api/campaigns/open", () => Results.Ok(new[]{ new { code="CMP-2026-014", title="HV connector inspection", type="Safety Campaign", affected=128, completed=87, open=41, status="Active" }, new { code="CMP-2026-021", title="VCU software update", type="Service Campaign", affected=342, completed=310, open=32, status="Active" } }));
app.MapGet("/api/documents/{vin}", (string vin) => Results.Ok(new[]{ new { type="Registration Certificate", file="TN12AB1234_RC.pdf", uploaded="01 Jan 2025", status="Active" }, new { type="Insurance", file="TN12AB1234_Insurance.pdf", uploaded="04 Apr 2026", status="Active" }, new { type="Service Evidence", file="SE-2026-001234_QC.pdf", uploaded="12 Sep 2026", status="Active" } }));
app.MapGet("/api/search", (string? q) => Results.Ok(new { query=(q??"").Trim(), results=new object[]{ new { type="Vehicle", key="TN12AB1234", title="TN12AB1234 · Montra eTruck 7T", status="Available" }, new { type="Service Event", key="SE-2026-001234", title="PM Service · TN12AB1234", status="In Progress" }, new { type="Job Card", key="JC-2026-00987", title="JC-2026-00987 · Bay-04", status="In Progress" } } }));
app.MapGet("/api/integrations/status", () => Results.Ok(new[]{ new { name="ERP / Parts", direction="Outbound + Inbound", status="Ready for interface", pending=3 }, new { name="Telematics", direction="Inbound", status="Phase-1 adapter", pending=0 }, new { name="Document Storage", direction="Outbound", status="Ready for object storage", pending=1 } }));
app.MapPost("/api/sla", (SlaClock x) => Results.Created($"/api/sla/{x.Id}", x));
app.MapPost("/api/warranty", (WarrantyEntitlement x) => Results.Created($"/api/warranty/{x.Id}", x));
app.MapPost("/api/campaigns", (Campaign x) => Results.Created($"/api/campaigns/{x.Id}", x));
app.MapPost("/api/vehicle-campaigns", (VehicleCampaign x) => Results.Created($"/api/vehicle-campaigns/{x.Id}", x));
app.MapPost("/api/documents", (VehicleDocument x) => Results.Created($"/api/documents/{x.Id}", x));
app.MapPost("/api/integrations/outbox", (IntegrationOutbox x) => Results.Created($"/api/integrations/outbox/{x.Id}", x));

app.MapFallback(async context =>
{
    var env = context.RequestServices.GetRequiredService<IWebHostEnvironment>();
    var webRoot = env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");
    var indexPath = Path.Combine(webRoot, "index.html");

    if (!File.Exists(indexPath))
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await context.Response.WriteAsync("Angular UI is not present in wwwroot. Check the Render Dockerfile/build context.");
        return;
    }

    context.Response.ContentType = "text/html; charset=utf-8";
    await context.Response.SendFileAsync(indexPath);
});
app.Run();
