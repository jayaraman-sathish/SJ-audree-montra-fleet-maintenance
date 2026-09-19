namespace MontraFleet.Api.Models;
public class WarrantyClaim
{
 public Guid Id {get;set;}=Guid.NewGuid(); public Guid WarrantyEntitlementId {get;set;} public Guid JobCardId {get;set;} public Guid? PartRequestId {get;set;}
 public string ClaimNumber {get;set;}=""; public string Description {get;set;}=""; public decimal Amount {get;set;} public string Status {get;set;}="Draft";
 public string CreatedBy {get;set;}=""; public DateTime CreatedAt {get;set;}=DateTime.UtcNow; public string DecisionBy {get;set;}=""; public string DecisionRemarks {get;set;}="";
 public DateTime? DecidedAt {get;set;}
}
