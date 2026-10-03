# Vehicle-First Fleet Portal UI Implementation Plan

## Overview

Establish a small, repo-owned UI foundation, make vehicle identity the primary organizing detail in card management, and align every current route surface to one visual language. Preserve the existing green, warm-neutral, and coral palette and all existing behavior.

## Current State Analysis

- The Angular app uses standalone components and component-scoped SCSS. `web/src/styles.scss` contains root foreground/background literals but no semantic token layer; repeated colors and primitive styles live in the shell and individual view stylesheets (`web/src/app/app.scss`, `web/src/app/card-management.component.scss`, `web/src/app/fleet-vehicles.component.scss`, `web/src/app/transaction-report.component.scss`).
- No shared UI component directory was found under `web/src/app`. The card view duplicates button styling and gives the masked PAN earlier visual emphasis than the assigned registration plate (`web/src/app/card-management.component.html:56-69`).
- The existing `FleetCard` response supplies its assigned vehicle ID and registration plate but not make, model, or fuel type (`web/src/app/card-management.service.ts:7-12`). The card view's vehicle selector requests active vehicles, so enriching an assigned card from that list would not cover an archived assigned vehicle (`web/src/app/card-management.component.ts:163-168`).
- Card management already has loading, empty, error, disabled, state-change, and history UI. Its component tests cover API interactions, permissions, and localization (`web/src/app/card-management.component.spec.ts`).
- Playwright is configured for Aspire-backed E2E tests, but the inspected E2E suite has no screenshot assertions (`web/playwright.config.ts`, `web/e2e/card-management.spec.ts`). Angular's unit-test builder uses Vitest (`web/angular.json`).
- Current route surfaces are login, overview, cars, cards, transactions, and the programs placeholder; all five authenticated content areas share the shell component and stylesheet (`web/src/app/app.routes.ts`, `web/src/app/shell.component.html`, `web/src/app/app.scss`).
- The earlier card plan allowed reassignment, but the later lifecycle-corrections plan and current tests make issue-time assignment immutable (`context/changes/bug-fixes-after-s04-20260927/plan.md:25,239`, `web/src/app/card-management.component.spec.ts:166-173`).

## Desired End State

Card rows are easy to scan by assigned vehicle registration plate, with masked card information and state clearly available as secondary details. All current route surfaces—login, overview, cars, cards, transactions, and the programs placeholder—use the same semantic design tokens and visual language. Relevant buttons and status badges use small, importable Angular UI primitives stored in the repository, with the current palette preserved and no new package dependency.

The default, hover, focus-visible, disabled, error, empty, and loading states are deliberately covered. Desktop and mobile Playwright snapshots guard all current route surfaces, including the authenticated shell.

### Key Discoveries:

- `web/src/app/card-management.component.html:56-69` orders masked PAN, vehicle plate, and state in that sequence today.
- `web/src/app/card-management.service.ts:7-12` makes the assigned registration plate available on each card without an extra request.
- `web/src/styles.scss:2` is the global stylesheet entry point; it currently has raw root colors rather than semantic CSS variables.
- The shell and card styles each define `.primary-button` independently (`web/src/app/app.scss:35`, `web/src/app/card-management.component.scss:79-97`).
- There is no dark-mode selector in the inspected `web/src` scope. Do not add dark mode as part of this change.

## What We're NOT Doing

- Changing API or card data contracts to add make/model/fuel type, changing assigned-vehicle archival behavior, or introducing a new vehicle lookup.
- Restoring card reassignment or otherwise changing the issue-time assignment invariant.
- Implementing new program-management functionality beyond aligning the existing placeholder route.
- Rebranding the portal, adding dark mode, or adding a third-party design system or lint dependency.

## Implementation Approach

Follow the UI sequence of local component foundation, semantic token values, all current route surfaces, then state and visual verification. Create only the button and status-badge primitives needed by current views; derive token values from the current in-repo palette and record their provenance. Keep layout local to each feature while moving suitable shared-looking controls onto the importable primitives. Use the existing Vitest and Playwright toolchains; preserve view behavior and avoid adding dependencies.

## Phases at a Glance

| Phase | What it delivers | Key risk |
| --- | --- | --- |
| 1. Local UI Primitives | Importable button and state-badge components | Keep primitives small and preserve native accessibility |
| 2. Semantic UI Tokens | Global role-based values with recorded palette provenance | Assign existing colors to semantic roles without palette drift |
| 3. Vehicle-First Card Management and Route Migration | Vehicle-first card rows and shared styling across all current routes | Preserve each view's behavior and responsive layout while migrating styles |
| 4. Interaction States, Portal Visual Gate, and Guard | Seven-state card checks, six-route desktop/mobile screenshots, and agent rule | Keep screenshots deterministic and review baseline changes deliberately |

