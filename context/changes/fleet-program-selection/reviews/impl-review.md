<!-- IMPL-REVIEW-REPORT -->
# Implementation Review: Fleet Program Selection

- **Plan**: context/changes/fleet-program-selection/plan.md
- **Scope**: Full plan
- **Reviewed phases**: 1, 2, 3, 4, 5, 6, 7
- **Date**: 2026-10-11
- **Verdict**: NEEDS ATTENTION
- **Findings**: 0 critical, 3 warnings, 0 observations

## Verdicts

| Dimension | Verdict |
|-----------|---------|
| Plan Adherence | WARNING |
| Scope Discipline | PASS |
| Safety & Quality | WARNING |
| Architecture | PASS |
| Pattern Consistency | PASS |
| Success Criteria | PASS |

## Verification

- `dotnet test` — PASS: Application 62, API 61, Architecture 1; 124 tests total.
- `cd web && npm test` — PASS: 14 test files, 65 tests.
- `cd web && npx playwright test -g 'generates demo data, compares programs'` — PASS: 1 test.
- `cd web && npx playwright test e2e/portal-visual.spec.ts -g 'programs surface' --update-snapshots` — PASS: desktop and mobile snapshots regenerated, 2 tests.
- `cd web && npx playwright test e2e/portal-visual.spec.ts -g 'programs surface'` — PASS: both updated snapshots match, 2 tests.
- `dotnet test tests/FleetCards.Api.Tests/FleetCards.Api.Tests.csproj --filter "FullyQualifiedName~TransactionReportApiTests|FullyQualifiedName~ProgramApiTests"` — PASS: 15 tests after F1 fix.
- `dotnet test tests/FleetCards.Api.Tests/FleetCards.Api.Tests.csproj --filter FullyQualifiedName~DemoTransactionApiTests` — PASS: 6 tests after F2 fix.
- `cd web && npm test` — PASS after F2 fix: 14 test files, 65 tests.
- The full .NET suite initially exposed an `EfFleetCardService` test-fixture constructor mismatch after the service gained a vehicle dependency. The fixture was updated and the full suite then passed.

## Findings

### F1 — Seeded fallback transactions retain pre-migration totals

- **Severity**: ⚠️ WARNING
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Plan Adherence
- **Location**: src/FleetCards.Infrastructure/SeededFleetCardService.cs:72-76
- **Detail**: The EF seed and Phase 1 migration reset the seeded transaction discount to zero and recompute its totals (`FleetCardsDbContext.cs:365-370`, migration `20261005172757_AddFleetProgramSelection.cs:158-164`). The in-memory `SeededFleetCardService` still seeds `Discount = 0.18m`, `NetTotal = 265.53m`, `VatAmount = 61.07m`, and `GrossTotal = 326.60m`. Because the seeded read service reports this fact set, no-database mode shows a different baseline and discount history from the migrated EF mode, contrary to Phase 1's requirement that seeded services be updated consistently.
- **Fix**: Align the in-memory seeded transaction with the migrated zero-discount amounts and update its expectations.
- **Decision**: FIXED — aligned in-memory seeded transaction values and updated seeded API expectations.

### F2 — Demo generation does not follow the planned default or random card spread

- **Severity**: ⚠️ WARNING
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Plan Adherence
- **Location**: src/FleetCards.Infrastructure/EfFleetCardService.cs:99-119; web/src/app/admin-demo.component.ts:25
- **Detail**: The plan specifies a default count of 10 and random distribution across active cards' vehicles. The UI initializes the count to 20. Both seeded and EF services sort eligible cards by ID and assign transactions to the first `min(count, eligibleCards.Length)` cards, so when count is less than the active-card count the chosen subset is deterministic rather than randomly spread.
- **Fix**: Use the planned default of 10 and randomize eligible-card allocation while preserving the requested total count.
- **Decision**: FIXED — default count is 10 and both service modes shuffle eligible active cards before evenly distributing the requested count; the web spec asserts the default.

### F3 — Migration rollback leaves transaction values permanently rewritten

- **Severity**: ⚠️ WARNING
- **Impact**: 🔎 MEDIUM — real tradeoff; pause to reason through it
- **Dimension**: Safety & Quality
- **Location**: src/FleetCards.Infrastructure/Migrations/20261005172757_AddFleetProgramSelection.cs:147-175, 177-202
- **Detail**: The planned one-time data fix updates every row in `Transactions` to zero discount and recomputed totals. `Down` removes the new assignment/rule tables and restores the old program name but does not restore any transaction values. Rolling back therefore leaves transaction facts changed while reverting the schema and program catalog; the previous discounts cannot be reconstructed from the resulting values.
- **Fix A ⭐ Recommended**: Preserve the affected original transaction values in a migration-owned backup table and restore them in `Down`.
  - Strength: Makes rollback restore the pre-migration data as well as the schema.
  - Tradeoff: Adds temporary schema and rollback complexity for a one-time migration.
  - Confidence: HIGH — the current `Down` contains no transaction restoration.
  - Blind spot: No production backup/rollback operational policy was reviewed.
- **Fix B**: Explicitly make this migration forward-only and document the required pre-migration backup and recovery procedure.
  - Strength: Avoids retaining sensitive historical state in a backup table and acknowledges the accepted one-time data transformation.
  - Tradeoff: Database rollback cannot return the transaction values to their prior state.
  - Confidence: MED — depends on the deployment recovery contract, which is not in this change.
  - Blind spot: Deployment procedures are not implemented in this repository.
- **Decision**: FIXED — Fix B; documented the migration as forward-only, added migration-level warnings, and specified the required verified pre-migration backup/restore procedure.
