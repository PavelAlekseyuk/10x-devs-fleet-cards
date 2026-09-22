# Demo Deployment Runbook

The workflow in `.github/workflows/main.yml` runs only for pushes to `main`. A successful run builds, tests,
provisions the demo resources, applies the idempotent database migration, deploys the exact published API/SPA
archive, and checks the public root, API metadata, and database-backed card endpoint.

## Local seeded sign-in

Start the composed local runtime from the repository root:

```powershell
dotnet run --project src/FleetCards.AppHost/FleetCards.AppHost.csproj
```

Open the Angular portal at `http://localhost:4200/`. The API applies pending EF migrations during startup,
including the seeded credential migration. The local seeded accounts are:

| Username | Password | Permission |
| --- | --- | --- |
| `anna.nowak` | `Anna123!` | ReadWrite |
| `piotr.wisniewski` | `Piotr123!` | ReadOnly |

These credentials are synthetic MVP demo credentials only. Persistence stores password hashes, never these
password values. Do not reuse them outside a local demonstration or promote them to production configuration.

The browser first requests `GET /api/auth/csrf`, then sends the returned `XSRF-TOKEN` cookie value in the
`X-XSRF-TOKEN` header for login and logout. Successful login creates an eight-hour sliding,
HttpOnly `FleetCards.Session` cookie. Refreshing the browser restores the session through `GET /api/auth/me`.
Logout clears the session; subsequent client-data requests return `401`.

### Local permission matrix

| Capability | ReadWrite | ReadOnly |
| --- | --- | --- |
| Client, vehicle, card, and transaction views | Allowed | Allowed |
| Program view and benefit area | Allowed | Denied (`403`) |
| Mutating commands | Allowed when implemented | Denied (`403`) |
| Unauthenticated client-data requests | Denied (`401`) | Denied (`401`) |

Authentication outcomes are currently represented by structured logs with correlation IDs. Durable audit rows,
production identity integration, password reset, and production-grade distributed throttling are deferred MVP work.

Focused local verification:

```powershell
dotnet test tests/FleetCards.Api.Tests/FleetCards.Api.Tests.csproj
dotnet test tests/FleetCards.Application.Tests/FleetCards.Application.Tests.csproj
Push-Location web; npm test; npm run build; Pop-Location
dotnet build FleetCards.sln
```

During a composed run, inspect the `api` resource logs in the Aspire dashboard or the AppHost terminal for
successful, failed, throttled, logout, and denied authentication events. Logs must not contain passwords,
session values, full PAN, CVV, or PIN data.

## One-time owner setup

1. Complete the region and SKU record in `docs/deployment/demo-constraints.md`.
2. Run `scripts/bootstrap-azure.ps1` as a subscription owner. Create GitHub repository variables from its output:
   `AZURE_CLIENT_ID`, `AZURE_PRINCIPAL_OBJECT_ID`, `AZURE_PRINCIPAL_DISPLAY_NAME`, `AZURE_TENANT_ID`,
   `AZURE_SUBSCRIPTION_ID`, `TF_STATE_RESOURCE_GROUP`, `TF_STATE_STORAGE_ACCOUNT`,
   `TF_STATE_CONTAINER`, `DEMO_RESOURCE_GROUP`, and `AZURE_LOCATION`.
3. Configure the remaining repository variables:

   | Variable | Value |
   | --- | --- |
   | `TF_PROJECT_PREFIX` | Chosen lowercase project prefix. |
   | `TF_RESOURCE_SUFFIX` | Globally unique lowercase alphanumeric suffix. |
   | `TF_APP_SERVICE_SKU` | `F1` for the selected demo tier. |
   | `TF_SQL_DATABASE_SKU_NAME` | Verified free offer or explicitly selected paid SKU. |
   | `TF_SQL_DATABASE_IS_FREE` | `true` only for a verified free SQL offer. |
   | `TF_ALLOW_PAID_RESOURCES` | `true` only after explicitly accepting the selected paid SQL SKU. |
   | `TF_ENABLE_AZURE_SERVICES_SQL_FIREWALL_RULE` | `true` for this synthetic-data F1 demo. |
   | `TF_BUDGET_AMOUNT` | Positive monthly budget amount. |
   | `TF_BUDGET_ALERT_EMAIL` | Budget-alert recipient. |
   | `TF_BUDGET_START_DATE` | First day of a month in UTC, for example `2026-10-01T00:00:00Z`. |
   | `AZURE_SQL_ADMINISTRATOR_LOGIN` | Value of `AZURE_PRINCIPAL_DISPLAY_NAME`. |
   | `AZURE_SQL_ADMINISTRATOR_OBJECT_ID` | Value of `AZURE_PRINCIPAL_OBJECT_ID`. |

4. Re-run the bootstrap script or assign the deployment identity `Contributor` at the subscription scope.
   Terraform needs subscription-level provider-registration rights before it can create the demo resource
   group and Azure SQL resources. Resource-group-only access is not enough.

5. Run the workflow once or apply Terraform locally to create the SQL logical server. Read the
   `sql_server_identity_principal_id` Terraform output.
5. As a Microsoft Entra Privileged Role Administrator, run:

   ```powershell
   ./scripts/grant-sql-directory-readers.ps1 -SqlServerPrincipalId '<terraform-output>'
   ```

   The SQL server identity needs Directory Readers to resolve the Web App managed identity during
   `CREATE USER ... FROM EXTERNAL PROVIDER`. This is a one-time tenant-level grant that cannot be safely
   performed by the repository’s limited GitHub deployment identity.
6. Push an empty commit to `main` or rerun the workflow. It then creates the Web App database user and deploys.

## Release and recovery

- The concurrency group prevents overlapping deployments. A newly queued run replaces an older pending run,
  but never stops an active migration or deployment.
- The migration script is forward-only and idempotent. A migration failure stops the workflow before app
  deployment.
- F1 has no slots. To roll back application code, rerun a successful prior workflow or deploy its retained
  `fleet-cards-deployment` artifact. Do not attempt to reverse a schema migration automatically.
- Inspect failures in GitHub Actions first. Azure deployment details are available through the Web App
  deployment logs; runtime diagnostics remain constrained by the selected demo tier.

## Teardown

Delete the dedicated demo resource group after the demo. Retain the Terraform state resource group until the
demo resource deletion is confirmed and its final state is reviewed, then delete it separately as an owner.