## Phase 1: Local UI Primitives

### Overview

Add the smallest repository-owned standalone Angular primitives needed by current portal views without introducing a UI library.

### Changes Required:

#### 1. Shared button and status badge

**File**: `web/src/app/shared/ui/button.component.ts`, `web/src/app/shared/ui/button.component.html`, `web/src/app/shared/ui/button.component.scss`, `web/src/app/shared/ui/button.component.spec.ts`, `web/src/app/shared/ui/status-badge.component.ts`, `web/src/app/shared/ui/status-badge.component.html`, `web/src/app/shared/ui/status-badge.component.scss`, `web/src/app/shared/ui/status-badge.component.spec.ts`

**Intent**: Provide readable, importable primitives so views do not rebuild buttons or status badges with one-off markup and styles. Keep the components standalone and limited to the variants and state tones already needed by the portal.

**Contract**: The button primitive renders a native button, preserves submit/button type and disabled behavior, exposes primary/quiet/text variants, and keeps projected visible text as its accessible name. The status badge accepts a label and semantic tone for active/warning states. Both consume the semantic tokens introduced in Phase 2.

### Success Criteria:

#### Automated Verification:

- `cd web; npm test` passes with focused tests for native button type, accessible name, disabled behavior, variants, and status-badge tones.
- `cd web; npm run build` passes with the new standalone components under strict Angular template checking.

## Phase 2: Semantic UI Tokens

### Overview

Publish a role-based token source for the shared portal UI foundation, preserving the established palette instead of choosing an unrelated theme.

### Changes Required:

#### 1. Global token source and recorded values

**File**: `web/src/styles.scss`, `context/changes/improve-ui/ui-tokens.md`

**Intent**: Give all current views one source for shared visual decisions while retaining the existing per-component layout convention. Record the exact values used and where they came from so future work can adopt the same system without inventing another palette.

**Contract**: Add semantic CSS custom properties for foreground, primary/accent, surface, border, muted, destructive, status, and focus roles, plus the small spacing/radius/type scales needed by current views. Values refine colors already present in the repo; the token note records each raw value and its source, and the token block includes a concise provenance note. View styles are migrated in Phase 3.

### Success Criteria:

#### Automated Verification:

- `cd web; npm run build` passes with the token definitions available through the configured global stylesheet.
- `context/changes/improve-ui/ui-tokens.md` and the source provenance note agree on the raw values added to `web/src/styles.scss`.

#### Manual Verification:

- Confirm the recorded roles preserve the approved forest-green, warm-neutral, and coral direction and do not introduce a second palette.

## Phase 3: Vehicle-First Card Management and Route Migration

### Overview

Rework card summaries around the assigned registration plate, then apply the shared tokens and relevant primitives to every current route surface without changing API behavior.

### Changes Required:

#### 1. Card summary hierarchy and interaction styling

**File**: `web/src/app/card-management.component.html`, `web/src/app/card-management.component.scss`, `web/src/app/card-management.component.ts`, `web/src/app/card-management.component.spec.ts`

**Intent**: Make the assigned car immediately identifiable when scanning card rows, while keeping masked card data, state, and state history clear and secondary. Replace duplicated button/badge presentation and hardcoded palette values with the shared primitives and semantic tokens.

**Contract**: Use `assignedVehicleRegistrationPlate` as the prominent vehicle identity and retain the existing no-vehicle fallback; do not infer vehicle details from the active issue-selector list. Keep card issue, state changes, status history, permissions, and provider-token redaction behavior unchanged. Add explicit token-driven hover and focus-visible styles and preserve the existing responsive layout and loading/empty/error behavior.

#### 2. Migrate every current route surface

**Files**: `web/src/app/app.scss`, `web/src/app/shell.component.html`, `web/src/app/login.component.html`, `web/src/app/login.component.scss`, `web/src/app/fleet-vehicles.component.html`, `web/src/app/fleet-vehicles.component.scss`, `web/src/app/fleet-vehicles.component.spec.ts`, `web/src/app/transaction-report.component.html`, `web/src/app/transaction-report.component.scss`

