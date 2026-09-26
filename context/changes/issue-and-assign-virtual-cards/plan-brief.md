# Issue and Assign Virtual Cards — Plan Brief

> Full plan: `context/changes/issue-and-assign-virtual-cards/plan.md`

## What & Why

This change delivers the virtual-card slice as a working, secure flow: a read-write user issues a card for an active vehicle, completed mock transactions are generated with that vehicle's context, and users can inspect card state and assignment history. It builds on the required card/vehicle workflow while keeping full PAN, CVV, and PIN outside the application boundary.

## Starting Point

The backend has seeded and EF-backed card reads, a current card state/vehicle assignment, and transactions that already snapshot vehicle ID and plate. It has no card provider, mutation service, or assignment history, and the Angular cards route still displays generic placeholder content.

## Desired End State

Users can issue a card for an active vehicle, view masked card details, change it to any defined state, reassign it immediately, and inspect the effective assignment timeline. Each successful issue stores one card, assignment/audit history, and 1-5 deterministic completed transactions; ReadOnly users can view but not change card data.

## Key Decisions Made

| Decision | Choice | Why | Source |
| --- | --- | --- | --- |
| Issue and generated transactions | Issue requires an active vehicle; generate completed transactions during S-03 | Transactions need the assigned vehicle's allowed fuel type and immutable snapshot. | Plan |
| Transaction cases | Completed only; configured VAT uses the MVP 23% default and away-from-zero rounding | Refund, cancellation, and recalculation rules remain unresolved and are excluded. | Plan / Decisions |
| Provider boundary | Mock provider returns masked PAN, token, and safe metadata only | Full PAN/CVV/PIN must never enter the application. | Decisions / Security |
| Card state changes | Any defined state is selectable, including reactivation; validate enum and audit each change | Follows the explicit card-state decision; clarify the generic security guidance. | Decisions / Plan |
| Vehicle/card relationship | Each card has one current vehicle; multiple cards may share a vehicle | Matches current per-card relationship and supports multiple fuel networks. | Plan |
| Assignment lifecycle | Immediate issue/assignment/reassignment; no future dating, backdating, or unassignment | Keeps effective history simple and preserves the active assignment through state changes. | Plan |
| History UX | Persist append-only assignment events and show a timeline | Makes effective periods visible without rewriting prior transactions. | Plan |
| Access | Authenticated users may read cards/history; only ReadWrite may mutate with CSRF | Matches the seeded roles and existing API security pattern. | PRD / Security |

## Scope

**In scope:** card provider adapter, card issue/list/state/reassignment, assignment history timeline, deterministic completed transaction generation, audit/redaction, EF migration and seeded fallback, bilingual Angular feature, API and Aspire acceptance coverage.

**Out of scope:** real provider, card return, unassignment/scheduled assignment, transaction status exceptions, transaction browsing/totals, program comparison, production identity/deployment.

## Architecture / Approach

A card application service owns reads and lifecycle commands in both SQL and seeded modes. The mock provider returns only masked data and a token; successful issuance persists the card, assignment event, audit, and completed transactions transactionally. The protected Minimal API serves the list and history to both roles while restricting mutations to ReadWrite users. A standalone Angular card feature replaces the shell placeholder and reuses the fleet-vehicle service, permission, and localization patterns.

## Phases at a Glance

| Phase | What it delivers | Key risk |
| --- | --- | --- |
| 1. Card contracts, provider, and persistence | Safe issue path, assignment events, deterministic transactions, migration | Keeping all persisted issue effects atomic and seeded data consistent |
| 2. Protected card API | List, issue, state, reassignment, history, authorization/redaction tests | Permission or sensitive-data leaks at the HTTP boundary |
| 3. Angular card management | Bilingual issue/list/state/reassignment/history UI | Keeping UI controls aligned with backend rules |
| 4. Aspire acceptance and security alignment | SQL-backed E2E proof and explicit card-state security guidance | Migration/composition behavior differing from in-memory tests |

**Prerequisites:** Existing S-01/S-02 work remains available; local .NET 10, Node dependencies, and Aspire/SQL test prerequisites are installed.
**Estimated effort:** Approximately 4 implementation sessions across the four phases, subject to Aspire test setup.

## Open Risks & Assumptions

- Assignment migration backfill can establish only a known lower bound from existing transaction evidence, not the true historical assignment start.
- Transaction correctness for refunds, cancellations, and recalculations remains an S-04 blocker; this slice generates only completed records.
- The security guidance must be clarified so card state input validation/auditing is not mistaken for a transition matrix.

## Success Criteria (Summary)

- ReadWrite users can issue for an active vehicle, manage card state, reassign, and view an effective history timeline; ReadOnly users can view but cannot mutate.
- Completed generated transactions use the assigned vehicle's fuel type and snapshot, and reconcile with the configured VAT/rounding rules.
- Full PAN/CVV/PIN never appears in persisted application data, API output, or logs; the SQL-backed Aspire flow passes acceptance tests.
