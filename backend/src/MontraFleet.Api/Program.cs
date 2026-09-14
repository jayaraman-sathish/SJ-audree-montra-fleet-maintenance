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
    var cs = new NpgsqlConnectionStringBuilder
    {
        Host = uri.Host,
        Port = uri.IsDefaultPort ? 5432 : uri.Port,
        Username = userInfo[0],
        Password = userInfo.Length > 1 ? userInfo[1] : string.Empty,
        Database = uri.AbsolutePath.TrimStart('/'),
        SslMode = SslMode.Prefer
    };
    return cs.ConnectionString;
}

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(NormalizePostgresConnection(rawConnection)));

var app = builder.Build();
if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }
app.UseDefaultFiles();
app.UseStaticFiles();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();
    await db.Database.ExecuteSqlRawAsync("""
        ALTER TABLE "Appointments" ADD COLUMN IF NOT EXISTS "AppointmentNumber" text NOT NULL DEFAULT '';
        ALTER TABLE "Appointments" ADD COLUMN IF NOT EXISTS "SourceType" text NOT NULL DEFAULT 'Manual';
        ALTER TABLE "Appointments" ADD COLUMN IF NOT EXISTS "SourceReference" text NOT NULL DEFAULT '';
        ALTER TABLE "Appointments" ADD COLUMN IF NOT EXISTS "TechnicianId" uuid NULL;
        ALTER TABLE "Appointments" ADD COLUMN IF NOT EXISTS "Technician" text NOT NULL DEFAULT '';
        ALTER TABLE "Appointments" ADD COLUMN IF NOT EXISTS "Priority" text NOT NULL DEFAULT 'P3';
        ALTER TABLE "Appointments" ADD COLUMN IF NOT EXISTS "Reason" text NOT NULL DEFAULT '';
        ALTER TABLE "Appointments" ADD COLUMN IF NOT EXISTS "CreatedBy" text NOT NULL DEFAULT 'Service User';
        ALTER TABLE "Appointments" ADD COLUMN IF NOT EXISTS "CreatedAt" timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP;

        ALTER TABLE "WorkItems" ADD COLUMN IF NOT EXISTS "TaskCode" text NOT NULL DEFAULT '';
        ALTER TABLE "WorkItems" ADD COLUMN IF NOT EXISTS "AssignedToTechnicianId" uuid NULL;
        ALTER TABLE "WorkItems" ADD COLUMN IF NOT EXISTS "AssignedTo" text NOT NULL DEFAULT '';
        ALTER TABLE "WorkItems" ADD COLUMN IF NOT EXISTS "PlannedStartAt" timestamp with time zone NULL;
        ALTER TABLE "WorkItems" ADD COLUMN IF NOT EXISTS "DueAt" timestamp with time zone NULL;
        ALTER TABLE "WorkItems" ADD COLUMN IF NOT EXISTS "Priority" text NOT NULL DEFAULT 'P3';
        ALTER TABLE "WorkItems" ADD COLUMN IF NOT EXISTS "DependencyTaskId" uuid NULL;
        ALTER TABLE "WorkItems" ADD COLUMN IF NOT EXISTS "EstimatedHours" numeric(18,2) NULL;
        ALTER TABLE "WorkItems" ADD COLUMN IF NOT EXISTS "ActualHours" numeric(18,2) NULL;
        ALTER TABLE "WorkItems" ADD COLUMN IF NOT EXISTS "EvidenceReference" text NOT NULL DEFAULT '';
        ALTER TABLE "WorkItems" ADD COLUMN IF NOT EXISTS "CompletionRemarks" text NOT NULL DEFAULT '';
        ALTER TABLE "WorkItems" ADD COLUMN IF NOT EXISTS "UpdatedAt" timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP;

        CREATE UNIQUE INDEX IF NOT EXISTS "IX_Appointments_AppointmentNumber" ON "Appointments" ("AppointmentNumber") WHERE "AppointmentNumber" <> '';
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_WorkItems_TaskCode" ON "WorkItems" ("TaskCode") WHERE "TaskCode" <> '';
        CREATE TABLE IF NOT EXISTS "PartMasters" (
            "Id" uuid PRIMARY KEY, "PartNumber" text NOT NULL, "Description" text NOT NULL DEFAULT '', "Category" text NOT NULL DEFAULT '',
            "UnitOfMeasure" text NOT NULL DEFAULT 'EA', "ManufacturerPartNumber" text NOT NULL DEFAULT '', "IsSerialized" boolean NOT NULL DEFAULT false,
            "IsWarrantyReturnable" boolean NOT NULL DEFAULT false, "ReorderLevel" numeric(18,3) NOT NULL DEFAULT 0, "ReorderQuantity" numeric(18,3) NOT NULL DEFAULT 0, "IsActive" boolean NOT NULL DEFAULT true
        );
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_PartMaster_PartNumber" ON "PartMasters" ("PartNumber");
        CREATE TABLE IF NOT EXISTS "InventoryLocations" (
            "Id" uuid PRIMARY KEY, "LocationCode" text NOT NULL, "Name" text NOT NULL DEFAULT '', "ServiceCentre" text NOT NULL DEFAULT '', "Bin" text NOT NULL DEFAULT '', "IsActive" boolean NOT NULL DEFAULT true
        );
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_InventoryLocation_Code" ON "InventoryLocations" ("LocationCode");
        CREATE TABLE IF NOT EXISTS "PartStocks" (
            "Id" uuid PRIMARY KEY, "PartMasterId" uuid NOT NULL, "InventoryLocationId" uuid NOT NULL, "OnHandQty" numeric(18,3) NOT NULL DEFAULT 0, "ReservedQty" numeric(18,3) NOT NULL DEFAULT 0, "UpdatedAt" timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP
        );
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_PartStock_PartLocation" ON "PartStocks" ("PartMasterId","InventoryLocationId");
        CREATE TABLE IF NOT EXISTS "PartRequests" (
            "Id" uuid PRIMARY KEY, "RequestNumber" text NOT NULL, "JobCardId" uuid NOT NULL, "WorkItemId" uuid NULL, "PartMasterId" uuid NOT NULL, "InventoryLocationId" uuid NOT NULL,
            "QuantityRequired" numeric(18,3) NOT NULL DEFAULT 0, "QuantityReserved" numeric(18,3) NOT NULL DEFAULT 0, "QuantityIssued" numeric(18,3) NOT NULL DEFAULT 0,
            "QuantityReturned" numeric(18,3) NOT NULL DEFAULT 0, "QuantityConsumed" numeric(18,3) NOT NULL DEFAULT 0, "Status" text NOT NULL DEFAULT 'Requested',
            "WarrantyCandidate" boolean NOT NULL DEFAULT false, "FailedPartDisposition" text NOT NULL DEFAULT '', "RequestedBy" text NOT NULL DEFAULT 'Technician',
            "RequestedAt" timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP, "UpdatedAt" timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP
        );
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_PartRequest_Number" ON "PartRequests" ("RequestNumber");
        ALTER TABLE "PartTransactions" ADD COLUMN IF NOT EXISTS "PartRequestId" uuid NULL;
        ALTER TABLE "PartTransactions" ADD COLUMN IF NOT EXISTS "PartMasterId" uuid NULL;
        ALTER TABLE "PartTransactions" ADD COLUMN IF NOT EXISTS "InventoryLocationId" uuid NULL;
        ALTER TABLE "PartTransactions" ADD COLUMN IF NOT EXISTS "PerformedBy" text NOT NULL DEFAULT 'Store User';

    """);



    if (!await db.InventoryLocations.AnyAsync())
    {
        db.InventoryLocations.Add(new InventoryLocation { LocationCode="MAIN-STORE", Name="Main Parts Store", ServiceCentre="Chennai Service Centre", Bin="GENERAL" });
        await db.SaveChangesAsync();
    }
    if (!await db.PartMasters.AnyAsync())
    {
        db.PartMasters.AddRange(
            new PartMaster { PartNumber="CL-7T-018", Description="Coolant hose assembly", Category="Cooling", UnitOfMeasure="EA", IsWarrantyReturnable=true, ReorderLevel=2, ReorderQuantity=5 },
            new PartMaster { PartNumber="FLT-7T-002", Description="Cabin filter", Category="Filter", UnitOfMeasure="EA", ReorderLevel=4, ReorderQuantity=10 },
            new PartMaster { PartNumber="CLP-7T-007", Description="Hose clamp", Category="Cooling", UnitOfMeasure="EA", ReorderLevel=5, ReorderQuantity=20 }
        );
        await db.SaveChangesAsync();
    }
    var seedLocation=await db.InventoryLocations.FirstAsync();
    foreach (var p in await db.PartMasters.ToListAsync())
    {
        if (!await db.PartStocks.AnyAsync(x=>x.PartMasterId==p.Id && x.InventoryLocationId==seedLocation.Id))
            db.PartStocks.Add(new PartStock { PartMasterId=p.Id, InventoryLocationId=seedLocation.Id, OnHandQty=p.PartNumber=="CL-7T-018"?4:p.PartNumber=="FLT-7T-002"?8:12, ReservedQty=0 });
    }
    await db.SaveChangesAsync();
    if (!await db.Vehicles.AnyAsync())
    {
        db.Vehicles.AddRange(
            new Vehicle { Vin="MA1AU7EV001234", RegistrationNumber="TN12AB1234", Model="Montra eTruck 7T", Variant="Std", Status="Available", OdometerKm=48520, OperatingHours=3120, BatterySoc=78 },
            new Vehicle { Vin="MA1AU7EV007782", RegistrationNumber="KA05EV7782", Model="Montra eTruck 7T", Variant="Long Range", Status="Available", OdometerKm=36240, OperatingHours=2710, BatterySoc=64 },
            new Vehicle { Vin="MA1AU7EV009088", RegistrationNumber="TN22EV9088", Model="Montra eTruck 7T", Variant="Std", Status="Available", OdometerKm=52850, OperatingHours=3450, BatterySoc=82 }
        );
        db.ServiceBays.AddRange(
            new ServiceBay { ServiceCentre="Chennai Service Centre", BayCode="Bay-01", BayType="General" },
            new ServiceBay { ServiceCentre="Chennai Service Centre", BayCode="Bay-02", BayType="General" },
            new ServiceBay { ServiceCentre="Chennai Service Centre", BayCode="HV-01", BayType="HV" }
        );
        db.Technicians.AddRange(
            new Technician { EmployeeCode="TECH001", Name="Suresh K", ServiceCentre="Chennai Service Centre", SkillCodes="PM,BRAKE", HvAuthorized=false },
            new Technician { EmployeeCode="TECH002", Name="Meena P", ServiceCentre="Chennai Service Centre", SkillCodes="EV,HV,DIAG", HvAuthorized=true, HvAuthorizationValidUntil=DateTime.UtcNow.AddYears(1) }
        );
        await db.SaveChangesAsync();
    }
}

