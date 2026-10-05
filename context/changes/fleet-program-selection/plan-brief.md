# Fleet Program Selection: Plan Brief

Full plan: [plan.md](./plan.md). Framing: [frame.md](./frame.md).

**Goal:** Let clients compare four mock fleet programs on past transactions and schedule a future-dated switch, with history, program-driven discounts and a demo-data admin page.

**Phases:** 1 data model and migration; 2 discount resolution; 3 comparison API; 4 assignment API; 5 Programs UI; 6 demo generator; 7 e2e and snapshots.

**Key decisions:** per-litre `Discount` kept (`round(unitPrice × percent, 4)`); what-if on report rules in Europe/Warsaw, ranked by gross; a switch replaces a pending one and must be in the future; the demo generator takes a range and count over all active cards; one-time zero-discount migration.

**Risks:** the migration rewrites seeded totals; the issuance path backdates transactions; the Seeded and EF paths must both change; the Programs visual snapshots change.
