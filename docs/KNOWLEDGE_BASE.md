# Montra Fleet Maintenance Knowledge Base

## Document attachments

- A document is the metadata record; attachments are stored separately so one document can have multiple PDF, PNG or JPEG files.
- Legacy records that still contain the original embedded file remain readable.
- Edit replacement deactivates the prior attachment set and stores the newly selected files.
- Preview and download must use the attachment-specific endpoint.
- Do not treat a successful update-package copy as a Git push; the log must show the commit and push result separately.

## Safe update packages

- Extract the package beside the repository or use the package layout documented in `README_FIRST.txt`.
- Never include `node_modules`, `bin`, `obj`, `dist`, `.git`, screenshots or generated backups.
- Backups are written under `_update-backups/update-YYYYMMDD-HHmmss` before replacement.
- Git warnings such as LF/CRLF conversion are warnings, not failures; Git exit codes decide success.

## Dashboard rules

- Dashboard metrics must be derived from stored fleet transactions.
- If a metric cannot yet be derived reliably, show `Not available` and do not invent a value.
- Work Order remains the central maintenance workspace and analytics must preserve traceability to the source transaction.


## Maintenance action queue traceability

- Queue counts must come from backend fleet transactions, not hard-coded frontend values.
- Every queue record shown on the dashboard must expose a source identifier where available: task ID, Job Card ID/number, vehicle ID/registration, part request ID or PM obligation ID.
- Clicking a queue record must open the relevant operational screen with query parameters that preserve the source record context.
- If a queue category has no records, display No records; do not create placeholder IDs.

## UI issue found and fix direction

### Issue found

The dashboard shell looked unfinished because the sidebar hierarchy was visually flat, the content did not use the available width effectively, KPI cards had weak visual hierarchy, and the floating Veerai panel could overlap operational content.

### Fix prepared

- Use a structured enterprise shell with a clear dark navigation rail and stronger active-link state.
- Use a clean content canvas with consistent spacing, borders, shadows and responsive full-width behavior.
- Make dashboard cards and action-queue panels visually distinct while preserving existing routes and data bindings.
- Keep Veerai above the page layer and prevent the dashboard layout from depending on its position.

### Validation required before merge

1. dotnet build backend/src/MontraFleet.Api/MontraFleet.Api.csproj -c Release
2. npm run build from frontend
3. Regression checks for dashboard navigation and action-queue record links
4. Manual check of desktop and mobile sidebar behavior
5. Manual check that Veerai does not hide queue records or form controls
6. Verify no direct commit is made to main; merge only the reviewed pull request


## UI refresh review outcome

The first shell redesign passed compilation but failed visual acceptance in local review. The corrective action is to restore the previous shell styling while retaining the backend metrics and record traceability. Future visual work must be isolated, locally reviewed at desktop and mobile widths, and merged only after acceptance.


## VeerAI API boundary and working-screen rules

- The Angular application communicates with VeerAI through backend HTTPS API endpoints only.
- VeerAI must never receive database connection strings, SQL credentials or direct database access.
- Backend APIs enforce authorization, rate limiting, approved read-only data areas and safe error responses.
- The AI Configuration screen must load and save configuration through API calls; it must not use hard-coded production values.
- Configuration saves must contain every approved data area exactly once. Partial or duplicate payloads are rejected.
- VeerAI responses must preserve vehicle/Job Card context and show evidence/source identifiers where available.
- Unsupported or ambiguous questions must produce a clear clarification/refusal response rather than invented fleet or technical data.
- Changes are implemented on a feature branch and merged only after backend, Angular, API and browser checks pass.


## VeerAI orchestration service design

### Purpose

VeerAI must not access PostgreSQL, database connection strings or Entity Framework entities directly from the Angular client or from an external AI provider. The backend owns all data access and sends only approved, bounded evidence to the AI provider.

### Request flow

```
Angular VeerAI workspace
        ↓ HTTPS API
VeerAI API endpoint
        ↓ authenticated request
VeerAiOrchestrationService
        ↓ policy and intent checks
Approved fleet read services
        ↓ controlled queries
Fleet database
```

### Orchestration responsibilities

1. Validate the authenticated session and request size.
2. Resolve the vehicle or Job Card context; ask the user to clarify when multiple records match.
3. Classify the request as supported Montra service/maintenance guidance or unsupported content.
4. Check the enabled VeerAI read-access modules before requesting data.
5. Call approved read-only fleet services, never arbitrary table queries from the UI.
6. Build a bounded evidence package with source IDs, record labels, links and scope limits.
7. Apply safety rules: no record changes, no QC/release approval, no hazardous bypass instructions and no invented specifications.
8. Send the evidence package to the configured AI provider through a server-side credential.
9. Validate the provider response and reject missing, unknown or unsupported citations.
10. Return the answer, evidence sources, clarification choices or a safe error to Angular.
11. Write an audit event without storing provider secrets or unnecessary sensitive prompt content.

### API boundary

- Angular calls only `/api/veerai/chat`, `/api/veerai/transcribe` and approved VeerAI endpoints.
- The provider key exists only in server environment configuration.
- The browser receives no SQL credentials, connection strings or unrestricted database data.
- VeerAI operations are advisory and read-only. Any future write capability requires a separate approved workflow, user confirmation, authorization and audit trail.
- API responses must avoid returning provider response bodies, secrets, SQL errors or stack traces.

### Required result contract

Each response must contain a concise answer plus source identifiers where evidence was used. When evidence is insufficient, the orchestration service must state what is missing. When the question is ambiguous, it must return selectable context choices instead of silently using another vehicle or Job Card.

### Failure handling

- Invalid session: return 401.
- Disabled or incomplete provider configuration: return 503.
- Ambiguous context: return clarification choices.
- Provider timeout or invalid response: return a safe retry message; do not modify fleet records.
- Rate limit exceeded: return 429.
- Unsupported question: return a clear scope refusal.

The service must be independently testable with provider fixtures. Production merge requires backend build, Angular build, API authorization tests, read-only tests, response-citation tests and browser validation of the working VeerAI screen.


## Module access enforcement

Every VeerAI chat request must identify the requested data area before evidence retrieval. The backend checks the matching `AI_READ_ACCESS` module setting first.

| Module | Configuration code | Disabled behavior |
|---|---|---|
| Vehicles | `vehicles` | No access response; no vehicle evidence |
| Job Cards | `job-cards` | No job context, pending-job list or job evidence |
| Service Events | `service-events` | No service-event evidence |
| Breakdowns | `breakdowns` | No breakdown evidence |
| Appointments | `appointments` | No appointment/capacity evidence |
| Preventive Maintenance | `pm` | No PM evidence |
| Parts and Inventory | `parts` | No stock/part evidence |
| Technicians | `technicians` | No technician evidence |
| Documents and Evidence | `documents` | No document evidence |
| Audit Records | `audit` | No audit evidence |

When a module is disabled, the API returns a clear message such as **“No access to Parts and Inventory data is enabled for VeerAI.”** It must not query the underlying records and must not send that data to the AI provider.

The same check applies to shortcut paths such as pending-work queries; they must not bypass the orchestration permission boundary.

---

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
