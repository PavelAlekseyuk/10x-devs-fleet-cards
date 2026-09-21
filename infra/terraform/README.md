# Terraform Infrastructure

This root owns Azure resources inside the dedicated demo resource group created by
`scripts/bootstrap-azure.ps1`. The bootstrap owns the resource group, remote-state storage, GitHub OIDC
application, and its role assignments. Terraform creates the Windows App Service plan/Web App, Azure SQL
server/database, system-assigned Web App identity, SQL firewall exception, and cost budget.

## Prerequisites

- Azure CLI logged in as a subscription owner or equivalent bootstrap administrator.
- Terraform `1.9.x` or later.
- A globally unique lower-case storage account name.
- A price/SKU check for both `westeurope` and `polandcentral`, recorded in
  `docs/deployment/demo-constraints.md` before any apply.

## Bootstrap

Run once from PowerShell. It creates two resource groups, the state backend, a GitHub OIDC application
restricted to `main`, and minimum roles for deployment.

```powershell
./scripts/bootstrap-azure.ps1 `
  -SubscriptionId '<subscription-id>' `
  -ProjectPrefix 'fleetcards' `
  -Location 'westeurope' `
  -GitHubRepository '<owner>/10x-devs-fleet-cards' `
  -StateStorageAccountName '<globally-unique-name>'
```

Store the printed values as GitHub **repository variables**. Do not create a client secret or publish
profile. The action will exchange its GitHub token through the federated credential.

## Local validation

Copy `terraform.tfvars.example` to an ignored `terraform.tfvars`, then run:

```powershell
terraform -chdir=infra/terraform fmt -check
terraform -chdir=infra/terraform init -backend=false
terraform -chdir=infra/terraform validate
```

The CI pipeline will initialize the Azure Storage backend with OIDC and the non-secret state variables.
No real `.tfvars`, state files, or backend credentials belong in the repository.

## Apply safeguards

`terraform apply` deliberately fails unless the following are explicitly supplied in ignored runtime
configuration or CI variables:

- a globally unique `resource_suffix`;
- an exact `sql_database_sku_name` and either a verified `sql_database_is_free` declaration or
  `allow_paid_resources = true`;
- a budget amount, start date, and alert recipient;
- a Microsoft Entra user or group acting as the Azure SQL administrator;
- acknowledgement of the `AllowAzureServicesForDemo` firewall rule.

The firewall exception permits Azure-hosted workloads, including the F1 App Service and the GitHub-hosted
deployment runner, to reach the SQL server. It is acceptable only for this synthetic-data demo. Replace it
with explicit outbound IP allow rules or private networking before any non-demo data is deployed.