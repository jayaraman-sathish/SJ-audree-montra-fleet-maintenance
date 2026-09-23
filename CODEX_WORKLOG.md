# Montra Fleet Maintenance – Codex Worklog

Repository: https://github.com/jayaraman-sathish/SJ-audree-montra-fleet-maintenance

Main branch: `main`

Technology: Angular frontend, ASP.NET Core .NET 8 backend, PostgreSQL database, Render hosting.

## Purpose of this worklog

This file records completed work, confirmed defects, fixes, deployment status, and planned work for the Montra Fleet Maintenance project.

## Completed work

feature/v1.8.26
### Fleet status dashboard – prepared for next P0 update

- Added dashboard summary values for Available, In service, Breakdown, Off-hire, PM overdue and Unassigned jobs.
- Derived In service from active Service Events and Breakdown from distinct vehicles with open Breakdown records.
- Derived Off-hire from vehicles currently marked Off-Hire.
- Kept existing PM obligation and Work Item transaction logic as the source of PM overdue and unassigned job counts.
- Added a browser regression check for all six dashboard status labels.

### Platform and build

- Confirmed the project is a real Angular + .NET 8 + PostgreSQL application, not a demo.
- Confirmed the backend builds successfully with .NET 8.
- Confirmed the Angular frontend builds successfully with npm/Angular.
- Confirmed the application is deployed on Render.

### Veerai AI assistant

- Added Veerai chat and voice modes.
- Added language selection for English, Hindi, Tamil and Malayalam.
- Added microphone recording and transcription flow.
- Added stop-listening and stop-speaking controls.
- Added browser-session unlock for Veerai access.
- Added expandable Veerai panel.
- Added vehicle/Job Card context support in the assistant flow.
- Added source/evidence display in AI responses.
- Added configurable AI Configuration navigation and read-only access direction.

### Document upload

- Added manufacturer and customer document upload support.
- Added document metadata including vehicle/model, manufacturer, document type, title, revision and expiry.
- Added PDF, PNG and JPEG validation.
- Added a 10 MB maximum file-size validation.
- Added backend storage of uploaded document content in the database.
- Added document list loading and document grid display.
- Added Preview and Download links for uploaded files.

## Confirmed document issue and resolution history

### Initial issue

The Documents page showed:

`Unable to complete this action. Please retry.`

The Save button also failed or remained in a saving state.

### Confirmed production root cause

Render logs confirmed:

`AmbiguousMatchException`

The following endpoints were registered twice:

- `GET /api/documents`
- `POST /api/documents/upload`

The duplicate mappings existed in both the dedicated document endpoint file and `ControlEndpoints.cs`. ASP.NET Core could not decide which endpoint to execute, so both loading and saving failed.

### Fix completed in PR #79

- Removed the duplicate document endpoint registrations from `ControlEndpoints.cs`.
- Kept the dedicated document implementation in `Data/DocumentUpload.cs`.
- Restored normal document loading and saving.

### Follow-up grid issue

After saving worked, manufacturer documents were saved but not displayed in the Manufacturer tab because the grid requires document-scope and model metadata for filtering.

### Fix prepared in PR #80

- Restored `DocumentScope`.
- Restored `VehicleModelMasterId`.
- Restored manufacturer, vehicle type, model name, title and revision metadata.
- Kept the unsafe binary-length calculation out of the list query.

### Preview/download issue

The grid displayed `Legacy reference: database` instead of file actions because the list response did not include `hasFile` and `fileUrl`.

### Fix prepared in PR #81

- Added `hasFile` to the document list response.
- Added `fileUrl` pointing to `/api/documents/{id}/file`.
- Restored Preview and Download actions in the grid.

## Veerai voice work

### Current implementation

Veerai currently uses the browser's Web Speech API for response audio. It does not yet use a server-side paid or open-source text-to-speech model.

### Platform and build

- Confirmed the project is a real Angular + .NET 8 + PostgreSQL application, not a demo.
- Confirmed the backend builds successfully with .NET 8.
- Confirmed the Angular frontend builds successfully with npm/Angular.
- Confirmed the application is deployed on Render.

