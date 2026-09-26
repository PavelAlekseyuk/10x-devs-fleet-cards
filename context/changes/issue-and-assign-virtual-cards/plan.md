# Issue and Assign Virtual Cards Implementation Plan

## Overview

Deliver the S-03 card workflow as a secure, persisted vertical slice: a read-write user issues a virtual card for an active vehicle, generated completed transactions snapshot that vehicle, and users can manage card state and inspect assignment history. The provider boundary never returns full PAN, CVV, or PIN to application code.

## Current State Analysis

`FleetCard` already stores a current `CardState` and nullable `AssignedCarId`; `CardState` includes Active, Blocked, Stolen, and Archived. The existing card DTO exposes a masked PAN and provider token, and `/api/cards` is authenticated but read-only. There is no card command service, provider abstraction, assignment history, or transaction generator. `FuelTransaction` already stores the vehicle ID and plate snapshot needed to preserve historical context.

Vehicle writes establish the local pattern: Application contracts, EF and seeded service registrations, minimal API routes guarded by `ReadWrite` and CSRF, audit events, and API tests. The Angular `/cards` route still renders the generic shell placeholder; the standalone vehicle feature, its service/spec, and the language dictionaries are the nearest UI pattern.

## Desired End State

A read-write representative can issue a card by selecting an active vehicle, view its masked details, change it to any defined state, reassign it immediately to another active vehicle, and inspect an assignment timeline. Each successful issue creates one card, an assignment event, an audit event, and 1-5 deterministic completed mock transactions using the selected vehicle's fuel type and immutable vehicle snapshot. Read-only users can view cards and history but cannot mutate them.

### Key Discoveries:

- `src/FleetCards.Domain/FleetCardsModel.cs:3,60` defines card states and the current card assignment; `src/FleetCards.Domain/FleetCardsModel.cs:69` gives transactions vehicle ID and plate snapshot fields.
- `src/FleetCards.Contracts/Models.cs:37` and `src/FleetCards.Infrastructure/EfFleetCardsReadService.cs:27` define the existing card list projection, including masked PAN and provider token.
- `src/FleetCards.Api/Program.cs:272,387` shows read-write vehicle command conventions and the current read-only card route. `src/FleetCards.Infrastructure/FleetCardsInfrastructure.cs` selects EF services when SQL is configured and seeded services otherwise.
- `src/FleetCards.Infrastructure/FleetCardsDbContext.cs:69` maps the existing card/client/vehicle relationships and seed data; no assignment-history table exists in the current snapshot.
- `web/src/app/app.routes.ts:10` points `/cards` at `ShellComponent`; `web/src/app/shell.component.html:26` renders generic placeholder content. `web/src/app/fleet-vehicles.component.ts:24` and its colocated spec provide the Angular pattern.
- `context/foundation/architecture.md`, `context/foundation/decisions.md`, and `context/foundation/security.md` require a mock provider boundary, transactional issue-time generation, deterministic test data, immutable historical facts, and security-first handling. The explicit card decision allows any defined state, including reactivation; this plan updates the generic security wording to make that card-specific policy clear.

## What We're NOT Doing

- Accepting, storing, logging, or returning full PAN, CVV, or PIN; integrating a real card processor or provider callbacks.
- Implementing card return, explicit unassignment, scheduled or backdated assignment changes, or a one-card-per-vehicle restriction.
- Generating refund, cancellation, or recalculation records, or resolving their currently open calculation rules.
- Building transaction browsing, period filters, dashboard totals, or program comparison; those remain later roadmap work.
- Replacing seeded authentication, adding multi-client hosting, or completing production deployment.

## Implementation Approach

Follow the existing Domain → Application → Infrastructure → API direction. Add a card application contract used by both EF and seeded modes so card reads and mutations share one state source. Put the provider interface above Infrastructure and implement the mock adapter below it; only masked PAN, provider token, and safe metadata cross that boundary. Persist append-only card-assignment events and derive effective intervals from the next event rather than editing closed history. The API and Angular UI follow the established vehicle feature patterns, with a dedicated cards view and bilingual resources.

