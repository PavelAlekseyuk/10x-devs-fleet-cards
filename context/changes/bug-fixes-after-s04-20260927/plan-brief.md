# Card Lifecycle and Overview Corrections — Plan Brief

> Full plan: `context/changes/bug-fixes-after-s04-20260927/plan.md`
> Research: `context/changes/bug-fixes-after-s04-20260927/research.md`

## What & Why

This change locks each issued card to its issue-time vehicle, gives users a newest-first state timeline, and fixes the state editor showing Active while the card badge says Blocked after refresh. It also adds selected-period gross savings and makes overview month navigation update the same period used by transaction reporting.

## Starting Point

Both card service modes currently allow reassignment, and the UI shows vehicle assignment history rather than card state history. The reporting service already selects effective purchases and signed corrections, while the overview's selected month is shared with the transaction page but does not reload its summary when changed.

## Desired End State

Cards keep their original assigned vehicle and show issuance plus subsequent state changes, newest first. A refreshed state dropdown matches the state returned by the API. The overview shows explicit net/gross labels and selected-month gross savings calculated from POS prices, discounts, VAT, and signed correction data, with month controls that stay synchronized with the report.

## Key Decisions Made

| Decision | Choice | Why | Source |
| --- | --- | --- | --- |
| Assignment | Required and immutable after issue | The vehicle snapshot is an issue-time fact; reassignment changes what the card represents. | Plan (supersedes existing S-03 behavior) |
| State history | Include Active at issuance; newest first | A card's history should be complete from the moment it exists. | Plan |
| Refresh behavior | Select the persisted card state | The editor must agree with the read-only state badge and fresh API response. | Plan |
| Legacy null assignment | Delete card and all linked transactions, assignment events, and card audit events | User explicitly authorized a one-time purge, including referenced history. | Plan |
| Savings | Selected-period gross savings; apply signed corrections | This matches the selected-period gross-first reporting model and signed correction rows. | Plan / Research |
| POS basis | POS price is always present; savings uses POS price, quantity, VAT, and signed discounted gross | This permits direct savings calculation instead of estimating a correction's discount proportionally. | Plan |
| Month controls | Move shared month and reload summary | Keeps the overview and transaction report aligned. | Research |

## Scope

**In scope:**
- Enforce non-null immutable card assignment in EF, seeded services, API, and Angular.
- Replace assignment history with issuance-inclusive newest-first card state history.
- Purge legacy null-assignment cards and every linked transaction, assignment-history, and audit record before enforcing the required assignment column.
- Add selected-period gross savings from POS price and signed discounted transaction values.
- Correct EN/PL net and gross labels and add overview month navigation with summary reload.
- Verify with focused .NET/Angular tests and the Aspire composed flow.

**Out of scope:**
- Reassignment, unassignment, card return, lifetime savings, and a second savings metric.
- New user-facing refund/cancellation/recalculation workflows, real card processing, or changes to authentication.

## Architecture / Approach

The application uses the existing card audit stream for state history: CardIssued represents the initial Active state, and state-change actions provide later entries. A migration purges only cards whose issue-time vehicle is missing, then makes assignment required. The reporting service extends its existing selected-period calculation so the report and dashboard summary share effective-row selection and gross-savings logic; Angular consumes the returned value and the shared month signal.

## Phases at a Glance

| Phase | What it delivers | Key risk |
| --- | --- | --- |
| 1. Immutable Card Assignment and State-History API | Required issue-time assignment, targeted legacy cleanup, and issuance-inclusive state-history endpoint. | Migration cleanup deletes linked financial and audit facts by explicit user decision. |
| 2. Gross Savings and Angular Workflows | POS-based savings, corrected card controls/history, localized totals, and synchronized month navigation. | Signed correction rows must carry valid POS quantity/price basis for direct calculation. |
| 3. Composed Verification and Policy Alignment | SQL migration and Aspire flow verified, with requirements/decisions aligned. | Composed SQL/browser tests must prove no assigned-card history is removed. |

**Prerequisites:** S-04 reporting and the current SQL-backed Aspire test environment remain available; take the standard database backup before applying the destructive legacy cleanup migration.
**Estimated effort:** Approximately three implementation sessions across the three phases, subject to the SQL-backed migration test setup.

## Open Risks & Assumptions

- Purging unassigned cards and linked transactions/history is a deliberate exception to append-only facts and cannot be reconstructed by rolling back the migration; verify backups and scope the deletion strictly by `AssignedCarId IS NULL`.
- POS unit price is the undiscounted station price. Correction rows must also carry a signed quantity basis so their POS gross and savings reversal can be calculated directly.
- The existing seeded card is assigned, so the cleanup should be a no-op on normal seed data; migration tests must still exercise the null-assignment case.

## Success Criteria (Summary)

- No issued card can be reassigned, and fresh card history shows Active issuance plus later states newest first.
- The editable state control matches the persisted state after reload.
- Selected-month gross savings, net/gross spend, and transaction rows reconcile under signed corrections in both languages.
- SQL-backed migration and composed browser tests pass without removing assigned-card facts.