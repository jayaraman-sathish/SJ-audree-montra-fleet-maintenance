namespace MontraFleet.Api.Models;

public class Vehicle
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Vin { get; set; } = string.Empty;
    public string RegistrationNumber { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Variant { get; set; } = string.Empty;
    public string VehicleTypeCode { get; set; } = string.Empty;
    public string ManufacturerCode { get; set; } = string.Empty;
    public Guid? ModelMasterId { get; set; }
    public Guid? VariantMasterId { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public string MotorNumber { get; set; } = string.Empty;
    public DateTime? PurchaseDate { get; set; }
    public decimal? PurchaseCost { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public string DealerName { get; set; } = string.Empty;
    public DateTime? CommissioningDate { get; set; }
    public DateTime? RegistrationDate { get; set; }
    public DateTime? RegistrationExpiry { get; set; }
    public string InsuranceNumber { get; set; } = string.Empty;
    public DateTime? InsuranceStartDate { get; set; }
    public DateTime? InsuranceExpiryDate { get; set; }
    public DateTime? WarrantyStartDate { get; set; }
    public DateTime? WarrantyExpiryDate { get; set; }
    public DateTime? BatteryWarrantyStartDate { get; set; }
    public DateTime? BatteryWarrantyExpiryDate { get; set; }
    public string DepotCode { get; set; } = string.Empty;
    public string ServiceCentreCode { get; set; } = string.Empty;
    public string CustomerCode { get; set; } = string.Empty;
    public string OwnershipTypeCode { get; set; } = string.Empty;
    public Guid? MaintenanceProgramId { get; set; }
    public string Remarks { get; set; } = string.Empty;
    public string Status { get; set; } = "Available";
    public decimal OdometerKm { get; set; }
    public decimal OperatingHours { get; set; }
    public decimal EnergyKwh { get; set; }
    public decimal? BatterySoc { get; set; }
    public bool IsActive { get; set; } = true;
}

public class PmObligation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid VehicleId { get; set; }
    public Guid? MaintenancePlanId { get; set; }
    public string PlanCode { get; set; } = string.Empty;
    public string TriggerType { get; set; } = string.Empty;
    public DateTime? DueDate { get; set; }
    public decimal? DueReading { get; set; }
    public decimal? DueOperatingHours { get; set; }
    public decimal? DueEnergyKwh { get; set; }
    public string Status { get; set; } = "Upcoming";
    public DateTime? GeneratedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public Guid? SupersededById { get; set; }
}

public class Appointment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string AppointmentNumber { get; set; } = string.Empty;
    public Guid VehicleId { get; set; }
    public Guid? PmObligationId { get; set; }
    public string SourceType { get; set; } = "Manual";
    public string SourceReference { get; set; } = string.Empty;
    public DateTime StartAt { get; set; }
    public DateTime? EndAt { get; set; }
    public string ServiceCentre { get; set; } = string.Empty;
    public string Bay { get; set; } = string.Empty;
    public Guid? TechnicianId { get; set; }
    public string Technician { get; set; } = string.Empty;
    public string AppointmentType { get; set; } = "PM";
    public string Priority { get; set; } = "P3";
    public string Reason { get; set; } = string.Empty;
    public decimal PlannedHours { get; set; }
    public string Status { get; set; } = "Requested";
    public string CreatedBy { get; set; } = "Service User";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
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
    public decimal HourlyRate { get; set; }
    public bool IsActive { get; set; } = true;
}