static void Audit(AppDbContext db, string action, string entityType, Guid? entityId, string details, string user="System")
{
    db.AuditEvents.Add(new AuditEvent {
        UserName=user, Action=action, EntityType=entityType, EntityId=entityId,
        CorrelationId=Guid.NewGuid().ToString("N"), Details=details
    });
}

app.MapGet("/api/health", () => Results.Ok(new { status="ok", service="MontraFleet.Api", version="1.3" }));
app.MapGet("/api/db/health", async (AppDbContext db) =>
{
    try { return await db.Database.CanConnectAsync()
        ? Results.Ok(new { status="ok", database="PostgreSQL", connected=true, version="1.3" })
        : Results.Problem("Database connection check returned false.", statusCode:503); }
    catch (Exception ex) { return Results.Problem("Database connection failed", ex.Message, statusCode:503); }
});
app.MapGet("/api/ui/health", (IWebHostEnvironment env) =>
{
    var webRoot = env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");
    var indexPath = Path.Combine(webRoot, "index.html");
    return Results.Ok(new { status=File.Exists(indexPath)?"ok":"missing", indexExists=File.Exists(indexPath), webRoot, version="1.3" });
});

app.MapGet("/api/dashboard/summary", async (AppDbContext db) =>
{
    var total = await db.Vehicles.CountAsync();
    var available = await db.Vehicles.CountAsync(x => x.Status == "Available");
    var maintenance = await db.Vehicles.CountAsync(x => x.Status == "Under Maintenance");
    var offHire = await db.OffHireRecords.CountAsync(x => x.Status != "Closed");
    var today = DateTime.UtcNow.Date;
    var tomorrow = today.AddDays(1);
    var appointments = await db.Appointments.CountAsync(x => x.StartAt >= today && x.StartAt < tomorrow);
    var breakdowns = await db.Breakdowns.CountAsync(x => x.Status != "Closed");
    var pmOverdue = await db.PmObligations.CountAsync(x => x.Status == "Overdue");
    return Results.Ok(new { totalVehicles=total, available, underMaintenance=maintenance, offHire, appointmentsToday=appointments, breakdownRequests=breakdowns, pmOverdue, slaBreaches=0, firstTimeFix=100.0, uptime30d=99.0 });
});

app.MapGet("/api/vehicles", async (AppDbContext db) =>
    Results.Ok(await db.Vehicles.AsNoTracking().OrderBy(x=>x.RegistrationNumber).ToListAsync()));

app.MapPost("/api/vehicles", async (Vehicle vehicle, AppDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(vehicle.Vin) || string.IsNullOrWhiteSpace(vehicle.RegistrationNumber))
        return Results.BadRequest(new { message="VIN and registration are required." });
    if (await db.Vehicles.AnyAsync(x => x.Vin == vehicle.Vin))
        return Results.Conflict(new { message="VIN already exists." });
    db.Vehicles.Add(vehicle); Audit(db,"CREATE","Vehicle",vehicle.Id,vehicle.RegistrationNumber);
    await db.SaveChangesAsync();
    return Results.Created($"/api/vehicles/{vehicle.Id}", vehicle);
});

app.MapPut("/api/vehicles/{id:guid}", async (Guid id, Vehicle input, AppDbContext db) =>
{
    var v = await db.Vehicles.FindAsync(id); if (v is null) return Results.NotFound();
    v.RegistrationNumber=input.RegistrationNumber; v.Model=input.Model; v.Variant=input.Variant;
    v.Status=input.Status; v.OdometerKm=input.OdometerKm; v.OperatingHours=input.OperatingHours; v.BatterySoc=input.BatterySoc;
    Audit(db,"UPDATE","Vehicle",v.Id,v.RegistrationNumber); await db.SaveChangesAsync(); return Results.Ok(v);
});

app.MapGet("/api/pm/obligations", async (AppDbContext db) =>
{
    var rows = await (from p in db.PmObligations.AsNoTracking()
                      join v in db.Vehicles.AsNoTracking() on p.VehicleId equals v.Id
                      orderby p.GeneratedAt descending
                      select new { p.Id, p.VehicleId, vehicle=v.RegistrationNumber, plan=p.PlanCode, trigger=p.TriggerType, p.DueDate, p.DueReading, p.Status,
                          remaining = p.TriggerType=="Odometer" && p.DueReading!=null ? (p.DueReading-v.OdometerKm)+" km" :
                                      p.DueDate!=null ? (p.DueDate.Value.Date-DateTime.UtcNow.Date).Days+" days" : "-" }).ToListAsync();
    return Results.Ok(rows);
});

app.MapPost("/api/pm/obligations/generate", async (PmRequest r, AppDbContext db) =>
{
    var v = await db.Vehicles.FindAsync(r.VehicleId); if (v is null) return Results.BadRequest(new { message="Vehicle not found." });
    var p = new PmObligation { VehicleId=v.Id, PlanCode=r.PlanCode, TriggerType=r.TriggerType, DueDate=r.DueDate, DueReading=r.DueReading };
    if (r.TriggerType=="Odometer" && r.DueReading.HasValue) p.Status = r.DueReading.Value <= v.OdometerKm ? "Overdue" : (r.DueReading.Value-v.OdometerKm<=2000 ? "Due Soon":"Upcoming");
    else if (r.DueDate.HasValue) p.Status = r.DueDate.Value.Date < DateTime.UtcNow.Date ? "Overdue" : ((r.DueDate.Value.Date-DateTime.UtcNow.Date).Days<=7 ? "Due Soon":"Upcoming");
    db.PmObligations.Add(p); Audit(db,"GENERATE","PmObligation",p.Id,$"{v.RegistrationNumber} {p.PlanCode}");
    await db.SaveChangesAsync(); return Results.Created($"/api/pm/obligations/{p.Id}", p);
});


app.MapGet("/api/appointments", async (AppDbContext db) =>
{
    var rows = await (from a in db.Appointments.AsNoTracking()
                      join v in db.Vehicles.AsNoTracking() on a.VehicleId equals v.Id
                      orderby a.StartAt descending
                      select new { a.Id,a.AppointmentNumber,a.VehicleId,vehicle=v.RegistrationNumber,a.SourceType,a.SourceReference,
                          a.StartAt,a.EndAt,a.ServiceCentre,a.Bay,a.TechnicianId,a.Technician,a.AppointmentType,a.Priority,a.Reason,
                          a.PlannedHours,a.Status,a.PmObligationId,a.CreatedBy,a.CreatedAt }).ToListAsync();
    return Results.Ok(rows);
});

app.MapGet("/api/capacity", async (DateTime? date, AppDbContext db) =>
{
    var d=(date ?? DateTime.UtcNow).Date; var next=d.AddDays(1);
    var bays=await db.ServiceBays.AsNoTracking().Where(x=>x.IsActive).OrderBy(x=>x.BayCode).ToListAsync();
    var techs=await db.Technicians.AsNoTracking().Where(x=>x.IsActive).OrderBy(x=>x.Name).ToListAsync();
    var appts=await db.Appointments.AsNoTracking().Where(x=>x.StartAt>=d && x.StartAt<next && x.Status!="Cancelled" && x.Status!="No-show").ToListAsync();
    return Results.Ok(new {
        date=d,
        bays=bays.Select(b=>new {b.Id,b.BayCode,b.BayType,b.ServiceCentre,bookedHours=appts.Where(a=>a.Bay==b.BayCode).Sum(a=>a.PlannedHours)}),
        technicians=techs.Select(t=>new {t.Id,t.EmployeeCode,t.Name,t.SkillCodes,t.HvAuthorized,t.HvAuthorizationValidUntil,
            bookedHours=appts.Where(a=>a.TechnicianId==t.Id).Sum(a=>a.PlannedHours)})
    });
});

