# Manage Fleet Vehicles — Plan Brief

> Full plan: `context/changes/manage-fleet-vehicles/plan.md`

## What & Why

This change gives client representatives a real way to manage fleet vehicles: create, view, edit, search, archive, and restore them with a single allowed fuel type. It completes the vehicle step in the first operational card flow while preserving assignments and transaction history and enforcing the existing two-role access boundary.

## Starting Point

The EF model already stores registration plates, normalized plates, optional make/model, and archive state, but has no fuel type or mutation/audit service. The API only reads cars and searches the raw plate; the Angular cars route is a placeholder over an existing authenticated, bilingual shell.

## Desired End State

ReadWrite users can complete vehicle management in the portal, while ReadOnly users can search and view without mutation controls. Plate identity ignores case and whitespace, only active vehicles must have unique normalized plates, archive preserves existing card links, restore conflicts are explicit, and successful changes produce append-only audit rows.

## Key Decisions Made

| Decision | Choice | Why |
| --- | --- | --- |
| Fuel selection | Exactly one of the six specified fuel types per vehicle | Matches the user's selected rule and makes transaction generation deterministic. |
| Seed mapping | Volvo FH → Diesel; Toyota Corolla → 95 | Diesel matches the Volvo's existing transaction; 95 is an explicit demo assumption for the Toyota. |
| Plate identity | Trim and uppercase; ignore all whitespace for normalized identity/search; require nonblank and max 20; other characters remain significant | Implements the stated case/whitespace behavior without imposing a strict Polish-format rule. |
| Vehicle API | GET `/api/cars` with search/archive query; POST create, PUT update, POST archive/restore | Defines one backend/frontend contract and explicit status handling. |
| Active uniqueness | Unique normalized plate among active vehicles; archived plate may be reused | Matches the domain invariant while allowing vehicle replacement. |
| Restore collision | Reject restore and leave the vehicle archived until the collision is resolved | Keeps active plate uniqueness unambiguous. |
| Archive behavior | Retain any existing card assignment; do not hard-delete history; disallow future assignments to archived vehicles | Preserves current relationships and avoids silently detaching cards. |
| Archived list | Active vehicles by default with an archived filter | Keeps the daily list focused while making restore possible. |
| Audit scope | Persist accepted vehicle mutations atomically where practical; rejected/unauthorized attempts remain in structured logs | Keeps durable audit rows tied to successful state changes. |
| Browser verification | Add focused Playwright acceptance for S-02; leave full Aspire operational-flow E2E to S-05 | Satisfies this slice's browser criterion without duplicating the later full-flow suite. |
| Browser test data | Use a dedicated disposable SQL database with deterministic reset; never reset the normal persistent developer database | Makes repeated runs safe and isolated. |

## Scope

**In scope:** vehicle schema/contracts, EF migration and active-only uniqueness, read/write API, normalization and validation, archive/restore, assignment eligibility rule, successful-mutation audit events, bilingual Angular vehicle workflow, API/unit/browser tests.

**Out of scope:** card issuance or assignment workflows, transaction generation/reporting, hard deletion, strict Polish plate-format validation, durable audit rows for rejected commands, and full operational-flow Aspire E2E.

## Architecture / Approach

Use a focused application vehicle service with EF-backed persistence in the Aspire runtime and the existing seeded fallback mode for no-connection operation. Minimal API reads remain available to authenticated users; mutations require ReadWrite permission and the existing CSRF cookie/header pair. A named Angular vehicle service/component uses the explicit GET/POST/PUT route contract; Playwright exercises the composed web endpoint and SQL-backed API using an isolated disposable test database.

## Phases at a Glance

| Phase | What it delivers | Key risk |
| --- | --- | --- |
| 1. Vehicle Contracts and Persistence | Fuel type, audit table, seed backfill, active-only plate index | Existing rows need an explicit fuel mapping. |
| 2. Vehicle Commands and API Enforcement | Normalized CRUD/archive/restore, authorization, CSRF, audit | Archive/restore must preserve history and handle uniqueness races. |
| 3. Bilingual Vehicle Management UI | Search, active/archive views, forms, role-aware commands | Shell placeholder must become API-backed without hiding ReadOnly viewing. |
| 4. Browser Acceptance and Handoff | Playwright acceptance and repeatable local verification | Browser runner requires browser setup and isolated SQL test data. |

**Prerequisites:** Complete S-01 seeded sign-in and access enforcement; the roadmap currently lists it as in progress.
**Estimated effort:** Approximately 3-5 focused sessions across four phases, depending on migration and Playwright setup.

## Open Risks & Assumptions

- The Toyota Corolla's `95` fuel type is a demo seed assumption; it has no existing transaction from which to infer fuel compatibility.
- Playwright is new to the repository and requires browser installation, an available Aspire/SQL Server runtime, and a dedicated disposable database per test run.
- The existing card assignment remains attached after archive; S-03 must reject new assignments to archived vehicle IDs.
- The no-connection fallback is seeded and non-durable; SQL Server through Aspire remains the persistence acceptance path.

## Success Criteria (Summary)

- The two seeded vehicles migrate with the agreed fuel types, active-only plate uniqueness, preserved card/transaction references, and audit persistence.
- ReadWrite and ReadOnly users receive the correct API and UI behavior for normalized search and all vehicle lifecycle operations.
- Unit, API, Angular, and focused Playwright checks pass against the repeatable local runtime.