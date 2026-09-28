# Harden Operational Card Flow Implementation Plan

## Overview

Harden the completed FleetCards operational journey at its composition boundary. The change will align the roadmap and demo documentation with the settled MVP contract, complete the missing Aspire acceptance assertions, and make local versus deployed migration behavior explicit.

## Current State Analysis

The repository already has focused API tests, Angular tests, and a Playwright harness that starts Aspire with a unique SQL database per run. The card E2E test covers authentication, card issuance, immutable assignment, state history, refresh behavior, redaction, and ReadOnly mutation denial; the transaction E2E test covers report/summary reconciliation, gross savings, month navigation, timezone handling, empty periods, and ReadOnly reads.

The remaining gaps are contract and release-gate gaps rather than a missing test framework:

- The roadmap still describes S-06 as checking acceptance criteria `1-6, 10`, omitting the S-05 card/report guarantees and Aspire criterion `11` while including parked program work (`context/foundation/roadmap.md`).
- The product specification and older roadmap text still say generated transactions are `1-5`, while the current requirement and generator contract are `5-15` (`context/foundation/product-specification.md`, `context/foundation/requirements.md`, `src/FleetCards.Application/MockFuelTransactionGenerator.cs`).
- The product specification and roadmap still contain card-return wording even though archive is the selected MVP action (`context/foundation/product-specification.md`, `context/foundation/decisions.md`).
- `FleetCards.Api` applies EF migrations automatically whenever a database connection exists, while the deployment workflow also applies the generated migration script (`src/FleetCards.Api/Program.cs`, `.github/workflows/main.yml`).
- The architecture test project is currently only a loadability smoke test (`tests/FleetCards.Architecture.Tests/FoundationTests.cs`), so S-06 must not invent blocking architecture rules.

## Desired End State

A clean Aspire run creates an isolated database, starts SQL Server, API, Angular, and telemetry, and proves the full operational flow for both seeded roles. The release gate covers acceptance criteria `1-9` and `11`; program comparison criterion `10` remains parked.

The repository documents 5-15 generated previous-month transactions, archive rather than card return, deterministic reset/seed behavior, and the distinction between automatic local/E2E migrations and controlled deployed migrations. Critical authentication, authorization, data-integrity, migration, and sensitive-data failures block completion; accessibility and architecture results are reported without undefined thresholds.

### Key Discoveries:

- `web/playwright.config.ts` already generates a fresh database name and starts AppHost with `aspire run --isolated`.
- `src/FleetCards.AppHost/AppHost.cs` validates the E2E database name, uses session-lifetime SQL, and passes the isolated database to the API.
- `web/e2e/card-management.spec.ts` and `web/e2e/transaction-report.spec.ts` already provide most of the required composed-flow coverage.
- `tests/FleetCards.Api.Tests/FleetCardsMigrationTests.cs` already proves that the assignment-required migration removes only unassigned-card facts and preserves assigned-card facts.
- `docs/deployment/runbook.md` already applies the deployed migration script explicitly, but the API startup behavior and documentation do not yet state that deployed control boundary.

## What We're NOT Doing

- Implementing fleet-card program comparison or assignment; criterion `10` remains parked.
- Adding card return; archive is the MVP lifecycle action.
- Rebuilding the Playwright, Testcontainers, or Aspire harness.
- Adding a real identity provider, production password lifecycle, or multi-client tenancy.
- Making undefined architecture or accessibility standards blocking release criteria.
- Changing the settled transaction calculation, gross-savings formula, issue-time assignment invariant, or card state model.

## Implementation Approach

First align the foundation and demo contract documents with the settled behavior. Then extend the existing composed acceptance suite only where it does not yet prove the release gate, including the exact 5-15 transaction count and complete report/overview reconciliation. Finally make migration execution mode explicit in configuration and deployment documentation, run the full local verification commands, and record architecture/accessibility findings separately from blocking gates.

## Critical Implementation Details

### Timing & lifecycle

The destructive assignment-required migration may run automatically for local and E2E databases because those environments are disposable or explicitly controlled. Deployed environments must apply the generated migration script in the deployment workflow after the existing migration/backup control, and the API must not silently apply a newer destructive migration at startup.

### Debug & observability

