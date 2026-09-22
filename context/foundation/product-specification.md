# Fleet Card Client Module Specification

## 1. Product summary

This MVP is a client-facing web module for a single client installation. An authenticated client representative can inspect client details, maintain the vehicle fleet, manage virtual fleet cards, review fuel transactions, compare fleet card programs, and choose at most one program for the client.

Authentication and the card provider are mocked. Several users, cars, cards, programs, and transaction records are seeded or generated for local development and acceptance testing. No self-onboarding or operations module is part of this MVP.

Each client has its own installation and database. Fleet Card Programs are the only centrally authored concept; in the MVP they are represented by imported or seeded JSON data in the client installation.

## 2. Confirmed MVP decisions

| Area | Decision |
| --- | --- |
| Jurisdiction | Poland; client details use Polish business data |
| UI languages | Polish and English |
| Authentication | Mocked username/password authentication with seeded users |
| User permissions | Seeded read-write and read-only client users |
| Tenant model | One client per installation; no cross-client UI |
| Cars | Create, edit, archive, restore, search; registration plate is the key property |
| Cards | Virtual cards; provider mock returns masked PAN and unique token only |
| PAN | Never stored or returned in full; provider owns BIN and generates a valid 16-digit Luhn PAN |
| Card actions | Issue, assign to a car, block, mark stolen, archive; no return operation |
| Card archive | Non-destructive; read-write users may choose any card state, and card/transaction history remains available |
| Transactions | Mock provider generates 1-5 persisted fuel transactions for the previous month per issued card |
| Transaction facts | Date/time, station, fuel type, quantity, price, discount, totals, VAT, and status/version metadata |
| Programs | Seeded/imported JSON; fuel-type percentage discount or premium |
| Program assignment | Whole-client, effective-dated, immutable history; zero or one active program |
| Program changes | Future assignment start/end times are editable before activation; effective history remains immutable |
| Database | Relational persistence with EF Core; SQL Server/Azure SQL direction |
| Local environment | .NET Aspire composes API, Angular SPA, SQL Server, and telemetry |
| Deployment | Azure and Terraform/GitHub Actions remain documented follow-up planning, not MVP functionality |

## 3. Non-goals for the MVP

- Self-registration, invitations, password reset, email verification, or production identity integration.
- Operational-manager UI, client approval, client administration, or program authoring.
- Physical cards, PIN management, CVV, PAN recovery, or card return to a provider.
- Bulk issuance, asynchronous provider callbacks, real provider integration, or settlement.
- Multi-client tenancy in one running installation.
- Native mobile applications.
- Advanced reporting, exports, invoices, reconciliation, or settlement.
- Multi-country tax, currency, address, or regulatory rules.

## 4. Specification documents

- [Requirements and acceptance criteria](requirements.md)
- [Domain model and invariants](domain-model.md)
- [Architecture and local/deployment boundaries](architecture.md)
- [Security, privacy, audit, and test posture](security.md)
- [Open decisions and implementation slices](decisions.md)
- [Implementation roadmap](implementation-roadmap.md)