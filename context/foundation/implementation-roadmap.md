# Implementation roadmap

The roadmap is intentionally vertical. Every slice keeps the solution buildable and is verified through the Aspire composition where the behavior crosses service boundaries.

## 00. Repository and Aspire foundation

- Keep the existing API, Domain, Application, Infrastructure, Contracts, ServiceDefaults, AppHost, Angular, and test projects.
- Configure AppHost for API, Angular SPA, SQL Server, and telemetry.
- Add restore/build/test/lint commands and health checks.

**Exit criteria:** a clean local environment starts the composed stack and exposes health endpoints.

## 01. Relational persistence and deterministic seed data

- Add EF Core SQL Server provider, migrations, database initializer, and local connection wiring.
- Seed one Polish client, read-write/read-only users, cars, programs, and safe demo data.
- Make mock generation deterministic for acceptance tests.

**Exit criteria:** Aspire creates the database and repeats the same seed setup safely.

## 02. Mock authentication and bilingual shell

- Implement seeded username/password login, logout, current-user endpoint, and session handling.
- Add Angular routing, language switching, client details, and permission-aware navigation.
- Add API authorization tests for authenticated, unauthenticated, read-only, and read-write users.

**Exit criteria:** both seeded permission levels work and all MVP shell views have Polish and English text.

## 03. Fleet cars

- Implement normalized plate CRUD, search, archive/restore, and validation.
- Add Angular fleet list and edit flow.
- Add assignment-safe archive rules and audit events.

**Exit criteria:** car behavior and permission rules pass API and browser acceptance tests.

## 04. Mock card issuing and lifecycle

- Define `ICardProvider` and a mock implementation that returns masked PAN and token only.
- Implement issue, list, assign, block, stolen, and archive operations.
- Add unrestricted card state selection, redaction tests, and card/car assignment history.

**Exit criteria:** issuing a card creates one local card without sensitive PAN data and every supported card state can be selected without losing history.

## 05. Generated transactions and VAT

- Generate and persist 1-5 previous-month transactions after issuance.
- Implement decimal precision, VAT fields, transaction status/version metadata, filtering, totals, and UI views.
- Add deterministic calculation tests for discounted and non-discounted totals.

**Exit criteria:** users can inspect transactions and period totals with explicit net/gross/VAT presentation.

## 06. Programs and client assignment timeline

- Import/seed read-only program JSON.
- Implement hypothetical comparison by fuel type, effective period, VAT basis, and rounding policy.
- Implement future assignment, editable start/end timestamps before activation, replacement, removal, overlap rejection, and immutable effective history.
- Add UI for comparison and client-level selection.

**Exit criteria:** at most one program resolves at every instant and historical choices remain unchanged.

## 07. Acceptance hardening

- Run end-to-end tests through Aspire for login, cars, cards, transactions, program comparison, and program switching.
- Add architecture tests, migration checks, sensitive-data scans, and accessibility checks for the core workflows.
- Document seeded accounts, local startup, test data reset, and known MVP limitations.

**Exit criteria:** all requirements in `requirements.md` pass locally through Aspire.

## 08. Deferred deployment planning

- Document Azure App Service or equivalent hosting, Azure SQL, secrets, and telemetry choices.
- Add Terraform/GitHub Actions only after the local MVP contract is stable.
- Keep production deployment outside the MVP release gate unless explicitly re-scoped.