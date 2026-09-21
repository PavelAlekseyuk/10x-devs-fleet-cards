# FleetCards — Agent Guide

Single-client fleet card management portal: ASP.NET Core 10 API + Angular 20 SPA, composed locally via .NET Aspire, targeting Azure App Service + Azure SQL in production. See [spec/README.md](spec/README.md) for product scope and [context/foundation/prd.md](context/foundation/prd.md) for the full PRD.

## Architecture

Layering (dependencies point one way): `Domain` (no deps) ← `Application` (Domain, Contracts) ← `Infrastructure` (Domain, Application) ← `Api` (all of the above + ServiceDefaults). `Contracts` holds DTO records only, no deps. `AppHost` composes SQL Server + Api + the Angular dev server for local dev and acceptance testing — it is not required at runtime in production. Full details: [spec/architecture.md](spec/architecture.md), locked decisions: [spec/decisions.md](spec/decisions.md), domain aggregates: [spec/domain-model.md](spec/domain-model.md), security/audit rules: [spec/security.md](spec/security.md).

Current state: only read endpoints exist, backed by a hardcoded `SeededFleetCardsReadService` (no EF Core DbContext wired yet). Auth is mocked with seeded users — there is no real identity provider. **Never store or log full PAN/CVV/PIN**; the card provider is mocked behind `ICardProvider`.

## Build & test

- Run everything locally (SQL Server + API + Angular): `dotnet run --project src/FleetCards.AppHost/FleetCards.AppHost.csproj`
- Build/test .NET only: `dotnet build` / `dotnet test` (xUnit; `FleetCards.Api.Tests`, `FleetCards.Application.Tests`, `FleetCards.Architecture.Tests` — the latter is a placeholder pending NetArchTest rules for the layering above)
- Angular, from `web/`: `npm start` (dev server :4200), `npm run build`, `npm test` (Karma/Jasmine)
- No root `.editorconfig`, `Directory.Build.props`, or `global.json` — .NET formatting/SDK version isn't pinned yet; `web/.editorconfig` + Prettier (100 width, single quotes) govern the Angular side.

## Conventions

- **API**: Minimal APIs only (no MVC controllers), grouped by module under `/api/*` (see module list in [spec/architecture.md](spec/architecture.md)). Nullable + implicit usings enabled everywhere.
- **Angular**: Standalone components only (no NgModules), strict TypeScript, component selectors prefixed `app-`. Routing (`app.routes.ts`) and HTTP client wiring are not yet set up — expect to add both when building real features.
- **Domain rules to respect**: transactions and program-assignment history are immutable/append-only (corrections are new versions, never edits); program assignments must be non-overlapping; every generated transaction snapshots its vehicle so later card reassignment doesn't rewrite history.

## Deployment / CI

No GitHub Actions workflows or Azure infrastructure exist yet — production deployment (Azure App Service + Azure SQL, GitHub Actions CI/CD) is documented follow-up work, not implemented (see "Deployment direction" in [spec/architecture.md](spec/architecture.md) and "Deferred after MVP" in [spec/decisions.md](spec/decisions.md)). When implementing CI/CD, check current free-tier availability by region/subscription before assuming Azure App Service F1 is usable.

Known gaps tracked in [context/foundation/health-check.md](context/foundation/health-check.md): 3 high-severity NuGet vulnerabilities (Microsoft.OpenApi, MessagePack via Aspire), no NuGet lockfile, outdated test SDK/coverlet packages.
