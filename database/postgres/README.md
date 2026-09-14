# PostgreSQL deployment model - v0.6

For Render deployment, PostgreSQL is the runtime database.

The historical SQL Server scripts under `database/001...005` are retained for traceability,
but Render does not execute them.

In v0.6, ASP.NET Core + EF Core uses the Npgsql provider and creates the PostgreSQL schema
from `AppDbContext` / domain models on first startup (`Database.EnsureCreated`).

For production hardening after the demo/pilot phase, replace `EnsureCreated` with formal EF Core
migrations and disable `AUTO_CREATE_DB`.
