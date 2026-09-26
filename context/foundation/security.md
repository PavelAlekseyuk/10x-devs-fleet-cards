# Security, privacy, audit, and test posture

## Authentication and authorization

- Seeded credentials are for local/MVP use and must be clearly separated from production configuration.
- Store passwords as hashes if they are persisted; never log credentials.
- Enforce authentication and `ReadWrite`/`ReadOnly` permission in API handlers and application services.
- Use generic login errors to avoid account enumeration.
- Use secure session handling appropriate to the SPA deployment and expire sessions on logout.
- Treat Angular route guards as UX only; direct API calls must receive the same authorization checks.

## Card and transaction data

- The full PAN, CVV, and PIN must never be accepted by application endpoints, persisted, logged, or returned.
- The mock provider may generate a valid PAN internally for test realism, but only masked PAN and provider token cross the adapter boundary.
- Logs and telemetry use allowlisted fields and redact provider responses.
- Transaction data is business data; access is limited to authenticated users of the client installation.

## Integrity and audit

- Audit card issuance, assignment, blocking, stolen marking, archive, car mutations, program changes, and authentication outcomes.
- Audit records are append-only and contain actor, timestamp, action, entity, result, and correlation ID.
- Persist state changes and their audit records in one transaction where practical.
- Transaction facts and program assignment history are immutable; corrections are new versions or related records.

## Input and abuse controls

- Validate plates, date ranges, fuel types, decimal precision, VAT rates, percentages, and card state values server-side. Card state changes require `ReadWrite` authorization and audit records, but the MVP has no additional transition matrix: any defined state may be selected, including reactivation from `Archived`.
- Use parameterized EF Core queries and bounded pagination/filter inputs.
- Rate-limit login attempts in the deployed version; a lightweight local implementation is sufficient for the MVP.
- Do not place credentials, NIP, full card references, or transaction-sensitive data in URLs or analytics.

## Acceptance security tests

- Read-only users cannot mutate through any API route.
- Unauthenticated requests cannot read client, card, or transaction data.
- Full PAN/CVV values cannot be found in database snapshots, logs, telemetry, or API responses.
- Archived cards may receive state changes, including reactivation, but cannot be reassigned or used for fuel operations until reactivated; archived vehicles are ineligible assignment targets.
- Card state commands reject undefined state values; program commands reject invalid transitions and overlapping intervals.
- Aspire acceptance tests verify the same authorization and redaction behavior against the composed services.