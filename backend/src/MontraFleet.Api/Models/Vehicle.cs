namespace MontraFleet.Api.Models;

public class Vehicle
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Vin { get; set; } = string.Empty;
    public string RegistrationNumber { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Variant { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string DepotName { get; set; } = string.Empty;
    public string Status { get; set; } = "Available";
    public decimal OdometerKm { get; set; }
    public decimal OperatingHours { get; set; }
    public decimal? BatterySoc { get; set; }
    public DateTime? LastTelematicsAtUtc { get; set; }
}
