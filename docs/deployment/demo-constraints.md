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

## Required Preflight Record

Before the first `terraform apply`, record the following values below. The resource group must contain only
this demo's resources so it can be torn down safely.

| Field | Value |
| --- | --- |
| Checked on | Not yet checked |
| Selected region | Not yet selected |
| West Europe availability/price | Not yet checked |
| Poland Central availability/price | Not yet checked |
| Windows .NET 10 runtime verified | Not yet checked |
| App Service SKU | Not yet selected |
| Azure SQL offer/SKU | Not yet selected |
| Estimated monthly cost | Not yet checked |
| Budget alert recipient | Not yet selected |

## Teardown

Delete the dedicated demo resource group to remove the app and database. Keep the Terraform state resource
group until the deletion is confirmed and its final state has been reviewed; then delete that group as a
separate owner action.