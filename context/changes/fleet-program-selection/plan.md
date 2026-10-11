# Fleet Program Selection Implementation Plan

## Overview

Show-case fleet-card program selection: four read-only mock programs, a what-if comparison over a past period (ranked by hypothetical gross cost), future-dated program switches or ends by read-write users with an effective-dated assignment history that may contain gaps, program-based discount resolution for newly generated transactions, and an admin panel that generates demo transactions.

Source: [frame.md](./frame.md) (confidence MEDIUM).

## Current State

- `FleetCardProgram` has only `Id/Name/ActiveFrom/ActiveTo` (`src/FleetCards.Domain/FleetCardsModel.cs`); no per-fuel rules, no client-program assignment entity.
- One seeded program "Baltic Standard"; `GET /api/programs` (ReadWrite) returns it; `IFleetCardsReadService.GetPrograms()` is synchronous; Seeded and EF service variants both exist (`FleetCardsInfrastructure.cs`).
- `MockFuelTransactionGenerator` assigns random per-litre discounts 0.05–0.30 at card issuance, with `OccurredAt` backdated into the previous month.
- Net/VAT formulas in `TransactionReportingService.cs`: `Round(qty × (unitPrice − discount), 2, AwayFromZero)`; VAT 23%.
- UI: Programs is a placeholder in `shell.component.html` with an "Add new" button and a single-row table.

## Desired End State

- Four programs (Nebula Basic, Orion Diesel, Andromeda Petrol, Voyager Balanced) with per-fuel percentages; no add/edit.
- Client-level assignment history: non-overlapping effective-dated intervals may contain gaps; client, program and start time are immutable once started. The system may set or revise an active assignment's future end time when a future switch or end is scheduled/replaced. The initial entry = Nebula Basic from the earliest transaction.
- Transaction discount = `round(unitPrice × percent, 4)` from the program active at `OccurredAt`; 0 when none.
- Existing transactions have `Discount = 0` and recomputed totals (one-time migration).
- Programs view: list, history, "Change program" popup (period → ranked comparison → pick program + start date/time, default now + 1 s, future only).
- `/admin/demo` page generates demo transactions for a date range and count.

## What We're NOT Doing

- Program lifecycle/CRUD, third-party sync, propagation delay.
- Feature flags.
- Editing or deleting the client, program or start time of a started assignment, or editing/deleting transactions. The system-managed future end time is the only assignment-history exception.
- Real identity or card-provider changes.

## Key Decisions

- Percent is not stored on transactions; the per-litre `Discount` column is kept.
- What-if uses the report eligibility rules, Europe/Warsaw, presets Last month / Last 30 days / custom. The baseline is the currently calculated transaction totals (including the discounts actually stored, whether or not an assignment exists); each program shows hypothetical net and gross plus savings versus this baseline, ranked by hypothetical gross ascending and program name ascending for ties.
- Assignment intervals are half-open (`StartsAt` inclusive, `EndsAt` exclusive); gaps are valid and transactions in a gap have no program discount.
- A future switch replaces any pending (not-yet-started) assignment and ends the active one at the new start. A separate future end operation replaces any pending assignment and ends the active one without a successor, leaving a gap.
- Once an assignment has started, its client, program and start time cannot be edited or deleted. The system may set or revise its future `EndsAt` only when a read-write user schedules or replaces a future switch or end; once the interval has ended, it is immutable.
- Demo generator: date range plus count (default 10), spread randomly across active cards' vehicles, seed varied per call.
- Negative percent = premium (b: −2% petrol; c: −3% diesel).

## Phase 1: Data model, seed and migration

**Intent:** Introduce program rules and assignments and reseed the programs.
**Contract:**
- Domain: `FleetCardProgramRule` (program, fuel type, percent) and `ClientProgramAssignment` (client, program, `StartsAt`, `EndsAt?`).
- DbContext seeds four programs and rules, replacing "Baltic Standard", plus the initial assignment from the earliest transaction.
- EF migration sets existing transactions' `Discount = 0` and recomputes net/VAT/gross; snapshot updated.
- Seeded in-memory services updated consistently.
- Migration test (Testcontainers) covers the schema, seed and data fix.
**Files:** `src/FleetCards.Domain/FleetCardsModel.cs`; `src/FleetCards.Infrastructure/FleetCardsDbContext.cs` and generated migration/snapshot under `src/FleetCards.Infrastructure/Migrations/`; `src/FleetCards.Application/FleetCardsApplication.cs`; `src/FleetCards.Infrastructure/EfFleetCardsReadService.cs`; `tests/FleetCards.Api.Tests/FleetCardsMigrationTests.cs`.

