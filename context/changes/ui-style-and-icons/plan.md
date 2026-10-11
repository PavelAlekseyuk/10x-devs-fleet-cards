# UI Style and Icons Implementation Plan

## Overview

Update the browser-tab title and favicon to reflect the Beznynowicz portal identity, then make the existing style tokens easier to use by documenting a reusable style-request handoff. The favicon will use original fuel-pump-and-canopy artwork and the selected BP-inspired colors; the site's current visual theme is not changing.

## Current State Analysis

The Angular document title is `Web` and its icon link points to `favicon.ico` in the public asset directory. Angular copies the contents of `web/public` into the build output, and the current ICO includes 16x16, 32x32, and 48x48 variants. Playwright is already configured for browser tests, but no title or favicon assertion was found.

The portal's semantic tokens are defined in `web/src/styles.scss` and catalogued in `context/changes/improve-ui/ui-tokens.md`. The catalogue explains existing values but does not tell a user how to describe a visual reference and request token changes. The user chose to add that reusable guidance to the existing catalogue.

The user selected a fuel pump with a small station-canopy cue and BP-inspired green, lime, yellow, and white. The current [bp.com](https://www.bp.com/) homepage exposes these CSS values: `--bp-green-rest: #007f00`, `--bp-bright-green-rest: #ccff00`, `--bp-yellow-orange-600: #edac1a`, and white `#ffffff`. These are current public-site values, not a separate formal brand specification. Use original artwork, not BP's logo or wordmark.

### Key Discoveries:

- The document title and favicon link are at [`web/src/index.html`](../../../web/src/index.html#L5) and [`web/src/index.html`](../../../web/src/index.html#L8), respectively.
- Angular copies all files from `web/public` as static assets ([`web/angular.json`](../../../web/angular.json#L26)).
- Playwright uses the existing E2E configuration and is run with `npm run test:e2e` ([`web/package.json`](../../../web/package.json#L10), [`web/playwright.config.ts`](../../../web/playwright.config.ts#L13)).
- The token catalogue has color, spacing/type, and primitive-alias sections ([`ui-tokens.md`](../improve-ui/ui-tokens.md#L5)).
- The frame identified style-reference translation—not a missing token system—as the lead need ([`frame.md`](./frame.md#L42)).

## Desired End State

The browser tab says “Beznynowicz” and displays a clear, original fuel-pump-and-canopy icon in the selected BP-inspired palette at favicon sizes. A reusable section beside the existing token catalogue explains where tokens live, which details to provide when asking for a style change, and gives a copyable example request. Existing site-wide UI token values and view styling remain unchanged.

## What We're NOT Doing

- Rebranding the whole portal or changing the existing `--ui-*` token values.
- Applying BP colors to application views; the BP-inspired palette is for the favicon only.
- Copying BP's logo, wordmark, or other distinctive brand artwork.
- Changing visible shell branding, authentication, backend behavior, or application routes.
- Adding an image-processing dependency or introducing a new design-system package.

## Implementation Approach

Keep the existing public favicon URL and multi-size ICO contract so the Angular asset pipeline and browser link remain unchanged. Change the static document title, replace the ICO with original artwork, and add a focused Playwright test for the title and served favicon. Extend the current token catalogue with a short, reusable user-to-agent style-request workflow without changing its token values.

## Phase 1: Update Browser Identity

### Overview

Set the browser title to the client name and replace the current favicon with the user-approved fuel-pump-and-canopy mark, using the selected colors while retaining the existing public asset path and sizes.

### Changes Required:

#### 1. Browser document metadata

**File**: `web/src/index.html`

**Intent**: Replace the generic browser title with “Beznynowicz” so the tab identifies the client portal.

**Contract**: The document retains one `<title>` with the exact text `Beznynowicz`; keep the existing favicon link to `favicon.ico`.

#### 2. Favicon asset

**File**: `web/public/favicon.ico`

**Intent**: Replace the generic icon with original, compact fuel-pump artwork that includes a small station-canopy cue. Use BP-inspired green (`#007f00`), lime (`#ccff00`), yellow-orange (`#edac1a`), and white (`#ffffff`) based on current public-site CSS values.

**Contract**: Keep the existing filename, public URL, and 16x16, 32x32, and 48x48 ICO variants. The artwork must not reproduce the BP logo or wordmark. Do not change the portal's global UI tokens.

#### 3. Browser metadata test

**File**: `web/e2e/site-metadata.spec.ts`

**Intent**: Add a focused Playwright check for the browser title and static favicon response so a future generic title or missing asset is caught.

**Contract**: The browser test loads an application route, asserts the exact page title, and verifies that `/favicon.ico` returns a successful, non-empty response. It does not attempt to assert the icon's visual shape; that remains a manual check.

### Success Criteria:

#### Automated Verification:

- `cd web; npm run build` succeeds and includes the updated index and public favicon in the production output.
- `cd web; npm run test:e2e -- e2e/site-metadata.spec.ts` passes the title and favicon-response assertions.

#### Manual Verification:

- Inspect the icon at 16x16, 32x32, and 48x48: the pump and canopy remain recognizable, colors are distinguishable, and the mark is original rather than a BP logo.
- Open the app in a browser and confirm the tab shows “Beznynowicz” and the new icon; hard-refresh if the browser displays a cached favicon.

**Implementation Note**: Pause for human confirmation of the icon at small sizes before proceeding to the documentation phase.

## Phase 2: Document the Style and Token Handoff

### Overview

Extend the existing token catalogue with reusable instructions for turning a brand impression or visual reference into a scoped request to update the portal's existing semantic tokens.

### Changes Required:

#### 1. User-facing style-request guidance

**File**: `context/changes/improve-ui/ui-tokens.md`

**Intent**: Add a concise section that helps the user find available token names and give enough context for a future style change without asking for implementation details.

**Contract**: The section points to `web/src/styles.scss` as the token source and the catalogue as the readable index; explains how to identify semantic token roles; and gives a reusable request example covering target screen/component, overall impression, optional screenshot or URL, desired token direction, what must stay unchanged, and accessibility/contrast expectations. It distinguishes the favicon-only BP palette from the portal's existing `--ui-*` tokens. Do not change the existing token values.

### Success Criteria:

#### Automated Verification:

- `git diff --check` reports no whitespace errors in the planned changes.

#### Manual Verification:

- Review the new section to confirm it tells the user where to find token names/values and how to supply a scoped visual-reference request, including what to preserve.
- Confirm the example does not imply that a favicon palette request changes the site's global theme.

**Implementation Note**: Pause for human confirmation that the guide answers the token-selection and style-request questions.

## Testing Strategy

### Unit Tests:

- No unit test is needed for the static document title or documentation-only guidance.

### Integration Tests:

- Use the existing Playwright/AppHost setup for a focused browser test that checks the title and that the public favicon is served.

### Manual Testing Steps:

1. Open an app route and confirm the browser tab title is exactly “Beznynowicz”.
2. Inspect the favicon at 16x16, 32x32, and 48x48, including pump/canopy legibility and contrast among green, lime, yellow-orange, and white.
3. Review the new token guidance and use its example to identify the target surface, desired visual direction, and unchanged parts.

## Performance Considerations

No runtime or performance impact is expected; the change replaces a small static asset and edits documentation.

## Migration Notes

No data or runtime migration is required. Reusing the existing favicon path may leave a previously cached icon visible until the browser refreshes its favicon cache.

## References

- Frame brief: [`frame.md`](./frame.md)
- Current token catalogue: [`ui-tokens.md`](../improve-ui/ui-tokens.md)
- Angular document and public asset configuration: [`web/src/index.html`](../../../web/src/index.html), [`web/angular.json`](../../../web/angular.json)
- Existing Playwright patterns: [`web/e2e/portal-visual.spec.ts`](../../../web/e2e/portal-visual.spec.ts), [`web/playwright.config.ts`](../../../web/playwright.config.ts)
- BP-inspired color reference: [bp.com](https://www.bp.com/) (current homepage CSS values; not a formal standalone brand specification)

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append ` — <commit sha>` when a step lands. Do not rename step titles. See `references/progress-format.md`.

### Phase 1: Update Browser Identity

#### Automated

- [ ] 1.1 `cd web; npm run build` succeeds and includes the updated index and public favicon in the production output.
- [ ] 1.2 `cd web; npm run test:e2e -- e2e/site-metadata.spec.ts` passes the title and favicon-response assertions.

#### Manual

- [ ] 1.3 Inspect the icon at 16x16, 32x32, and 48x48: the pump and canopy remain recognizable, colors are distinguishable, and the mark is original rather than a BP logo.
- [ ] 1.4 Open the app in a browser and confirm the tab shows “Beznynowicz” and the new icon; hard-refresh if the browser displays a cached favicon.

### Phase 2: Document the Style and Token Handoff

#### Automated

- [ ] 2.1 `git diff --check` reports no whitespace errors in the planned changes.

#### Manual

- [ ] 2.2 Review the new section to confirm it tells the user where to find token names/values and how to supply a scoped visual-reference request, including what to preserve.
- [ ] 2.3 Confirm the example does not imply that a favicon palette request changes the site's global theme.
