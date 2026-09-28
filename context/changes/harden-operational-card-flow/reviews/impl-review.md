<!-- IMPL-REVIEW-REPORT -->
# Implementation Review: Harden Operational Card Flow

- **Plan**: context/changes/harden-operational-card-flow/plan.md
- **Scope**: Full plan (phases 1, 2, 3)
- **Reviewed phases**: 1, 2, 3
- **Date**: 2026-09-28
- **Verdict**: NEEDS ATTENTION
- **Findings**: 0 critical, 3 warnings, 1 observation

## Verdicts

| Dimension | Verdict |
|-----------|---------|
| Plan Adherence | WARNING |
| Scope Discipline | WARNING |
| Safety & Quality | PASS |
| Architecture | PASS |
| Pattern Consistency | PASS |
| Success Criteria | WARNING |

## Verification

- `dotnet test FleetCards.sln --configuration Release` — PASS, 100 tests.
- `npm test` from `web/` — PASS, 10 test files and 42 tests.
- `npm run build` from `web/` — PASS; existing `app.scss` style budget warning (6.74 kB vs 4.00 kB).
- `npm run test:e2e` from `web/` — PASS, 4 tests against isolated Aspire runtime.
- Workflow review — idempotent migration script is applied before deployment; smoke checks cover root, API metadata, CSRF/login, current user, and client access.
- The first Angular commands were invoked from the repository root and failed because no root `package.json` exists; rerunning from `web/` succeeded.

## Findings

### F1 — Transaction-count assertion includes the current month

- **Severity**: ⚠️ WARNING
- **Impact**: 🏃 LOW — Quick decision. Fix is obvious and narrowly scoped. Safe to batch.
- **Dimension**: Success Criteria
- **Location**: web/e2e/card-management.spec.ts:116, 212
- **Detail**: `getTransactionsForCard` combines the previous and current UTC-month reports, then the new 5–15 assertion counts that combined set. The plan and demo contract require 5–15 persisted previous-month transactions specifically, so this can pass without proving the previous-month count.
- **Fix**: Count only transactions from the previous-month report for the 5–15 assertion.
- **Decision**: FIXED — helper now queries only the previous UTC month; focused composed card-flow E2E passed.

### F2 — Keyboard accessibility is not tested

- **Severity**: ⚠️ WARNING
- **Impact**: 🏃 LOW — Quick decision. Fix is obvious and narrowly scoped. Safe to batch.
- **Dimension**: Plan Adherence
- **Location**: web/e2e/card-management.spec.ts:192, 237
- **Detail**: The added accessibility checks verify accessible names for the issue button and status selector, but do not exercise keyboard navigation or activation. Phase 2 explicitly calls for evidence that core controls are keyboard-reachable; the completed progress item therefore overstates the implemented check.
- **Fix**: Add a focused Playwright keyboard-reachability assertion for the core card controls and record the resulting check.
- **Decision**: FIXED — keyboard-only issuance and status changes now assert tab reachability; focused composed card-flow E2E passed.

### F3 — Changed implementation files are missing from the phase file lists

- **Severity**: ⚠️ WARNING
- **Impact**: 🏃 LOW — Quick decision. Fix is obvious and narrowly scoped. Safe to batch.
- **Dimension**: Scope Discipline
- **Location**: infra/terraform/main.tf:61; web/src/app/language.service.ts:95
- **Detail**: The Terraform Production override for `Database:ApplyMigrations` and the card terminology translation changes are relevant to the intended migration and bilingual-flow contracts, but these implementation files are not named in the corresponding phase `Changes Required` lists. The edits appear purposeful, but the plan's file-level scope does not account for them.
- **Fix**: Add the Terraform settings and Angular translation/spec files to the relevant phase file lists as an implementation addendum.
- **Decision**: FIXED — added the Terraform and Angular implementation files to the corresponding phase file lists.

### F4 — Manual checklist completions have no separate run record

- **Severity**: ℹ️ OBSERVATION
- **Impact**: 🏃 LOW — Quick decision. Fix is obvious and narrowly scoped. Safe to batch.
- **Dimension**: Success Criteria
- **Location**: context/changes/harden-operational-card-flow/plan.md, Progress
- **Detail**: Manual items 1.3, 2.5–2.7, and 3.4–3.5 are checked and cite commit SHAs, but the progress section contains no separate demonstration or review record. The changed docs and passing E2E suite support much of the behavior, but do not independently establish that each claimed manual review was performed.
- **Fix**: Add a short dated evidence note for the manual checks, or leave any unverified manual item pending.
- **Decision**: FIXED — added dated test and documentation-review evidence, explicitly distinguishing automated evidence from unrecorded historical hands-on sessions.