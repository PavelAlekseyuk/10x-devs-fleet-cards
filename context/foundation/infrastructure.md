---
project: fleet-cards
researched_at: 2026-09-21
recommended_platform: Azure App Service + Azure SQL
runner_up: Railway + Azure SQL
context_type: mvp
tech_stack:
  language: C#
  framework: ASP.NET Core 10 Web API + Angular 20 SPA
  runtime: .NET 10
---

## Recommendation

**Deploy the API to Azure App Service and use Azure SQL in the same Azure region.** This is the only
option evaluated that natively hosts the existing ASP.NET Core 10 API, preserves the selected Azure SQL
production database, and meets the preference for managed service co-location. Azure familiarity and
first-class Azure CLI/GitHub Actions support break the agent-operability tie in its favor; its higher
minimum cost is recorded as the primary trade-off.

Research was performed on 2026-09-21 from official platform documentation and pricing pages. Current
availability, region support, and subscription-specific pricing must be checked before provisioning.

## Platform Comparison

| Platform | CLI-first | Managed / serverless | Agent-readable docs | Stable deploy API | MCP / integration | Total | Outcome |
|---|---|---|---|---|---|---|---|
| Azure App Service + Azure SQL | Pass | Pass | Pass | Pass | Pass | 5/5 | Recommended |
| Railway + Azure SQL | Pass | Pass | Pass | Pass | Pass | 5/5 | Runner-up |
| Render + Azure SQL | Pass | Pass | Pass | Pass | Pass | 5/5 | Third choice |
| Fly.io + Azure SQL | Pass | Pass | Pass | Pass | Partial | 4/5 | Viable alternative |
| Cloudflare Workers + Pages | Fail | N/A | N/A | N/A | N/A | N/A | Dropped: no ASP.NET Core/.NET runtime; Hyperdrive does not support Azure SQL |
| Vercel | Partial | Pass | Pass | Pass | Partial | N/A | Dropped: .NET requires the non-GA Dockerfile path |
| Netlify | Fail | N/A | N/A | N/A | N/A | N/A | Dropped: Functions do not host ASP.NET Core/.NET |

### Notes

- **Azure App Service + Azure SQL**: `az webapp` provides create, deploy, settings, identity, slot, and log
  commands with JSON output. Azure App Service natively supports ASP.NET Core and Azure SQL through
  managed services in one region. Microsoft Learn documentation is Markdown-backed and GitHub Actions
  supports OpenID Connect. The free/shared plans are **preview** and unsupported for production. On the
  current US Windows pricing page, B1 starts at $54.75/month before Azure SQL; regional and
  subscription pricing varies. Azure CLI `az webapp exec`, `az webapp ssh`, and startup-log commands are
  **preview** and are not part of the operating baseline.
- **Railway + Azure SQL**: Railpack supports C# projects and `railway up`, `railway logs`, and its remote
  MCP/agent tooling provide a strong terminal workflow. Railway's cloud agents are **beta** and its MCP
  offering is **preview** as of the research date. Azure SQL is external, so the co-location preference
  is not met.
- **Render + Azure SQL**: Managed web services, a GA CLI, Markdown/`llms.txt` docs, and a GA MCP server
  are excellent for agent operation. ASP.NET Core requires Docker, which introduces build and image
  maintenance; Azure SQL remains external.
- **Fly.io + Azure SQL**: `flyctl`, the Machines API, and GitHub-hosted Markdown docs are mature. It
  runs ASP.NET Core in microVMs with persistent processes, but has no native MCP server and would use
  external Azure SQL. Its automatic rollback-on-health-check capability is **beta**.
- **Cloudflare Workers + Pages**: Workers does not execute ASP.NET Core/.NET. The Workers paid plan has a
  $5/month minimum and excellent Wrangler/docs support, but that does not overcome the runtime and Azure
  SQL incompatibility.
- **Vercel**: Strong CLI, OpenAPI-backed docs, and rollout controls, but the Dockerfile option for this
  .NET API is non-GA. Its MCP and extended observability capability are **beta**.
- **Netlify**: Strong CLI and Markdown docs but Functions are JavaScript/TypeScript-based, so it cannot
  host the ASP.NET Core API.

### Shortlisted Platforms

#### 1. Azure App Service + Azure SQL (Recommended)

Native .NET 10 hosting, Azure SQL co-location, familiar Azure operations, and GitHub Actions OIDC make
this the lowest-change deployment target. It is a managed PaaS with a scriptable CLI and durable security
controls such as managed identity and scoped RBAC.

#### 2. Railway + Azure SQL

Railway is a low-friction, lower-cost alternative with excellent CLI, documentation, and emerging agent
integration. It loses to Azure because the production database remains external and Azure SQL networking
and credentials must be managed across providers.

#### 3. Render + Azure SQL

Render has particularly strong GA agent tooling and simple managed web services. It ranks behind Railway
because this stack needs a Dockerfile there and still cannot co-locate the selected Azure SQL database.

## Anti-Bias Cross-Check: Azure App Service + Azure SQL

### Devil's Advocate - Weaknesses

1. The minimum production App Service plan can dominate MVP cost, while Azure SQL is billed separately;
   low traffic does not reduce either bill proportionally.
2. The ASP.NET API and separately built Angular SPA need an explicit hosting and release model. Without
   one, SPA routing, CORS, and cache invalidation can break releases.
3. A publish profile or subscription-wide service principal would create a broad, long-lived production
   credential if used as a shortcut for GitHub Actions deployment.
4. Safe deployment-slot rollback is tier-dependent. At a lower tier, recovery may require a redeploy
   instead of a quick slot swap.
5. Moving Azure SQL from a public firewall exception to private endpoints/VNet integration introduces
   networking complexity that is easy to defer indefinitely.