## Phase 2: Discount resolution

**Intent:** Apply the active program at `OccurredAt` when generating transactions.
**Contract:**
- A program-resolution service in Application (`GetPercent(clientId, fuelType, occurredAt)`).
- `MockFuelTransactionGenerator` uses it instead of random discounts and accepts an optional period.
- Both the EF issuance flow and the Seeded path use it.
- Unit tests cover covered, uncovered and boundary instants.
**Files:** new `src/FleetCards.Application/ProgramResolutionService.cs`; `src/FleetCards.Application/MockFuelTransactionGenerator.cs`; `src/FleetCards.Application/FleetCardsApplication.cs` and `src/FleetCards.Infrastructure/EfFleetCardsReadService.cs` for both resolution data paths; `src/FleetCards.Infrastructure/EfFleetCardService.cs`; `src/FleetCards.Infrastructure/SeededFleetCardService.cs`; `src/FleetCards.Infrastructure/FleetCardsInfrastructure.cs`; existing `tests/FleetCards.Application.Tests/FleetCardServiceTests.cs` and new `tests/FleetCards.Application.Tests/ProgramResolutionTests.cs`.

## Phase 3: Comparison API

**Intent:** Ranked what-if cost per program.
**Contract:**
- `GET /api/programs/comparison?from&to` (ReadWrite).
- Reuses the report eligibility rules. Program percentages are fractional decimals (for example, 10% is `0.10`). For each eligible transaction and program, calculate `Discount = round(UnitPrice × percent, 4, AwayFromZero)`, `Net = round(Quantity × (UnitPrice − Discount), 2, AwayFromZero)`, `VAT = round(Net × transaction.VatRate, 2, AwayFromZero)`, and `Gross = Net + VAT`.
- Include the baseline as the sum of each eligible transaction's currently stored net, VAT and gross, using the report's normal eligibility rules. This is the actual current cost, not a hypothetical zero-discount POS price.
- For each program, return hypothetical net and gross and net/gross savings versus the baseline. Savings = baseline amount − hypothetical amount; a negative value means the program would cost more than the current transactions.
- Sum transaction-level net and gross by program and rank by aggregate hypothetical gross ascending, then program name ascending.
- Response is sorted by gross ascending, then program name ascending to break ties deterministically.
- Contracts DTOs; tests for actual baseline, hypothetical amounts, net/gross savings, negative savings and the empty period, including deterministic ordering when totals tie.
**Files:** new `src/FleetCards.Application/ProgramComparisonService.cs`; `src/FleetCards.Contracts/Models.cs`; `src/FleetCards.Api/Program.cs`; `src/FleetCards.Infrastructure/FleetCardsInfrastructure.cs`; new `tests/FleetCards.Application.Tests/ProgramComparisonTests.cs` and `tests/FleetCards.Api.Tests/ProgramApiTests.cs`.

## Phase 4: Assignment API

**Intent:** Read history and schedule a switch.
**Contract:**
- `GET /api/programs` returns the programs with rules and the current assignment.
- `GET /api/programs/assignments` returns the history.
- `POST /api/programs/assignments` takes `{programId, startsAt}`. It is ReadWrite plus CSRF and audited.
- `POST /api/programs/assignments/end` takes `{endsAt}`. It is ReadWrite plus CSRF and audited.
- Validation: a switch start and an end time must be in the future, and resulting assignment intervals must not overlap. A switch replaces a pending assignment and starts the chosen program at that instant. An end operation replaces a pending assignment, ends the active assignment at that instant, and creates no successor.
- Tests cover the 400/403 cases, overlap/replace rules, and an uncovered interval after an end without a successor.
**Files:** new `src/FleetCards.Application/ProgramAssignmentService.cs` and `src/FleetCards.Infrastructure/EfProgramAssignmentService.cs`; `src/FleetCards.Application/FleetCardsApplication.cs` and `src/FleetCards.Infrastructure/EfFleetCardsReadService.cs` for Seeded and EF program reads; `src/FleetCards.Infrastructure/FleetCardsInfrastructure.cs`; `src/FleetCards.Api/Program.cs`; `src/FleetCards.Contracts/Models.cs`; new `tests/FleetCards.Application.Tests/ProgramAssignmentTests.cs` and `tests/FleetCards.Api.Tests/ProgramApiTests.cs`.

