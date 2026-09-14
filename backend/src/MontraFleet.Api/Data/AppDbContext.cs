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
    public DbSet<PartMaster> PartMasters => Set<PartMaster>();
    public DbSet<InventoryLocation> InventoryLocations => Set<InventoryLocation>();
    public DbSet<PartStock> PartStocks => Set<PartStock>();
    public DbSet<PartRequest> PartRequests => Set<PartRequest>();
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
    public DbSet<SlaClock> SlaClocks => Set<SlaClock>();
    public DbSet<WarrantyEntitlement> WarrantyEntitlements => Set<WarrantyEntitlement>();
    public DbSet<Campaign> Campaigns => Set<Campaign>();
    public DbSet<VehicleCampaign> VehicleCampaigns => Set<VehicleCampaign>();
    public DbSet<VehicleDocument> VehicleDocuments => Set<VehicleDocument>();
    public DbSet<IntegrationOutbox> IntegrationOutbox => Set<IntegrationOutbox>();

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
        m.Entity<PartMaster>().HasIndex(x => x.PartNumber).IsUnique().HasDatabaseName("IX_PartMaster_PartNumber");
        m.Entity<PartMaster>().Property(x => x.ReorderLevel).HasPrecision(18, 3);
        m.Entity<PartMaster>().Property(x => x.ReorderQuantity).HasPrecision(18, 3);
        m.Entity<InventoryLocation>().HasIndex(x => x.LocationCode).IsUnique().HasDatabaseName("IX_InventoryLocation_Code");
        m.Entity<PartStock>().HasIndex(x => new { x.PartMasterId, x.InventoryLocationId }).IsUnique().HasDatabaseName("IX_PartStock_PartLocation");
        m.Entity<PartStock>().Property(x => x.OnHandQty).HasPrecision(18, 3);
        m.Entity<PartStock>().Property(x => x.ReservedQty).HasPrecision(18, 3);
        m.Entity<PartRequest>().HasIndex(x => x.RequestNumber).IsUnique().HasDatabaseName("IX_PartRequest_Number");
        m.Entity<PartRequest>().Property(x => x.QuantityRequired).HasPrecision(18, 3);
        m.Entity<PartRequest>().Property(x => x.QuantityReserved).HasPrecision(18, 3);
        m.Entity<PartRequest>().Property(x => x.QuantityIssued).HasPrecision(18, 3);
        m.Entity<PartRequest>().Property(x => x.QuantityReturned).HasPrecision(18, 3);
        m.Entity<PartRequest>().Property(x => x.QuantityConsumed).HasPrecision(18, 3);
        m.Entity<LabourEntry>().Property(x => x.Hours).HasPrecision(18, 2);
        m.Entity<Appointment>().Property(x => x.PlannedHours).HasPrecision(18, 2);
        m.Entity<Appointment>().HasIndex(x => x.AppointmentNumber).IsUnique();
        m.Entity<WorkItem>().HasIndex(x => x.TaskCode).IsUnique();
        m.Entity<WorkItem>().Property(x => x.EstimatedHours).HasPrecision(18, 2);
        m.Entity<WorkItem>().Property(x => x.ActualHours).HasPrecision(18, 2);
        m.Entity<WarrantyEntitlement>().Property(x => x.OdometerLimitKm).HasPrecision(18, 2);
        m.Entity<Campaign>().HasIndex(x => x.CampaignCode).IsUnique();
        m.Entity<VehicleCampaign>().HasIndex(x => new { x.VehicleId, x.CampaignId }).IsUnique();
        m.Entity<VehicleDocument>().HasIndex(x => new { x.VehicleId, x.DocumentType, x.UploadedAt });
        m.Entity<IntegrationOutbox>().HasIndex(x => new { x.Status, x.CreatedAt });
    }
}
