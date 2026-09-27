# Requirements and acceptance criteria

## 1. Actors and authorization

The installation has one client and seeded users. A `ReadWrite` user may perform the mutations listed below. A `ReadOnly` user may view data but cannot mutate it. The API, not Angular guards, is the authorization boundary.

Both users can:

- Log in with a seeded username and password.
- View client details.
- Change the UI language between Polish and English.
- Search and view cars, cards, transactions, and program comparisons.
- View transaction totals for a selected date range.

A `ReadWrite` user can additionally:

- Create, edit, archive, and restore cars.
- Issue virtual cards through the mock provider.
- Assign cards to cars.
- Block cards, mark cards stolen, and archive cards.
- Schedule, replace, or remove the client's program assignment.

## 2. Authentication and client details

1. Seeded users can authenticate with username and password.
2. Invalid credentials produce a generic failure and do not reveal whether a username exists.
3. An authenticated user sees only the seeded client associated with the installation.
4. The client detail view includes Polish jurisdiction data such as legal name, NIP, and address.
5. The selected language persists for the current user/session and all MVP screens have Polish and English labels.

## 3. Cars

- A car has a normalized registration plate and optional make, model, and description.
- Plate search is case-insensitive and ignores insignificant whitespace.
- Read-write users can create, edit, archive, and restore cars.
- An archived car cannot be selected when issuing a card.
- Archiving a car does not change the immutable vehicle assignment of cards already issued to it.
- One car can have multiple issued cards; each issued card has exactly one assigned vehicle.

## 4. Cards and mocked provider

- Issuing a card calls a mocked provider adapter.
- The adapter returns only a masked PAN, a unique provider token, and safe metadata. Full PAN, CVV, and PIN never cross the application boundary.
- The provider mock generates a valid 16-digit Luhn PAN internally but exposes only its masked form.
- Issuing a card requires an active vehicle and permanently records that vehicle as the card's assignment.
- A card cannot be reassigned or unassigned after issuance.
- Card state history includes issuance as the initial `Active` event, followed by state changes in newest-first order; read-only users can inspect this history.
- A card can be blocked or marked stolen. These states are visible in the card list.
- A read-write user may choose any supported card state, including `Active`, `Blocked`, `Stolen`, and `Archived`. State changes are unrestricted in this MVP, and card/transaction history remains visible.
- There is no card return operation in this MVP.
- A one-time migration removes legacy cards with no assigned vehicle together with their transaction versions and corrections, assignment events, and card audit events before requiring the assignment. This is a narrow exception to append-only transaction and audit retention; assigned cards and their facts are preserved.

## 5. Transactions and VAT

Each generated transaction contains:

- UTC date/time;
- random station name;
- one of `95`, `98`, `Diesel`, `95+`, `98+`, `Diesel+`;
- quantity with three decimal places;
- station unit price and transaction discount/premium with two decimal places;
- VAT rate and VAT amount;
- vehicle ID and vehicle registration-plate snapshot;
- total without discount and amount paid with the transaction discount.

The generated records are immutable facts. A status may represent `Completed`, `Refunded`, `Cancelled`, or `Recalculated`; later corrections create a new version or related record rather than rewriting the original fact. The VAT rate is configurable per transaction and seeded with the applicable Polish MVP rate.

Users can filter transactions by period and see totals with and without discount. Totals state clearly whether they are net or gross and include VAT consistently.
Selected-period gross savings use each selected row's undiscounted point-of-sale gross amount, calculated from quantity, station unit price, and VAT, minus its signed discounted gross amount. Signed refunds and cancellations reverse savings in their own reporting period.

## 6. Fleet Card Programs

- Programs are read-only data imported or seeded from JSON.
- Each program has a name, active period, fuel type, and percentage discount or premium. A negative percentage is a premium.
- For a selected period, the system calculates the hypothetical amount for each available program by applying the fuel-type percentage to the station price of matching transactions.
- The comparison shows the current amount without a program, the hypothetical amount, and the difference.
- Program calculation respects transaction status, VAT basis, decimal precision, and the program's effective period.

## 7. Client program assignment

- The client has zero or one active program at any instant.
- A read-write user can schedule a program from a future start timestamp, edit its start and end timestamps before activation, replace an existing future assignment, or remove a future assignment.
- When a new assignment starts, the previous assignment ends at that exact timestamp.
- Assignment history is append-only and immutable; past assignments are never rewritten.
- The system rejects overlapping assignments and invalid periods. Editing is unavailable once an assignment becomes effective.
- Removing an assignment means the client pays the station amount without a program discount for that period.

## 8. Acceptance criteria

1. Seeded read-write and read-only users can log in; read-only direct API writes are rejected.
2. Client data cannot be accessed outside the installation's authenticated client context.
3. Switching the UI language changes the supported screens between Polish and English.
4. Car CRUD, normalization, search, archive/restore, and immutable issue-time vehicle assignment work through the API and UI.
5. Issuing a card stores only masked PAN and provider token; no full PAN/CVV appears in database records, logs, telemetry, or API responses.
6. Each issued card has 5-15 persisted previous-month transactions with valid calculated totals and VAT fields.
7. Each issued card retains its issue-time vehicle assignment; its state history includes initial `Active` issuance and later state changes newest first, and remains readable to read-only users.
8. Read-write users can select any supported card state, including reactivating blocked, stolen, or archived cards; card and transaction history remains available.
9. Transaction period totals, gross savings, and program comparisons are deterministic and respect transaction status, signed corrections, and VAT.
10. Program history contains no overlapping intervals and never has more than one active program.
11. Aspire starts the API, Angular SPA, SQL Server, and local telemetry for acceptance tests from a clean local environment.