# Review Generated Card Spending — Plan Brief

> Full plan: `context/changes/review-generated-card-spending/plan.md`

## What & Why

This change delivers the reporting slice that completes the first operational card journey. It makes generated transactions inspectable for a selected calendar month and ensures the overview spending total is calculated from the same effective records as the transaction list.

## Starting Point

S-03 already persists deterministic Completed transactions with vehicle snapshots, VAT, discounts, net totals, and gross totals. The API has basic period filtering, but no aggregate contract; the Angular transactions screen and overview metrics still use placeholders.

## Desired End State

Users choose a month and timezone, defaulting to CET, then see full transaction rows with user-local and POS-local timestamps plus net, VAT, and gross values. The overview shows live vehicle/card counts and gross paid-after-discount, with totals that reconcile exactly to the visible effective rows.

## Key Decisions Made

| Decision | Choice | Why | Source |
| --- | --- | --- | --- |
| Headline amount | Gross paid after discount | It represents actual spending and matches the existing `GrossTotal` and seeded 326.60 PLN value. | Plan |
| Generated demo data | Completed records only | Preserves the deterministic S-03 issue flow while reporting supports future correction statuses. | Plan / Research |
| Correction selection | Explicit lineage with effective latest version | Prevents double-counting while keeping immutable facts and auditable corrections. | Plan |
| Correction economics | Proposed: Completed/Recalculated positive, Refunded explicit negative, Cancelled zero | Provisional rules only; S-04 still requires stakeholder confirmation. | Plan proposal; pending |
| Month boundaries | User-local month converted to a UTC half-open interval | Avoids end-of-day precision bugs and supports configurable user timezones. | Plan |
| User timezone | Configurable local preference, CET-compatible default | Lets month selection reflect the user's calendar while keeping storage in UTC. | Plan |
| POS timestamp | Persist UTC plus POS-local time and timezone information | POS time is a fact and must not change when a user changes their display timezone. | Plan |
| Overview data | Dedicated authenticated summary endpoint | Keeps counts and financial totals authoritative and aligned with report calculations. | Plan |
| Report UI | Month picker with full transaction rows | Makes the selected period and reconciliation inspectable in the MVP. | Plan |
| Invalid input | Structured `400` response | Prevents silent timezone and period interpretation errors. | Plan |
| Verification boundary | API, Angular, and Aspire E2E | Proves both isolated rules and the SQL-backed user journey. | Plan |

## Scope

**In scope:** transaction lineage/version fields, UTC/POS-local timestamps, timezone-aware month reports, status-aware totals, summary endpoint, live overview metrics, Angular transaction UI, bilingual resources, migrations, and API/component/Aspire verification.

**Out of scope:** generating correction scenarios during issuance, program comparison, exports, invoices, settlement, real identity, multi-client settings, pagination, caching, and advanced reporting.

## Architecture / Approach

The application layer owns effective-row selection and decimal aggregation. EF and seeded services implement the same reporting contract, while Minimal API endpoints expose a transaction report and a dedicated dashboard summary. Angular stores the user's timezone preference locally, renders server-calculated totals, and uses the existing standalone-component, localization, and Playwright patterns.

## Phases at a Glance

| Phase | What it delivers | Key risk |
| --- | --- | --- |
| 1. Transaction facts and reporting contracts | Immutable lineage, timestamps, status rules, and shared calculations | Getting correction semantics and timezone boundaries deterministic |
| 2. Persisted reporting API | Authenticated rows, totals, and overview summary for EF and seeded modes | Keeping summary and row totals identical across persistence modes |
| 3. Angular reporting experience | Month/timezone controls, detailed bilingual report, live overview | Avoiding stale placeholders and client-side calculation drift |
| 4. Reconciliation and Aspire verification | SQL-backed browser proof and regression protection | Catching migration or composed-runtime differences |

**Prerequisites:** S-03 card issue/assignment behavior is available; .NET/Node dependencies and Aspire SQL composition remain usable.
**Estimated effort:** Approximately 4 implementation sessions across four phases, with the main uncertainty in migration and timezone edge-case coverage.

## Open Risks & Assumptions

- The local timezone preference is stored in the browser for the MVP; a server-backed user settings model is deferred.
- The correction economics in the plan are proposals; confirm and record the stakeholder decision before implementing status aggregation.
- Cross-month correction reporting is unresolved; decide filter/reduction order and version tie-breaking before implementing report queries.
- Existing seeded transactions receive deterministic POS timezone metadata during migration/backfill.
- Correction records are supported by the data/reporting contract and tests but are not generated by the issue-time demo flow.
- CET-compatible default behavior should use an IANA timezone identifier so daylight-saving transitions remain testable.

## Success Criteria (Summary)

- Users can inspect a selected month with both user-local and POS-local timestamps and explicit net, VAT, and gross values.
- The overview's gross spending total equals the sum of effective report rows for the same period.
- API, Angular, and Aspire verification pass without weakening authentication, immutable history, or sensitive card-data boundaries.