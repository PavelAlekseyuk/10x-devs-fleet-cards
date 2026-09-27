# Card Lifecycle and Overview Corrections Implementation Plan

## Overview

Make each issued card's vehicle assignment immutable, replace assignment history with a newest-first state timeline, and correct the state editor's initial selection after a fresh read. Extend selected-period reporting with gross savings based on POS price and discounted amounts, then synchronize Spend Overview navigation with the transaction report.

## Current State Analysis

Cards currently have a nullable `AssignedCarId`; the EF and seeded services allow reassignment and append assignment events. The reassignment API and UI are covered by tests, and the current requirements explicitly permit reassignment. A null-assignment card can have linked transactions, assignment events, and audit events; restrictive foreign keys prevent deleting it without first removing those linked facts.

Card state changes already update the card and append a `CardStateChangedTo{State}` audit event. EF card issuance records `CardIssued`, but the seeded card has no initial audit event. The current assignment-history query is chronological and has a separate route. The card UI's read-only badge uses the fetched state, while its dynamically rendered edit options can leave the native select at its first option after a fresh load; the user's observed `Blocked` badge and `Active` selection place the defect in the Angular control binding.

The transaction report selects the effective purchase version and signed refund/cancellation rows for the selected period. `TransactionReportingService.CreateSummary` reuses those rows and totals. Transaction `UnitPrice` is the undiscounted station price; correction fixtures currently set it and quantity to zero. The shared month signal already drives report requests, but the overview fetches its summary only on entry or retry.

### Key Discoveries:

- `src/FleetCards.Domain/FleetCardsModel.cs:60-68`, `src/FleetCards.Infrastructure/EfFleetCardService.cs:188-266`, and `src/FleetCards.Infrastructure/SeededFleetCardService.cs:221-279` define the current nullable assignment and reassignment behavior.
- `src/FleetCards.Infrastructure/FleetCardsDbContext.cs` configures restrictive card references for transactions and assignment events; the seed card is assigned, but the seeded card service's audit list starts empty.
- `src/FleetCards.Infrastructure/EfFleetCardService.cs:149-186` and the seeded equivalent record state changes as audit events, so the state timeline can use the existing audit model rather than adding a separate event table.
- `src/FleetCards.Application/TransactionReportingService.cs:76-144` is the shared effective-row selection and summary calculation point. `web/src/app/transaction-report.service.ts:89-99` already moves the shared selected month, while `web/src/app/shell.component.ts:88-102` loads the overview summary without reacting to month changes.
- The supplied research is marked partial for the original refresh report; the user's clarification establishes that the badge is `Blocked` while only the edit select shows `Active`. The plan therefore verifies the rendered select value after a fresh API read and browser reload.

## Desired End State

Every card has one required issue-time vehicle assignment that cannot be changed through the service, API, or UI. Card history shows issuance as an initial `Active` event followed by state changes, newest first, and the editable state selector matches the state returned by a fresh read.

The overview's net and gross labels identify their tax basis, and its savings metric reports selected-period gross savings from undiscounted POS price and discounted totals. Signed correction rows affect savings in their own report period, and overview month navigation reloads the summary for the same month shared with the transaction report.

## What We're NOT Doing

- Adding card reassignment, unassignment, card return, or a new card-state transition matrix.
- Adding a user-facing workflow for creating refunds, cancellations, or recalculations; this change only ensures reportable transaction rows carry the POS basis needed for savings.
- Changing the mocked card provider, authentication, PAN redaction boundary, or card issuance transaction generation beyond the transaction pricing fields needed for the new aggregate.
- Implementing program comparison, lifetime savings, or a second savings metric.

## Implementation Approach

Update the card application contract and both implementations first, then expose state history through the authenticated Minimal API using existing audit events. A SQL migration removes any legacy cards whose issue-time vehicle is missing, including their dependent transaction, assignment-history, and card-audit rows as explicitly authorized, before making the assignment column required. Next, extend the shared reporting totals with gross savings and update the Angular card and overview surfaces. Finish with focused tests, the composed Aspire flow, and policy-document alignment.

