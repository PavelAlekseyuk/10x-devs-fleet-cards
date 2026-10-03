# Vehicle-First Card Management UI — Plan Brief

> Full plan: `context/changes/improve-ui/plan.md`
> Research: `context/changes/improve-ui/research.md`

## What & Why

Make card management easier to scan by vehicle, using the assigned registration plate as the primary identity on each card row. Establish a small shared token and component foundation, then align every current route surface to the portal's existing visual language.

## Starting Point

The card view already has the assigned vehicle plate, card state, issue flow, and status history, but emphasizes the masked PAN first and repeats hardcoded styles. The Angular app has no semantic token layer or shared UI component folder, while its established forest-green, warm-neutral, and coral palette is repeated across the login, authenticated shell, and feature views.

## Desired End State

Card rows lead with the assigned plate, with masked card details and state still visible. Login, overview, cars, cards, transactions, the programs placeholder, and their shared shell use repository-owned semantic tokens and applicable reusable Angular button/status-badge primitives. Interaction states are deliberately covered, and desktop/mobile Playwright screenshots guard all current route surfaces.

## Key Decisions Made

| Decision | Choice | Why | Source |
| --- | --- | --- | --- |
| Scope | Shared foundation + migration of all current routes and shell | Resolves the change note's portal-wide alignment goal while excluding new feature work | Plan |
| Vehicle identity | Assigned registration plate only | Already available on the card; avoids joins that miss archived vehicles | Research + Plan |
| Assignment behavior | Preserve immutable issue-time assignment | Later lifecycle correction and current test supersede the original reassignment plan | Research |
| Visual direction | Refine current forest-green, warm-neutral, coral palette | Maintains visual continuity rather than introducing a second theme | Plan |
| Shared UI layer | Two local standalone primitives; no package dependency | No shared component library exists, and the selected view needs a button and state badge | Research + Plan |
| Visual gate | Playwright screenshots for all current routes at desktop and mobile plus focused state tests | Reuses the existing browser-test stack and protects the portal-wide visual alignment | Plan |

## Scope

**In scope:** create button and status-badge primitives; define role-based global tokens and record their provenance; migrate login, overview, cars, cards, transactions, the programs placeholder, and shared shell; prioritize assigned plate; test the seven card UI states; add desktop/mobile screenshot comparisons for every current route and an agent rule.

**Out of scope:** new programs functionality; changing API/data contracts; showing make/model/fuel on cards; changing assignment behavior; adding dark mode, a third-party UI library, or a lint dependency.

## Architecture / Approach

Add standalone Angular UI primitives under `web/src/app/shared/ui` and semantic CSS custom properties in `web/src/styles.scss`. All current route surfaces consume the tokens and relevant primitives while retaining feature-specific layouts and existing behavior. Vitest covers component states; the existing Aspire-backed Playwright setup covers desktop and mobile screenshots for all current routes.

## Phases at a Glance

| Phase | What it delivers | Key risk |
| --- | --- | --- |
| 1. Local UI Primitives | Importable button and state-badge components | Keep primitives small and preserve native accessibility |
| 2. Semantic UI Tokens | Global role-based values with recorded palette provenance | Assign existing colors to semantic roles without palette drift |
| 3. Vehicle-First Card Management and Route Migration | Card list led by assigned plate and all current routes styled from shared tokens/primitives | Preserve current workflows, permissions, and responsive behavior |
| 4. Interaction States, Portal Visual Gate, and Guard | Seven-state tests, desktop/mobile screenshots for six route surfaces, and agent rule | Keep browser screenshots stable and review baseline changes deliberately |

**Prerequisites:** Existing Angular/Vitest and Aspire-backed Playwright setup; no new package is needed.  
**Estimated effort:** One focused implementation cycle across four small phases.

## Open Risks & Assumptions

- Existing global font loading uses Google Fonts; visual tests wait for `document.fonts.ready` and freeze time where dates/months are rendered.
- There is no dark mode in the inspected app scope; this plan does not add one.
- The programs route is currently a placeholder; this change aligns its presentation without implementing program-management behavior.

## Success Criteria (Summary)

- A user can identify each card's assigned vehicle at a glance without losing masked card, state, history, or permission behavior.
- All current route surfaces use documented semantic tokens and applicable shared repo components, with all seven card interaction states covered.
- Playwright desktop/mobile snapshots pass for all six route surfaces, and the migrated views contain no hex, RGB, or HSL color literals.
