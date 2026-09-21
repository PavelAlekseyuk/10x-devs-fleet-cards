# Demo Deployment Runbook

The workflow in `.github/workflows/main.yml` runs only for pushes to `main`. A successful run builds, tests,
provisions the demo resources, applies the idempotent database migration, deploys the exact published API/SPA
archive, and checks the public root, API metadata, and database-backed card endpoint.

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

4. Run the workflow once or apply Terraform locally to create the SQL logical server. Read the
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