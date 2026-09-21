# Azure Demo Constraints

This deployment is a public MVP demonstration, not a production service. It must contain only seeded or
synthetic data. Never deploy real client data, credentials, full PAN, CVV, PIN, or provider secrets.

## Tier Rules

- Prefer a Windows App Service `F1` plan and an Azure SQL free offer only after confirming availability in
  the selected subscription and region.
- `F1` and promotional/free Azure SQL capacity have quotas, cold starts, no production SLA, and may change
  without notice. Deployment slots, high availability, and private networking are intentionally excluded.
- If a free Azure SQL offer is unavailable, select the cheapest viable paid SKU explicitly, set
  `allow_paid_resources = true`, and update this document with the SKU, estimated monthly price, and date.
- Add an Azure Cost Management budget and email alert before the first paid deployment.
- The F1 Web App plan does not expose stable outbound IP addresses before creation. This demo therefore uses
  Azure SQL's `AllowAzureServicesForDemo` firewall rule, which permits Azure-hosted services and the
  GitHub-hosted deployment runner. It is acceptable only for synthetic demo data and must be replaced before
  handling any non-demo data.

## Required Preflight Record

Before the first `terraform apply`, record the following values below. The resource group must contain only
this demo's resources so it can be torn down safely.

| Field | Value |
| --- | --- |
| Checked on | 2026-09-21 |
| Selected region | West Europe |
| West Europe availability/price | Azure SQL Free tier available; App Service F1 selected. |
| Poland Central availability/price | Azure SQL Free tier available; not selected. |
| Windows .NET 10 runtime verified | `dotnet:10` available. |
| App Service SKU | F1 |
| Azure SQL offer/SKU | Free |
| Estimated monthly cost | Intended zero-cost demo tiers; Azure Cost Management price-sheet API is unavailable for this subscription. |
| Budget alert recipient | pavel.alexeyuk@gmail.com |

## Teardown

Delete the dedicated demo resource group to remove the app and database. Keep the Terraform state resource
group until the deletion is confirmed and its final state has been reviewed; then delete that group as a
separate owner action.