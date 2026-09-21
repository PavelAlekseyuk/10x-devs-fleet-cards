---
starter_id: dotnet
package_manager: dotnet
project_name: fleet-cards
hints:
  language_family: dotnet
  backend: aspnet-core-10-web-api
  frontend: angular-20-spa
  local_orchestration: dotnet-aspire
  local_database: sql-server
  production_database: azure-sql
  team_size: solo
  deployment_target: azure-app-service
  ci_provider: github-actions
  ci_default_flow: auto-deploy-on-merge
  bootstrapper_confidence: verified
  path_taken: standard
  quality_override: false
  self_check_answers: null
  has_auth: true
  has_payments: false
  has_realtime: false
  has_ai: false
  has_background_jobs: false
---

## Why this stack

The FleetCards MVP uses a separate ASP.NET Core 10 Web API backend and Angular 20 TypeScript SPA frontend. .NET Aspire composes the API, Angular development server, SQL Server, service references, and local telemetry for development and acceptance testing. EF Core provides relational persistence with SQL Server locally and Azure SQL in production. Azure App Service is the production hosting target, with GitHub Actions and auto-deploy on merge.
