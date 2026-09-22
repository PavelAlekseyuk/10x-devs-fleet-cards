# Seeded Sign-In and Access Enforcement Implementation Plan

## Overview

Deliver the first authenticated boundary for the FleetCards client portal. Seeded users will sign in through the ASP.NET API, receive an expiring cookie session protected by a CSRF token, and access only the client views permitted by their role. The Angular shell will replace preview identity data with authenticated state, provide bilingual login and navigation, and protect its routes as a user-experience layer while the API remains authoritative.

## Current State Analysis

- `FleetUser` stores only username and permission; the EF seed currently contains one `ReadWrite` user and no password hash (`src/FleetCards.Domain/FleetCardsModel.cs`, `src/FleetCards.Infrastructure/FleetCardsDbContext.cs`).
- `/api/auth/me` returns the seeded user without authentication, and all client data endpoints are publicly callable (`src/FleetCards.Api/Program.cs`).
- The application and EF read services hardcode the current user rather than resolving an authenticated principal (`src/FleetCards.Application/FleetCardsApplication.cs`, `src/FleetCards.Infrastructure/EfFleetCardsReadService.cs`).
- Angular has an empty route table, no HTTP client provider, no auth service or guard, and preview identity/navigation data in the root component (`web/src/app/app.routes.ts`, `web/src/app/app.config.ts`, `web/src/app/app.ts`, `web/src/app/app.html`).
- Existing API integration tests use `WebApplicationFactory<Program>`; frontend specs use Vitest globals and jsdom (`tests/FleetCards.Api.Tests/PlatformTests.cs`, `web/tsconfig.spec.json`).

## Desired End State

Two deterministic local users can log in: `anna.nowak` with `ReadWrite` permission and `piotr.wisniewski` with `ReadOnly` permission. Passwords are stored only as hashes in seeded persistence, login failures are generic and lightly throttled, successful authentication creates an eight-hour sliding SameSite cookie session, and logout clears it. A CSRF cookie/header pair protects the auth commands and future mutations.

Authenticated users can view client details, vehicles, cards, and transactions. ReadWrite users can see the full shell, while ReadOnly users cannot access or navigate to program assignment/benefit views and cannot execute commands. English is the default; Polish translations are application resources and the selected language persists locally. API integration and focused Angular tests prove the access contract.

### Key Discoveries:

- Existing user data and migrations are EF-backed, but the current schema has no password hash or authentication infrastructure.
- The API uses Minimal APIs and the Angular app uses standalone components, so the change must extend those boundaries rather than introduce controllers or NgModules.
- The PRD explicitly excludes program assignment and benefit calculations for ReadOnly users; that rule controls the ambiguous roadmap wording.
- Security guidance requires API-side authorization, generic login errors, secure expiring sessions, and redacted structured logs. Durable auth audit rows are intentionally deferred by the agreed S-01 scope.

## What We're NOT Doing

- Real identity-provider integration, self-registration, password reset, or production password lifecycle.
- Persisted audit rows, production-grade distributed rate limiting, or multi-client identity isolation.
- Vehicle/card/transaction mutation workflows; S-01 only establishes their authenticated route and permission boundary.
- Program assignment or benefit-calculation implementation; ReadOnly users are denied that area and the existing program placeholder remains future work.
- Aspire browser end-to-end automation; composed-runtime hardening remains S-05, while this change uses API integration and focused frontend tests.

## Implementation Approach

Keep the existing Domain/Application/Infrastructure/Api layering and Minimal API style. Add a small application authentication contract for credential validation and current-user resolution, persist password hashes and the second seeded user through EF migration/seed data, and configure ASP.NET cookie authentication with explicit SameSite, expiry, logout, and CSRF behavior. Protect every client data route with API authorization and a role/permission policy for the program area. In Angular, provide `HttpClient`, centralize auth/session state, attach the CSRF header, guard routes, and render the existing shell from current-user and client responses with English/Polish resources and local language preference.

## Critical Implementation Details

### State sequencing

The API must issue or refresh the CSRF token before Angular submits cookie-authenticated commands, and the Angular guard must treat an `/api/auth/me` failure as signed out before redirecting to `/login`. Route guards are UX only; every protected API endpoint must independently enforce authentication and permission.

### Debug & observability

Authentication success, failure, logout, throttling, and authorization denial should use structured allowlisted fields with correlation IDs and no password, cookie, or sensitive card data. Tests should assert generic failure responses and status codes rather than logging or comparing credential material.

