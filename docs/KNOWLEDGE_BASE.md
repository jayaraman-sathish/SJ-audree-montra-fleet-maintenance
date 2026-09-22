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
