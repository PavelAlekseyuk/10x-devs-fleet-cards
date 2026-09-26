# Review Generated Card Spending Implementation Plan

## Overview

Deliver the S-04 reporting slice that turns persisted fuel transactions into a trustworthy, selected-month view. The API will apply explicit transaction-lineage and status rules, convert a user's calendar-month selection into UTC using a configurable timezone (CET by default), and expose net, VAT, and gross totals. Angular will replace the transactions placeholder and load the overview counts and spending total from an authoritative summary response.

## Current State Analysis

S-03 already persists 1-5 deterministic `Completed` transactions when a card is issued. `FuelTransaction` contains the vehicle snapshot, quantity, unit price, discount, net total, VAT rate/amount, gross total, and status. Both EF and seeded card services expose inclusive `from`/`to` filtering through `GET /api/transactions`, but there is no reporting aggregate contract, correction lineage/version metadata, POS-local timestamp, or timezone-aware month selection.
The exact economic treatment for refunds, cancellations, and recalculations remains a blocking stakeholder decision in the S-04 roadmap; the status rules below are proposals until that decision is recorded in `context/foundation/decisions.md`.

The Angular `/transactions` route still renders the generic shell placeholder, while the overview hardcodes vehicle count, card count, and spend. Existing standalone components, typed HTTP services, bilingual `LanguageService` resources, xUnit API tests, Vitest component tests, and Playwright/Aspire tests provide the local patterns to extend.

## Desired End State

An authenticated user can choose a calendar month and timezone, defaulting to CET, and inspect every matching transaction with both user-timezone and POS-local timestamps, vehicle/fuel facts, quantity, discount, net, VAT, gross, and status. The report displays period totals with gross paid after discount as the primary spend measure and keeps net and VAT visible.

The overview obtains vehicle count, card count, and the same last-month net/VAT/gross totals from a dedicated authenticated summary endpoint. For every selected period, the summary gross total equals the sum of the effective transaction rows shown by the report, including correction-lineage rules.

### Key Discoveries:

- `src/FleetCards.Domain/FleetCardsModel.cs` and `src/FleetCards.Contracts/Models.cs` already carry the core financial fields, but transaction facts need explicit lineage/version and POS-time data.
- `src/FleetCards.Infrastructure/EfFleetCardService.cs` and `SeededFleetCardService.cs` duplicate period filtering and currently return rows without totals; reporting logic must be shared or behaviorally identical across both modes.
- `src/FleetCards.Api/Program.cs` already protects `/api/transactions` with authentication and maps inclusive `from`/`to` query values; the new reporting contract should preserve authenticated access while moving month-boundary semantics into a validated reporting service.
- `web/src/app/shell.component.html` hardcodes overview metrics and `web/src/app/app.routes.ts` sends `/transactions` to `ShellComponent`; the reporting feature must own real data loading without weakening route guards.
- `context/foundation/decisions.md` locks 23% MVP VAT and away-from-zero rounding, while the user decision for this plan adds a configurable user timezone with CET default and dual UTC/POS-local display.

## What We're NOT Doing

- Generating refunds, cancellations, or recalculations during card issuance; the demo generator remains deterministic and `Completed`-only.
- Building fleet-card program comparison, discount-program assignment, exports, invoices, settlement, or advanced reporting.
- Replacing mocked authentication or adding multi-client settings storage.
- Treating correction rows as mutable edits; corrections remain append-only and linked through explicit lineage.
- Inferring POS-local time from the signed-in user's timezone; each transaction stores its POS-local time and timezone information.
- Adding pagination or caching; the MVP targets one client installation with low volume.

## Implementation Approach

Keep the Domain → Contracts → Application → Infrastructure → Minimal API layering. Add a reporting model/service that normalizes a month selection into a half-open UTC interval using the requested timezone, selects one effective row per transaction lineage, and aggregates net, VAT, and gross with away-from-zero decimal rules. Use the same application contract for EF and seeded implementations, with a dedicated summary response for the overview and a transaction response that carries both timestamp representations.

Persist the user's timezone as an Angular user preference in local storage, defaulting to `Europe/Warsaw`/CET-compatible behavior, and send it with reporting requests. Validate timezone IDs and month values at the API boundary, return structured `400` responses for invalid input, and keep all report endpoints authenticated. Add localized Angular error/empty/loading states and replace only the transactions and overview preview surfaces.

