# Frame Brief: UI Style, Icon, and Brand Handoff

> Framing step before /10x-plan. This document captures what is *actually*
> at issue, separated from what was initially assumed.

## Reported Observation

“I want to
- Change the web site icon to resemple a fuel pump on a petrol station
- Rename the SAP title from "Web" to "Beznynowicz"
- Describe how to change the style - where to select tokens and how to provide them to you”

## Initial Framing (preserved)

- **User's stated cause or approach**: Treat the icon, title, and style-token guidance as related UI identity/style needs.
- **User's proposed direction**: Change the icon to a petrol-station fuel pump, rename the title to “Beznynowicz,” and explain where to select tokens and how to provide them.
- **Pre-dispatch narrowing**: The leading scope is a clear way to specify visual style. The user wants to describe a visual reference and is unsure what details to provide; the hardest part is communicating its overall impression or brand feel.

## Dimension Map

The observation could originate at any of these dimensions:

1. **Visual-reference translation** — an overall impression may be difficult to express as concrete style preferences. This is the user's narrowed focus.
2. **Token vocabulary and availability** — the request may assume tokens are missing or hard to find, when a usable token set may already exist.
3. **Browser identity metadata** — the displayed title and site icon are document-level identity details, separate from visual styling.
4. **Scope relationship** — the three requested outcomes may be one coordinated brand pass or separate pieces of work.

## Hypothesis Investigation

| Hypothesis | Evidence | Verdict |
| --- | --- | --- |
| The leading difficulty is translating a broad brand impression into usable style guidance. | The user selected “overall impression/brand feel” as the hardest part. The repository has a semantic-token reference, but the inspected token documentation catalogs values rather than recording a visual-reference handoff. | STRONG |
| The main blocker is that the project has no usable design tokens. | [`web/src/styles.scss`](../../../web/src/styles.scss#L7) defines semantic colors, spacing, radii, and typography. [`ui-tokens.md`](../improve-ui/ui-tokens.md#L1) records their meanings and provenance. | NONE |
| The “Web” title and favicon are the source of the style-guidance difficulty. | [`web/src/index.html`](../../../web/src/index.html#L5) contains the title and favicon link separately. The product name “Beznynowicz” is already present in [`prd.md`](../../foundation/prd.md#L2). | NONE |
| The three requests are necessarily one indivisible style change. | The source material places the title/icon in page identity metadata and the design vocabulary in global styles; no repository evidence makes them technically inseparable. | WEAK |

## Narrowing Signals

- The user picked the style-guidance need as the leading scope, rather than the icon or title.
- The unresolved detail is how to communicate a visual reference; specifically, overall impression or brand feel.

## Cross-System Convention

The existing UI convention is a semantic token source with a written token catalogue. That supports expressing style in terms of roles such as primary color, background, typography, and spacing. The reviewed project material does not establish a separate visual-reference intake convention, so the handoff gap is plausible; the icon and title remain separate identity details.

## Reframed (or Confirmed) Problem Statement

> **The actual problem to plan around is**: The user needs a clear way to describe a desired brand impression so it can be translated into the existing semantic UI-token vocabulary; the favicon and title are related identity requests, not evidence of a missing style system.

The repository already has global tokens and documentation for their current values, so creating a token system is not the core need. The user's answers identify an expression/translation gap, while the title and favicon can remain explicit, separately verifiable outcomes if they stay in scope.

## Confidence

- **MEDIUM** — repository evidence confirms an existing token vocabulary and separate metadata surfaces, and the user decisively narrowed the concern to brand impression. The exact visual reference and desired target surfaces remain unspecified.

## What Changes for /10x-plan

Plan around the style-reference handoff and how the desired impression relates to the current tokens, rather than assuming tokens need to be invented. Treat the site icon and browser title as separately scoped brand-identity outcomes.

## References

- Source files: [`web/src/styles.scss`](../../../web/src/styles.scss#L7), [`web/src/index.html`](../../../web/src/index.html#L5), [`prd.md`](../../foundation/prd.md#L2), [`ui-tokens.md`](../improve-ui/ui-tokens.md#L1)
- Related research: [`context/changes/improve-ui/research.md`](../improve-ui/research.md)
- Investigation tasks: None; this small surface was inspected directly.