### Veerai AI assistant

- Added Veerai chat and voice modes.
- Added language selection for English, Hindi, Tamil and Malayalam.
- Added microphone recording and transcription flow.
- Added stop-listening and stop-speaking controls.
- Added browser-session unlock for Veerai access.
- Added expandable Veerai panel.
- Added vehicle/Job Card context support in the assistant flow.
- Added source/evidence display in AI responses.
- Added configurable AI Configuration navigation and read-only access direction.

### Document upload

- Added manufacturer and customer document upload support.
- Added document metadata including vehicle/model, manufacturer, document type, title, revision and expiry.
- Added PDF, PNG and JPEG validation.
- Added a 10 MB maximum file-size validation.
- Added backend storage of uploaded document content in the database.
- Added document list loading and document grid display.
- Added Preview and Download links for uploaded files.

## Confirmed document issue and resolution history

### Initial issue

The Documents page showed:

`Unable to complete this action. Please retry.`

The Save button also failed or remained in a saving state.

### Confirmed production root cause

Render logs confirmed:

`AmbiguousMatchException`

The following endpoints were registered twice:

- `GET /api/documents`
- `POST /api/documents/upload`

The duplicate mappings existed in both the dedicated document endpoint file and `ControlEndpoints.cs`. ASP.NET Core could not decide which endpoint to execute, so both loading and saving failed.

### Fix completed in PR #79

- Removed the duplicate document endpoint registrations from `ControlEndpoints.cs`.
- Kept the dedicated document implementation in `Data/DocumentUpload.cs`.
- Restored normal document loading and saving.

### Follow-up grid issue

After saving worked, manufacturer documents were saved but not displayed in the Manufacturer tab because the grid requires document-scope and model metadata for filtering.

### Fix prepared in PR #80

- Restored `DocumentScope`.
- Restored `VehicleModelMasterId`.
- Restored manufacturer, vehicle type, model name, title and revision metadata.
- Kept the unsafe binary-length calculation out of the list query.

### Preview/download issue

The grid displayed `Legacy reference: database` instead of file actions because the list response did not include `hasFile` and `fileUrl`.

### Fix prepared in PR #81

- Added `hasFile` to the document list response.
- Added `fileUrl` pointing to `/api/documents/{id}/file`.
- Restored Preview and Download actions in the grid.

## Veerai voice work

### Current implementation

Veerai currently uses the browser's Web Speech API for response audio. It does not yet use a server-side paid or open-source text-to-speech model.

### Completed voice improvements

- Added voice-language selection.
- Added voice loading and language matching.
- Added Tamil, Hindi and Malayalam language matching where the browser provides a suitable voice.
- Added speaking state and Stop button.
- Added speech cancellation when closing, changing mode or starting a new request.

### Remaining voice issue

The UI receives the response text, but some browsers remain silent even when the UI shows the speaking state. This is a browser speech-engine reliability issue, not an AI-response issue.

### Fix prepared in PR #83

- Added speech-engine recovery using cancel and resume.
- Added delayed speech restart.
- Added visible error feedback when the browser reports a speech failure.

For production-grade multilingual voice, a later phase should use a controlled server-side TTS service or a tested open-source TTS service instead of relying only on the browser's installed voices.

## Current security direction for Veerai

Veerai must use approved backend APIs only. It must not connect directly to PostgreSQL or directly query database tables.

The planned read-only access areas are:

- Vehicles and vehicle models
- Job Cards
- Service events
- Breakdowns and RSA records
- Appointments and capacity
- PM plans, obligations and checklists
- Parts and inventory
- Technicians and assignment information
- Uploaded documents
- Audit records

Veerai must not directly modify these records. Any future write action must use a separate, explicitly approved workflow with user confirmation, authorization and audit logging.

## Planned technician Veerai capability

Technicians should be able to ask questions about a vehicle, Job Card or service issue and receive evidence-based guidance.

Examples:

