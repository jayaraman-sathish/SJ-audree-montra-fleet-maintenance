# VeerAI dependency-aware access addendum

This addendum applies to the application design, maintenance task matrix/service execution design, and VeerAI technician assistance design documents in the repository.

## Design decision

VeerAI never receives direct SQL credentials and never queries PostgreSQL directly. The browser calls approved backend endpoints. The orchestration service is the policy boundary: it resolves the business context, expands dependencies, checks AI read-access settings, collects only approved evidence, applies field-level minimization, and then calls the configured AI provider.

## Business dependency model

Access is evaluated by business answer, not by isolated tables:

| Answer area | Required read areas |
|---|---|
| Vehicle status/enrollment | Vehicles |
| Service history | Vehicles + Service Events |
| Job Card/work order | Vehicles + Service Events + Job Cards |
| Breakdown context | Vehicles + Service Events + Breakdowns |
| Preventive maintenance | Vehicles + Preventive Maintenance |
| Technician assignment on a job | Vehicles + Service Events + Job Cards + Technicians |
| Job Card documents/evidence | Vehicles + Service Events + Job Cards + Documents |
| Parts stock | Parts and Inventory |
| Appointments/capacity | Appointments |
| Audit explanation | Audit Records |

If any required area is disabled, VeerAI returns a clear access-controlled response and does not load partial evidence. This prevents misleading answers when a related record is visible in the application but not approved for AI use.

## Vehicle and Job Card relationship

A vehicle is not rejected merely because it has no service job. Vehicle-only questions use the approved vehicle evidence reader. Job-specific questions use the Job Card evidence reader and require the complete Job Card dependency chain. This preserves the domain relationship without conflating vehicle enrollment with service execution.

## UI behavior

AI Configuration displays the dependency relationship and warns when a business answer will be blocked by a disabled dependency. The backend remains authoritative; UI state is advisory and cannot bypass policy.

## Enterprise control pattern

This follows the standard ERP pattern: a governed read-only integration/API layer, least-privilege business scopes, dependency-aware authorization, evidence minimization, auditability, and explicit “no access” behavior. AI provider credentials remain server-side configuration and are never sent to the browser.


The three existing Word design documents remain the controlled presentation artifacts; this Markdown addendum is the source-controlled implementation delta for this release.