Sensitive-data verification must inspect API responses, persisted EF entities, captured application logs, and transaction/report payloads. Assertions must use sentinel PAN/CVV/PIN values and prove their absence without logging the sensitive fixtures themselves.

## Phase 1: Align MVP and Release Contracts

### Overview

Update foundation, roadmap, and local/deployment documentation so the written contract matches the implemented S-05 behavior and the approved S-06 release gate.

### Changes Required:

#### 1. Foundation and roadmap alignment

**File**: `context/foundation/requirements.md`, `context/foundation/product-specification.md`, `context/foundation/decisions.md`, `context/foundation/roadmap.md`

**Intent**: Remove stale card-return and 1-5 transaction statements and make S-06 point at the operational acceptance surface that now exists.

**Contract**: Use 5-15 persisted previous-month transactions as the canonical requirement; use archive and explicitly exclude return; update S-03/S-04 descriptions where they still describe superseded behavior; set S-06 references to acceptance criteria `1-9` and `11`, excluding parked program criterion `10`; preserve the immutable assignment, state-history, gross-savings, and migration-cleanup decisions.

#### 2. Demo and local verification documentation

**File**: `web/README.md`, `docs/deployment/runbook.md`, `docs/deployment/demo-constraints.md`

**Intent**: Give a developer or reviewer one accurate local demonstration contract and distinguish disposable E2E setup from deployed migration operations.

**Contract**: Document the seeded accounts, clean isolated E2E database behavior, 5-15 transaction expectation, archive lifecycle, blocking security/data checks, non-blocking architecture/accessibility reporting, and the exact commands for .NET, Angular, and Playwright verification.

### Success Criteria:

#### Automated Verification:

- Repository documentation contains no operational S-06 claim that contradicts the approved 5-15 transaction, archive, or acceptance-criteria contract.
- The roadmap identifies S-06 as the gate for acceptance criteria `1-9` and `11`, with program criterion `10` still parked.

#### Manual Verification:

- A reviewer can follow the local runbook from a clean checkout, identify both seeded accounts, understand the disposable test-data boundary, and distinguish local automatic migrations from deployed controlled migrations.

## Phase 2: Complete Composed Acceptance Coverage

### Overview

Close the remaining executable gaps in the existing Aspire/Playwright and SQL-backed test suites without introducing a second test harness.

### Changes Required:

#### 1. Operational card-flow browser coverage

**File**: `web/e2e/card-management.spec.ts`, `web/e2e/transaction-report.spec.ts`, `web/e2e/fleet-vehicles.spec.ts`

**Intent**: Make the browser suite prove the full operational gate, including the approved generated-transaction count and cross-view financial reconciliation.

**Contract**: Assert each newly issued card has 5-15 persisted previous-month transactions; retain checks for issue-time vehicle snapshots, state-history ordering, refreshed state selection, all supported card states, redaction, ReadOnly visibility/denial, bilingual labels, and month synchronization; ensure the transaction report and overview totals, including gross savings, reconcile for the same selected period.

#### 2. Migration, authorization, and sensitive-data regression coverage

**File**: `tests/FleetCards.Api.Tests/FleetCardsMigrationTests.cs`, `tests/FleetCards.Api.Tests/CardApiTests.cs`, `tests/FleetCards.Api.Tests/PlatformTests.cs`

**Intent**: Keep critical release-gate behavior executable at the API and database boundaries, independent of browser behavior.

**Contract**: Preserve and run the SQL-backed test that deletes only null-assignment legacy-card facts and makes `AssignedCarId` required; cover anonymous denial, both seeded roles, ReadOnly mutation rejection, CSRF protection, state validation, migration cleanliness, and absence of full PAN/CVV/PIN in responses, persisted entities, and logs.

#### 3. Architecture and accessibility evidence

**File**: `tests/FleetCards.Architecture.Tests/FoundationTests.cs`, `web/e2e/` or focused Angular component specs as needed

**Intent**: Produce useful evidence for architecture and accessibility without turning undefined quality thresholds into blockers.

**Contract**: Keep the existing architecture smoke test unless a concrete layering assertion can be added without inventing a new standard; add focused accessibility assertions for accessible names, keyboard-reachable core controls, and absence of obvious blocking violations where the existing UI/test tooling supports them; report findings separately from critical release failures.