- What is the PM checklist for this vehicle?
- What should I check if the AC is not working?
- What was the previous repair history for this vehicle?
- Are there repeated failures for this vehicle?
- Which parts are normally associated with this fault?
- What checks are still pending on this Job Card?
- What does the manufacturer service manual say?

Expected response structure:

1. Direct answer in simple language.
2. Vehicle and Job Card context used.
3. Recommended checks or next steps.
4. Document section or fleet record source.
5. Warning where technician, supervisor or safety confirmation is required.

The assistant must ask for a vehicle registration number or Job Card number when the question is ambiguous. It must say when no approved document or fleet record supports an answer and must not invent technical values or repair instructions.

## Planned document intelligence

Uploaded documents are currently stored and displayed. The next document-AI phase should:

- Extract searchable text from PDF documents.
- Index documents by vehicle model, manufacturer, document type and revision.
- Preserve document version and approval status.
- Return source document and section references in Veerai answers.
- Prevent Veerai from using obsolete or unapproved documents.
- Keep document access read-only for technicians.

## Deployment and testing rules

- Backend must build with .NET 8 before merge.
- Frontend must build successfully before merge.
- Document endpoint routes must be unique.
- Opening the Documents page must be tested before testing upload.
- Upload must be tested with one PDF, one PNG and one JPEG.
- After upload, the record must appear in the correct Customer or Manufacturer tab.
- Preview and Download must be tested from the grid.
- Voice mode must be tested in English first, then Indian languages.
- Cypress/regression checks should be intentionally configured and should not run unnecessarily for every unrelated change.

## Important current status

- Document save: fixed and confirmed working after duplicate endpoint removal.
- Document grid visibility: follow-up fix prepared.
- Document Preview/Download: follow-up fix prepared.
- Veerai text chat: working.
- Veerai voice transcription: implemented, browser-dependent.
- Veerai response audio: recovery fix prepared; multilingual production TTS remains future work.
- Technician document-and-fleet assistance: planned next capability.

## Next recommended sequence

1. Merge and deploy PR #79 if not already deployed.
2. Merge and deploy PR #80.
3. Merge and deploy PR #81.
4. Merge and deploy PR #83.
5. Test Documents page, upload, Preview and Download.
6. Test Veerai English response audio.
7. Implement technician read-only APIs and document search/indexing.
8. Add technician-specific prompts, sources and safety controls.
 main

### Completed voice improvements

- Added voice-language selection.
- Added voice loading and language matching.
- Added Tamil, Hindi and Malayalam language matching where the browser provides a suitable voice.
- Added speaking state and Stop button.
- Added speech cancellation when closing, changing mode or starting a new request.

### Remaining voice issue

The UI receives the response text, but some browsers remain silent even when the UI shows the speaking state. This is a browser speech-engine reliability issue, not an AI-response issue.

### Fix prepared in PR #83

- Added speech-engine recovery using cancel and resume.
- Added delayed speech restart.
- Added visible error feedback when the browser reports a speech failure.

For production-grade multilingual voice, a later phase should use a controlled server-side TTS service or a tested open-source TTS service instead of relying only on the browser's installed voices.

## Current security direction for Veerai

Veerai must use approved backend APIs only. It must not connect directly to PostgreSQL or directly query database tables.

The planned read-only access areas are:

- Vehicles and vehicle models
- Job Cards
- Service events
- Breakdowns and RSA records
- Appointments and capacity
- PM plans, obligations and checklists
- Parts and inventory
- Technicians and assignment information
- Uploaded documents
- Audit records

Veerai must not directly modify these records. Any future write action must use a separate, explicitly approved workflow with user confirmation, authorization and audit logging.

## Planned technician Veerai capability

Technicians should be able to ask questions about a vehicle, Job Card or service issue and receive evidence-based guidance.

Examples:

- What is the PM checklist for this vehicle?
- What should I check if the AC is not working?
- What was the previous repair history for this vehicle?
- Are there repeated failures for this vehicle?
- Which parts are normally associated with this fault?
- What checks are still pending on this Job Card?
- What does the manufacturer service manual say?

Expected response structure:

