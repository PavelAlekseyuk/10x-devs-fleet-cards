# Portal Color Palette Implementation Plan

## Overview

Replace the portal's coral and warm-neutral theme with a green-led palette inspired by the previously selected BP-like green, lime, yellow, and white colors. Apply it through the existing semantic token system, tune color values for contrast, and preserve red for destructive and error feedback.

## Current State Analysis

`web/src/styles.scss` defines the global semantic color tokens and button/status aliases used by the shell, login, routed views, and shared controls. Its initial `:root` rule also sets foreground and background colors directly, outside the token definitions. Component styles already consume semantic variables, so the palette can be changed centrally rather than by adding per-view color literals.

The portal routes login separately and renders overview, cars, cards, transactions, programs, and admin/demo through the shell. The Playwright visual suite covers login and five signed-in views at desktop and mobile sizes, plus card-control hover/focus states; it does not cover admin/demo or representative error/status states. No automated contrast check is currently evident.

The previously selected public-site color samples were green `#007f00`, lime `#ccff00`, yellow-orange `#edac1a`, and white `#ffffff`. These are inspiration and starting values, not a formal client brand specification. Their measured contrast varies by pairing: white on green is 5.20:1, lime on green is 4.43:1, and yellow-orange on green is 2.61:1.

### Key Discoveries:

- Global semantic values and button/status aliases live in [`web/src/styles.scss`](../../../web/src/styles.scss#L2).
- The shell, navigation, overview, and fallback section consume styles in [`web/src/app/app.scss`](../../../web/src/app/app.scss#L1); login has a separate style surface in [`web/src/app/login.component.scss`](../../../web/src/app/login.component.scss#L10).
- Routed view styles are in [`web/src/app/fleet-vehicles.component.scss`](../../../web/src/app/fleet-vehicles.component.scss), [`web/src/app/card-management.component.scss`](../../../web/src/app/card-management.component.scss), [`web/src/app/transaction-report.component.scss`](../../../web/src/app/transaction-report.component.scss), and [`web/src/app/programs.component.scss`](../../../web/src/app/programs.component.scss); admin/demo is styled in [`web/src/app/admin-demo.component.scss`](../../../web/src/app/admin-demo.component.scss#L82).
- Shared button and status-badge primitives use the same variables ([`button.component.scss`](../../../web/src/app/shared/ui/button.component.scss#L16), [`status-badge.component.scss`](../../../web/src/app/shared/ui/status-badge.component.scss#L12)).
- The current visual suite covers login and five main views at desktop/mobile sizes in [`web/e2e/portal-visual.spec.ts`](../../../web/e2e/portal-visual.spec.ts#L8), but not admin/demo or all feedback states.

## Desired End State

Every portal route and shared control uses a consistent, green-led theme with white surfaces and lime/yellow accents. Palette values may be adjusted to meet contrast requirements; red remains reserved for destructive and error states. Layout, typography, spacing, route behavior, and product workflows remain unchanged.

The semantic-token catalogue describes the portal-wide palette, automated checks verify key rendered foreground/background pairs, and visual coverage makes the theme reviewable on desktop and mobile.

## What We're NOT Doing

- Changing layout, typography, spacing, radii, page structure, navigation, or product behavior.
- Changing routes, authentication, API contracts, persisted data, or backend code.
- Reproducing BP logos, wordmarks, or distinctive brand artwork; only a color direction is used.
- Forcing destructive and error feedback into the brand palette.
- Adding a design-system dependency or replacing the existing semantic-token architecture.

## Implementation Approach

Keep `web/src/styles.scss` as the single source for semantic palette values and aliases. Update the raw root foreground/background declarations there as well, then audit the existing token consumers across all routes and shared primitives; only adjust a consumer where its current semantic mapping prevents the agreed hierarchy. Update the token catalogue to describe the global theme and its color-role guidance.

Add a focused browser test that reads the computed CSS custom properties and checks key text/control contrast pairs. Extend the existing Playwright visual suite to include the entire portal and representative feedback states, then update and review its baselines at the configured desktop and mobile sizes.

## Critical Implementation Details

Contrast must be checked on the actual foreground/background pairs resolved from the page's CSS custom properties, not inferred from isolated swatches. The previous lime/green and yellow/green samples do not meet normal-text contrast; keep bright colors out of small text pairings or tune the palette and supporting shades until the required thresholds pass.

## Phase 1: Define the Accessible Palette

### Overview

Map the selected green-led direction onto the existing semantic token roles, update global root colors and documentation, and add an automated guard for key contrast pairs.

### Changes Required:

#### 1. Global semantic palette

**File**: `web/src/styles.scss`

**Intent**: Replace the current coral/warm-neutral values with a tuned green-led palette using white surfaces and lime/yellow accents, while preserving red destructive/error feedback.

**Contract**: Update the existing foreground, primary, accent, sidebar, background, surface, border, focus, active, warning, and button/status alias values. Make the root foreground and background use the semantic tokens rather than duplicate literal colors. Leave spacing, radii, typography, and behavior tokens unchanged.

#### 2. Palette documentation

**File**: `context/changes/improve-ui/ui-tokens.md`

**Intent**: Keep the readable token catalogue aligned with the new global theme and make its existing style-request guidance accurate for the portal-wide palette.

**Contract**: Record the final token names and values, role meanings, and the red destructive/error exception. Remove the obsolete statement that the BP-like palette is favicon-only; retain guidance to request scoped, contrast-conscious visual changes.

#### 3. Computed contrast regression test

**File**: `web/e2e/palette-contrast.spec.ts`

**Intent**: Prevent future token edits from introducing low-contrast text or control/focus combinations.

**Contract**: Read the rendered CSS custom properties from the application and assert at least 4.5:1 for normal text, 3:1 for large text, and 3:1 for meaningful control boundaries/focus indicators across the key semantic pairs used by the palette. Do not duplicate the production palette as test-only hex values.

### Success Criteria:

#### Automated Verification:

- `cd web; npm run build` succeeds.
- `cd web; npm test` passes the Angular/Vitest suite.
- `cd web; npm run test:e2e -- e2e/palette-contrast.spec.ts` passes against the rendered CSS token values.
- `git diff --check` reports no whitespace errors in the planned changes.

#### Manual Verification:

- Review the login and shell at desktop and mobile widths; confirm green carries primary/navigation emphasis, white remains the main surface, and lime/yellow remain legible accents.
- Confirm destructive/error feedback remains distinct and all text, controls, and focus indicators remain understandable without relying on color alone.

**Implementation Note**: Pause for human confirmation of the palette direction and representative contrast pairings before proceeding to the whole-portal visual baseline phase.

## Phase 2: Verify the Whole Portal

### Overview

Exercise the palette across every route and shared control, expand missing visual coverage, and approve updated desktop/mobile baselines.

### Changes Required:

#### 1. Whole-portal visual coverage

**Files**: `web/e2e/portal-visual.spec.ts`, `web/e2e/portal-visual.spec.ts-snapshots/`

**Intent**: Make the palette observable across the complete client portal and representative states rather than relying only on the default active-state screenshots.

**Contract**: Retain desktop (`1280x800`) and mobile (`390x844`) coverage for login, overview, cars, cards, transactions, and programs. Add admin/demo and representative warning, error/destructive, and focus-visible states with deterministic fixtures. Regenerate the affected baseline images only after visual review.

#### 2. Token-consumer review

**Files**: `web/src/app/app.scss`, `web/src/app/login.component.scss`, routed-view component SCSS, and `web/src/app/shared/ui/`

**Intent**: Confirm that shell, login, routed views, and shared controls consistently express the approved semantic color roles.

**Contract**: Keep component styling token-based; add no one-off literal palette values. Change consumer mappings only when needed to preserve the approved green-led hierarchy and accessible state distinctions. Do not alter layout or interactions.

### Success Criteria:

#### Automated Verification:

- `cd web; npm run test:e2e -- e2e/portal-visual.spec.ts` passes with the reviewed desktop/mobile baselines and added route/state coverage.
- `cd web; npm run test:e2e` passes the complete Playwright suite.
- `git diff --check` reports no whitespace errors in the planned changes.

#### Manual Verification:

- Review login, overview, cars, cards, transactions, programs, and admin/demo at desktop and mobile sizes.
- Inspect representative active, warning, error/destructive, and keyboard-focus states; confirm the palette hierarchy is consistent, text/control contrast is clear, and status meaning remains identifiable without color alone.
- Confirm only the palette changed: page structure, content, typography, spacing, and workflows remain intact.

**Implementation Note**: Pause for human confirmation of the full route/state review before marking the visual baselines complete.

## Testing Strategy

### Unit Tests:

- Run the existing Angular/Vitest suite to protect component behavior while shared token values change.

### Integration Tests:

- Add a Playwright contrast check against computed CSS token values and key rendered pairings.
- Extend the existing visual E2E suite to cover every portal route at desktop/mobile sizes and representative feedback states.

### Manual Testing Steps:

1. Review the selected palette and computed contrast results on login and the shared shell.
2. Inspect all routes at desktop and mobile sizes, including admin/demo and feedback/focus states.
3. Confirm the red destructive/error exception and non-color status cues remain clear.
4. Confirm layout, typography, spacing, and workflows did not change.

## Performance Considerations

No runtime performance impact is expected; the work changes CSS custom-property values and browser-test coverage only.

## Migration Notes

No data migration is required. Existing screenshot baselines will change and must be regenerated and reviewed as part of Phase 2. Consumers of the global tokens will receive the new colors together when the frontend is deployed.

## References

- Related frame and prior identity work: [`context/changes/ui-style-and-icons/frame.md`](../ui-style-and-icons/frame.md) and [`context/changes/ui-style-and-icons/plan.md`](../ui-style-and-icons/plan.md). The prior favicon-only palette boundary is superseded by the latest user decision.
- Existing UI research: [`context/changes/improve-ui/research.md`](../improve-ui/research.md); its earlier narrow-scope recommendation is superseded by the explicit portal-wide request and current tokenized implementation.
- Current token catalogue: [`context/changes/improve-ui/ui-tokens.md`](../improve-ui/ui-tokens.md).
- Frontend build/test scripts: [`web/package.json`](../../../web/package.json).
- Visual-test configuration: [`web/playwright.config.ts`](../../../web/playwright.config.ts).

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append ` — <commit sha>` when a step lands. Do not rename step titles. See `references/progress-format.md`.

### Phase 1: Define the Accessible Palette

#### Automated

- [x] 1.1 `cd web; npm run build` succeeds. — 4bfb750
- [x] 1.2 `cd web; npm test` passes the Angular/Vitest suite. — 4bfb750
- [x] 1.3 `cd web; npm run test:e2e -- e2e/palette-contrast.spec.ts` passes against the rendered CSS token values. — 4bfb750
- [x] 1.4 `git diff --check` reports no whitespace errors in the planned changes. — 4bfb750

#### Manual

- [x] 1.5 Review the login and shell at desktop and mobile widths; confirm the agreed palette hierarchy and readable accents. — 4bfb750
- [x] 1.6 Confirm destructive/error feedback, controls, and focus indicators remain clear without relying on color alone. — 4bfb750

### Phase 2: Verify the Whole Portal

#### Automated

- [x] 2.1 `cd web; npm run test:e2e -- e2e/portal-visual.spec.ts` passes with reviewed desktop/mobile baselines and added route/state coverage. — 163612f
- [x] 2.2 `cd web; npm run test:e2e` passes the complete Playwright suite. — 163612f
- [x] 2.3 `git diff --check` reports no whitespace errors in the planned changes. — 163612f

#### Manual

- [x] 2.4 Review all portal routes at desktop and mobile sizes, including admin/demo and feedback/focus states. — 163612f
- [x] 2.5 Confirm palette consistency, accessible status meaning, and unchanged layout, typography, spacing, and workflows. — 163612f
