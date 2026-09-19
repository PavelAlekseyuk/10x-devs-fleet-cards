---
starter_id: dotnet
package_manager: dotnet
project_name: client-portal
hints:
  language_family: dotnet
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

The small web-app MVP has a five-week timeline, low scale, seeded authentication, and a Microsoft Azure hosting preference. The recommended .NET ASP.NET Core webapi starter provides strong typing, conventional structure, dependency injection, OpenAPI, and Entity Framework integration, and its bootstrapper support is verified. Azure App Service F1 is the selected deployment target, with GitHub Actions and auto-deploy on merge. The Angular TypeScript SPA and Azure database choice remain application-level setup decisions around the .NET API.
