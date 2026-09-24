# Manage Fleet Vehicles Implementation Plan

## Overview

Deliver full vehicle management for an authenticated client representative: create, view, edit, search, archive, and restore vehicles with exactly one allowed fuel type. Keep vehicle identity consistent across persistence and search, preserve card and transaction history, and enforce the existing ReadWrite/ReadOnly access boundary.

## Current State Analysis

- `FleetCar` already stores `RegistrationPlate`, `NormalizedRegistrationPlate`, optional make/model, and `IsArchived`, but has no fuel type or persisted audit entity (`src/FleetCards.Domain/FleetCardsModel.cs:36-45`). The vehicle response contains no fuel type or mutation request records (`src/FleetCards.Contracts/Models.cs:14-19`).
- EF Core maps a unique `(ClientId, NormalizedRegistrationPlate)` index without an active-only filter. Cards and transactions retain restrictive vehicle foreign keys; transactions also store a vehicle-plate snapshot (`src/FleetCards.Infrastructure/FleetCardsDbContext.cs:36-50`).
- `GET /api/cars` is the only vehicle route. Its EF query trims the raw search string and searches `RegistrationPlate`, so it does not implement case- and whitespace-insensitive normalized search (`src/FleetCards.Api/Program.cs:198-202`, `src/FleetCards.Infrastructure/EfFleetCardsReadService.cs:32-48`).
- Authentication and the `ReadWrite` authorization policy are already wired. `CsrfProtection` provides the cookie/header validation helper, but only login/logout currently invoke it (`src/FleetCards.Api/Program.cs`, `src/FleetCards.Api/AuthenticationSupport.cs:6-42`).
- Angular routes, auth guards, HttpClient, and EN/PL resources exist. `/cars` still renders one hardcoded shell row; no vehicle API client, form, search control, archive filter, or mutation tests exist (`web/src/app/shell.component.html`, `web/src/app/app.routes.ts`, `web/src/app/language.service.ts`).
- API tests use xUnit and `WebApplicationFactory<Program>`; Angular unit tests use Vitest. There is no Playwright setup. The user approved a focused Playwright browser suite for S-02.
- The S-01 change is `impl_reviewed`, but the roadmap still lists it as `in-progress`; complete S-01 before implementing S-02. Its auth plan confirms the no-connection-string seeded fallback as an existing runtime/test mode (`context/changes/seeded-sign-in-and-access/plan.md`).

## Desired End State

ReadWrite users can manage vehicles through the API and bilingual portal; ReadOnly users can search and view active or archived vehicles but cannot mutate them. Plate identity is uppercase and whitespace-insensitive, each vehicle has exactly one allowed fuel type, only active plates must be unique, and restores that conflict with an active plate fail without unarchiving the vehicle.

Archiving is non-destructive: the existing card relationship and transaction snapshots remain intact, while archived vehicles cannot receive future card assignments. Every successful vehicle mutation produces an append-only audit event. API and application tests plus browser acceptance cover the behavior and permission boundary.

### Key Discoveries:

- The EF index currently includes archived vehicles, so it must become active-only to permit a new active vehicle to reuse an archived vehicle's plate (`src/FleetCards.Infrastructure/FleetCardsDbContext.cs:43-46`).
- The primary seeded Volvo FH has a Diesel transaction; migration seed mapping is Volvo FH → Diesel and Toyota Corolla → 95. The Toyota value is an explicit demo assumption because it has no historical transaction.
- The seeded primary vehicle already has a card assigned. Archiving it must retain that link; archive must not delete or rewrite related transactions (`src/FleetCards.Infrastructure/FleetCardsDbContext.cs:160-226`).
- Security guidance requires append-only audit rows with actor, action, entity, timestamp, outcome, and correlation ID (`context/foundation/security.md`). The current code has no such persistence model.
- Aspire composes SQL Server, API, and the Angular dev server; its web endpoint is port 4200 (`src/FleetCards.AppHost/AppHost.cs`).

## What We're NOT Doing

- Issuing, blocking, returning, or assigning cards; S-03 owns those workflows. This change establishes the rule that archived vehicles are not assignable and tests that rule for the later assignment service.
- Generating or reviewing transactions, changing transaction snapshots, or implementing dashboard totals.
- Hard-deleting vehicles, detaching cards during archive, or rewriting historical vehicle/transaction data.
- Strict Polish registration-format validation. Validation is nonblank and at most 20 characters; case and whitespace do not distinguish identity, while non-whitespace punctuation remains significant.
- Persisting rejected or unauthorized command attempts as audit rows. Rejections remain in the existing structured API logs; durable audit rows are written for successful vehicle changes only.
- Full operational-flow Aspire end-to-end hardening, which remains S-05.
- Real identity-provider integration, multi-client hosting, or production audit retention policy.

