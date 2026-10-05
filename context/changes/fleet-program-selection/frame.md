# Frame Brief: Fleet program selection (MVP showcase)

> Framing step before /10x-plan. Separates what is actually at issue from what was assumed.

## Reported Observation

The Programs view is a placeholder. A client needs to pick the most suitable fleet-card program: for a selectable past period (e.g. last month) the system calculates what the client would have paid under each available program and ranks them. A read-write user can then schedule a switch from a chosen start date/time (default now + 1 s); history of selections is kept and visible in the UI.

## Initial Framing (preserved)

- **User's stated cause or approach**: Programs are third-party mock data (4 cosmos-named programs with per-fuel-type percentages); the history of selection plus a what-if comparison is all that is missing.
- **User's proposed direction**: Mock 4 programs, record selection history (initialised from the first transaction), comparison popup sorted by total cost, switch with a future start, remove the "add program" button, apply the new discounts to transactions generated after the switch.
- **Pre-dispatch narrowing** (user answers):
  - Comparison base: recompute from POS price, `qty × unit price × (1 − program %)` for every transaction; the discount actually applied is ignored.
  - Display both net and gross, rank by gross.
  - Newly generated transactions (card issuance) should also take the discount from the program active at their time.
  - History starts with program (a) from the earliest existing transaction. Backdated transactions older than that get no program (POS price).

## Dimension Map

