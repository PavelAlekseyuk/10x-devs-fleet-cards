import { expect, test, type Page } from '@playwright/test';

test.describe.configure({ mode: 'serial' });

interface ApiResponse<T> {
  status: number;
  body: T | null;
}

interface FleetVehicle {
  id: string;
  registrationPlate: string;
  fuelType: string;
  isArchived: boolean;
}

interface FleetCard {
  id: string;
  maskedPan: string;
  providerToken: string;
  state: string;
  assignedCarId: string | null;
  assignedVehicleRegistrationPlate: string | null;
}

interface FuelTransaction {
  id: string;
  cardId: string;
  vehicleId: string | null;
  vehiclePlate: string;
  fuelType: string;
  status: string;
}

interface TransactionReport {
  transactions: FuelTransaction[];
}

interface AssignmentHistoryEntry {
  cardId: string;
  vehicleId: string;
  vehicleRegistrationPlate: string;
  effectiveFrom: string;
  effectiveTo: string | null;
  orderingKey: number;
}

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
  await page.goto('/cards');
  await expect(page.getByRole('heading', { level: 2, name: 'Fleet cards' })).toBeVisible();
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
  body?: Record<string, string>,
): Promise<ApiResponse<T>> {
  const serializedBody = body === undefined ? null : JSON.stringify(body);
  const response = await page.evaluate(
    async ({ requestPath, requestMethod, requestBody }) => {
      const headers: Record<string, string> = {};
      if (requestBody !== null) {
        headers['Content-Type'] = 'application/json';
      }
      if (requestMethod !== 'GET') {
        const csrfCookie = document.cookie
          .split('; ')
          .find(cookie => cookie.startsWith('XSRF-TOKEN='))
          ?.slice('XSRF-TOKEN='.length);
        headers['X-XSRF-TOKEN'] = decodeURIComponent(csrfCookie ?? '');
      }

      const response = await fetch(requestPath, {
        method: requestMethod,
        headers,
        ...(requestBody === null ? {} : { body: requestBody }),
      });
      return {
        status: response.status,
        body: await response.json().catch(() => null),
      };
    },
    { requestPath: path, requestMethod: method, requestBody: serializedBody },
  );
  return { status: response.status, body: response.body as T | null };
}

function utcMonthAtOffset(offset: number): string {
  const current = new Date();
  return new Date(Date.UTC(current.getUTCFullYear(), current.getUTCMonth() + offset, 1))
    .toISOString()
    .slice(0, 7);
}

function reportPath(month: string): string {
  const [year, monthNumber] = month.split('-');
  return `/api/transactions/report?year=${year}&month=${Number(monthNumber)}&timeZone=UTC`;
}

async function getTransactionsForCard(page: Page, cardId: string): Promise<FuelTransaction[]> {
  const months = [...new Set([utcMonthAtOffset(-1), utcMonthAtOffset(0)])];
  const reports = await Promise.all(
    months.map(month => requestApi<TransactionReport>(page, reportPath(month))),
  );
  expect(reports.map(report => report.status)).toEqual(months.map(() => 200));
  return reports
    .flatMap(report => report.body?.transactions ?? [])
    .filter(transaction => transaction.cardId === cardId);
}

function expectCardRedacted(card: FleetCard): void {
  expect(card.maskedPan).toMatch(/^\*{4} \*{4} \*{4} \d{4}$/);
  const fieldNames = Object.keys(card).map(field => field.toLowerCase());
  for (const sensitiveField of ['pan', 'fullpan', 'cvv', 'pin']) {
    expect(fieldNames).not.toContain(sensitiveField);
  }
  const exposedValues = Object.entries(card)
    .filter(([field]) => field.toLowerCase() !== 'providertoken')
    .map(([, value]) => String(value));
  expect(exposedValues.join(' ')).not.toMatch(/\b\d{13,19}\b/);
}

