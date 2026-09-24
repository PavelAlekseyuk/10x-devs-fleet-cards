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

The Playwright runner starts its own AppHost with `aspire run --isolated`, disables reuse of any existing web server, and generates a fresh `fleetcards_e2e_<32 lowercase hex>` physical database name for that run. Aspire keeps the connection resource alias as `fleetcards`; the API verifies the effective physical database name before applying migrations. The SQL Server container uses session lifetime without a data volume, so stopping the runner removes its database storage. Migrations seed the same known Anna, Piotr, Volvo, Toyota, card, and transaction rows on every run. No reset, drop, or migration command targets the persistent default developer database.

This suite is limited to S-02 vehicle acceptance: seeded ReadWrite and ReadOnly access, normalized search, create/update/archive/restore, restore conflict, and preservation of the seeded Volvo's card assignment and Diesel transaction snapshot. It does not automate card issuance, transactions, or the complete operational journey; those remain in S-03/S-05.
