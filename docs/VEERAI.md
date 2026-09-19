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