## Phase 5: Programs UI

**Intent:** Replace the placeholder with the real view.
**Contract:**
- Remove the "Add new" button on Programs.
- Program list, history table, and a "Change program" popup (period presets/custom → show the current actual net/gross baseline and each program's hypothetical net/gross plus savings → select a program or schedule an end without a successor → date/time → confirm); show unassigned gaps in history.
- A service using HttpClient; EN/PL strings; design tokens and shared primitives only.
- Vitest specs.
**Files:** `web/src/app/app.routes.ts`; `web/src/app/shell.component.ts` and `web/src/app/shell.component.html`; new `web/src/app/programs.component.ts`, `programs.component.html`, `programs.component.scss`, `programs.component.spec.ts`, and `programs.service.ts`; `web/src/app/language.service.ts`.

## Phase 6: Demo generator

**Intent:** Cuttable slice that creates demo data.
**Contract:**
- `POST /api/admin/demo-transactions` `{from, to, count}` (ReadWrite, CSRF, audited); the generator resolves discounts by `OccurredAt`.
- `/admin/demo` route (read-write guard), nav item and form; EN/PL strings.
- API and Vitest tests.
**Files:** `src/FleetCards.Application/MockFuelTransactionGenerator.cs`; `src/FleetCards.Api/Program.cs`; `src/FleetCards.Contracts/Models.cs`; `web/src/app/app.routes.ts`, `shell.component.ts`, and `language.service.ts`; new `web/src/app/admin-demo.component.ts`, `admin-demo.component.html`, `admin-demo.component.scss`, and `admin-demo.component.spec.ts`; new `tests/FleetCards.Api.Tests/DemoTransactionApiTests.cs`.

## Phase 7: E2E and snapshots

**Intent:** Prove the flow end to end.
**Contract:**
- One Playwright test: generate demo data, compare, schedule an end without a successor, and see the resulting history gap.
- Update the Programs visual snapshots.
- Run the full `dotnet test` and the web tests.
**Files:** new `web/e2e/program-selection.spec.ts`; `web/e2e/portal-visual.spec.ts`; Programs snapshots under `web/e2e/portal-visual.spec.ts-snapshots/`.

## Progress

### Phase 1: Data model, seed and migration

#### Automated

- [x] 1.1 Domain entities and DbContext seed — 1e86984
- [x] 1.2 Migration with zero-discount data fix and snapshot — 1e86984
- [x] 1.3 Seeded services updated and migration test — 1e86984

### Phase 2: Discount resolution

#### Automated

- [x] 2.1 Program resolution service — 3439945
- [x] 2.2 Generator and issuance flows use it, with tests — 3439945

### Phase 3: Comparison API

#### Automated

- [x] 3.1 Comparison service and endpoint with tests — 17ce576

### Phase 4: Assignment API

#### Automated

- [x] 4.1 Programs/history endpoints — 4d901a7
- [x] 4.2 Switch endpoint with validation and tests — 4d901a7
- [x] 4.3 Schedule assignment end without successor, with validation and tests — 4d901a7

### Phase 5: Programs UI

#### Automated

- [x] 5.1 Programs list, history and removal of Add new — 8b02e4f
- [x] 5.2 Change-program popup and specs — 8b02e4f
- [x] 5.3 Schedule an unassigned period from Programs UI and specs — 8b02e4f

### Phase 6: Demo generator

#### Automated

- [x] 6.1 Demo generator endpoint with tests
- [x] 6.2 Admin demo page, route and nav

### Phase 7: E2E and snapshots

#### Automated

- [ ] 7.1 Playwright e2e and snapshot updates
- [ ] 7.2 Full test run green
