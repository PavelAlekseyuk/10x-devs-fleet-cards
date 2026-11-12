# Test stack

## E2E

<!-- Written by /10x-e2e-setup. Re-run it to change this section; other skills only read it. -->

- runner: Playwright Test, @playwright/test 1.63.0
- config: web/playwright.config.ts
- single-spec command: from web, `npm run test:e2e -- seed.spec.ts`
- full-suite command: from web, `npm run test:e2e`
- base URL: http://127.0.0.1:4200
- port: 4200 (fixed by the Aspire AppHost web endpoint; E2E_PORT override is not supported)
- web server command: `aspire run --isolated --apphost "src/FleetCards.AppHost/FleetCards.AppHost.csproj" --non-interactive --nologo` from the repository root; starts the Angular development server and local API, with an isolated SQL database and `reuseExistingServer: false`
- auth setup project: setup (web/e2e/auth.setup.ts), credentials from E2E_USERNAME / E2E_PASSWORD in ignored web/.env.e2e or the environment; the seven pre-existing specs retain their original signed-out starting state
- storageState: web/playwright/.auth/user.json (gitignored); used by the chromium project, with signed-out tests opting out explicitly
- seed: web/e2e/seed.spec.ts — protects signed-out-transactions-redirect: signed-out access to transactions must reach sign-in
- browser CLI: playwright-cli, command skill at .github/skills/playwright-cli/SKILL.md
- updated: 2026-11-12
