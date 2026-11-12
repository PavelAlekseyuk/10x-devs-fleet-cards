import { expect, test, type Page } from '@playwright/test';

interface ApiResponse<T> {
  status: number;
  body: T | null;
}

interface FleetVehicle {
  isArchived: boolean;
}

interface FleetCard {
  state: string;
}

interface TransactionReportRow {
  id: string;
  occurredAtUtc: string;
  userOccurredAt: string;
  posOccurredAtLocal: string;
  posTimeZoneId: string;
  vehiclePlate: string;
  stationName: string;
  quantity: number;
  unitPrice: number;
  netTotal: number;
  vatRate: number;
  vatAmount: number;
  grossTotal: number;
}

interface TransactionReportTotals {
  netTotal: number;
  vatTotal: number;
  grossTotal: number;
  grossSavingsTotal: number;
  rowCount: number;
}

interface TransactionReport {
  year: number;
  month: number;
  timeZoneId: string;
  transactions: TransactionReportRow[];
  totals: TransactionReportTotals;
}

interface DashboardSummary {
  activeVehicleCount: number;
  activeCardCount: number;
  periodTotals: TransactionReportTotals;
}

const reportingMonth = '2026-09';
const defaultTimeZone = 'Europe/Warsaw';
const alternateTimeZone = 'America/Los_Angeles';
async function signIn(page: Page, userName: string, password: string): Promise<void> {
  await page.goto('/login');
  await page.getByLabel('Username').fill(userName);
  await page.getByLabel('Password').fill(password);
  const loginResponsePromise = page.waitForResponse(
    response => new URL(response.url()).pathname === '/api/auth/login',
  );
  await page.getByRole('button', { name: 'Sign in' }).click();
  expect((await loginResponsePromise).status()).toBe(200);
  await expect(page).toHaveURL(/\/overview$/);
}

async function verifyDatabaseIdentity(page: Page, expectedDatabaseName: string): Promise<void> {
  expect(expectedDatabaseName).toMatch(/^fleetcards_e2e_[0-9a-f]{32}$/);
  const identity = await page.evaluate(async () => {
    const response = await fetch('/api/e2e/database');
    return { status: response.status, body: await response.json() };
  });
  expect(identity).toEqual({ status: 200, body: { databaseName: expectedDatabaseName } });
}

async function requestApi<T>(
  page: Page,
  path: string,
  method = 'GET',
): Promise<ApiResponse<T>> {
  const response = await page.evaluate(
    async ({ requestPath, requestMethod }) => {
      const headers: Record<string, string> = {};
      if (requestMethod !== 'GET') {
        const csrfCookie = document.cookie
          .split('; ')
          .find(cookie => cookie.startsWith('XSRF-TOKEN='))
          ?.slice('XSRF-TOKEN='.length);
        headers['X-XSRF-TOKEN'] = decodeURIComponent(csrfCookie ?? '');
      }

      const response = await fetch(requestPath, { method: requestMethod, headers });
      return {
        status: response.status,
        body: await response.json().catch(() => null),
      };
    },
    { requestPath: path, requestMethod: method },
  );
  return { status: response.status, body: response.body as T | null };
}

function reportPath(endpoint: string, month: string, timeZone: string): string {
  const [year, monthNumber] = month.split('-');
  const query = new URLSearchParams({ year, month: String(Number(monthNumber)), timeZone });
  return `${endpoint}?${query}`;
}

function cents(value: number): number {
  return Math.sign(value) * Math.round(Math.abs(value) * 100);
}

function expectSummaryToReconcile(summary: DashboardSummary, report: TransactionReport): void {
  expect(summary.periodTotals.rowCount).toBe(report.transactions.length);
  expect(cents(summary.periodTotals.netTotal)).toBe(
    report.transactions.reduce((total, row) => total + cents(row.netTotal), 0),
  );
  expect(cents(summary.periodTotals.vatTotal)).toBe(
    report.transactions.reduce((total, row) => total + cents(row.vatAmount), 0),
  );
  expect(cents(summary.periodTotals.grossTotal)).toBe(
    report.transactions.reduce((total, row) => total + cents(row.grossTotal), 0),
  );
  expect(cents(summary.periodTotals.grossSavingsTotal)).toBe(
    cents(report.totals.grossSavingsTotal),
  );
}

function expectNoSensitiveCardData(report: TransactionReport): void {
  const serialized = JSON.stringify(report);
  expect(serialized).not.toMatch(/"(?:pan|fullPan|cardNumber|cvv|pin)"/i);
  expect(serialized).not.toMatch(/\b\d{13,19}\b/);
}

