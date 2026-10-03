<!-- IMPL-REVIEW-REPORT -->
# Implementation Review: Vehicle-First Fleet Portal UI

- **Plan**: `context/changes/improve-ui/plan.md`
- **Scope**: Full plan
- **Reviewed phases**: 1, 2, 3, 4
- **Date**: 2026-10-03
- **Verdict**: NEEDS ATTENTION
- **Findings**: 0 critical, 1 unresolved warning, 1 documented observation (2 warnings fixed during triage)

## Verdicts

| Dimension | Verdict |
|-----------|---------|
| Plan Adherence | PASS |
| Scope Discipline | PASS |
| Safety & Quality | WARNING |
| Architecture | PASS |
| Pattern Consistency | PASS |
| Success Criteria | PASS |

## Findings

### F1 — Primary button text contrast is low

- **Severity**: ⚠️ WARNING
- **Impact**: 🔎 MEDIUM — real tradeoff; pause to reason through it
- **Dimension**: Safety & Quality
- **Location**: `web/src/styles.scss:15-17`; `web/src/app/shared/ui/button.component.scss:15-21`
- **Detail**: The primary button pairs white text (`--ui-color-primary-foreground`) with coral `#e86c54` at about 3.1:1 contrast. The primitive uses 12px text, so this is below the 4.5:1 contrast target for normal-sized text and may reduce readability for users with low vision.
- **Fix**: Adjust the primary foreground/background token pairing to provide stronger text contrast, then review the affected button states.
  - Strength: Improves readability consistently wherever the primary primitive is used.
  - Tradeoff: May shift the approved coral appearance or require a separate accessible foreground treatment.
  - Confidence: HIGH — the token values and primitive font size are explicit in source.
  - Blind spot: Contrast was not an explicit success criterion, and the plan intentionally limits accessibility scope to basic names and visible focus.
- **Decision**: FIXED — use `--ui-color-foreground` for primary-button text; left global white foreground unchanged for the shell and login.

### F2 — Muted small text contrast is low

- **Severity**: ⚠️ WARNING
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: `web/src/styles.scss:14`
- **Detail**: `--ui-color-muted-subtle` (`#75827a`) has about 3.5:1 contrast on the page background and 4.0:1 on white. It is used for 10px eyebrow and table-header text as well as small record counts across the migrated routes.
- **Fix**: Darken `--ui-color-muted-subtle` enough to meet the normal-text contrast target on both page and white surfaces, then review the snapshots.
- **Decision**: FIXED — alias `--ui-color-muted-subtle` to the existing `--ui-color-muted` token to increase contrast without adding a palette value.

### F3 — Visual baselines are Windows-specific

- **Severity**: ⚠️ WARNING
- **Impact**: 🔎 MEDIUM — real tradeoff; pause to reason through it
- **Dimension**: Safety & Quality
- **Location**: `web/e2e/portal-visual.spec.ts:162,188`; `web/e2e/portal-visual.spec.ts-snapshots/`
- **Detail**: The committed snapshot files are suffixed `-win32.png`, and the Playwright config has no custom snapshot path template. Playwright selects platform-specific snapshot names, so the current visual suite will look for Linux/macOS baselines on those platforms and fail to find the committed Windows images.
- **Fix**: Either add reviewed baselines for every supported OS or pin this visual gate to the Windows environment that produced the baselines.
  - Strength: Makes the supported execution environment explicit and prevents unexplained missing-snapshot failures.
  - Tradeoff: Multi-OS baselines add review and storage work; pinning limits where the visual gate can run.
  - Confidence: HIGH — the existing filenames include `win32` and the config does not override snapshot naming.
  - Blind spot: The repository currently has no CI workflow, so the intended cross-platform runner matrix is not defined.
- **Decision**: DEFERRED — user wants Windows, Linux, and macOS support; generate and review platform-specific baselines on each target OS before claiming cross-platform coverage.

### F4 — Supporting vehicle test changed outside the phase file list

- **Severity**: ℹ️ OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Scope Discipline
- **Location**: `web/src/app/fleet-vehicles.component.spec.ts`
- **Detail**: This test file is not listed in Phase 3 Changes Required, but its selectors were updated to reach the native buttons inside the new shared-component wrappers. The change is limited to test locators and is directly coupled to the planned markup migration; no business assertions were weakened.
- **Fix**: No code change required. Keep this as a documented supporting test adjustment.
- **Decision**: DOCUMENTED — added `fleet-vehicles.component.spec.ts` to the Phase 3 route-migration file list as the supporting selector update.

## Verification

- `cd web; npm test` — PASS (12 files, 57 tests).
- `cd web; npm run build` — PASS; Angular reports the existing `app.scss` 4 kB warning-budget overage, below the 8 kB error budget.
- Phase 3 color-literal scan — PASS (no matches).
- `cd web; npm run test:e2e -- e2e/portal-visual.spec.ts` — PASS (14 tests; desktop/mobile route screenshots and card interaction checks).
- After the approved contrast-token fixes, `npm test` passed (57 tests), `npm run build` passed, the color scan remained clean, and the visual snapshots were refreshed; the normal Playwright run passed all 14 tests.
- Phase 2 token documentation/source comparison — PASS.
- Manual checks — user confirmed the card states and the initial desktop/mobile screenshot diffs, then approved the refreshed diffs after the contrast-token fixes.