## Implementation Approach

Keep the current Domain → Application → Infrastructure → API layering and Minimal API style. Add a focused vehicle service contract used by both the EF-backed runtime and the existing seeded no-connection fallback so list reads reflect successful writes in either mode. Back vehicle commands with EF Core in the composed SQL runtime; keep the database as the durable source of truth.

Add a single required fuel type to each vehicle and its Contracts DTOs, create append-only audit persistence, and replace the all-record plate index with active-only normalized-plate uniqueness. Expose read routes to authenticated users and mutations to ReadWrite users only; mutation requests must pass the existing double-submit CSRF check. In Angular, retain the authenticated shell and give its vehicle section a dedicated feature view, defaulting to active vehicles with an archived filter. Add Playwright for the bounded browser acceptance contract; S-05 remains responsible for the complete operational journey.

## Critical Implementation Details

The active unique index must filter on `IsArchived = 0`, otherwise an archived plate cannot be reused; a conflicting restore returns an error and leaves the vehicle archived. Archive preserves `FleetCard.AssignedCarId`, future assignment code rejects archived vehicle IDs, and plate/archive operations never rewrite a `FuelTransaction.VehiclePlate` snapshot.

## Phase 1: Vehicle Contracts and Persistence

### Overview

Extend the domain and API contract for one fuel type per vehicle, then migrate the relational model for active-only plate uniqueness and durable successful-mutation audit events.

### Changes Required:

#### 1. Vehicle domain and Contracts

**File**: `src/FleetCards.Domain/FleetCardsModel.cs`, `src/FleetCards.Contracts/Models.cs`

**Intent**: Represent the user-approved single allowed fuel type and define safe request/response records for vehicle management.

**Contract**: Every persisted and returned vehicle has exactly one value from `95`, `98`, `Diesel`, `95+`, `98+`, or `Diesel+`. Registration plate is required, make/model remain optional, and the normalized plate is internal rather than returned to clients.

#### 2. EF mapping, audit model, and migration

**File**: `src/FleetCards.Domain/FleetCardsModel.cs`, `src/FleetCards.Infrastructure/FleetCardsDbContext.cs`, `src/FleetCards.Infrastructure/Migrations/`

**Intent**: Persist vehicle fuel type and append-only audit events while allowing normalized plate reuse only after a vehicle is archived.

**Contract**: The `(ClientId, NormalizedRegistrationPlate)` unique index applies only to active vehicles. Audit events carry actor, action, entity identity, timestamp, successful outcome, and correlation ID. Migration maps the seeded Volvo FH to Diesel and Toyota Corolla to 95, preserving existing card and transaction relationships/snapshots.

### Success Criteria:

#### Automated Verification:

- The SQL Server migration adds one required fuel type per car, append-only vehicle audit storage, and active-only normalized-plate uniqueness, and applies successfully to the seeded database.
- Migration verification confirms the seeded Volvo FH is Diesel and Toyota Corolla is 95, and the existing Diesel transaction and card-to-vehicle reference remain unchanged.

#### Manual Verification:

- Inspect the migrated database to confirm both seeded cars remain active and the seeded card assignment and transaction plate snapshot are preserved.

**Implementation Note**: After automated checks pass, pause for human confirmation of the manual database inspection before proceeding.

## Phase 2: Vehicle Commands and API Enforcement

### Overview

Implement normalized read/search and vehicle commands in the application boundary, expose them through authenticated Minimal API routes, and write audit events atomically with successful state changes.

### Changes Required:

#### 1. Vehicle application service and validation

**File**: `src/FleetCards.Application/FleetCardsApplication.cs`, `src/FleetCards.Infrastructure/EfFleetCardsReadService.cs`, `src/FleetCards.Infrastructure/FleetCardsInfrastructure.cs`

**Intent**: Give vehicle reads and mutations one focused application contract and support both EF persistence and the established seeded fallback.

**Contract**: Support list/search, create, update, archive, restore, and an assignment eligibility check. Normalize identity by trimming and uppercasing the entered plate, then removing all whitespace for uniqueness and search; reject blank or over-20-character input. Preserve non-whitespace punctuation as significant. Enforce one supported fuel type, optional make/model, active-only uniqueness, and conflict-safe restoration. Archiving preserves existing assignments; archived vehicles are not eligible for future assignments.