## Critical Implementation Details

The legacy cleanup is intentionally destructive and overrides the usual append-only treatment for those null-assignment cards only. Delete correction rows before their referenced purchase rows, then assignment events and card audit events, then the card; do not remove related data for assigned cards. This cleanup cannot be rolled back without a backup.

Calculate each report row's undiscounted POS net from quantity multiplied by the station unit price, round using the established away-from-zero rule, apply that row's VAT rate to obtain POS gross, then subtract the row's signed discounted gross. Correction rows must retain a POS unit price and encode the corrected quantity with its sign, so refunds and cancellations reverse the corresponding savings instead of relying on a proportional estimate. Keep report row selection and period attribution unchanged.

## Phase 1: Immutable Card Assignment and State-History API

### Overview

Remove reassignment at the application and API boundaries, make issue-time assignment required, define the explicit legacy cleanup, and expose a newest-first state timeline derived from card audit events.

### Changes Required:

#### 1. Required issue-time assignment and legacy cleanup

**File**: `src/FleetCards.Domain/FleetCardsModel.cs`, `src/FleetCards.Contracts/Models.cs`, `src/FleetCards.Application/FleetCardService.cs`, `src/FleetCards.Infrastructure/EfFleetCardService.cs`, `src/FleetCards.Infrastructure/SeededFleetCardService.cs`, `src/FleetCards.Infrastructure/FleetCardsDbContext.cs`, `src/FleetCards.Infrastructure/Migrations/<timestamp>_MakeFleetCardAssignmentRequired.cs`, `src/FleetCards.Infrastructure/Migrations/FleetCardsDbContextModelSnapshot.cs`, `src/FleetCards.Api/Program.cs`

**Intent**: Make a card's issue-time vehicle a required immutable fact in both persistence modes and remove the supported reassignment path.

**Contract**: Make `AssignedCarId` non-null throughout card domain and response contracts. Remove reassignment from the application interface and both implementations and remove the assignment mutation route. Add an EF migration that deletes each null-assignment card's dependent transaction versions, assignment events, and card audit events in foreign-key-safe order, then enforces a non-null assignment column. Issuance continues to require an active vehicle and existing transaction vehicle snapshots remain unchanged.

#### 2. State-history contract and projection

**File**: `src/FleetCards.Contracts/Models.cs`, `src/FleetCards.Application/FleetCardService.cs`, `src/FleetCards.Infrastructure/EfFleetCardService.cs`, `src/FleetCards.Infrastructure/SeededFleetCardService.cs`, `src/FleetCards.Infrastructure/FleetCardsDbContext.cs`, `src/FleetCards.Api/Program.cs`

**Intent**: Replace the vehicle-assignment timeline with a card-state timeline backed by the audit events already written for issuance and state changes.

**Contract**: Add an authenticated `GET /api/cards/{id}/state-history` response containing state and occurrence time, ordered by `OccurredAt` descending and then audit-event `Id` descending for deterministic newest-first ties. Project `CardIssued` as the initial `Active` event and `CardStateChangedTo{State}` as the selected destination state. Seed an equivalent issuance event for the EF and seeded baseline cards; preserve existing audit records for assigned cards.

#### 3. Card lifecycle and API regression tests

**File**: `tests/FleetCards.Application.Tests/FleetCardServiceTests.cs`, `tests/FleetCards.Api.Tests/CardApiTests.cs`, `tests/FleetCards.Api.Tests/`

**Intent**: Replace assertions that permit reassignment and prove the immutable assignment and state-history contracts in both runtime modes.

**Contract**: Cover immutable assignment after issue, the removed assignment endpoint, required assignment in card responses, authenticated state-history reads, initial `Active` issuance history, newest-first state changes, and preservation of transaction vehicle snapshots.

### Success Criteria:

#### Automated Verification:

