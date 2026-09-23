# Veerai — service-job reasoning pilot

Choose the floating **Ask Veerai** button at the bottom right of any page and select a vehicle/Job Card. In a Service Workspace, the button reads **Veerai · Analyse this job** and uses the current job automatically. Ask about causes, repeat failures or repair/QC evidence. Results separate findings, hypotheses, missing evidence, recommended checks and QC concerns, with source excerpts and links. Reanalyse after changes; this is a snapshot, not a live diagnosis.

## Configure on the backend (Render environment)

- `Veerai__Enabled=true`
- `Veerai__ApiKey`: your OpenAI API key (server only; never commit or enter in browser).
- `Veerai__Model`: an available Responses API text model for your account.
- `Veerai__AccessKey`: independently generated random secret of at least 32 characters. Give only to authorised pilot users. This is the key entered in the Veerai panel, not the provider key. Browser memory only; cleared on close/job change.

Disabled until all settings exist. No live provider credentials are bundled. Configure only after agreeing to send the selected job excerpts to OpenAI. Requests set `store=false`; this is not a guarantee of zero provider retention. See your provider's data terms. Do not enable on a public/shared deployment as a replacement for user authentication. The temporary shared pilot key does not provide individual user attribution or role permissions; individual sign-in and per-job authorisation are prerequisites for wider rollout.

## Evidence and limits

Backend reads current job complaints, tasks, template readings/specifications, defects, notes, part usage, QC and up to five earlier jobs on the same vehicle. Bounded excerpts are labelled. Queries use AsNoTracking and never save records. Names/VIN/registration are excluded from structured fields; free-text may contain personal data. No photos, actual OEM manuals or telemetry are sent, and no compatibility is inferred from part use. Manual ingestion/search and broader parts/stock reasoning are future scope.

Output is an advisory draft. The server rejects missing/unknown citations, incomplete provider responses and invalid JSON; a valid citation does not prove factual correctness. Instructions resist record-borne prompt injection, but model reasoning remains fallible and requires technician/supervisor review. There are no write tools and no AI QC/release authority. No automatic retries, six requests/minute per application instance, 90-second timeout, input/context/output caps. Distributed quotas/individual authorisation are future work. Analysis is not persisted to official history.

## Verification

Automated checks exercise evidence scoping, response validation, credentials, provider request construction and unavailable-provider UI. Provider replies in protocol tests are explicit fixtures, not live reasoning. A live model quality evaluation using approved case histories is still required after configuring credentials. Existing Cypress service journeys run against isolated PostgreSQL only.

API transport: https://developers.openai.com/api/reference/resources/responses/methods/create

## v1.8.22 conversational chat

The global launcher opens a compact, draggable, minimisable chat. Type a Montra service question directly. Current workspace context is used automatically. A vehicle or Job Card mentioned in the question resolves server-side; multiple visits produce conversational choices. Unknown references never silently substitute the previous job. General Montra guidance is available without a service record; it is not an OEM manual lookup.

The chat uses `Veerai__Enabled`, `Veerai__ApiKey` and `Veerai__Model` on the server. The access key is entered once to unlock this browser for eight hours, not attached to each chat question. The server issues an encrypted, HttpOnly, SameSite=Strict cookie; production cookies are Secure. Key rotation invalidates existing sessions. The legacy analysis endpoint retains its access-key check. Chat remains protected and this release does not add individual user sign-in. It shares the existing six-requests-per-minute instance limiter. Configure provider project spending limits. Do not embed provider credentials in frontend code.

Chat is temporary browser memory, reset on workspace navigation or New chat; minimise preserves it. The server receives at most ten recent UI messages and bounded job evidence, with `store:false`. No fleet records are modified. Structured output separates relevance, reply and valid evidence IDs. Relevance is model judgement, not a guaranteed semantic security boundary. No speech input, images, manuals or telemetry are integrated.

Provider HTTP errors distinguish credentials, quota, model/request rejection, timeout and malformed output without returning provider bodies or secrets. The previous generic error cannot establish which one happened in production. Cypress uses simulated replies; local backend checks test context resolution and schema validation, not live model diagnostic quality.

Provider format follows https://developers.openai.com/api/docs/guides/structured-outputs?api-mode=responses.


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