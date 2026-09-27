---
date: 2026-09-27T10:53:49+02:00
researcher: "GitHub Copilot"
git_commit: 19e0cb7650d84ccde301f32d54997024149fe36c
branch: "bugfix/small-fixes"
repository: "10x-devs-fleet-cards"
topic: "Card lifecycle and overview corrections before proceeding from S-04"
tags: [research, codebase, cards, dashboard, transactions, localization]
status: partial
last_updated: 2026-09-27
last_updated_by: "GitHub Copilot"
---

# Research: Card lifecycle and overview corrections before proceeding from S-04

**Date**: 2026-09-27T10:53:49+02:00  
**Researcher**: GitHub Copilot  
**Git Commit**: 19e0cb7650d84ccde301f32d54997024149fe36c  
**Branch**: bugfix/small-fixes  
**Repository**: 10x-devs-fleet-cards

## Research Question

Ground the requested pre-S-05 corrections: make a card's issue-time vehicle assignment immutable; replace assignment history with newest-first card state history; explain why a blocked card's state control appears Active after refresh; correct overview net/gross labels, add a savings metric from POS-price and discounted amounts, and add previous/next month controls to Spend Overview.

## Summary

The assignment, history, and overview label behavior is directly supported by the inspected code and tests. The existing application permits reassignment, its history is an ascending vehicle-assignment timeline, and its dashboard summary supplies discounted net/VAT/gross totals but no POS-price or savings total. The month service already exposes shared selected-month navigation, while the overview loads its summary on entry or retry rather than when the month changes.

The blocked-card refresh report is not explained by the normal EF-backed read path: state changes are persisted, the card list returns the stored state, and the dropdown binds to that response. In the seeded mode, state is in memory and survives a browser refresh while the API process remains alive, but a process restart recreates the seeded Active card. The issue remains partial until the `/api/cards` response and rendered dropdown are compared after the reported refresh.

## Detailed Findings

### Cards