1. Direct answer in simple language.
2. Vehicle and Job Card context used.
3. Recommended checks or next steps.
4. Document section or fleet record source.
5. Warning where technician, supervisor or safety confirmation is required.

The assistant must ask for a vehicle registration number or Job Card number when the question is ambiguous. It must say when no approved document or fleet record supports an answer and must not invent technical values or repair instructions.

## Planned document intelligence

Uploaded documents are currently stored and displayed. The next document-AI phase should:

- Extract searchable text from PDF documents.
- Index documents by vehicle model, manufacturer, document type and revision.
- Preserve document version and approval status.
- Return source document and section references in Veerai answers.
- Prevent Veerai from using obsolete or unapproved documents.
- Keep document access read-only for technicians.

## Deployment and testing rules

- Backend must build with .NET 8 before merge.
- Frontend must build successfully before merge.
- Document endpoint routes must be unique.
- Opening the Documents page must be tested before testing upload.
- Upload must be tested with one PDF, one PNG and one JPEG.
- After upload, the record must appear in the correct Customer or Manufacturer tab.
- Preview and Download must be tested from the grid.
- Voice mode must be tested in English first, then Indian languages.
- Cypress/regression checks should be intentionally configured and should not run unnecessarily for every unrelated change.

## Important current status

- Document save: fixed and confirmed working after duplicate endpoint removal.
- Document grid visibility: follow-up fix prepared.
- Document Preview/Download: follow-up fix prepared.
- Veerai text chat: working.
- Veerai voice transcription: implemented, browser-dependent.
- Veerai response audio: recovery fix prepared; multilingual production TTS remains future work.
- Technician document-and-fleet assistance: planned next capability.

## Next recommended sequence

1. Merge and deploy PR #79 if not already deployed.
2. Merge and deploy PR #80.
3. Merge and deploy PR #81.
4. Merge and deploy PR #83.
5. Test Documents page, upload, Preview and Download.
6. Test Veerai English response audio.
7. Implement technician read-only APIs and document search/indexing.
8. Add technician-specific prompts, sources and safety controls.
## 2026-09-22 - Document attachment and dashboard follow-up

- Found the remaining document-edit limitation: the model stored only one embedded file, so Edit could not show an attachment collection or support replacing several files.
- Added `DocumentAttachment` storage and a dedicated attachment preview/download route, while retaining compatibility with legacy embedded files.
- Added the live knowledge base at `docs/KNOWLEDGE_BASE.md` with the attachment, safe-package and dashboard rules.
- Next dashboard work is scoped into five sections: fleet summary, today's operational status, maintenance performance, fleet utilization/cost, and alerts/action queue.
- Validation still required before packaging: ASP.NET Core build, Angular build, API/route tests, and a browser test for edit, attachment display, remove/replace and multi-file upload.

## 2026-09-22 - Repeatable local build setup

- Added repository `global.json` to pin backend builds to .NET SDK 8.0.425 instead of selecting .NET 10 automatically.
- Kept Angular CLI as a local dependency and added `npm run build:clean` for a repeatable `npm ci` plus production build.
- Removed the existing Angular NG8107 optional-chain warning in the PM program matrix template.


## 2026-09-22 - Maintenance action queue and UI refresh

- Added API-backed maintenance action queue counts for jobs awaiting assignment, jobs blocked by parts, jobs awaiting QC/release, critical failed checks and overdue PM.
- Added record-level identifiers to the action queue so users can trace each item to its task, Job Card, vehicle, part request or PM obligation.
- Added dashboard links that route users to the relevant operational screen with source identifiers in the query string.
- Confirmed the backend and Angular builds passed after the action-queue implementation.
- Identified the production UI issue: the shell had a plain navigation hierarchy, excessive unused space, flat cards and a floating Veerai panel that could cover dashboard content.
- Prepared a separate UI-only branch for the visual refresh so backend behavior remains isolated.
- Refreshed the shell with a stronger enterprise navigation hierarchy, clearer active states, improved header/search styling, full-width content treatment, action-queue card styling and responsive layout rules.
- UI changes must be merged only through a feature-branch pull request after backend build, frontend build and regression checks pass.


