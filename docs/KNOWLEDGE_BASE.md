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
