const { defineConfig } = require('@playwright/test');
const { randomBytes } = require('node:crypto');
const { resolve } = require('node:path');

const repositoryDirectory = resolve(__dirname, '..');
const appHostProject = resolve(
  repositoryDirectory,
  'src/FleetCards.AppHost/FleetCards.AppHost.csproj',
);
const databaseName = `fleetcards_e2e_${randomBytes(16).toString('hex')}`;

module.exports = defineConfig({
  testDir: './e2e',
  fullyParallel: false,
  workers: 1,
  reporter: 'list',
  metadata: { databaseName },
  use: {
    baseURL: 'http://127.0.0.1:4200',
    trace: 'retain-on-failure',
  },
  webServer: {
    command: `aspire run --isolated --apphost "${appHostProject}" --non-interactive --nologo`,
    cwd: repositoryDirectory,
    url: 'http://127.0.0.1:4200/login',
    timeout: 180_000,
    reuseExistingServer: false,
    env: {
      FLEETCARDS_E2E_DATABASE: databaseName,
    },
  },
});