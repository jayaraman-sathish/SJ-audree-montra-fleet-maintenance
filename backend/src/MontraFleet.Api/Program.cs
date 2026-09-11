using Microsoft.EntityFrameworkCore;
using MontraFleet.Api.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(options => options.AddPolicy("web", policy =>
    policy.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin()));

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Server=localhost;Database=MontraFleetMaintenance;Trusted_Connection=True;TrustServerCertificate=True";
builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlServer(connectionString));

var app = builder.Build();
app.UseCors("web");
if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }

app.MapGet("/api/health", () => Results.Ok(new { status = "ok", service = "MontraFleet.Api" }));
app.MapGet("/api/dashboard/summary", () => Results.Ok(new {
    totalVehicles = 1248,
    inService = 1102,
    underMaintenance = 96,
    offHire = 50,
    appointmentsToday = 18,
    breakdownRequests = 7,
    pmOverdue = 12,
    slaBreaches = 3
}));

app.MapGet("/api/vehicles/{vin}", async (string vin, AppDbContext db) => {
    var vehicle = await db.Vehicles.AsNoTracking().FirstOrDefaultAsync(v => v.Vin == vin);
    return vehicle is null ? Results.NotFound() : Results.Ok(vehicle);
});

app.Run();