## Critical Implementation Details

Card issue requires an active vehicle because transactions are generated on issue and must use that vehicle's permitted fuel type and snapshot. After the mock provider returns safe card data, persist the card, assignment event, audit entry, and generated transactions as one EF transaction. Assignment changes are immediate and append a new event; past events and transaction snapshots are never rewritten. A card's assignment remains in place when its state changes, and every defined state is selectable after enum validation without a transition matrix.

## Phase 1: Card Contracts, Provider, and Persistence

### Overview

Establish the card application and persistence foundation, including safe mock issuance, immutable assignment history, and deterministic issue-time transaction generation.

### Changes Required:

#### 1. Card domain and contracts

**File**: `src/FleetCards.Domain/FleetCardsModel.cs`, `src/FleetCards.Contracts/Models.cs`

**Intent**: Represent card assignment history and the request/response data required for issue, state change, reassignment, and a history timeline.

**Contract**: Add an append-only assignment-event shape with card ID, vehicle ID, UTC effective timestamp, and a unique per-card ordering key. Order events by timestamp then ordering key so equal timestamps remain deterministic. Expose UTC half-open periods `[effectiveFrom, effectiveTo)`, deriving each end from the next event; the current event has `effectiveTo: null`. Keep one current assignment per card and allow multiple cards per vehicle. Issue requests identify an active vehicle; contracts contain no PAN/CVV/PIN input or output fields.

#### 2. Card application service and provider boundary

**File**: `src/FleetCards.Application/FleetCardService.cs`, `src/FleetCards.Application/FleetCardsApplication.cs`

**Intent**: Define the card lifecycle operations and provider contract at the application boundary, following the existing vehicle command pattern.

**Contract**: Provide list, issue, state-change, immediate reassignment, and assignment-history operations. `ICardProvider` returns only masked PAN, provider token, and safe metadata. State changes accept any defined `CardState`; audit actions identify the selected state. An Archived card may receive a state change, including reactivation, but reassignment and fuel operations are rejected until it is reactivated. Reassignment accepts only another active vehicle and does not support clearing an existing assignment.

#### 3. Safe mock provider and transaction generator

**File**: `src/FleetCards.Infrastructure/MockCardProvider.cs`, `src/FleetCards.Application/MockFuelTransactionGenerator.cs`

**Intent**: Provide local card issuance and completed transaction generation without allowing raw payment credentials into application persistence or responses.

**Contract**: The mock provider returns masked PAN and a unique provider token only. Generate 1-5 deterministic completed transactions in the previous month using the assigned vehicle's fuel type, configured VAT rate (23% MVP default), and away-from-zero decimal rounding. Transactions snapshot the vehicle ID and registration plate at generation; refunds, cancellations, and recalculations are excluded.

#### 4. EF and seeded card services, registration, and migration

**File**: `src/FleetCards.Infrastructure/EfFleetCardService.cs`, `src/FleetCards.Infrastructure/SeededFleetCardService.cs`, `src/FleetCards.Infrastructure/FleetCardsDbContext.cs`, `src/FleetCards.Infrastructure/FleetCardsInfrastructure.cs`, `src/FleetCards.Infrastructure/Migrations/`

**Intent**: Persist card issue, assignment events, state changes, audit events, and generated transactions consistently in SQL and the existing seeded fallback mode.

**Contract**: Add the assignment-event set, mapping, unique per-card ordering constraint, timestamp/order indexes, and migration/snapshot updates. Backfill an initial event for existing assigned cards; for the seeded card, use a deterministic effective time no later than its earliest linked transaction. A successful issue writes the card, initial assignment, audit, and generated transactions atomically. Reassignment appends an event, updates only the current card assignment, and leaves earlier events and transaction snapshots unchanged. Route card list reads through the same card service/store as commands.

### Success Criteria:

#### Automated Verification:

- Application tests verify card issuance safety, assignment rules, unrestricted defined-state changes, deterministic completed transaction generation, and immutable vehicle snapshots.
- EF model/migration checks pass, and existing seeded card and transaction data remain readable after migration.

