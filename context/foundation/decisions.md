# Decisions and open questions

## Locked decisions

1. The MVP is client-facing only; there is no operations UI or self-registration.
2. Authentication is mocked with seeded accounts, including read-write and read-only users.
3. The card provider is mocked behind an adapter and never exposes full PAN, CVV, or PIN.
4. Card return is excluded. Read-write users may choose any card state in the MVP, including reactivating blocked, stolen, or archived cards.
5. Programs are seeded/imported JSON and assigned to the whole client, not individual cards.
6. Transactions are persisted mock facts generated on issue and include a configurable VAT rate supplied by the transaction-processing subsystem. MVP seed transactions use 23% VAT, and the UI displays both net and gross values.
7. Program history is effective-dated, immutable, and contains zero or one active program.
8. Relational persistence is the implementation direction: EF Core, local SQL Server, Azure SQL later.
9. Aspire remains mandatory for local development and acceptance-test composition.
10. Terraform, GitHub Actions, and Azure production hosting remain documented follow-up work.
11. Quantity multiplication, program percentage calculations, VAT, and displayed totals use industry-standard decimal rounding away from zero.
12. Future program assignment start and end times can be edited before the assignment becomes effective; effective and past history remains immutable.
13. English is the baseline UI language. Polish translations are maintained in Angular application resources, and users can switch freely between languages.
14. Every generated transaction stores a vehicle reference/snapshot so reporting preserves the vehicle assigned to the card at issuance.
15. Transaction recalculations replace the purchase's full effective amounts in the purchase's original reporting month and retain the original occurrence time. Refunds and cancellations are separate immutable financial events linked to the purchase for audit, reported in their own occurrence month with explicit signed net, VAT, and gross amounts; multiple partial refunds are additive. A cancellation contributes its signed reversal amounts, not an automatic zero.
16. Each issued card has one required, immutable vehicle assignment captured at issuance. Its state history includes issuance as the initial `Active` event and lists subsequent state changes newest first.
17. Selected-period gross savings are calculated from each effective transaction row's undiscounted point-of-sale gross value, using quantity, station unit price, and VAT, less the row's signed discounted gross value. Signed corrections affect savings in their own reporting period.
18. A one-time migration deletes legacy cards with a null vehicle assignment and their transaction versions/corrections, assignment events, and card audit events before making the assignment required. This narrowly scoped cleanup is an explicit exception to append-only transaction and audit retention; assigned cards and their facts are preserved.

## Deferred after MVP

- Real identity provider and password lifecycle.
- Real card processor and provider callbacks.
- Operations module and centrally synchronized program catalog.
- Multi-client hosting, production deployment automation, alerting, and advanced audit retention.