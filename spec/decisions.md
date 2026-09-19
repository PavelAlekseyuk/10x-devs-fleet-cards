# Open decisions and implementation slices

## Deferred decisions

These do not block the MVP specification but must be resolved before the related feature is implemented:

1. Select the production card processor and map its tokenization, issuance, callbacks, sandbox, and SLA.
2. Confirm the exact Azure subscription, region, free grants, PostgreSQL availability, and monthly budget exception for email.
3. Decide whether client representatives may request cards directly or only view cards issued by an operational manager.
4. Define the complete Polish NIP checksum/validation policy and acceptable address/registration evidence.
5. Choose cookie-based or token-based SPA authentication after threat-model review.
6. Define whether pricing adjustments may be negative, their decimal precision, and maximum bounds.
7. Confirm password/MFA policy and whether email verification is mandatory before first login.
8. Define operational-manager break-glass and support-access procedures.

## Suggested feature slices

1. Repository foundations, Terraform bootstrap, configuration, CI, health checks, and database migrations.
2. Identity, self-registration, email verification, password reset, roles, and tenant authorization.
3. Operational registration review and client lifecycle.
4. Client users and read-write/read-only membership permissions.
5. Fleet car CRUD, plate normalization, uniqueness, and assignment history.
6. Card processor adapter, virtual-card issuance state machine, callbacks, and idempotency.
7. Single-card lifecycle operations.
8. Asynchronous bulk issuance, retries, and per-item results.
9. Fleet Card Programs, effective-dated pricing, and client allowlists.
10. Card-to-car and card-to-program assignments.
11. Immutable audit trail, notifications, observability, and retention jobs.
12. Angular UX hardening, accessibility, integration tests, threat-model tests, and production-readiness review.

Each slice should include API authorization tests, tenant-isolation tests, audit assertions, and an Angular workflow before it is considered complete.
