# Portal Color Palette — Plan Brief

> Full plan: `context/changes/portal-color-palette/plan.md`
> Prior frame: `context/changes/ui-style-and-icons/frame.md` (original scope corrected by the latest user decision)

## What & Why

The portal already has shared semantic color tokens, but the user has clarified that the BP-inspired green/lime/yellow/white direction should apply to the whole portal, not only the favicon. This plan remaps the existing token roles, tunes values for contrast, and verifies every route without changing product behavior or the page layout.

## Starting Point

Global color tokens in `web/src/styles.scss` already feed the shell, login, routed pages, and shared controls; the root foreground/background declarations also need to be moved onto those semantic tokens. The current visual suite covers login and five views at desktop/mobile sizes, but omits admin/demo and representative feedback states.

## Desired End State

The complete client portal has a consistent, green-led palette with white surfaces and lime/yellow accents, with red retained for destructive/error feedback. Key text and control pairs meet WCAG AA contrast targets, the token catalogue describes the global palette, and reviewed visual baselines cover every route at desktop and mobile sizes.

## Key Decisions Made

| Decision | Choice | Why | Source |
| --- | --- | --- | --- |
| Palette hierarchy | Green primary/navigation, white surfaces, lime/yellow accents | Keeps the requested colors recognizable without overwhelming dense screens. | Plan |
| Values | Tune the previous public-site samples for contrast | The earlier lime/green pair measured 4.43:1 and yellow/green 2.61:1. | Plan |
| Destructive/error colors | Retain red as a semantic exception | Preserves familiar danger feedback while the rest of the portal adopts the brand direction. | Plan |
| Accessibility | WCAG AA: 4.5:1 normal text, 3:1 large text and meaningful controls/focus | Prevents bright accents from reducing readability or focus visibility. | Plan |
| Plan boundary | Separate `portal-color-palette` change | Preserves the already committed browser-identity phase in the implementing `ui-style-and-icons` change. | Plan |
| Visual scope | All routes and shared controls; keep layout and behavior | Matches the user's portal-wide palette request without expanding into a general redesign. | User |

## Scope

**In scope:**
- Remap global semantic color tokens and root colors.
- Update token documentation and add computed-contrast regression coverage.
- Review all route/shared token consumers and add whole-portal visual coverage, including admin/demo and representative feedback/focus states.
- Review desktop/mobile visual baselines.

**Out of scope:**
- Layout, typography, spacing, route, behavior, API, and data changes.
- Replacing the semantic-token architecture or copying BP marks/logos.
- Recoloring destructive/error feedback away from red.

## Architecture / Approach

Use the existing token pipeline: define the tuned palette centrally in `styles.scss`, keep view styles consuming semantic roles, and verify rendered contrast in Playwright. Expand the existing visual E2E suite and snapshots rather than creating a parallel screenshot system.

## Phases at a Glance

| Phase | What it delivers | Key risk |
| --- | --- | --- |
| 1. Define the Accessible Palette | New semantic values, accurate token docs, and computed contrast checks. | Color combinations must pass contrast without losing the requested green-led identity. |
| 2. Verify the Whole Portal | Desktop/mobile visual coverage for all routes and representative states. | Uncovered admin/demo or feedback states may expose inconsistent styling. |

**Prerequisites:** Existing Angular token system and Playwright/AppHost setup are available; no backend or data work is required.  
**Estimated effort:** Two focused frontend phases; medium complexity.

## Open Risks & Assumptions

- BP-like colors are a visual inspiration, not a formal client brand specification; final values may be tuned to meet contrast thresholds.
- Red remains the only deliberate non-brand color family for destructive/error feedback.
- Existing visual snapshots will need review and regeneration after palette changes.

## Success Criteria (Summary)

- All portal routes use the approved semantic palette without changing layout or workflow.
- Automated checks enforce text and control/focus contrast on the rendered token pairs.
- Desktop/mobile visual baselines and representative status/error states are reviewed and pass.
