\# Montra Fleet Maintenance – Codex Worklog



\## Repository

\- GitHub: https://github.com/jayaraman-sathish/SJ-audree-montra-fleet-maintenance

\- Main branch: `main`

\- Current version: v1.8.25



\## Latest Completed Work

\- Manufacturer document upload support

\- Customer/Manufacturer upload toggle

\- Vehicle type image upload and fallback

\- Frontend and backend builds completed

\- Changes pushed to `main`



\## Current Known Issues

\- Document save takes approximately 65 seconds and does not complete reliably.

\- VeerAI currently supports text chat only.



\## Next Task

\- Fix document save performance and timeout.

\- Add VeerAI Chat/Voice toggle.

\- Support English, Hindi, Tamil and Malayalam.

\- Add microphone input and spoken AI response.

\- Build and test backend and frontend.

\## Current Update In Progress

\- VeerAI Chat/Voice toggle added in the working source.

\- Voice languages: English, Hindi, Tamil and Malayalam.

\- Browser microphone input and spoken response use the existing secured VeerAI chat API.

\- Document files remain in PostgreSQL for the current Render environment; object storage is deferred until production.

\- The document-save delay still requires verification on the deployed Render service to distinguish cold-start delay from database upload time.



\## Build Commands



\### Backend



```cmd




## 2026-09-21 — Document Save and AI Access Control

### Document save fix submitted

- PR #71: https://github.com/jayaraman-sathish/SJ-audree-montra-fleet-maintenance/pull/71
- Root cause found: the Angular Documents screen posted to `/api/documents/upload`, but the .NET backend did not register the matching POST endpoint in the current `main` code.
- Added multipart upload handling for PDF, PNG and JPEG files up to 10 MB.
- Added validation for document scope, vehicle, vehicle model and Job Card references.
- Saves document metadata and file content in one database transaction.
- Added a 60-second frontend timeout so the Save button cannot remain on `Saving…` forever.
- The UI now shows a clear success, failure or timeout message.

### AI read-only access control

Planned under **Configuration → AI Configuration**:

- Vehicles
- Job Cards
- Service Events
- Breakdowns
- Appointments
- Preventive Maintenance
- Parts and inventory
- Technicians
- Documents and evidence
- Audit records

Design requirements:

- VeerAI uses approved backend read-only APIs, never direct SQL access.
- Admin must be enforced by server-side authentication and authorization.
- Each data area can be enabled or disabled.
- AI cannot create, edit, approve, delete, close or release records.
- AI data access must be audit logged.
- The AI Configuration screen must not be enabled until real backend Admin role enforcement is available.

### Verification

- The user's local .NET 8 build previously succeeded.
- The document-save PR still needs to be merged and deployed to Render before browser testing.
- After deployment: refresh with Ctrl+F5, upload one small PDF, confirm the document appears in the Documents list, then test a failed/oversized upload.
