# Fleet Program Selection Implementation Plan

## Overview

Show-case fleet-card program selection: four read-only mock programs, a what-if comparison over a past period (ranked by hypothetical gross cost), future-dated program switches by read-write users with an immutable assignment history, program-based discount resolution for newly generated transactions, and an admin panel that generates demo transactions.

Source: [frame.md](./frame.md) (confidence MEDIUM).

## Current State

- `FleetCardProgram` has only `Id/Name/ActiveFrom/ActiveTo` (`src/FleetCards.Domain/FleetCardsModel.cs`); no per-fuel rules, no client-program assignment entity.
- One seeded program "Baltic Standard"; `GET /api/programs` (ReadWrite) returns it; `IFleetCardsReadService.GetPrograms()` is synchronous; Seeded and EF service variants both exist (`FleetCardsInfrastructure.cs`).
- `MockFuelTransactionGenerator` assigns random per-litre discounts 0.05–0.30 at card issuance, with `OccurredAt` backdated into the previous month.
- Net/VAT formulas in `TransactionReportingService.cs`: `Round(qty × (unitPrice − discount), 2, AwayFromZero)`; VAT 23%.
- UI: Programs is a placeholder in `shell.component.html` with an "Add new" button and a single-row table.

## Desired End State

- Four programs (Nebula Basic, Orion Diesel, Andromeda Petrol, Voyager Balanced) with per-fuel percentages; no add/edit.
- Client-level assignment history: non-overlapping, effective-dated, immutable once started; initial entry = Nebula Basic from the earliest transaction.
- Transaction discount = `round(unitPrice × percent, 4)` from the program active at `OccurredAt`; 0 when none.
- Existing transactions have `Discount = 0` and recomputed totals (one-time migration).
- Programs view: list, history, "Change program" popup (period → ranked comparison → pick program + start date/time, default now + 1 s, future only).
- `/admin/demo` page generates demo transactions for a date range and count.

## What We're NOT Doing

- Program lifecycle/CRUD, third-party sync, propagation delay.
- Feature flags.
- Editing or deleting started assignments or transactions.
- Real identity or card-provider changes.

## Key Decisions

- Percent is not stored on transactions; the per-litre `Discount` column is kept.
- What-if uses the report eligibility rules, Europe/Warsaw, presets Last month / Last 30 days / custom; net and gross shown, ranked by gross ascending.
- A new switch replaces any pending (not-yet-started) assignment and ends the active one at the new start; start must be in the future.
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

## Phase 2: Discount resolution

**Intent:** Apply the active program at `OccurredAt` when generating transactions.
**Contract:**
- A program-resolution service in Application (`GetPercent(clientId, fuelType, occurredAt)`).
- `MockFuelTransactionGenerator` uses it instead of random discounts and accepts an optional period.
- Both the EF issuance flow and the Seeded path use it.
- Unit tests cover covered, uncovered and boundary instants.

## Phase 3: Comparison API

**Intent:** Ranked what-if cost per program.
**Contract:**
- `GET /api/programs/comparison?from&to` (ReadWrite).
- Reuses the report eligibility logic; per program, net and gross are computed from `qty × unitPrice × (1 − percent)`.
- Response is sorted by gross ascending.
- Contracts DTOs; tests for the numbers and the empty period.

## Phase 4: Assignment API

**Intent:** Read history and schedule a switch.
**Contract:**
- `GET /api/programs` returns the programs with rules and the current assignment.
- `GET /api/programs/assignments` returns the history.
- `POST /api/programs/assignments` takes `{programId, startsAt}`. It is ReadWrite plus CSRF and audited.
- Validation: the start must be in the future, must not overlap started periods, and replaces a pending assignment.
- Tests cover the 400/403 cases and the overlap/replace rules.

## Phase 5: Programs UI

**Intent:** Replace the placeholder with the real view.
**Contract:**
- Remove the "Add new" button on Programs.
- Program list, history table, and a "Change program" popup (period presets/custom → ranked results → select, date/time → confirm).
- A service using HttpClient; EN/PL strings; design tokens and shared primitives only.
- Vitest specs.

## Phase 6: Demo generator

**Intent:** Cuttable slice that creates demo data.
**Contract:**
- `POST /api/admin/demo-transactions` `{from, to, count}` (ReadWrite, CSRF, audited); the generator resolves discounts by `OccurredAt`.
- `/admin/demo` route (read-write guard), nav item and form; EN/PL strings.
- API and Vitest tests.

## Phase 7: E2E and snapshots

**Intent:** Prove the flow end to end.
**Contract:**
- One Playwright test: generate demo data, compare, switch, see the history.
- Update the Programs visual snapshots.
- Run the full `dotnet test` and the web tests.

## Progress

- [ ] 1.1 Domain entities and DbContext seed
- [ ] 1.2 Migration with zero-discount data fix and snapshot
- [ ] 1.3 Seeded services updated and migration test
- [ ] 2.1 Program resolution service
- [ ] 2.2 Generator and issuance flows use it, with tests
- [ ] 3.1 Comparison service and endpoint with tests
- [ ] 4.1 Programs/history endpoints
- [ ] 4.2 Switch endpoint with validation and tests
- [ ] 5.1 Programs list, history and removal of Add new
- [ ] 5.2 Change-program popup and specs
- [ ] 6.1 Demo generator endpoint with tests
- [ ] 6.2 Admin demo page, route and nav
- [ ] 7.1 Playwright e2e and snapshot updates
- [ ] 7.2 Full test run green