app.MapPost("/api/appointments", async (AppointmentRequest r, AppDbContext db) =>
{
    var vehicle=await db.Vehicles.FindAsync(r.VehicleId);
    if(vehicle is null) return Results.BadRequest(new {message="Vehicle not found."});
    if(r.StartAt==default || r.PlannedHours<=0) return Results.BadRequest(new {message="Start date/time and planned hours are required."});

    var end=r.StartAt.AddHours((double)r.PlannedHours);
    var bayClash=await db.Appointments.AnyAsync(x=>x.Bay==r.Bay && x.Status!="Cancelled" && x.Status!="No-show" &&
        x.StartAt<end && (x.EndAt ?? x.StartAt.AddHours((double)x.PlannedHours))>r.StartAt);
    if(bayClash) return Results.Conflict(new {message=$"{r.Bay} is already booked for the selected time."});

    Technician? tech=null;
    if(r.TechnicianId.HasValue)
    {
        tech=await db.Technicians.FindAsync(r.TechnicianId.Value);
        if(tech is null || !tech.IsActive) return Results.BadRequest(new {message="Selected technician is not active."});
        var techClash=await db.Appointments.AnyAsync(x=>x.TechnicianId==r.TechnicianId && x.Status!="Cancelled" && x.Status!="No-show" &&
            x.StartAt<end && (x.EndAt ?? x.StartAt.AddHours((double)x.PlannedHours))>r.StartAt);
        if(techClash) return Results.Conflict(new {message=$"{tech.Name} is already assigned during the selected time."});
        if(r.Bay.StartsWith("HV",StringComparison.OrdinalIgnoreCase) && (!tech.HvAuthorized || tech.HvAuthorizationValidUntil<DateTime.UtcNow))
            return Results.Conflict(new {message="An active HV-authorized technician is required for an HV bay."});
    }

    var seq=await db.Appointments.CountAsync()+1;
    var a=new Appointment{
        AppointmentNumber=$"APT-{DateTime.UtcNow:yyyyMMdd}-{seq:D5}",VehicleId=r.VehicleId,PmObligationId=r.PmObligationId,
        SourceType=r.SourceType,SourceReference=r.SourceReference,StartAt=r.StartAt,EndAt=end,ServiceCentre=r.ServiceCentre,
        Bay=r.Bay,TechnicianId=r.TechnicianId,Technician=tech?.Name ?? "",AppointmentType=r.AppointmentType,Priority=r.Priority,
        Reason=r.Reason,PlannedHours=r.PlannedHours,Status="Requested",CreatedBy=r.CreatedBy,CreatedAt=DateTime.UtcNow
    };
    db.Appointments.Add(a); Audit(db,"CREATE","Appointment",a.Id,$"{a.AppointmentNumber} {vehicle.RegistrationNumber}");
    await db.SaveChangesAsync(); return Results.Created($"/api/appointments/{a.Id}",a);
});

app.MapPut("/api/appointments/{id:guid}/status", async (Guid id, StatusRequest r, AppDbContext db) =>
{
    var a=await db.Appointments.FindAsync(id); if(a is null)return Results.NotFound();
    var allowed=new[]{"Requested","Confirmed","Checked-In","In Progress","Completed","Cancelled","No-show"};
    if(!allowed.Contains(r.Status))return Results.BadRequest(new{message="Invalid appointment status."});
    a.Status=r.Status; Audit(db,"STATUS","Appointment",a.Id,r.Status); await db.SaveChangesAsync(); return Results.Ok(a);
});

app.MapPost("/api/appointments/{id:guid}/start-service", async (Guid id, AppDbContext db) =>
{
    var a=await db.Appointments.FindAsync(id); if(a is null)return Results.NotFound();
    var v=await db.Vehicles.FindAsync(a.VehicleId); if(v is null)return Results.BadRequest();
    if(a.Status=="In Progress")return Results.Conflict(new{message="Appointment already started."});
    if(a.Status=="Cancelled"||a.Status=="No-show"||a.Status=="Completed")return Results.Conflict(new{message=$"Cannot start a {a.Status} appointment."});

    var e=new ServiceEvent{VehicleId=v.Id,EventNumber=$"SE-{DateTime.UtcNow:yyyy}-{(await db.ServiceEvents.CountAsync()+1):D6}",
        EventType=a.AppointmentType,Priority=a.Priority,Status="In Progress"};
    var jc=new JobCard{ServiceEventId=e.Id,JobCardNumber=$"JC-{DateTime.UtcNow:yyyy}-{(await db.JobCards.CountAsync()+1):D6}",
        Status="Open",Bay=a.Bay,TechnicianId=a.TechnicianId,Technician=a.Technician,StartedAt=DateTime.UtcNow};
    a.Status="In Progress"; v.Status="Under Maintenance"; db.ServiceEvents.Add(e); db.JobCards.Add(jc);
    db.VehicleAvailabilityLedger.Add(new VehicleAvailabilityLedger{VehicleId=v.Id,State="Under Maintenance",StartAt=DateTime.UtcNow,
        ReasonCode=a.AppointmentType,SourceType="ServiceEvent",SourceServiceEventId=e.Id});
    if(a.PmObligationId.HasValue){var p=await db.PmObligations.FindAsync(a.PmObligationId.Value);if(p!=null)p.Status="In Service";}
    Audit(db,"START","ServiceEvent",e.Id,$"{v.RegistrationNumber} via {a.AppointmentNumber}");
    await db.SaveChangesAsync(); return Results.Ok(new{serviceEvent=e,jobCard=jc});
});

app.MapGet("/api/service-events/active", async (AppDbContext db) =>
{
    var rows = await (from e in db.ServiceEvents.AsNoTracking()
                      join v in db.Vehicles.AsNoTracking() on e.VehicleId equals v.Id
                      join j in db.JobCards.AsNoTracking() on e.Id equals j.ServiceEventId into jj
                      from j in jj.DefaultIfEmpty()
                      where e.Status!="Closed"
                      orderby e.OpenedAt descending
                      select new { e.Id,eventNo=e.EventNumber,vehicle=v.RegistrationNumber,type=e.EventType,e.Status,jobCard=j!=null?j.JobCardNumber:"",jobCardId=j!=null?j.Id:(Guid?)null,bay=j!=null?j.Bay:"",technician=j!=null?j.Technician:"",sla=e.Priority }).ToListAsync();
    return Results.Ok(rows);
});

app.MapGet("/api/job-cards", async (AppDbContext db) =>
{
    var rows = await (from j in db.JobCards.AsNoTracking()
                      join e in db.ServiceEvents.AsNoTracking() on j.ServiceEventId equals e.Id
                      join v in db.Vehicles.AsNoTracking() on e.VehicleId equals v.Id
                      orderby j.StartedAt descending
                      select new { j.Id,j.JobCardNumber,j.Status,j.Bay,j.Technician,j.TechnicianId,j.StartedAt,j.CompletedAt,serviceEventId=e.Id,e.EventNumber,vehicle=v.RegistrationNumber }).ToListAsync();
    return Results.Ok(rows);
});


app.MapGet("/api/job-cards/{id:guid}/work-items", async (Guid id, AppDbContext db) =>
    Results.Ok(await db.WorkItems.AsNoTracking().Where(x=>x.JobCardId==id).OrderBy(x=>x.DueAt).ThenBy(x=>x.TaskCode).ToListAsync()));

app.MapGet("/api/tasks", async (Guid? jobCardId, AppDbContext db) =>
{
    var q=db.WorkItems.AsNoTracking().AsQueryable();
    if(jobCardId.HasValue)q=q.Where(x=>x.JobCardId==jobCardId.Value);
    var rows=await (from t in q join j in db.JobCards.AsNoTracking() on t.JobCardId equals j.Id
                    join e in db.ServiceEvents.AsNoTracking() on j.ServiceEventId equals e.Id
                    join v in db.Vehicles.AsNoTracking() on e.VehicleId equals v.Id
                    orderby t.DueAt,t.TaskCode
                    select new{t.Id,t.TaskCode,t.JobCardId,jobCard=j.JobCardNumber,vehicle=v.RegistrationNumber,t.WorkType,t.Description,t.Status,
                        t.AssignedToTechnicianId,t.AssignedTo,t.PlannedStartAt,t.DueAt,t.Priority,t.DependencyTaskId,t.EstimatedHours,t.ActualHours,
                        t.EvidenceReference,t.CompletionRemarks,t.RequiresQc,t.RequiresHvAuthorization,t.UpdatedAt}).ToListAsync();
    return Results.Ok(rows);
});

app.MapPost("/api/tasks", async (TaskRequest r, AppDbContext db) =>
{
    var jc=await db.JobCards.FindAsync(r.JobCardId);if(jc is null)return Results.BadRequest(new{message="Job card not found."});
    Technician? tech=null;if(r.AssignedToTechnicianId.HasValue){tech=await db.Technicians.FindAsync(r.AssignedToTechnicianId.Value);if(tech is null)return Results.BadRequest(new{message="Technician not found."});}
    if(r.DependencyTaskId.HasValue && !await db.WorkItems.AnyAsync(x=>x.Id==r.DependencyTaskId.Value && x.JobCardId==r.JobCardId))
        return Results.BadRequest(new{message="Dependency must belong to the same job card."});
    var t=new WorkItem{JobCardId=r.JobCardId,TaskCode=$"TSK-{DateTime.UtcNow:yyyyMMdd}-{(await db.WorkItems.CountAsync()+1):D5}",
        WorkType=r.WorkType,Description=r.Description,Status="Not Started",AssignedToTechnicianId=r.AssignedToTechnicianId,AssignedTo=tech?.Name??"",
        PlannedStartAt=r.PlannedStartAt,DueAt=r.DueAt,Priority=r.Priority,DependencyTaskId=r.DependencyTaskId,EstimatedHours=r.EstimatedHours,
        RequiresQc=r.RequiresQc,RequiresHvAuthorization=r.RequiresHvAuthorization,UpdatedAt=DateTime.UtcNow};
    db.WorkItems.Add(t);Audit(db,"CREATE","Task",t.Id,$"{t.TaskCode} {t.Description}");await db.SaveChangesAsync();return Results.Created($"/api/tasks/{t.Id}",t);
});