1. **Program data model** — a program is `Id/Name/ActiveFrom/ActiveTo` only. There are no per-fuel percentage rules. `FleetCardsModel.cs:103`. The framing assumes rules exist.
2. **Selection history** — there is no `ClientProgramAssignment` entity, API or table, even though domain-model.md:12 and :33-34 specify it. The framing assumes it can be "verified"; it must be built.
3. **Discount semantics on transactions** — `FuelTransaction.Discount` is an absolute per-litre amount, e.g. 0.18 PLN/L (`FleetCardsModel.cs:95`, net = `qty × (unitPrice − discount)` at `TransactionReportingService.cs:150`). Programs are percentages. Applying a program means converting percent to per-litre, or changing semantics.
4. **Transaction generation timing** — `MockFuelTransactionGenerator.Generate` creates transactions at card issuance, but with `OccurredAt` randomly in the previous calendar month, and with a random discount of 0.05-0.30/L (`MockFuelTransactionGenerator.cs:60-75`). "Generated after the switch" is therefore not the same as "occurred after the switch". Program lookup has to be by `OccurredAt`.
5. **Transaction immutability** — transactions and history are append-only (decisions.md #6, #7, #12, domain-model.md:32). Changing discounts on existing rows is forbidden, so a switch affects only rows created afterwards.
6. **UI/permission surface** — the Programs section is a one-row placeholder (`shell.component.html:102`); the route is guarded as read-write (`app.routes.ts:12`, `app.routes.spec.ts:9`). The "add program" button is not visible in the Programs row template, so the removal may be moot. To confirm during planning.

## Hypothesis Investigation

| Hypothesis | Evidence | Verdict |
| --- | --- | --- |
| Program rules already exist, only history and UI are missing (initial framing) | `FleetCardProgram` has no rules; one seeded program "Baltic Standard" (`FleetCardsDbContext.cs`, seed) | NONE |
| History/assignment model is missing and must be built (explicit in docs) | No entity or table; domain-model.md:12, :33, :43; roadmap.md:216 lists it as parked scope | STRONG |
| Percent vs per-litre discount mismatch hides a calculation/semantics decision | `Discount` HasPrecision(18,4) per litre; generator randomises it; seeded value 0.18 | STRONG |
| Generation time vs occurrence time breaks "applies after switch" | Generator backdates into the previous month (`MockFuelTransactionGenerator.cs`) | STRONG |
| UI already contains an add-program button to remove | Not found in `shell.component.html` program section; only the "compare programs" link at :90 | WEAK |

## Narrowing Signals

- The user wants the comparison to recompute from POS price, so the existing `Discount` per litre is irrelevant to the what-if. That removes the need for a "current actual" baseline in the popup.
- The user wants issuance-time transactions to take the program active at their `OccurredAt`. Because all mock transactions are in the past, a future-dated switch can never be observed in generated data; only a switch with a past or "now" start can. This is a showcase limitation worth stating up front.
- History initialises with program (a) from the earliest transaction. A new card can later add transactions older than that start, and these get no discount. This is an accepted edge case.

## Cross-System Convention

The requirements already describe the intended design: programs are read-only seeded JSON with per-fuel percentages, negative percent is a premium, client-level assignments are effective-dated and non-overlapping, with zero or one program at an instant (requirements.md §6-7, domain-model.md:33-34, decisions.md #5, #7, #12). Prior art in this repo matches the user's framing; only the code is missing. Open question #2 in prd.md:127 ("exact comparison formula") is answered by the user's narrowing choices above, so FR-008 can be un-parked.

## Reframed (or Confirmed) Problem Statement

> **The actual problem to plan around is**: introduce the missing program domain, meaning percentage rules per fuel type and an effective-dated client assignment history, and resolve discounts by transaction `OccurredAt`; the comparison popup and switch flow are thin layers over that.

The initial framing was largely correct, but it understated the work. This is a data-model change (rules, assignments, migration and seed), not just a UI feature. The two real design decisions are the percent-to-per-litre conversion on stored transactions and the mismatch between generation time and occurrence time.

## Confidence

**MEDIUM** — the code and docs are consistent and the user's answers are decisive. Remaining open points (below) are small and can be settled in /10x-plan.

## What Changes for /10x-plan

Plan the data model first (programs with fuel rules, client assignment history, seed of 4 programs plus the initial assignment, migration), then the comparison query, the switch endpoint (read-write only, future-or-now start, overlap rejection) and the UI. Treat applying discounts in the generator as a separate, explicit step.

## Decisions from the discussion (2026-10-05)

1. **Discount calculation.** The what-if comparison is computed on the fly from the POS price using program percentages. It is never stored. For stored transactions, the program whose period contains the transaction's `OccurredAt` supplies the percentage. If no program covers that instant, the discount is 0 and the POS price applies. Programs have non-overlapping periods of activity.
2. **Demo data.** Open. The user suggests an admin/demo panel that generates random transactions for a chosen period (see below).
3. **"Add new +" button.** It is the shared `addNew` button in the Programs section intro (`shell.component.html:101`, `language.service.ts:47`). It is removed for Programs and replaced by "Change program".
4. **Seed.** "Baltic Standard" is replaced by the four mock programs; "no discounts" is the initial assignment. Existing transactions get discount 0 (see the immutability caveat below).
5. **Switch start.** A new program can start only in the future and must not intersect any previously selected period. The default start is now + 1 s.

## Resolved (2026-10-05, second round)

- Zeroing discounts on existing transactions (with net/VAT/gross recompute) is accepted as a one-time migration.
- Demo transactions: build an admin panel (read-write) that generates random demo transactions for a chosen period. Plan it as its own slice.
- Pending future switch: replacing it is allowed; the MVP changes current behaviour directly, with no feature flags.

## Remaining Open Questions for /10x-plan (the first three bullets and the demo-generation item are resolved above)

- **Immutability caveat.** Zeroing the discount on existing transactions modifies stored facts (decisions.md #6, domain-model.md:32). Treat it as a one-time seed/migration reset, like decision #18, and recompute `NetTotal`, `VatAmount` and `GrossTotal` in the same step. Confirm that is acceptable.
- **Percent to stored amount.** `FuelTransaction.Discount` is per-litre. Decide whether the generator stores `unitPrice × percent` (keeping the existing column and reporting formula) or adds a percent column. Rounding follows decisions.md #11.
- **Fuel mapping.** Petrol means 95, 98, 95+ and 98+; diesel means Diesel and Diesel+. Program (b) gives 10% on Diesel and Diesel+ and -2% on petrol, so the rules need an explicit per-fuel-type table, including premiums as negative percent.
- **Comparison scope.** Which timezone defines "last month", and which statuses count (existing report rules: Completed and Recalculated)?
- **Pending switch.** A future assignment can be replaced before it starts (decisions.md #12). Does replacing it count as "intersecting"? I'd say it does not.
- **Existing-period gap.** The initial program starts at the earliest existing transaction. Later backdated transactions before that point pay the POS price.
- **Demo transaction generation (item 2).** Three options:
  - (a) A read-write "Generate demo transactions" action for a chosen date range on the Programs or Cards screen. It is the closest to your suggestion, and the generator already supports deterministic creation.
  - (b) Extend the card-issuance generator to accept a period. It is smaller but hidden.
  - (c) Do nothing: the showcase shows history and the what-if, and discounts apply to only new cards.
  I'd recommend (a), scoped as its own slice so it can be cut. It also lets a future-dated switch be demonstrated once its start has passed.

## References

- `src/FleetCards.Domain/FleetCardsModel.cs:79-109`
- `src/FleetCards.Infrastructure/FleetCardsDbContext.cs` (seed, program mapping)
- `src/FleetCards.Application/MockFuelTransactionGenerator.cs`
- `src/FleetCards.Application/TransactionReportingService.cs:150`
- `src/FleetCards.Api/Program.cs:572`
- `web/src/app/shell.component.html:87-102`
- `context/foundation/{prd,requirements,domain-model,decisions,roadmap}.md`
