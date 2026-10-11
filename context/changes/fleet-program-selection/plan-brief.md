# Fleet Program Selection: Plan Brief

Full plan: [plan.md](./plan.md). Framing: [frame.md](./frame.md).

**Goal:** Let clients compare four mock fleet programs on past transactions and schedule a future-dated switch or end without a successor, with assignment gaps, history, program-driven discounts and a demo-data admin page.

**Phases:** 1 data model and migration; 2 discount resolution; 3 comparison API; 4 assignment API; 5 Programs UI; 6 demo generator; 7 e2e and snapshots.

**Key decisions:** percentages are fractional decimals (10% = `0.10`); per-litre `Discount` kept (`round(unitPrice × percent, 4)`); comparison mirrors transaction-level net/VAT/gross rounding and uses each transaction's VAT rate; baseline is current actual transaction amounts (including stored discounts), with per-program net/gross savings = baseline minus hypothetical; what-if on report rules in Europe/Warsaw, ranked by hypothetical gross; future switches and end-only schedules replace pending entries; assignment gaps are valid and resolve to zero discount; started assignments cannot have their client, program or start edited, while the system may set or revise a future end when a future switch or end is scheduled/replaced; Nebula Basic is the initial assignment from the earliest transaction; the demo generator takes a range and count over all active cards; one-time zero-discount migration.

**Risks:** the migration rewrites seeded totals; the issuance path backdates transactions; the Seeded and EF paths must both change; the Programs visual snapshots change.
