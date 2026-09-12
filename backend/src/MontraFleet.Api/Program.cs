using Microsoft.EntityFrameworkCore;using MontraFleet.Api.Data;using MontraFleet.Api.Models;
var builder=WebApplication.CreateBuilder(args);builder.Services.AddEndpointsApiExplorer();builder.Services.AddSwaggerGen();builder.Services.AddCors(o=>o.AddPolicy("web",p=>p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));var cs=builder.Configuration.GetConnectionString("DefaultConnection")??"Server=localhost;Database=MontraFleetMaintenance;Trusted_Connection=True;TrustServerCertificate=True";builder.Services.AddDbContext<AppDbContext>(o=>o.UseSqlServer(cs));var app=builder.Build();app.UseCors("web");if(app.Environment.IsDevelopment()){app.UseSwagger();app.UseSwaggerUI();}
app.MapGet("/api/health",()=>Results.Ok(new{status="ok",service="MontraFleet.Api"}));
app.MapGet("/api/dashboard/summary",()=>Results.Ok(new{totalVehicles=1248,inService=1102,underMaintenance=96,offHire=50,appointmentsToday=18,breakdownRequests=7,pmOverdue=12,slaBreaches=3}));
app.MapGet("/api/demo/vehicle-360",()=>Results.Ok(new{vehicle=new{registrationNumber="TN12AB1234",vin="MT7T72036001234",model="Montra eTruck 7T",variant="ExT 7T - Std",odometerKm=48520,operatingHours=3120,batterySoc=78,uptime30Days=96.2},pm=new[]{new{plan="PM-5K",trigger="Odometer",due="50,000 km",status="Due Soon"},new{plan="PM-3M",trigger="Time",due="01 Oct 2026",status="Upcoming"}}}));
app.MapGet("/api/pm/obligations",()=>Results.Ok(new[]{new{vehicle="TN12AB1234",plan="PM-5K",trigger="Odometer",due="50,000 km",status="Due Soon"},new{vehicle="KA05EV7782",plan="PM-3M",trigger="Time",due="10 Sep 2026",status="Overdue"}}));
app.MapPost("/api/appointments",(Appointment a)=>Results.Created($"/api/appointments/{a.Id}",a));
app.MapPost("/api/service-events",(ServiceEvent e)=>Results.Created($"/api/service-events/{e.Id}",e));
app.MapPost("/api/job-cards",(JobCard j)=>Results.Created($"/api/job-cards/{j.Id}",j));
app.MapPost("/api/work-items",(WorkItem w)=>Results.Created($"/api/work-items/{w.Id}",w));
app.Run();