test('composed card issue and reassignment persist history without rewriting transactions', async ({
  page,
}) => {
  await page.goto('/login');
  const unauthenticatedStatuses = await page.evaluate(async () => {
    const responses = await Promise.all([
      fetch('/api/cards'),
      fetch('/api/transactions/report?year=2026&month=9&timeZone=UTC'),
    ]);
    return responses.map(response => response.status);
  });
  expect(unauthenticatedStatuses).toEqual([401, 401]);

  await signIn(page, 'anna.nowak', 'Anna123!');
  await verifyDatabaseIdentity(page, String(test.info().config.metadata['databaseName']));

  const vehiclesResponse = await requestApi<FleetVehicle[]>(page, '/api/cars');
  expect(vehiclesResponse.status).toBe(200);
  const vehicles = vehiclesResponse.body ?? [];
  const issueVehicle = vehicles.find(vehicle => vehicle.registrationPlate === 'WA 12345');
  const reassignmentVehicle = vehicles.find(vehicle => vehicle.registrationPlate === 'PO 98765');
  expect(issueVehicle).toBeTruthy();
  expect(reassignmentVehicle).toBeTruthy();
  expect(issueVehicle?.isArchived).toBe(false);
  expect(reassignmentVehicle?.isArchived).toBe(false);

  const archivedVehicleResponse = await requestApi<FleetVehicle>(page, '/api/cars', 'POST', {
    registrationPlate: 'E2E P4 ARCHIVED',
    fuelType: 'Diesel',
    make: 'FleetCards',
    model: 'Acceptance',
  });
  expect(archivedVehicleResponse.status).toBe(201);
  const archivedVehicle = archivedVehicleResponse.body!;
  const archiveResponse = await requestApi<FleetVehicle>(
    page,
    `/api/cars/${archivedVehicle.id}/archive`,
    'POST',
  );
  expect(archiveResponse.status).toBe(200);
  expect(archiveResponse.body?.isArchived).toBe(true);

  const archivedIssueResponse = await requestApi<FleetCard>(page, '/api/cards', 'POST', {
    vehicleId: archivedVehicle.id,
  });
  expect(archivedIssueResponse.status).toBe(409);

  await page.locator('#card-issue-vehicle').selectOption(issueVehicle!.id);
  const issueResponsePromise = page.waitForResponse(
    response =>
      new URL(response.url()).pathname === '/api/cards' &&
      response.request().method() === 'POST',
  );
  await page.getByRole('button', { name: 'Issue card', exact: true }).click();
  const issueResponse = await issueResponsePromise;
  expect(issueResponse.status()).toBe(201);
  const issuedCard = (await issueResponse.json()) as FleetCard;
  expect(issuedCard).toMatchObject({
    state: 'Active',
    assignedCarId: issueVehicle!.id,
    assignedVehicleRegistrationPlate: issueVehicle!.registrationPlate,
  });
  expectCardRedacted(issuedCard);

  const cardItem = page.locator('.card-item').filter({ hasText: issuedCard.maskedPan });
  await expect(cardItem).toBeVisible();
  const persistedCardsResponse = await requestApi<FleetCard[]>(page, '/api/cards');
  expect(persistedCardsResponse.status).toBe(200);
  const persistedCard = persistedCardsResponse.body?.find(card => card.id === issuedCard.id);
  expect(persistedCard).toMatchObject(issuedCard);
  expectCardRedacted(persistedCard!);

  const issuedTransactions = (await getTransactionsForCard(page, issuedCard.id))
    .sort((left, right) => left.id.localeCompare(right.id));
  expect(issuedTransactions.length).toBeGreaterThan(0);
  for (const transaction of issuedTransactions) {
    expect(transaction).toMatchObject({
      cardId: issuedCard.id,
      vehicleId: issueVehicle!.id,
      vehiclePlate: issueVehicle!.registrationPlate,
      fuelType: issueVehicle!.fuelType,
      status: 'Completed',
    });
  }

  const initialHistoryResponse = await requestApi<AssignmentHistoryEntry[]>(
    page,
    `/api/cards/${issuedCard.id}/assignment-history`,
  );
  expect(initialHistoryResponse.status).toBe(200);
  expect(initialHistoryResponse.body).toHaveLength(1);
  expect(initialHistoryResponse.body?.[0]).toMatchObject({
    vehicleId: issueVehicle!.id,
    vehicleRegistrationPlate: issueVehicle!.registrationPlate,
    orderingKey: 1,
    effectiveTo: null,
  });

  await cardItem
    .getByLabel(`Reassign vehicle: ${issuedCard.maskedPan}`)
    .selectOption(reassignmentVehicle!.id);
  const reassignmentResponsePromise = page.waitForResponse(
    response =>
      new URL(response.url()).pathname === `/api/cards/${issuedCard.id}/assignment` &&
      response.request().method() === 'PUT',
  );
  await cardItem.getByRole('button', { name: 'Reassign vehicle', exact: true }).click();
  const reassignmentResponse = await reassignmentResponsePromise;
  expect(reassignmentResponse.status()).toBe(200);

  const historyResponse = await requestApi<AssignmentHistoryEntry[]>(
    page,
    `/api/cards/${issuedCard.id}/assignment-history`,
  );
  expect(historyResponse.status).toBe(200);
  expect(historyResponse.body).toHaveLength(2);
  expect(historyResponse.body?.[0]).toMatchObject({
    vehicleId: issueVehicle!.id,
    vehicleRegistrationPlate: issueVehicle!.registrationPlate,
    orderingKey: 1,
  });
  expect(historyResponse.body?.[1]).toMatchObject({
    vehicleId: reassignmentVehicle!.id,
    vehicleRegistrationPlate: reassignmentVehicle!.registrationPlate,
    orderingKey: 2,
    effectiveTo: null,
  });
  expect(historyResponse.body?.[0].effectiveTo).toBe(historyResponse.body?.[1].effectiveFrom);

  const transactionsAfterReassignment = await getTransactionsForCard(page, issuedCard.id);
  expect(
    transactionsAfterReassignment
      .sort((left, right) => left.id.localeCompare(right.id)),
  ).toEqual(issuedTransactions);

  await cardItem.getByRole('button', { name: 'Assignment history' }).click();
  await expect(
    cardItem.getByRole('heading', { name: 'Vehicle assignment timeline' }),
  ).toBeVisible();
  await expect(cardItem).toContainText(issueVehicle!.registrationPlate);
  await expect(cardItem).toContainText(reassignmentVehicle!.registrationPlate);

  await page.context().clearCookies();
  await signIn(page, 'piotr.wisniewski', 'Piotr123!');
  await verifyDatabaseIdentity(page, String(test.info().config.metadata['databaseName']));

  const readOnlyCard = page.locator('.card-item').filter({ hasText: issuedCard.maskedPan });
  await expect(readOnlyCard).toBeVisible();
  await expect(page.getByRole('button', { name: 'Issue card', exact: true })).toHaveCount(0);
  await expect(readOnlyCard.locator('select')).toHaveCount(0);
  await expect(
    readOnlyCard.getByRole('button', { name: 'Reassign vehicle', exact: true }),
  ).toHaveCount(0);
  await readOnlyCard.getByRole('button', { name: 'Assignment history' }).click();
  await expect(
    readOnlyCard.getByRole('heading', { name: 'Vehicle assignment timeline' }),
  ).toBeVisible();
  await expect(readOnlyCard).toContainText(issueVehicle!.registrationPlate);
  await expect(readOnlyCard).toContainText(reassignmentVehicle!.registrationPlate);

  const readOnlyMutations = await Promise.all([
    requestApi<FleetCard>(page, '/api/cards', 'POST', { vehicleId: issueVehicle!.id }),
    requestApi<FleetCard>(page, `/api/cards/${issuedCard.id}/state`, 'PUT', {
      state: 'Blocked',
    }),
    requestApi<FleetCard>(page, `/api/cards/${issuedCard.id}/assignment`, 'PUT', {
      vehicleId: issueVehicle!.id,
    }),
  ]);
  expect(readOnlyMutations.map(response => response.status)).toEqual([403, 403, 403]);
});