**Intent**: Align the authenticated shell (including overview and the programs placeholder), login, vehicle management, and transaction report with the same semantic palette and interaction treatment as cards.

**Contract**: Replace raw palette literals in these route surfaces with the Phase 2 tokens; reuse the shared button and status-badge primitives where they match existing semantics. Preserve navigation, responsive behavior, localization, access restrictions, data presentation, and all current feature interactions. Do not create programs functionality or redesign data/API contracts.

### Success Criteria:

#### Automated Verification:

- `cd web; npm test` passes with existing component coverage preserved, including card vehicle-first hierarchy, issue/state/history behavior, localization, and permissions.
- `cd web; npm run build` passes with the current route surfaces consuming the shared tokens and applicable UI primitives.
- `rg -n -i --glob '*.scss' --glob '*.html' --glob '*.ts' '(#[0-9a-f]{3,8}\b|\b(?:rgba?|hsla?)\s*\()' web/src/app` reports no matches in the migrated route surfaces. For this gate, color literals mean hex, RGB, or HSL notation.

#### Manual Verification:

- Inspect login, overview, cars, cards, transactions, and programs at 1280×800 desktop and 390×844 mobile widths; confirm each remains usable and visually aligned.
- Review hover and keyboard focus-visible feedback on interactive controls across all route surfaces; confirm card assignment, permissions, history, localization, and transaction behavior remain unchanged.

## Phase 4: Interaction States, Portal Visual Gate, and Guard

### Overview

Make the required card interaction states verifiable, protect the portal's current route surfaces with desktop/mobile visual comparisons, and keep future view work pointed at the shared tokens and components.

### Changes Required:

#### 1. State and visual regression coverage

**Files**: `web/src/app/card-management.component.spec.ts`, `web/e2e/portal-visual.spec.ts`

**Intent**: Protect card behavior and make all current route surfaces' desktop/mobile presentation reviewable through the existing Playwright setup.

**Contract**: Cover default, disabled, error, empty, and loading DOM behavior in Vitest without changing business assertions to match visual output. In Playwright, exercise hover on card action buttons and keyboard focus-visible on the vehicle selector, issue action, card-state selector, and history button; assert actual focus and capture the hover/focus treatment for review. Add Playwright screenshots for login, overview, cars, cards, transactions, and programs at 1280×800 and 390×844. Use deterministic seeded content, freeze time where rendered dates/months could vary, wait for `document.fonts.ready`, and capture the viewport consistently. Create initial baselines with `cd web; npm run test:e2e -- e2e/portal-visual.spec.ts --update-snapshots`, review the generated diffs, then verify with the normal test command; update baselines only for intentional, reviewed visual changes.

#### 2. Persistent UI rule

**File**: `AGENTS.md`

**Intent**: Direct future agents to the token and component sources rather than allowing route-specific palette or primitive drift to return.

**Contract**: Add a concise UI block outside any generated CLI block that names `web/src/styles.scss` as the token source and `web/src/app/shared/ui` as the primitive source, directs contributors to reuse these primitives before adding one, and prohibits new literal colors or one-off palette values in new or updated views. Document the exact Phase 3 `rg` command and define the scanned literal forms as hex, RGB, or HSL; no lint dependency is added.

### Success Criteria:

#### Automated Verification:

- `cd web; npm test` passes with component assertions for default, disabled, error, empty, and loading card states and retains existing card workflow coverage.
- The Playwright visual test verifies keyboard focus on card controls and captures hover/focus-visible treatment in a real browser.
- `cd web; npm run test:e2e -- e2e/portal-visual.spec.ts` passes with desktop and mobile screenshot comparisons for all six route surfaces.
- `cd web; npm run build` passes after the state and guard changes.

#### Manual Verification:

- Inspect each of the seven card states for readable content, accessible control names, visible keyboard focus, and token-driven feedback.
- Review all desktop and mobile route screenshot diffs; accept only diffs that match the approved portal-wide visual alignment.

## Testing Strategy

### Unit Tests:

- Exercise the two local UI primitives, including native button semantics, disabled state, accessible projected text, and active/warning badge tones.
- Exercise card default, loading, empty, error, issue-validation, disabled, vehicle-first, state, history, permission, and EN/PL rendering behavior through the existing Vitest component spec.
- Keep existing vehicle, transaction-report, shell, login, and routing tests passing to guard behavior across every migrated route.

### Integration Tests:

- Use the existing Aspire-backed Playwright setup for stable desktop and mobile screenshots of all six route surfaces, with deterministic data, frozen time for date-dependent content, and fonts ready before capture.
- Keep current composed card workflow assertions in `web/e2e/card-management.spec.ts` intact; the visual gate must not weaken assignment immutability, redaction, authorization, or transaction-history assertions.

### Manual Testing Steps:

1. Inspect login and sign-in behavior at desktop and mobile sizes.
2. Sign in as the ReadWrite user; inspect overview, cars, cards, transactions, and programs, confirming shared visual language and preserved content.
3. Use only the keyboard to navigate the shell and each page's controls; confirm visible focus, disabled behavior, and access restrictions.
4. Review default, hover, focus-visible, disabled, error, empty, and loading card presentations; compare every approved route screenshot at desktop and mobile.

## Performance Considerations

The change adds only local presentation components and CSS variables; it makes no new requests and changes no card or vehicle payloads.

## Migration Notes

No data migration or API compatibility work is required. All current route surfaces adopt the shared tokens in this change; future views should reuse the same tokens and applicable primitives.

## References

- Research: `context/changes/improve-ui/research.md`
- UI acceptance checklist: `.github/skills/10x-ui/references/ui-quality-checklist.md`
- Existing routed surfaces: `web/src/app/app.routes.ts`, `web/src/app/shell.component.html`, `web/src/app/login.component.html`
- Existing card view and tests: `web/src/app/card-management.component.html`, `web/src/app/card-management.component.spec.ts`
- Existing browser acceptance path: `web/e2e/card-management.spec.ts`, `web/playwright.config.ts`

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append ` — <commit sha>` when a step lands. Do not rename step titles.

### Phase 1: Local UI Primitives

#### Automated

- [x] 1.1 `cd web; npm test` passes with focused tests for native button type, accessible name, disabled behavior, variants, and status-badge tones. — 3058fc2
- [x] 1.2 `cd web; npm run build` passes with the new standalone components under strict Angular template checking. — 3058fc2

### Phase 2: Semantic UI Tokens

#### Automated

- [x] 2.1 `cd web; npm run build` passes with the token definitions available through the configured global stylesheet. — 210e511
- [x] 2.2 `context/changes/improve-ui/ui-tokens.md` and the source provenance note agree on the raw values added to `web/src/styles.scss`. — 210e511

#### Manual

- [x] 2.3 Confirm the recorded roles preserve the approved forest-green, warm-neutral, and coral direction and do not introduce a second palette. — 210e511

### Phase 3: Vehicle-First Card Management and Route Migration

#### Automated

- [x] 3.1 `cd web; npm test` passes with component coverage for vehicle-first hierarchy, issue/state/history behavior, localization, permissions, and no reassignment controls or requests. — 579d012
- [x] 3.2 `cd web; npm run build` passes with the card page consuming the shared UI primitives. — 579d012
- [x] 3.3 `rg -n -i --glob '*.scss' --glob '*.html' --glob '*.ts' '(#[0-9a-f]{3,8}\b|\b(?:rgba?|hsla?)\s*\()' web/src/app` reports no matches in the migrated route surfaces. For this gate, color literals mean hex, RGB, or HSL notation. — 579d012

#### Manual

- [x] 3.4 Inspect login, overview, cars, cards, transactions, and programs at 1280×800 desktop and 390×844 mobile widths; confirm each remains usable and visually aligned. — 579d012
- [x] 3.5 Review hover and keyboard focus-visible feedback on interactive controls across all route surfaces; confirm existing behavior remains unchanged. — 579d012

### Phase 4: Interaction States, Portal Visual Gate, and Guard

#### Automated

- [x] 4.1 `cd web; npm test` passes with component assertions for default, disabled, error, empty, and loading card states and retains existing workflow coverage. — 81245e6
- [x] 4.2 `cd web; npm run test:e2e -- e2e/portal-visual.spec.ts` verifies card-control hover and keyboard focus-visible feedback in a real browser. — 81245e6
- [x] 4.3 `cd web; npm run test:e2e -- e2e/portal-visual.spec.ts` passes with desktop and mobile screenshot comparisons for all six route surfaces. — 81245e6
- [x] 4.4 `cd web; npm run build` passes after the state and guard changes. — 81245e6

#### Manual

- [x] 4.5 Inspect each of the seven card states for readable content, accessible control names, visible keyboard focus, and token-driven feedback. — 81245e6
- [x] 4.6 Review all desktop and mobile route screenshot diffs; accept only diffs that match the approved portal-wide visual alignment. — 81245e6
