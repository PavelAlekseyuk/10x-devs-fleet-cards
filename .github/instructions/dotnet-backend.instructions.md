---
applyTo: "src/**/*.cs,tests/**/*.cs"
description: "ASP.NET Core / EF Core backend conventions for FleetCards"
---

# .NET backend conventions

- Respect the layering: `Domain` has zero project references; `Application` depends only on `Domain`/`Contracts`; `Infrastructure` depends on `Application`/`Domain`; `Api` wires everything together. Never add a reference that points the wrong direction (e.g. `Domain` → `Infrastructure`).
- Use Minimal APIs (`app.MapGet/MapPost/...`) grouped by module (`/api/cars`, `/api/cards`, etc.) — do not introduce MVC controllers.
- DTOs live in `FleetCards.Contracts` as records; domain entities/invariants live in `FleetCards.Domain`. Don't leak domain entities across the API boundary — map to Contracts DTOs.
- Card issuance, assignment changes, and generated transactions must be wrapped in a database transaction once EF Core persistence lands (see "Reliability and observability" in [context/foundation/architecture.md](../context/foundation/architecture.md)).
- Never log or return full PAN, CVV, or PIN — only masked PAN/provider token via `ICardProvider`. Structured logs must use correlation IDs and redact card/provider data (see [context/foundation/security.md](../context/foundation/security.md)).
- Transactions and program-assignment history are immutable/append-only: model corrections as new rows, never in-place edits.
- New projects/tests should follow the existing pattern: xUnit test project per layer (`*.Tests`), one `WebApplicationFactory<Program>` fixture for API-level tests (see [tests/FleetCards.Api.Tests/PlatformTests.cs](../tests/FleetCards.Api.Tests/PlatformTests.cs)).
- `FleetCards.Architecture.Tests` is meant to enforce the layering above with NetArchTest — extend it rather than relying on convention alone as real projects/references get added.
