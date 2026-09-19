# Domain model and invariants

## Aggregate boundaries

### Identity and tenancy

- `User`: identity, normalized email, status, and global role.
- `Client`: legal company and approval lifecycle.
- `ClientMembership`: user-to-client relationship and `ReadWrite`/`ReadOnly` permission.
- `RegistrationReview`: approval decision, reviewer, reason, and timestamps.

### Fleet

- `FleetCar`: normalized plate and client ownership.
- `CardCarAssignment`: effective-dated card/car relationship.

### Cards

- `FleetCard`: client ownership, provider token reference, last four, lifecycle state, and replacement link.
- `CardIssuanceBatch`: batch request, status, idempotency key, and timestamps.
- `CardIssuanceItem`: requested card metadata, provider correlation, outcome, and retry count.

### Pricing

- `FleetCardProgram`: program identity and lifecycle.
- `ProgramVersion`: effective-dated pricing version and VAT mode.
- `FuelPriceRule`: fuel type and pricing model/value.
- `ProgramClientAccess`: allow/disallow relationship.
- `CardProgramAssignment`: effective-dated card/program relationship.

### Cross-cutting

- `AuditEvent`: append-only security and business event.
- `OutboxMessage`: durable notification/provider integration message.

## Important invariants

- Every tenant-scoped query is constrained by the authenticated client ID unless the actor is an operational manager.
- Client approval is required before card issuance, program assignment, or fleet mutation.
- NIP and normalized plate uniqueness are enforced by database constraints, not only application checks.
- Card provider operations are idempotent using a local idempotency key.
- Provider webhook handling is authenticated, replay-safe, and state-transition validated.
- Audit events are written in the same transaction as the business state change where possible.
- Soft-deleted entities are excluded from normal queries but remain available to authorized audit/support views.
- A historical assignment references immutable IDs and timestamps; it is never rewritten to alter history.

## Suggested status transitions

### Client

`Pending -> Approved | Rejected`; `Approved -> Archived`; `Archived -> Approved` only by an operational manager.

### Card

`Pending -> Active | Failed`; `Active -> Suspended | Revoked | Expired | Replaced`; `Suspended -> Active | Revoked`; `Active -> Replaced` only through a replacement operation.

### Issuance batch

`Queued -> Processing -> Completed | CompletedWithErrors | Failed`; individual items are independently retryable while their provider request is not terminal.