#### Manual Verification:

- Inspect the resulting card and assignment records to confirm the seed history is represented and no raw card credentials are persisted.

**Implementation Note**: After completing this phase and all automated verification passes, pause for human confirmation of the manual check before proceeding.

## Phase 2: Protected Card API

### Overview

Expose the card lifecycle and assignment history through authenticated endpoints with the same server-side permission and CSRF enforcement as vehicle mutations.

### Changes Required:

#### 1. Card routes and command result mapping

**File**: `src/FleetCards.Api/Program.cs`

**Intent**: Replace the read-only card wiring with API access to the card application service while preserving the existing list contract.

**Contract**: Define and test this HTTP contract: authenticated `GET /api/cards` returns card IDs, masked PAN, provider token, string state, assigned vehicle ID, and assigned vehicle registration plate; `POST /api/cards` accepts `{ vehicleId }` and returns `201 Created` with the card response and Location; `PUT /api/cards/{id}/state` accepts `{ state }` using a case-sensitive defined state name and returns `200 OK`; `PUT /api/cards/{id}/assignment` accepts `{ vehicleId }` and returns `200 OK`; `GET /api/cards/{id}/assignment-history` returns vehicle IDs/plates and UTC half-open periods with `effectiveFrom` inclusive, `effectiveTo` exclusive, and null `effectiveTo` for the current assignment. History sorts by effective timestamp then the per-card ordering key. Reject numeric, unknown, or incorrectly-cased state values. Return `400` for invalid input, `401` for anonymous callers, `403` for read-only mutation or invalid CSRF, `404` for missing cards/vehicles, and `409` for lifecycle conflicts. Every mutation requires `ReadWrite` and a valid CSRF token; list and history remain readable to either authenticated role.

#### 2. Card API authorization, history, and redaction tests

**File**: `tests/FleetCards.Api.Tests/CardApiTests.cs`, `tests/FleetCards.Api.Tests/PlatformTests.cs`

**Intent**: Prove the endpoint contract, role boundary, audit behavior, history visibility, and payment-data boundary at the HTTP layer.

**Contract**: Cover anonymous rejection, read-only reads and denied mutations, read-write mutations, missing/invalid CSRF, invalid enum values, inactive-vehicle rejection, Archived-card reassignment rejection until reactivation, issue/reassignment history, and audit entries. Assert full PAN/CVV/PIN sentinels are absent from API bodies, persisted entities, and captured logs. The in-memory API host verifies route behavior; relational transaction behavior is verified through Aspire in Phase 4.

### Success Criteria:

#### Automated Verification:

- `dotnet test tests/FleetCards.Api.Tests/FleetCards.Api.Tests.csproj` passes with role, CSRF, status, history, audit, and redaction coverage.
- Authenticated card reads return masked card data and effective assignment periods; card mutation routes reject read-only and anonymous callers.

#### Manual Verification:

- Exercise issue, reassignment, each defined state, and history reads with both seeded accounts; confirm state changes do not erase the vehicle assignment.

**Implementation Note**: After completing this phase and all automated verification passes, pause for human confirmation of the manual check before proceeding.

## Phase 3: Angular Card Management

### Overview

Replace the cards placeholder with the real issue, list, state, reassignment, and assignment-history workflows.

### Changes Required:

#### 1. Card HTTP service and standalone feature

**File**: `web/src/app/card-management.service.ts`, `web/src/app/card-management.component.ts`, `web/src/app/card-management.component.html`, `web/src/app/card-management.component.scss`

**Intent**: Give the protected cards route an actual card-management experience that uses the API rather than shell preview data.

**Contract**: Add typed API operations and a standalone component for listing masked PAN/state/current vehicle, issuing with an active vehicle selected, changing to any defined state, reassigning to another active vehicle, and viewing assignment periods in a timeline. Do not render provider tokens. Preserve assignment when changing state, and offer no explicit unassign action.

#### 2. Route, shell, translations, and component tests

