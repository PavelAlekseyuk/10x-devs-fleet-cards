const { defineConfig } = require('@playwright/test');
const { randomBytes } = require('node:crypto');
const { existsSync } = require('node:fs');
const { resolve } = require('node:path');

const repositoryDirectory = resolve(__dirname, '..');
const envFile = resolve(__dirname, '.env.e2e');
if (existsSync(envFile)) process.loadEnvFile(envFile);

const appHostProject = resolve(
  repositoryDirectory,
  'src/FleetCards.AppHost/FleetCards.AppHost.csproj',
);
const databaseName = `fleetcards_e2e_${randomBytes(16).toString('hex')}`;
const authFile = resolve(__dirname, 'playwright/.auth/user.json');
const existingSpecs =
  /(?:card-management|fleet-vehicles|palette-contrast|portal-visual|program-selection|site-metadata|transaction-report)\.spec\.ts$/;

module.exports = defineConfig({
  testDir: './e2e',
  fullyParallel: false,
  workers: 1,
  reporter: 'list',
  metadata: { databaseName },
  use: {
    baseURL: 'http://127.0.0.1:4200',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
  },
  projects: [
    { name: '', testMatch: existingSpecs },
    { name: 'setup', testMatch: /auth\.setup\.ts/ },
    {
      name: 'chromium',
      testIgnore: existingSpecs,
      use: { browserName: 'chromium', storageState: authFile },
      dependencies: ['setup'],
    },
  ],
  webServer: {
    command: `aspire run --isolated --apphost "${appHostProject}" --non-interactive --nologo`,
    cwd: repositoryDirectory,
    url: 'http://127.0.0.1:4200/api',
    timeout: 180_000,
    reuseExistingServer: false,
    env: {
      FLEETCARDS_E2E_DATABASE: databaseName,
    },
  },
});