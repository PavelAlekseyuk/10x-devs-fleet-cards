# Fleet Card Program Application Specification

## 1. Product summary

The Fleet Card Program application is a multi-tenant web application for issuing and administering virtual fleet cards for Polish business clients. An operational manager approves client registrations, administers client users and fleets, issues cards through a provider-neutral card processor adapter, and manages fuel-pricing programs. Client representatives manage their company users, cars, cards, and permitted pricing programs.

The first release is an administration and card-lifecycle product. It does not process or display fuel transactions, invoices, or settlement data.

## 2. Confirmed MVP decisions

| Area | Decision |
| --- | --- |
| Jurisdiction | Poland only |
| UI language | English only |
| Authentication | Self-hosted ASP.NET Identity with email/password; MFA optional |
| Operational role | One `OperationalManager` role |
| Client roles | `ClientRepresentative` with read-write or read-only company rights |
| Company approval | NIP, legal name, registered address, representative identity/contact; approve or reject |
| Client-user onboarding | Client representative creates the user immediately; invitation acceptance is not required |
| Cards | Virtual cards; external processor owns PAN, BIN, account range, and Luhn validation |
| Stored card data | Processor token and last four digits; never store PAN or CVV |
| Card lifecycle | Pending, active, suspended, revoked, expired, replaced |
| Programs | Petrol, diesel, and LPG; multiple pricing models; effective-dated versions |
| Program access | Client allowlist; disallow blocks new assignments but does not invalidate existing assignments |
| Card/car/program assignments | Independent |
| Cars | One client at a time; normalized Polish registration plate is globally unique; assignment history retained |
| Deletion | Companies and cards are soft-deleted; draft cars and programs may be hard-deleted |
| Audit | All authentication, KYC, user, car, card, program, assignment, and permission changes; seven-year retention |
| Notifications | Registration, approval/rejection, invitation, card issued/revoked, and password-reset emails |
| Card batches | Asynchronous bulk issuance with per-card results and retry support |
| Scale target | 100 clients, 1,000 users, 10,000 cars/cards, 50 concurrent users |
| Environments | One environment initially; Terraform must leave a clear path to isolated environments |
| Network | Public HTTPS with Azure-managed TLS and standard hardening |
| Azure cost strategy | Strict free-tier/grant MVP where available, with upgrade notes |

## 3. Non-goals for the MVP

- Fuel transaction authorization, ingestion, reconciliation, invoicing, or settlement.
- Physical cards, card manufacturing, PIN management, CVV display, or PAN recovery.
- Spending, volume, merchant-category, station, geographic, or time-of-day limits.
- Multi-country tax, currency, address, or regulatory rules.
- Native mobile applications.
- Self-service deletion of companies or issued cards.
- CSV exports and advanced reporting.

## 4. Specification documents

- [Requirements and acceptance criteria](requirements.md)
- [Domain model and invariants](domain-model.md)
- [Architecture and Azure deployment](architecture.md)
- [Security, privacy, audit, and operations](security.md)
- [Open decisions and implementation slices](decisions.md)
- [Implementation roadmap](implementation-roadmap.md)
