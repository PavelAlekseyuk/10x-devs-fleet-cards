<!-- IMPL-REVIEW-REPORT -->
# Implementation Review: Issue and Assign Virtual Cards

- **Plan**: `context/changes/issue-and-assign-virtual-cards/plan.md`
- **Scope**: Full plan
- **Reviewed phases**: 1, 2, 3, 4
- **Date**: 2026-09-26
- **Verdict**: APPROVED
- **Findings**: 0 critical, 3 warnings fixed, 1 observation accepted

## Verdicts

| Dimension | Verdict |
|-----------|---------|
| Plan Adherence | PASS |
| Scope Discipline | PASS |
| Safety & Quality | PASS |
| Architecture | PASS |
| Pattern Consistency | PASS |
| Success Criteria | PASS |

## Findings

### F1 — EF card mutations are vulnerable to concurrent eligibility and ordering races

- **Severity**: ⚠️ WARNING
- **Impact**: 🔎 MEDIUM — real tradeoff; pause to reason through it.
- **Dimension**: Safety & Quality
- **Location**: `src/FleetCards.Infrastructure/EfFleetCardService.cs:72`, `:107`, `:181`, `:196`, `:217`
- **Detail**: Issue and reassignment validate vehicle eligibility before opening the transaction. A concurrent vehicle archive can therefore race with the write and leave a card assigned to an archived vehicle. Reassignment also reads the previous assignment and calculates the next `OrderingKey` before its transaction; two concurrent reassignments can choose the same key. The unique index rejects one write, but this path has no controlled conflict/retry handling, so it can surface as an unhandled database error.
- **Fix**: Serialize or revalidate eligibility and allocate the next ordering key within a concurrency-safe transaction; handle ordering collisions as a controlled retry or conflict.
- **Decision**: FIXED — use serializable transactions around eligibility/order reads and writes, recheck vehicle eligibility after provider issuance, and map SQL ordering/deadlock conflicts to `409`; added a provider-window archive regression test.

### F2 — Reassignment can leave the visible timeline stale

- **Severity**: ⚠️ WARNING
- **Impact**: 🏃 LOW — quick decision; the fix is narrow.
- **Dimension**: Safety & Quality
- **Location**: `web/src/app/card-management.component.ts:128`, `:137`, `:143`
- **Detail**: Assignment history is cached by card ID. A successful reassignment updates the card but does not invalidate or reload that cache. If a user has already opened the timeline, it can continue showing the old periods after the reassignment succeeds.
- **Fix**: Invalidate the card's cached history and reload it when the timeline is open after successful reassignment.
- **Decision**: FIXED — invalidate the history cache on reassignment and reload an already-open timeline; the added component regression test and Angular suite pass (24 tests).

### F3 — Runtime transaction generation always uses the default VAT rate

- **Severity**: ⚠️ WARNING
- **Impact**: 🏃 LOW — quick decision; the fix is narrow.
- **Dimension**: Plan Adherence
- **Location**: `src/FleetCards.Infrastructure/FleetCardsInfrastructure.cs:21`, `:34`; `src/FleetCards.Application/MockFuelTransactionGenerator.cs:23`
- **Detail**: The generator accepts a VAT rate and defaults to 23%, but both real dependency-injection registrations construct it without configuration. The configured-rate plan contract is therefore not available in the runtime services, even though the rate is overrideable in tests.
- **Fix**: Bind a VAT setting from application configuration when registering `MockFuelTransactionGenerator`, retaining 23% as the MVP default.
- **Decision**: FIXED — bind `Transactions:VatRate` in both EF and seeded DI registrations, defaulting to 23%; added a test covering configured VAT in both modes.

### F4 — Assignment history displays the vehicle's current plate

- **Severity**: ℹ️ OBSERVATION
- **Impact**: 🔎 MEDIUM — a real historical-data tradeoff; pause to reason through it.
- **Dimension**: Safety & Quality
- **Location**: `src/FleetCards.Domain/FleetCardsModel.cs:70`; `src/FleetCards.Infrastructure/EfFleetCardService.cs:240`, `:247`
- **Detail**: Assignment events store the vehicle ID but no plate snapshot. The history query joins each event to the vehicle's current `RegistrationPlate`; since vehicle updates can change that value, a past assignment's displayed plate can change retroactively. The plan's event shape does not explicitly require a plate snapshot, so this is a historical-label risk rather than a direct contract mismatch.
- **Fix**: Consider storing a registration-plate snapshot on assignment events in a follow-up if historical labels must remain immutable.
- **Decision**: SKIPPED — the user accepts current vehicle plate labels in historical assignment periods; preserving vehicle ID and effective dates is sufficient for this MVP, and immutable plate labels can be revisited if required.

## Review Notes

- The composed E2E intentionally does not inspect persisted SQL audit rows because no supported observation path exists; this was explicitly approved. Phase 1/2 tests verify audit creation. No diagnostic API endpoint or SQL driver was added.
- The roadmap `S-03` status update is the required implementation-entry sync, not unrelated scope.
- Manual checks in Progress were confirmed by the user during this session.

## Verification

- `dotnet test tests/FleetCards.Application.Tests/FleetCards.Application.Tests.csproj`: 35 passed after triage fixes.
- EF `has-pending-model-changes`: no changes; migration SQL script generated. The installed EF tool is 10.0.0 while runtime packages are 10.0.12.
- `dotnet test tests/FleetCards.Api.Tests/FleetCards.Api.Tests.csproj`: 36 passed.
- `npm test`: 24 passed across 7 files.
- `npm run build`: passed; existing shared `app.scss` exceeded its 4 kB component-style budget by 2.14 kB.
- `npm run test:e2e`: 3 passed through Aspire with an isolated SQL database, including after F1/F3 fixes.
- `dotnet test`: 72 passed after triage fixes.
- Deliberate-break checks confirmed API vehicle-plate assertions, UI provider-token redaction assertions, and composed transaction-snapshot assertions detect their respective regressions.
- F1 fix verification: a provider-window vehicle archival is rejected before card, assignment, or transaction rows are created; serializable reassignments map assignment-order/deadlock conflicts to `409`.
- F2 fix verification: reassignment invalidates loaded assignment history and refreshes an open timeline; Angular suite passes 24 tests and production build succeeds.
- F3 fix verification: `Transactions:VatRate` is honored in EF and seeded service registrations; default remains 23%.