**File**: `web/src/app/app.routes.ts`, `web/src/app/shell.component.ts`, `web/src/app/shell.component.html`, `web/src/app/language.service.ts`, `web/src/app/card-management.component.spec.ts`

**Intent**: Integrate the card feature into the existing shell and maintain its permission-aware, bilingual interaction pattern.

**Contract**: Render the standalone card feature for the `cards` section; remove the generic placeholder action for this route. Add matching EN/PL translation keys. Hide mutation controls for ReadOnly users while leaving server authorization authoritative. Colocate Vitest coverage for HTTP requests, issue validation, state and reassignment controls, history rendering, permissions, and both languages.

### Success Criteria:

#### Automated Verification:

- From `web/`, `npm test` passes with card service/component coverage for both roles and both languages.
- From `web/`, `npm run build` passes under strict TypeScript and Angular template checking.

#### Manual Verification:

- Use the cards view in English and Polish to issue for an active vehicle, inspect the timeline, reassign, and select each state; confirm the provider token is not displayed and ReadOnly users see no mutation controls.

**Implementation Note**: After completing this phase and all automated verification passes, pause for human confirmation of the manual check before proceeding.

## Phase 4: Aspire Acceptance and Security Alignment

### Overview

Verify the complete persisted issue-to-history flow in the composed runtime and align the security guidance with the agreed card state policy.

### Changes Required:

#### 1. Composed card-flow acceptance coverage

**File**: `web/e2e/card-management.spec.ts`, `web/playwright.config.ts`

**Intent**: Prove card operations work against the SQL-backed Aspire composition rather than only isolated test services.

**Contract**: Verify ReadWrite issue requires an active vehicle, creates the card/assignment/audit/generated completed transactions, assignment history records immediate reassignment without changing prior transaction snapshots, and ReadOnly can inspect but not mutate. Verify unauthenticated rejection and sensitive-data redaction through the composed API. Reuse the existing isolated database contract and deterministic transaction seed.

#### 2. Card state security wording

**File**: `context/foundation/security.md`

**Intent**: Remove the conflict between the generic transition-validation guidance and the explicit S-03 decision allowing every defined card state, including reactivation.

**Contract**: Clarify that card state inputs must be valid defined states and every change is authorized and audited, but card state transitions have no additional transition matrix in this MVP. Archived cards may receive state changes, including reactivation, but cannot be reassigned or used for fuel operations until reactivated; archived vehicles remain ineligible assignment targets.

### Success Criteria:

#### Automated Verification:

- From `web/`, `npm run test:e2e` passes through Aspire with a fresh isolated SQL database.
- `dotnet test` passes for the solution; migration startup and card/assignment/transaction writes complete in the SQL-backed composed run.

#### Manual Verification:

- Complete the issue-to-reassignment journey with the seeded ReadWrite user, then verify the ReadOnly user can inspect card and assignment history but cannot mutate; confirm older generated transaction vehicle snapshots remain unchanged.

**Implementation Note**: After completing this phase and all automated verification passes, pause for human confirmation that the composed journey meets the acceptance criteria.

## Testing Strategy

### Unit Tests:

- Exercise provider response allowlisting and prove raw PAN/CVV/PIN never enter application contracts, EF entities, logs, or API output.
- Verify issue requires an active vehicle; generated transactions are completed, deterministic, in the previous month, use that vehicle's fuel type, configured VAT and rounding, and snapshot its ID and plate.
- Verify multiple cards may share a vehicle, one card has only one current assignment, immediate reassignment appends history, and prior transaction snapshots remain unchanged.
- Verify every defined state can be selected, state changes create audit entries, and assignment remains unchanged across Blocked, Stolen, and Archived states.

### Integration Tests:

- API tests cover auth, permission, CSRF, validation, card issue/state/reassignment/history, audit, and redaction.
- Aspire/Playwright tests exercise the SQL migration and the full issue-to-generated-transaction-to-history flow against the composed API and SPA.

### Manual Testing Steps:

