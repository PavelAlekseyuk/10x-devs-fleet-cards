# Web

FleetCards uses Angular CLI 20.3.37. Install dependencies from this directory with `npm ci`.

## Local Development

Run the Angular development server with `npm start`, then open `http://localhost:4200/`.
The normal composed development runtime is `dotnet run --project src/FleetCards.AppHost/FleetCards.AppHost.csproj` from the repository root.

Run the Angular production build with `npm run build` and unit tests with `npm test`. Unit tests use the Angular CLI's Vitest runner.

## Fleet Vehicle Browser Acceptance

Prerequisites are .NET SDK 10, the Aspire CLI, Node.js 22 or newer, Docker Desktop with its Linux container engine running, and the npm dependencies installed. Install the Playwright Chromium browser once per machine with:

```powershell
npx playwright install chromium
```

From this directory, run the focused suite with:

```powershell
npm run test:e2e
```

The Playwright runner starts its own AppHost with `aspire run --isolated`, disables reuse of any existing web server, and generates a fresh `fleetcards_e2e_<32 lowercase hex>` physical database name for that run. Aspire keeps the connection resource alias as `fleetcards`; the API verifies the effective physical database name before applying migrations. The SQL Server container uses session lifetime without a data volume, so stopping the runner removes its database storage. Migrations seed the same known Anna, Piotr, Volvo, Toyota, card, and transaction rows on every run. Each newly issued card must receive 5-15 persisted previous-month transactions. No reset, drop, or migration command targets the persistent default developer database.

The suite covers the operational MVP gate: seeded ReadWrite and ReadOnly access, normalized vehicle search and lifecycle, issue-time card assignment, card state history and refresh, masked-card redaction, generated transactions, selected-period totals and gross savings, bilingual labels, and overview/report month synchronization. Card archive is the non-destructive MVP lifecycle action; card return is not implemented. Critical authentication, authorization, migration, data-integrity, and sensitive-data failures block acceptance. Architecture and accessibility findings are recorded separately unless a concrete existing rule makes them objectively testable.