## Phase 1: Persisted Seeded Identity and Auth Contracts

### Overview

Extend the user model and persistence contract for hashed credentials and two deterministic seeded accounts, then establish the shared request/response and authentication service contracts that later API and Angular phases consume.

### Changes Required:

#### 1. User schema and deterministic seed data

**File**: `src/FleetCards.Domain/FleetCardsModel.cs`, `src/FleetCards.Infrastructure/FleetCardsDbContext.cs`, `src/FleetCards.Infrastructure/Migrations/`

**Intent**: Store only password hashes for the seeded identities and add the agreed ReadOnly account without changing the existing client data model.

**Contract**: `FleetUser` exposes a required password-hash field; EF maps it with a bounded required column; migration and seed data contain `anna.nowak`/`ReadWrite` and `piotr.wisniewski`/`ReadOnly` with deterministic hashes and preserve the unique username constraint.

#### 2. Authentication contracts and credential validation

**File**: `src/FleetCards.Contracts/Models.cs`, `src/FleetCards.Application/FleetCardsApplication.cs`, `src/FleetCards.Infrastructure/EfFleetCardsReadService.cs`, `src/FleetCards.Infrastructure/FleetCardsInfrastructure.cs`

**Intent**: Replace hardcoded current-user behavior with a reusable credential-validation and authenticated-user lookup boundary that works for the EF runtime and the existing no-connection-string local fallback.

**Contract**: Login input never crosses the response boundary; validation returns either a safe current-user record or a generic failure; permission values remain the existing `ReadWrite`/`ReadOnly` contract.

### Success Criteria:

#### Automated Verification:

- The new EF migration applies and the seeded user rows contain hashes rather than plaintext passwords.
- Application and Infrastructure tests validate both seeded users and reject invalid credentials without exposing account-existence detail.
- `dotnet build FleetCards.sln` passes after the model, contracts, and service changes.

#### Manual Verification:

- Inspect the local database/model snapshot and confirm no plaintext demo password is stored or logged.
- Confirm both documented seeded identities are available to the later login flow with the expected permissions.

## Phase 2: Protected API Access and Auth Observability

### Overview

Wire ASP.NET authentication and authorization, implement login/logout/current-user and CSRF behavior, protect the existing API modules, and add the agreed lightweight abuse and logging controls.

### Changes Required:

#### 1. Cookie session and CSRF configuration

**File**: `src/FleetCards.Api/Program.cs` and supporting API/Application files as needed

**Intent**: Establish an eight-hour sliding, SameSite cookie session suitable for the same-hosted SPA and a double-submit CSRF contract for authentication commands and future mutation endpoints.

**Contract**: `POST /api/auth/login`, `POST /api/auth/logout`, `GET /api/auth/me`, and the CSRF bootstrap/validation path use explicit status codes, secure cookie settings, generic credential errors, and no credential-bearing URLs.

#### 2. API authorization and permission policy

**File**: `src/FleetCards.Api/Program.cs`, `src/FleetCards.Application/`, `src/FleetCards.Infrastructure/`

**Intent**: Resolve the authenticated user from claims and enforce authentication on client, car, card, transaction, and program routes, with the PRD-defined ReadOnly restriction on program assignment/benefit views and all commands.

**Contract**: Unauthenticated client-data requests receive `401`; authenticated users lacking a required permission receive `403`; health/platform endpoints remain available as configured; `/api/auth/me` reflects the session principal rather than a hardcoded user.

#### 3. Throttling and structured authentication logs

**File**: `src/FleetCards.Api/Program.cs` and ServiceDefaults/logging integration points

**Intent**: Add deterministic lightweight login throttling and structured logs for success, failure, logout, throttling, and authorization denial using allowlisted fields and correlation IDs.

**Contract**: Invalid credentials produce one generic response regardless of username existence; throttled attempts are bounded and testable; logs never contain passwords, session values, full PAN, CVV, or PIN. Durable audit rows remain out of scope for this change.

### Success Criteria:

#### Automated Verification:

- API integration tests cover valid login for both roles, generic invalid-login behavior, logout, current-user resolution, session expiry configuration, and CSRF rejection/acceptance.
- API integration tests prove anonymous access is denied, ReadWrite access is allowed, ReadOnly commands are denied, and ReadOnly program access is denied.
- API tests verify health/platform endpoints retain their intended availability and authentication logs use only allowlisted fields.

#### Manual Verification:

- Use an HTTP client or browser to sign in, refresh, call a protected endpoint, log out, and confirm the same endpoint is denied afterward.
- Repeat failed logins and confirm the response remains generic while throttling eventually applies without revealing account existence.

## Phase 3: Angular Login and Guarded Bilingual Shell

### Overview

Turn the preview Angular shell into a real authenticated client: add login and protected routes, load current user/client data, enforce permission-aware navigation, support local English/Polish resources, and provide logout/session-expiry recovery.

### Changes Required:

#### 1. Auth state, HTTP, interceptor, and guards

**File**: `web/src/app/app.config.ts`, `web/src/app/app.routes.ts`, new `web/src/app/auth.service.ts`, guard/interceptor/supporting files

**Intent**: Centralize browser authentication state and connect the SPA to the cookie/CSRF API contract while keeping route guards as a UX layer.

**Contract**: `/login` is public; `/overview`, `/cars`, `/cards`, `/transactions`, and `/programs` are protected; unauthenticated navigation redirects to `/login`; ReadOnly navigation excludes `/programs`; logout clears local state and redirects to login; HTTP failures from protected calls recover to the signed-out state.

#### 2. Login and authenticated shell rendering

**File**: `web/src/app/app.ts`, `web/src/app/app.html`, `web/src/app/app.scss`, new login/client/authenticated view files

**Intent**: Replace hardcoded Anna, client, counts, and permission labels with authenticated API-backed state while preserving the existing visual shell and providing generic inline login errors.

**Contract**: Login accepts username/password, never places credentials in URLs, displays one generic failure message, binds current-user permission and client details, exposes logout, and hides unavailable actions/routes for ReadOnly users.

#### 3. English/Polish resources and preference

**File**: `web/src/app/` translation resources/services and affected templates

**Intent**: Make English the default baseline and allow users to switch all S-01 shell/login/navigation text to Polish, persisting the choice locally.

**Contract**: The language toggle is functional, survives reload in the same browser, and does not require a user-profile API or schema field.

### Success Criteria:

#### Automated Verification:

- Angular tests cover login state transitions, generic errors, logout, guard redirects, CSRF/header wiring, local language persistence, and ReadOnly route/navigation filtering.
- Angular component tests verify authenticated user/client bindings replace the preview identity and unauthorized program controls are absent.
- `npm run build` and `npm test` pass in `web/`.

#### Manual Verification:

- In a browser, log in as each seeded user, refresh the page, switch EN/PL, navigate through the permitted shell routes, and log out.
- Confirm the ReadOnly user sees client, vehicle, card, and transaction views but cannot reach the program view or command controls.
- Confirm expired or invalid sessions return to login with a recoverable non-sensitive notice.

## Phase 4: Focused Verification and Operational Documentation

### Overview

Consolidate the API and Angular checks into a repeatable S-01 verification path and document the local credentials, startup assumptions, session behavior, permission matrix, and known deferred audit limitation.

### Changes Required:

#### 1. Test and documentation contract

**File**: `tests/FleetCards.Api.Tests/`, `web/src/app/*.spec.ts`, `docs/` or the existing local runbook location

**Intent**: Make the seeded access flow repeatable for contributors and future slices without introducing Aspire browser automation into this change.

**Contract**: Documentation names the two local accounts, how to start/reset the runtime, the cookie/CSRF/session expectations, the ReadWrite/ReadOnly matrix, and that auth outcomes are currently structured-log evidence rather than persisted audit rows.

### Success Criteria:

#### Automated Verification:

- `dotnet test` passes for the API and application test projects with the new authorization cases.
- `npm test` and `npm run build` pass from `web/`.
- A clean solution build and the documented focused test commands reproduce the same results without warnings introduced by this change.

#### Manual Verification:

- Follow the documented seeded sign-in flow from a clean local runtime and verify the role matrix end to end at the HTTP/UI boundary.
- Review logs during successful, failed, throttled, logout, and denied requests and confirm sensitive values are absent.

## Testing Strategy

### Unit Tests:

- Credential validation for both seeded users, invalid credentials, generic errors, password-hash handling, permission mapping, and throttling decisions.
- Angular auth state, guard decisions, local language persistence, generic error rendering, and permission-aware navigation.

### Integration Tests:

- `WebApplicationFactory<Program>` tests for cookie login, current-user resolution, logout, CSRF, protected endpoints, `401`/`403` behavior, and preserved health access.
- EF/migration verification for the password-hash column and both deterministic user seeds.

