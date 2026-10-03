---
date: 2026-10-03T17:25:40+02:00
researcher: Copilot
git_commit: f6787d50ae30369957f403e6c6bc2b77c45d345e
branch: feature/improve-ui
repository: PavelAlekseyuk/10x-devs-fleet-cards
topic: "Concentrate on cars and align the UI across views"
tags: [research, ui, card-management, fleet-vehicles]
status: complete
last_updated: 2026-10-03
last_updated_by: Copilot
---

# Research: Align card management around vehicle context

**Date**: 2026-10-03T17:25:40+02:00  
**Researcher**: Copilot  
**Git Commit**: f6787d50ae30369957f403e6c6bc2b77c45d345e  
**Branch**: feature/improve-ui  
**Repository**: PavelAlekseyuk/10x-devs-fleet-cards

## Research Question

How can the card-management view emphasize its vehicle context and align with the other portal views without changing the agreed card workflow?

## Summary

The current card list presents the masked PAN before the assigned vehicle plate, and the card view's styles use hardcoded values rather than semantic tokens. In the inspected Angular styles, the root stylesheet has no semantic token block and button, badge, form-control, and color rules are repeated in per-view stylesheets. This leaves no shared UI component directory to import into the selected view.

The vehicle context can be emphasized using the existing assigned-plate field. The current contract does not provide make, model, or fuel type on a card, and the active-vehicle lookup used for issuing excludes archived vehicles. Keep the visual change focused on the plate unless the data contract and archived-vehicle behavior are deliberately expanded.

Treat assignment as an issue-time fact, not an editable control: the later lifecycle-corrections plan makes assignments immutable, and the current card-component test verifies that no reassignment control or request is exposed. This supersedes the earlier virtual-card plan's reassignment workflow.

## Detailed Findings

### Vehicle context and card data

