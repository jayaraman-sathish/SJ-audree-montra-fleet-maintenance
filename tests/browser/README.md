# Isolated Cypress checks

The `Cypress isolated service flows` workflow creates a disposable PostgreSQL 16 database, builds .NET/Angular and runs the Electron browser against `http://127.0.0.1:5080`. The configuration rejects other base URLs. No production URL, connection string or credentials are used.

Run through a draft pull request or GitHub Actions workflow dispatch. Review screenshots, video and backend log in the `cypress-test-evidence` artifact. A failed build/setup is not a passed browser test.

## Coverage

- 26 page-load checks with JavaScript errors left enabled and API 5xx detection.
- PM, Breakdown and Maintenance journeys with API-created unique vehicles, intake, supervisor/technician allocation, additional work and recorded checks.
- Browser checks for premature-release blocking, QC save, saved QC summary and successful release.
- API assertions for completion report, final vehicle/job status, and reassignment blocked after release.
- Enrollment model-picture fallback preview in the browser.
- Browser stock receipt and ledger display; real API reservation, partial issue, consumption, return, cancellation and reconciliation assertions.

This is a hybrid browser/API suite, not exhaustive click-through testing of every field, role, concurrent action or failure branch. It uses real application APIs and PostgreSQL, with no mocked service responses. Catalogue photos/OEM numbers are not fabricated; picture and part fixtures are explicitly test-only.

Local execution requires an isolated application and database at the fixed loopback address, Node 22 and Cypress dependencies:

```
cd tests/browser
npm install
npm test
```
