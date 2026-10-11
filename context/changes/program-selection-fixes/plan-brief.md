# Program Selection Fixes — Plan Brief

> Full plan: `context/changes/program-selection-fixes/plan.md`

## What & Why

Fix four defects on the Programs page and program selection dialog: swapped program names, the wrong default active program, inverted discount/surcharge signs, and an unsortable comparison table. Users currently see "Nebula Basic" as active while the numbers behave like a no-discount program, and "+9%" on a discount.

## Starting Point

Program `…555` is named "Nebula Basic" with Diesel +10% / petrol −2% rules; `…556` is "Orion Diesel" with 0% everywhere. The seeded assignment points at `…555`. Stored percent means discount, but the UI prints it unchanged. The comparison table is not sortable.

## Desired End State

"Nebula Basic" is the 0% program and the default active one; "Orion Diesel" carries the diesel discount rules. Discounts display negative (`-9%`), surcharges positive, zero as `0%`. The four cost/savings columns can be sorted. Existing databases are corrected by migration.

## Key Decisions Made

| Decision | Choice | Why (1 sentence) |
| --- | --- | --- |
| Name fix | Swap names on IDs `…555`/`…556`, re-point seeded assignment to `…556` | Matches the requested naming with minimal data change |
| Existing databases | New EF migration (names, assignment, delete transactions) | Fixes live data without editing history of migrations |
| Demo transactions | Migration deletes them; regenerate via existing demo action | SQL cannot use the C# generator; user accepted bypassing append-only for demo data |
| Sign display | Negate stored value in `formatPercent`; `-9%`, `+2%`, `0%` | Stored discount semantics stay untouched |
| Sorting | Header click cycles asc → desc → default, `aria-sort` | Accessible and reversible to the backend order |
| Baseline row | Pinned on top, only program rows sorted | Baseline has no savings values |

## Scope

**In scope:** name swap, default assignment, migration, sign formatting, sortable comparison columns, tests.

**Out of scope:** changing cost calculation or stored sign, regenerating transactions in SQL, sorting other tables, changing backend response order.

## Architecture / Approach

Backend: seed + in-memory source + one EF migration. Frontend: display-only `formatPercent` change and client-side sort state in `programs.component.ts` over the comparison rows.

## Phases at a Glance

| Phase | What it delivers | Key risk |
| --- | --- | --- |
| 1. Backend names/default | Swapped names, default 0% program, migration | Migration deletes transactions in any database it runs against |
| 2. Sign display | Discounts negative, surcharges positive | Missing a place that renders percent |
| 3. Sortable table | Four sortable columns, baseline pinned | Radio selection correctness after sorting |

**Prerequisites:** none.
**Estimated effort:** ~2 sessions across 3 phases.

## Open Risks & Assumptions

- Deleting transactions violates the append-only rule; acceptable only because this is demo data.
- Assumption: no non-demo database will run this migration.

## Success Criteria (Summary)

- UI active program and comparison numbers agree (Nebula Basic = baseline, 0% savings).
- Discounts and surcharges display with the intuitive sign.
- All four columns sort and the baseline stays on top.
