# Requirements and acceptance criteria

## 1. Actors and authorization

### Operational manager

An operational manager can:

- Review, approve, or reject pending company registrations.
- Create, view, update, archive, and restore client users.
- Manage client fleet cars.
- Issue, suspend, replace, revoke, and view cards for any approved client.
- Submit single-card or bulk issuance requests.
- Create and manage Fleet Card Programs and effective-dated versions.
- Allow or disallow a program for selected clients.
- View audit entries and operational batch results.

### Client representative

A client representative can:

- Register one company and an initial representative account.
- View their company approval state and registration outcome.
- Add, update, deactivate, and list company users with read-write or read-only rights.
- Create, update, archive, restore, and list their cars.
- List cards belonging to their company and view non-sensitive card metadata.
- Request or view cards issued to their company, subject to the final issuance workflow.
- Assign their cards to cars and permitted programs.
- List permitted programs and their effective fuel prices.

### Read-only client user

A read-only user can view the company’s users, cars, cards, assignments, and permitted programs, but cannot mutate them or submit issuance/assignment changes.

## 2. Company registration and approval

1. A visitor submits a registration containing normalized Polish NIP, legal name, registered address, representative name, email, and telephone.
2. The system rejects a second active or pending registration for the same NIP.
3. The system creates a pending company and initial client representative.
4. The representative cannot administer cards or fleet data until approval.
5. An operational manager approves or rejects the registration with an audit entry and optional reason.
6. Approval activates the company and initial representative. Rejection preserves the record and reason for audit.
7. Approval and rejection notifications are sent without exposing credentials or card secrets.

## 3. Users

- Email addresses are unique within the identity system and normalized case-insensitively.
- A company may have multiple users.
- A representative can immediately create another user, choosing read-write or read-only rights.
- User deactivation is reversible and prevents login and API access.
- A user cannot elevate their own rights.
- An operational manager can correct or deactivate any client user.

## 4. Cars

- A car has a normalized registration plate, make/model (optional), and optional descriptive metadata.
- Plate normalization removes insignificant whitespace and normalizes case according to Polish registration rules.
- A normalized plate can belong to only one active client fleet.
- Archiving a car does not remove card assignment history.
- An active card may be assigned to at most one active car at a time.

## 5. Cards and issuance

- The backend never accepts, persists, logs, or returns a full PAN or CVV.
- Issuance creates a local pending card and an idempotent provider request.
- Provider callbacks or polling transition the card to active, failed, or retryable state.
- Every batch has a durable status and per-item result, including provider correlation ID and safe error information.
- Retrying a failed item must be idempotent and must not create duplicate cards.
- Cards support pending, active, suspended, revoked, expired, and replaced states.
- Revoke is terminal for that card; replacement creates a new card and preserves a link to the replaced card.
- Only authorized operational managers can perform lifecycle actions that affect provider state.

## 6. Fleet Card Programs

- A program has a name, description, lifecycle state, and one or more effective-dated versions.
- A version contains fuel pricing rules for petrol, diesel, and LPG.
- Pricing rules support fixed price, station price plus/minus amount, and station price multiplied by a percentage adjustment.
- Each version has a start timestamp and optional end timestamp; overlapping active versions for the same program are forbidden.
- A client allowlist determines which programs a client may newly assign.
- Disallowing a program does not change existing card assignments.
- Archived programs cannot receive new assignments.
- Prices are displayed in PLN, with an explicit VAT-inclusive or VAT-exclusive flag per version.

## 7. Assignments

- Card-to-car and card-to-program assignments are independent.
- Assignments have effective timestamps and history.
- An assignment mutation checks tenant ownership, card state, car state, program allowlist, and effective pricing version.
- A client cannot assign a card belonging to another client or use a disallowed program.
- A card may have no car and/or no program while pending or during administration.

## 8. Search and UX

- All list screens are paginated and support stable sorting.
- Cars, cards, users, programs, and registrations support exact or prefix search appropriate to their fields.
- Sensitive card data is represented by provider token reference and last four digits only.
- Mutating operations return a correlation ID suitable for support investigation.
- Validation errors are field-specific and do not disclose whether unrelated accounts exist.

## 9. Acceptance criteria

The MVP is acceptable when:

1. A duplicate NIP cannot create a second active/pending company.
2. A rejected company cannot issue cards or mutate fleet data.
3. Tenant isolation tests prove that a client user cannot read or mutate another client’s data.
4. A read-only user cannot perform any write operation, including through direct API calls.
5. PAN/CVV values do not occur in application database records, logs, telemetry, or API responses.
6. Duplicate issuance retries produce one provider request and one local card.
7. Effective-dated pricing resolves deterministically at an assignment/read timestamp.
8. Program disallowing blocks new assignments while preserving existing assignments.
9. Every state-changing domain action has an immutable audit record with actor, timestamp, tenant, action, entity, outcome, and correlation ID.
10. Terraform can provision the documented single environment from a clean state.