## Critical Implementation Details

Reporting semantics when a correction falls outside the original transaction's selected month are unresolved. Before implementing the report query, decide whether month filtering happens before lineage reduction or the latest lineage state is resolved first, define deterministic version ordering and a stable tie-breaker, and test the chosen behavior across a month boundary.

Transaction selection must use a half-open UTC interval derived from the user's local month start and the next local month start; this avoids inclusive end-of-day precision bugs and handles daylight-saving transitions. A lineage has one effective version: `Completed` and `Recalculated` contribute their positive effective amounts, `Refunded` contributes its explicit negative correction amount, and `Cancelled` contributes zero; the original row is not counted again once a later linked version exists. Existing generated records remain single-version Completed lineages.

POS-local time is a persisted fact, represented alongside UTC with the POS timezone information. User-local display is derived from UTC using the configured user timezone; the API must not overwrite the stored POS representation when a user changes preference.

## Phase 1: Transaction Facts and Reporting Contracts

### Overview

Extend the immutable transaction model and define the shared reporting/calculation contract before adding transport or UI behavior.

### Changes Required:

#### 1. Transaction lineage, version, and timestamp data

**File**: `src/FleetCards.Domain/FleetCardsModel.cs`, `src/FleetCards.Infrastructure/FleetCardsDbContext.cs`, `src/FleetCards.Infrastructure/Migrations/`

**Intent**: Preserve immutable transaction facts while making future refunds, cancellations, and recalculations selectable without double-counting and while retaining both UTC and POS-local time.

**Contract**: Add explicit lineage identity, version/order metadata, correction linkage, UTC occurrence, POS-local occurrence, and POS timezone fields as appropriate to the existing model. Map decimal precision consistently with the existing quantity, price, VAT, and total columns; enforce valid lineage/version relationships and indexes needed for period and lineage lookup. Existing seeded and generated Completed rows remain readable and receive deterministic POS metadata.

#### 2. Reporting DTOs and application service

**File**: `src/FleetCards.Contracts/Models.cs`, `src/FleetCards.Application/FleetCardsApplication.cs` or a focused reporting service/contract file

**Intent**: Give API and UI one typed contract for selected-period rows, totals, and overview metrics.

**Contract**: Define a report request carrying year, month, and IANA timezone; transaction responses carrying UTC time, user-local time, POS-local time/timezone, financial fields, and status; totals carrying net, VAT, gross, and row count; and a summary response carrying active vehicle count, active card count, selected-period totals, and period boundaries. The application contract must expose equivalent reporting operations for EF and seeded modes and reject invalid timezone/month input before querying.

#### 3. Effective transaction and decimal calculation rules

**File**: `src/FleetCards.Application/` reporting/calculation implementation and `tests/FleetCards.Application.Tests/`

**Intent**: Centralize status-aware selection and aggregation so the summary and row response cannot drift.

**Contract**: Select the highest effective version within each explicit lineage, exclude superseded originals, apply the stakeholder-approved Completed/Recalculated/Refunded/Cancelled economic rules, and sum stored monetary values using decimal arithmetic with away-from-zero rounding where a derived amount is required. Do not implement status aggregation until the economic rules are confirmed and recorded in `context/foundation/decisions.md`. A month boundary is local midnight at the start of the selected month through local midnight at the start of the following month, converted to UTC. The default user timezone is CET-compatible and configurable; unknown timezone IDs and invalid periods are validation failures.

### Success Criteria:

#### Automated Verification:

- Application tests cover Completed-only generated data, effective lineage selection, refunded/cancelled/recalculated cases, duplicate-prevention, decimal rounding, UTC month boundaries, timezone/DST conversion, corrections crossing report-month boundaries with deterministic version tie-breaking, and net/VAT/gross reconciliation.
- EF model and migration tests apply cleanly and preserve existing seeded transactions, vehicle snapshots, and card-issued data.

#### Manual Verification:

- Inspect a generated and a hand-created correction lineage to confirm original facts remain unchanged while only the effective version contributes to totals.

## Phase 2: Persisted Reporting API

### Overview