public class ServiceEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid VehicleId { get; set; }
    public Guid? BreakdownId { get; set; }
    public Guid? PmObligationId { get; set; }
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
    public string TaskCode { get; set; } = string.Empty;
    public string WorkType { get; set; } = "Repair";
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = "Not Started";
    public Guid? AssignedToTechnicianId { get; set; }
    public string AssignedTo { get; set; } = string.Empty;
    public DateTime? PlannedStartAt { get; set; }
    public DateTime? DueAt { get; set; }
    public string Priority { get; set; } = "P3";
    public Guid? DependencyTaskId { get; set; }
    public decimal? EstimatedHours { get; set; }
    public decimal? ActualHours { get; set; }
    public string EvidenceReference { get; set; } = string.Empty;
    public string CompletionRemarks { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
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

public class PartMaster
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string PartNumber { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string UnitOfMeasure { get; set; } = "EA";
    public string ManufacturerPartNumber { get; set; } = string.Empty;
    public bool IsSerialized { get; set; }
    public bool IsWarrantyReturnable { get; set; }
    public decimal ReorderLevel { get; set; }
    public decimal ReorderQuantity { get; set; }
    public decimal StandardCost { get; set; }
    public bool IsActive { get; set; } = true;
}

public class InventoryLocation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string LocationCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ServiceCentre { get; set; } = string.Empty;
    public string Bin { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

public class PartStock
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PartMasterId { get; set; }
    public Guid InventoryLocationId { get; set; }
    public decimal OnHandQty { get; set; }
    public decimal ReservedQty { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class PartRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string RequestNumber { get; set; } = string.Empty;
    public Guid JobCardId { get; set; }
    public Guid? WorkItemId { get; set; }
    public Guid PartMasterId { get; set; }
    public Guid InventoryLocationId { get; set; }
    public decimal QuantityRequired { get; set; }
    public decimal QuantityReserved { get; set; }
    public decimal QuantityIssued { get; set; }
    public decimal QuantityReturned { get; set; }
    public decimal QuantityConsumed { get; set; }
    public string Status { get; set; } = "Requested";
    public bool WarrantyCandidate { get; set; }
    public string FailedPartDisposition { get; set; } = string.Empty;
    public string RequestedBy { get; set; } = "Technician";
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class PartTransaction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? PartRequestId { get; set; }
    public Guid? PartMasterId { get; set; }
    public Guid? InventoryLocationId { get; set; }
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
    public string PerformedBy { get; set; } = "Store User";
    public DateTime TransactionAt { get; set; } = DateTime.UtcNow;
    public decimal UnitCost { get; set; }
    public decimal ExtendedCost { get; set; }
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
    public decimal HourlyRate { get; set; }
    public decimal CostAmount { get; set; }
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


public class MasterOption
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Category { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public class VehicleModelMaster
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ModelCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ManufacturerCode { get; set; } = string.Empty;
    public string VehicleTypeCode { get; set; } = string.Empty;
    public string PowertrainCode { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public decimal? GvwKg { get; set; }
    public decimal? BatteryCapacityKwh { get; set; }
    public bool IsActive { get; set; } = true;
}

public class VehicleVariantMaster
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid VehicleModelMasterId { get; set; }
    public string VariantCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public decimal? GvwKg { get; set; }
    public decimal? PayloadKg { get; set; }
    public decimal? BatteryCapacityKwh { get; set; }
    public decimal? MotorPowerKw { get; set; }
    public decimal? WheelbaseMm { get; set; }
    public string Configuration { get; set; } = string.Empty;
    public DateTime? EffectiveFrom { get; set; }
    public bool IsActive { get; set; } = true;
}


public class ManufacturerMaster
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ManufacturerCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Country { get; set; } = "India";
    public string WebsiteUrl { get; set; } = string.Empty;
    public string ContactPhone { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

public class CustomerMaster
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string CustomerCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string AddressLine1 { get; set; } = string.Empty;
    public string AddressLine2 { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string Country { get; set; } = "India";
    public string Gstin { get; set; } = string.Empty;
    public string ContactPerson { get; set; } = string.Empty;
    public string Mobile { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

public class DepotMaster
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string DepotCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Guid? CustomerMasterId { get; set; }
    public string AddressLine1 { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string ContactPerson { get; set; } = string.Empty;
    public string Mobile { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

public class ServiceCentreMaster
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string CentreCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string CentreType { get; set; } = "Company";
    public string AddressLine1 { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string ContactPerson { get; set; } = string.Empty;
    public string Mobile { get; set; } = string.Empty;
    public string WorkingHours { get; set; } = string.Empty;
    public int BayCount { get; set; }
    public bool IsActive { get; set; } = true;
}

public class MaintenanceProgram
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ProgramCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Guid? VehicleModelMasterId { get; set; }
    public Guid? VehicleVariantMasterId { get; set; }
    public DateTime EffectiveFrom { get; set; } = DateTime.UtcNow;
    public DateTime? EffectiveTo { get; set; }
    public bool IsActive { get; set; } = true;
}

public class MaintenancePlan
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MaintenanceProgramId { get; set; }
    public string PlanCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string RecurrenceBasis { get; set; } = "Completion";
    public int Sequence { get; set; }
    public bool IsActive { get; set; } = true;
}

public class MaintenancePlanTrigger
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MaintenancePlanId { get; set; }
    public string TriggerCode { get; set; } = "ODOMETER";
    public decimal IntervalValue { get; set; }
    public decimal? InitialDueValue { get; set; }
    public string UnitCode { get; set; } = string.Empty;
    public decimal WarningValue { get; set; }
    public decimal ToleranceValue { get; set; }
    public bool IsActive { get; set; } = true;
}

public class MaintenancePlanTask
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MaintenancePlanId { get; set; }
    public Guid ServiceTaskMasterId { get; set; }
    public int Sequence { get; set; }
    public bool IsMandatory { get; set; } = true;
}


public class MaintenanceRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string RequestNumber { get; set; } = string.Empty;
    public Guid VehicleId { get; set; }
    public string SourceType { get; set; } = "Manual";
    public string SourceReference { get; set; } = string.Empty;
    public string RequestType { get; set; } = "Repair";
    public string Priority { get; set; } = "P3";
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = "Open";
    public string RequestedBy { get; set; } = "Fleet User";
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public DateTime? TargetDate { get; set; }
    public Guid? JobCardId { get; set; }
}

public class ServiceTaskMaster
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TaskCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal StandardHours { get; set; }
    public string RequiredSkillCode { get; set; } = string.Empty;
    public bool RequiresHvAuthorization { get; set; }
    public bool RequiresQc { get; set; } = true;
    public string ChecklistCode { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

public class ServiceTaskStandardPart
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ServiceTaskMasterId { get; set; }
    public Guid PartMasterId { get; set; }
    public decimal Quantity { get; set; }
}

public class WorkOrderCost
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid JobCardId { get; set; }
    public Guid? WorkItemId { get; set; }
    public string CostType { get; set; } = "Other";
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string VendorReference { get; set; } = string.Empty;
    public string PostedBy { get; set; } = "Service User";
    public DateTime PostedAt { get; set; } = DateTime.UtcNow;
}
