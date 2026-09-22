# Seeded Sign-In and Access Enforcement — Plan Brief

> Full plan: `context/changes/seeded-sign-in-and-access/plan.md`

## What & Why

This change establishes the first real access boundary for the FleetCards client portal. Seeded company representatives need to sign in, see the client workspace, and receive different capabilities for full-access and read-only roles before the vehicle and card workflows can be built safely.

## Starting Point

The API currently exposes an always-authenticated `anna.nowak` through `/api/auth/me`, leaves all client data endpoints public, and has only one seeded user without a password hash. The Angular app has an empty route table and a polished but hardcoded preview shell with no HTTP, auth, guard, or translation infrastructure.

## Desired End State

`anna.nowak` can sign in as ReadWrite and `piotr.wisniewski` can sign in as ReadOnly using deterministic local credentials. Sessions use an eight-hour sliding SameSite cookie plus a double-submit CSRF token, and the API returns `401`/`403` consistently for anonymous or unauthorized access.

The Angular app has a public login route and protected shell routes for overview, vehicles, cards, and transactions, with the program route excluded for ReadOnly users. User/client data, logout, bilingual resources, and local language preference are real behaviors rather than preview data.

## Key Decisions Made

| Decision | Choice | Why (1 sentence) | Source |
| --- | --- | --- | --- |
| Session transport | SameSite HttpOnly cookie with CSRF | Keeps credentials out of JavaScript while giving the same-hosted SPA an explicit mutation defense. | Plan |
| Seeded identities | Hashed EF seed for Anna ReadWrite and Piotr ReadOnly | Reuses the existing relational user model and makes both role paths repeatable. | Plan |
| Session lifetime | Eight-hour sliding expiry | Fits a workday portal while limiting stale access. | Plan |
| ReadOnly scope | Client, vehicles, cards, transactions; no program area or commands | Follows the PRD's explicit access-control boundary. | PRD / Plan |
| Login abuse | Generic errors plus lightweight throttling | Avoids account enumeration and meets the local MVP security expectation. | Security / Plan |
| Auth evidence | Structured allowlisted logs, not persisted audit rows | Keeps S-01 bounded while making the deferred durability limitation explicit. | Plan |
| Language | English baseline, Polish resources, browser-local persistence | Extends the existing toggle without adding user-profile schema. | Decisions / Plan |
| Verification | API integration plus focused Angular tests | Covers each new contract without pulling S-05 Aspire browser hardening into this slice. | Roadmap / Plan |

## Scope

**In scope:**

- Password-hash persistence and deterministic two-user seed data.
- Login, logout, current-user, cookie session, CSRF, throttling, generic errors, API authorization, and structured auth logs.
- Angular login, guards, protected shell routes, permission-aware navigation, client/user binding, logout, and EN/PL resources.
- API integration tests, Angular tests, build/test verification, and local runbook updates.

**Out of scope:**

- Real identity provider, self-registration, password lifecycle, multi-client identity isolation, and production-grade rate limiting.
- Vehicle/card/transaction command implementation, program comparison implementation, durable auth audit rows, and Aspire browser E2E.

## Architecture / Approach

The API remains Minimal API based and preserves the existing project layering. EF-backed users provide hashed credentials; ASP.NET authentication creates the cookie principal; API handlers enforce authentication and permission; Angular uses an auth service, CSRF/interceptor support, and route guards only for navigation UX. The API is the source of truth for every protected request.

## Phases at a Glance

| Phase | What it delivers | Key risk |
| --- | --- | --- |
| 1. Persisted identity and contracts | Hashed users, migration, credential validation, shared auth contracts | Seed/runtime paths must stay deterministic. |
| 2. Protected API access | Cookie/CSRF auth, login/logout, policies, throttling, logs | Incorrect cookie or CSRF settings can break browser requests. |
| 3. Angular guarded shell | Login, routes, user/client state, translations, permission-aware UI | Preview state must not survive as an authorization source. |
| 4. Verification and documentation | Repeatable tests, local credentials, role matrix, known limitations | Local docs must match the actual composed runtime. |

**Prerequisites:** F-01 repeatable demo runtime and its database initialization/seed contract must be available before implementation begins.
**Estimated effort:** ~3-5 focused sessions across 4 phases, depending on the F-01 runtime state and migration/test setup.

## Open Risks & Assumptions

- This plan assumes F-01 has made the EF runtime seed repeatable; if not, S-01 cannot be verified reliably.
- Structured logs satisfy the agreed S-01 evidence choice, but durable auth audit records remain a known deferred security capability.
- Cookie and CSRF behavior should be verified again through Aspire when S-05 adds composed browser acceptance.
- Demo passwords are local/MVP credentials only and must not become production configuration.

## Success Criteria (Summary)

- Both seeded users can sign in and receive the correct role behavior; anonymous and unauthorized API calls are rejected.
- The Angular portal survives refresh, supports EN/PL switching, shows authenticated client/user data, and routes ReadOnly users away from programs and commands.
- Focused API/frontend tests and documented local verification reproduce the access contract without exposing credentials or sensitive data.