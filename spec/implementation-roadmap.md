# Implementation roadmap

This roadmap is ordered so every feature leaves the repository buildable and deployable. Features are intentionally vertical where practical: each slice includes backend, frontend, persistence, authorization, tests, and documentation rather than creating an untested API first.

## Delivery rules

- Keep `main` green after every feature.
- Use a short-lived branch and pull request per feature.
- Every API command has authorization, validation, audit, and integration coverage.
- Every tenant-scoped read and write has a cross-tenant denial test.
- Use migrations for schema changes; never edit production data manually.
- Add a feature to the deployment pipeline only after it works locally through Aspire.
- Prefer contract-first API DTOs and generated TypeScript clients to prevent frontend/backend drift.
- Record architectural decisions in the specification when implementation choices change.

## Feature 00 — Repository and engineering boilerplate

**Outcome:** an empty but professional monorepo with reproducible builds.

- Create solution structure:
  - `src/FleetCards.AppHost` — .NET Aspire orchestrator.
  - `src/FleetCards.ServiceDefaults` — shared service defaults.
  - `src/FleetCards.Api` — ASP.NET 10 API.
  - `src/FleetCards.Worker` — background jobs.
  - `src/FleetCards.Domain`, `Application`, and `Infrastructure`.
  - `web` — Angular TypeScript SPA.
  - `tests` — unit, integration, architecture, and end-to-end test projects.
  - `infra` — Terraform modules and environment configuration.
- Add editorconfig, analyzers, formatting, pinned SDK/package versions, and license/readme metadata.
- Add local commands for restore, build, test, lint, format-check, and Aspire startup.
- Add CI checks without deployment.

**Exit criteria:** a clean checkout restores, builds, runs tests, and starts the placeholder Angular/API resources through Aspire.

## Feature 01 — Local platform and persistence

**Outcome:** local dependencies are reproducible and the API has a durable database.

- Add Aspire PostgreSQL resource and database connection wiring.
- Add EF Core, migrations, health checks, and a development-only database initializer.
- Add Redis only if a measured need emerges; do not make it an MVP dependency.
- Add OpenTelemetry traces, metrics, and structured logs through ServiceDefaults.
- Add a minimal `/health` and `/alive` contract.

**Exit criteria:** `aspire run` starts the API, SPA, PostgreSQL, telemetry, and migrations from a clean machine.

## Feature 02 — Identity and authorization foundation

**Outcome:** secure users can log in and API policies can distinguish operational and client access.

- Add ASP.NET Core Identity, password hashing, lockout, reset tokens, email verification, and session/token strategy.
- Add `OperationalManager`, client membership, `ReadWrite`, and `ReadOnly` policies.
- Add correlation IDs, problem-details errors, validation conventions, and global exception handling.
- Add authorization tests for role, membership, read-only, inactive-user, and unauthenticated paths.
- Add Angular shell, routing, login, logout, session expiry, and route guards.

**Exit criteria:** users can authenticate locally, protected API calls work, and direct API calls cannot bypass read-only or tenant policies.

## Feature 03 — Client self-registration and manager review

**Outcome:** a company can register once per NIP and an operational manager can approve or reject it.

- Implement `Client`, `RegistrationReview`, initial membership, NIP normalization/validation, and uniqueness constraint.
- Add registration API and Angular form with safe duplicate handling.
- Add manager queue, detail view, approve/reject commands, reason capture, and notifications.
- Add audit events for submission, review, approval, rejection, and activation.

**Exit criteria:** duplicate pending/approved NIP registration is rejected; unapproved companies have no fleet/card administration access.

## Feature 04 — Company users and permissions

**Outcome:** approved client representatives can administer company users safely.

- Add membership CRUD, read-write/read-only permission changes, deactivation, and manager correction.
- Implement immediate client-created users according to the confirmed MVP decision.
- Add email notification and first-login/password setup flow.
- Add permission matrix tests and audit events for every membership change.

**Exit criteria:** a representative can add another user; read-only users can view but cannot mutate any company resource.

## Feature 05 — Fleet cars

**Outcome:** each client can maintain a normalized, unique fleet.

- Add `FleetCar`, Polish plate normalization, uniqueness constraint, CRUD, archive/restore, and pagination/search.
- Add Angular fleet screens and form validation.
- Add audit events and historical references needed by future card assignments.

**Exit criteria:** a normalized plate cannot belong to two active clients and archive/restore preserves identity and audit history.

## Feature 06 — Provider-neutral card integration foundation

**Outcome:** card issuance is isolated behind a safe, testable adapter.

- Define `ICardProcessor` and provider DTOs containing token/last-four data only.
- Add mock/sandbox adapter and deterministic failure modes for local tests.
- Add callback authentication, replay protection, provider correlation IDs, and redaction.
- Add `FleetCard`, issuance idempotency, lifecycle state machine, and outbox/worker infrastructure.
- Add contract tests that prove PAN/CVV cannot enter persistence, logs, or API DTOs.

**Exit criteria:** a local provider request can be initiated and completed without storing or returning PAN/CVV.

## Feature 07 — Single-card lifecycle

**Outcome:** operational managers can issue and administer individual virtual cards.

- Add issue, activate, suspend, resume, replace, revoke, and safe-detail endpoints.
- Add manager UI and client card list/detail UI.
- Enforce approved-client, ownership, state-transition, and permission rules.
- Add notification and immutable audit events for lifecycle actions.

