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




## 2026-09-21 — Configurable VeerAI access control

- PR #72: https://github.com/jayaraman-sathish/SJ-audree-montra-fleet-maintenance/pull/72
- Added **Configuration → AI Configuration** screen.
- Added ten read-only module controls: Vehicles, Job Cards, Service Events, Breakdowns, Appointments, PM, Parts, Technicians, Documents and Audit.
- Settings are stored in `MasterOptions` using category `AI_READ_ACCESS`.
- Configuration changes are audited.
- The screen and API require the server-side `Veerai:AdminKey` and `X-Veerai-Admin-Key` header.
- The document upload endpoint and Save timeout from PR #71 are included in this branch.
- Before deployment, set a strong Render environment variable named `Veerai__AdminKey`.
- The remaining integration step is to make every VeerAI data query consult the selected module policy before returning records.
