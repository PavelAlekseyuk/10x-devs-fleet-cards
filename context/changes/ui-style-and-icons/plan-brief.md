# UI Style and Icons — Plan Brief

> Full plan: `context/changes/ui-style-and-icons/plan.md`
> Frame brief: `context/changes/ui-style-and-icons/frame.md`

## What & Why

The user needs a clear way to describe an overall brand impression so it can be related to the portal's existing semantic UI-token vocabulary; the favicon and title are related identity requests, not evidence of a missing style system. This plan changes the browser title, replaces the favicon with original fuel-pump-and-canopy artwork in a BP-inspired palette, and adds reusable guidance beside the existing token catalogue.

## Starting Point

The browser title is `Web`, and `web/src/index.html` already points to `/favicon.ico`. Angular copies `web/public` into the build; the existing ICO has 16x16, 32x32, and 48x48 variants. The semantic UI tokens are defined in `web/src/styles.scss` and listed in `context/changes/improve-ui/ui-tokens.md`, but there is no established style-reference request workflow.

## Desired End State

The browser tab reads “Beznynowicz” and uses a recognizable, original pump-and-canopy favicon. Its selected BP-inspired colors are based on current bp.com homepage CSS values: green `#007f00`, lime `#ccff00`, yellow-orange `#edac1a`, and white `#ffffff`; the portal's global UI tokens remain unchanged. The token catalogue includes a reusable explanation of where to find tokens and how to give a scoped style request with a visual reference.

## Key Decisions Made

| Decision | Choice | Why (1 sentence) | Source |
| --- | --- | --- | --- |
| Main plan focus | Translate a brand impression into guidance using the existing tokens; keep title and favicon as separate identity outcomes. | The project already has tokens and the user's main uncertainty was how to describe a visual reference. | Frame |
| Title scope | Change the browser document title to `Beznynowicz`. | The current `Web` title is in the document head. | Plan |
| Favicon motif | Original fuel-pump glyph with a small station-canopy cue; retain 16x16, 32x32, and 48x48 variants. | The user chose the canopy cue while preserving the existing ICO delivery contract. | Plan |
| Favicon palette | Match current bp.com homepage colors as closely as practical; do not copy its logo or wordmark. | This uses the user's selected green/lime/yellow/white reference while keeping the artwork original. | Plan |
| Site theme | Do not change existing global `--ui-*` token values. | The BP palette request was made specifically for the favicon. | Frame / Plan |
| Style guidance | Add a reusable request section to the existing token catalogue, including a prompt example. | The user chose persistent guidance beside the token names and values. | Plan |
| Browser regression check | Add a focused Playwright check for title and favicon response; inspect icon appearance manually. | Automated checks can verify metadata and delivery, but not whether the small mark is recognizable. | Plan |

## Scope

**In scope:**
- Change the browser title to `Beznynowicz`.
- Replace `web/public/favicon.ico` with original pump-and-canopy artwork in the user-selected BP-inspired colors.
- Keep the existing favicon path and multi-size ICO variants.
- Add a Playwright metadata test.
- Add reusable instructions and an example style request to the existing token catalogue.

**Out of scope:**
- A portal-wide visual rebrand or changes to existing `--ui-*` values.
- BP logos, wordmarks, or copied brand artwork.
- Changes to the visible application shell, routes, backend, or authentication.
- New image-processing or design-system dependencies.

## Architecture / Approach

Update the static title and public ICO without changing the Angular asset pipeline or favicon URL. Verify the title and served icon through the existing Playwright/AppHost setup, then manually inspect the mark at supported sizes. Extend the existing token catalogue with a short, reusable handoff for the user-to-agent style request; keep the favicon-specific BP colors distinct from the portal's semantic UI theme.

## Phases at a Glance

| Phase | What it delivers | Key risk |
| --- | --- | --- |
| 1. Update Browser Identity | New browser title, original multi-size favicon, and focused browser assertion. | The canopy cue or colors may lose clarity at 16x16. |
| 2. Document the Style and Token Handoff | Reusable token-selection guidance and example request beside the current catalogue. | Guidance could be mistaken as permission to change the entire site theme. |

**Prerequisites:** Existing Angular public-asset pipeline and Playwright/AppHost test setup; no new packages or data migration.  
**Estimated effort:** One focused implementation session, with a possible short iteration if the small-size icon needs adjustment.

## Open Risks & Assumptions

- The BP colors were read from the current public homepage CSS, not a separate formal brand specification; keep the artwork original and verify the chosen shades manually.
- Favicon caching may delay the visible update in an already-open browser; use a hard refresh for verification.

## Success Criteria (Summary)

- A browser route displays the exact title “Beznynowicz” and serves the new ICO successfully.
- The original pump-and-canopy mark is distinguishable at 16x16, 32x32, and 48x48.
- The token catalogue clearly shows where to select existing semantic tokens and how to provide a scoped visual-reference request without changing the global theme by implication.