app.MapPut("/api/tasks/{id:guid}/status", async (Guid id, TaskStatusRequest r, AppDbContext db) =>
{
    var t=await db.WorkItems.FindAsync(id);if(t is null)return Results.NotFound();
    var allowed=new[]{"Not Started","Assigned","In Progress","On Hold","Completed","Cancelled"};if(!allowed.Contains(r.Status))return Results.BadRequest(new{message="Invalid task status."});
    if(r.Status=="In Progress" && t.DependencyTaskId.HasValue){var d=await db.WorkItems.FindAsync(t.DependencyTaskId.Value);if(d!=null&&d.Status!="Completed")return Results.Conflict(new{message=$"Dependency {d.TaskCode} must be completed first."});}
    t.Status=r.Status;t.ActualHours=r.ActualHours??t.ActualHours;t.CompletionRemarks=r.CompletionRemarks??t.CompletionRemarks;t.EvidenceReference=r.EvidenceReference??t.EvidenceReference;t.UpdatedAt=DateTime.UtcNow;
    Audit(db,"STATUS","Task",t.Id,$"{t.TaskCode}:{r.Status}");await db.SaveChangesAsync();return Results.Ok(t);
});

app.MapPut("/api/tasks/{id:guid}/assign", async (Guid id, TaskAssignRequest r, AppDbContext db) =>
{
    var t=await db.WorkItems.FindAsync(id);if(t is null)return Results.NotFound();var tech=await db.Technicians.FindAsync(r.TechnicianId);
    if(tech is null||!tech.IsActive)return Results.BadRequest(new{message="Technician not active."});
    if(t.RequiresHvAuthorization && (!tech.HvAuthorized || tech.HvAuthorizationValidUntil<DateTime.UtcNow))return Results.Conflict(new{message="Task requires active HV authorization."});
    t.AssignedToTechnicianId=tech.Id;t.AssignedTo=tech.Name;if(t.Status=="Not Started")t.Status="Assigned";t.UpdatedAt=DateTime.UtcNow;
    Audit(db,"ASSIGN","Task",t.Id,$"{t.TaskCode}->{tech.Name}");await db.SaveChangesAsync();return Results.Ok(t);
});

app.MapPost("/api/job-cards/{id:guid}/work-items", async (Guid id, WorkItemRequest r, AppDbContext db) =>
{
    if(!await db.JobCards.AnyAsync(x=>x.Id==id))return Results.NotFound();
    var w=new WorkItem{JobCardId=id,TaskCode=$"TSK-{DateTime.UtcNow:yyyyMMdd}-{(await db.WorkItems.CountAsync()+1):D5}",WorkType=r.WorkType,
        Description=r.Description,Status="Not Started",EstimatedHours=r.StandardRepairHours,StandardRepairHours=r.StandardRepairHours,
        RequiresQc=r.RequiresQc,RequiresHvAuthorization=r.RequiresHvAuthorization,UpdatedAt=DateTime.UtcNow};
    db.WorkItems.Add(w);Audit(db,"CREATE","Task",w.Id,w.Description);await db.SaveChangesAsync();return Results.Created($"/api/tasks/{w.Id}",w);
});
app.MapPut("/api/work-items/{id:guid}/status", async (Guid id, StatusRequest r, AppDbContext db) =>
{
    var w=await db.WorkItems.FindAsync(id);if(w is null)return Results.NotFound();w.Status=r.Status;w.UpdatedAt=DateTime.UtcNow;Audit(db,"STATUS","Task",w.Id,r.Status);await db.SaveChangesAsync();return Results.Ok(w);
});

app.MapGet("/api/labour", async (Guid? jobCardId, AppDbContext db) =>
{
    var q=db.LabourEntries.AsNoTracking().AsQueryable();if(jobCardId.HasValue)q=q.Where(x=>x.JobCardId==jobCardId.Value);
    return Results.Ok(await q.OrderByDescending(x=>x.StartAt).ToListAsync());
});

app.MapGet("/api/service-workspace/{jobCardId:guid}", async (Guid jobCardId, AppDbContext db) =>
{
    var data=await (from j in db.JobCards.AsNoTracking() join e in db.ServiceEvents.AsNoTracking() on j.ServiceEventId equals e.Id
                    join v in db.Vehicles.AsNoTracking() on e.VehicleId equals v.Id where j.Id==jobCardId
                    select new{jobCardId=j.Id,j.JobCardNumber,j.Status,j.Bay,j.Technician,j.TechnicianId,serviceEventId=e.Id,e.EventNumber,e.EventType,e.Priority,
                        vehicleId=v.Id,vehicle=v.RegistrationNumber,v.Vin,v.Model,v.OdometerKm,v.OperatingHours}).FirstOrDefaultAsync();
    if(data is null)return Results.NotFound();
    var tasks=await db.WorkItems.CountAsync(x=>x.JobCardId==jobCardId);var completed=await db.WorkItems.CountAsync(x=>x.JobCardId==jobCardId&&x.Status=="Completed");
    var defects=await db.Defects.CountAsync(x=>x.JobCardId==jobCardId&&x.Disposition!="Closed");
    var parts=await db.PartRequests.CountAsync(x=>x.JobCardId==jobCardId);var qc=await db.QcInspections.Where(x=>x.JobCardId==jobCardId).OrderByDescending(x=>x.InspectedAt).Select(x=>x.Result).FirstOrDefaultAsync();
    return Results.Ok(new{data,tasks,completedTasks=completed,openDefects=defects,partTransactions=parts,qcStatus=qc??"Pending"});
});

