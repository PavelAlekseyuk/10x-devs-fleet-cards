# Architecture and Azure deployment

## Application shape

- Angular and TypeScript SPA served as static assets.
- ASP.NET 10 Web API with layered application/domain/infrastructure boundaries.
- ASP.NET Core Identity for credentials, password hashing, lockout, reset tokens, and email verification.
- PostgreSQL as the system-of-record database.
- Provider-neutral `ICardProcessor` adapter for issuance, activation, suspension, replacement, and revocation.
- Background worker for issuance batches, outbox messages, retries, and provider callbacks.
- Append-only audit pipeline.

The API is the sole authority for authorization and business invariants. Angular guards improve UX but are not a security boundary.

## Recommended Azure resources

| Concern | MVP choice | Notes |
| --- | --- | --- |
| SPA hosting | Azure Static Web Apps Free | Public HTTPS and managed TLS |
| API/worker | Azure Container Apps Consumption | One API container initially; split worker when load requires it |
| Database | Azure Database for PostgreSQL Flexible Server, burstable/free offer where available | Confirm current regional free grant during provisioning; PostgreSQL remains the portable production choice |
| Secrets | Azure Key Vault | No credentials or provider secrets in Terraform variables/state where avoidable |
| Background scheduling | Container Apps jobs or hosted worker | Use durable database-backed outbox and leases |
| Email | Azure Communication Services Email or approved SMTP provider | Treat email as a low-volume cost exception if no free grant is available |
| Observability | Application Insights / Log Analytics with retention and sampling limits | Never log PAN, CVV, passwords, reset tokens, or raw provider payloads |
| Terraform state | Azure Storage account with blob lease locking | Separate state resource/bootstrap from application resources |

The exact free-tier availability of Azure services changes by subscription, region, and date. Terraform variables must make SKUs configurable, and the README should document the paid fallback before deployment.

## API modules

- `/api/auth`: registration, login, logout, password reset, email verification, current user.
- `/api/clients`: registration review and client administration.
- `/api/users`: membership and user administration.
- `/api/cars`: tenant fleet CRUD.
- `/api/cards`: cards, lifecycle commands, assignments, issuance batches.
- `/api/programs`: programs, versions, fuel rules, client access.
- `/api/audit`: authorized audit search.
- `/api/integrations/card-processor`: authenticated provider callbacks.

Commands should use idempotency keys for registration, issuance, lifecycle transitions, and assignment mutations where retries are possible.

## Deployment and delivery

- Terraform provisions resource groups, identities, networking configuration, database, Key Vault references, monitoring, and hosting.
- CI validates formatting, builds Angular and ASP.NET, runs unit/integration/security tests, produces immutable artifacts, and applies Terraform only with an approved environment.
- Database migrations run as a controlled deployment step before the API receives traffic.
- Configuration is environment-specific and injected through managed configuration/secrets.
- Start with one environment, but use Terraform modules and separate state keys so development, staging, and production can be introduced without redesign.

## Reliability

- Database transactions protect state changes.
- Outbox records guarantee eventual email/provider work after committed changes.
- Background jobs use leases, bounded retries, exponential backoff, and dead-letter/failed states.
- Provider callbacks are idempotent and tolerate duplicate delivery.
- Health checks distinguish process health, database readiness, and provider dependency health.