Expose the selected-period transaction report and dedicated overview summary through authenticated Minimal API endpoints with matching EF and seeded behavior.

### Changes Required:

#### 1. Reporting service implementations

**File**: `src/FleetCards.Infrastructure/EfFleetCardsReadService.cs`, `src/FleetCards.Application/FleetCardsApplication.cs`, `src/FleetCards.Infrastructure/FleetCardsInfrastructure.cs`

**Intent**: Move report filtering, effective-row selection, timestamp conversion, and aggregation behind one application-facing capability in both runtime modes.

**Contract**: EF queries must use UTC range predicates and lineage/version ordering, project DTOs without leaking domain entities, and return deterministic row ordering by UTC occurrence then stable ID. Seeded mode must use the same rules and default data. The summary and transaction report must share the same calculation path or equivalent tested implementation.

#### 2. Authenticated report endpoints and validation

**File**: `src/FleetCards.Api/Program.cs`, `src/FleetCards.Contracts/Models.cs`, `tests/FleetCards.Api.Tests/`

**Intent**: Replace the loose inclusive `from`/`to` reporting contract with explicit month/timezone semantics while preserving authenticated access.

**Contract**: Add an authenticated transaction report route such as `GET /api/transactions/report?year=&month=&timeZone=` and an authenticated `GET /api/dashboard/summary?year=&month=&timeZone=`. Both return the selected half-open period, totals, and deterministic rows/metrics as applicable. Reject missing/invalid year, month, timezone, or malformed values with `400`; anonymous requests return `401`. Keep read-only access valid and do not add write permissions.

#### 3. API authorization and reconciliation tests

**File**: `tests/FleetCards.Api.Tests/` transaction/report test file and existing platform fixtures

**Intent**: Prove the reporting contracts and security boundary at HTTP level.

**Contract**: Cover seeded and EF-compatible report responses, timezone conversion, status lineage cases, empty periods, invalid inputs, anonymous rejection, read-only reads, stable ordering, and the invariant that summary gross equals the sum of effective report-row gross values. Assert the API returns no unrelated card sensitive data.

### Success Criteria:

#### Automated Verification:

- `dotnet test tests/FleetCards.Application.Tests/FleetCards.Application.Tests.csproj` passes calculation, lineage, rounding, and timezone tests.
- `dotnet test tests/FleetCards.Api.Tests/FleetCards.Api.Tests.csproj` passes authenticated report, validation, role, and reconciliation coverage.

#### Manual Verification:

- Call both endpoints for a month containing the seeded transaction and confirm the returned gross total is `326.60` PLN in the seeded baseline while net and VAT remain separately visible.

## Phase 3: Angular Reporting Experience

### Overview

Replace the transactions placeholder and hardcoded overview metrics with typed, bilingual reporting views and a configurable user timezone.

### Changes Required:

#### 1. Timezone preference and reporting HTTP service

**File**: `web/src/app/transaction-report.service.ts`, `web/src/app/user-settings.service.ts` or equivalent focused service, `web/src/app/language.service.ts`

**Intent**: Make the selected-month and timezone contract usable from Angular without duplicating financial calculations in the browser.

**Contract**: Store the user's selected IANA timezone locally with CET-compatible default, expose month navigation/selection state, call the report and summary endpoints with year/month/timezone, and map API responses to strict TypeScript models. Displayed totals are server-provided values; the client may verify row sums for tests but must not become the authority.

#### 2. Transactions component and template

**File**: `web/src/app/transaction-report.component.ts`, `.html`, `.scss`, `web/src/app/transaction-report.component.spec.ts`, `web/src/app/shell.component.ts`, `web/src/app/shell.component.html`

**Intent**: Give users a real selected-month transaction view with enough detail to inspect and reconcile spending.

**Contract**: The standalone feature provides month picker controls, timezone setting, loading/error/empty states, period totals, and full rows showing user-timezone time, POS-local time/timezone, vehicle plate, fuel type, quantity, discount, net, VAT, gross, and status. Keep `/transactions` mapped to the existing `ShellComponent` route with `authGuard`; import and render the report component in the shell's transactions branch. Add EN/PL resource keys; use responsive table/stacked-row behavior without hardcoded preview data.

