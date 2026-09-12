namespace MontraFleet.Api.Models;

public class Vehicle
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Vin { get; set; } = string.Empty;
    public string RegistrationNumber { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Variant { get; set; } = string.Empty;
    public string Status { get; set; } = "Available";
    public decimal OdometerKm { get; set; }
    public decimal OperatingHours { get; set; }
    public decimal? BatterySoc { get; set; }
}

public class PmObligation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid VehicleId { get; set; }
    public string PlanCode { get; set; } = string.Empty;
    public string TriggerType { get; set; } = string.Empty;
    public DateTime? DueDate { get; set; }
    public decimal? DueReading { get; set; }
    public string Status { get; set; } = "Upcoming";
}

public class Appointment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid VehicleId { get; set; }
    public Guid? PmObligationId { get; set; }
    public DateTime StartAt { get; set; }
    public string ServiceCentre { get; set; } = string.Empty;
    public string Bay { get; set; } = string.Empty;
    public string Status { get; set; } = "Scheduled";
}

public class ServiceEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid VehicleId { get; set; }
    public Guid? BreakdownId { get; set; }
    public string EventNumber { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string Priority { get; set; } = "P3";
    public string Status { get; set; } = "Open";
    public DateTime OpenedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ClosedAt { get; set; }
}

public class JobCard
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ServiceEventId { get; set; }
    public string JobCardNumber { get; set; } = string.Empty;
    public string Status { get; set; } = "Open";
    public string Bay { get; set; } = string.Empty;
    public string Technician { get; set; } = string.Empty;
    public decimal? StandardRepairHours { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class WorkItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid JobCardId { get; set; }
    public string WorkType { get; set; } = "Repair";
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public decimal? StandardRepairHours { get; set; }
    public bool RequiresQc { get; set; } = true;
}

public class Breakdown
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid VehicleId { get; set; }
    public string BreakdownNumber { get; set; } = string.Empty;
    public string Priority { get; set; } = "P2";
    public string Location { get; set; } = string.Empty;
    public string Complaint { get; set; } = string.Empty;
    public string TriageDecision { get; set; } = "Pending";
    public string DispatchMode { get; set; } = "Workshop";
    public string Status { get; set; } = "Reported";
    public DateTime ReportedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResponseAt { get; set; }
    public DateTime? RestoredAt { get; set; }
}

public class PartTransaction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid JobCardId { get; set; }
    public Guid? WorkItemId { get; set; }
    public string PartNumber { get; set; } = string.Empty;
    public string PartDescription { get; set; } = string.Empty;
    public string TransactionType { get; set; } = "Issue";
    public decimal Quantity { get; set; }
    public string SerialNumber { get; set; } = string.Empty;
    public bool WarrantyCandidate { get; set; }
    public string FailedPartDisposition { get; set; } = string.Empty;
    public DateTime TransactionAt { get; set; } = DateTime.UtcNow;
}

public class LabourEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid JobCardId { get; set; }
    public Guid? WorkItemId { get; set; }
    public string Technician { get; set; } = string.Empty;
    public DateTime StartAt { get; set; }
    public DateTime? EndAt { get; set; }
    public decimal Hours { get; set; }
    public string SkillCode { get; set; } = string.Empty;
}

public class QcInspection
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid JobCardId { get; set; }
    public string Inspector { get; set; } = string.Empty;
    public string Result { get; set; } = "Pending";
    public string Remarks { get; set; } = string.Empty;
    public DateTime? InspectedAt { get; set; }
}

public class VehicleRelease
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ServiceEventId { get; set; }
    public Guid VehicleId { get; set; }
    public string ReleaseStatus { get; set; } = "Pending";
    public string ReleasedBy { get; set; } = string.Empty;
    public DateTime? ReleasedAt { get; set; }
    public string Remarks { get; set; } = string.Empty;
}

public class VehicleAvailabilityLedger
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid VehicleId { get; set; }
    public string State { get; set; } = "Available";
    public DateTime StartAt { get; set; } = DateTime.UtcNow;
    public DateTime? EndAt { get; set; }
    public string ReasonCode { get; set; } = string.Empty;
    public string SourceType { get; set; } = string.Empty;
    public Guid? SourceServiceEventId { get; set; }
    public Guid? SourceBreakdownId { get; set; }
    public string ChangedBy { get; set; } = "System";
}
