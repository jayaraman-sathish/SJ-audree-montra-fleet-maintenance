# VeerAI API and database governance

## API registration

Every endpoint or internal evidence reader used by VeerAI must be registered in `VeeraiApiCatalog` with:

- route and HTTP method
- business module and dependencies
- data sensitivity
- read-only classification
- owner and version

The catalog is administrator-visible at `GET /api/ai/api-catalog` and is protected by the VeerAI Administrator key.

Unregistered AI capabilities must fail closed. Adding a backend endpoint alone does not grant VeerAI access.

## Production visibility

The production checklist must include:

1. Add the endpoint to the catalog.
2. Assign its business dependencies.
3. Confirm it is read-only.
4. Review its data sensitivity and field minimization.
5. Confirm administrator visibility.
6. Add an access-denied test and an enabled-path test.
7. Review deployment logs and API catalog after release.

## Database rule

VeerAI must not receive direct SQL credentials or arbitrary SQL execution. It may receive only evidence returned by approved backend readers. If a database reader is ever required, use a separate read-only database principal restricted to approved views, with query timeout, row limits, network restrictions, and database audit logging.

## Detection

Review application configuration, connection strings, database grants, provider tool definitions, API gateway logs, application audit events, and database audit logs. Any AI path that bypasses the catalog or approved orchestration service is a security defect.