#### 3. Overview summary integration

**File**: `web/src/app/shell.component.ts`, `web/src/app/shell.component.html`, `web/src/app/shell.component.spec.ts` or focused overview tests

**Intent**: Replace hardcoded vehicle/card/spend metrics and chart labels with the dedicated summary response.

**Contract**: Overview requests the default selected month and current timezone preference, renders active vehicle count, active card count, and gross paid-after-discount as the primary spend value with net/VAT available in the spend panel, and links the period to the transactions view. Summary loading and failure states must not silently retain stale preview numbers.

#### 4. Browser-facing localization and accessibility

**File**: `web/src/app/language.service.ts`, reporting templates/styles, `web/e2e/`

**Intent**: Keep the new reporting workflow consistent with the existing bilingual standalone-component and Playwright patterns.

**Contract**: All visible labels, validation messages, statuses, currency labels, timestamps, and empty/error states have EN and PL translations; controls have accessible names and the table has meaningful headers. ReadOnly users can inspect reports and cannot be presented with mutation controls that do not exist.

### Success Criteria:

#### Automated Verification:

- From `web/`, `npm test` passes report service/component tests for month changes, timezone persistence/default, dual timestamp rendering, totals, errors, empty periods, and EN/PL labels.
- From `web/`, `npm run build` passes strict TypeScript and Angular template checking.

#### Manual Verification:

- Sign in in both languages, change the timezone and month, confirm the same transaction moves between local calendar months at a UTC boundary as expected, and verify POS time remains unchanged.
- Confirm the overview gross total and transaction-period gross total visibly agree with the listed rows.

## Phase 4: Reconciliation and Aspire Verification

### Overview

Exercise the full SQL-backed journey and protect the reporting contract against regressions in migration, timezone conversion, seeded data, and overview reconciliation.

### Changes Required:

#### 1. Composed browser acceptance flow

**File**: `web/e2e/transaction-report.spec.ts`, existing Playwright configuration and fixtures

**Intent**: Verify the user-visible report and overview against the Aspire-composed API, Angular SPA, and isolated SQL database.

**Contract**: The test signs in as ReadWrite and ReadOnly, loads the default month, verifies summary counts and gross spend, opens transactions, checks all generated rows and both timestamp forms, changes timezone/month, and asserts the summary total equals the visible effective rows. It also covers unauthenticated rejection and invalid report parameters.

#### 2. Runtime and regression alignment

**File**: `src/FleetCards.Infrastructure/Migrations/`, `src/FleetCards.ServiceDefaults/` only if required, `context/foundation/roadmap.md`

**Intent**: Ensure the new data shape and verification contract are represented in the composed runtime without broadening the product scope.

**Contract**: Aspire startup applies the migration, both EF and seeded modes expose the same report semantics, and no existing card/vehicle history or sensitive-data boundary regresses. Update only the S-04 roadmap status to `in-progress` when implementation begins; completion remains for the implementation/archive workflow.

### Success Criteria:

#### Automated Verification:

- `npm run test:e2e` passes the SQL-backed overview-to-transactions reconciliation flow through Aspire.
- `dotnet test` and `npm run build` pass after migration and API/UI integration changes.
- A deliberate test mutation that changes the summary gross or row gross causes the reconciliation assertion to fail.

#### Manual Verification:

- Complete the seeded operational journey from login through the overview and transactions page in English and Polish; confirm the selected month, timezone labels, gross headline, net/VAT details, and POS timestamps are understandable and consistent.

## Testing Strategy

### Unit Tests:

- Test effective lineage selection for Completed, Recalculated, Refunded, and Cancelled records, including superseded-row exclusion and explicit negative/zero correction amounts.
- Test decimal multiplication, VAT, aggregation, and away-from-zero rounding at half-cent boundaries.
- Test user-local month conversion across UTC boundaries and daylight-saving transitions, plus default CET-compatible behavior.
- Test Angular service serialization, local preference persistence, component rendering, localization, loading/error/empty states, and summary/row reconciliation.

### Integration Tests:

- Test authenticated Minimal API report and summary contracts for anonymous, ReadOnly, and ReadWrite users.
- Apply the EF migration and query seeded SQL data; compare EF and seeded service outputs for the same period.
- Run the Playwright journey through Aspire with a fresh isolated database.