## 2026-09-22 - UI refresh review outcome

- Local visual review found the merged shell refresh was not acceptable: it introduced excessive empty space, weak dashboard proportions and a less usable action-queue presentation.
- Opened PR #91 to restore the original shell/sidebar/header styling.
- The restoration intentionally preserves the backend action-queue APIs, record identifiers, dashboard bindings and document fixes.
- A future UI redesign must be prototyped and visually reviewed locally before merge.


## 2026-09-23 - Secure VeerAI API workspace

- Created isolated branch `feature/secure-veerai-api-workspace`; no production or `main` branch changes are used during implementation.
- Confirmed Angular VeerAI calls backend endpoints only; it does not connect to PostgreSQL or execute SQL.
- Kept VeerAI read-only: data access is controlled by backend API authorization and the AI read-access configuration.
- Strengthened configuration validation so a save must include every approved data area exactly once; partial or duplicate payloads are rejected instead of silently changing defaults.
- The working screen must provide API-backed chat, vehicle/Job Card context, evidence/source records, safe error handling and refusal when approved data is unavailable.
- Required validation before PR: .NET Release build, Angular production build, API authorization/configuration checks, read-only behavior and browser review of the AI Configuration screen.


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


## 2026-09-23 - VeerAI module access enforcement

- Added module detection before VeerAI chat data retrieval.
- Added no-access responses for disabled Vehicles, Job Cards, Service Events, Breakdowns, Appointments, PM, Parts and Inventory, Technicians, Documents and Audit Records modules.
- Closed the pending-work shortcut path so it cannot bypass the Job Cards permission.
- Confirmed disabled modules must not be queried or included in the AI evidence package.
- Updated `docs/VEERAI.md` and `docs/KNOWLEDGE_BASE.md` with the control model and test expectation.
- Validation required: .NET build, Angular build, disabled-module API tests, and browser tests for each configuration toggle.


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


## 2026-09-24 — Vehicle registration, reporting, and VeerAI API governance requirements

### Completed

- Added dependency-aware VeerAI module access control.
- Added vehicle-only evidence handling for vehicles without a Job Card.
- Added the registered VeerAI API catalog with route, method, module, dependencies, sensitivity, owner, version, and read-only classification.
- Exposed the catalog through an administrator-protected backend endpoint.
- Added the registered API catalog to the AI Configuration screen.
- Documented that VeerAI must not receive direct SQL access or arbitrary SQL execution.
- Backend and frontend builds passed on the dependency-aware branch.

### Required next implementation

#### Vehicle registration management

- Add New Vehicle / New Registration action.
- Add Edit Vehicle / Edit Registration action.
- Search by registration number or VIN.
- Validate VIN and registration number uniqueness.
- Keep the internal vehicle ID unchanged during edits.
- Record create and edit audit events.
- Protect create/edit APIs with normal application authorization; these are not VeerAI read-only APIs.

#### Vehicle Maintenance Report

Generate a professional PDF containing the complete vehicle history through the report date:

- Registration, VIN, model, variant, current status, odometer, operating hours, and battery status.
- Service Events, Job Cards, breakdowns, complaints, PM history, appointments, parts, labour, technicians, QC, defects, documents, open items, and a chronological timeline.

#### Job Card Completion Report

Generate a professional PDF for one Job Card containing:

- Vehicle, complaint, diagnosis, assigned staff, tasks, checklist results, defects, corrective actions, parts, labour, costs, documents/evidence, QC, approval/release status, and activity timeline.

Reports must be generated by the backend from verified records. VeerAI may summarise records but must not invent report facts.

#### VeerAI API catalog administration

- Add a stable API registration ID for each catalog entry.
- Add administrator-controlled Create API Registration and Edit API Registration actions.
- Capture route, method, business module, dependencies, data sensitivity, read/write classification, owner, version, and approval status.
- New AI APIs must be unregistered and unavailable by default.
- Prevent registration of write-capable endpoints for VeerAI.
- Audit who registered, edited, enabled, disabled, or approved an API.
- Add CI validation so an AI endpoint missing catalog registration fails the build or deployment review.