#### 2. API routes, authorization, and CSRF

**File**: `src/FleetCards.Api/Program.cs`, `src/FleetCards.Api/AuthenticationSupport.cs`

**Intent**: Turn the existing read-only cars endpoint into the complete authenticated vehicle API without weakening the S-01 access boundary.

**Contract**: `GET /api/cars?search={search}&includeArchived={false}` returns `FleetCarResponse[]` to any authenticated user; `includeArchived` defaults to false. `POST /api/cars` accepts `CreateFleetCarRequest` and returns `201 Created`, a `FleetCarResponse`, and its `Location`. `PUT /api/cars/{id}` accepts `UpdateFleetCarRequest` and returns `200 OK` with the updated response. `POST /api/cars/{id}/archive` and `/restore` return `200 OK` with the updated response. The request records contain registration plate, one fuel type, and optional make/model; the response contains ID, display plate, fuel type, make/model, and archive state, never normalized identity. Mutations require the `ReadWrite` policy and valid CSRF cookie/header pair. Return `400` validation problem details, `404` for unknown IDs, and `409` conflict problem details for active-plate collisions, including restore collisions; anonymous calls receive `401` and ReadOnly or invalid-CSRF calls receive `403`.

#### 3. Successful mutation audit events

**File**: `src/FleetCards.Application/`, `src/FleetCards.Infrastructure/`, `src/FleetCards.Api/Program.cs`

**Intent**: Record durable evidence for every successful vehicle change while keeping rejected and unauthorized attempts in the existing structured logs.

**Contract**: Each successful create, update, archive, or restore creates exactly one append-only audit row with actor, action, entity, timestamp, outcome, and correlation ID in the same database transaction as the vehicle change where practical. No plate or vehicle history is hard-deleted.

### Success Criteria:

#### Automated Verification:

- Application tests cover plate normalization (case and all whitespace), blank/length validation, optional descriptive fields, single fuel-type validation, duplicate active plates, and restore conflicts.
- API integration tests prove ReadWrite-only car mutations, 401 anonymous access, 403 ReadOnly/invalid-CSRF access, route response contracts, CRUD/archive/restore, normalized search, and 400/404/409 error responses.
- Tests verify each successful vehicle mutation writes one append-only audit event and archiving preserves an existing card link while making the vehicle ineligible for future assignment.

#### Manual Verification:

- Exercise the API as ReadWrite and ReadOnly users; confirm mutation status codes, restore-conflict behavior, archived search/filter results, audit details, and unchanged transaction snapshots.

**Implementation Note**: After automated checks pass, pause for human confirmation of the API behavior before proceeding.

## Phase 3: Bilingual Vehicle Management UI

### Overview

Replace the vehicle placeholder with a real list and edit flow, retaining view access for both roles and showing commands only to ReadWrite users.

### Changes Required:

#### 1. Vehicle client and feature view

**File**: `web/src/app/fleet-vehicles.service.ts`, `web/src/app/fleet-vehicles.component.ts`, `web/src/app/fleet-vehicles.component.html`, `web/src/app/fleet-vehicles.component.scss`, `web/src/app/fleet-vehicles.component.spec.ts`, `web/src/app/shell.component.html`, `web/src/app/language.service.ts`

**Intent**: Connect the cars section to the vehicle API and provide focused list, search, create/edit, archive, and restore interactions within the existing authenticated shell.

**Contract**: The standalone vehicle feature component is rendered from the existing cars shell section and calls the named vehicle service, which uses the Phase 2 route/DTO contract. Active vehicles are shown by default; users can search and switch to archived vehicles. Vehicle forms require a plate and exactly one supported fuel type, with optional make/model. Existing assignments are not changed by archive. ReadOnly users can view/search but receive no mutation controls; API authorization remains authoritative.

#### 2. Bilingual labels and accessible states

**File**: `web/src/app/language.service.ts`, vehicle feature templates and colocated specs

**Intent**: Add English and Polish labels and validation/status messages for all vehicle actions and states.

**Contract**: Fuel types, active/archived state, search, empty results, validation errors, and create/edit/archive/restore actions are translated through the existing typed language resource pattern.

### Success Criteria:

#### Automated Verification:

- Angular tests cover active-first listing, archived filtering, normalized search, form validation, ReadOnly rendering, mutation flows, and EN/PL vehicle labels.
- `npm test` and `npm run build` pass from `web/`.

