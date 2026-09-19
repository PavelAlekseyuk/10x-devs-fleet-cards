---
project: "Beznynowicz Fleet Cards Client Portal"
context_type: greenfield
created: 2026-09-19
updated: 2026-09-19
checkpoint:
  current_phase: 8
  phases_completed: [1, 2, 3, 4, 5, 6, 7]
  gray_areas_resolved:
    - topic: "primary pain"
      decision: "Limited fuel-network choice and difficulty comparing fuel-card program costs"
    - topic: "primary persona scope"
      decision: "One company representative per client installation"
  frs_drafted: 8
  quality_check_status: accepted
product_type: web-app
target_scale:
  users: small
  qps: low
  data_volume: small
timeline_budget:
  mvp_weeks: 5
  hard_deadline: 2026-12-06
  after_hours_only: false
---

## Vision & Problem Statement

Company representatives who manage vehicle fueling need access to multiple petrol-station networks instead of being tied to one fuel-company contract. They also need to compare what their company would pay under different fuel-card programs using historical transactions, including VAT and the program that was active at the time. Today, limited network choice and fragmented cost visibility make it harder to choose a cheaper option.

The product aggregates access to multiple fuel-station networks and uses historical, program-aware transaction data to make cost differences visible while preserving the access benefit. The MVP focuses on fleet-card operations and program decisions; future spending limits and credit features are outside the current scope.

## User & Persona

The primary persona is a company representative using one client installation. They use the portal when fueling company vehicles, reviewing recent card spending and VAT, managing virtual cards, or deciding whether to switch to another fuel-card program from a future effective date.

## Success Criteria

### Primary

- A seeded user can log in, view client details and last-month aggregations, create a vehicle with allowed fuel types, issue and assign a virtual card, receive generated test transactions, and review them.

### Secondary

- A user can select a new fleet-card program using historical transaction data.

### Guardrails

- Full PAN is never stored.
- Fleet-card program history remains immutable.
- Transaction totals and VAT are correct.
- Public deployment receives security-first treatment from the beginning.

## Timeline acknowledgment

Acknowledged on 2026-09-19: 5-week MVP requires sustained dedication; user accepted. The planned six hours per week are a mix of after-hours and day-job time.

## User Stories

### US-01: Establish a card and review its spending

- **Given** a seeded full-access user
- **When** they log in, create a vehicle with allowed fuel types, issue and assign a virtual card, and open the portal views
- **Then** the landing page shows one additional vehicle, one additional card, and the last-month total spending from the generated transactions
- **And** the transactions page shows the generated fuel transactions for the selected period

#### Acceptance Criteria

- The new vehicle stores its allowed fuel types.
- The new card is assigned to the newly created vehicle.
- Generated transactions use the vehicle's allowed fuel types.
- The landing-page total is expressed in PLN and matches the generated transactions for the last month.

## Functional Requirements

### Authentication

- FR-001: A company representative can sign in with a seeded username and password. Priority: must-have
  > Socrates: Counter-argument considered: "Seeded accounts may not represent the client's real access model." Resolution: kept seeded accounts as a documented mock of the intended client roles; real roles must be validated before production.

### Vehicles and cards

- FR-002: A full-access company representative can create, view, update, and remove vehicles, including their allowed fuel types. Priority: must-have
  > Socrates: Counter-argument considered: "Removing or editing a vehicle could invalidate existing card assignments or historical transactions." Resolution: removal becomes an inactive/archive action when history or assignments exist.
- FR-003: A full-access company representative can issue, view, update the status of, and return virtual fleet cards without storing the full PAN. Priority: must-have
  > Socrates: Counter-argument considered: "Issuing cards is needed to demonstrate the core portal flow and the PAN boundary." Resolution: kept as written.
- FR-004: A full-access company representative can assign a fleet card to a vehicle. Priority: must-have
  > Socrates: Counter-argument considered: "A card may need to move between vehicles, making assignment history ambiguous." Resolution: keep assignment and record effective periods so historical transactions retain their original vehicle context.