### Pre-Mortem - How This Could Fail

Six months in, the team finds deployment slower and more expensive than expected. They chose App Service
because it fit .NET and Azure SQL, but treated the services as a cheap host rather than independently
billed managed products. The app runs on a Basic plan, Azure SQL has its own minimum tier, and a staging
slot is unavailable or unaffordable, so every release deploys directly to production. The Angular build
is served without an explicit cache policy, and users receive an old client against a changed API. To get
releases working quickly, a wide Azure service principal or publish profile was stored in GitHub and never
revisited. Azure SQL remains exposed through permissive networking because private connectivity appeared
too complex for the MVP. A schema migration then fails after deployment; application binaries can be
restored, but the database cannot be rolled back with them. The product has cost pressure, excessive
production access, and no rehearsed recovery path because convenient early decisions became permanent.

### Unknown Unknowns

- `az webapp up` is deprecated. Use explicit `az webapp create` followed by `az webapp deploy`, or a
  GitHub Actions workflow.
- Verify .NET 10 is available in the intended region immediately before provisioning with
  `az webapp list-runtimes --os linux --runtime dotnet`.
- Free/shared App Service plans are **preview** and unsupported for production workloads; pricing and
  availability vary by region and subscription.
- `az webapp exec`, `az webapp ssh`, and startup-log commands are **preview**. Use GA deployment-log and
  log-tail commands for normal operations.
- Reverting an app deployment does not undo EF Core migrations or Azure SQL data changes. Releases need
  backward-compatible migrations and a separately rehearsed database recovery procedure.

## Operational Story

- **Preview deploys**: Build PR artifacts in GitHub Actions. For an MVP, deploy manually approved branch
  builds to a non-production App Service app; use an App Service deployment slot only after verifying the
  selected tier supports it. Protect the preview through App Service Authentication or IP restrictions
  before it contains real client data.
- **Secrets**: Put application settings and connection strings in App Service; prefer managed identity for
  Azure SQL over a password when the data layer is implemented. Keep GitHub Actions OIDC identifiers in
  GitHub Secrets, never in workflow source. Limit the identity to `Website Contributor` on this app.
- **Rollback**: On a slot-capable plan, deploy to staging and run `az webapp deployment slot swap` only
  after verification; swap back to reverse an application release. Without a slot, redeploy the previous
  published artifact. Database migrations do not roll back automatically.
- **Approval**: A human approves production publication, slot swaps, primary-secret rotation, database
  deletion, firewall changes, and migration execution. An agent may run builds, tests, read-only status
  queries, and deployment to a non-production target with a scoped identity.
- **Logs**: Read deployment logs with `az webapp log deployment show --resource-group <rg> --name <app>`
  and runtime logs with `az webapp log tail --resource-group <rg> --name <app>`. Configure application
  diagnostics before relying on log tailing.

## Risk Register

| Risk | Source | Likelihood | Impact | Mitigation |
|---|---|---|---|---|
| Hosting and database minimum cost exceeds MVP budget | Devil's advocate | M | M | Price the exact App Service and Azure SQL region/SKU before provisioning; set Azure Cost Management budgets and alerts. |
| Over-scoped deployment credential | Devil's advocate | M | H | Use GitHub OIDC with a user-assigned identity scoped to the specific Web App; do not use publish profiles for automation. |
| App rollback leaves an incompatible schema | Pre-mortem | M | H | Require backward-compatible EF Core migrations and test restore/redeploy before the first production data change. |
| Stale Angular client after release | Pre-mortem | M | M | Choose and test one hosting model; version API contracts and set explicit cache headers for Angular assets. |
| Preview/slot capabilities unavailable at the chosen price tier | Devil's advocate | M | M | Verify regional SKU and slot availability before committing; use a separate non-production app when slots are unavailable. |
| Azure SQL remains publicly reachable | Pre-mortem | M | H | Start with the narrowest firewall rule possible, then schedule managed identity and private connectivity before real client data. |
| Deployment commands change or become deprecated | Unknown unknowns | L | M | Pin CLI versions in automation and use `az webapp create` plus `az webapp deploy`, not deprecated `az webapp up`. |
| Assumed .NET 10 runtime is unavailable in target region | Unknown unknowns | L | H | Run `az webapp list-runtimes --os linux --runtime dotnet` for the target subscription before resource creation. |
| Preview-only diagnostics used in incident response | Unknown unknowns | L | M | Base runbooks on GA log-tail and deployment-log commands; treat preview CLI features as optional. |

## Getting Started

1. Select the Azure region and use Azure Pricing Calculator to price the exact Linux App Service and Azure
   SQL SKUs. Do not use the Free/Shared plans for production; their current status is **preview**.
2. Confirm the .NET runtime in that subscription and region: `az webapp list-runtimes --os linux --runtime dotnet`.
   Proceed only when `DOTNETCORE:10.0` is listed.
3. Create the resource group and App Service plan, then create the Linux web app with the verified
   `DOTNETCORE:10.0` runtime. Create Azure SQL in the same region.
4. Publish `src/FleetCards.Api` with .NET 10 and deploy its resulting artifact with
   `az webapp deploy --resource-group <rg> --name <app> --src-path <published-zip>`. Build the Angular 20
   SPA independently with `npm run build`; select its final production host and caching model before
   exposing it publicly.
5. Set up GitHub Actions with OIDC and an identity scoped to this app. Build and test both the .NET API
   and Angular SPA before deployment; do not add a publish profile to repository secrets.

## Out of Scope

The following were not evaluated in this research:

- Docker image configuration
- CI/CD pipeline implementation
- Production-scale architecture, including multi-region HA and disaster recovery