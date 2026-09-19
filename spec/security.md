# Security, privacy, audit, and operations

## Authentication and authorization

- Use ASP.NET Core Identity with a strong password policy, lockout, secure reset tokens, email verification, and session/token revocation.
- Use short-lived access tokens and rotating refresh tokens if the SPA uses token authentication; protect refresh tokens in secure, appropriately scoped cookies.
- MFA is optional in the MVP but the design must support enforcing it for operational managers later.
- Enforce role and tenant membership in API policies and application services.
- Default to deny; read-only permissions must be checked on every command endpoint.

## Card and secrets handling

- The application is out of scope for PAN storage and must not log or persist PAN/CVV.
- Store only processor token reference, last four digits, lifecycle data, and safe provider correlation identifiers.
- Key Vault stores database credentials, Identity signing material, email credentials, and card-provider secrets.
- Provider webhook signatures, timestamps, replay identifiers, and allowed state transitions are verified before processing.

## Audit and retention

Every authentication event and business mutation records:

- actor/user ID and effective role;
- client/tenant ID;
- action, entity type, entity ID, before/after summary where safe;
- result, reason/error category, correlation ID, IP/device metadata as permitted;
- UTC timestamp and source.

Audit records are append-only, access-controlled, tamper-evident, and retained for seven years. Raw sensitive payloads are excluded; audit values are redacted/allowlisted.

## Privacy and compliance

- Collect only the KYC and operational data required by the MVP.
- Provide a retention/deletion policy that distinguishes legal audit retention from user deactivation.
- Encrypt data in transit and at rest.
- Restrict production support access and record support access in audit logs.
- Do not put NIP, email, card references, or provider payloads in URLs or client-side analytics.

## Threat and abuse controls

- Rate-limit login, registration, password reset, and provider callback endpoints.
- Use generic authentication and registration errors where account enumeration is possible.
- Apply CSRF protection when cookie authentication is used.
- Validate all input server-side, including NIP, plates, price values, date ranges, and state transitions.
- Use parameterized queries/ORM protections and strict outbound HTTP allowlists.
- Add dependency, secret, container, and IaC scanning to CI.

## Operational requirements

- Structured logs use correlation IDs and safe field allowlists.
- Metrics cover registration outcomes, issuance latency/failures, queue depth, callback failures, login failures, and database health.
- Alerts cover failed migrations, repeated provider failures, queue age, authentication abuse, and database capacity.
- Backups are encrypted and regularly restore-tested.
- Define incident procedures for credential compromise, provider outage, accidental card revocation, and data exposure.