function formatUserTime(value: string, timeZone: string): string {
  return new Intl.DateTimeFormat('en-GB', {
    dateStyle: 'medium',
    timeStyle: 'short',
    timeZone,
  }).format(new Date(value));
}

function formatPointOfSaleTime(value: string): string {
  const wallClock = value.slice(0, 19);
  return new Intl.DateTimeFormat('en-GB', {
    dateStyle: 'medium',
    timeStyle: 'short',
    timeZone: 'UTC',
  }).format(new Date(`${wallClock}Z`));
}

async function waitForReportResponse(
  page: Page,
  month: string,
  timeZone: string,
): Promise<TransactionReport> {
  const [year, monthNumber] = month.split('-');
  const response = await page.waitForResponse(candidate => {
    const url = new URL(candidate.url());
    return (
      url.pathname === '/api/transactions/report' &&
      url.searchParams.get('year') === year &&
      Number(url.searchParams.get('month')) === Number(monthNumber) &&
      url.searchParams.get('timeZone') === timeZone
    );
  });
  expect(response.status()).toBe(200);
  return (await response.json()) as TransactionReport;
}

async function waitForSummaryResponse(
  page: Page,
  month: string,
  timeZone: string,
): Promise<DashboardSummary> {
  const [year, monthNumber] = month.split('-');
  const response = await page.waitForResponse(candidate => {
    const url = new URL(candidate.url());
    return (
      url.pathname === '/api/dashboard/summary' &&
      url.searchParams.get('year') === year &&
      Number(url.searchParams.get('month')) === Number(monthNumber) &&
      url.searchParams.get('timeZone') === timeZone
    );
  });
  expect(response.status()).toBe(200);
  return (await response.json()) as DashboardSummary;
}