### Manual Testing Steps:

1. Start the documented local API/SPA runtime and sign in with each seeded account.
2. Refresh, switch language, inspect client details, and exercise each permitted route.
3. Attempt anonymous access, ReadOnly program access, ReadOnly commands, invalid credentials, logout, and session-expiry recovery.
4. Inspect structured logs for redaction and correlation IDs.

## Performance Considerations

The MVP has low traffic and small seeded data, so no caching is required. Password verification and throttling should remain bounded per request; avoid loading unrelated client data during authentication, and keep protected read queries parameterized and scoped through the existing service boundaries.

## Migration Notes

Add one forward EF migration for the required password-hash column and the second seeded user. Existing local databases must apply the migration before login is tested; the deterministic seed must be repeatable without replacing user-entered production data, and seeded credentials remain local/MVP configuration only.

## References

- `context/foundation/roadmap.md` — S-01 scope and exit criteria
- `context/foundation/prd.md` — FR-001 and the two-role access-control contract
- `context/foundation/security.md` — session, authorization, logging, and abuse-control constraints
- `context/foundation/architecture.md` — Minimal API, layering, and SPA boundaries
- `src/FleetCards.Api/Program.cs` — current public endpoint wiring
- `web/src/app/app.ts` and `web/src/app/app.routes.ts` — current preview shell and empty route table
- `tests/FleetCards.Api.Tests/PlatformTests.cs` — existing API integration-test seam

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append ` — <commit sha>` when a step lands. Do not rename step titles.

### Phase 1: Persisted Seeded Identity and Auth Contracts

#### Automated

- [ ] 1.1 The new EF migration applies and the seeded user rows contain hashes rather than plaintext passwords.
- [ ] 1.2 Application and Infrastructure tests validate both seeded users and reject invalid credentials without exposing account-existence detail.
- [ ] 1.3 `dotnet build FleetCards.sln` passes after the model, contracts, and service changes.

#### Manual

- [ ] 1.4 Inspect the local database/model snapshot and confirm no plaintext demo password is stored or logged.
- [ ] 1.5 Confirm both documented seeded identities are available to the later login flow with the expected permissions.

### Phase 2: Protected API Access and Auth Observability

#### Automated

- [ ] 2.1 API integration tests cover valid login for both roles, generic invalid-login behavior, logout, current-user resolution, session expiry configuration, and CSRF rejection/acceptance.
- [ ] 2.2 API integration tests prove anonymous access is denied, ReadWrite access is allowed, ReadOnly commands are denied, and ReadOnly program access is denied.
- [ ] 2.3 API tests verify health/platform endpoints retain their intended availability and authentication logs use only allowlisted fields.

#### Manual

- [ ] 2.4 Use an HTTP client or browser to sign in, refresh, call a protected endpoint, log out, and confirm the same endpoint is denied afterward.
- [ ] 2.5 Repeat failed logins and confirm the response remains generic while throttling eventually applies without revealing account existence.

### Phase 3: Angular Login and Guarded Bilingual Shell

#### Automated

- [ ] 3.1 Angular tests cover login state transitions, generic errors, logout, guard redirects, CSRF/header wiring, local language persistence, and ReadOnly route/navigation filtering.
- [ ] 3.2 Angular component tests verify authenticated user/client bindings replace the preview identity and unauthorized program controls are absent.
- [ ] 3.3 `npm run build` and `npm test` pass in `web/`.

#### Manual

- [ ] 3.4 In a browser, log in as each seeded user, refresh the page, switch EN/PL, navigate through the permitted shell routes, and log out.
- [ ] 3.5 Confirm the ReadOnly user sees client, vehicle, card, and transaction views but cannot reach the program view or command controls.
- [ ] 3.6 Confirm expired or invalid sessions return to login with a recoverable non-sensitive notice.

### Phase 4: Focused Verification and Operational Documentation

#### Automated

- [ ] 4.1 `dotnet test` passes for the API and application test projects with the new authorization cases.
- [ ] 4.2 `npm test` and `npm run build` pass from `web/`.
- [ ] 4.3 A clean solution build and the documented focused test commands reproduce the same results without warnings introduced by this change.

#### Manual

- [ ] 4.4 Follow the documented seeded sign-in flow from a clean local runtime and verify the role matrix end to end at the HTTP/UI boundary.
- [ ] 4.5 Review logs during successful, failed, throttled, logout, and denied requests and confirm sensitive values are absent.