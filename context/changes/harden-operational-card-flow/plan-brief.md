# Harden Operational Card Flow — Plan Brief

> Full plan: `context/changes/harden-operational-card-flow/plan.md`

## What & Why

S-06 will turn the current FleetCards implementation into a dependable composed MVP demonstration. It aligns the written contract with the newly settled card and transaction behavior, closes the remaining Aspire acceptance gaps, and makes the local versus deployed migration boundary explicit.

The release gate will validate acceptance criteria `1-9` and `11`; program comparison criterion `10` remains parked because it is not part of the operational card flow.

## Starting Point

The repository already has a unique-database Playwright/AppHost harness, SQL-backed migration coverage, API authorization and redaction tests, and browser tests for card lifecycle and transaction reporting. The main gaps are stale documentation, incomplete exact-count coverage, and automatic migration behavior that is not distinguished between disposable local environments and deployed environments.

## Desired End State

A clean Aspire run starts SQL Server, the API, Angular, and telemetry with isolated deterministic data. A ReadWrite user can complete the vehicle-to-card-to-spending journey, a ReadOnly user can inspect but not mutate it, and the browser/API evidence proves card assignment, state history, transaction count, VAT, gross savings, localization, and period synchronization.

The repository clearly documents 5-15 generated transactions, archive rather than return, clean test-data setup, controlled deployed migrations, and rollback limitations.

## Key Decisions Made

| Decision | Choice | Why (1 sentence) | Source |
| --- | --- | --- | --- |
| Generated transaction count | 5-15 previous-month transactions | This is the current acceptance contract and generator behavior. | Plan |
| Card lifecycle | Archive, no return operation | Archive preserves card and transaction history while return is outside the MVP. | Plan / Foundation |
| S-06 release gate | Acceptance criteria 1-9 and 11 | It covers the operational flow and excludes parked program work. | Plan |
| Quality checks | Critical checks block; accessibility/architecture findings are reported | Security and data integrity need a hard gate, while undefined quality thresholds should not block delivery. | Plan |
| Local migration | Automatic for local and isolated E2E environments | Disposable environments benefit from repeatable startup. | Plan |
| Deployed migration | Controlled workflow step before application deployment | The destructive migration must not run silently at API startup. | Plan |
| Composed test data | Disposable database per E2E run | Unique SQL databases prevent state leakage between acceptance runs. | Research / Plan |

## Scope

**In scope:**

- Align foundation, roadmap, and demo documentation with the 5-15, archive, and S-06 gate decisions.
- Extend existing Playwright, API, and migration coverage for the remaining operational acceptance gaps.
- Make startup migration configuration explicit for local/E2E versus deployed environments.
- Document clean local demonstration, controlled deployment migration, smoke testing, and rollback limitations.

**Out of scope:**

- Fleet-card program comparison or assignment.
- Card return, real identity, production password lifecycle, or multi-client tenancy.
- Rebuilding the existing Aspire, Playwright, or Testcontainers infrastructure.
- Inventing blocking architecture or accessibility standards.

## Architecture / Approach

Reuse the existing flow: Playwright starts `FleetCards.AppHost` with a generated `fleetcards_e2e_<hex>` database name; Aspire starts session-lifetime SQL Server, the API, and Angular; EF migrations seed deterministic data; API and browser tests verify the contracts. Local/E2E startup migration remains enabled, while the deployment workflow applies the generated SQL migration before deploying the API archive and deployed API startup migration is guarded.

## Phases at a Glance

| Phase | What it delivers | Key risk |
| --- | --- | --- |
| 1. Align MVP and Release Contracts | Corrects stale requirements, roadmap references, and demo instructions. | Documentation can drift again if acceptance ownership is unclear. |
| 2. Complete Composed Acceptance Coverage | Adds the missing exact-count and operational regression evidence. | Existing serial browser tests may expose hidden state coupling. |
| 3. Make Migration and Demonstration Operations Explicit | Separates local automatic setup from controlled deployed schema changes. | A configuration mistake could leave deployed startup unable to initialize its schema. |

**Prerequisites:** S-05 behavior and tests remain available; Docker, .NET 10, Aspire CLI, Node 22+, npm dependencies, and Playwright Chromium are installed for composed verification.

**Estimated effort:** Approximately 2-3 focused sessions across three phases.

## Open Risks & Assumptions

- The deployed demo contains synthetic data only; no real client data or payment credentials are in scope.
- The architecture test project remains a smoke test unless a concrete layering rule is added without broadening the change.
- Application rollback cannot undo an applied database migration or reconstruct deleted legacy facts.

## Success Criteria (Summary)

- `dotnet test FleetCards.sln --configuration Release`, `npm test`, `npm run build`, and `npm run test:e2e` pass.
- The composed flow verifies acceptance criteria `1-9` and `11`, including 5-15 transactions, card-history integrity, redaction, gross savings, and bilingual period synchronization.
- Operators can distinguish disposable local setup from controlled deployed migration and recovery procedures.
