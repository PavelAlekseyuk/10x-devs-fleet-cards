# Program Selection Fixes Implementation Plan

## Overview

Fix four defects on the Programs page and program selection dialog: swapped program names (the active program is labelled "Nebula Basic" but behaves like a no-discount program), wrong default active program, inverted discount/surcharge sign display, and a comparison table that cannot be sorted.

## Current State Analysis

- Stored `FleetCardProgramRule.Percent` is a **discount** (positive = cheaper): `ProgramComparisonService` and `MockFuelTransactionGenerator` compute `discount = unitPrice * percent` and subtract it from the net total.
- Program `…555` is named "Nebula Basic" and carries Diesel +10% / petrol −2%; program `…556` is named "Orion Diesel" and carries 0% on every fuel. The seeded client assignment points at `…555`, so the UI header says "Nebula Basic" while comparison numbers look like a 0%-discount program.
- `formatPercent` in `programs.component.ts` prints the stored value as-is, so a 9% discount shows as "+9%".
- The comparison table in the change dialog has no sorting; the backend returns rows ordered by gross total, then name.
- Demo transactions are generated at runtime (card issuance / `GenerateDemoTransactionsAsync`), not seeded, using the program assignment active at `OccurredAt`.

## Desired End State

- "Nebula Basic" is the 0%-on-everything program and is the seeded default active program. "Orion Diesel" carries the Diesel +10% / petrol −2% rules.
- Programs page shows discounts as negative (`-9%`), surcharges as positive (`+2%`), zero as `0%`.
- Comparison table columns "Hypothetical cost · Net", "Hypothetical cost · Gross", "Net savings", "Gross savings" are sortable; baseline row stays on top.
- Existing databases are corrected by an EF migration; old demo transactions are deleted so they can be regenerated against the corrected program.

### Key Discoveries:

- Names appear in `src/FleetCards.Infrastructure/FleetCardsDbContext.cs` (seed, ~line 376 and assignment ~line 552), `src/FleetCards.Application/FleetCardsApplication.cs` (`SeededProgramResolutionDataSource` rules ~line 130, `GetPrograms()` ~line 247), and migration `20261005172757_AddFleetProgramSelection`.
- Sign convention lives in `web/src/app/programs.component.ts` `formatPercent` (~line 223); the backend and stored data stay unchanged.
- Test fixtures referencing names: `tests/FleetCards.Api.Tests/FleetCardsMigrationTests.cs` (~line 145), `web/src/app/programs.component.spec.ts`.

## What We're NOT Doing

