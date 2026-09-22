# Architecture and environment boundaries

## Application shape

- Angular and TypeScript SPA served by the ASP.NET API.
- ASP.NET 10 Web API with Domain, Application, Infrastructure, and API projects already represented in the solution.
- EF Core with SQL Server locally and Azure SQL as the production direction.
- A single-client database per installation. No cross-client tenant routing is required for MVP, but client ownership remains explicit in the model.
- Mock authentication service backed by seeded users.
- `ICardProvider` adapter whose MVP implementation returns masked PAN, provider token, and safe metadata only.
- Transaction generator called after successful card issuance.
- Append-only client program assignment history and immutable transaction facts with vehicle references/snapshots.

## Aspire boundary

The AppHost is the local development and acceptance-testing composition boundary. It owns:

- SQL Server and database connection wiring;
- the ASP.NET API;
- the Angular SPA;
- service references and environment configuration;
- health checks, logs, traces, and metrics through ServiceDefaults;
- startup seeding of users, client details, cars, programs, and test data where appropriate.

The deployed application uses the same configuration contracts but does not require Aspire at runtime.

## API modules

- `/api/auth`: mocked login, logout, and current user.
- `/api/client`: current client details.
- `/api/cars`: search and car CRUD/archive/restore.
- `/api/cards`: list, issue, assign, block, mark stolen, and archive.
- `/api/transactions`: period-filtered list and totals.
- `/api/programs`: available programs and hypothetical comparisons.
- `/api/client-program`: effective-dated assignment and immutable history.

All command endpoints enforce the seeded user's permission and validate the selected card state. Future program intervals may be edited before activation; effective history is immutable. Correlation IDs are returned for mutation support diagnostics.

## Persistence

Use EF Core migrations and relational constraints for:

- normalized plate uniqueness;
- card state and assignment relationships;
- transaction decimal precision and VAT fields;
- transaction vehicle ID and vehicle registration-plate snapshot fields;
- non-overlapping client program intervals, enforced in application logic and covered by database-aware tests;
- immutable history records.

Seed data must be deterministic enough for acceptance tests. Random transaction generation may use a recorded seed or provider request seed so tests can assert calculation results without relying on incidental randomness.

## Deployment direction

Azure hosting and Terraform/GitHub Actions are retained as follow-up planning. The production target is an ASP.NET-hosted Angular SPA with Azure SQL and Azure-managed secrets/telemetry. Free-tier availability must be checked at implementation time by region and subscription. Production infrastructure is not an MVP acceptance gate.

## Reliability and observability

- Database transactions protect card issuance, generated transactions, and assignment changes.
- Structured logs use correlation IDs and redact card/provider data.
- Health checks distinguish API process health and database readiness.
- Aspire telemetry is sufficient for local acceptance testing; production alerting is a follow-up.