#### Manual Verification:

- In a browser, verify ReadWrite users can create, edit, archive, and restore vehicles, while ReadOnly users can search and view without mutation controls; repeat key states in English and Polish.

**Implementation Note**: After automated checks pass, pause for human confirmation of the vehicle UI and role behavior before proceeding.

## Phase 4: Browser Acceptance and Handoff

### Overview

Prove the S-02 vehicle workflow in a real browser against the composed local runtime and document the repeatable focused verification path.

### Changes Required:

#### 1. Playwright acceptance suite

**File**: `web/package.json`, `web/playwright.config.ts`, `web/e2e/fleet-vehicles.spec.ts`

**Intent**: Add the browser runner selected during planning and exercise the vehicle workflow through the actual Angular UI and API.

**Contract**: `npm run test:e2e` runs the named spec against the Aspire web endpoint at port 4200 using a dedicated disposable SQL database with deterministic seed/reset for each run. Test setup must never reset the normal persistent developer database. Cover both seeded roles, normalized search, create/edit/archive/restore, duplicate restore rejection, and the ReadOnly control boundary. Keep card issuance, transactions, and full-flow browser automation for S-03/S-05.

#### 2. Focused run and verification notes

**File**: `web/README.md`

**Intent**: Make browser setup and the vehicle acceptance path repeatable for contributors.

**Contract**: Document required local runtime/browser setup, Playwright command, seed mapping assumptions, and the distinction between this focused browser suite and S-05 composed-flow hardening.

### Success Criteria:

#### Automated Verification:

- The Playwright acceptance suite passes against the Aspire-composed API, Angular app, and isolated disposable SQL database for ReadWrite and ReadOnly vehicle workflows.
- `dotnet test FleetCards.sln`, `npm test`, `npm run build`, and `npm run test:e2e` pass without regressions introduced by this change.

#### Manual Verification:

- Start Aspire from a clean seeded database and confirm created/edited vehicle data persists across API reads and the browser workflow completes in both languages.
- Archive the seeded Volvo through the browser and confirm protected card and transaction reads still show its existing card link and unchanged Diesel transaction snapshot.

**Implementation Note**: After automated checks pass, pause for human confirmation of the composed browser workflow before closing the change.

## Testing Strategy

### Unit Tests:

- Application tests cover canonical plate identity, length and blank validation, supported single fuel types, active uniqueness, archive/restore rules, restore collisions, and future assignment eligibility.
- Persistence tests cover the filtered unique index, fuel-type mapping, append-only audit shape, and preservation of seeded card/transaction relationships.
- Angular tests cover vehicle client state, form and validation behavior, active/archived views, permissions, and EN/PL resources.

### Integration Tests:

- `WebApplicationFactory<Program>` tests cover authenticated vehicle reads, ReadWrite mutations, ReadOnly/anonymous denial, CSRF failures, successful commands, normalized search, conflict/error statuses, and audit writes.
- `web/e2e/fleet-vehicles.spec.ts` exercises the visible workflow against the AppHost-composed web endpoint and SQL-backed API using the isolated disposable test database; setup and teardown affect only that database.
- Migration verification applies the new migration to the seeded SQL Server database and checks existing references and snapshots.

### Manual Testing Steps:

1. Sign in as the seeded ReadWrite user, create a vehicle using mixed case and whitespace, and verify normalized search returns it consistently.
2. Update the vehicle and fuel type, archive it, find it through the archived filter, and restore it.
3. Create an active vehicle with a previously archived plate and confirm the archived duplicate cannot be restored until the conflict is resolved.
4. Sign in as ReadOnly and verify vehicle search/list works while every mutation control is absent and direct API writes are forbidden.
5. Archive the seeded Volvo and verify protected card and transaction reads still show its assigned card and historical Diesel transaction snapshot unchanged.

## Performance Considerations

The target dataset and request rate are small. Use bounded, parameterized EF queries and the normalized-plate index; do not add caching or pagination in this slice. Keep plate normalization consistent between writes and search so the database predicate can use the stored normalized value.

## Migration Notes

Add a forward EF migration for the required vehicle fuel type and audit table, and replace the existing unfiltered unique index with a SQL Server filtered unique index for active vehicles. Backfill the two current seeds as Volvo FH → Diesel and Toyota Corolla → 95; the latter is an explicit demo assumption. Preserve card foreign keys and transaction plate snapshots. Validate the migration against a clean Aspire SQL Server database before relying on the browser suite.