- Not changing stored `Percent` sign semantics or the cost calculation.
- Not regenerating transactions inside the migration (SQL cannot use the C# generator); they are regenerated via the existing demo-generation action or card issuance.
- Not changing backend ordering of the comparison response (default order is retained as the unsorted state).
- Not adding sorting to the programs list or assignment history tables.

## Implementation Approach

Swap names while IDs and rules stay put: `…555` becomes "Orion Diesel", `…556` becomes "Nebula Basic", and the seeded assignment is re-pointed to `…556`. Ship as a new EF migration so existing databases update. Frontend changes are display-only and client-side.

## Phase 1: Backend — program names and default active program

### Overview

Correct names and the default assignment in seed data, the in-memory seeded source, and existing databases.

### Changes Required:

#### 1. Seed data and in-memory source

**File**: `src/FleetCards.Infrastructure/FleetCardsDbContext.cs`, `src/FleetCards.Application/FleetCardsApplication.cs`

**Intent**: Rename program `…555` to "Orion Diesel" and `…556` to "Nebula Basic"; point the seeded client assignment (`aaaaaaaa-0000-0000-0000-000000000001`) at `…556`. Keep the in-memory `SeededProgramResolutionDataSource` consistent (identifiers, rules and `GetPrograms()` names).

**Contract**: ID→name→rules mapping becomes `…555` Orion Diesel (Diesel 10%, petrol −2%), `…556` Nebula Basic (all 0%); default assignment ProgramId = `…556`.

#### 2. EF migration for existing databases

**File**: `src/FleetCards.Infrastructure/Migrations/<timestamp>_FixProgramNamesAndDefaultAssignment.cs` (+ Designer, updated `FleetCardsDbContextModelSnapshot.cs`)

**Intent**: Update the two program names, re-point the seeded assignment row to `…556`, and delete existing transactions so demo data can be regenerated against the corrected active program.

**Contract**: `UpdateData` on `Programs` (Name) and `ClientProgramAssignments` (ProgramId for the seeded assignment Id); `DELETE FROM` the transactions table. Down migration reverses names and assignment; deleted transactions are not restorable (documented in a comment). Deleting transactions runs against every database the migration touches — accepted for this demo-only deployment per the planning decision.

#### 3. Tests

**File**: `tests/FleetCards.Api.Tests/FleetCardsMigrationTests.cs`, `tests/FleetCards.Api.Tests/ProgramApiTests.cs`, `tests/FleetCards.Application.Tests/ProgramResolutionTests.cs`, `tests/FleetCards.Application.Tests/FleetCardServiceTests.cs`

**Intent**: Cover the migration (names swapped, assignment re-pointed, transactions cleared), the default active program resolving to 0%, and generated transactions applying the resolved percent at their occurrence time.

**Contract**: Generated transactions assert `discount == round(unitPrice * resolvedPercent, 4)`. A zero-percent Nebula Basic rule therefore produces a zero discount; tests retain the existing deterministic period and total assertions without requiring a positive discount.

### Success Criteria:

#### Automated Verification:

- Solution builds: `dotnet build`
- Tests pass: `dotnet test`
- Migration test asserts swapped names, default assignment on Nebula Basic and empty transactions after upgrade

#### Manual Verification:

- After running AppHost on an existing database, the Programs page shows "Nebula Basic" as active
- After regenerating demo transactions, comparison baseline equals the "Nebula Basic" row (zero savings)

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase.

---

## Phase 2: Frontend — discount and surcharge signs

### Overview

Display stored discount values with the user-facing sign convention.

### Changes Required:

#### 1. Percent formatter

**File**: `web/src/app/programs.component.ts`, `web/src/app/programs.component.html`

**Intent**: Show the negated stored value on program rule rows: discounts negative, surcharges positive, zero unsigned.

**Contract**: `formatPercent(value)` renders `-9%` for stored `0.09`, `+2%` for stored `-0.02`, `0%` for `0`, keeping existing locale handling.

#### 2. Spec updates

**File**: `web/src/app/programs.component.spec.ts`

**Intent**: Update fixtures for swapped names and assert the new sign display, including the zero case.

### Success Criteria:

#### Automated Verification:

- Angular tests pass: `npm test` (from `web/`)
- Build passes: `npm run build` (from `web/`)
- Literal-colour check reports no matches: `rg -n -i --glob '*.scss' --glob '*.html' --glob '*.ts' '(#[0-9a-f]{3,8}\b|\b(?:rgba?|hsla?)\s*\()' web/src/app`

#### Manual Verification:

- Andromeda Petrol shows petrol as `+…` only when it is a surcharge and `-9%` when it is a discount
- Zero-percent rules show `0%`

---

## Phase 3: Frontend — sortable comparison table

### Overview

Allow sorting program rows in the comparison table by the four numeric columns.

### Changes Required:

#### 1. Sort state and header controls

**File**: `web/src/app/programs.component.ts`, `web/src/app/programs.component.html`, `web/src/app/programs.component.scss`, language text source used by `language.text`

**Intent**: Make the four numeric column headers buttons that cycle ascending → descending → default (backend) order; expose `aria-sort` on the active header; keep the baseline row pinned at the top; sort only the program rows client-side.

**Contract**: Sort key ∈ {hypotheticalNetTotal, hypotheticalGrossTotal, netSavings, grossSavings} plus direction; unsorted state uses the response order. Reuse design tokens from `web/src/styles.scss` and shared UI primitives; no literal colours. Sort state resets when a new comparison loads. Add translated accessible labels for the sort controls.

#### 2. Spec tests

**File**: `web/src/app/programs.component.spec.ts`

**Intent**: Test cycle order, `aria-sort` values, baseline row pinned, and that radio selection still works after sorting.

### Success Criteria:

#### Automated Verification:

- Angular tests pass: `npm test` (from `web/`)
- Build passes: `npm run build` (from `web/`)
- Literal-colour check reports no matches (same command as Phase 2)

#### Manual Verification:

- Clicking each of the four headers sorts ascending, then descending, then restores default order
- Baseline row stays on top; selecting a program after sorting selects the correct program
- Keyboard and screen-reader behaviour of the sortable headers is acceptable

---

## Testing Strategy

### Unit Tests:

- Migration test (name swap, assignment, transaction deletion)
- Program resolution default returns 0% for the seeded client
- Generated transactions use the resolved percent at `OccurredAt`, permitting a zero discount for the default Nebula Basic program while retaining deterministic period and total checks
- `formatPercent` sign cases: negative, positive, zero
- Sort cycle, direction, tie handling

### Integration Tests:

- `ProgramApiTests` returns the swapped names with correct rules

### Manual Testing Steps:

1. Start AppHost, open Programs: active program is Nebula Basic with all 0%
2. Regenerate demo transactions, open Change program: baseline and Nebula Basic match
3. Check Orion Diesel and Andromeda Petrol sign display
4. Sort each column in the comparison table

## Performance Considerations

Client-side sort over at most a handful of programs; no concerns.

## Migration Notes

The migration deletes all transactions (append-only rule intentionally bypassed for demo data). Regenerate demo data afterwards via the demo-generation action or card issuance. Down migration restores names and assignment but not transactions.

## References

- Similar implementation: `web/src/app/programs.component.ts` (`formatPercent`), `src/FleetCards.Application/ProgramComparisonService.cs`
- Prior change: `context/changes/fleet-program-selection/`

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append ` — <commit sha>` when a step lands. Do not rename step titles. See `references/progress-format.md`.

### Phase 1: Backend — program names and default active program

#### Automated

- [x] 1.1 Solution builds: `dotnet build`
- [x] 1.2 Tests pass: `dotnet test`
- [x] 1.3 Migration test asserts swapped names, default assignment on Nebula Basic and empty transactions after upgrade

#### Manual

- [x] 1.4 After running AppHost on an existing database, the Programs page shows "Nebula Basic" as active
- [x] 1.5 After regenerating demo transactions, comparison baseline equals the "Nebula Basic" row (zero savings)

### Phase 2: Frontend — discount and surcharge signs

#### Automated

- [ ] 2.1 Angular tests pass: `npm test` (from `web/`)
- [ ] 2.2 Build passes: `npm run build` (from `web/`)
- [ ] 2.3 Literal-colour check reports no matches

#### Manual

- [ ] 2.4 Discounts show as negative, surcharges as positive (e.g. Andromeda Petrol petrol `-9%`)
- [ ] 2.5 Zero-percent rules show `0%`

### Phase 3: Frontend — sortable comparison table

#### Automated

- [ ] 3.1 Angular tests pass: `npm test` (from `web/`)
- [ ] 3.2 Build passes: `npm run build` (from `web/`)
- [ ] 3.3 Literal-colour check reports no matches

#### Manual

- [ ] 3.4 Each of the four headers sorts ascending, then descending, then restores default order
- [ ] 3.5 Baseline row stays on top; selecting a program after sorting selects the correct program
- [ ] 3.6 Keyboard and screen-reader behaviour of the sortable headers is acceptable