test('Aspire transaction reporting reconciles generated spending and timezone display', async ({
  page,
}) => {
  await page.clock.install({ time: new Date('2026-10-01T12:00:00.000Z') });
  await page.goto('/login');

  const anonymousRequests = await Promise.all([
    requestApi(page, reportPath('/api/transactions/report', reportingMonth, defaultTimeZone)),
    requestApi(page, reportPath('/api/dashboard/summary', reportingMonth, defaultTimeZone)),
    requestApi(page, '/api/e2e/database'),
  ]);
  expect(anonymousRequests.map(response => response.status)).toEqual([401, 401, 401]);

  await signIn(page, 'anna.nowak', 'Anna123!');
  await verifyDatabaseIdentity(page, String(test.info().config.metadata['databaseName']));

  const existingReportResponse = await requestApi<TransactionReport>(
    page,
    reportPath('/api/transactions/report', reportingMonth, defaultTimeZone),
  );
  expect(existingReportResponse.status).toBe(200);

  await page.goto('/admin/demo');
  await page.getByLabel('From date (UTC)').fill('2026-09-09');
  await page.getByLabel('Through date (UTC)').fill('2026-09-09');
  await page.getByLabel('Number of transactions').fill('1');
  await page.getByRole('button', { name: 'Generate transactions' }).click();
  await expect(
    page.getByRole('status').filter({ hasText: 'Demo transactions generated: 1' }),
  ).toBeVisible();
  await page.goto('/overview');

  const [vehiclesResponse, cardsResponse, defaultSummaryResponse, defaultReportResponse] =
    await Promise.all([
      requestApi<FleetVehicle[]>(page, '/api/cars'),
      requestApi<FleetCard[]>(page, '/api/cards'),
      requestApi<DashboardSummary>(
        page,
        reportPath('/api/dashboard/summary', reportingMonth, defaultTimeZone),
      ),
      requestApi<TransactionReport>(
        page,
        reportPath('/api/transactions/report', reportingMonth, defaultTimeZone),
      ),
    ]);
  expect(vehiclesResponse.status).toBe(200);
  expect(cardsResponse.status).toBe(200);
  expect(defaultSummaryResponse.status).toBe(200);
  expect(defaultReportResponse.status).toBe(200);

  const activeVehicleCount = (vehiclesResponse.body ?? []).filter(vehicle => !vehicle.isArchived)
    .length;
  const activeCardCount = (cardsResponse.body ?? []).filter(card => card.state === 'Active').length;
  const defaultSummary = defaultSummaryResponse.body!;
  const defaultReport = defaultReportResponse.body!;
  expect(defaultSummary.activeVehicleCount).toBe(activeVehicleCount);
  expect(defaultSummary.activeCardCount).toBe(activeCardCount);
  expectSummaryToReconcile(defaultSummary, defaultReport);
  expectNoSensitiveCardData(defaultReport);

  const existingTransactionIds = new Set(
    existingReportResponse.body!.transactions.map(row => row.id),
  );
  const newlyGeneratedRows = defaultReport.transactions.filter(
    row => !existingTransactionIds.has(row.id),
  );
  expect(newlyGeneratedRows).toHaveLength(1);
  const generatedRow = newlyGeneratedRows[0];
  expect(generatedRow).toMatchObject({
    vehiclePlate: 'WA 12345',
    posTimeZoneId: defaultTimeZone,
  });

  await expect(
    page.locator('.metric-card').filter({ hasText: 'Fleet vehicles' }).locator('strong'),
  ).toHaveText(String(activeVehicleCount));
  await expect(
    page.locator('.metric-card').filter({ hasText: 'Active cards' }).locator('strong'),
  ).toHaveText(String(activeCardCount));
  await expect(page.locator('.spend-panel .spend-total strong')).toHaveText(
    new Intl.NumberFormat('en-GB', { style: 'currency', currency: 'PLN' }).format(
      defaultSummary.periodTotals.grossTotal,
    ),
  );
  await expect(
    page.locator('.metric-card').filter({ hasText: 'Gross savings' }).locator('strong'),
  ).toHaveText(
    new Intl.NumberFormat('en-GB', { style: 'currency', currency: 'PLN' }).format(
      defaultSummary.periodTotals.grossSavingsTotal,
    ),
  );

  const nextMonth = '2026-10';
  const nextSummaryResponse = waitForSummaryResponse(page, nextMonth, defaultTimeZone);
  await page.getByRole('button', { name: 'Next month' }).click();
  const nextSummary = await nextSummaryResponse;
  await expect(
    page.locator('.metric-card').filter({ hasText: 'Gross savings' }).locator('strong'),
  ).toHaveText(
    new Intl.NumberFormat('en-GB', { style: 'currency', currency: 'PLN' }).format(
      nextSummary.periodTotals.grossSavingsTotal,
    ),
  );
  await expect(page.locator('.period-link')).toContainText('October 2026');

  const nextUiReportResponse = waitForReportResponse(page, nextMonth, defaultTimeZone);
  await page.getByRole('link', { name: /View transactions for this period/ }).click();
  await expect(page).toHaveURL(/\/transactions$/);
  const nextUiReport = await nextUiReportResponse;
  await expect(page.getByRole('combobox', { name: 'Reporting month' })).toHaveValue('10');
  await expect(page.getByRole('textbox', { name: 'Reporting year' })).toHaveValue('2026');
  expectSummaryToReconcile(nextSummary, nextUiReport);

  const defaultUiReportResponse = waitForReportResponse(page, reportingMonth, defaultTimeZone);
  await page.getByRole('button', { name: 'Previous month' }).click();
  const defaultUiReport = await defaultUiReportResponse;
  await expect(page.getByRole('combobox', { name: 'Reporting month' })).toHaveValue('09');
  await expect(page.getByRole('textbox', { name: 'Reporting year' })).toHaveValue('2026');
  expectSummaryToReconcile(defaultSummary, defaultUiReport);
  await expect(page.locator('.report-table-wrap tbody tr')).toHaveCount(
    defaultUiReport.transactions.length,
  );
  await expect(page.getByRole('columnheader', { name: 'Your local time' })).toBeVisible();
  await expect(page.getByRole('columnheader', { name: 'Point-of-sale time' })).toBeVisible();

  const timeZoneInput = page.getByLabel('Reporting timezone');
  await expect(timeZoneInput).toHaveValue(defaultTimeZone);
  const utcReportResponse = waitForReportResponse(page, reportingMonth, 'UTC');
  await timeZoneInput.fill('UTC');
  await page.getByRole('button', { name: 'Apply timezone' }).click();
  const utcReport = await utcReportResponse;
  const utcGeneratedRow = utcReport.transactions.find(row => row.id === generatedRow.id);
  expect(utcGeneratedRow).toBeTruthy();
  expect(utcGeneratedRow!.userOccurredAt).not.toBe(generatedRow.userOccurredAt);
  expect(utcGeneratedRow!.posOccurredAtLocal).toBe(generatedRow.posOccurredAtLocal);
  const utcSummaryResponse = await requestApi<DashboardSummary>(
    page,
    reportPath('/api/dashboard/summary', reportingMonth, 'UTC'),
  );
  expect(utcSummaryResponse.status).toBe(200);
  expectSummaryToReconcile(utcSummaryResponse.body!, utcReport);
  const utcVisibleSeedRow = page
    .locator('.report-table-wrap tbody tr')
    .filter({ hasText: generatedRow.stationName });
  await expect(utcVisibleSeedRow.getByRole('cell').nth(0)).toContainText(
    formatUserTime(utcGeneratedRow!.userOccurredAt, 'UTC'),
  );
  await expect(utcVisibleSeedRow.getByRole('cell').nth(1)).toContainText(
    formatPointOfSaleTime(utcGeneratedRow!.posOccurredAtLocal),
  );

  const localReportResponse = waitForReportResponse(page, reportingMonth, alternateTimeZone);
  await timeZoneInput.fill(alternateTimeZone);
  await page.getByRole('button', { name: 'Apply timezone' }).click();
  const localReport = await localReportResponse;
  const localGeneratedRow = localReport.transactions.find(row => row.id === generatedRow.id);
  expect(localGeneratedRow).toBeTruthy();
  expect(localGeneratedRow!.userOccurredAt).not.toBe(utcGeneratedRow!.userOccurredAt);
  expect(localGeneratedRow!.posOccurredAtLocal).toBe(utcGeneratedRow!.posOccurredAtLocal);
  const localSummaryResponse = await requestApi<DashboardSummary>(
    page,
    reportPath('/api/dashboard/summary', reportingMonth, alternateTimeZone),
  );
  expect(localSummaryResponse.status).toBe(200);
  expectSummaryToReconcile(localSummaryResponse.body!, localReport);
  const localVisibleSeedRow = page
    .locator('.report-table-wrap tbody tr')
    .filter({ hasText: generatedRow.stationName });
  await expect(localVisibleSeedRow.getByRole('cell').nth(0)).toContainText(
    formatUserTime(localGeneratedRow!.userOccurredAt, alternateTimeZone),
  );
  await expect(localVisibleSeedRow.getByRole('cell').nth(1)).toContainText(
    formatPointOfSaleTime(localGeneratedRow!.posOccurredAtLocal),
  );
  await expect(localVisibleSeedRow.getByRole('cell').nth(1)).toContainText(
    localGeneratedRow!.posTimeZoneId,
  );

  const emptyMonth = '1900-01';
  const [emptyReportResponse, emptySummaryResponse] = await Promise.all([
    requestApi<TransactionReport>(
      page,
      reportPath('/api/transactions/report', emptyMonth, alternateTimeZone),
    ),
    requestApi<DashboardSummary>(
      page,
      reportPath('/api/dashboard/summary', emptyMonth, alternateTimeZone),
    ),
  ]);
  expect(emptyReportResponse.status).toBe(200);
  expect(emptySummaryResponse.status).toBe(200);
  expect(emptyReportResponse.body!.transactions).toHaveLength(0);
  expectSummaryToReconcile(emptySummaryResponse.body!, emptyReportResponse.body!);

  const invalidRequests = await Promise.all([
    requestApi(page, '/api/transactions/report?year=2026&month=13&timeZone=UTC'),
    requestApi(page, '/api/transactions/report?year=2026&month=9&timeZone=Not/A-Time-Zone'),
    requestApi(page, '/api/dashboard/summary?year=2026&month=0&timeZone=UTC'),
  ]);
  expect(invalidRequests.map(response => response.status)).toEqual([400, 400, 400]);

  await page.context().clearCookies();
  await signIn(page, 'piotr.wisniewski', 'Piotr123!');
  await verifyDatabaseIdentity(page, String(test.info().config.metadata['databaseName']));
  const readOnlyReport = await requestApi<TransactionReport>(
    page,
    reportPath('/api/transactions/report', reportingMonth, defaultTimeZone),
  );
  const readOnlySummary = await requestApi<DashboardSummary>(
    page,
    reportPath('/api/dashboard/summary', reportingMonth, defaultTimeZone),
  );
  expect(readOnlyReport.status).toBe(200);
  expect(readOnlySummary.status).toBe(200);
  expectNoSensitiveCardData(readOnlyReport.body!);
  await page.goto('/transactions');
  await expect(page.getByRole('heading', { level: 2, name: 'Transactions' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Issue card', exact: true })).toHaveCount(0);
});