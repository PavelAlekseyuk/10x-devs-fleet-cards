# Transaction Month Localization and Account Access Implementation Plan

## Overview

Make the transactions month picker follow the application's English/Polish language setting and make account controls accessible near the top of the portal, including on mobile. Preserve the existing reporting-period and authentication behavior.

## Current State Analysis

- `web/src/app/transaction-report.component.html` uses a native `type="month"` input, whose visible language is browser-controlled; its report totals heading is separately formatted using the application language in `transaction-report.component.ts`.
- `web/src/app/transaction-report.service.ts` owns a shared `YYYY-MM` selection, validates years 0001 through 9998, and feeds the transaction report and overview requests.
- `web/src/app/shell.component.html` places the language switch, logout and user chip after navigation. In `web/src/app/app.scss`, the footer is pushed to the bottom of the content-height sidebar, account details are hidden on narrow desktop, and all footer controls are hidden on mobile.
- `web/src/app/language.service.ts` already provides `Log out` in English and `Wyloguj` in Polish. The goal is to preserve and verify these translations, not invent a new English label.
- Existing unit and browser tests include the native month input and must be adapted without losing reporting assertions. The graph's freshness metadata was changed for inspected files; findings above were verified against current source.

## Desired End State

The selected month and its available month names use the active app language even when the browser language differs. Users can select a month and year directly, navigate adjacent months, and see the same period on the overview. The user name is near the brand, logout is below it, and both are accessible at the top on desktop and mobile; mobile users can also switch languages. Scrolling may take the controls out of view.

### Key Discoveries:

- The shared period and year bounds are defined by `web/src/app/transaction-report.service.ts:65`.
- The existing locale-specific heading uses `Intl.DateTimeFormat` in `web/src/app/transaction-report.component.ts:87`.
- The shared shell owns logout and the current user in `web/src/app/shell.component.ts:104`.
- Responsive rules currently hide account controls in `web/src/app/app.scss:68`.

## What We're NOT Doing

- Pinning or making account controls sticky while the page scrolls.
- Changing backend endpoints, authorization, transaction data, reporting time zones, or persistence of the selected period.
- Replacing the complete navigation design or introducing a general-purpose date-picker library.
- Changing the existing English `Log out` and Polish `Wyloguj` wording unless a demonstrated defect requires it.

## Implementation Approach

Use application-controlled, accessible month and year controls rather than relying on native picker locale, with month names generated from the existing EN/PL locale and period updates routed through `TransactionReportService`. Reorganize the shared shell and responsive styles so the same account actions remain available at all widths, and retain the existing logout handler and language service. Update unit and browser coverage around the user-visible contracts.

## Critical Implementation Details

### State sequencing

The reporting service is the source of truth for both overview and transactions. Invalid or incomplete year input must not update that shared value or issue a report request; a valid selection must retain the existing year range and request semantics. A language switch must not change the selected period or trigger an unrelated report reload.

## Phase 1: Localized period selection

### Overview

Replace browser-dependent month presentation with app-localized direct selection while preserving existing period behavior.

### Changes Required:

#### 1. Transaction period controls

**Files**: `web/src/app/transaction-report.component.ts`, `web/src/app/transaction-report.component.html`, `web/src/app/transaction-report.component.scss`

**Intent**: Provide accessible month and year selection with month names in the active app language, independent of the browser locale. Preserve previous/next buttons, visible validation, responsive layout and keyboard operation.

**Contract**: Month selection supports all twelve months and direct year entry for 0001–9998; its committed value remains a zero-padded `YYYY-MM` passed through the existing `setSelectedMonth`/report-loading path. Preserve the current period when language changes and keep invalid or incomplete years from committing or requesting data.

#### 2. Localized labels and focused tests

**Files**: `web/src/app/language.service.ts`, `web/src/app/transaction-report.component.spec.ts`, `web/src/app/transaction-report.service.spec.ts`