app.MapGet("/api/parts/master", async (AppDbContext db) => Results.Ok(await db.PartMasters.AsNoTracking().Where(x=>x.IsActive).OrderBy(x=>x.PartNumber).ToListAsync()));
app.MapPost("/api/parts/master", async (PartMasterRequest r, AppDbContext db) =>
{
    if(string.IsNullOrWhiteSpace(r.PartNumber))return Results.BadRequest(new{message="Part number is required."});
    if(await db.PartMasters.AnyAsync(x=>x.PartNumber==r.PartNumber))return Results.Conflict(new{message="Part number already exists."});
    var p=new PartMaster{PartNumber=r.PartNumber.Trim(),Description=r.Description,Category=r.Category,UnitOfMeasure=r.UnitOfMeasure,ManufacturerPartNumber=r.ManufacturerPartNumber,
        IsSerialized=r.IsSerialized,IsWarrantyReturnable=r.IsWarrantyReturnable,ReorderLevel=r.ReorderLevel,ReorderQuantity=r.ReorderQuantity};
    db.PartMasters.Add(p);Audit(db,"CREATE","PartMaster",p.Id,p.PartNumber);await db.SaveChangesAsync();return Results.Created($"/api/parts/master/{p.Id}",p);
});
app.MapGet("/api/parts/locations", async (AppDbContext db) => Results.Ok(await db.InventoryLocations.AsNoTracking().Where(x=>x.IsActive).OrderBy(x=>x.LocationCode).ToListAsync()));
app.MapPost("/api/parts/locations", async (InventoryLocation r, AppDbContext db) =>
{
    if(string.IsNullOrWhiteSpace(r.LocationCode))return Results.BadRequest(new{message="Location code is required."});
    if(await db.InventoryLocations.AnyAsync(x=>x.LocationCode==r.LocationCode))return Results.Conflict(new{message="Location already exists."});
    r.Id=Guid.NewGuid();db.InventoryLocations.Add(r);Audit(db,"CREATE","InventoryLocation",r.Id,r.LocationCode);await db.SaveChangesAsync();return Results.Ok(r);
});
app.MapGet("/api/parts/stock", async (AppDbContext db) =>
{
    var rows=await (from s in db.PartStocks.AsNoTracking() join p in db.PartMasters.AsNoTracking() on s.PartMasterId equals p.Id
                    join l in db.InventoryLocations.AsNoTracking() on s.InventoryLocationId equals l.Id orderby p.PartNumber
                    select new{s.Id,s.PartMasterId,p.PartNumber,p.Description,p.Category,p.UnitOfMeasure,s.InventoryLocationId,l.LocationCode,location=l.Name,l.Bin,
                        s.OnHandQty,s.ReservedQty,availableQty=s.OnHandQty-s.ReservedQty,p.ReorderLevel,p.ReorderQuantity,reorderRequired=(s.OnHandQty-s.ReservedQty)<=p.ReorderLevel,s.UpdatedAt}).ToListAsync();
    return Results.Ok(rows);
});
app.MapPost("/api/parts/receive", async (StockMovementRequest r, AppDbContext db) =>
{
    if(r.Quantity<=0)return Results.BadRequest(new{message="Quantity must be greater than zero."});
    var p=await db.PartMasters.FindAsync(r.PartMasterId);var l=await db.InventoryLocations.FindAsync(r.InventoryLocationId);if(p is null||l is null)return Results.BadRequest(new{message="Part or location not found."});
    var s=await db.PartStocks.FirstOrDefaultAsync(x=>x.PartMasterId==p.Id&&x.InventoryLocationId==l.Id);if(s is null){s=new PartStock{PartMasterId=p.Id,InventoryLocationId=l.Id};db.PartStocks.Add(s);}
    s.OnHandQty+=r.Quantity;s.UpdatedAt=DateTime.UtcNow;
    var tx=new PartTransaction{PartMasterId=p.Id,InventoryLocationId=l.Id,JobCardId=Guid.Empty,PartNumber=p.PartNumber,PartDescription=p.Description,TransactionType="Receipt",Quantity=r.Quantity,PerformedBy=r.User};
    db.PartTransactions.Add(tx);Audit(db,"RECEIPT","PartStock",s.Id,$"{p.PartNumber} +{r.Quantity}",r.User);await db.SaveChangesAsync();return Results.Ok(s);
});
app.MapGet("/api/part-requests", async (Guid? jobCardId, AppDbContext db) =>
{
    var q=db.PartRequests.AsNoTracking().AsQueryable();if(jobCardId.HasValue)q=q.Where(x=>x.JobCardId==jobCardId.Value);
    var rows=await (from r in q join p in db.PartMasters.AsNoTracking() on r.PartMasterId equals p.Id join l in db.InventoryLocations.AsNoTracking() on r.InventoryLocationId equals l.Id
                    join j in db.JobCards.AsNoTracking() on r.JobCardId equals j.Id
                    from t in db.WorkItems.AsNoTracking().Where(x=>x.Id==r.WorkItemId).DefaultIfEmpty()
                    orderby r.RequestedAt descending select new{r.Id,r.RequestNumber,r.JobCardId,jobCard=j.JobCardNumber,r.WorkItemId,task=t==null?"":t.TaskCode,r.PartMasterId,
                        p.PartNumber,p.Description,p.UnitOfMeasure,r.InventoryLocationId,l.LocationCode,r.QuantityRequired,r.QuantityReserved,r.QuantityIssued,r.QuantityReturned,r.QuantityConsumed,
                        r.Status,r.WarrantyCandidate,r.FailedPartDisposition,r.RequestedBy,r.RequestedAt,r.UpdatedAt}).ToListAsync();return Results.Ok(rows);
});
app.MapPost("/api/part-requests", async (PartRequestCreate r, AppDbContext db) =>
{
    if(r.Quantity<=0)return Results.BadRequest(new{message="Quantity must be greater than zero."});
    if(!await db.JobCards.AnyAsync(x=>x.Id==r.JobCardId))return Results.BadRequest(new{message="Job card not found."});
    if(r.WorkItemId.HasValue&&!await db.WorkItems.AnyAsync(x=>x.Id==r.WorkItemId&&x.JobCardId==r.JobCardId))return Results.BadRequest(new{message="Task must belong to the job card."});
    var p=await db.PartMasters.FindAsync(r.PartMasterId);var l=await db.InventoryLocations.FindAsync(r.InventoryLocationId);if(p is null||l is null)return Results.BadRequest(new{message="Part or location not found."});
    var s=await db.PartStocks.FirstOrDefaultAsync(x=>x.PartMasterId==p.Id&&x.InventoryLocationId==l.Id);var available=s==null?0:s.OnHandQty-s.ReservedQty;
    var pr=new PartRequest{RequestNumber=$"PR-{DateTime.UtcNow:yyyyMMdd}-{(await db.PartRequests.CountAsync()+1):D5}",JobCardId=r.JobCardId,WorkItemId=r.WorkItemId,PartMasterId=p.Id,InventoryLocationId=l.Id,
        QuantityRequired=r.Quantity,Status=available>=r.Quantity?"Requested":"Awaiting Stock",WarrantyCandidate=r.WarrantyCandidate,FailedPartDisposition=r.FailedPartDisposition,RequestedBy=r.RequestedBy};
    db.PartRequests.Add(pr);Audit(db,"REQUEST","PartRequest",pr.Id,$"{pr.RequestNumber} {p.PartNumber} x {r.Quantity}",r.RequestedBy);await db.SaveChangesAsync();return Results.Ok(pr);
});
app.MapPost("/api/part-requests/{id:guid}/reserve", async (Guid id, QuantityAction r, AppDbContext db) =>
{
    var pr=await db.PartRequests.FindAsync(id);if(pr is null)return Results.NotFound();var p=await db.PartMasters.FindAsync(pr.PartMasterId);var s=await db.PartStocks.FirstOrDefaultAsync(x=>x.PartMasterId==pr.PartMasterId&&x.InventoryLocationId==pr.InventoryLocationId);
    var qty=r.Quantity<=0?pr.QuantityRequired-pr.QuantityReserved:r.Quantity;if(qty<=0)return Results.BadRequest(new{message="Nothing to reserve."});if(s is null||s.OnHandQty-s.ReservedQty<qty)return Results.Conflict(new{message="Insufficient available stock. Receive/replenish stock first."});
    s.ReservedQty+=qty;pr.QuantityReserved+=qty;pr.Status=pr.QuantityReserved>=pr.QuantityRequired?"Reserved":"Partially Reserved";pr.UpdatedAt=DateTime.UtcNow;
    db.PartTransactions.Add(new PartTransaction{PartRequestId=pr.Id,PartMasterId=pr.PartMasterId,InventoryLocationId=pr.InventoryLocationId,JobCardId=pr.JobCardId,WorkItemId=pr.WorkItemId,PartNumber=p!.PartNumber,PartDescription=p.Description,TransactionType="Reserve",Quantity=qty,PerformedBy=r.User});
    Audit(db,"RESERVE","PartRequest",pr.Id,$"{p.PartNumber} x {qty}",r.User);await db.SaveChangesAsync();return Results.Ok(pr);
});
app.MapPost("/api/part-requests/{id:guid}/issue", async (Guid id, QuantityAction r, AppDbContext db) =>
{
    var pr=await db.PartRequests.FindAsync(id);if(pr is null)return Results.NotFound();var p=await db.PartMasters.FindAsync(pr.PartMasterId);var s=await db.PartStocks.FirstOrDefaultAsync(x=>x.PartMasterId==pr.PartMasterId&&x.InventoryLocationId==pr.InventoryLocationId);if(s is null)return Results.Conflict(new{message="No stock record."});
    var remaining=pr.QuantityRequired-pr.QuantityIssued;var qty=r.Quantity<=0?remaining:r.Quantity;if(qty<=0||qty>remaining)return Results.BadRequest(new{message="Invalid issue quantity."});if(pr.QuantityReserved-pr.QuantityIssued<qty)return Results.Conflict(new{message="Reserve the quantity before issue."});if(s.OnHandQty<qty)return Results.Conflict(new{message="Insufficient on-hand stock."});
    s.OnHandQty-=qty;s.ReservedQty=Math.Max(0,s.ReservedQty-qty);pr.QuantityIssued+=qty;pr.Status=pr.QuantityIssued>=pr.QuantityRequired?"Issued":"Partially Issued";pr.UpdatedAt=DateTime.UtcNow;
    db.PartTransactions.Add(new PartTransaction{PartRequestId=pr.Id,PartMasterId=pr.PartMasterId,InventoryLocationId=pr.InventoryLocationId,JobCardId=pr.JobCardId,WorkItemId=pr.WorkItemId,PartNumber=p!.PartNumber,PartDescription=p.Description,TransactionType="Issue",Quantity=qty,PerformedBy=r.User});
    Audit(db,"ISSUE","PartRequest",pr.Id,$"{p.PartNumber} x {qty}",r.User);await db.SaveChangesAsync();return Results.Ok(pr);
});
app.MapPost("/api/part-requests/{id:guid}/return", async (Guid id, QuantityAction r, AppDbContext db) =>
{
    var pr=await db.PartRequests.FindAsync(id);if(pr is null)return Results.NotFound();var p=await db.PartMasters.FindAsync(pr.PartMasterId);var s=await db.PartStocks.FirstOrDefaultAsync(x=>x.PartMasterId==pr.PartMasterId&&x.InventoryLocationId==pr.InventoryLocationId);if(s is null)return Results.Conflict();
    var availableToReturn=pr.QuantityIssued-pr.QuantityReturned-pr.QuantityConsumed;var qty=r.Quantity<=0?availableToReturn:r.Quantity;if(qty<=0||qty>availableToReturn)return Results.BadRequest(new{message="Invalid return quantity."});s.OnHandQty+=qty;pr.QuantityReturned+=qty;pr.Status=(pr.QuantityReturned+pr.QuantityConsumed)>=pr.QuantityIssued?"Returned":"Partially Returned";pr.UpdatedAt=DateTime.UtcNow;
    db.PartTransactions.Add(new PartTransaction{PartRequestId=pr.Id,PartMasterId=pr.PartMasterId,InventoryLocationId=pr.InventoryLocationId,JobCardId=pr.JobCardId,WorkItemId=pr.WorkItemId,PartNumber=p!.PartNumber,PartDescription=p.Description,TransactionType="Return",Quantity=qty,PerformedBy=r.User});Audit(db,"RETURN","PartRequest",pr.Id,$"{p.PartNumber} x {qty}",r.User);await db.SaveChangesAsync();return Results.Ok(pr);
});
app.MapPost("/api/part-requests/{id:guid}/consume", async (Guid id, QuantityAction r, AppDbContext db) =>
{
    var pr=await db.PartRequests.FindAsync(id);if(pr is null)return Results.NotFound();var p=await db.PartMasters.FindAsync(pr.PartMasterId);var available=pr.QuantityIssued-pr.QuantityReturned-pr.QuantityConsumed;var qty=r.Quantity<=0?available:r.Quantity;if(qty<=0||qty>available)return Results.BadRequest(new{message="Invalid consume quantity."});pr.QuantityConsumed+=qty;pr.Status=(pr.QuantityReturned+pr.QuantityConsumed)>=pr.QuantityIssued&&pr.QuantityIssued>=pr.QuantityRequired?"Consumed":"Partially Consumed";pr.UpdatedAt=DateTime.UtcNow;
    db.PartTransactions.Add(new PartTransaction{PartRequestId=pr.Id,PartMasterId=pr.PartMasterId,InventoryLocationId=pr.InventoryLocationId,JobCardId=pr.JobCardId,WorkItemId=pr.WorkItemId,PartNumber=p!.PartNumber,PartDescription=p.Description,TransactionType="Consume",Quantity=qty,PerformedBy=r.User});Audit(db,"CONSUME","PartRequest",pr.Id,$"{p.PartNumber} x {qty}",r.User);await db.SaveChangesAsync();return Results.Ok(pr);
});
app.MapGet("/api/parts/transactions", async (AppDbContext db) => Results.Ok(await db.PartTransactions.AsNoTracking().OrderByDescending(x=>x.TransactionAt).Take(500).ToListAsync()));
app.MapGet("/api/parts", async (AppDbContext db) => Results.Ok(await db.PartTransactions.AsNoTracking().OrderByDescending(x=>x.TransactionAt).Take(200).ToListAsync()));