**Exit criteria:** every supported transition is tested, terminal revocation is enforced, and replacement links old/new cards.

## Feature 08 — Asynchronous bulk issuance

**Outcome:** managers can issue multiple cards with durable per-item results.

- Add `CardIssuanceBatch` and `CardIssuanceItem`.
- Add queue processing, leases, exponential retry, idempotency, safe error categories, and completed-with-errors state.
- Add batch progress and result UI.
- Add retry-item command that cannot duplicate a successful provider request.

**Exit criteria:** a batch survives worker restart and returns deterministic per-card success/failure results.

## Feature 09 — Fleet Card Programs and pricing

**Outcome:** managers can define effective-dated Polish fuel pricing and client access.

- Add programs, versions, petrol/diesel/LPG rules, pricing model validation, precision/bounds, VAT mode, and overlap constraints.
- Add program CRUD, archive, versioning, and client allowlist UI.
- Add client read-only program list with resolved effective prices.
- Add audit events and tests for historical price resolution.

**Exit criteria:** overlapping versions are rejected; a disallowed program cannot receive a new assignment; existing assignments remain intact.

## Feature 10 — Card assignments

**Outcome:** authorized users can independently assign cards to cars and programs.

- Add effective-dated card/car and card/program assignment aggregates.
- Add client and manager commands with tenant, state, allowlist, and active-entity validation.
- Add assignment history screens and audit entries.
- Add optimistic concurrency to prevent competing assignment updates from silently overwriting each other.

**Exit criteria:** a card may independently have no car, no program, or both; historical assignments remain queryable.

## Feature 11 — Notifications, audit, and operational administration

**Outcome:** the MVP has reliable communication and support-grade traceability.

- Complete outbox-backed email notifications: registration, approval/rejection, user onboarding, issued/revoked cards, and password reset.
- Add paginated audit search for authorized operational managers.
- Add retention configuration/job for seven-year audit records.
- Add dashboards/metrics for issuance, queues, auth abuse, and provider failures.
- Add redaction tests and support correlation-ID workflow.

**Exit criteria:** every required lifecycle action emits one safe notification/audit outcome, and failed delivery is retryable and visible.

## Feature 12 — Azure infrastructure and Terraform

**Outcome:** the single MVP environment is reproducibly deployable.

- Add Terraform bootstrap for remote state with blob locking.
- Add modules for resource group, Static Web Apps, Container Apps, PostgreSQL, Key Vault, managed identity, monitoring, and email configuration.
- Use parameterized SKUs/regions and document free-tier/grant assumptions plus paid fallbacks.
- Add database migration deployment and secret references.
- Add HTTPS, CORS, health probes, minimum network/security settings, and backup configuration.

**Exit criteria:** a clean subscription can be provisioned from Terraform, the API and SPA deploy, migrations complete, and smoke tests pass.

## Feature 13 — MVP hardening and release

**Outcome:** the application is ready for controlled production use.

- Run full unit, integration, API contract, browser E2E, accessibility, load-at-target, and disaster-restore tests.
- Run dependency, secret, container, IaC, and dynamic security scans.
- Perform threat-model review against tenant escape, account takeover, webhook replay, provider abuse, and sensitive logging.
- Add runbooks for incident response, provider outage, restore, failed batches, and compromised credentials.
- Perform a data-retention/privacy review and production configuration review.
- Tag a release and publish deployment/rollback instructions.

**MVP release gate:** all requirements in `requirements.md` pass; no critical/high security findings remain open; Terraform deploy and rollback are tested; the mock provider contract can be replaced by the selected processor adapter without changing domain APIs.

## Dependency graph

```text
00 Boilerplate
  -> 01 Local platform
  -> 02 Identity
  -> 03 Client review
  -> 04 Users
  -> 05 Cars
  -> 06 Card integration
  -> 07 Single-card lifecycle
  -> 08 Bulk issuance
  -> 09 Programs
  -> 10 Assignments
  -> 11 Notifications/audit
  -> 12 Terraform deployment
  -> 13 Hardening/release
```

Features 05 and 06 can be developed in parallel after Feature 04, but Feature 10 requires both. Feature 12 can be bootstrapped earlier, while final deployment wiring depends on Features 11 and 13.

## Requested development tools

### Aspire

Use Aspire as the local composition and observability boundary. The AppHost should own local PostgreSQL, API, worker, SPA, health, environment configuration, and dependency references. Production should consume the same application configuration contracts but not require Aspire to run the deployed services.

### `codebase-memory-mcp`

The requested server should be used to index the repository after Feature 00, then updated after each feature. Capture:

- file/module dependency edges;
- public API and DTO relationships;
- schema migration ownership;
- frontend route-to-API relationships;
- feature-to-test relationships;
- architectural decisions and known constraints.

The CI process should refresh the index after merged changes and fail only on an explicit index/update error, not on an unavailable optional developer tool. Store no secrets or production data in the index.

The Agent Finder lookup did not return an exact `codebase-memory-mcp` match. It returned alternatives for semantic search/context: Sourcegraph MCP, ContextStream MCP, Serena, and xmp4. Do not substitute one automatically; confirm the chosen server and client configuration before wiring it into the repository.