- The current cards UI permits choosing a different active vehicle and submitting it. Both EF and seeded services accept a different active vehicle for a non-archived card, update `AssignedCarId`, and append an assignment event. The current API exposes the assignment mutation. There is no release control in the inspected card UI/API route path. This conflicts with the requested post-issuance immutability and with the currently recorded requirement that cards may be reassigned. ([card-management.component.html](web/src/app/card-management.component.html#L85), [card-management.component.ts](web/src/app/card-management.component.ts#L113), [EfFleetCardService.cs](src/FleetCards.Infrastructure/EfFleetCardService.cs#L188), [SeededFleetCardService.cs](src/FleetCards.Infrastructure/SeededFleetCardService.cs#L221), [Program.cs](src/FleetCards.Api/Program.cs#L483), [requirements.md](context/foundation/requirements.md#L46))
- The visible history view is backed by `CardAssignmentEvents`, contains vehicle/plate and effective-period data, and is ordered oldest-first by effective timestamp and ordering key in both service implementations. It does not show card states. State changes instead update the card and append an audit action such as `CardStateChangedToBlocked`; the inspected card routes expose assignment history but no state-history query. A state-history view therefore needs a status-event projection, newest-first ordering, and a decision on whether the issue-time Active state appears as an initial entry. ([card-management.component.html](web/src/app/card-management.component.html#L113), [EfFleetCardService.cs](src/FleetCards.Infrastructure/EfFleetCardService.cs#L149), [EfFleetCardService.cs](src/FleetCards.Infrastructure/EfFleetCardService.cs#L267), [SeededFleetCardService.cs](src/FleetCards.Infrastructure/SeededFleetCardService.cs#L280), [FleetCardsModel.cs](src/FleetCards.Domain/FleetCardsModel.cs#L50))
- On the normal EF path, `ChangeStateAsync` writes the new card state and audit event before returning; `GET /api/cards` maps the service response state, and the Angular state `<select>` binds to `card.state` while offering the four supported states. The seeded service is registered as a singleton without a database connection, so a browser reload against the same running API should retain its in-memory state; restarting that API recreates the seeded card. The code reviewed does not establish which case produced the reported Active display. Compare the actual `GET /api/cards` payload after refresh: `Active` points to persistence/runtime setup, while `Blocked` with an Active-looking control points to rendering/binding. Existing state coverage demonstrates Active-to-Blocked in one component fixture and API state responses, but not the reported fresh-read path. ([EfFleetCardService.cs](src/FleetCards.Infrastructure/EfFleetCardService.cs#L149), [Program.cs](src/FleetCards.Api/Program.cs#L394), [card-management.component.html](web/src/app/card-management.component.html#L74), [FleetCardsInfrastructure.cs](src/FleetCards.Infrastructure/FleetCardsInfrastructure.cs#L25), [SeededFleetCardService.cs](src/FleetCards.Infrastructure/SeededFleetCardService.cs#L178), [card-management.component.spec.ts](web/src/app/card-management.component.spec.ts#L142), [CardApiTests.cs](tests/FleetCards.Api.Tests/CardApiTests.cs#L93))
- Current application and API tests explicitly expect reassignment to succeed and history to remain chronological. Updating the invariant requires replacing those assertions, not merely hiding the UI control. ([FleetCardServiceTests.cs](tests/FleetCards.Application.Tests/FleetCardServiceTests.cs#L222), [CardApiTests.cs](tests/FleetCards.Api.Tests/CardApiTests.cs#L93))

### Overview

- Localization uses `netSpend` for the breakdown label: current English is “Net spend” and current Polish is “Wydatki netto”. `totalPaidWithDiscount` currently reads “Total paid with discount” in English and “Łącznie po rabacie” in Polish. The spend panel binds that second label to `periodTotals.grossTotal`; its net and VAT rows use `netTotal` and `vatTotal`. The requested texts map to these existing keys, with the Polish gross wording explicitly adding “brutto”. ([language.service.ts](web/src/app/language.service.ts#L158), [language.service.ts](web/src/app/language.service.ts#L312), [language.service.ts](web/src/app/language.service.ts#L41), [language.service.ts](web/src/app/language.service.ts#L195), [shell.component.html](web/src/app/shell.component.html#L60))
- The third top metric currently displays the discounted gross total under `totalPaidWithDiscount`; its smaller caption is a separate `summaryStateLabel` (`Current total` / `Bieżąca wartość`). Replacing the metric title with “Total savings” is straightforward, but decide whether the secondary caption should remain or instead identify the selected period. The summary DTO currently has active counts and `periodTotals` (net, VAT, gross, row count), with no POS-price total or savings field. ([shell.component.html](web/src/app/shell.component.html#L35), [shell.component.ts](web/src/app/shell.component.ts#L55), [Models.cs](src/FleetCards.Contracts/Models.cs#L97), [Models.cs](src/FleetCards.Contracts/Models.cs#L138))
- Transaction rows expose quantity, unit price, discount, VAT rate, and actual net/gross totals. The generator sets a station unit price and per-unit discount; `CalculateNetTotal` computes and rounds `quantity * (unitPrice - discount)`. A net POS-price baseline can therefore be derived as `quantity * unitPrice`, with net savings as that baseline minus the effective discounted `netTotal`. The report service selects effective purchase versions plus refund/cancellation events before totaling, so savings should use the same report-row selection and correction rules. The request does not specify whether savings is net or gross; a gross figure also needs an undiscounted VAT calculation. ([MockFuelTransactionGenerator.cs](src/FleetCards.Application/MockFuelTransactionGenerator.cs#L61), [TransactionReportingService.cs](src/FleetCards.Application/TransactionReportingService.cs#L76), [TransactionReportingService.cs](src/FleetCards.Application/TransactionReportingService.cs#L146), [Models.cs](src/FleetCards.Contracts/Models.cs#L119), [requirements.md](context/foundation/requirements.md#L59))
- The existing selected-month signal is shared by report and summary requests, and `moveSelectedMonth` handles month changes. Spend Overview displays the selected month but has no controls; `ShellComponent` loads the summary on overview construction or retry. Overview arrows must move the shared month and trigger a summary reload. The report view already provides previous/next month controls. ([transaction-report.service.ts](web/src/app/transaction-report.service.ts#L59), [transaction-report.service.ts](web/src/app/transaction-report.service.ts#L89), [shell.component.ts](web/src/app/shell.component.ts#L47), [shell.component.ts](web/src/app/shell.component.ts#L88), [shell.component.html](web/src/app/shell.component.html#L48), [transaction-report.component.html](web/src/app/transaction-report.component.html#L8))

## Architecture Insights

Card state mutations currently share the audit-event mechanism, while assignment periods have their own append-only event model and API response. The requested state timeline can build from state audit actions, but the existing audit action records the destination state rather than an explicit previous/next-state pair. The S-04 report service is the shared point for effective transaction selection and money calculations; keeping the savings aggregate there would preserve the existing server-authoritative totals pattern.

## Historical Context (from prior changes)

- S-03's plan and the current `requirements.md` explicitly authorize immediate reassignment. That history accurately describes the shipped behavior; the new user request supersedes it by changing the product invariant. ([plan.md](context/changes/issue-and-assign-virtual-cards/plan.md#L15), [requirements.md](context/foundation/requirements.md#L46))
- S-04's plan defines gross paid after discount as the primary spend amount, with net and VAT available separately. Current overview bindings follow that model; the requested labels and savings metric extend or rename the existing presentation rather than revealing a mismatch in the current gross calculation. ([plan.md](context/changes/review-generated-card-spending/plan.md#L16), [shell.component.html](web/src/app/shell.component.html#L60))
- The inspected S-03 and S-04 change folders contain plans and reviews but no `research.md` artifacts.

## Related Research

No related research artifact was present in the active change folder before this report. The S-03 and S-04 plans above provide the relevant historical decisions.

## Open Questions

- For “Total savings”, should the displayed result be net or gross, and should refunds/cancellations reverse savings using their signed correction values?
- Should state history include an initial Active entry at issuance, or only subsequent status changes?
- For the Active-after-refresh report, what does `GET /api/cards` return immediately after the refresh, and was the API process restarted?
- Should pre-existing cards with a null `AssignedCarId` be treated differently from cards issued under the new immutable-assignment rule?

## Verification Scope

Read-only source and prior-plan review. No build or tests were run. The code graph snapshot predates the current commit and marks candidate files as metadata-changed; `web/src/app/card-management.component.html` also has a recorded parse gap at line 51. Findings use direct reads of the current source, but the refresh symptom remains unverified without a runtime request/DOM observation.