### Success Criteria:

#### Automated Verification:

- `dotnet test FleetCards.sln --configuration Release` passes, including migration, authorization, lifecycle, and redaction tests.
- From `web/`, `npm test`, `npm run build`, and `npm run test:e2e` pass against the isolated Aspire runtime.
- The composed browser suite proves the full operational gate for acceptance criteria `1-9` and `11`, including 5-15 generated transactions and report/overview reconciliation.
- Any architecture or accessibility checks produce explicit results without being treated as a pass merely because the test project is loadable.

#### Manual Verification:

- In English, complete sign-in, vehicle creation, card issuance, state changes, reload, state-history inspection, and spending review as ReadWrite; confirm the vehicle and transaction snapshots remain unchanged.
- Switch to Polish and confirm the same card, state-history, overview, and transaction-report evidence remains understandable and period-aligned.
- Sign in as ReadOnly and confirm visibility remains available while card and vehicle commands are unavailable.

## Phase 3: Make Migration and Demonstration Operations Explicit

### Overview

Separate disposable local/E2E migration behavior from deployed migration execution and finish the handoff documentation for repeatable demonstration and recovery.

### Changes Required:

#### 1. Migration execution mode

**File**: `src/FleetCards.Api/Program.cs`, `src/FleetCards.Api/appsettings.json`, `src/FleetCards.Api/appsettings.Development.json`, `.github/workflows/main.yml`

**Intent**: Prevent deployed API startup from silently applying a destructive or otherwise unreviewed migration while preserving automatic setup for local and E2E environments.

**Contract**: Introduce an explicit environment/configuration decision for startup migration; enable it for Development and validated E2E runs, disable or fail closed for deployed environments, and keep the workflow's generated idempotent SQL migration step before application deployment as the deployed schema authority.

#### 2. Runbook and release evidence

**File**: `docs/deployment/runbook.md`, `docs/deployment/demo-constraints.md`, `web/README.md`

**Intent**: Document the operational sequence and its recovery boundary so a demo release cannot confuse application rollback with database rollback.

**Contract**: State that deployed migrations require the controlled workflow step and backup/approval decision, that application rollback does not reverse schema changes, that local E2E databases are disposable, and that the MVP contains synthetic data only; include the exact release verification commands and expected health/auth smoke checks.

### Success Criteria:

#### Automated Verification:

- A configuration-focused test or startup check proves local/validated E2E automatic migration is enabled and deployed configuration does not silently migrate the database.
- The deployment workflow still generates and applies the idempotent migration script before deploying the application archive, and its smoke test validates API, authentication, and client-data access.
- A clean E2E run leaves no pending migrations and uses only its generated database name.

#### Manual Verification:

- Review the deployment workflow and runbook together and confirm the sequence is understandable: provision, controlled migration, deploy, smoke test, and rollback limitations.
- Perform one clean local demonstration using the documented commands and verify the seeded users and synthetic-data limitation are visible to the operator.

## Testing Strategy

### Unit Tests:

- Reuse existing transaction-reporting, card-state, authorization, and generator tests.
- Add or retain the exact 5-15 generator-bound test at the service/application boundary.
- Keep architecture validation limited to rules that are explicitly defined and executable.

### Integration Tests:

- Use the existing SQL-backed migration test to verify destructive cleanup scope and required assignment nullability.
- Use the existing `WebApplicationFactory` API tests for authentication, authorization, CSRF, state history, and redaction.
- Use the existing Playwright/AppHost setup for the composed card, vehicle, transaction, language, and period-navigation journey.

### Manual Testing Steps:

1. Start the local composed runtime from a clean environment and sign in as the ReadWrite user.
2. Create or select an active vehicle, issue a card, confirm 5-15 previous-month transactions, change card state, reload, and inspect state history.
3. Compare overview and transaction-report totals for the same month, including gross savings, then repeat the visible flow in Polish.
4. Sign in as ReadOnly and confirm reads work while direct mutation attempts are rejected.
5. Review the deployed migration and rollback instructions without applying a destructive migration manually to production data.

## Performance Considerations