### Manual Testing Steps:

1. Sign in as each seeded user and open the overview and transactions views for the default CET month.
2. Verify the overview vehicle/card counts and gross total match the report rows; inspect net and VAT separately.
3. Change the timezone and selected month around a transaction boundary; verify user time changes, POS time does not, and the row moves according to the selected local month.
4. Switch English/Polish and verify labels, statuses, validation, empty, and error states remain complete.

## Performance Considerations

The MVP has low volume and one client installation. Use UTC period predicates, lineage/version indexes, and server-side aggregation; no pagination, caching, or client-side financial recomputation is required. Keep summary and row queries bounded to the selected period and client context.

## Migration Notes

Add an EF migration for lineage/version and timestamp fields with deterministic defaults for existing rows. Preserve existing `OccurredAt` values as UTC facts, backfill POS-local fields from the configured seeded POS timezone, and keep existing vehicle/card snapshots unchanged. Existing Completed rows form one lineage/version each. The migration must be applied by Aspire startup and remain compatible with the seeded fallback service.

## References

- `context/foundation/roadmap.md` — S-04 scope, dependency, and blocked VAT/status decision.
- `context/foundation/prd.md` — US-01, FR-005 through FR-007, transaction/VAT guardrails, and access control.
- `context/foundation/requirements.md` — transaction fields, status model, VAT presentation, and persistence expectations.
- `context/foundation/decisions.md` — 23% VAT default, away-from-zero rounding, immutable transaction facts, and UTC vehicle snapshots.
- `context/changes/issue-and-assign-virtual-cards/plan.md` — completed-only deterministic generation and S-03 transaction handoff.
- `src/FleetCards.Application/MockFuelTransactionGenerator.cs`, `src/FleetCards.Infrastructure/EfFleetCardService.cs`, and `src/FleetCards.Infrastructure/SeededFleetCardService.cs` — current generation and period-read patterns.
- `src/FleetCards.Api/Program.cs` — authenticated Minimal API and existing transaction route.
- `web/src/app/shell.component.html`, `web/src/app/app.routes.ts`, and `web/src/app/language.service.ts` — current overview placeholder, route wiring, and bilingual resource pattern.

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append ` — <commit sha>` when a step lands. Do not rename step titles. See `references/progress-format.md`.

### Phase 1: Transaction Facts and Reporting Contracts

#### Automated

- [ ] 1.1 Application tests cover effective lineage selection, status handling, duplicate-prevention, decimal rounding, UTC month boundaries, timezone conversion, and net/VAT/gross reconciliation
- [ ] 1.2 EF model and migration tests preserve existing seeded transactions and apply the new transaction fields cleanly

#### Manual

- [ ] 1.3 Inspect generated and correction lineage records to confirm immutable originals and effective-only totals

### Phase 2: Persisted Reporting API

#### Automated

- [ ] 2.1 Application and API tests pass report calculation, authenticated contract, validation, role, ordering, and reconciliation coverage
- [ ] 2.2 EF and seeded service modes return equivalent selected-period rows and summary totals

#### Manual

- [ ] 2.3 Verify the seeded report returns 326.60 PLN gross for the baseline transaction with separate net and VAT values

### Phase 3: Angular Reporting Experience

#### Automated

- [ ] 3.1 Angular tests cover timezone preference/default, month selection, dual timestamps, totals, errors, empty states, permissions, and EN/PL labels
- [ ] 3.2 `npm run build` passes strict TypeScript and Angular template checking

#### Manual

- [ ] 3.3 Verify month/timezone changes, POS timestamp stability, overview/report reconciliation, and bilingual accessible UI behavior

### Phase 4: Reconciliation and Aspire Verification

#### Automated

- [ ] 4.1 Aspire Playwright coverage passes the SQL-backed overview-to-transactions reconciliation and invalid-parameter flow
- [ ] 4.2 `dotnet test` and `npm run build` pass after migration and composed integration changes
- [ ] 4.3 A deliberate total mismatch is detected by the reconciliation assertion

#### Manual

- [ ] 4.4 Complete the seeded journey in English and Polish and confirm the displayed period, timezone, gross headline, net/VAT details, and POS timestamps