### Transactions and dashboard

- FR-005: The system can generate mocked fuel transactions for a newly issued card using the assigned vehicle's allowed fuel types. Priority: must-have
  > Socrates: Counter-argument considered: "Without generated data, the portal cannot demonstrate its reporting flow without manual fixtures." Resolution: kept as written.
- FR-006: A company representative can view card transactions for a selected period. Priority: must-have
  > Socrates: Counter-argument considered: "Transaction history is the evidence needed to compare costs and verify generated data." Resolution: kept as written.
- FR-007: A company representative can see the landing-page vehicle count, card count, and last-month total spending. Priority: must-have
  > Socrates: Counter-argument considered: "The landing page is the first visible proof that the new card and transactions affected the client account." Resolution: kept as written.

### Fleet-card programs

- FR-008: A full-access company representative can select a fleet-card program using historical transaction data. Priority: nice-to-have
  > Socrates: Counter-argument considered: "Program selection is the domain decision that differentiates this product from a generic card list." Resolution: kept as a nice-to-have; it remains secondary to the operational card and transaction flow.

## Non-Functional Requirements

- Full PAN never leaves the card provider and never appears in client-accessible data.
- Transaction totals and VAT, including refunds, cancellations, and recalculations, reconcile exactly with the displayed records.
- Publicly exposed access rejects unauthenticated and unauthorized requests.

## Business Logic

For a selected historical period, the portal calculates what the company would have paid under each available fleet-card program, using the transactions and program-active periods, then orders the programs from the lowest to the highest amount.

The calculation consumes fuel type, quantity, station price, discount or premium, VAT, refunds, cancellations, recalculations, and the option of no assigned program. Program history is immutable, so a past transaction is evaluated against the program that was active at that time.

The representative encounters the result as a ranked list of available programs and comparable amounts for the selected period.

## Access Control

Users sign in with seeded username/password accounts. Self-onboarding is not part of the MVP. Unauthenticated users cannot access the portal.

The MVP has two roles:

- **Full access:** can manage client vehicles and cards, view transactions, and access fleet-card program assignment and benefit calculations.
- **Read-only:** can view client details, vehicles, cards, transactions, and language settings, but cannot edit data or access program assignment and benefit calculations.

Unauthorized actions are denied.

## Non-Goals

- No self-onboarding or real identity-provider integration; seeded accounts remain a demonstration model.
- No PIN management or storage of full PAN; the card provider remains mocked.
- No Operations Team module or editing of centrally defined programs in the client portal.
- No spending limits, card-group budgets, loans, or overdrafts in this MVP.
- No multi-region or offline-first guarantee; the MVP targets one client installation at a time.

## Open Questions

1. **What are the exact rules for VAT, refunds, cancellations, and recalculations?** — Owner: product/client stakeholders. By: before implementation of transaction calculations.
2. **What is the exact program comparison formula for discounts or premiums by fuel type and active period?** — Owner: product/client stakeholders. By: before implementation of program comparison.
3. **What are the final client-role boundaries beyond the seeded full-access and read-only users?** — Owner: product/client stakeholders. By: before replacing mocked authentication.
4. **Which client-installation data must be isolated and what evidence is required for that boundary?** — Owner: product/client stakeholders. By: before public deployment.

## Forward: tech-stack

The user supplied these implementation constraints for downstream stack and infrastructure work: .NET 10 ASP.NET REST API; Angular SPA with TypeScript hosted by the backend; Azure hosting using free-tier services where possible; Azure SQL, Azure Table Storage, or Cosmos DB depending on the relational need; and GitHub Actions with Terraform for infrastructure and application deployment.

## Forward: technical-roadmap

The user supplied future directions that are outside this MVP: spending limits on individual cards, card groups, or the whole client; future-dated budget periods; and client payment through loans or overdrafts. The shared Fleet Card Programs are centrally defined and copied to a client installation when selected.