**Intent**: Supply any needed accessible English and Polish labels through the existing translation resources and cover localized selection and regression cases.

**Contract**: Verify English and Polish month names on language switches, direct selection and request parameters, year transitions, year bounds, invalid entry, previous/next behavior, and preservation of the shared selection.

### Success Criteria:

#### Automated Verification:

- Focused transaction report and service unit tests pass: `cd web; npm test -- --include=src/app/transaction-report.component.spec.ts --include=src/app/transaction-report.service.spec.ts`.
- A language switch updates month labels without changing `YYYY-MM` or requesting a new report; valid and invalid selections and year boundaries are covered by unit tests.

#### Manual Verification:

- With the browser set to English, switch the app between English and Polish and verify the month controls, direct selection and previous/next navigation use the chosen language and period.

**Implementation Note**: After automated verification, pause for human confirmation of manual verification before proceeding.

---

## Phase 2: Responsive account controls

### Overview

Place the account identity and logout action at the top, and expose account and language controls across responsive layouts.

### Changes Required:

#### 1. Shared shell layout

**Files**: `web/src/app/shell.component.html`, `web/src/app/app.scss`, `web/src/app/shell.component.ts`

**Intent**: Place the account identity immediately after the brand block on larger screens, with logout below the user name; show account identity, logout and language switching at the top on mobile. Keep controls in normal document flow rather than pinned during scrolling.

**Contract**: Preserve the existing user identity, permission label, `logout()` behavior, navigation and language toggle; maintain readable, keyboard-accessible controls at desktop, 641–900px and 640px-or-less widths, without hidden or duplicate focusable actions.

#### 2. Shell behavior tests

**Files**: `web/src/app/shell.component.spec.ts`, `web/src/app/language.service.spec.ts`

**Intent**: Cover the reordered account controls, English and Polish logout wording and the existing authentication interaction.

**Contract**: Assert identity before logout in the account area, correct `Log out`/`Wyloguj` labels after switching languages, preserved permission display and logout request/navigation.

### Success Criteria:

#### Automated Verification:

- Focused shell and language unit tests pass: `cd web; npm test -- --include=src/app/shell.component.spec.ts --include=src/app/language.service.spec.ts`.
- Shell tests cover identity-before-logout ordering, both logout labels and the existing logout action.

#### Manual Verification:

- At desktop, 641–900px and mobile widths, verify that the user name and logout appear at the top, logout is below the name, language switching works, and keyboard focus reaches each visible control.

**Implementation Note**: After automated verification, pause for human confirmation of manual verification before proceeding.

---

## Phase 3: Regression verification

### Overview

Verify the two changes together against realistic long-report and responsive browser scenarios.

### Changes Required:

#### 1. Browser-level regression coverage

**Files**: `web/e2e/transaction-report.spec.ts`, `web/e2e/portal-visual.spec.ts`

**Intent**: Adapt existing period assertions to the new accessible control and cover long reports, mobile account access, language switching and logout. Review affected visual snapshots and update only intentional changes.

**Contract**: Use the `/10x-e2e` per-risk workflow and existing Playwright setup, accessible role/label/text locators, state-based waits, independent setup and cleanup. Exercise app language opposite to browser language, a report long enough to extend beyond the viewport, desktop and mobile account visibility at the page top, and successful logout.

#### 2. Final integration checks

**Files**: `web/src/app/transaction-report.component.spec.ts`, `web/src/app/shell.component.spec.ts`

**Intent**: Preserve existing report sharing, translation, navigation and auth behavior after integration.

**Contract**: The selected month stays consistent between overview and report; toggling language does not alter it; responsive layout does not hide required account controls.

### Success Criteria:

#### Automated Verification:

- Angular production build passes: `cd web; npm run build`.
- Targeted transaction-report and responsive account browser tests pass using the existing Aspire-backed Playwright configuration: `cd web; npm run test:e2e -- e2e/transaction-report.spec.ts` plus the focused account regression test.
- Existing visual coverage is reviewed and passes for affected desktop and mobile routes, with only intentional snapshot changes.

#### Manual Verification:

- With a long transactions report, return to the page top and confirm identity, translated logout and language switching are available on desktop and mobile; confirm controls need not remain visible when scrolled down.
- Verify keyboard selection and logout, switching EN/PL with a mismatched browser locale, and overview/report period consistency.

**Implementation Note**: After automated verification, pause for human confirmation of manual verification.

## Testing Strategy

### Unit Tests:

- Cover localized month options, selection, bounds, invalid values, no extra reload on language switch, account ordering, translated labels and logout.

### Integration Tests:

- Use the existing isolated Aspire/Playwright setup; follow `/10x-e2e` for new browser-risk tests. Keep each test independent and use accessible locators and state-based waits.
- Retain the existing report reconciliation and timezone assertions while replacing obsolete native-input expectations.

### Manual Testing Steps:

1. Use an English browser, switch the application to Polish, choose a month and year, and confirm the same selected period on the overview.
2. With a long report, inspect account controls at the top on desktop, intermediate width and mobile; switch languages and verify logout wording.
3. Use keyboard navigation to change the period and activate logout; confirm the sign-in page is reached.

## Performance Considerations

Do not generate thousands of year options for the full valid range. A language toggle should re-render labels without refetching report data.

## Migration Notes

No database, API or stored-settings migration is expected. Existing saved language preferences and the in-memory shared selected period remain valid.

## References

- `web/src/app/transaction-report.component.html`
- `web/src/app/transaction-report.service.ts`
- `web/src/app/shell.component.html`
- `web/src/app/app.scss`
- `web/src/app/language.service.ts`
- `web/e2e/transaction-report.spec.ts`

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append ` — <commit sha>` when a step lands. Do not rename step titles.

### Phase 1: Localized period selection

#### Automated

- [x] 1.1 Focused transaction report and service unit tests pass: `cd web; npm test -- --include=src/app/transaction-report.component.spec.ts --include=src/app/transaction-report.service.spec.ts`. — 32cb195
- [x] 1.2 A language switch updates month labels without changing `YYYY-MM` or requesting a new report; valid and invalid selections and year boundaries are covered by unit tests. — 32cb195

#### Manual

- [x] 1.3 With the browser set to English, switch the app between English and Polish and verify the month controls, direct selection and previous/next navigation use the chosen language and period. — 32cb195

### Phase 2: Responsive account controls

#### Automated

- [x] 2.1 Focused shell and language unit tests pass: `cd web; npm test -- --include=src/app/shell.component.spec.ts --include=src/app/language.service.spec.ts`. — f0c421c
- [x] 2.2 Shell tests cover identity-before-logout ordering, both logout labels and the existing logout action. — f0c421c

#### Manual

- [x] 2.3 At desktop, 641–900px and mobile widths, verify that the user name and logout appear at the top, logout is below the name, language switching works, and keyboard focus reaches each visible control. — f0c421c

### Phase 3: Regression verification

#### Automated

- [x] 3.1 Angular production build passes: `cd web; npm run build`.
- [x] 3.2 Targeted transaction-report and responsive account browser tests pass using the existing Aspire-backed Playwright configuration: `cd web; npm run test:e2e -- e2e/transaction-report.spec.ts` plus the focused account regression test.
- [x] 3.3 Existing visual coverage is reviewed and passes for affected desktop and mobile routes, with only intentional snapshot changes.

#### Manual

- [x] 3.4 With a long transactions report, return to the page top and confirm identity, translated logout and language switching are available on desktop and mobile; confirm controls need not remain visible when scrolled down.
- [x] 3.5 Verify keyboard selection and logout, switching EN/PL with a mismatched browser locale, and overview/report period consistency.
