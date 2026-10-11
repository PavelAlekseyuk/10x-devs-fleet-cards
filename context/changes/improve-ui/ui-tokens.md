# UI tokens

Semantic tokens defined in `web/src/styles.scss` (`:root`). The portal-wide palette is green-led, with white surfaces, lime and yellow status fills, and a reserved red destructive/error exception. Views migrate to these tokens in Phase 3.

## Colors

| Token | Value | Role |
| --- | --- | --- |
| `--ui-color-foreground` | `#173126` | Default text; also the global root foreground |
| `--ui-color-foreground-strong` | `#17432f` | High-emphasis headings and labels |
| `--ui-color-foreground-soft` | `#365748` | Secondary labels and supporting text |
| `--ui-color-muted` | `#53685d` | Secondary messages and metadata |
| `--ui-color-muted-subtle` | `var(--ui-color-muted)` | Eyebrows and record counts; aliases muted text to retain readable contrast |
| `--ui-color-primary` | `#007f00` | Green primary actions and navigation emphasis |
| `--ui-color-primary-hover` | `color-mix(in srgb, #007f00 88%, #173126)` | Darker green hover state derived from the primary and foreground |
| `--ui-color-primary-foreground` | `#ffffff` | Text on the green primary action |
| `--ui-color-accent` | `#006b3c` | Accessible green links, badges, and accents |
| `--ui-color-sidebar` | `#00512f` | Green shell and login background |
| `--ui-color-background` | `#ffffff` | Global page background and root background |
| `--ui-color-surface` | `#ffffff` | Panels and inputs |
| `--ui-color-surface-muted` | `#f1f6ed` | Pale green hover and muted fills |
| `--ui-color-border` | `#74877a` | Control boundaries with clear contrast against white |
| `--ui-color-border-subtle` | `#e2e9e2` | Low-emphasis row dividers |
| `--ui-color-destructive` | `#9b4238` | Red destructive and error text; red is intentionally reserved for these states |
| `--ui-color-destructive-background` | `#fbe5df` | Pale red destructive/error background |
| `--ui-color-status-active` | `#006b3c` | Green active badge text |
| `--ui-color-status-active-background` | `#e8f5bf` | Lime-tinted active badge background |
| `--ui-color-status-warning` | `#705000` | Dark yellow-brown warning text |
| `--ui-color-status-warning-background` | `#fff2b3` | Yellow warning badge background |
| `--ui-color-focus` | `#007f00` | High-contrast green keyboard focus outline |

## Spacing, radius, type

| Token | Value |
| --- | --- |
| `--ui-space-1` … `--ui-space-6` | `4px`, `8px`, `12px`, `16px`, `24px`, `32px` |
| `--ui-radius-none` / `-sm` / `-pill` | `0` / `2px` / `999px` |
| `--ui-font-body` / `--ui-font-display` | `'DM Sans', sans-serif` / `'Space Grotesk', sans-serif` (global font rules) |
| `--ui-font-size-xs` / `-sm` / `-md` / `-lg` | `11px`, `13px`, `14px`, `18px` (sizes used in `app.scss`/login) |

## Primitive aliases

Names referenced by `button.component.scss` and `status-badge.component.scss`:

| Alias | Resolves to |
| --- | --- |
| `--ui-button-primary-background` | `--ui-color-primary` |
| `--ui-button-primary-foreground` | `--ui-color-primary-foreground` |
| `--ui-button-primary-hover-background` | `--ui-color-primary-hover` |
| `--ui-button-quiet-background` | `--ui-color-surface` |
| `--ui-button-quiet-foreground` | `--ui-color-foreground-soft` |
| `--ui-button-quiet-border` | `--ui-color-border` |
| `--ui-button-quiet-hover-background` | `--ui-color-surface-muted` |
| `--ui-button-text-foreground` | `--ui-color-accent` |
| `--ui-button-text-hover-background` | `--ui-color-surface-muted` |
| `--ui-focus-ring` | `--ui-color-focus` |
| `--ui-status-active-foreground` / `-background` | `--ui-color-status-active` / `-background` |
| `--ui-status-warning-foreground` / `-background` | `--ui-color-status-warning` / `-background` |

## Asking for a style update

Use `web/src/styles.scss` as the source of truth for token names and current values; use this catalogue as the readable index of what those tokens are for. Find a semantic role that matches the part you want to change: for example, foreground and muted colors affect text emphasis, background and surface colors distinguish page and panel areas, status colors communicate state, and spacing, radius, and font tokens shape layout and typography. Check the token's source or alias here, then confirm its current definition in `styles.scss`.

You do not need to prescribe CSS or implementation details. Describe the screen or component, the overall impression you want, and which existing token roles should move in what direction. A screenshot or reference URL is optional; say what to take from it. State what must remain unchanged, and ask that text and controls keep accessible contrast and visible focus.

Example request:

> On the **[screen/component]**, aim for **[overall impression]**. Optionally use **[screenshot or URL]** as a visual reference, especially for **[what to take from it]**. Adjust the relevant existing `--ui-*` token roles toward **[desired direction, such as warmer surfaces or stronger text emphasis]**. Keep **[screens, components, or details]** unchanged, and preserve accessible text/control contrast and visible keyboard focus. Please propose the scoped token changes before applying them.

The green-led palette, with lime and yellow accents and red reserved for destructive/error feedback, applies portal-wide through the semantic `--ui-*` tokens. Keep style requests scoped to the intended screen or component and ask for accessible text/control contrast and visible focus; brighter accents should not be used for small text unless their pairing meets the contrast requirement.
