<!-- IMPL-REVIEW-REPORT -->
# Implementation Review: Review Generated Card Spending

- **Plan**: `context/changes/review-generated-card-spending/plan.md`
- **Scope**: Full plan
- **Reviewed phases**: 1, 2, 3, 4
- **Date**: 2026-09-26
- **Verdict**: APPROVED
- **Findings**: 0 critical, 1 warning, 1 observation

## Verdicts

| Dimension | Verdict |
|-----------|---------|
| Plan Adherence | PASS |
| Scope Discipline | PASS |
| Safety & Quality | PASS |
| Architecture | PASS |
| Pattern Consistency | PASS |
| Success Criteria | PASS |

## Verification

- `dotnet test tests/FleetCards.Application.Tests/FleetCards.Application.Tests.csproj` — PASS, 48 tests.
- `dotnet test tests/FleetCards.Api.Tests/FleetCards.Api.Tests.csproj` — PASS, 47 tests.
- `npm test` — PASS, 37 tests.
- `npm run build` — PASS. Angular reports the existing `app.scss` style-budget warning: 6.74 kB against 4 kB; the committed baseline was already over budget.
- `npm run test:e2e` — PASS, 4 Playwright specs through the isolated Aspire SQL database.
- `dotnet test` — PASS, 96 tests.
- Deliberate mismatch check — PASS: replacing aggregate gross with net made the SQL-backed reconciliation assertion fail (`326.60` expected, `265.53` received); the production calculation was restored and the focused E2E passed.
- EF migration — generated SQL, model snapshot check, and Aspire SQL-backed startup/read verified the migration and preservation of the seeded transaction and vehicle snapshot.
- Post-triage verification — `dotnet test` PASS, 96 tests; `npm test` PASS, 37 tests; `npm run build` PASS with the existing stylesheet-budget warning; `npm run test:e2e` PASS, 4 specs.
- Manual verification — the user confirmed the seeded reporting journey across English/Polish, timezone display, overview/report totals, and POS timestamp stability.

The user explicitly deferred cross-month reporting UI E2E because other seeded months have no transactions. The final browser flow stays on the deterministic September seed; generated-card reports continue to be checked through the existing card E2E's API calls. This is an accepted scope limitation, not unauthorized drift.

## Findings

### F1 — Overview defaults to the current month, not last month

- **Severity**: ⚠️ WARNING
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Plan Adherence
- **Location**: `web/src/app/transaction-report.service.ts:59`
- **Detail**: The shared report period initializes to the current calendar month in the selected timezone. Card issuance generates its 1–5 deterministic transactions in the preceding UTC month (`src/FleetCards.Application/MockFuelTransactionGenerator.cs:44`), while the plan promises the overview's last-month spending total (`context/changes/review-generated-card-spending/plan.md:18`). After a card is issued, the initial overview/report period can therefore omit those generated transactions until the user manually changes months.
- **Fix**: Default the shared selected period to the previous calendar month in the user's timezone and add a test proving newly generated prior-month spending is visible in the initial summary and report.
- **Decision**: FIXED — the shared default now selects the previous calendar month in the configured timezone; `npm test` passes (37 tests).

### F2 — Seeded transaction inputs do not derive the stored net

- **Severity**: ⚠️ WARNING
- **Impact**: 🔎 MEDIUM — real financial-data discrepancy; confirm which seeded fact is authoritative
- **Dimension**: Safety & Quality
- **Location**: `src/FleetCards.Infrastructure/SeededFleetCardService.cs:67`
- **Detail**: The seeded row stores quantity `42.125`, unit price `6.49`, discount `0.18`, and net `265.53` (`src/FleetCards.Infrastructure/SeededFleetCardService.cs:67`; the EF seed duplicates these values in `src/FleetCards.Infrastructure/FleetCardsDbContext.cs:313`). The shared generator formula is `quantity * (unitPrice - discount)` with away-from-zero rounding (`src/FleetCards.Application/TransactionReportingService.cs:150`), which gives `265.81`, not `265.53`. VAT and gross are consistent with the stored net, but the displayed row inputs do not reconcile to that net. The tests assert both sets of values independently and do not catch the mismatch.
- **Fix**: Establish which seeded amount/input is authoritative, align both EF and fallback seed facts with the shared calculation, and add a test that derives the stored net from the displayed quantity, price, and discount.
- **Decision**: FIXED — seeded quantity is 42.081 in both runtime paths; the EF regression test derives net/VAT/gross, and migration `20260926195450_ReconcileSeededTransactionQuantity` updates existing SQL rows. Application tests pass (48) and Aspire E2E passes (4).

### F3 — Plan and roadmap retain superseded cancellation rules

- **Severity**: 👁 OBSERVATION
- **Impact**: 🏃 LOW — documentation-only consistency fix
- **Dimension**: Plan Adherence
- **Location**: `context/changes/review-generated-card-spending/plan.md:47`
- **Detail**: The plan still says `Cancelled` contributes zero and describes correction economics as unresolved, while locked decision 15 says cancellations are separate signed reversal events (`context/foundation/decisions.md:19`). The S-04 roadmap still labels the same decision as a blocking unknown (`context/foundation/roadmap.md:148`). The implementation follows the approved signed-event semantics, but the remaining source documents could mislead a future change.
- **Fix**: Update the plan's superseded critical-detail text and the S-04 roadmap unknown/blocker to reflect locked decision 15.
- **Decision**: FIXED — the plan's background and critical guidance now match decision 15, and the resolved S-04 roadmap blocker/open question was removed without changing Status.