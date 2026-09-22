---
project: "Beznynowicz Fleet Cards Client Portal"
version: 1
status: draft
created: 2026-09-22
updated: 2026-09-22
prd_version: 1
main_goal: speed
top_blocker: decisions
milestone_id: first-operational-card-flow
milestone_seq: 1
milestone_status: open
---

# Roadmap: Beznynowicz Fleet Cards Client Portal

> Derived from `context/foundation/prd.md` (v1), `spec/implementation-roadmap.md`, and an auto-researched codebase baseline.
> Edit in place; archive when superseded.
> Slices below are listed in dependency order. The "At a glance" table is the index.

## Milestone

**M-1: First operational card flow** - Status: open

- **Intent:** Deliver the required representative journey from a seeded sign-in through vehicle and virtual-card handling to generated spending and totals. Keep the scope to the primary MVP outcome so the client can exercise one complete operational flow.
- **Source materials:** `context/foundation/prd.md` (v1), `spec/implementation-roadmap.md`
- **Done when:** every F-NN and S-NN below is `done`, and a seeded full-access user completes US-01 with totals that reconcile to the generated transactions.
- **Scope anchors:** FR-001 through FR-007; US-01; the PRD's transaction-correctness and unauthenticated-access requirements.

## Vision recap

Company representatives need both access to several fuel networks and clear cost visibility when choosing a fleet-card program. This milestone delivers the operational data needed to make card spending visible: managed vehicles, virtual cards, generated transactions, and a reconciled last-month total.

## North star

**S-04: Representative can review generated card spending and client totals** - This is the earliest complete proof of the primary success criterion because it turns the newly issued and assigned card into visible, reconciled spending information.

> The north star is the smallest end-to-end slice whose successful delivery proves the product's central user journey works; it is placed as early as its prerequisites allow.

## At a glance

| ID | Change ID | Outcome (user can ...) | Prerequisites | PRD refs | Status |
| --- | --- | --- | --- | --- | --- |
| F-01 | repeatable-demo-runtime | (foundation) run a repeatable local demonstration with safe seeded data and a verified health path | - | US-01, FR-001 | ready |
| S-01 | seeded-sign-in-and-access | sign in with a seeded account and access only the portal actions permitted to that account | F-01 | FR-001 | proposed |
| S-02 | manage-fleet-vehicles | create, view, change, archive, and restore fleet vehicles with allowed fuel types | S-01 | FR-002, US-01 | proposed |
| S-03 | issue-and-assign-virtual-cards | issue, manage, return, and assign a virtual card without exposing full PAN data | S-02 | FR-003, FR-004, US-01 | proposed |
| S-04 | review-generated-card-spending | review generated fuel transactions for a selected period and reconcile them to the landing-page total | S-03 | FR-005, FR-006, FR-007, US-01 | blocked |

## Baseline

What is already in place in the codebase as of `2026-09-22` (auto-researched and user-confirmed). Foundations below assume these are present and do not re-scaffold them.

- **Frontend:** partial - a standalone frontend exists, but its route table is empty and the root view uses preview data (`web/src/app/app.routes.ts`, `web/src/app/app.ts`).
- **Backend / API:** partial - read-only endpoints expose seeded client, vehicle, card, transaction, and program data (`src/FleetCards.Api/Program.cs`, `src/FleetCards.Application/FleetCardsApplication.cs`).
- **Data:** partial - a relational context, SQL provider, and initial migration exist, but deterministic runtime seed and write-flow use are incomplete (`src/FleetCards.Infrastructure/FleetCardsDbContext.cs`, `src/FleetCards.Infrastructure/Migrations/20260921165200_InitialCreate.cs`).
- **Auth:** partial - current-user data and permission types exist, but there is no sign-in, session, or route-level enforcement (`src/FleetCards.Api/Program.cs`, `src/FleetCards.Domain/FleetCardsModel.cs`).
- **Deploy / infra:** partial - infrastructure, CI/CD, and OIDC setup scaffolding exist; environment setup remains external to the MVP flow (`infra/terraform/main.tf`, `.github/workflows/main.yml`).
- **Observability:** partial - logging, telemetry, and development health endpoints are configured, but no product-specific instrumentation is present (`src/FleetCards.ServiceDefaults/Extensions.cs`).

## Foundations

### F-01: Repeatable demo runtime

- **Outcome:** (foundation) a local run initializes safe, deterministic demonstration data and offers a verified application health path.
- **Change ID:** repeatable-demo-runtime
- **PRD refs:** US-01, FR-001, Non-Functional Requirements (transaction correctness and protected public access)
- **Unlocks:** S-01, S-02, S-03, S-04; a reproducible verification path for the primary user journey
- **Prerequisites:** -
- **Parallel with:** -
- **Blockers:** -
- **Unknowns:** -
- **Risk:** Existing persistence and composition are only partly wired, so a narrow repeatable runtime contract must land before user-flow work can be verified safely.
- **Status:** ready