app.MapPost("/api/labour", async (LabourEntry l, AppDbContext db) =>
{
    if (!await db.JobCards.AnyAsync(x=>x.Id==l.JobCardId)) return Results.BadRequest(new { message="Job card not found." });
    l.Id=Guid.NewGuid(); db.LabourEntries.Add(l); Audit(db,"CREATE","LabourEntry",l.Id,$"{l.Technician} {l.Hours}h");
    await db.SaveChangesAsync(); return Results.Created($"/api/labour/{l.Id}", l);
});

app.MapGet("/api/breakdowns", async (AppDbContext db) =>
{
    var rows=await (from b in db.Breakdowns.AsNoTracking() join v in db.Vehicles.AsNoTracking() on b.VehicleId equals v.Id orderby b.ReportedAt descending
                    select new { b.Id,b.BreakdownNumber,b.VehicleId,vehicle=v.RegistrationNumber,b.Priority,b.Location,b.Complaint,b.TriageDecision,b.DispatchMode,b.Status,b.ReportedAt }).ToListAsync();
    return Results.Ok(rows);
});
app.MapPost("/api/breakdowns", async (BreakdownRequest r, AppDbContext db) =>
{
    var v=await db.Vehicles.FindAsync(r.VehicleId); if(v is null) return Results.BadRequest(new { message="Vehicle not found." });
    var b=new Breakdown { VehicleId=v.Id, BreakdownNumber=$"BD-{DateTime.UtcNow:yyyy}-{(await db.Breakdowns.CountAsync()+1):D6}", Priority=r.Priority, Location=r.Location, Complaint=r.Complaint, TriageDecision=r.TriageDecision, DispatchMode=r.DispatchMode, Status="Reported" };
    v.Status="Breakdown"; db.Breakdowns.Add(b); db.VehicleAvailabilityLedger.Add(new VehicleAvailabilityLedger { VehicleId=v.Id, State="Breakdown", ReasonCode="Breakdown", SourceType="Breakdown", SourceBreakdownId=b.Id });
    Audit(db,"CREATE","Breakdown",b.Id,b.BreakdownNumber); await db.SaveChangesAsync(); return Results.Created($"/api/breakdowns/{b.Id}",b);
});
app.MapPost("/api/breakdowns/{id:guid}/convert", async (Guid id, AppDbContext db) =>
{
    var b=await db.Breakdowns.FindAsync(id); if(b is null) return Results.NotFound();
    if (await db.ServiceEvents.AnyAsync(x=>x.BreakdownId==id)) return Results.Conflict(new { message="Already converted." });
    var e=new ServiceEvent { VehicleId=b.VehicleId, BreakdownId=b.Id, EventNumber=$"SE-{DateTime.UtcNow:yyyy}-{(await db.ServiceEvents.CountAsync()+1):D6}", EventType="Breakdown", Priority=b.Priority, Status="In Progress" };
    var j=new JobCard { ServiceEventId=e.Id, JobCardNumber=$"JC-{DateTime.UtcNow:yyyy}-{(await db.JobCards.CountAsync()+1):D6}", Status="Open", StartedAt=DateTime.UtcNow };
    b.Status="Converted"; db.ServiceEvents.Add(e); db.JobCards.Add(j); Audit(db,"CONVERT","Breakdown",b.Id,e.EventNumber); await db.SaveChangesAsync(); return Results.Ok(new { serviceEvent=e,jobCard=j });
});

app.MapPost("/api/job-cards/{id:guid}/qc", async (Guid id, QcRequest r, AppDbContext db) =>
{
    if (!await db.JobCards.AnyAsync(x=>x.Id==id)) return Results.NotFound();
    var qc=new QcInspection { JobCardId=id, Inspector=r.Inspector, Result=r.Result, RoadTestRequired=r.RoadTestRequired, RoadTestPassed=r.RoadTestPassed, Remarks=r.Remarks, InspectedAt=DateTime.UtcNow };
    db.QcInspections.Add(qc); Audit(db,"QC","JobCard",id,r.Result); await db.SaveChangesAsync(); return Results.Ok(qc);
});

app.MapPost("/api/service-events/{id:guid}/release", async (Guid id, ReleaseRequest r, AppDbContext db) =>
{
    var e=await db.ServiceEvents.FindAsync(id); if(e is null) return Results.NotFound();
    var j=await db.JobCards.FirstOrDefaultAsync(x=>x.ServiceEventId==id); if(j is null) return Results.BadRequest(new { message="Job card not found." });
    if (await db.WorkItems.AnyAsync(x=>x.JobCardId==j.Id && x.Status!="Completed")) return Results.Conflict(new { message="All work items must be completed before release." });
    if (await db.PartRequests.AnyAsync(x=>x.JobCardId==j.Id && x.Status!="Consumed" && x.Status!="Returned" && x.Status!="Cancelled")) return Results.Conflict(new { message="Open parts requests must be resolved before release." });
    var latestQc=await db.QcInspections.Where(x=>x.JobCardId==j.Id).OrderByDescending(x=>x.InspectedAt).FirstOrDefaultAsync();
    if (latestQc is null || latestQc.Result!="Pass" || (latestQc.RoadTestRequired && !latestQc.RoadTestPassed))
        return Results.Conflict(new { message="Passing QC is required before release." });
    var v=await db.Vehicles.FindAsync(e.VehicleId); if(v is null) return Results.BadRequest();
    var rel=new VehicleRelease { ServiceEventId=e.Id, VehicleId=v.Id, ReleaseStatus="Released", ReleasedBy=r.ReleasedBy, ReleasedAt=DateTime.UtcNow, Remarks=r.Remarks };
    e.Status="Closed"; e.ClosedAt=DateTime.UtcNow; j.Status="Completed"; j.CompletedAt=DateTime.UtcNow; v.Status="Available";
    var openLedger=await db.VehicleAvailabilityLedger.Where(x=>x.VehicleId==v.Id && x.EndAt==null).OrderByDescending(x=>x.StartAt).FirstOrDefaultAsync();
    if(openLedger!=null) openLedger.EndAt=DateTime.UtcNow;
    db.VehicleAvailabilityLedger.Add(new VehicleAvailabilityLedger { VehicleId=v.Id, State="Available", StartAt=DateTime.UtcNow, ReasonCode="Released", SourceType="ServiceEvent", SourceServiceEventId=e.Id });
    db.VehicleReleases.Add(rel); Audit(db,"RELEASE","Vehicle",v.Id,e.EventNumber,r.ReleasedBy); await db.SaveChangesAsync(); return Results.Ok(rel);
});

app.MapGet("/api/audit", async (AppDbContext db) => Results.Ok(await db.AuditEvents.AsNoTracking().OrderByDescending(x=>x.OccurredAt).Take(250).ToListAsync()));

app.MapGet("/api/warranty/{vin}", async (string vin, AppDbContext db) =>
{
    var v=await db.Vehicles.FirstOrDefaultAsync(x=>x.Vin==vin || x.RegistrationNumber==vin);
    if(v is null) return Results.NotFound();
    return Results.Ok(await db.WarrantyEntitlements.AsNoTracking().Where(x=>x.VehicleId==v.Id).ToListAsync());
});
app.MapGet("/api/campaigns/open", async (AppDbContext db) => Results.Ok(await db.Campaigns.AsNoTracking().Where(x=>x.Status=="Active").ToListAsync()));
app.MapGet("/api/documents/{vin}", async (string vin, AppDbContext db) =>
{
    var v=await db.Vehicles.FirstOrDefaultAsync(x=>x.Vin==vin || x.RegistrationNumber==vin); if(v is null) return Results.NotFound();
    return Results.Ok(await db.VehicleDocuments.AsNoTracking().Where(x=>x.VehicleId==v.Id).OrderByDescending(x=>x.UploadedAt).ToListAsync());
});
app.MapGet("/api/search", async (string? q, AppDbContext db) =>
{
    var term=(q??"").Trim().ToLower();
    var vehicles=await db.Vehicles.AsNoTracking().Where(x=>term=="" || x.Vin.ToLower().Contains(term) || x.RegistrationNumber.ToLower().Contains(term)).Take(10)
        .Select(x=>new { type="Vehicle",key=x.RegistrationNumber,title=x.Model+" · "+x.Vin,status=x.Status }).ToListAsync();
    return Results.Ok(new { query=q??"", results=vehicles });
});