- In the card summary, the masked PAN is the first value, the assigned vehicle plate is second, and state is third ([card-management.component.html](../../../web/src/app/card-management.component.html#L56)). This hierarchy makes it harder to scan the list by vehicle.
- `FleetCard` supplies `assignedCarId` and `assignedVehicleRegistrationPlate`, while `FleetVehicle` additionally supplies make, model, and fuel type ([card-management.service.ts](../../../web/src/app/card-management.service.ts#L7), [fleet-vehicles.service.ts](../../../web/src/app/fleet-vehicles.service.ts#L4)).
- The card page's vehicle lookup requests active vehicles and filters archived records ([card-management.component.ts](../../../web/src/app/card-management.component.ts#L163)). Therefore, use the card's assigned plate for its summary; enriching it by joining only against the issue selector's vehicle list would not cover an archived assigned vehicle.
- Issuance is already vehicle-centered: the form selects a vehicle and sends its ID to the card service ([card-management.component.html](../../../web/src/app/card-management.component.html#L14), [card-management.service.ts](../../../web/src/app/card-management.service.ts#L34)).

### Styling contract across views

- The skill's candidate hardcoded-value scan over `card-management.component.html`, `card-management.component.scss`, and `card-management.component.ts` returned 33 matching lines; all 33 were in the component SCSS. This is a candidate-line count, not a count of unique values ([card-management.component.scss](../../../web/src/app/card-management.component.scss#L1)).
- The global stylesheet's root rule contains literal foreground and background colors, with no semantic token names ([styles.scss](../../../web/src/styles.scss#L2)). The `web/src` search for CSS custom-property references, `.dark`, and `prefers-color-scheme` returned no matches; no dark-mode token layer was found in that inspected scope.
- The shell and card styles each declare `.primary-button` with different padding values ([app.scss](../../../web/src/app/app.scss#L35), [card-management.component.scss](../../../web/src/app/card-management.component.scss#L79)). `quiet-button` and `badge` styles also appear in the card, vehicle, and transaction view stylesheets, while a search for `web/src/app/**/components/**` returned no shared components. The Angular instruction keeps styles per-component, so global semantic tokens plus locally scoped styles fit the existing convention better than copying another stylesheet into the card view ([angular-frontend.instructions.md](../../../.github/instructions/angular-frontend.instructions.md#L11)).

### Interaction and content states

- The card view renders loading, empty, and error messages and disables the issue action while saving, loading vehicles, or when there are no vehicles ([card-management.component.html](../../../web/src/app/card-management.component.html#L11)). These existing states should be preserved when changing layout.
- The card stylesheet provides disabled-control styling, but the inspected file has no explicit hover or `:focus-visible` treatment ([card-management.component.scss](../../../web/src/app/card-management.component.scss#L66)). A keyboard user may not get the same clear focus cue intended for the other controls.
- The checked-in Playwright dependency and E2E configuration provide an existing browser-level path for a visual check ([package.json](../../../web/package.json#L10), [playwright.config.ts](../../../web/playwright.config.ts#L1)). No screenshot assertion was found in the searched `web/e2e` path.

## Charges

1. **Missing tokens** — [card-management.component.scss](../../../web/src/app/card-management.component.scss#L6) uses hardcoded colors throughout, and the global source has only raw root colors ([styles.scss](../../../web/src/styles.scss#L2)). Users see the card page drift from other views as palette decisions are repeated locally.
2. **Missing shared component** — the shell and card view define `.primary-button` separately with different padding ([app.scss](../../../web/src/app/app.scss#L35), [card-management.component.scss](../../../web/src/app/card-management.component.scss#L79)); no shared component was found under `web/src/app/**/components/**`. Users can encounter the same action with different proportions and button treatment between views.
3. **Vehicle hierarchy** — [card-management.component.html](../../../web/src/app/card-management.component.html#L56) places the masked PAN before the assigned vehicle plate. Users scanning cards by car must look past the card number to identify the vehicle.
4. **Focus and hover states** — [card-management.component.scss](../../../web/src/app/card-management.component.scss#L66) styles controls and disabled state but has no explicit hover or focus-visible rules. Users navigating by keyboard may have no view-specific focus cue, and pointer actions have no view-specific hover feedback.

## Architecture Insights

- The Angular app uses standalone components and per-component SCSS; the instruction file names no shared UI library. No semantic-token or shared-component system was found in the inspected `web/src` styles and component tree.
- `web/src/styles.scss` is the candidate home for global semantic variables; component-level SCSS can consume them while retaining per-view layout. Avoid a new UI-library dependency for this single-view change.
- Keep scope to the card-management view plus shared token definitions and the small primitives it actually needs. Broader replacement of literals across the vehicle and transaction views is a separate scope decision, even though their styles show the same token drift.

## Historical Context (from prior changes)

- The original virtual-card plan included card reassignment and an assignment timeline ([issue-and-assign-virtual-cards/plan.md](../issue-and-assign-virtual-cards/plan.md#L9)).
- The later lifecycle-corrections plan explicitly makes issue-time vehicle assignment immutable and replaces assignment history with card-state history ([bug-fixes-after-s04-20260927/plan.md](../bug-fixes-after-s04-20260927/plan.md#L25)). Its completed Angular test checklist includes removal of reassignment controls ([bug-fixes-after-s04-20260927/plan.md](../bug-fixes-after-s04-20260927/plan.md#L239)); the current component spec also asserts that no reassignment control or request is present ([card-management.component.spec.ts](../../../web/src/app/card-management.component.spec.ts#L166)).
- The earlier reassignment plan is therefore superseded on that behavior; preserve issue-time assignment and state history rather than restoring reassignment as part of a visual pass.

## Related Research

- [bug-fixes-after-s04-20260927/research.md](../bug-fixes-after-s04-20260927/research.md) — prior lifecycle and reporting research; its reassignment findings describe the pre-correction state and are superseded by the later plan and current tests.

## Open Questions

- Is the assigned registration plate sufficient as the prominent vehicle identity, or should the view also show make/model/fuel? The current card response contains only the assigned ID and plate; supporting more fields safely for archived assignments would require a data-source/contract decision.
- The current scope selected card management plus shared styling tokens. If “align across all views” means migrating every vehicle and transaction style to the new token source in this change, expand the view scope in planning before implementation.

## Code References

- [card-management.component.html](../../../web/src/app/card-management.component.html#L56) — card and vehicle summary order, issue form, and loading/empty/error UI.
- [card-management.component.scss](../../../web/src/app/card-management.component.scss#L66) — control, disabled, and card-summary styles.
- [styles.scss](../../../web/src/styles.scss#L2) — global root foreground/background values.
- [app.scss](../../../web/src/app/app.scss#L35) — shell button declarations.
- [card-management.service.ts](../../../web/src/app/card-management.service.ts#L7) — card response fields.
- [fleet-vehicles.service.ts](../../../web/src/app/fleet-vehicles.service.ts#L4) — available vehicle fields.
- [card-management.component.spec.ts](../../../web/src/app/card-management.component.spec.ts#L166) — regression check for immutable card assignment in the UI.
