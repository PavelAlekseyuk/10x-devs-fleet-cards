---
applyTo: "web/src/**/*.ts,web/src/**/*.html"
description: "Angular frontend conventions for FleetCards"
---

# Angular frontend conventions

- Standalone components only — do not introduce `NgModule`s. Bootstrap is via `bootstrapApplication` in [web/src/app/app.config.ts](../../web/src/app/app.config.ts).
- Component selectors are prefixed `app-`.
- TypeScript strict mode is on (`strict`, `noImplicitOverride`, `noPropertyAccessFromIndexSignature`, `noImplicitReturns`, `noFallthroughCasesInSwitch`) — don't relax these in `tsconfig.json`.
- Styles are per-component SCSS (`inlineStyleLanguage: scss`), not global CSS.
- Routing and `HttpClient` are already configured in `app.routes.ts` and `app.config.ts`; extend those existing boundaries rather than hardcoding feature data in the shell.
- English is the baseline UI language; Polish translations belong in Angular i18n/resources, not hardcoded strings, per [context/foundation/decisions.md](../../context/foundation/decisions.md).
- Formatting is enforced via Prettier (100 print width, single quotes, Angular parser for templates) — don't hand-format against these settings.
- Use the Angular CLI for project commands (`ng build`, `ng test` or the corresponding `npm` scripts). The configured Angular CLI unit-test builder runs Vitest, not Karma/Jasmine; use Vitest globals and colocate `*.spec.ts` next to the component under test, following existing specs such as [web/src/app/app.spec.ts](../../web/src/app/app.spec.ts).
