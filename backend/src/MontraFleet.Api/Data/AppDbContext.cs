using Microsoft.EntityFrameworkCore;
using MontraFleet.Api.Models;

namespace MontraFleet.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<PmObligation> PmObligations => Set<PmObligation>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<ServiceBay> ServiceBays => Set<ServiceBay>();
    public DbSet<Technician> Technicians => Set<Technician>();
    public DbSet<ServiceEvent> ServiceEvents => Set<ServiceEvent>();
    public DbSet<JobCard> JobCards => Set<JobCard>();
    public DbSet<WorkItem> WorkItems => Set<WorkItem>();
    public DbSet<ChecklistExecution> ChecklistExecutions => Set<ChecklistExecution>();
    public DbSet<Defect> Defects => Set<Defect>();
    public DbSet<Breakdown> Breakdowns => Set<Breakdown>();
    public DbSet<PartTransaction> PartTransactions => Set<PartTransaction>();
    public DbSet<LabourEntry> LabourEntries => Set<LabourEntry>();
    public DbSet<QcInspection> QcInspections => Set<QcInspection>();
    public DbSet<VehicleRelease> VehicleReleases => Set<VehicleRelease>();
    public DbSet<VehicleAvailabilityLedger> VehicleAvailabilityLedger => Set<VehicleAvailabilityLedger>();
    public DbSet<OffHireRecord> OffHireRecords => Set<OffHireRecord>();
    public DbSet<RecommissioningInspection> RecommissioningInspections => Set<RecommissioningInspection>();
    public DbSet<RepeatFailureMatch> RepeatFailureMatches => Set<RepeatFailureMatch>();
    public DbSet<FirstTimeFixResult> FirstTimeFixResults => Set<FirstTimeFixResult>();
    public DbSet<ApprovalRecord> ApprovalRecords => Set<ApprovalRecord>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    protected override void OnModelCreating(ModelBuilder m)
    {
        m.Entity<Vehicle>().HasIndex(x => x.Vin).IsUnique();
        m.Entity<ServiceEvent>().HasIndex(x => x.EventNumber).IsUnique();
        m.Entity<JobCard>().HasIndex(x => x.JobCardNumber).IsUnique();
        m.Entity<Breakdown>().HasIndex(x => x.BreakdownNumber).IsUnique();
        m.Entity<Defect>().HasIndex(x => x.DefectNumber).IsUnique();
        m.Entity<ServiceBay>().HasIndex(x => new { x.ServiceCentre, x.BayCode }).IsUnique();
        m.Entity<Technician>().HasIndex(x => x.EmployeeCode).IsUnique();
        m.Entity<VehicleAvailabilityLedger>().HasIndex(x => new { x.VehicleId, x.StartAt });
        m.Entity<RepeatFailureMatch>().HasIndex(x => new { x.VehicleId, x.MatchKey, x.EvaluatedAt });
        m.Entity<PartTransaction>().Property(x => x.Quantity).HasPrecision(18, 3);
        m.Entity<LabourEntry>().Property(x => x.Hours).HasPrecision(18, 2);
        m.Entity<Appointment>().Property(x => x.PlannedHours).HasPrecision(18, 2);
    }
}