The MVP has low volume and the existing card-scoped and selected-period queries are bounded by the single-client installation. The E2E suite remains serial with one worker and one disposable database per run to avoid cross-test state races; no caching or load-testing work is required for S-06.

## Migration Notes

The assignment-required migration remains forward-only in its data effect for null-assignment legacy cards. Local and E2E startup may apply it automatically because those databases are disposable or explicitly isolated. Deployed environments apply the generated idempotent script through the controlled workflow before application deployment; application rollback does not reconstruct deleted facts or reverse schema changes.

## References

- `context/foundation/requirements.md` — current acceptance criteria and card/transaction invariants.
- `context/foundation/decisions.md` — locked archive, immutable assignment, savings, and migration-cleanup decisions.
- `context/foundation/roadmap.md` — S-06 roadmap contract and stale predecessor wording to align.
- `src/FleetCards.AppHost/AppHost.cs` — local and E2E service composition.
- `src/FleetCards.Api/Program.cs` — current automatic migration startup behavior.
- `tests/FleetCards.Api.Tests/FleetCardsMigrationTests.cs` — SQL-backed migration preservation test.
- `tests/FleetCards.Api.Tests/CardApiTests.cs` and `tests/FleetCards.Api.Tests/PlatformTests.cs` — API security, lifecycle, and redaction coverage.
- `web/playwright.config.ts`, `web/e2e/card-management.spec.ts`, and `web/e2e/transaction-report.spec.ts` — composed browser acceptance harness.
- `docs/deployment/runbook.md` and `web/README.md` — operational and local verification documentation.

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append ` — <commit sha>` when a step lands. Do not rename step titles.

### Phase 1: Align MVP and Release Contracts

#### Automated

- [x] 1.1 Repository documentation contains no operational S-06 claim that contradicts the approved 5-15 transaction, archive, or acceptance-criteria contract. — 354c0e2
- [x] 1.2 The roadmap identifies S-06 as the gate for acceptance criteria `1-9` and `11`, with program criterion `10` still parked. — 354c0e2

#### Manual

- [x] 1.3 A reviewer can follow the local runbook from a clean checkout, identify both seeded accounts, understand the disposable test-data boundary, and distinguish local automatic migrations from deployed controlled migrations. — 354c0e2

### Phase 2: Complete Composed Acceptance Coverage

#### Automated

- [x] 2.1 `dotnet test FleetCards.sln --configuration Release` passes, including migration, authorization, lifecycle, and redaction tests. — 990a81e
- [x] 2.2 From `web/`, `npm test`, `npm run build`, and `npm run test:e2e` pass against the isolated Aspire runtime. — 990a81e
- [x] 2.3 The composed browser suite proves the full operational gate for acceptance criteria `1-9` and `11`, including 5-15 generated transactions and report/overview reconciliation. — 990a81e
- [x] 2.4 Any architecture or accessibility checks produce explicit results without being treated as a pass merely because the test project is loadable. — 990a81e

#### Manual

- [x] 2.5 In English, complete sign-in, vehicle creation, card issuance, state changes, reload, state-history inspection, and spending review as ReadWrite; confirm the vehicle and transaction snapshots remain unchanged. — 990a81e
- [x] 2.6 Switch to Polish and confirm the same card, state-history, overview, and transaction-report evidence remains understandable and period-aligned. — 990a81e
- [x] 2.7 Sign in as ReadOnly and confirm visibility remains available while card and vehicle commands are unavailable. — 990a81e

### Phase 3: Make Migration and Demonstration Operations Explicit

#### Automated

- [x] 3.1 A configuration-focused test or startup check proves local/validated E2E automatic migration is enabled and deployed configuration does not silently migrate the database. — 5780c4d
- [x] 3.2 The deployment workflow still generates and applies the idempotent migration script before deploying the application archive, and its smoke test validates API, authentication, and client-data access. — 5780c4d
- [x] 3.3 A clean E2E run leaves no pending migrations and uses only its generated database name. — 5780c4d

#### Manual

- [x] 3.4 Review the deployment workflow and runbook together and confirm the sequence is understandable: provision, controlled migration, deploy, smoke test, and rollback limitations. — 5780c4d
- [x] 3.5 Perform one clean local demonstration using the documented commands and verify the seeded users and synthetic-data limitation are visible to the operator. — 5780c4d