- Application tests prove cards cannot be reassigned in EF-backed and seeded service modes and state history includes issuance followed by newest-first changes.
- API tests prove card assignment mutation is no longer available, card reads always contain an assigned vehicle, and state history remains readable to authenticated read-only users.

#### Manual Verification:

- Issue a card, change it through supported states, and verify the vehicle stays fixed while the timeline starts at `Active` and lists the latest state change first.

**Implementation Note**: After completing this phase and all automated verification passes, pause for human confirmation of the manual check before proceeding to the next phase.

## Phase 2: Gross Savings and Angular Workflows

### Overview

Add a server-authoritative selected-period gross-savings total and update the card-management and overview experiences to present the new contracts accurately.

### Changes Required:

#### 1. POS-based gross-savings aggregate

**File**: `src/FleetCards.Contracts/Models.cs`, `src/FleetCards.Application/TransactionReportingService.cs`, `src/FleetCards.Application/FleetCardsApplication.cs`, `src/FleetCards.Infrastructure/EfFleetCardsReadService.cs`, `src/FleetCards.Infrastructure/SeededFleetCardService.cs`, `tests/FleetCards.Application.Tests/TransactionReportingTests.cs`, `tests/FleetCards.Api.Tests/TransactionReportApiTests.cs`

**Intent**: Extend the existing report and summary calculation so gross savings reconcile to the same effective transaction rows and selected period as spend totals.

**Contract**: Add a `GrossSavingsTotal` decimal property to shared report totals and expose it unchanged through report and dashboard responses. For each selected row, calculate POS net as `decimal.Round(quantity * unitPrice, 2, MidpointRounding.AwayFromZero)`, calculate POS VAT as `decimal.Round(posNet * vatRate, 2, MidpointRounding.AwayFromZero)`, calculate POS gross as `posNet + posVat`, then calculate row savings as `posGross - GrossTotal`; sum the rounded row savings without a second final rounding step. Preserve the current effective-purchase and signed-correction selection rules. Ensure every correction row carries POS unit price and signed quantity data needed by the calculation, and verify EF and seeded behavior agree. Update the `TransactionReportTotals` contract and all direct consumers/fixtures, including `web/src/app/transaction-report.service.ts`, `web/src/app/shell.component.ts`, `web/src/app/shell.component.spec.ts`, `web/src/app/transaction-report.component.spec.ts`, and the report API/application tests.

#### 2. Card state control and state-history view

**File**: `web/src/app/card-management.service.ts`, `web/src/app/card-management.component.ts`, `web/src/app/card-management.component.html`, `web/src/app/card-management.component.spec.ts`, `web/src/app/language.service.ts`

**Intent**: Remove reassignment UI, show the new state timeline, and fix the edit selector so it reflects the card state after options and fresh card data render.

**Contract**: Replace assignment-history calls and display with the state-history API and newest-first state labels/timestamps. Remove vehicle reassignment controls. Bind the native select so its selected value remains equal to `card.state` after a fresh response; tests must inspect the actual DOM value, not only the read-only badge. Add matching English and Polish history labels.

#### 3. Overview savings labels and shared month controls

**File**: `web/src/app/transaction-report.service.ts`, `web/src/app/shell.component.ts`, `web/src/app/shell.component.html`, `web/src/app/shell.component.spec.ts`, `web/src/app/language.service.ts`

**Intent**: Present net, gross, and savings with accurate labels and make overview period navigation reload the same selected month used by the transaction report.

**Contract**: Bind the savings metric to the API's selected-period gross-savings value; label the existing net/gross amounts explicitly in English and Polish. Add accessible previous/next month controls to Spend Overview using `moveSelectedMonth`; after a successful month change, reload the summary. Keep the report and overview on the shared month signal and keep all displayed financial totals server-authoritative.

### Success Criteria:

#### Automated Verification:

- Reporting tests cover POS gross and gross savings for effective purchases, recalculations, signed partial refunds, signed cancellations, empty periods, and report/summary reconciliation.
- Angular tests prove the state dropdown's actual selected value matches a freshly fetched `Blocked` card, state history is newest first, reassignment controls are absent, and month navigation reloads the summary and keeps the report period synchronized.
- From `web/`, `npm test` and `npm run build` pass with English and Polish net, gross, and savings labels.

