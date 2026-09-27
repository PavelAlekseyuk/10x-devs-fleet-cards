<!-- IMPL-REVIEW-REPORT -->
# Implementation Review: Card Lifecycle and Overview Corrections

- **Plan**: `context/changes/bug-fixes-after-s04-20260927/plan.md`
- **Scope**: Full plan
- **Reviewed phases**: 1, 2, 3
- **Date**: 2026-09-27
- **Verdict**: NEEDS ATTENTION
- **Findings**: 0 critical, 5 warnings, 3 observations

## Verdicts

| Dimension | Verdict |
|-----------|---------|
| Plan Adherence | WARNING |
| Scope Discipline | WARNING |
| Safety & Quality | WARNING |
| Architecture | PASS |
| Pattern Consistency | PASS |
| Success Criteria | WARNING |

## Findings

### F1 — Destructive cleanup applies automatically at API startup

- **Severity**: ⚠️ WARNING
- **Impact**: 🔬 HIGH — architectural stakes; think carefully before deciding
- **Dimension**: Safety & Quality
- **Location**: `src/FleetCards.Api/Program.cs:88`; `src/FleetCards.Infrastructure/Migrations/20260927120000_MakeFleetCardAssignmentRequired.cs:15`
- **Detail**: Whenever a database connection string is configured, API startup calls `MigrateAsync`. The new migration irreversibly removes null-assignment cards and linked transaction, assignment, and audit facts. The plan requires a backup before application, but startup does not enforce a backup or an explicit operator-controlled deployment step.
- **Fix**: Keep automatic migration application for local development, but require a controlled production migration step with a confirmed backup before applying this destructive migration.
  - Strength: Makes the plan's backup prerequisite operationally enforceable.
  - Tradeoff: Production deployments need a separate migration step.
  - Confidence: HIGH — the startup migration path and destructive SQL are explicit.
  - Blind spot: No production migration pipeline currently exists to assess.
- **Decision**: ACCEPTED — automatic startup migration is intentional.

### F2 — Test dependency brings in vulnerable SSH.NET

- **Severity**: ⚠️ WARNING
- **Impact**: 🔎 MEDIUM — real tradeoff; pause to reason through it
- **Dimension**: Safety & Quality
- **Location**: `tests/FleetCards.Api.Tests/FleetCards.Api.Tests.csproj:16`
- **Detail**: `Testcontainers.MsSql 4.8.1` brings in `Testcontainers 4.8.1`, which brings in `SSH.NET 2024.2.0`. NuGet reports high-severity advisories GHSA-mggc-4xg6-vcxf and GHSA-q939-rpr3-3284. This dependency is in the test project, not the production API.
- **Fix**: Update the Testcontainers dependency chain or pin a patched SSH.NET version, then verify the NuGet vulnerability audit is clear.
  - Strength: Removes known vulnerable code from restore/test environments.
  - Tradeoff: May require checking compatibility with Testcontainers.
  - Confidence: HIGH — confirmed with `dotnet nuget why` and `dotnet list package --vulnerable`.
  - Blind spot: The advisories were limited to the test dependency graph.
- **Decision**: FIXED — upgraded Testcontainers.MsSql to 4.15.0, pinned the SQL Server image, verified no vulnerable packages, and passed all 48 API tests.

### F3 — Older summary responses can overwrite the selected month

- **Severity**: ⚠️ WARNING
- **Impact**: 🔎 MEDIUM — real tradeoff; pause to reason through it
- **Dimension**: Safety & Quality
- **Location**: `web/src/app/shell.component.ts:81`, `web/src/app/shell.component.ts:94`
- **Detail**: Each month navigation click starts a summary request. Existing requests are not cancelled or checked against the currently selected month, so a slower response for an earlier month can arrive last and replace the newer totals while the period caption shows the new month. The test exercises month changes sequentially and does not cover out-of-order responses.
- **Fix**: Cancel/supersede prior summary requests or ignore responses whose requested month no longer matches `selectedMonth`.
  - Strength: Prevents totals from disagreeing with the selected period.
  - Tradeoff: Adds request lifecycle handling to the overview.
  - Confidence: HIGH — subscriptions are retained until response or component destruction.
  - Blind spot: The race was identified from control flow, not reproduced with delayed responses.
- **Decision**: FIXED — ShellComponent cancels the prior summary request before loading a new month; a regression test asserts cancellation and the selected-month savings display.

### F4 — State history cache is not invalidated after a state change

- **Severity**: ⚠️ WARNING
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: `web/src/app/card-management.component.ts:87`, `web/src/app/card-management.component.ts:110`, `web/src/app/card-management.component.ts:161`
- **Detail**: After a successful state change, the component updates the card but leaves `stateHistory[card.id]` cached. If the history panel was already loaded, it remains stale; closing and reopening returns early on the cached history instead of requesting the new event.
- **Fix**: Invalidate and reload the changed card's history after a successful state update when its panel is open.
  - Strength: Keeps the visible history consistent with the persisted card state.
  - Tradeoff: Adds a request after a state mutation.
  - Confidence: HIGH — successful mutations do not clear the cached history.
  - Blind spot: E2E changes state before opening history, so this sequence is not covered.
