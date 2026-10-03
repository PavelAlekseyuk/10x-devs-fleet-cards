# UI tokens

Semantic tokens defined in `web/src/styles.scss` (`:root`). Raw values reuse the existing forest-green, warm-neutral and coral palette; no new palette is introduced. Views migrate to these tokens in Phase 3.

## Colors

| Token | Value | Source |
| --- | --- | --- |
| `--ui-color-foreground` | `#17231f` | Global `:root` color in `styles.scss` |
| `--ui-color-foreground-strong` | `#24372f` | Headings/sidebar hover in view and shell styles |
| `--ui-color-foreground-soft` | `#344b40` | Labels in card/report/vehicle styles |
| `--ui-color-muted` | `#64736a` | Messages/secondary text in `app.scss`, views |
| `--ui-color-muted-subtle` | `#75827a` | Eyebrow/record-count text |
| `--ui-color-primary` | `#e86c54` | Coral primary button, active nav border (`app.scss`, login) |
| `--ui-color-primary-hover` | `color-mix(in srgb, #e86c54 88%, #16241f)` | Derived: coral darkened with existing sidebar green |
| `--ui-color-primary-foreground` | `#fff` | Primary button text |
| `--ui-color-accent` | `#34755f` | Badge/link green in card management |
| `--ui-color-sidebar` | `#16241f` | Sidebar/login visual background |
| `--ui-color-background` | `#f6f3ed` | Global and shell background |
| `--ui-color-surface` | `#fff` | Panels, inputs |
| `--ui-color-surface-muted` | `#edf1ec` | Hover/muted fills in card management |
| `--ui-color-border` | `#cbd4cc` | Input borders (transaction report) |
| `--ui-color-border-subtle` | `#eef0eb` | Row dividers |
| `--ui-color-destructive` | `#9b4238` | Login error text |
| `--ui-color-destructive-background` | `#fbe5df` | Login error background |
| `--ui-color-status-active` | `#34755f` | Active badge text |
| `--ui-color-status-active-background` | `#e2f2e8` | Active badge background |
| `--ui-color-status-warning` | `#9b6632` | Warning badge text |
| `--ui-color-status-warning-background` | `#f8ead4` | Warning badge background |
| `--ui-color-focus` | `#5a9d89` | Login input focus outline |

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