#### Manual Verification:

- Block a card and reload the cards view; confirm both the state badge and edit selector show `Blocked`, and inspect its issuance and state history.
- Navigate Spend Overview to another month; confirm savings, net, and gross totals change with the selected period and match the corresponding transaction report in both languages.

**Implementation Note**: After completing this phase and all automated verification passes, pause for human confirmation of the manual check before proceeding to the next phase.

## Phase 3: Composed Verification and Policy Alignment

### Overview

Verify migration behavior and the complete card-to-overview experience through the composed runtime, then update the requirements and decision record to reflect the new immutable assignment and its narrowly scoped cleanup exception.

### Changes Required:

#### 1. SQL migration and composed-flow acceptance

**File**: `tests/FleetCards.Api.Tests/FleetCardsMigrationTests.cs`, `tests/FleetCards.Api.Tests/`, `web/e2e/card-management.spec.ts`, `web/e2e/transaction-report.spec.ts`, `web/playwright.config.ts`

**Intent**: Prove the migration and user-visible contracts against SQL Server, API, and Angular rather than relying only on isolated tests.

**Contract**: Add a dedicated SQL-backed migration test that starts from the database schema immediately before the new migration, inserts a null-assignment card plus transaction versions/corrections, assignment events, and audit entries, applies the migration, and asserts all records for that card are removed while assigned cards/facts remain and `AssignedCarId` is required. Through Aspire, verify assignment immutability, state history and selected state after browser reload, gross-savings reconciliation, and month synchronization between Spend Overview and transactions.

#### 2. Requirements and decision alignment

**File**: `context/foundation/requirements.md`, `context/foundation/decisions.md`

**Intent**: Remove the prior requirement that cards may be reassigned and record the agreed issue-time assignment, state-history, savings, and exceptional legacy-cleanup behavior.

**Contract**: Specify one immutable vehicle assignment per issued card, state history including issuance in newest-first order, selected-period gross savings based on POS and discounted values with signed corrections, and the one-time deletion of null-assignment cards with their dependent records. Explicitly identify that deletion as a narrow exception to append-only transaction and audit retention.

### Success Criteria:

#### Automated Verification:

- `dotnet test` passes for the solution, including the SQL-backed migration and API coverage.
- From `web/`, `npm run test:e2e` passes through Aspire for card issue/state/reload/history and selected-period savings reconciliation/navigation.

#### Manual Verification:

- Complete the ReadWrite card-to-overview journey in English and Polish, then verify ReadOnly users can inspect state history and reports but cannot mutate card state.

**Implementation Note**: After completing this phase and all automated verification passes, pause for human confirmation that the composed flow meets the agreed acceptance criteria.

## Testing Strategy

### Unit Tests:

- Verify both card service modes cannot alter an issue-time assignment; state changes preserve it.
- Verify the state projection maps issuance to `Active`, maps each defined state-change audit action, and sorts newest first.
- Verify the state projection uses `OccurredAt DESC, Id DESC` when state events share a timestamp.
- Verify gross savings uses each effective row's POS price, quantity, VAT rate, and signed discounted gross, including partial refund and cancellation rows; test rounding and zero-row periods.
- Verify report and summary totals are identical for the same month in EF and seeded modes.
- Verify the Angular select's DOM value after a fresh card response, not only the text badge, and test shared month navigation/reload in both languages.

### Integration Tests:

- Test API card reads, removed reassignment route, state-history authentication, and history ordering.
- Apply the EF migration to a SQL database containing both an unassigned legacy card with dependent facts and assigned cards; verify only the unassigned card's records are purged before the required-column constraint is applied.
- Run Playwright through Aspire for refreshed card state, visible history, assignment immutability, selected-month navigation, and savings/report reconciliation.

### Manual Testing Steps:

1. In the ReadWrite account, issue a card, change its state to Blocked, reload the browser, and confirm the badge and editor both show Blocked.
2. Inspect the card state timeline and confirm Active-at-issuance is present and later state changes appear newest first.
3. Navigate between months in Spend Overview and Transactions; reconcile gross savings and gross spend to transaction rows containing purchases and signed corrections.
4. Switch to the ReadOnly account and verify history and reports remain visible while card mutation controls are unavailable.

## Performance Considerations

The MVP has low request volume and one client installation. Query audit history by card entity type and ID with newest-first ordering; add an appropriate index only if needed to keep this card-scoped query bounded. Savings uses the already selected-period report rows and does not require a separate client-side financial calculation or lifetime scan.

## Migration Notes

The migration must first identify null-assignment cards, remove their correction/transaction rows before linked purchase rows, then remove their assignment events and card audit events, and finally remove the cards. Only after cleanup should it alter `AssignedCarId` to non-null. Assigned cards and all their transaction snapshots remain untouched. Seeded mode already issues cards with a vehicle; its deterministic card state history must include the seeded `CardIssued` event.

The cleanup is intentionally irreversible under normal migration rollback. Take the environment's standard database backup before applying it; a down migration can restore nullability but cannot reconstruct deleted transaction or audit facts. The expected current seed has no null-assignment card, so normal seeded startup should make no data deletions.

## References

- `context/changes/bug-fixes-after-s04-20260927/research.md` — card behavior, report calculations, overview bindings, and original refresh investigation.
- `context/changes/issue-and-assign-virtual-cards/plan.md` — existing card reassignment and assignment-history contracts being replaced.
- `context/changes/review-generated-card-spending/plan.md` — selected-period effective transaction and signed-correction rules.
- `context/foundation/requirements.md` — current card reassignment and transaction immutability statements to align.
- `context/foundation/decisions.md` — signed correction, rounding, and VAT decisions.
- `src/FleetCards.Infrastructure/EfFleetCardService.cs`, `src/FleetCards.Infrastructure/SeededFleetCardService.cs`, `src/FleetCards.Infrastructure/FleetCardsDbContext.cs`, and `src/FleetCards.Api/Program.cs` — card persistence and route patterns.
- `src/FleetCards.Application/TransactionReportingService.cs`, `web/src/app/transaction-report.service.ts`, and `web/src/app/shell.component.ts` — shared report totals and selected-month patterns.

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append ` — <commit sha>` when a step lands. Do not rename step titles. See `references/progress-format.md`.

### Phase 1: Immutable Card Assignment and State-History API

#### Automated

- [x] 1.1 Application tests prove immutable issue-time assignment and newest-first state history in EF-backed and seeded modes. — f2be194
- [x] 1.2 API tests prove reassignment is unavailable and authenticated state history includes issuance and subsequent state changes. — f2be194

#### Manual

- [x] 1.3 Verify through the UI that a card remains assigned to its issue-time vehicle and its history starts at Active and lists the latest change first. — f2be194

### Phase 2: Gross Savings and Angular Workflows

#### Automated

- [x] 2.1 Reporting tests prove POS-based gross savings, signed correction handling, and report/summary reconciliation.
- [x] 2.2 Angular tests prove refreshed state selection, state-history rendering, removal of reassignment controls, and shared-month summary reload.
- [x] 2.3 `npm test` and `npm run build` pass with English and Polish overview labels.

#### Manual

- [x] 2.4 Verify refreshed blocked state and reconcile savings and spend across two selected months in both languages.

### Phase 3: Composed Verification and Policy Alignment

#### Automated

- [ ] 3.1 SQL-backed migration tests remove null-assignment cards and dependent facts while preserving assigned cards and their history.
- [ ] 3.2 `dotnet test` and `npm run test:e2e` pass through the composed card and selected-period reporting flow.

#### Manual

- [ ] 3.3 Verify the ReadWrite journey and ReadOnly visibility/permission boundary through the composed application.