## References

- `context/foundation/roadmap.md` — S-02 scope, prerequisite, and exit criteria
- `context/foundation/prd.md` — FR-002 and US-01
- `context/foundation/requirements.md` — vehicle behavior and acceptance criteria
- `context/foundation/domain-model.md` — vehicle, card, and transaction invariants
- `context/foundation/architecture.md` — layering, EF persistence, and Aspire boundary
- `context/foundation/security.md` — authorization, CSRF, audit, and input validation
- `context/foundation/decisions.md` — relational persistence and immutable transaction snapshots
- `context/changes/seeded-sign-in-and-access/plan.md` — established auth policy, CSRF intent, and no-connection fallback
- `src/FleetCards.Domain/FleetCardsModel.cs`, `src/FleetCards.Contracts/Models.cs`, and `src/FleetCards.Infrastructure/FleetCardsDbContext.cs` — current vehicle schema and seed
- `src/FleetCards.Infrastructure/EfFleetCardsReadService.cs` and `src/FleetCards.Api/Program.cs` — current vehicle search and API route
- `src/FleetCards.Api/AuthenticationSupport.cs` — CSRF token contract
- `src/FleetCards.AppHost/AppHost.cs`, `web/src/app/shell.component.html`, and `web/package.json` — composed runtime, vehicle placeholder, and frontend test tooling
- `tests/FleetCards.Api.Tests/PlatformTests.cs` and `web/src/app/shell.component.spec.ts` — existing API and Angular test patterns

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append ` — <commit sha>` when a step lands. Do not rename step titles.

### Phase 1: Vehicle Contracts and Persistence

#### Automated

- [x] 1.1 The SQL Server migration adds one required fuel type per car, append-only vehicle audit storage, and active-only normalized-plate uniqueness, and applies successfully to the seeded database. — e6cb733
- [x] 1.2 Migration verification confirms the seeded Volvo FH is Diesel and Toyota Corolla is 95, and the existing Diesel transaction and card-to-vehicle reference remain unchanged. — e6cb733

#### Manual

- [x] 1.3 Inspect the migrated database to confirm both seeded cars remain active and the seeded card assignment and transaction plate snapshot are preserved. — e6cb733

### Phase 2: Vehicle Commands and API Enforcement

#### Automated

- [x] 2.1 Application tests cover plate normalization (case and all whitespace), blank/length validation, optional descriptive fields, single fuel-type validation, duplicate active plates, and restore conflicts. — 590689d
- [x] 2.2 API integration tests prove ReadWrite-only car mutations, 401 anonymous access, 403 ReadOnly/invalid-CSRF access, route response contracts, CRUD/archive/restore, normalized search, and 400/404/409 error responses. — 590689d
- [x] 2.3 Tests verify each successful vehicle mutation writes one append-only audit event and archiving preserves an existing card link while making the vehicle ineligible for future assignment. — 590689d

#### Manual

- [x] 2.4 Exercise the API as ReadWrite and ReadOnly users; confirm mutation status codes, restore-conflict behavior, archived search/filter results, audit details, and unchanged transaction snapshots. — 590689d

### Phase 3: Bilingual Vehicle Management UI

#### Automated

- [x] 3.1 Angular tests cover active-first listing, archived filtering, normalized search, form validation, ReadOnly rendering, mutation flows, and EN/PL vehicle labels. — 1a97cb0
- [x] 3.2 `npm test` and `npm run build` pass from `web/`. — 1a97cb0

#### Manual

- [x] 3.3 In a browser, verify ReadWrite users can create, edit, archive, and restore vehicles, while ReadOnly users can search and view without mutation controls; repeat key states in English and Polish. — 1a97cb0

### Phase 4: Browser Acceptance and Handoff

#### Automated

- [x] 4.1 The Playwright acceptance suite passes against the Aspire-composed API, Angular app, and SQL Server for ReadWrite and ReadOnly vehicle workflows. — 057d74c
- [x] 4.2 `dotnet test FleetCards.sln`, `npm test`, `npm run build`, and `npm run test:e2e` pass without regressions introduced by this change. — 057d74c

#### Manual

- [x] 4.3 Start Aspire from a clean seeded database and confirm created/edited vehicle data persists across API reads and the browser workflow completes in both languages. — 057d74c
- [x] 4.4 Archive the seeded Volvo through the browser and confirm protected card and transaction reads still show its existing card link and unchanged Diesel transaction snapshot. — 057d74c