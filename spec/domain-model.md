# Domain model and invariants

## Aggregates and entities

- `User`: seeded username, password hash or mock credential reference, status, and permission (`ReadWrite` or `ReadOnly`).
- `Client`: legal name, Polish NIP, jurisdiction, and address.
- `FleetCar`: normalized plate, optional descriptive fields, client ownership, and archive state.
- `FleetCard`: provider token, masked PAN, lifecycle state, issue timestamp, archive timestamp, and optional assigned car.
- `CardCarAssignment`: card, car, start/end timestamps, and immutable history metadata.
- `FuelTransaction`: card reference, vehicle ID, vehicle registration-plate snapshot, timestamp, station, fuel type, quantity, station price, transaction discount, net/gross totals, VAT rate/amount, and status/version.
- `FleetCardProgram`: imported program identity, name, active period, and fuel-type percentage rules.
- `ClientProgramAssignment`: client, program, effective start/end timestamps, and immutable creation metadata.
- `AuditEvent`: append-only actor, action, entity, timestamp, outcome, and correlation ID.

## Enumerations

- Card state: `Active`, `Blocked`, `Stolen`, `Archived`.
- Transaction status: `Completed`, `Refunded`, `Cancelled`, `Recalculated`.
- Fuel type: `95`, `98`, `Diesel`, `95+`, `98+`, `Diesel+`.

## Invariants

- Only seeded authenticated users can access the client installation.
- Read-only users cannot execute mutation commands.
- Full PAN, CVV, and PIN are absent from all application-owned models.
- A normalized plate is unique among active cars in the client installation.
- An active card has at most one active car assignment; a car can have many cards.
- Archived cars cannot receive new assignments.
- Card state changes are unrestricted in the MVP; read-write users may select any supported state, including reactivating blocked, stolen, or archived cards.
- Blocking and stolen marking do not delete transaction history.
- Each issued card has its generated transaction set persisted atomically with issuance completion.
- Original transaction facts and assignment history are immutable.
- Client program assignments do not overlap and resolve to zero or one program at any timestamp.
- A new assignment starts when the previous assignment ends; no interval has two programs.
- Program calculations use only eligible transaction statuses and explicit VAT/rounding rules.

## State transitions

### Card

Any supported card state may transition to any other supported card state in the MVP. Issuance creates an active card, and read-write users may later select `Active`, `Blocked`, `Stolen`, or `Archived` without transition restrictions. State changes do not delete card or transaction history.

### Client program assignment

An assignment is created with a start timestamp and optional end timestamp. A future assignment may have both timestamps edited before it becomes effective. Scheduling a new assignment closes the current assignment at the new start timestamp. Removing a future assignment cancels that future interval; completed historical intervals remain unchanged.