1. Sign in as `anna.nowak`, issue a card for each active-vehicle fuel type, and inspect its masked details and generated completed transactions.
2. Reassign a card to another active vehicle and confirm the timeline shows the prior and current periods while existing transactions retain their original vehicle snapshots.
3. Change the card through Active, Blocked, Stolen, and Archived states; confirm its assignment remains, then sign in as `piotr.wisniewski` and verify viewing works while mutations are unavailable.

## Performance Considerations

The PRD targets one client installation with low request volume and small data volume. Use card-ID/effective-time indexes for history lookup and avoid loading unrelated audit or transaction rows; pagination or caching is not required for this slice.

## Migration Notes

Add an EF migration and update the model snapshot for append-only assignment events. Backfill currently assigned cards without modifying existing cards or transaction facts; use the earliest linked transaction as the seeded card's known lower bound, and document that it is not a claim about the original assignment date. Aspire startup already applies migrations. The new issue flow writes card, assignment event, audit, and completed transaction records atomically.

## References

- `context/foundation/roadmap.md` — S-03 scope and dependencies.
- `context/foundation/prd.md` — FR-003/004, US-01, access rules, and transaction requirements.
- `context/foundation/decisions.md` — mock provider, unrestricted card states, issue-time generation, VAT default/rounding, and transaction snapshots.
- `context/foundation/architecture.md` and `context/foundation/security.md` — layering, persistence, authorization, audit, and redaction boundaries.
- `src/FleetCards.Application/FleetVehicleService.cs`, `src/FleetCards.Infrastructure/EfFleetVehicleService.cs`, and `tests/FleetCards.Api.Tests/FleetVehicleApiTests.cs` — command and API test patterns.
- `web/src/app/fleet-vehicles.component.ts`, `web/src/app/fleet-vehicles.component.spec.ts`, and `web/e2e/fleet-vehicles.spec.ts` — Angular and Aspire acceptance patterns.

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append ` — <commit sha>` when a step lands. Do not rename step titles. See `references/progress-format.md`.

### Phase 1: Card Contracts, Provider, and Persistence

#### Automated

- [ ] 1.1 Application tests verify card issuance safety, assignment rules, unrestricted defined-state changes, deterministic completed transaction generation, and immutable vehicle snapshots.
- [ ] 1.2 EF model/migration checks pass, and existing seeded card and transaction data remain readable after migration.

#### Manual

- [ ] 1.3 Inspect the resulting card and assignment records to confirm the seed history is represented and no raw card credentials are persisted.

### Phase 2: Protected Card API

#### Automated

- [ ] 2.1 `dotnet test tests/FleetCards.Api.Tests/FleetCards.Api.Tests.csproj` passes with role, CSRF, status, history, audit, and redaction coverage.
- [ ] 2.2 Authenticated card reads return masked card data and effective assignment periods; card mutation routes reject read-only and anonymous callers.

#### Manual

- [ ] 2.3 Exercise issue, reassignment, each defined state, and history reads with both seeded accounts; confirm state changes do not erase the vehicle assignment.

### Phase 3: Angular Card Management

#### Automated

- [ ] 3.1 From `web/`, `npm test` passes with card service/component coverage for both roles and both languages.
- [ ] 3.2 From `web/`, `npm run build` passes under strict TypeScript and Angular template checking.

#### Manual

- [ ] 3.3 Use the cards view in English and Polish to issue for an active vehicle, inspect the timeline, reassign, and select each state; confirm the provider token is not displayed and ReadOnly users see no mutation controls.

### Phase 4: Aspire Acceptance and Security Alignment

#### Automated

- [ ] 4.1 From `web/`, `npm run test:e2e` passes through Aspire with a fresh isolated SQL database.
- [ ] 4.2 `dotnet test` passes for the solution; migration startup and card/assignment/transaction writes complete in the SQL-backed composed run.

#### Manual

- [ ] 4.3 Complete the issue-to-reassignment journey with the seeded ReadWrite user, then verify the ReadOnly user can inspect card and assignment history but cannot mutate; confirm older generated transaction vehicle snapshots remain unchanged.