app.MapGet("/api/vehicle360/{id:guid}", async (Guid id, AppDbContext db) =>
{
    var v=await db.Vehicles.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==id); if(v is null) return Results.NotFound();
    var nextPm=await db.PmObligations.AsNoTracking().Where(x=>x.VehicleId==id && x.Status!="Completed").OrderBy(x=>x.DueDate).ThenBy(x=>x.DueReading).FirstOrDefaultAsync();
    var defects=await db.Defects.CountAsync(x=>x.VehicleId==id && x.Disposition!="Closed");
    var warranty=await db.WarrantyEntitlements.AnyAsync(x=>x.VehicleId==id && x.Status=="Active" && x.EndDate>=DateTime.UtcNow);
    var campaigns=await db.VehicleCampaigns.CountAsync(x=>x.VehicleId==id && x.Status!="Completed");
    var docs=await db.VehicleDocuments.CountAsync(x=>x.VehicleId==id && x.Status=="Active");
    var lastService=await db.ServiceEvents.AsNoTracking().Where(x=>x.VehicleId==id && x.ClosedAt!=null).OrderByDescending(x=>x.ClosedAt).Select(x=>x.ClosedAt).FirstOrDefaultAsync();
    return Results.Ok(new { v.Id,v.Vin,registration=v.RegistrationNumber,v.Model,v.Variant,availability=v.Status,v.OdometerKm,v.OperatingHours,v.BatterySoc,
        nextPm=nextPm==null?"-":(nextPm.DueDate.HasValue?nextPm.DueDate.Value.ToString("dd MMM yyyy"):(nextPm.DueReading?.ToString("0")+" km")),
        openDefects=defects,activeWarranty=warranty,openCampaigns=campaigns,lastService=lastService,documents=docs });
});

app.MapGet("/api/checklists/{jobCardId:guid}", async (Guid jobCardId, AppDbContext db) =>
    Results.Ok(await db.ChecklistExecutions.AsNoTracking().Where(x=>x.JobCardId==jobCardId).OrderBy(x=>x.ItemCode).ToListAsync()));

app.MapPost("/api/checklists/{jobCardId:guid}", async (Guid jobCardId, ChecklistRequest r, AppDbContext db) =>
{
    var job=await db.JobCards.FindAsync(jobCardId);if(job is null)return Results.NotFound();
    var c=new ChecklistExecution{JobCardId=jobCardId,WorkItemId=r.WorkItemId,ChecklistCode=r.ChecklistCode,ItemCode=r.ItemCode,ItemText=r.ItemText,
        Result=r.Result,Remarks=r.Remarks,ExecutedBy=r.ExecutedBy,ExecutedAt=DateTime.UtcNow,IsMandatory=r.IsMandatory};
    db.ChecklistExecutions.Add(c);Audit(db,"EXECUTE","Checklist",c.Id,$"{r.ItemCode}:{r.Result}",r.ExecutedBy);
    Defect? defect=null;
    if(string.Equals(r.Result,"Fail",StringComparison.OrdinalIgnoreCase))
    {
        var evt=await db.ServiceEvents.FindAsync(job.ServiceEventId);
        if(evt!=null){defect=new Defect{VehicleId=evt.VehicleId,JobCardId=jobCardId,WorkItemId=r.WorkItemId,DefectNumber=$"DF-{DateTime.UtcNow:yyyy}-{(await db.Defects.CountAsync()+1):D6}",
            Category="Checklist",Severity=r.DefectSeverity??"Major",Description=r.DefectDescription??r.ItemText,Disposition="Open",FailureCode=r.FailureCode??""};
            db.Defects.Add(defect);Audit(db,"CREATE","Defect",defect.Id,$"{defect.DefectNumber} from checklist");}
    }
    await db.SaveChangesAsync();return Results.Ok(new{checklist=c,defect});
});
app.MapGet("/api/defects", async (AppDbContext db) =>
{
    var rows=await (from d in db.Defects.AsNoTracking() join v in db.Vehicles.AsNoTracking() on d.VehicleId equals v.Id orderby d.ReportedAt descending
                    select new { d.Id,d.DefectNumber,d.VehicleId,vehicle=v.RegistrationNumber,d.JobCardId,d.WorkItemId,d.Category,d.Severity,d.Description,d.Disposition,d.FailureCode,d.RcaSummary,d.ReportedAt,d.ClosedAt }).ToListAsync();
    return Results.Ok(rows);
});
app.MapPost("/api/defects", async (DefectRequest r, AppDbContext db) =>
{
    var j=await db.JobCards.FindAsync(r.JobCardId); if(j is null) return Results.BadRequest(new{message="Job card not found."});
    var e=await db.ServiceEvents.FindAsync(j.ServiceEventId); if(e is null) return Results.BadRequest();
    var d=new Defect{VehicleId=e.VehicleId,JobCardId=j.Id,WorkItemId=r.WorkItemId,DefectNumber=$"DF-{DateTime.UtcNow:yyyy}-{(await db.Defects.CountAsync()+1):D6}",Category=r.Category,Severity=r.Severity,Description=r.Description,Disposition="Open",FailureCode=r.FailureCode};
    db.Defects.Add(d); Audit(db,"CREATE","Defect",d.Id,d.DefectNumber); await db.SaveChangesAsync(); return Results.Created($"/api/defects/{d.Id}",d);
});
app.MapPut("/api/defects/{id:guid}/close", async (Guid id, DefectCloseRequest r, AppDbContext db) =>
{
    var d=await db.Defects.FindAsync(id); if(d is null)return Results.NotFound(); d.Disposition="Closed";d.RcaSummary=r.RcaSummary;d.ClosedAt=DateTime.UtcNow;Audit(db,"CLOSE","Defect",d.Id,r.RcaSummary);await db.SaveChangesAsync();return Results.Ok(d);
});

app.MapGet("/api/technicians", async (AppDbContext db)=>Results.Ok(await db.Technicians.AsNoTracking().OrderBy(x=>x.EmployeeCode).ToListAsync()));
app.MapPost("/api/technicians", async (Technician t, AppDbContext db)=>{t.Id=Guid.NewGuid();db.Technicians.Add(t);Audit(db,"CREATE","Technician",t.Id,t.EmployeeCode);await db.SaveChangesAsync();return Results.Created($"/api/technicians/{t.Id}",t);});

app.MapGet("/api/availability", async (AppDbContext db)=>
{
    var rows=await (from a in db.VehicleAvailabilityLedger.AsNoTracking() join v in db.Vehicles.AsNoTracking() on a.VehicleId equals v.Id orderby a.StartAt descending
                    select new {a.Id,a.VehicleId,vehicle=v.RegistrationNumber,a.State,a.StartAt,a.EndAt,a.ReasonCode,a.SourceType,a.RuleVersion}).Take(300).ToListAsync();
    return Results.Ok(rows);
});
app.MapPost("/api/availability/correct", async (AvailabilityCorrection r, AppDbContext db)=>
{
    var prev=await db.VehicleAvailabilityLedger.FindAsync(r.CorrectsLedgerId); if(prev is null)return Results.NotFound();
    var a=new VehicleAvailabilityLedger{VehicleId=prev.VehicleId,State=r.State,StartAt=r.StartAt,EndAt=r.EndAt,ReasonCode=r.ReasonCode,SourceType="Correction",ChangedBy=r.ChangedBy,RuleVersion="AVL-1.0",CorrectsLedgerId=prev.Id};
    db.VehicleAvailabilityLedger.Add(a);Audit(db,"CORRECT","Availability",a.Id,r.ReasonCode,r.ChangedBy);await db.SaveChangesAsync();return Results.Ok(a);
});

app.MapGet("/api/offhire", async (AppDbContext db)=>
{
    var rows=await (from o in db.OffHireRecords.AsNoTracking() join v in db.Vehicles.AsNoTracking() on o.VehicleId equals v.Id orderby o.StartAt descending
                    select new{o.Id,o.VehicleId,vehicle=v.RegistrationNumber,o.StartAt,o.ExpectedReturnAt,o.EndAt,o.ReasonCode,o.RequestedBy,o.ApprovedBy,o.Status}).ToListAsync();
    return Results.Ok(rows);
});
app.MapPost("/api/offhire", async (OffHireRequest r, AppDbContext db)=>
{
    var v=await db.Vehicles.FindAsync(r.VehicleId);if(v is null)return Results.BadRequest(new{message="Vehicle not found."});
    var o=new OffHireRecord{VehicleId=v.Id,StartAt=r.StartAt,ExpectedReturnAt=r.ExpectedReturnAt,ReasonCode=r.ReasonCode,RequestedBy=r.RequestedBy,Status="Pending Approval"};
    db.OffHireRecords.Add(o);Audit(db,"REQUEST","OffHire",o.Id,r.ReasonCode,r.RequestedBy);await db.SaveChangesAsync();return Results.Ok(o);
});
app.MapPost("/api/offhire/{id:guid}/approve", async (Guid id, ApprovalRequest r, AppDbContext db)=>
{
    var o=await db.OffHireRecords.FindAsync(id);if(o is null)return Results.NotFound();o.Status="Approved";o.ApprovedBy=r.User;
    var v=await db.Vehicles.FindAsync(o.VehicleId);if(v!=null)v.Status="Off-Hire";Audit(db,"APPROVE","OffHire",o.Id,o.ReasonCode,r.User);await db.SaveChangesAsync();return Results.Ok(o);
});
app.MapPost("/api/offhire/{id:guid}/recommission", async (Guid id, RecommissionRequest r, AppDbContext db)=>
{
    var o=await db.OffHireRecords.FindAsync(id);if(o is null)return Results.NotFound();
    var ri=new RecommissioningInspection{OffHireRecordId=o.Id,VehicleId=o.VehicleId,Inspector=r.Inspector,Result=r.Result,Remarks=r.Remarks,InspectedAt=DateTime.UtcNow};
    db.RecommissioningInspections.Add(ri);
    if(r.Result=="Pass"){o.Status="Closed";o.EndAt=DateTime.UtcNow;var v=await db.Vehicles.FindAsync(o.VehicleId);if(v!=null)v.Status="Available";}
    Audit(db,"RECOMMISSION","OffHire",o.Id,r.Result,r.Inspector);await db.SaveChangesAsync();return Results.Ok(ri);
});

