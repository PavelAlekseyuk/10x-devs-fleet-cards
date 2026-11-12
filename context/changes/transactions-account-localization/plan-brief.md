# Transaction Month Localization and Account Access — Plan Brief

> Full plan: [plan.md](./plan.md)

## What & Why

The transactions month picker should follow the portal's chosen language, and account controls should be easy to find even when a report is long. The change also makes account and language controls accessible on mobile.

## Starting Point

The month picker is browser-native even though report headings are app-localized. The shared sidebar pushes account controls to its bottom and hides them on mobile; translation resources already contain English `Log out` and Polish `Wyloguj`.

## Desired End State

English and Polish month selection follows the app setting independently of browser language, while preserving the shared reporting period. The user name appears near the brand with logout underneath, and mobile exposes the name, logout and language switch at the top. The controls are not pinned during scrolling.

## Key Decisions Made

| Decision | Choice | Why |
| --- | --- | --- |
| Picker language | Follow the app's EN/PL setting, regardless of browser language | Native picker language is browser-dependent. |
| Period selection | Accessible direct month/year selection plus previous/next navigation | Preserves selecting arbitrary reporting periods and existing navigation. |
| Account placement | Near the brand, logout below the name | Keeps account actions discoverable without scrolling through a long report. |
| Mobile | Show user name, logout and language switch at the top | Makes both account and language actions accessible on small screens. |
| Scrolling | Normal page-top placement, not sticky | Delivers the requested relocation without permanently consuming viewport space. |
| Logout wording | Preserve `Log out` and `Wyloguj` | Both translations already exist in the resource. |

## Scope

**In scope:**
- Localized, accessible transaction period selection and regression tests.
- Shared responsive account controls and language access.
- Browser verification of long reports, language switching and logout.

**Out of scope:**
- Sticky controls, backend or auth redesign, and a general-purpose date-picker library.
- Changes to the reporting API, time-zone behavior or selected-period persistence.

## Architecture / Approach

The transaction component renders localized month/year controls and commits valid `YYYY-MM` values through the existing shared report service. The shared shell continues to use the current language and authentication services while repositioning their controls responsively.

## Phases at a Glance

| Phase | What it delivers | Key risk |
| --- | --- | --- |
| 1. Localized period selection | EN/PL direct selection and existing month navigation | Incorrect year bounds or unwanted reloads. |
| 2. Responsive account controls | Identity, logout and language access at the top | Hidden or duplicate actions at responsive breakpoints. |
| 3. Regression verification | Unit, browser and visual coverage | Long-report and mobile behavior require real browser checks. |

**Prerequisites:** Existing Angular dependencies and the configured Aspire/Playwright environment for browser tests.
**Estimated effort:** Approximately 1–2 implementation sessions across three phases.

## Open Risks & Assumptions

- Browser-level validation requires the local Aspire and database prerequisites to be available.
- Existing visual snapshots may need deliberate review because the shared shell changes on multiple routes.

## Success Criteria (Summary)

- Switching the app language updates the month selector without changing its selected period, even when the browser language differs.
- Name and translated logout appear near the top at desktop and mobile sizes; mobile language switching works.
- Existing reporting, navigation and logout behavior remains covered by automated and manual checks.