## Slices

### S-01: Seeded sign-in and access

- **Outcome:** user can sign in with a seeded account and access only the portal actions permitted to that account.
- **Change ID:** seeded-sign-in-and-access
- **PRD refs:** FR-001, Access Control
- **Prerequisites:** F-01
- **Parallel with:** -
- **Blockers:** -
- **Unknowns:**
  - What are the final client-role boundaries beyond the seeded full-access and read-only users? - Owner: product/client stakeholders. Block: no.
- **Risk:** The PRD deliberately uses mocked identities, so this slice must enforce the documented two roles without prematurely deciding production identity rules.
- **Status:** proposed

### S-02: Manage fleet vehicles

- **Outcome:** user can create, view, change, archive, and restore fleet vehicles with allowed fuel types.
- **Change ID:** manage-fleet-vehicles
- **PRD refs:** FR-002, US-01
- **Prerequisites:** S-01
- **Parallel with:** -
- **Blockers:** -
- **Unknowns:** -
- **Risk:** Vehicle history must remain usable by later card assignments and transactions, so archival behavior precedes destructive removal.
- **Status:** proposed

### S-03: Issue and assign virtual cards

- **Outcome:** user can issue, manage, return, and assign a virtual card to a vehicle without seeing or storing full PAN data.
- **Change ID:** issue-and-assign-virtual-cards
- **PRD refs:** FR-003, FR-004, US-01, Non-Functional Requirements (full PAN boundary)
- **Prerequisites:** S-02
- **Parallel with:** -
- **Blockers:** -
- **Unknowns:** -
- **Risk:** Card state changes and vehicle reassignment need effective history so future reporting cannot rewrite past vehicle context.
- **Status:** proposed

### S-04: Review generated card spending

- **Outcome:** user can review generated fuel transactions for a selected period and reconcile them to the landing-page vehicle count, card count, and last-month spending total.
- **Change ID:** review-generated-card-spending
- **PRD refs:** FR-005, FR-006, FR-007, US-01, Non-Functional Requirements (transaction totals and VAT)
- **Prerequisites:** S-03
- **Parallel with:** -
- **Blockers:** -
- **Unknowns:**
  - What are the exact rules for VAT, refunds, cancellations, and recalculations? - Owner: product/client stakeholders. Block: yes.
- **Risk:** The primary journey cannot claim reconciled transaction correctness until the calculation rules are explicit; resolving them makes the required totals verifiable.
- **Status:** blocked

## Backlog Handoff

| Roadmap ID | Change ID | Suggested issue title | Ready for `/10x-plan` | Notes |
| --- | --- | --- | --- | --- |
| F-01 | repeatable-demo-runtime | Establish repeatable local demonstration runtime | yes | It unlocks every milestone slice. |
| S-01 | seeded-sign-in-and-access | Add seeded sign-in and access enforcement | no | Requires F-01. |
| S-02 | manage-fleet-vehicles | Deliver fleet vehicle management | no | Requires S-01. |
| S-03 | issue-and-assign-virtual-cards | Deliver virtual-card issue and assignment | no | Requires S-02. |
| S-04 | review-generated-card-spending | Deliver generated transaction and total review | no | Requires S-03 and the VAT decision. |

## Open Roadmap Questions

1. **What are the exact rules for VAT, refunds, cancellations, and recalculations?** - Owner: product/client stakeholders. Block: S-04.
2. **What is the exact program comparison formula for discounts or premiums by fuel type and active period?** - Owner: product/client stakeholders. Block: parked program-comparison work.
3. **What are the final client-role boundaries beyond the seeded full-access and read-only users?** - Owner: product/client stakeholders. Block: no current milestone slice; required before replacing mocked authentication.
4. **Which client-installation data must be isolated and what evidence is required for that boundary?** - Owner: product/client stakeholders. Block: public deployment work.

## Parked

- **Fleet-card program selection and comparison (FR-008).** - Why parked: it is nice-to-have and depends on an unresolved formula; the speed goal prioritizes the required operational flow.
- **Self-onboarding and real identity-provider integration.** - Why parked: PRD Non-Goals retain seeded accounts as the MVP demonstration model.
- **PIN management and storage of full PAN.** - Why parked: PRD Non-Goals prohibit handling these values; the provider stays mocked.
- **Operations Team functions and centrally defined program editing.** - Why parked: PRD Non-Goals exclude them from the client portal.
- **Spending limits, card-group budgets, loans, and overdrafts.** - Why parked: PRD Non-Goals defer credit and budget capabilities.
- **Multi-region and offline-first guarantees.** - Why parked: PRD Non-Goals limit the MVP to one client installation.
- **Production deployment completion.** - Why parked: the existing implementation roadmap defers it until the local MVP contract is stable, and the isolation decision remains open.

## Milestone History

<!-- Append-only. Empty for the first milestone. -->

## Done

<!-- `/10x-archive` appends completed items here. -->