app.MapGet("/api/warranty", async (AppDbContext db)=>
{
    var rows=await (from w in db.WarrantyEntitlements.AsNoTracking() join v in db.Vehicles.AsNoTracking() on w.VehicleId equals v.Id orderby w.EndDate
                    select new{w.Id,w.VehicleId,vehicle=v.RegistrationNumber,w.EntitlementType,w.ReferenceNo,w.StartDate,w.EndDate,w.OdometerLimitKm,w.Status,w.CoverageNotes}).ToListAsync();
    return Results.Ok(rows);
});
app.MapPost("/api/warranty", async (WarrantyEntitlement w, AppDbContext db)=>{w.Id=Guid.NewGuid();db.WarrantyEntitlements.Add(w);Audit(db,"CREATE","Warranty",w.Id,w.ReferenceNo);await db.SaveChangesAsync();return Results.Ok(w);});

app.MapGet("/api/campaigns", async (AppDbContext db)=>Results.Ok(await db.Campaigns.AsNoTracking().OrderByDescending(x=>x.EffectiveFrom).ToListAsync()));
app.MapPost("/api/campaigns", async (Campaign c, AppDbContext db)=>{c.Id=Guid.NewGuid();db.Campaigns.Add(c);Audit(db,"CREATE","Campaign",c.Id,c.CampaignCode);await db.SaveChangesAsync();return Results.Ok(c);});
app.MapPost("/api/campaigns/{campaignId:guid}/vehicles/{vehicleId:guid}", async (Guid campaignId,Guid vehicleId,AppDbContext db)=>
{
    if(!await db.Campaigns.AnyAsync(x=>x.Id==campaignId)||!await db.Vehicles.AnyAsync(x=>x.Id==vehicleId))return Results.BadRequest();
    if(await db.VehicleCampaigns.AnyAsync(x=>x.CampaignId==campaignId&&x.VehicleId==vehicleId))return Results.Conflict(new{message="Vehicle already assigned."});
    var vc=new VehicleCampaign{CampaignId=campaignId,VehicleId=vehicleId};db.VehicleCampaigns.Add(vc);Audit(db,"ASSIGN","CampaignVehicle",vc.Id,$"{campaignId}/{vehicleId}");await db.SaveChangesAsync();return Results.Ok(vc);
});

app.MapGet("/api/documents", async (AppDbContext db)=>
{
    var rows=await (from d in db.VehicleDocuments.AsNoTracking() join v in db.Vehicles.AsNoTracking() on d.VehicleId equals v.Id orderby d.UploadedAt descending
                    select new{d.Id,d.VehicleId,vehicle=v.RegistrationNumber,d.DocumentType,d.FileName,d.StorageReference,d.UploadedBy,d.UploadedAt,d.Status}).ToListAsync();
    return Results.Ok(rows);
});
app.MapPost("/api/documents", async (VehicleDocument d, AppDbContext db)=>{d.Id=Guid.NewGuid();d.UploadedAt=DateTime.UtcNow;db.VehicleDocuments.Add(d);Audit(db,"UPLOAD","Document",d.Id,d.FileName,d.UploadedBy);await db.SaveChangesAsync();return Results.Ok(d);});

app.MapGet("/api/sla", async (AppDbContext db)=>
{
    var rows=await (from s in db.SlaClocks.AsNoTracking() join e in db.ServiceEvents.AsNoTracking() on s.ServiceEventId equals e.Id orderby s.StartedAt descending
                    select new{s.Id,s.ServiceEventId,eventNo=e.EventNumber,e.Priority,s.ClockType,s.StartedAt,s.DueAt,s.PausedAt,s.TotalPausedMinutes,s.PauseReason,s.Status}).ToListAsync();
    return Results.Ok(rows);
});
app.MapPost("/api/sla/{serviceEventId:guid}", async (Guid serviceEventId,SlaStartRequest r,AppDbContext db)=>
{
    if(!await db.ServiceEvents.AnyAsync(x=>x.Id==serviceEventId))return Results.NotFound();
    var s=new SlaClock{ServiceEventId=serviceEventId,ClockType=r.ClockType,StartedAt=DateTime.UtcNow,DueAt=DateTime.UtcNow.AddMinutes(r.TargetMinutes),Status="Running"};
    db.SlaClocks.Add(s);Audit(db,"START","SLA",s.Id,r.ClockType);await db.SaveChangesAsync();return Results.Ok(s);
});
app.MapPost("/api/sla/{id:guid}/pause", async (Guid id,PauseRequest r,AppDbContext db)=>{var s=await db.SlaClocks.FindAsync(id);if(s is null)return Results.NotFound();s.PausedAt=DateTime.UtcNow;s.PauseReason=r.Reason;s.Status="Paused";Audit(db,"PAUSE","SLA",s.Id,r.Reason);await db.SaveChangesAsync();return Results.Ok(s);});
app.MapPost("/api/sla/{id:guid}/resume", async (Guid id,AppDbContext db)=>{var s=await db.SlaClocks.FindAsync(id);if(s is null)return Results.NotFound();if(s.PausedAt.HasValue){s.TotalPausedMinutes+=(int)(DateTime.UtcNow-s.PausedAt.Value).TotalMinutes;s.PausedAt=null;}s.Status="Running";Audit(db,"RESUME","SLA",s.Id,"");await db.SaveChangesAsync();return Results.Ok(s);});

app.MapGet("/api/quality", async (AppDbContext db)=>
{
    var qcs=await db.QcInspections.AsNoTracking().ToListAsync();var total=qcs.Count;var pass=qcs.Count(x=>x.Result=="Pass");
    var repeats=await db.RepeatFailureMatches.CountAsync(x=>x.IsRepeat);var openRca=await db.Defects.CountAsync(x=>x.Disposition!="Closed"&&x.RcaSummary=="");
    var ftf=await db.FirstTimeFixResults.AsNoTracking().ToListAsync();var eligible=ftf.Count(x=>x.Eligible);var ftfPct=eligible==0?100:Math.Round(ftf.Count(x=>x.Eligible&&x.Passed)*100.0/eligible,1);
    return Results.Ok(new{firstTimeFix=ftfPct,repeatFailures=repeats,openRca,qcPass=total==0?100:Math.Round(pass*100.0/total,1)});
});

app.MapFallback(async context =>
{
    var env=context.RequestServices.GetRequiredService<IWebHostEnvironment>();
    var webRoot=env.WebRootPath ?? Path.Combine(env.ContentRootPath,"wwwroot");
    var indexPath=Path.Combine(webRoot,"index.html");
    if(!File.Exists(indexPath)){ context.Response.StatusCode=500; await context.Response.WriteAsync("Angular UI missing."); return; }
    context.Response.ContentType="text/html; charset=utf-8"; await context.Response.SendFileAsync(indexPath);
});
app.Run();

record PmRequest(Guid VehicleId, string PlanCode, string TriggerType, DateTime? DueDate, decimal? DueReading);
record AppointmentRequest(Guid VehicleId, Guid? PmObligationId, string SourceType, string SourceReference, DateTime StartAt, string ServiceCentre, string Bay,
    Guid? TechnicianId, string AppointmentType, string Priority, string Reason, decimal PlannedHours, string CreatedBy);
record WorkItemRequest(string WorkType, string Description, decimal? StandardRepairHours, bool RequiresQc, bool RequiresHvAuthorization);
record StatusRequest(string Status);
record BreakdownRequest(Guid VehicleId, string Priority, string Location, string Complaint, string TriageDecision, string DispatchMode);
record QcRequest(string Inspector, string Result, bool RoadTestRequired, bool RoadTestPassed, string Remarks);
record ReleaseRequest(string ReleasedBy, string Remarks);

record ChecklistRequest(Guid? WorkItemId,string ChecklistCode,string ItemCode,string ItemText,string Result,string Remarks,string ExecutedBy,bool IsMandatory,
    string? DefectSeverity,string? DefectDescription,string? FailureCode);
record DefectRequest(Guid JobCardId,Guid? WorkItemId,string Category,string Severity,string Description,string FailureCode);
record DefectCloseRequest(string RcaSummary);
record AvailabilityCorrection(Guid CorrectsLedgerId,string State,DateTime StartAt,DateTime? EndAt,string ReasonCode,string ChangedBy);
record OffHireRequest(Guid VehicleId,DateTime StartAt,DateTime? ExpectedReturnAt,string ReasonCode,string RequestedBy);
record ApprovalRequest(string User);
record RecommissionRequest(string Inspector,string Result,string Remarks);
record SlaStartRequest(string ClockType,int TargetMinutes);
record PauseRequest(string Reason);

record TaskRequest(Guid JobCardId,string WorkType,string Description,Guid? AssignedToTechnicianId,DateTime? PlannedStartAt,DateTime? DueAt,string Priority,
    Guid? DependencyTaskId,decimal? EstimatedHours,bool RequiresQc,bool RequiresHvAuthorization);
record TaskStatusRequest(string Status,decimal? ActualHours,string? CompletionRemarks,string? EvidenceReference);
record TaskAssignRequest(Guid TechnicianId);

record PartMasterRequest(string PartNumber,string Description,string Category,string UnitOfMeasure,string ManufacturerPartNumber,bool IsSerialized,bool IsWarrantyReturnable,decimal ReorderLevel,decimal ReorderQuantity);
record StockMovementRequest(Guid PartMasterId,Guid InventoryLocationId,decimal Quantity,string User);
record PartRequestCreate(Guid JobCardId,Guid? WorkItemId,Guid PartMasterId,Guid InventoryLocationId,decimal Quantity,bool WarrantyCandidate,string FailedPartDisposition,string RequestedBy);
record QuantityAction(decimal Quantity,string User);