- **Decision**: FIXED — successful state changes invalidate cached history and reload it for an open panel; a component regression test verifies a fresh request and updated timeline. The Angular suite passes (42 tests).

### F5 — Plan still excludes the authorized transaction-count change

- **Severity**: ⚠️ WARNING
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Scope Discipline
- **Location**: `context/changes/bug-fixes-after-s04-20260927/plan.md:33`; `context/foundation/requirements.md:95`
- **Detail**: The user explicitly requested 5–15 generated transactions per issued card, and the implementation and requirements now state that range. The plan's “What We're NOT Doing” still excludes changes to card-issuance transaction generation beyond pricing fields, so the plan contradicts the authorized implementation.
- **Fix**: Amend the plan's scope guardrail to record the user-approved 5–15 transaction range.
  - Strength: Keeps the plan, requirements, and implementation aligned.
  - Tradeoff: Updates the approved plan after implementation.
  - Confidence: HIGH — the user request and current requirement are explicit.
  - Blind spot: None significant.
- **Decision**: FIXED — updated the plan's scope guardrail to include the user-approved 5–15 previous-month demo transaction range while keeping unrelated generation changes out of scope.

### F6 — Frontend card type still permits a missing assignment

- **Severity**: 👁️ OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Plan Adherence
- **Location**: `web/src/app/card-management.service.ts:11`
- **Detail**: The backend card contract now requires `AssignedCarId`, but the Angular `FleetCard` interface still declares `assignedCarId: string | null`. Runtime responses are non-null, so this does not currently break behavior, but the client contract permits an impossible state and the template retains a no-vehicle fallback.
- **Fix**: Make the client-side `assignedCarId` non-null and remove the obsolete fallback if no other response can omit the vehicle.
  - Strength: Aligns the frontend type with the API/domain invariant.
  - Tradeoff: Requires updating any nullable fixtures or fallback handling.
  - Confidence: HIGH — backend model and API response use a required identifier.
  - Blind spot: None significant.
- **Decision**: FIXED — `FleetCard.assignedCarId` is now non-null; card-management tests (10) and the production build pass.

### F7 — Demo transaction month is based on UTC, not the reporting timezone

- **Severity**: 👁️ OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Success Criteria
- **Location**: `src/FleetCards.Application/MockFuelTransactionGenerator.cs:44`; `src/FleetCards.Application/TransactionReportingService.cs:25`
- **Detail**: The generator derives the previous month from `issuedAtUtc`, while reporting defaults to `Europe/Warsaw`. If a card is issued shortly before a UTC month boundary but after midnight in Warsaw, generation can target the month before the local previous month and the resulting rows can cross the reporting-period boundary.
- **Fix**: Derive the previous business month from issue time converted to the POS/reporting timezone, then generate timestamps within that local month.
  - Strength: Keeps generated data aligned with the selected reporting calendar.
  - Tradeoff: Changes deterministic seed inputs at month boundaries.
  - Confidence: MEDIUM — the timezone mismatch is explicit; boundary behavior is not separately tested.
  - Blind spot: The intended generator timezone is not explicitly stated in the requirements.
- **Decision**: FIXED — generation now chooses the previous Europe/Warsaw month and samples within its UTC boundaries; the issue-at-UTC/local-month-boundary regression test and full .NET suite pass.

### F8 — Seeded correction parity is not directly exercised

- **Severity**: 👁️ OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Success Criteria
- **Location**: `src/FleetCards.Application/FleetCardsApplication.cs:157`; `src/FleetCards.Infrastructure/EfFleetCardsReadService.cs:54`; `tests/FleetCards.Api.Tests/TransactionReportApiTests.cs:141`
- **Detail**: Both read paths call the shared `TransactionReportingService`, and unit tests cover signed correction math. The API correction scenario uses the EF-backed reader, while seeded-mode coverage uses generated purchase facts; there is no test passing signed correction facts through the seeded reader path. Shared calculation reduces the risk, but the plan explicitly asks to verify seeded and EF behavior agree.
- **Fix**: Add a seeded read-service report/summary case with signed correction facts, or document why correction facts cannot be represented in seeded mode and why shared-calculator coverage is sufficient.
  - Strength: Closes the remaining cross-mode coverage question.
  - Tradeoff: Requires constructing a seeded correction fixture or documenting a constrained mode.
  - Confidence: MEDIUM — read paths share the calculator but supply facts differently.
  - Blind spot: The seeded mode has no user-facing correction workflow in this MVP.
- **Decision**: FIXED — added a seeded-read-service test with a signed October refund; it verifies gross savings of -2.46 and report/summary reconciliation. The full .NET suite passes (50 Application, 48 API, 1 Architecture).
