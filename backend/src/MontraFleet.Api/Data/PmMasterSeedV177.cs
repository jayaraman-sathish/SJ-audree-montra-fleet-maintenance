using Microsoft.EntityFrameworkCore;
using MontraFleet.Api.Data;
using MontraFleet.Api.Models;

namespace MontraFleet.Api.Data;

public static class PmMasterSeedV177
{
    private sealed record LevelSeed(string Code, decimal Usage, string Trigger, string Unit, int CalendarMonths);
    private sealed record TaskSeed(string Section, string Code, string Name, string DefaultAction, string Specification, string Severity, int SortOrder, string[] Actions);

    public static async Task SeedAsync(AppDbContext db)
    {
        async Task<VehicleVariantMaster> EnsureVariantExact(VehicleModelMaster model, string exactCode, string name, string[] aliases, string configuration = "")
        {
            var row = await db.VehicleVariantMasters.FirstOrDefaultAsync(x => x.VehicleModelMasterId == model.Id && x.VariantCode == exactCode);
            if (row is null && aliases.Length > 0)
                row = await db.VehicleVariantMasters.FirstOrDefaultAsync(x => x.VehicleModelMasterId == model.Id && aliases.Contains(x.VariantCode));
            if (row is null)
            {
                row = new VehicleVariantMaster { VehicleModelMasterId = model.Id, VariantCode = exactCode, Name = name, Configuration = configuration, IsActive = true };
                db.VehicleVariantMasters.Add(row);
            }
            else
            {
                row.VariantCode = exactCode; row.Name = name; row.Configuration = string.IsNullOrWhiteSpace(configuration) ? row.Configuration : configuration; row.IsActive = true;
            }
            await db.SaveChangesAsync();
            return row;
        }

        string InferUnit(string specification)
        {
            var s = specification ?? string.Empty;
            if (s.Contains("°C")) return "°C";
            if (s.Contains("%")) return "%";
            if (s.Contains("psi", StringComparison.OrdinalIgnoreCase)) return "psi";
            if (s.Contains("bar", StringComparison.OrdinalIgnoreCase)) return "bar";
            if (s.Contains("mm", StringComparison.OrdinalIgnoreCase)) return "mm";
            if (s.Contains("Nm", StringComparison.OrdinalIgnoreCase)) return "Nm";
            if (s.Contains("V", StringComparison.OrdinalIgnoreCase)) return "V";
            return string.Empty;
        }

        var models = await db.VehicleModelMasters.ToDictionaryAsync(x => x.ModelCode);
        if (!models.TryGetValue("RHINO_5538_EV", out var rhino) || !models.TryGetValue("EVIATOR", out var eviator) ||
            !models.TryGetValue("SUPER_AUTO", out var superAuto) || !models.TryGetValue("SUPER_CARGO", out var superCargo) ||
            !models.TryGetValue("TRACTOR_E27", out var tractor27)) return;

        await EnsureVariantExact(rhino, "RH5538-4X2", "Rhino 5538 EV 4x2", new[]{"RHINO_5538_4X2"}, "4x2");
        await EnsureVariantExact(rhino, "RH5538-6X4", "Rhino 5538 EV 6x4", new[]{"RHINO_5538_6X4"}, "6x4");
        await EnsureVariantExact(eviator, "EVI350-32", "EVIATOR 350 (32 kWh)", new[]{"EVIATOR_350_32"}, "Urban / short-distance");
        await EnsureVariantExact(eviator, "EVICORE-40", "EVIATOR Core (40 kWh)", new[]{"EVIATOR_40"}, "Core EVIATOR");
        await EnsureVariantExact(eviator, "EVI350LP-50", "EVIATOR 350L+ (50 kWh)", new[]{"EVIATOR_350L_50"}, "Long-distance / intercity");
        await EnsureVariantExact(superAuto, "EPL20-STD", "Super Auto ePL 2.0 Standard", Array.Empty<string>(), "ePL 2.0");
        await EnsureVariantExact(superAuto, "EPL20R-STD", "Super Auto ePL 2.0 R Standard", Array.Empty<string>(), "ePL 2.0");
        var superCargoVariant = await EnsureVariantExact(superCargo, "SCARGO-CGO", "Super Cargo", Array.Empty<string>(), "Cargo");
        await EnsureVariantExact(tractor27, "E27-2WD", "E-27 2WD", Array.Empty<string>(), "2WD");
        await EnsureVariantExact(tractor27, "E27-4WD", "E-27 4WD", Array.Empty<string>(), "4WD");

        async Task SeedProgram(string programCode, string programName, string description, VehicleModelMaster model, VehicleVariantMaster? onlyVariant, LevelSeed[] levels, TaskSeed[] tasks)
        {
            var program = await db.MaintenancePrograms.FirstOrDefaultAsync(x => x.ProgramCode == programCode);
            if (program is null)
            {
                program = new MaintenanceProgram { ProgramCode = programCode, Name = programName, Description = description, VehicleModelMasterId = model.Id, VehicleVariantMasterId = onlyVariant?.Id, EffectiveFrom = DateTime.UtcNow, IsActive = true };
                db.MaintenancePrograms.Add(program); await db.SaveChangesAsync();
            }

            var plans = await db.MaintenancePlans.Where(x => x.MaintenanceProgramId == program.Id).OrderBy(x => x.Sequence).ToListAsync();
            if (plans.Count == 0)
            {
                var seq = 0;
                foreach (var l in levels)
                {
                    seq += 10;
                    var plan = new MaintenancePlan { MaintenanceProgramId = program.Id, PlanCode = l.Code, Name = l.Code, Description = $"{l.Usage} {l.Unit} OR {l.CalendarMonths} months, whichever comes first", RecurrenceBasis = "ScheduledDue", Sequence = seq, IsActive = true };
                    db.MaintenancePlans.Add(plan);
                    if (l.Trigger != "NONE") db.MaintenancePlanTriggers.Add(new MaintenancePlanTrigger { MaintenancePlanId = plan.Id, TriggerCode = l.Trigger, IntervalValue = l.Usage, InitialDueValue = l.Usage, UnitCode = l.Unit, WarningValue = 0, ToleranceValue = 0, IsActive = true });
                    if (l.CalendarMonths > 0) db.MaintenancePlanTriggers.Add(new MaintenancePlanTrigger { MaintenancePlanId = plan.Id, TriggerCode = "TIME", IntervalValue = l.CalendarMonths, InitialDueValue = l.CalendarMonths, UnitCode = "MONTH", WarningValue = 0, ToleranceValue = 0, IsActive = true });
                }
                await db.SaveChangesAsync();
                plans = await db.MaintenancePlans.Where(x => x.MaintenanceProgramId == program.Id).OrderBy(x => x.Sequence).ToListAsync();
            }

            var scopedCount = await db.MaintenanceTaskDefinitions.CountAsync(x => x.MaintenanceProgramId == program.Id);
            if (scopedCount == 0)
            {
                var defs = new Dictionary<string, MaintenanceTaskDefinition>(StringComparer.OrdinalIgnoreCase);
                foreach (var t in tasks)
                {
                    var def = new MaintenanceTaskDefinition { MaintenanceProgramId = program.Id, SectionName = t.Section, TaskCode = t.Code, TaskName = t.Name, ActionCode = t.DefaultAction, Specification = t.Specification, Severity = t.Severity, UnitCode = InferUnit(t.Specification), SuggestedIssueCode = string.Empty, SortOrder = t.SortOrder, IsActive = true };
                    db.MaintenanceTaskDefinitions.Add(def); defs[t.Code] = def;
                }
                await db.SaveChangesAsync();
                for (var ti = 0; ti < tasks.Length; ti++)
                {
                    var t = tasks[ti]; var def = defs[t.Code];
                    for (var li = 0; li < levels.Length && li < t.Actions.Length; li++)
                    {
                        var action = t.Actions[li]; if (string.IsNullOrWhiteSpace(action)) continue;
                        var plan = plans.FirstOrDefault(x => x.PlanCode == levels[li].Code); if (plan is null) continue;
                        db.MaintenancePlanMatrixItems.Add(new MaintenancePlanMatrixItem { MaintenancePlanId = plan.Id, MaintenanceTaskDefinitionId = def.Id, ActionCode = action, Sequence = t.SortOrder, IsMandatory = true });
                    }
                }
                await db.SaveChangesAsync();
            }
        }
        var levels0 = new[] {
            new LevelSeed("PM-20K", 20000m, "ODOMETER", "KM", 6),
            new LevelSeed("PM-40K", 40000m, "ODOMETER", "KM", 12),
            new LevelSeed("PM-80K", 80000m, "ODOMETER", "KM", 24),
            new LevelSeed("PM-160K", 160000m, "ODOMETER", "KM", 48),
        };
        var tasks0 = new[] {
            new TaskSeed("Battery & High-Voltage System", "BAT-01", "Battery pack physical condition", "I", "", "Critical", 10, new[]{"I", "I", "I", "I"}),
            new TaskSeed("Battery & High-Voltage System", "BAT-02", "Battery mounting and protection", "T", "", "Critical", 20, new[]{"T", "T", "T", "T"}),
            new TaskSeed("Battery & High-Voltage System", "BAT-03", "HV cables and harness condition", "I", "", "Critical", 30, new[]{"I", "I", "I", "I"}),
            new TaskSeed("Battery & High-Voltage System", "BAT-04", "HV connectors and locking", "I", "", "Critical", 40, new[]{"I", "I", "I", "I"}),
            new TaskSeed("Battery & High-Voltage System", "BAT-05", "Signs of overheating, arcing or damage", "I", "", "Critical", 50, new[]{"I", "I", "I", "I"}),
            new TaskSeed("Battery & High-Voltage System", "BAT-06", "Battery temperature reading", "M", "5–45 °C", "Major", 60, new[]{"M", "M", "M", "M"}),
            new TaskSeed("Battery & High-Voltage System", "BAT-07", "State of charge", "M", "0–100 %", "", 70, new[]{"M", "M", "M", "M"}),
            new TaskSeed("Battery & High-Voltage System", "BAT-08", "State of health", "M", "80–100 %", "Major", 80, new[]{"M", "M", "M", "M"}),
            new TaskSeed("Battery & High-Voltage System", "BAT-09", "Cell imbalance indication", "D", "", "Major", 90, new[]{"D", "D", "D", "D"}),
            new TaskSeed("Battery & High-Voltage System", "BAT-10", "Isolation / insulation status", "D", "", "Critical", 100, new[]{"D", "D", "D", "D"}),
            new TaskSeed("Battery & High-Voltage System", "BAT-11", "Battery cooling system condition", "I", "", "Major", 110, new[]{"", "I", "I", "I"}),
            new TaskSeed("Battery & High-Voltage System", "BAT-12", "Coolant leakage", "I", "", "Major", 120, new[]{"I", "I", "I", "I"}),
            new TaskSeed("Battery & High-Voltage System", "BAT-13", "Battery warning or fault indication", "D", "", "Critical", 130, new[]{"D", "D", "D", "D"}),
            new TaskSeed("Battery & High-Voltage System", "BAT-14", "Battery communication status", "D", "", "Major", 140, new[]{"D", "D", "D", "D"}),
            new TaskSeed("Motor / Inverter / Controller", "MOT-01", "Motor mounting", "T", "", "Major", 150, new[]{"", "T", "T", "T"}),
            new TaskSeed("Motor / Inverter / Controller", "MOT-02", "Abnormal motor noise", "I", "", "Major", 160, new[]{"", "I", "I", "I"}),
            new TaskSeed("Motor / Inverter / Controller", "MOT-03", "Motor vibration", "I", "", "Major", 170, new[]{"", "I", "I", "I"}),
            new TaskSeed("Motor / Inverter / Controller", "MOT-04", "Motor temperature", "M", "0–90 °C", "Major", 180, new[]{"M", "M", "M", "M"}),
            new TaskSeed("Motor / Inverter / Controller", "MOT-05", "Motor electrical connections", "I", "", "Critical", 190, new[]{"I", "I", "I", "I"}),
            new TaskSeed("Motor / Inverter / Controller", "MOT-06", "Inverter / controller condition", "I", "", "Critical", 200, new[]{"I", "I", "I", "I"}),
            new TaskSeed("Motor / Inverter / Controller", "MOT-07", "Cooling connections and leakage", "I", "", "Major", 210, new[]{"", "I", "I", "I"}),
            new TaskSeed("Motor / Inverter / Controller", "MOT-08", "Fault indication / DTC review", "D", "", "Major", 220, new[]{"D", "D", "D", "D"}),
            new TaskSeed("Motor / Inverter / Controller", "MOT-09", "Regenerative braking functional check", "F", "", "Critical", 230, new[]{"F", "F", "F", "F"}),
            new TaskSeed("Charging System", "CHG-01", "Charge inlet condition", "I", "", "Major", 240, new[]{"", "I", "I", "I"}),
            new TaskSeed("Charging System", "CHG-02", "Charge connector condition", "I", "", "Major", 250, new[]{"", "I", "I", "I"}),
            new TaskSeed("Charging System", "CHG-03", "Connector pins", "T", "", "Major", 260, new[]{"", "T", "T", "T"}),
            new TaskSeed("Charging System", "CHG-04", "Locking mechanism", "I", "", "Major", 270, new[]{"", "I", "I", "I"}),
            new TaskSeed("Charging System", "CHG-05", "Charging communication", "D", "", "Major", 280, new[]{"D", "D", "D", "D"}),
            new TaskSeed("Charging System", "CHG-06", "Charging functional check", "F", "", "Major", 290, new[]{"F", "F", "F", "F"}),
            new TaskSeed("Charging System", "CHG-07", "Signs of overheating or burning", "I", "", "Critical", 300, new[]{"I", "I", "I", "I"}),
            new TaskSeed("Charging System", "CHG-08", "Protective caps and covers", "I", "", "Minor", 310, new[]{"", "", "I", "I"}),
            new TaskSeed("Low-Voltage Electrical", "LV-01", "12 V auxiliary battery condition", "I", "", "Major", 320, new[]{"", "I", "I", "I"}),
            new TaskSeed("Low-Voltage Electrical", "LV-02", "Battery terminals", "I", "", "Minor", 330, new[]{"", "", "I", "I"}),
            new TaskSeed("Low-Voltage Electrical", "LV-03", "Headlamps", "I", "", "Major", 340, new[]{"I", "I", "I", "I"}),
            new TaskSeed("Low-Voltage Electrical", "LV-04", "Tail lamps", "I", "", "Major", 350, new[]{"I", "I", "I", "I"}),
            new TaskSeed("Low-Voltage Electrical", "LV-05", "Indicators and hazard lights", "I", "", "Major", 360, new[]{"I", "I", "I", "I"}),
            new TaskSeed("Low-Voltage Electrical", "LV-06", "Horn", "I", "", "Major", 370, new[]{"I", "I", "I", "I"}),
            new TaskSeed("Low-Voltage Electrical", "LV-07", "Instrument cluster", "I", "", "Major", 380, new[]{"", "I", "I", "I"}),
            new TaskSeed("Low-Voltage Electrical", "LV-08", "Warning lamps", "D", "", "Major", 390, new[]{"D", "D", "D", "D"}),
            new TaskSeed("Low-Voltage Electrical", "LV-09", "Visible wiring harness condition", "I", "", "Major", 400, new[]{"", "I", "I", "I"}),
            new TaskSeed("Low-Voltage Electrical", "LV-10", "Fuse and relay condition", "I", "", "Minor", 410, new[]{"", "", "I", "I"}),
            new TaskSeed("Motor & AMT", "AMT-01", "Gearbox oil level and condition", "I", "", "Major", 420, new[]{"", "I", "I", "I"}),
            new TaskSeed("Motor & AMT", "AMT-02", "Gearbox leakage", "I", "", "Major", 430, new[]{"", "I", "I", "I"}),
            new TaskSeed("Motor & AMT", "AMT-03", "AMT operation", "F", "", "Critical", 440, new[]{"F", "F", "F", "F"}),
            new TaskSeed("Motor & AMT", "AMT-04", "Gear-shift function", "I", "", "Critical", 450, new[]{"I", "I", "I", "I"}),
            new TaskSeed("Motor & AMT", "AMT-05", "Transmission mounting", "T", "", "Major", 460, new[]{"", "T", "T", "T"}),
            new TaskSeed("Axles / Driveline", "AXL-01", "Front axle", "I", "", "Major", 470, new[]{"", "I", "I", "I"}),
            new TaskSeed("Axles / Driveline", "AXL-02", "Rear axle(s)", "I", "", "Major", 480, new[]{"", "I", "I", "I"}),
            new TaskSeed("Axles / Driveline", "AXL-03", "Differential", "I", "", "Major", 490, new[]{"", "I", "I", "I"}),
            new TaskSeed("Axles / Driveline", "AXL-04", "Hub and bearing", "I", "", "Critical", 500, new[]{"I", "I", "I", "I"}),
            new TaskSeed("Axles / Driveline", "AXL-05", "Leakage", "I", "", "Major", 510, new[]{"", "I", "I", "I"}),
            new TaskSeed("Axles / Driveline", "AXL-06", "Driveline play", "I", "", "Major", 520, new[]{"", "I", "I", "I"}),
            new TaskSeed("Air / Brake System", "BRK-01", "Brake lining / pad thickness", "M", "5–30 mm", "Critical", 530, new[]{"M", "M", "M", "M"}),
            new TaskSeed("Air / Brake System", "BRK-02", "Brake chamber", "I", "", "Critical", 540, new[]{"I", "I", "I", "I"}),
            new TaskSeed("Air / Brake System", "BRK-03", "Air pressure build-up", "M", "7–12 bar", "Critical", 550, new[]{"M", "M", "M", "M"}),
            new TaskSeed("Air / Brake System", "BRK-04", "Air leakage", "I", "", "Critical", 560, new[]{"I", "I", "I", "I"}),
            new TaskSeed("Air / Brake System", "BRK-05", "Hoses and valves", "I", "", "Critical", 570, new[]{"I", "I", "I", "I"}),
            new TaskSeed("Air / Brake System", "BRK-06", "Parking brake", "I", "", "Critical", 580, new[]{"I", "I", "I", "I"}),
            new TaskSeed("Air / Brake System", "BRK-07", "ABS/EBS warning indication", "D", "", "Critical", 590, new[]{"D", "D", "D", "D"}),
            new TaskSeed("Air / Brake System", "BRK-08", "Brake balance functional check", "F", "", "Critical", 600, new[]{"F", "F", "F", "F"}),
            new TaskSeed("Tyres / Chassis / Coupling", "TYR-01", "Tyre pressure — front left", "M", "90–130 psi", "Major", 610, new[]{"M", "M", "M", "M"}),
            new TaskSeed("Tyres / Chassis / Coupling", "TYR-02", "Tyre pressure — front right", "M", "90–130 psi", "Major", 620, new[]{"M", "M", "M", "M"}),
            new TaskSeed("Tyres / Chassis / Coupling", "TYR-03", "Tread depth — minimum across axles", "M", "3–20 mm", "Critical", 630, new[]{"M", "M", "M", "M"}),
            new TaskSeed("Tyres / Chassis / Coupling", "TYR-04", "Abnormal or uneven wear", "I", "", "Major", 640, new[]{"", "I", "I", "I"}),
            new TaskSeed("Tyres / Chassis / Coupling", "TYR-05", "Wheel alignment and fasteners", "T", "", "Critical", 650, new[]{"T", "T", "T", "T"}),
            new TaskSeed("Tyres / Chassis / Coupling", "TYR-06", "Frame cracks or deformation", "I", "", "Critical", 660, new[]{"I", "I", "I", "I"}),
            new TaskSeed("Tyres / Chassis / Coupling", "TYR-07", "Fifth wheel / kingpin coupling", "I", "", "Critical", 670, new[]{"I", "I", "I", "I"}),
            new TaskSeed("Tyres / Chassis / Coupling", "TYR-08", "Trailer electrical and air connections", "I", "", "Critical", 680, new[]{"I", "I", "I", "I"}),
            new TaskSeed("Cabin & Safety", "CAB-01", "Steering", "I", "", "Critical", 690, new[]{"I", "I", "I", "I"}),
            new TaskSeed("Cabin & Safety", "CAB-02", "Seat and seat belt", "I", "", "Critical", 700, new[]{"I", "I", "I", "I"}),
            new TaskSeed("Cabin & Safety", "CAB-03", "Mirrors", "I", "", "Major", 710, new[]{"I", "I", "I", "I"}),
            new TaskSeed("Cabin & Safety", "CAB-04", "Windscreen and wipers", "I", "", "Major", 720, new[]{"", "I", "I", "I"}),
            new TaskSeed("Cabin & Safety", "CAB-05", "HVAC", "I", "", "Minor", 730, new[]{"", "", "I", "I"}),
            new TaskSeed("Cabin & Safety", "CAB-06", "Mandatory emergency equipment", "I", "", "Major", 740, new[]{"I", "I", "I", "I"}),
        };
        await SeedProgram("MONTRA-RHINO-MHCV", "Rhino MHCV Standard Maintenance Program", "Rhino 5538 EV — RH5538-4X2, RH5538-6X4. Imported from Montra_Service_Schedule-js.xlsx.", rhino, null, levels0, tasks0);

        var levels1 = new[] {
            new LevelSeed("PM-10K", 10000m, "ODOMETER", "KM", 6),
            new LevelSeed("PM-20K", 20000m, "ODOMETER", "KM", 12),
            new LevelSeed("PM-40K", 40000m, "ODOMETER", "KM", 24),
            new LevelSeed("PM-80K", 80000m, "ODOMETER", "KM", 48),
        };
        var tasks1 = new[] {
            new TaskSeed("Battery & High-Voltage System", "BAT-01", "Battery pack physical condition", "I", "", "Critical", 10, new[]{"I", "I", "I", "I"}),
            new TaskSeed("Battery & High-Voltage System", "BAT-02", "Battery mounting and protection", "T", "", "Critical", 20, new[]{"T", "T", "T", "T"}),
            new TaskSeed("Battery & High-Voltage System", "BAT-03", "HV cables and harness condition", "I", "", "Critical", 30, new[]{"I", "I", "I", "I"}),
            new TaskSeed("Battery & High-Voltage System", "BAT-04", "HV connectors and locking", "I", "", "Critical", 40, new[]{"I", "I", "I", "I"}),
            new TaskSeed("Battery & High-Voltage System", "BAT-05", "Signs of overheating, arcing or damage", "I", "", "Critical", 50, new[]{"I", "I", "I", "I"}),
            new TaskSeed("Battery & High-Voltage System", "BAT-06", "Battery temperature reading", "M", "5–45 °C", "Major", 60, new[]{"M", "M", "M", "M"}),
            new TaskSeed("Battery & High-Voltage System", "BAT-07", "State of charge", "M", "0–100 %", "", 70, new[]{"M", "M", "M", "M"}),
            new TaskSeed("Battery & High-Voltage System", "BAT-08", "State of health", "M", "80–100 %", "Major", 80, new[]{"M", "M", "M", "M"}),
            new TaskSeed("Battery & High-Voltage System", "BAT-09", "Cell imbalance indication", "D", "", "Major", 90, new[]{"D", "D", "D", "D"}),
            new TaskSeed("Battery & High-Voltage System", "BAT-10", "Isolation / insulation status", "D", "", "Critical", 100, new[]{"D", "D", "D", "D"}),
            new TaskSeed("Battery & High-Voltage System", "BAT-11", "Battery cooling system condition", "I", "", "Major", 110, new[]{"", "I", "I", "I"}),
            new TaskSeed("Battery & High-Voltage System", "BAT-12", "Coolant leakage", "I", "", "Major", 120, new[]{"I", "I", "I", "I"}),
            new TaskSeed("Battery & High-Voltage System", "BAT-13", "Battery warning or fault indication", "D", "", "Critical", 130, new[]{"D", "D", "D", "D"}),
            new TaskSeed("Battery & High-Voltage System", "BAT-14", "Battery communication status", "D", "", "Major", 140, new[]{"D", "D", "D", "D"}),
            new TaskSeed("Motor / Inverter / Controller", "MOT-01", "Motor mounting", "T", "", "Major", 150, new[]{"", "T", "T", "T"}),
            new TaskSeed("Motor / Inverter / Controller", "MOT-02", "Abnormal motor noise", "I", "", "Major", 160, new[]{"", "I", "I", "I"}),
            new TaskSeed("Motor / Inverter / Controller", "MOT-03", "Motor vibration", "I", "", "Major", 170, new[]{"", "I", "I", "I"}),
            new TaskSeed("Motor / Inverter / Controller", "MOT-04", "Motor temperature", "M", "0–90 °C", "Major", 180, new[]{"M", "M", "M", "M"}),
            new TaskSeed("Motor / Inverter / Controller", "MOT-05", "Motor electrical connections", "I", "", "Critical", 190, new[]{"I", "I", "I", "I"}),
            new TaskSeed("Motor / Inverter / Controller", "MOT-06", "Inverter / controller condition", "I", "", "Critical", 200, new[]{"I", "I", "I", "I"}),
            new TaskSeed("Motor / Inverter / Controller", "MOT-07", "Cooling connections and leakage", "I", "", "Major", 210, new[]{"", "I", "I", "I"}),
            new TaskSeed("Motor / Inverter / Controller", "MOT-08", "Fault indication / DTC review", "D", "", "Major", 220, new[]{"D", "D", "D", "D"}),
            new TaskSeed("Motor / Inverter / Controller", "MOT-09", "Regenerative braking functional check", "F", "", "Critical", 230, new[]{"F", "F", "F", "F"}),
            new TaskSeed("Charging System", "CHG-01", "Charge inlet condition", "I", "", "Major", 240, new[]{"", "I", "I", "I"}),
            new TaskSeed("Charging System", "CHG-02", "Charge connector condition", "I", "", "Major", 250, new[]{"", "I", "I", "I"}),
            new TaskSeed("Charging System", "CHG-03", "Connector pins", "T", "", "Major", 260, new[]{"", "T", "T", "T"}),
            new TaskSeed("Charging System", "CHG-04", "Locking mechanism", "I", "", "Major", 270, new[]{"", "I", "I", "I"}),
            new TaskSeed("Charging System", "CHG-05", "Charging communication", "D", "", "Major", 280, new[]{"D", "D", "D", "D"}),
            new TaskSeed("Charging System", "CHG-06", "Charging functional check", "F", "", "Major", 290, new[]{"F", "F", "F", "F"}),
            new TaskSeed("Charging System", "CHG-07", "Signs of overheating or burning", "I", "", "Critical", 300, new[]{"I", "I", "I", "I"}),
            new TaskSeed("Charging System", "CHG-08", "Protective caps and covers", "I", "", "Minor", 310, new[]{"", "", "I", "I"}),
            new TaskSeed("Low-Voltage Electrical", "LV-01", "12 V auxiliary battery condition", "I", "", "Major", 320, new[]{"", "I", "I", "I"}),
            new TaskSeed("Low-Voltage Electrical", "LV-02", "Battery terminals", "I", "", "Minor", 330, new[]{"", "", "I", "I"}),
            new TaskSeed("Low-Voltage Electrical", "LV-03", "Headlamps", "I", "", "Major", 340, new[]{"I", "I", "I", "I"}),
            new TaskSeed("Low-Voltage Electrical", "LV-04", "Tail lamps", "I", "", "Major", 350, new[]{"I", "I", "I", "I"}),
            new TaskSeed("Low-Voltage Electrical", "LV-05", "Indicators and hazard lights", "I", "", "Major", 360, new[]{"I", "I", "I", "I"}),
            new TaskSeed("Low-Voltage Electrical", "LV-06", "Horn", "I", "", "Major", 370, new[]{"I", "I", "I", "I"}),
            new TaskSeed("Low-Voltage Electrical", "LV-07", "Instrument cluster", "I", "", "Major", 380, new[]{"", "I", "I", "I"}),
            new TaskSeed("Low-Voltage Electrical", "LV-08", "Warning lamps", "D", "", "Major", 390, new[]{"D", "D", "D", "D"}),
            new TaskSeed("Low-Voltage Electrical", "LV-09", "Visible wiring harness condition", "I", "", "Major", 400, new[]{"", "I", "I", "I"}),
            new TaskSeed("Low-Voltage Electrical", "LV-10", "Fuse and relay condition", "I", "", "Minor", 410, new[]{"", "", "I", "I"}),
            new TaskSeed("Brakes", "BRK-01", "Front brake condition", "I", "", "Critical", 420, new[]{"I", "I", "I", "I"}),
            new TaskSeed("Brakes", "BRK-02", "Rear brake condition", "I", "", "Critical", 430, new[]{"I", "I", "I", "I"}),
            new TaskSeed("Brakes", "BRK-03", "Brake fluid level and leakage", "I", "", "Critical", 440, new[]{"I", "I", "I", "I"}),
            new TaskSeed("Brakes", "BRK-04", "Service brake operation", "F", "", "Critical", 450, new[]{"F", "F", "F", "F"}),
            new TaskSeed("Brakes", "BRK-05", "Parking brake", "I", "", "Critical", 460, new[]{"I", "I", "I", "I"}),
            new TaskSeed("Steering & Suspension", "STR-01", "Steering free play and operation", "F", "", "Critical", 470, new[]{"F", "F", "F", "F"}),
            new TaskSeed("Steering & Suspension", "STR-02", "Front suspension condition", "I", "", "Major", 480, new[]{"", "I", "I", "I"}),
            new TaskSeed("Steering & Suspension", "STR-03", "Rear suspension condition", "I", "", "Major", 490, new[]{"", "I", "I", "I"}),
            new TaskSeed("Steering & Suspension", "STR-04", "Shock absorber condition", "I", "", "Major", 500, new[]{"", "I", "I", "I"}),
            new TaskSeed("Steering & Suspension", "STR-05", "Mounting points", "T", "", "Major", 510, new[]{"", "T", "T", "T"}),
            new TaskSeed("Tyres & Wheels", "TYR-01", "Tyre pressure", "M", "28–50 psi", "Major", 520, new[]{"M", "M", "M", "M"}),
            new TaskSeed("Tyres & Wheels", "TYR-02", "Tread depth", "M", "2–12 mm", "Critical", 530, new[]{"M", "M", "M", "M"}),
            new TaskSeed("Tyres & Wheels", "TYR-03", "Uneven wear", "I", "", "Major", 540, new[]{"", "I", "I", "I"}),
            new TaskSeed("Tyres & Wheels", "TYR-04", "Sidewall damage", "I", "", "Critical", 550, new[]{"I", "I", "I", "I"}),
            new TaskSeed("Tyres & Wheels", "TYR-05", "Wheel fastener condition", "T", "", "Critical", 560, new[]{"T", "T", "T", "T"}),
            new TaskSeed("Body & Safety", "BOD-01", "Driver seat and mounting", "T", "", "Major", 570, new[]{"", "T", "T", "T"}),
            new TaskSeed("Body & Safety", "BOD-02", "Body panels and doors", "I", "", "Minor", 580, new[]{"", "", "I", "I"}),
            new TaskSeed("Body & Safety", "BOD-03", "Mirrors", "I", "", "Major", 590, new[]{"I", "I", "I", "I"}),
            new TaskSeed("Body & Safety", "BOD-04", "Windscreen", "I", "", "Major", 600, new[]{"", "I", "I", "I"}),
            new TaskSeed("Body & Safety", "BOD-05", "Wiper", "I", "", "Minor", 610, new[]{"", "", "I", "I"}),
        };
        await SeedProgram("MONTRA-EVIATOR-SCV", "EVIATOR SCV Standard Maintenance Program", "EVIATOR — EVI350-32, EVICORE-40, EVI350LP-50. Imported from Montra_Service_Schedule-js.xlsx.", eviator, null, levels1, tasks1);

        var levels2 = new[] {
            new LevelSeed("PM-5K", 5000m, "ODOMETER", "KM", 3),
            new LevelSeed("PM-10K", 10000m, "ODOMETER", "KM", 6),
            new LevelSeed("PM-20K", 20000m, "ODOMETER", "KM", 12),
            new LevelSeed("PM-40K", 40000m, "ODOMETER", "KM", 24),
        };
        var tasks2 = new[] {
            new TaskSeed("Battery & High-Voltage System", "BAT-01", "Battery pack physical condition", "I", "", "Critical", 10, new[]{"I", "I", "I", "I"}),
            new TaskSeed("Battery & High-Voltage System", "BAT-02", "Battery mounting and protection", "T", "", "Critical", 20, new[]{"T", "T", "T", "T"}),
            new TaskSeed("Battery & High-Voltage System", "BAT-03", "HV cables and harness condition", "I", "", "Critical", 30, new[]{"I", "I", "I", "I"}),
            new TaskSeed("Battery & High-Voltage System", "BAT-04", "HV connectors and locking", "I", "", "Critical", 40, new[]{"I", "I", "I", "I"}),
            new TaskSeed("Battery & High-Voltage System", "BAT-05", "Signs of overheating, arcing or damage", "I", "", "Critical", 50, new[]{"I", "I", "I", "I"}),
            new TaskSeed("Battery & High-Voltage System", "BAT-06", "Battery temperature reading", "M", "5–45 °C", "Major", 60, new[]{"M", "M", "M", "M"}),
            new TaskSeed("Battery & High-Voltage System", "BAT-07", "State of charge", "M", "0–100 %", "", 70, new[]{"M", "M", "M", "M"}),
            new TaskSeed("Battery & High-Voltage System", "BAT-08", "State of health", "M", "80–100 %", "Major", 80, new[]{"M", "M", "M", "M"}),
            new TaskSeed("Battery & High-Voltage System", "BAT-09", "Cell imbalance indication", "D", "", "Major", 90, new[]{"D", "D", "D", "D"}),
            new TaskSeed("Battery & High-Voltage System", "BAT-10", "Isolation / insulation status", "D", "", "Critical", 100, new[]{"D", "D", "D", "D"}),
            new TaskSeed("Battery & High-Voltage System", "BAT-11", "Battery cooling system condition", "I", "", "Major", 110, new[]{"", "I", "I", "I"}),
            new TaskSeed("Battery & High-Voltage System", "BAT-12", "Coolant leakage", "I", "", "Major", 120, new[]{"I", "I", "I", "I"}),
            new TaskSeed("Battery & High-Voltage System", "BAT-13", "Battery warning or fault indication", "D", "", "Critical", 130, new[]{"D", "D", "D", "D"}),
            new TaskSeed("Battery & High-Voltage System", "BAT-14", "Battery communication status", "D", "", "Major", 140, new[]{"D", "D", "D", "D"}),
            new TaskSeed("Motor / Inverter / Controller", "MOT-01", "Motor mounting", "T", "", "Major", 150, new[]{"", "T", "T", "T"}),
            new TaskSeed("Motor / Inverter / Controller", "MOT-02", "Abnormal motor noise", "I", "", "Major", 160, new[]{"", "I", "I", "I"}),
            new TaskSeed("Motor / Inverter / Controller", "MOT-03", "Motor vibration", "I", "", "Major", 170, new[]{"", "I", "I", "I"}),
            new TaskSeed("Motor / Inverter / Controller", "MOT-04", "Motor temperature", "M", "0–90 °C", "Major", 180, new[]{"M", "M", "M", "M"}),
            new TaskSeed("Motor / Inverter / Controller", "MOT-05", "Motor electrical connections", "I", "", "Critical", 190, new[]{"I", "I", "I", "I"}),
            new TaskSeed("Motor / Inverter / Controller", "MOT-06", "Inverter / controller condition", "I", "", "Critical", 200, new[]{"I", "I", "I", "I"}),
            new TaskSeed("Motor / Inverter / Controller", "MOT-07", "Cooling connections and leakage", "I", "", "Major", 210, new[]{"", "I", "I", "I"}),
            new TaskSeed("Motor / Inverter / Controller", "MOT-08", "Fault indication / DTC review", "D", "", "Major", 220, new[]{"D", "D", "D", "D"}),
            new TaskSeed("Motor / Inverter / Controller", "MOT-09", "Regenerative braking functional check", "F", "", "Critical", 230, new[]{"F", "F", "F", "F"}),
            new TaskSeed("Charging System", "CHG-01", "Charge inlet condition", "I", "", "Major", 240, new[]{"", "I", "I", "I"}),
            new TaskSeed("Charging System", "CHG-02", "Charge connector condition", "I", "", "Major", 250, new[]{"", "I", "I", "I"}),
            new TaskSeed("Charging System", "CHG-03", "Connector pins", "T", "", "Major", 260, new[]{"", "T", "T", "T"}),
            new TaskSeed("Charging System", "CHG-04", "Locking mechanism", "I", "", "Major", 270, new[]{"", "I", "I", "I"}),
            new TaskSeed("Charging System", "CHG-05", "Charging communication", "D", "", "Major", 280, new[]{"D", "D", "D", "D"}),
            new TaskSeed("Charging System", "CHG-06", "Charging functional check", "F", "", "Major", 290, new[]{"F", "F", "F", "F"}),
            new TaskSeed("Charging System", "CHG-07", "Signs of overheating or burning", "I", "", "Critical", 300, new[]{"I", "I", "I", "I"}),
            new TaskSeed("Charging System", "CHG-08", "Protective caps and covers", "I", "", "Minor", 310, new[]{"", "", "I", "I"}),
            new TaskSeed("Low-Voltage Electrical", "LV-01", "12 V auxiliary battery condition", "I", "", "Major", 320, new[]{"", "I", "I", "I"}),
            new TaskSeed("Low-Voltage Electrical", "LV-02", "Battery terminals", "I", "", "Minor", 330, new[]{"", "", "I", "I"}),
            new TaskSeed("Low-Voltage Electrical", "LV-03", "Headlamps", "I", "", "Major", 340, new[]{"I", "I", "I", "I"}),
            new TaskSeed("Low-Voltage Electrical", "LV-04", "Tail lamps", "I", "", "Major", 350, new[]{"I", "I", "I", "I"}),
            new TaskSeed("Low-Voltage Electrical", "LV-05", "Indicators and hazard lights", "I", "", "Major", 360, new[]{"I", "I", "I", "I"}),
            new TaskSeed("Low-Voltage Electrical", "LV-06", "Horn", "I", "", "Major", 370, new[]{"I", "I", "I", "I"}),
            new TaskSeed("Low-Voltage Electrical", "LV-07", "Instrument cluster", "I", "", "Major", 380, new[]{"", "I", "I", "I"}),
            new TaskSeed("Low-Voltage Electrical", "LV-08", "Warning lamps", "D", "", "Major", 390, new[]{"D", "D", "D", "D"}),
            new TaskSeed("Low-Voltage Electrical", "LV-09", "Visible wiring harness condition", "I", "", "Major", 400, new[]{"", "I", "I", "I"}),
            new TaskSeed("Low-Voltage Electrical", "LV-10", "Fuse and relay condition", "I", "", "Minor", 410, new[]{"", "", "I", "I"}),
            new TaskSeed("Brakes", "BRK-01", "Front brake condition", "I", "", "Critical", 420, new[]{"I", "I", "I", "I"}),
            new TaskSeed("Brakes", "BRK-02", "Rear brake condition", "I", "", "Critical", 430, new[]{"I", "I", "I", "I"}),
            new TaskSeed("Brakes", "BRK-03", "Brake fluid level and leakage", "I", "", "Critical", 440, new[]{"I", "I", "I", "I"}),
            new TaskSeed("Brakes", "BRK-04", "Service brake operation", "F", "", "Critical", 450, new[]{"F", "F", "F", "F"}),
            new TaskSeed("Brakes", "BRK-05", "Parking brake", "I", "", "Critical", 460, new[]{"I", "I", "I", "I"}),
            new TaskSeed("Steering & Suspension", "STR-01", "Steering free play and operation", "F", "", "Critical", 470, new[]{"F", "F", "F", "F"}),
            new TaskSeed("Steering & Suspension", "STR-02", "Front suspension condition", "I", "", "Major", 480, new[]{"", "I", "I", "I"}),
            new TaskSeed("Steering & Suspension", "STR-03", "Rear suspension condition", "I", "", "Major", 490, new[]{"", "I", "I", "I"}),
            new TaskSeed("Steering & Suspension", "STR-04", "Shock absorber condition", "I", "", "Major", 500, new[]{"", "I", "I", "I"}),
            new TaskSeed("Steering & Suspension", "STR-05", "Mounting points", "T", "", "Major", 510, new[]{"", "T", "T", "T"}),
            new TaskSeed("Tyres & Wheels", "TYR-01", "Tyre pressure", "M", "28–50 psi", "Major", 520, new[]{"M", "M", "M", "M"}),
            new TaskSeed("Tyres & Wheels", "TYR-02", "Tread depth", "M", "2–12 mm", "Critical", 530, new[]{"M", "M", "M", "M"}),
            new TaskSeed("Tyres & Wheels", "TYR-03", "Uneven wear", "I", "", "Major", 540, new[]{"", "I", "I", "I"}),
            new TaskSeed("Tyres & Wheels", "TYR-04", "Sidewall damage", "I", "", "Critical", 550, new[]{"I", "I", "I", "I"}),
            new TaskSeed("Tyres & Wheels", "TYR-05", "Wheel fastener condition", "T", "", "Critical", 560, new[]{"T", "T", "T", "T"}),
            new TaskSeed("Body & Safety", "BOD-01", "Driver seat and mounting", "T", "", "Major", 570, new[]{"", "T", "T", "T"}),
            new TaskSeed("Body & Safety", "BOD-02", "Body panels and doors", "I", "", "Minor", 580, new[]{"", "", "I", "I"}),
            new TaskSeed("Body & Safety", "BOD-03", "Mirrors", "I", "", "Major", 590, new[]{"I", "I", "I", "I"}),
            new TaskSeed("Body & Safety", "BOD-04", "Windscreen", "I", "", "Major", 600, new[]{"", "I", "I", "I"}),
            new TaskSeed("Body & Safety", "BOD-05", "Wiper", "I", "", "Minor", 610, new[]{"", "", "I", "I"}),
        };
        await SeedProgram("MONTRA-SUPER-AUTO-3W", "Super Auto 3W Standard Maintenance Program", "Super Auto ePL 2.0 — EPL20-STD, EPL20R-STD. Uses the Super Auto 3W service ladder and matrix from Montra_Service_Schedule-js.xlsx.", superAuto, null, levels2, tasks2);

        var levels3 = new[] {
            new LevelSeed("PM-5K", 5000m, "ODOMETER", "KM", 3),
            new LevelSeed("PM-10K", 10000m, "ODOMETER", "KM", 6),
            new LevelSeed("PM-20K", 20000m, "ODOMETER", "KM", 12),
            new LevelSeed("PM-40K", 40000m, "ODOMETER", "KM", 24),
        };
        var tasks3 = new[] {
            new TaskSeed("Battery & High-Voltage System", "BAT-01", "Battery pack physical condition", "I", "", "Critical", 10, new[]{"I", "I", "I", "I"}),
            new TaskSeed("Battery & High-Voltage System", "BAT-02", "Battery mounting and protection", "T", "", "Critical", 20, new[]{"T", "T", "T", "T"}),
            new TaskSeed("Battery & High-Voltage System", "BAT-03", "HV cables and harness condition", "I", "", "Critical", 30, new[]{"I", "I", "I", "I"}),
            new TaskSeed("Battery & High-Voltage System", "BAT-04", "HV connectors and locking", "I", "", "Critical", 40, new[]{"I", "I", "I", "I"}),
            new TaskSeed("Battery & High-Voltage System", "BAT-05", "Signs of overheating, arcing or damage", "I", "", "Critical", 50, new[]{"I", "I", "I", "I"}),
            new TaskSeed("Battery & High-Voltage System", "BAT-06", "Battery temperature reading", "M", "5–45 °C", "Major", 60, new[]{"M", "M", "M", "M"}),
            new TaskSeed("Battery & High-Voltage System", "BAT-07", "State of charge", "M", "0–100 %", "", 70, new[]{"M", "M", "M", "M"}),
            new TaskSeed("Battery & High-Voltage System", "BAT-08", "State of health", "M", "80–100 %", "Major", 80, new[]{"M", "M", "M", "M"}),
            new TaskSeed("Battery & High-Voltage System", "BAT-09", "Cell imbalance indication", "D", "", "Major", 90, new[]{"D", "D", "D", "D"}),
            new TaskSeed("Battery & High-Voltage System", "BAT-10", "Isolation / insulation status", "D", "", "Critical", 100, new[]{"D", "D", "D", "D"}),
            new TaskSeed("Battery & High-Voltage System", "BAT-11", "Battery cooling system condition", "I", "", "Major", 110, new[]{"", "I", "I", "I"}),
            new TaskSeed("Battery & High-Voltage System", "BAT-12", "Coolant leakage", "I", "", "Major", 120, new[]{"I", "I", "I", "I"}),
            new TaskSeed("Battery & High-Voltage System", "BAT-13", "Battery warning or fault indication", "D", "", "Critical", 130, new[]{"D", "D", "D", "D"}),
            new TaskSeed("Battery & High-Voltage System", "BAT-14", "Battery communication status", "D", "", "Major", 140, new[]{"D", "D", "D", "D"}),
            new TaskSeed("Motor / Inverter / Controller", "MOT-01", "Motor mounting", "T", "", "Major", 150, new[]{"", "T", "T", "T"}),
            new TaskSeed("Motor / Inverter / Controller", "MOT-02", "Abnormal motor noise", "I", "", "Major", 160, new[]{"", "I", "I", "I"}),
            new TaskSeed("Motor / Inverter / Controller", "MOT-03", "Motor vibration", "I", "", "Major", 170, new[]{"", "I", "I", "I"}),
            new TaskSeed("Motor / Inverter / Controller", "MOT-04", "Motor temperature", "M", "0–90 °C", "Major", 180, new[]{"M", "M", "M", "M"}),
            new TaskSeed("Motor / Inverter / Controller", "MOT-05", "Motor electrical connections", "I", "", "Critical", 190, new[]{"I", "I", "I", "I"}),
            new TaskSeed("Motor / Inverter / Controller", "MOT-06", "Inverter / controller condition", "I", "", "Critical", 200, new[]{"I", "I", "I", "I"}),
            new TaskSeed("Motor / Inverter / Controller", "MOT-07", "Cooling connections and leakage", "I", "", "Major", 210, new[]{"", "I", "I", "I"}),
            new TaskSeed("Motor / Inverter / Controller", "MOT-08", "Fault indication / DTC review", "D", "", "Major", 220, new[]{"D", "D", "D", "D"}),
            new TaskSeed("Motor / Inverter / Controller", "MOT-09", "Regenerative braking functional check", "F", "", "Critical", 230, new[]{"F", "F", "F", "F"}),
            new TaskSeed("Charging System", "CHG-01", "Charge inlet condition", "I", "", "Major", 240, new[]{"", "I", "I", "I"}),
            new TaskSeed("Charging System", "CHG-02", "Charge connector condition", "I", "", "Major", 250, new[]{"", "I", "I", "I"}),
            new TaskSeed("Charging System", "CHG-03", "Connector pins", "T", "", "Major", 260, new[]{"", "T", "T", "T"}),
            new TaskSeed("Charging System", "CHG-04", "Locking mechanism", "I", "", "Major", 270, new[]{"", "I", "I", "I"}),
            new TaskSeed("Charging System", "CHG-05", "Charging communication", "D", "", "Major", 280, new[]{"D", "D", "D", "D"}),
            new TaskSeed("Charging System", "CHG-06", "Charging functional check", "F", "", "Major", 290, new[]{"F", "F", "F", "F"}),
            new TaskSeed("Charging System", "CHG-07", "Signs of overheating or burning", "I", "", "Critical", 300, new[]{"I", "I", "I", "I"}),
            new TaskSeed("Charging System", "CHG-08", "Protective caps and covers", "I", "", "Minor", 310, new[]{"", "", "I", "I"}),
            new TaskSeed("Low-Voltage Electrical", "LV-01", "12 V auxiliary battery condition", "I", "", "Major", 320, new[]{"", "I", "I", "I"}),
            new TaskSeed("Low-Voltage Electrical", "LV-02", "Battery terminals", "I", "", "Minor", 330, new[]{"", "", "I", "I"}),
            new TaskSeed("Low-Voltage Electrical", "LV-03", "Headlamps", "I", "", "Major", 340, new[]{"I", "I", "I", "I"}),
            new TaskSeed("Low-Voltage Electrical", "LV-04", "Tail lamps", "I", "", "Major", 350, new[]{"I", "I", "I", "I"}),
            new TaskSeed("Low-Voltage Electrical", "LV-05", "Indicators and hazard lights", "I", "", "Major", 360, new[]{"I", "I", "I", "I"}),
            new TaskSeed("Low-Voltage Electrical", "LV-06", "Horn", "I", "", "Major", 370, new[]{"I", "I", "I", "I"}),
            new TaskSeed("Low-Voltage Electrical", "LV-07", "Instrument cluster", "I", "", "Major", 380, new[]{"", "I", "I", "I"}),
            new TaskSeed("Low-Voltage Electrical", "LV-08", "Warning lamps", "D", "", "Major", 390, new[]{"D", "D", "D", "D"}),
            new TaskSeed("Low-Voltage Electrical", "LV-09", "Visible wiring harness condition", "I", "", "Major", 400, new[]{"", "I", "I", "I"}),
            new TaskSeed("Low-Voltage Electrical", "LV-10", "Fuse and relay condition", "I", "", "Minor", 410, new[]{"", "", "I", "I"}),
            new TaskSeed("Brakes", "BRK-01", "Front brake condition", "I", "", "Critical", 420, new[]{"I", "I", "I", "I"}),
            new TaskSeed("Brakes", "BRK-02", "Rear brake condition", "I", "", "Critical", 430, new[]{"I", "I", "I", "I"}),
            new TaskSeed("Brakes", "BRK-03", "Brake fluid level and leakage", "I", "", "Critical", 440, new[]{"I", "I", "I", "I"}),
            new TaskSeed("Brakes", "BRK-04", "Service brake operation", "F", "", "Critical", 450, new[]{"F", "F", "F", "F"}),
            new TaskSeed("Brakes", "BRK-05", "Parking brake", "I", "", "Critical", 460, new[]{"I", "I", "I", "I"}),
            new TaskSeed("Steering & Suspension", "STR-01", "Steering free play and operation", "F", "", "Critical", 470, new[]{"F", "F", "F", "F"}),
            new TaskSeed("Steering & Suspension", "STR-02", "Front suspension condition", "I", "", "Major", 480, new[]{"", "I", "I", "I"}),
            new TaskSeed("Steering & Suspension", "STR-03", "Rear suspension condition", "I", "", "Major", 490, new[]{"", "I", "I", "I"}),
            new TaskSeed("Steering & Suspension", "STR-04", "Shock absorber condition", "I", "", "Major", 500, new[]{"", "I", "I", "I"}),
            new TaskSeed("Steering & Suspension", "STR-05", "Mounting points", "T", "", "Major", 510, new[]{"", "T", "T", "T"}),
            new TaskSeed("Tyres & Wheels", "TYR-01", "Tyre pressure", "M", "28–50 psi", "Major", 520, new[]{"M", "M", "M", "M"}),
            new TaskSeed("Tyres & Wheels", "TYR-02", "Tread depth", "M", "2–12 mm", "Critical", 530, new[]{"M", "M", "M", "M"}),
            new TaskSeed("Tyres & Wheels", "TYR-03", "Uneven wear", "I", "", "Major", 540, new[]{"", "I", "I", "I"}),
            new TaskSeed("Tyres & Wheels", "TYR-04", "Sidewall damage", "I", "", "Critical", 550, new[]{"I", "I", "I", "I"}),
            new TaskSeed("Tyres & Wheels", "TYR-05", "Wheel fastener condition", "T", "", "Critical", 560, new[]{"T", "T", "T", "T"}),
            new TaskSeed("Body & Safety", "BOD-01", "Driver seat and mounting", "T", "", "Major", 570, new[]{"", "T", "T", "T"}),
            new TaskSeed("Body & Safety", "BOD-02", "Body panels and doors", "I", "", "Minor", 580, new[]{"", "", "I", "I"}),
            new TaskSeed("Body & Safety", "BOD-03", "Mirrors", "I", "", "Major", 590, new[]{"I", "I", "I", "I"}),
            new TaskSeed("Body & Safety", "BOD-04", "Windscreen", "I", "", "Major", 600, new[]{"", "I", "I", "I"}),
            new TaskSeed("Body & Safety", "BOD-05", "Wiper", "I", "", "Minor", 610, new[]{"", "", "I", "I"}),
        };
        await SeedProgram("MONTRA-SUPER-CARGO-3W", "Super Cargo Standard Maintenance Program", "Super Cargo — SCARGO-CGO. Uses the Super Auto 3W service ladder and matrix from Montra_Service_Schedule-js.xlsx.", superCargo, superCargoVariant, levels3, tasks3);

        var levels4 = new[] {
            new LevelSeed("PM-250H", 250m, "OPERATING_HOURS", "HOUR", 6),
            new LevelSeed("PM-500H", 500m, "OPERATING_HOURS", "HOUR", 12),
            new LevelSeed("PM-1000H", 1000m, "OPERATING_HOURS", "HOUR", 24),
            new LevelSeed("PM-2000H", 2000m, "OPERATING_HOURS", "HOUR", 48),
        };
        var tasks4 = new[] {
            new TaskSeed("Battery & High-Voltage System", "BAT-01", "Battery pack physical condition", "I", "", "Critical", 10, new[]{"I", "I", "I", "I"}),
            new TaskSeed("Battery & High-Voltage System", "BAT-02", "Battery mounting and protection", "T", "", "Critical", 20, new[]{"T", "T", "T", "T"}),
            new TaskSeed("Battery & High-Voltage System", "BAT-03", "HV cables and harness condition", "I", "", "Critical", 30, new[]{"I", "I", "I", "I"}),
            new TaskSeed("Battery & High-Voltage System", "BAT-04", "HV connectors and locking", "I", "", "Critical", 40, new[]{"I", "I", "I", "I"}),
            new TaskSeed("Battery & High-Voltage System", "BAT-05", "Signs of overheating, arcing or damage", "I", "", "Critical", 50, new[]{"I", "I", "I", "I"}),
            new TaskSeed("Battery & High-Voltage System", "BAT-06", "Battery temperature reading", "M", "5–45 °C", "Major", 60, new[]{"M", "M", "M", "M"}),
            new TaskSeed("Battery & High-Voltage System", "BAT-07", "State of charge", "M", "0–100 %", "", 70, new[]{"M", "M", "M", "M"}),
            new TaskSeed("Battery & High-Voltage System", "BAT-08", "State of health", "M", "80–100 %", "Major", 80, new[]{"M", "M", "M", "M"}),
            new TaskSeed("Battery & High-Voltage System", "BAT-09", "Cell imbalance indication", "D", "", "Major", 90, new[]{"D", "D", "D", "D"}),
            new TaskSeed("Battery & High-Voltage System", "BAT-10", "Isolation / insulation status", "D", "", "Critical", 100, new[]{"D", "D", "D", "D"}),
            new TaskSeed("Battery & High-Voltage System", "BAT-11", "Battery cooling system condition", "I", "", "Major", 110, new[]{"", "I", "I", "I"}),
            new TaskSeed("Battery & High-Voltage System", "BAT-12", "Coolant leakage", "I", "", "Major", 120, new[]{"I", "I", "I", "I"}),
            new TaskSeed("Battery & High-Voltage System", "BAT-13", "Battery warning or fault indication", "D", "", "Critical", 130, new[]{"D", "D", "D", "D"}),
            new TaskSeed("Battery & High-Voltage System", "BAT-14", "Battery communication status", "D", "", "Major", 140, new[]{"D", "D", "D", "D"}),
            new TaskSeed("Motor / Inverter / Controller", "MOT-01", "Motor mounting", "T", "", "Major", 150, new[]{"", "T", "T", "T"}),
            new TaskSeed("Motor / Inverter / Controller", "MOT-02", "Abnormal motor noise", "I", "", "Major", 160, new[]{"", "I", "I", "I"}),
            new TaskSeed("Motor / Inverter / Controller", "MOT-03", "Motor vibration", "I", "", "Major", 170, new[]{"", "I", "I", "I"}),
            new TaskSeed("Motor / Inverter / Controller", "MOT-04", "Motor temperature", "M", "0–90 °C", "Major", 180, new[]{"M", "M", "M", "M"}),
            new TaskSeed("Motor / Inverter / Controller", "MOT-05", "Motor electrical connections", "I", "", "Critical", 190, new[]{"I", "I", "I", "I"}),
            new TaskSeed("Motor / Inverter / Controller", "MOT-06", "Inverter / controller condition", "I", "", "Critical", 200, new[]{"I", "I", "I", "I"}),
            new TaskSeed("Motor / Inverter / Controller", "MOT-07", "Cooling connections and leakage", "I", "", "Major", 210, new[]{"", "I", "I", "I"}),
            new TaskSeed("Motor / Inverter / Controller", "MOT-08", "Fault indication / DTC review", "D", "", "Major", 220, new[]{"D", "D", "D", "D"}),
            new TaskSeed("Motor / Inverter / Controller", "MOT-09", "Regenerative braking functional check", "F", "", "Critical", 230, new[]{"F", "F", "F", "F"}),
            new TaskSeed("Charging System", "CHG-01", "Charge inlet condition", "I", "", "Major", 240, new[]{"", "I", "I", "I"}),
            new TaskSeed("Charging System", "CHG-02", "Charge connector condition", "I", "", "Major", 250, new[]{"", "I", "I", "I"}),
            new TaskSeed("Charging System", "CHG-03", "Connector pins", "T", "", "Major", 260, new[]{"", "T", "T", "T"}),
            new TaskSeed("Charging System", "CHG-04", "Locking mechanism", "I", "", "Major", 270, new[]{"", "I", "I", "I"}),
            new TaskSeed("Charging System", "CHG-05", "Charging communication", "D", "", "Major", 280, new[]{"D", "D", "D", "D"}),
            new TaskSeed("Charging System", "CHG-06", "Charging functional check", "F", "", "Major", 290, new[]{"F", "F", "F", "F"}),
            new TaskSeed("Charging System", "CHG-07", "Signs of overheating or burning", "I", "", "Critical", 300, new[]{"I", "I", "I", "I"}),
            new TaskSeed("Charging System", "CHG-08", "Protective caps and covers", "I", "", "Minor", 310, new[]{"", "", "I", "I"}),
            new TaskSeed("Low-Voltage Electrical", "LV-01", "12 V auxiliary battery condition", "I", "", "Major", 320, new[]{"", "I", "I", "I"}),
            new TaskSeed("Low-Voltage Electrical", "LV-02", "Battery terminals", "I", "", "Minor", 330, new[]{"", "", "I", "I"}),
            new TaskSeed("Low-Voltage Electrical", "LV-03", "Headlamps", "I", "", "Major", 340, new[]{"I", "I", "I", "I"}),
            new TaskSeed("Low-Voltage Electrical", "LV-04", "Tail lamps", "I", "", "Major", 350, new[]{"I", "I", "I", "I"}),
            new TaskSeed("Low-Voltage Electrical", "LV-05", "Indicators and hazard lights", "I", "", "Major", 360, new[]{"I", "I", "I", "I"}),
            new TaskSeed("Low-Voltage Electrical", "LV-06", "Horn", "I", "", "Major", 370, new[]{"I", "I", "I", "I"}),
            new TaskSeed("Low-Voltage Electrical", "LV-07", "Instrument cluster", "I", "", "Major", 380, new[]{"", "I", "I", "I"}),
            new TaskSeed("Low-Voltage Electrical", "LV-08", "Warning lamps", "D", "", "Major", 390, new[]{"D", "D", "D", "D"}),
            new TaskSeed("Low-Voltage Electrical", "LV-09", "Visible wiring harness condition", "I", "", "Major", 400, new[]{"", "I", "I", "I"}),
            new TaskSeed("Low-Voltage Electrical", "LV-10", "Fuse and relay condition", "I", "", "Minor", 410, new[]{"", "", "I", "I"}),
            new TaskSeed("Brakes", "BRK-01", "Front brake condition", "I", "", "Critical", 420, new[]{"I", "I", "I", "I"}),
            new TaskSeed("Brakes", "BRK-02", "Rear brake condition", "I", "", "Critical", 430, new[]{"I", "I", "I", "I"}),
            new TaskSeed("Brakes", "BRK-03", "Brake fluid level and leakage", "I", "", "Critical", 440, new[]{"I", "I", "I", "I"}),
            new TaskSeed("Brakes", "BRK-04", "Service brake operation", "F", "", "Critical", 450, new[]{"F", "F", "F", "F"}),
            new TaskSeed("Brakes", "BRK-05", "Parking brake", "I", "", "Critical", 460, new[]{"I", "I", "I", "I"}),
            new TaskSeed("Steering & Suspension", "STR-01", "Steering free play and operation", "F", "", "Critical", 470, new[]{"F", "F", "F", "F"}),
            new TaskSeed("Steering & Suspension", "STR-02", "Front suspension condition", "I", "", "Major", 480, new[]{"", "I", "I", "I"}),
            new TaskSeed("Steering & Suspension", "STR-03", "Rear suspension condition", "I", "", "Major", 490, new[]{"", "I", "I", "I"}),
            new TaskSeed("Steering & Suspension", "STR-04", "Shock absorber condition", "I", "", "Major", 500, new[]{"", "I", "I", "I"}),
            new TaskSeed("Steering & Suspension", "STR-05", "Mounting points", "T", "", "Major", 510, new[]{"", "T", "T", "T"}),
            new TaskSeed("Tyres & Wheels", "TYR-01", "Tyre pressure", "M", "28–50 psi", "Major", 520, new[]{"M", "M", "M", "M"}),
            new TaskSeed("Tyres & Wheels", "TYR-02", "Tread depth", "M", "2–12 mm", "Critical", 530, new[]{"M", "M", "M", "M"}),
            new TaskSeed("Tyres & Wheels", "TYR-03", "Uneven wear", "I", "", "Major", 540, new[]{"", "I", "I", "I"}),
            new TaskSeed("Tyres & Wheels", "TYR-04", "Sidewall damage", "I", "", "Critical", 550, new[]{"I", "I", "I", "I"}),
            new TaskSeed("Tyres & Wheels", "TYR-05", "Wheel fastener condition", "T", "", "Critical", 560, new[]{"T", "T", "T", "T"}),
            new TaskSeed("Transmission", "TRN-01", "Gear selection", "F", "", "Major", 570, new[]{"F", "F", "F", "F"}),
            new TaskSeed("Transmission", "TRN-02", "8F + 2R operation", "F", "", "Major", 580, new[]{"F", "F", "F", "F"}),
            new TaskSeed("Transmission", "TRN-03", "Transmission oil condition", "I", "", "Major", 590, new[]{"", "I", "I", "I"}),
            new TaskSeed("Transmission", "TRN-04", "Leakage", "I", "", "Major", 600, new[]{"", "I", "I", "I"}),
            new TaskSeed("PTO", "PTO-01", "PTO engagement", "F", "", "Critical", 610, new[]{"F", "F", "F", "F"}),
            new TaskSeed("PTO", "PTO-02", "540 RPM mode operation", "F", "", "Major", 620, new[]{"F", "F", "F", "F"}),
            new TaskSeed("PTO", "PTO-03", "1000 RPM mode operation", "F", "", "Major", 630, new[]{"F", "F", "F", "F"}),
            new TaskSeed("PTO", "PTO-04", "PTO shaft condition", "I", "", "Critical", 640, new[]{"I", "I", "I", "I"}),
            new TaskSeed("PTO", "PTO-05", "PTO guard", "I", "", "Critical", 650, new[]{"I", "I", "I", "I"}),
            new TaskSeed("Hydraulics", "HYD-01", "Hydraulic oil level and condition", "I", "", "Major", 660, new[]{"", "I", "I", "I"}),
            new TaskSeed("Hydraulics", "HYD-02", "Leakage", "I", "", "Major", 670, new[]{"", "I", "I", "I"}),
            new TaskSeed("Hydraulics", "HYD-03", "Hoses", "I", "", "Major", 680, new[]{"", "I", "I", "I"}),
            new TaskSeed("Hydraulics", "HYD-04", "Lift arms", "I", "", "Major", 690, new[]{"", "I", "I", "I"}),
            new TaskSeed("Hydraulics", "HYD-05", "Lifting function", "F", "", "Critical", 700, new[]{"F", "F", "F", "F"}),
            new TaskSeed("Implement Attachment", "IMP-01", "Three-point linkage", "T", "", "Critical", 710, new[]{"T", "T", "T", "T"}),
            new TaskSeed("Implement Attachment", "IMP-02", "Top link and pins", "T", "", "Major", 720, new[]{"", "T", "T", "T"}),
            new TaskSeed("Implement Attachment", "IMP-03", "Safety locking", "I", "", "Critical", 730, new[]{"I", "I", "I", "I"}),
        };
        await SeedProgram("MONTRA-E-TRACTOR-E27", "E-Tractor E-27 Standard Maintenance Program", "E-27 — E27-2WD, E27-4WD. Operating hours is the primary usage trigger. Imported from Montra_Service_Schedule-js.xlsx.", tractor27, null, levels4, tasks4);

    }
}
