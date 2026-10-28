using Microsoft.EntityFrameworkCore;
using MontraFleet.Api.Models;

namespace MontraFleet.Api.Data;

/// <summary>
/// Idempotent non-production demo workload. The master-data seeds give the
/// system vehicles, plans and resources; this one gives it a day's work in
/// flight, so every operational screen has content on a fresh database.
///
/// Records are built the same way the live endpoints build them, so statuses,
/// numbering and foreign keys are the ones the UI and dashboard already query.
/// Runs only when no service event exists, and never modifies existing rows.
/// </summary>
public static class DemoWorkflowSeedV1826
{
    public static async Task SeedAsync(AppDbContext db)
    {
        // Any of these means a workload already exists. An earlier run that
        // half-succeeded would otherwise collide on the unique document numbers.
        if (await db.ServiceEvents.AnyAsync()) return;
        if (await db.MaintenanceRequests.AnyAsync()) return;
        if (await db.Breakdowns.AnyAsync()) return;
        if (await db.Appointments.AnyAsync()) return;
        if (await db.PmObligations.AnyAsync()) return;

        // Commit everything or nothing, so a failure never leaves half a workload.
        await using var tx = await db.Database.BeginTransactionAsync();
        var vehicles = await db.Vehicles.Where(v => v.IsActive).OrderBy(v => v.RegistrationNumber).ToListAsync();
        if (vehicles.Count < 8) return;                          // demo fleet not seeded

        var now = DateTime.UtcNow;
        var year = now.Year;
        var techs = await db.Technicians.Where(t => t.IsActive).OrderBy(t => t.EmployeeCode).ToListAsync();
        var bays = await db.ServiceBays.Where(b => b.IsActive).OrderBy(b => b.BayCode).ToListAsync();
        var plan = await db.MaintenancePlans.OrderBy(p => p.Id).FirstOrDefaultAsync();
        Technician? Tech(int i) => techs.Count == 0 ? null : techs[i % techs.Count];
        string Bay(int i) => bays.Count == 0 ? $"Bay {i + 1}" : bays[i % bays.Count].BayCode;

        var seq = await db.ServiceEvents.CountAsync();
        string SeNo() => $"SE-{year}-{++seq:D6}";
        var jcSeq = await db.JobCards.CountAsync();
        string JcNo() => $"JC-{year}-{++jcSeq:D6}";

        // ---------------------------------------------------------------
        // 1. PM obligations across the fleet - overdue, due, due soon
        // ---------------------------------------------------------------
        var pmStates = new[] { "Overdue", "Overdue", "Overdue", "Due", "Due", "Due Soon", "Due Soon", "Upcoming" };
        var obligations = new List<PmObligation>();
        for (var i = 0; i < pmStates.Length && i < vehicles.Count; i++)
        {
            var v = vehicles[i];
            var state = pmStates[i];
            var offsetDays = state switch { "Overdue" => -(7 + i * 5), "Due" => 0, "Due Soon" => 6 + i, _ => 30 + i * 3 };
            var o = new PmObligation
            {
                VehicleId = v.Id,
                MaintenancePlanId = plan?.Id,
                PlanCode = plan?.PlanCode ?? "PM-STD",
                TriggerType = i % 3 == 0 ? "ODOMETER" : "TIME",
                DueDate = now.Date.AddDays(offsetDays),
                DueReading = v.OdometerKm + (state == "Overdue" ? -1200 : 900),
                Status = state,
                GeneratedAt = now.AddDays(-45),
            };
            obligations.Add(o);
            db.PmObligations.Add(o);
        }
        await db.SaveChangesAsync();

        // ---------------------------------------------------------------
        // 2. Maintenance requests - the intake queue
        // ---------------------------------------------------------------
        var requests = new (int Vehicle, string Type, string Priority, string Desc, string Status)[]
        {
            (8,  "Repair",                 "P2", "Intermittent regenerative braking warning on descent", "Open"),
            (9,  "Repair",                 "P3", "Cabin HVAC blowing warm air",                          "Open"),
            (10, "Preventive Maintenance", "P3", "Customer requested service ahead of long haul",        "Open"),
            (11, "Inspection",             "P2", "Tyre wear check after uneven wear reported by driver", "In Progress"),
        };
        var mrSeq = await db.MaintenanceRequests.CountAsync();
        foreach (var r in requests)
        {
            if (r.Vehicle >= vehicles.Count) continue;
            db.MaintenanceRequests.Add(new MaintenanceRequest
            {
                RequestNumber = $"MR-{year}-{++mrSeq:D6}",
                VehicleId = vehicles[r.Vehicle].Id,
                SourceType = "Manual",
                RequestType = r.Type,
                Priority = r.Priority,
                Description = r.Desc,
                Status = r.Status,
                RequestedBy = "Fleet Supervisor",
                RequestedAt = now.AddHours(-6 - mrSeq * 9),
                TargetDate = now.Date.AddDays(2),
            });
        }
        await db.SaveChangesAsync();

        // ---------------------------------------------------------------
        // 3. Breakdowns - two open, one restored
        // ---------------------------------------------------------------
        var bdSeq = await db.Breakdowns.CountAsync();
        var breakdowns = new (int Vehicle, string Priority, string Location, string Complaint, string Status, bool Open)[]
        {
            (12, "P1", "NH-44 near Shamirpet toll", "Vehicle immobilised - no drive, HV fault lamp on", "Dispatched", true),
            (13, "P2", "Customer depot, Medchal",   "Air pressure dropping, parking brake drags",       "Reported",   true),
            (14, "P3", "Workshop yard",             "Charging port flap damaged",                       "Restored",   false),
        };
        foreach (var b in breakdowns)
        {
            if (b.Vehicle >= vehicles.Count) continue;
            var v = vehicles[b.Vehicle];
            var reported = now.AddHours(-(2 + bdSeq * 5));
            var bd = new Breakdown
            {
                VehicleId = v.Id,
                BreakdownNumber = $"BD-{year}-{++bdSeq:D6}",
                Priority = b.Priority,
                Location = b.Location,
                Complaint = b.Complaint,
                TriageDecision = b.Open ? "Workshop" : "Workshop",
                DispatchMode = "Workshop",
                Status = b.Status,
                ReportedAt = reported,
                ResponseAt = reported.AddMinutes(b.Priority == "P1" ? 22 : 95),
                RestoredAt = b.Open ? null : reported.AddHours(6),
            };
            db.Breakdowns.Add(bd);

            if (b.Open)
            {
                var ev = new ServiceEvent
                {
                    VehicleId = v.Id, BreakdownId = bd.Id, EventNumber = SeNo(), EventType = "Breakdown",
                    Priority = b.Priority, Status = "Awaiting Assignment", OpenedAt = reported,
                    OpenedOdometerKm = v.OdometerKm,
                    AssignedSupervisor = "Ravi Kumar", SupervisorAssignedAt = reported.AddMinutes(12),
                };
                db.ServiceEvents.Add(ev);

                var t = Tech(bdSeq);
                var jc = new JobCard
                {
                    ServiceEventId = ev.Id, JobCardNumber = JcNo(), Status = "Open",
                    Bay = Bay(bdSeq), TechnicianId = t?.Id, Technician = t?.Name ?? "",
                };
                db.JobCards.Add(jc);

                db.WorkItems.Add(new WorkItem
                {
                    JobCardId = jc.Id, TaskCode = "DIAG-001", WorkType = "Diagnosis",
                    Description = "Diagnose reported complaint and confirm failure mode",
                    Status = "In Progress", AssignedToTechnicianId = t?.Id, AssignedTo = t?.Name ?? "",
                    Priority = b.Priority, EstimatedHours = 1.5m, RequiresQc = true,
                    RequiresHvAuthorization = b.Priority == "P1", UpdatedAt = now.AddMinutes(-40),
                });

                v.Status = "Breakdown";
                db.VehicleAvailabilityLedger.Add(new VehicleAvailabilityLedger
                {
                    VehicleId = v.Id, State = "Down-Breakdown", StartAt = reported,
                    ReasonCode = "BREAKDOWN", SourceType = "Breakdown", SourceBreakdownId = bd.Id,
                    ChangedBy = "Demo Seed",
                });
            }
        }
        await db.SaveChangesAsync();

        // ---------------------------------------------------------------
        // 4. PM work in flight - one per bottleneck the dashboard reports
        // ---------------------------------------------------------------
        var flows = new (int Vehicle, string Jc, string Bottleneck)[]
        {
            (0, "In Progress", "unassigned"),
            (1, "In Progress", "parts"),
            (3, "Completed",   "qc"),
        };
        var part = await db.PartMasters.OrderBy(p => p.PartNumber).FirstOrDefaultAsync();
        var location = await db.InventoryLocations.OrderBy(l => l.Id).FirstOrDefaultAsync();
        var f = 0;
        foreach (var flow in flows)
        {
            if (flow.Vehicle >= vehicles.Count) continue;
            var v = vehicles[flow.Vehicle];
            var ob = obligations.FirstOrDefault(x => x.VehicleId == v.Id);
            var opened = now.AddHours(-(4 + f * 3));
            var t = Tech(f + 1);

            var ev = new ServiceEvent
            {
                VehicleId = v.Id, PmObligationId = ob?.Id, EventNumber = SeNo(), EventType = "Preventive Maintenance",
                Priority = "P3", Status = "In Progress", OpenedAt = opened, OpenedOdometerKm = v.OdometerKm,
                AssignedSupervisor = "Ravi Kumar", SupervisorAssignedAt = opened,
            };
            db.ServiceEvents.Add(ev);

            var jc = new JobCard
            {
                ServiceEventId = ev.Id, JobCardNumber = JcNo(), Status = flow.Jc, Bay = Bay(f + 2),
                TechnicianId = t?.Id, Technician = t?.Name ?? "", StandardRepairHours = 4m,
                StartedAt = opened.AddMinutes(20),
                CompletedAt = flow.Jc == "Completed" ? now.AddMinutes(-25) : null,
            };
            db.JobCards.Add(jc);
            if (ob is not null) ob.Status = "Planned";

            var tasks = new (string Code, string Desc, string Status, bool Assign)[]
            {
                ("HV-01",  "High-voltage isolation verified before work",        "Completed", true),
                ("BRK-01", "Brake lining thickness measured on all axles",       flow.Jc == "Completed" ? "Completed" : "In Progress", true),
                ("TYR-03", "Tread depth recorded, minimum across axles",          flow.Jc == "Completed" ? "Completed" : "Not Started", flow.Bottleneck != "unassigned"),
                ("CHG-06", "Charging functional check at rated current",          flow.Jc == "Completed" ? "Completed" : "Not Started", flow.Bottleneck != "unassigned"),
            };
            WorkItem? firstItem = null;
            foreach (var (code, desc, status, assign) in tasks)
            {
                var wi = new WorkItem
                {
                    JobCardId = jc.Id, TaskCode = code, WorkType = "Preventive Maintenance", Description = desc,
                    Status = status,
                    AssignedToTechnicianId = assign ? t?.Id : null,
                    AssignedTo = assign ? t?.Name ?? "" : "",
                    Priority = "P3", EstimatedHours = 1m, RequiresQc = true,
                    RequiresHvAuthorization = code.StartsWith("HV"),
                    UpdatedAt = now.AddMinutes(-15 * (f + 1)),
                };
                firstItem ??= wi;
                db.WorkItems.Add(wi);
            }

            if (flow.Bottleneck == "parts" && part is not null && location is not null)
            {
                db.PartRequests.Add(new PartRequest
                {
                    RequestNumber = $"PR-{now:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}",
                    JobCardId = jc.Id, WorkItemId = firstItem?.Id, PartMasterId = part.Id,
                    InventoryLocationId = location.Id,
                    QuantityRequired = 4, QuantityReserved = 1, QuantityIssued = 1,
                    Status = "Awaiting Stock", RequestedBy = t?.Name ?? "Technician",
                    RequestedAt = opened.AddHours(1), UpdatedAt = now.AddMinutes(-30),
                });
            }

            if (flow.Bottleneck == "qc")
            {
                db.QcInspections.Add(new QcInspection
                {
                    JobCardId = jc.Id, Inspector = "Priya N", Result = "Pending",
                    RoadTestRequired = true, RoadTestPassed = false,
                    Remarks = "Awaiting road test slot",
                });
            }

            v.Status = "Under Maintenance";
            db.VehicleAvailabilityLedger.Add(new VehicleAvailabilityLedger
            {
                VehicleId = v.Id, State = "Down-PM", StartAt = opened, ReasonCode = "PM",
                SourceType = "ServiceEvent", SourceServiceEventId = ev.Id, ChangedBy = "Demo Seed",
            });
            f++;
        }
        await db.SaveChangesAsync();

        // ---------------------------------------------------------------
        // 5. Appointments - today's diary and the days ahead
        // ---------------------------------------------------------------
        var centre = (await db.ServiceCentreMasters.OrderBy(c => c.CentreCode).FirstOrDefaultAsync())?.Name ?? "Chennai Service Centre";
        var apSeq = await db.Appointments.CountAsync();
        var slots = new (int Vehicle, int DayOffset, int Hour, string Type, string Status)[]
        {
            (4,  0, 9,  "PM",         "Confirmed"),
            (5,  0, 11, "PM",         "Confirmed"),
            (6,  0, 14, "Inspection", "Requested"),
            (7,  1, 9,  "PM",         "Confirmed"),
            (9,  2, 10, "Repair",     "Requested"),
            (10, 3, 15, "PM",         "Confirmed"),
        };
        foreach (var s in slots)
        {
            if (s.Vehicle >= vehicles.Count) continue;
            var v = vehicles[s.Vehicle];
            var t = Tech(apSeq);
            var start = now.Date.AddDays(s.DayOffset).AddHours(s.Hour);
            db.Appointments.Add(new Appointment
            {
                AppointmentNumber = $"APT-{start:yyyyMMdd}-{++apSeq:D5}",
                VehicleId = v.Id,
                PmObligationId = obligations.FirstOrDefault(x => x.VehicleId == v.Id)?.Id,
                SourceType = "Manual", StartAt = start, EndAt = start.AddHours(4),
                ServiceCentre = centre, Bay = Bay(apSeq), TechnicianId = t?.Id, Technician = t?.Name ?? "",
                AppointmentType = s.Type, Priority = "P3",
                Reason = s.Type == "PM" ? "Scheduled preventive maintenance" : "Customer reported issue",
                PlannedHours = 4m, Status = s.Status, CreatedBy = "Service Coordinator",
                CreatedAt = now.AddDays(-1),
            });
        }
        await db.SaveChangesAsync();

        // ---------------------------------------------------------------
        // 6. Campaigns
        // ---------------------------------------------------------------
        var campaigns = new (string Code, string Title, string Type, int Days)[]
        {
            ("CMP-2026-01", "Motor controller firmware update to FW-MC-2.4.1", "Service Campaign", 60),
            ("CMP-2026-02", "HV junction box inspection - safety recall",       "Safety Recall",    30),
        };
        var c = 0;
        foreach (var cm in campaigns)
        {
            var campaign = new Campaign
            {
                CampaignCode = cm.Code, Title = cm.Title, CampaignType = cm.Type,
                EffectiveFrom = now.AddDays(-20), EffectiveTo = now.AddDays(cm.Days),
                ApplicabilityRule = c == 0 ? "All electric commercial vehicles" : "Rhino and EVIATOR platforms",
                Status = "Active",
            };
            db.Campaigns.Add(campaign);
            foreach (var v in vehicles.Skip(c * 3).Take(4))
            {
                db.VehicleCampaigns.Add(new VehicleCampaign
                {
                    CampaignId = campaign.Id, VehicleId = v.Id,
                    Status = v == vehicles.Skip(c * 3).First() ? "Completed" : "Open",
                    CompletedAt = v == vehicles.Skip(c * 3).First() ? now.AddDays(-4) : null,
                });
            }
            c++;
        }
        await db.SaveChangesAsync();

        // Vehicles not otherwise occupied are available and on the road.
        foreach (var v in vehicles.Where(x => string.IsNullOrWhiteSpace(x.Status) || x.Status == "Available"))
        {
            db.VehicleAvailabilityLedger.Add(new VehicleAvailabilityLedger
            {
                VehicleId = v.Id, State = "In Service", StartAt = now.AddDays(-30),
                ReasonCode = "IN_SERVICE", SourceType = "Commissioning", ChangedBy = "Demo Seed",
            });
        }
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }
}
