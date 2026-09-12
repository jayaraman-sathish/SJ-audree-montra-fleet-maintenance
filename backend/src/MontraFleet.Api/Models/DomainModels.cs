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
    public DateTime? GeneratedAt { get; set; } = DateTime.UtcNow;
    public Guid? SupersededById { get; set; }
}

public class Appointment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid VehicleId { get; set; }
    public Guid? PmObligationId { get; set; }
    public DateTime StartAt { get; set; }
    public DateTime? EndAt { get; set; }
    public string ServiceCentre { get; set; } = string.Empty;
    public string Bay { get; set; } = string.Empty;
    public string AppointmentType { get; set; } = "PM";
    public decimal PlannedHours { get; set; }
    public string Status { get; set; } = "Scheduled";
}

public class ServiceBay
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ServiceCentre { get; set; } = string.Empty;
    public string BayCode { get; set; } = string.Empty;
    public string BayType { get; set; } = "General";
    public bool IsActive { get; set; } = true;
}

public class Technician
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string EmployeeCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ServiceCentre { get; set; } = string.Empty;
    public string SkillCodes { get; set; } = string.Empty;
    public bool HvAuthorized { get; set; }
    public DateTime? HvAuthorizationValidUntil { get; set; }
    public bool IsActive { get; set; } = true;
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
    public Guid? TechnicianId { get; set; }
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
    public bool RequiresHvAuthorization { get; set; }
}

public class ChecklistExecution
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid JobCardId { get; set; }
    public Guid? WorkItemId { get; set; }
    public string ChecklistCode { get; set; } = string.Empty;
    public string ItemCode { get; set; } = string.Empty;
    public string ItemText { get; set; } = string.Empty;
    public string Result { get; set; } = "Pending";
    public string Remarks { get; set; } = string.Empty;
    public string ExecutedBy { get; set; } = string.Empty;
    public DateTime? ExecutedAt { get; set; }
    public bool IsMandatory { get; set; } = true;
}

public class Defect
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid VehicleId { get; set; }
    public Guid JobCardId { get; set; }
    public Guid? WorkItemId { get; set; }
    public string DefectNumber { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Severity { get; set; } = "Minor";
    public string Description { get; set; } = string.Empty;
    public string Disposition { get; set; } = "Open";
    public string FailureCode { get; set; } = string.Empty;
    public string RcaSummary { get; set; } = string.Empty;
    public DateTime ReportedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ClosedAt { get; set; }
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
    public string AuthorizationStatus { get; set; } = "Not Required";
    public DateTime TransactionAt { get; set; } = DateTime.UtcNow;
}

public class LabourEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid JobCardId { get; set; }
    public Guid? WorkItemId { get; set; }
    public Guid? TechnicianId { get; set; }
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
    public bool RoadTestRequired { get; set; }
    public bool RoadTestPassed { get; set; }
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
    public string RuleVersion { get; set; } = "AVL-1.0";
    public Guid? CorrectsLedgerId { get; set; }
}

public class OffHireRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid VehicleId { get; set; }
    public DateTime StartAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpectedReturnAt { get; set; }
    public DateTime? EndAt { get; set; }
    public string ReasonCode { get; set; } = string.Empty;
    public string EvidenceReference { get; set; } = string.Empty;
    public string RequestedBy { get; set; } = string.Empty;
    public string ApprovedBy { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending Approval";
}

public class RecommissioningInspection
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OffHireRecordId { get; set; }
    public Guid VehicleId { get; set; }
    public string Inspector { get; set; } = string.Empty;
    public string Result { get; set; } = "Pending";
    public string Remarks { get; set; } = string.Empty;
    public DateTime? InspectedAt { get; set; }
}

public class RepeatFailureMatch
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid VehicleId { get; set; }
    public Guid SourceServiceEventId { get; set; }
    public Guid RepeatServiceEventId { get; set; }
    public string MatchKey { get; set; } = string.Empty;
    public int WindowDays { get; set; } = 30;
    public bool IsRepeat { get; set; }
    public DateTime EvaluatedAt { get; set; } = DateTime.UtcNow;
}

public class FirstTimeFixResult
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ServiceEventId { get; set; }
    public Guid VehicleId { get; set; }
    public bool Eligible { get; set; } = true;
    public bool Passed { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime EvaluatedAt { get; set; } = DateTime.UtcNow;
}

public class ApprovalRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string RequestedBy { get; set; } = string.Empty;
    public string ApprovedBy { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ApprovedAt { get; set; }
}

public class AuditEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    public string UserName { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public Guid? EntityId { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
}
public class SlaClock { public Guid Id { get; set; } = Guid.NewGuid(); public Guid ServiceEventId { get; set; } public string ClockType { get; set; } = "Resolution"; public DateTime StartedAt { get; set; } = DateTime.UtcNow; public DateTime? DueAt { get; set; } public DateTime? PausedAt { get; set; } public int TotalPausedMinutes { get; set; } public string PauseReason { get; set; } = string.Empty; public string Status { get; set; } = "Running"; }
public class WarrantyEntitlement { public Guid Id { get; set; } = Guid.NewGuid(); public Guid VehicleId { get; set; } public string EntitlementType { get; set; } = "Vehicle Warranty"; public string ReferenceNo { get; set; } = string.Empty; public DateTime StartDate { get; set; } public DateTime EndDate { get; set; } public decimal? OdometerLimitKm { get; set; } public string Status { get; set; } = "Active"; public string CoverageNotes { get; set; } = string.Empty; }
public class Campaign { public Guid Id { get; set; } = Guid.NewGuid(); public string CampaignCode { get; set; } = string.Empty; public string Title { get; set; } = string.Empty; public string CampaignType { get; set; } = "Service Campaign"; public DateTime EffectiveFrom { get; set; } = DateTime.UtcNow; public DateTime? EffectiveTo { get; set; } public string ApplicabilityRule { get; set; } = string.Empty; public string Status { get; set; } = "Active"; }
public class VehicleCampaign { public Guid Id { get; set; } = Guid.NewGuid(); public Guid CampaignId { get; set; } public Guid VehicleId { get; set; } public string Status { get; set; } = "Open"; public Guid? ServiceEventId { get; set; } public DateTime? CompletedAt { get; set; } }
public class VehicleDocument { public Guid Id { get; set; } = Guid.NewGuid(); public Guid VehicleId { get; set; } public string DocumentType { get; set; } = string.Empty; public string FileName { get; set; } = string.Empty; public string StorageReference { get; set; } = string.Empty; public string UploadedBy { get; set; } = string.Empty; public DateTime UploadedAt { get; set; } = DateTime.UtcNow; public string Status { get; set; } = "Active"; }
public class IntegrationOutbox { public Guid Id { get; set; } = Guid.NewGuid(); public string IntegrationName { get; set; } = string.Empty; public string MessageType { get; set; } = string.Empty; public string EntityType { get; set; } = string.Empty; public Guid? EntityId { get; set; } public string PayloadJson { get; set; } = "{}"; public string Status { get; set; } = "Pending"; public int AttemptCount { get; set; } public DateTime CreatedAt { get; set; } = DateTime.UtcNow; public DateTime? ProcessedAt { get; set; } }
