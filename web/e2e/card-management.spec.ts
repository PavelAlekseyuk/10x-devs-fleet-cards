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
  assignedCarId: string;
  assignedVehicleRegistrationPlate: string;
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

interface StateHistoryEntry {
  state: string;
  occurredAt: string;
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
  const previousMonth = utcMonthAtOffset(-1);
  const report = await requestApi<TransactionReport>(page, reportPath(previousMonth));
  expect(report.status).toBe(200);
  return (report.body?.transactions ?? []).filter(transaction => transaction.cardId === cardId);
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

test('composed card issue keeps its assignment immutable and shows state history after reload', async ({
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
  const otherVehicle = vehicles.find(vehicle => vehicle.registrationPlate === 'PO 98765');
  expect(issueVehicle).toBeTruthy();
  expect(otherVehicle).toBeTruthy();
  expect(issueVehicle?.isArchived).toBe(false);
  expect(otherVehicle?.isArchived).toBe(false);

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
  const issueButton = page.getByRole('button', { name: 'Issue card', exact: true });
  await expect(issueButton).toHaveAccessibleName('Issue card');
  await page.locator('#card-issue-vehicle').focus();
  await page.keyboard.press('Tab');
  await expect(issueButton).toBeFocused();
  await page.keyboard.press('Enter');
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
  expect(issuedTransactions.length).toBeGreaterThanOrEqual(5);
  expect(issuedTransactions.length).toBeLessThanOrEqual(15);
  for (const transaction of issuedTransactions) {
    expect(transaction).toMatchObject({
      cardId: issuedCard.id,
      vehicleId: issueVehicle!.id,
      vehiclePlate: issueVehicle!.registrationPlate,
      fuelType: issueVehicle!.fuelType,
      status: 'Completed',
    });
  }

  const initialHistoryResponse = await requestApi<StateHistoryEntry[]>(
    page,
    `/api/cards/${issuedCard.id}/state-history`,
  );
  expect(initialHistoryResponse.status).toBe(200);
  expect(initialHistoryResponse.body).toHaveLength(1);
  expect(initialHistoryResponse.body?.[0].state).toBe('Active');

  const stateSelector = cardItem.getByRole('combobox', {
    name: `Status: ${issuedCard.maskedPan}`,
  });
  await expect(stateSelector).toHaveAccessibleName(`Status: ${issuedCard.maskedPan}`);
  const stateResponsePromise = page.waitForResponse(
    response =>
      new URL(response.url()).pathname === `/api/cards/${issuedCard.id}/state` &&
      response.request().method() === 'PUT',
  );
  await page.keyboard.press('Tab');
  await expect(stateSelector).toBeFocused();
  await page.keyboard.press('ArrowDown');
  expect((await stateResponsePromise).status()).toBe(200);
  await expect(stateSelector).toHaveValue('Blocked');
  await expect(cardItem.locator('.badge')).toHaveText('Blocked');

  await page.reload();
  await expect(page.getByRole('heading', { level: 2, name: 'Fleet cards' })).toBeVisible();
  await expect(cardItem.getByRole('combobox', {
    name: `Status: ${issuedCard.maskedPan}`,
  })).toHaveValue('Blocked');
  await expect(cardItem.locator('.badge')).toHaveText('Blocked');

  const historyResponse = await requestApi<StateHistoryEntry[]>(
    page,
    `/api/cards/${issuedCard.id}/state-history`,
  );
  expect(historyResponse.status).toBe(200);
  expect(historyResponse.body?.map(entry => entry.state)).toEqual(['Blocked', 'Active']);
  await cardItem.getByRole('button', { name: 'Status history' }).click();
  await expect(cardItem.getByRole('heading', { name: 'Card status history' })).toBeVisible();
  await expect(cardItem.locator('.state-timeline li strong')).toHaveText(['Blocked', 'Active']);

  await page.getByRole('button', { name: 'EN' }).click();
  await expect(cardItem.getByRole('combobox', {
    name: `Status: ${issuedCard.maskedPan}`,
  })).toHaveValue('Blocked');
  await expect(cardItem.locator('.badge')).toHaveText('Zablokowana');
  await expect(cardItem.getByRole('button', { name: 'Historia statusu' })).toBeVisible();
  await expect(cardItem.getByRole('heading', { name: 'Historia statusu karty' })).toBeVisible();
  await expect(cardItem.locator('.state-timeline li strong')).toHaveText([
    'Zablokowana',
    'Aktywna',
  ]);
  await page.getByRole('button', { name: 'PL' }).click();

  const removedAssignmentResponse = await requestApi<FleetCard>(
    page,
    `/api/cards/${issuedCard.id}/assignment`,
    'PUT',
    { vehicleId: otherVehicle!.id },
  );
  expect(removedAssignmentResponse.status).toBe(404);
  await expect(cardItem).toContainText(issueVehicle!.registrationPlate);
  await expect(cardItem.getByRole('button', { name: 'Reassign vehicle' })).toHaveCount(0);
  const cardsAfterAssignmentAttempt = await requestApi<FleetCard[]>(page, '/api/cards');
  expect(
    cardsAfterAssignmentAttempt.body?.find(card => card.id === issuedCard.id),
  ).toMatchObject({
    assignedCarId: issueVehicle!.id,
    assignedVehicleRegistrationPlate: issueVehicle!.registrationPlate,
  });
  expect(
    (await getTransactionsForCard(page, issuedCard.id)).sort((left, right) =>
      left.id.localeCompare(right.id),
    ),
  ).toEqual(issuedTransactions);

  await page.context().clearCookies();
  await signIn(page, 'piotr.wisniewski', 'Piotr123!');
  await verifyDatabaseIdentity(page, String(test.info().config.metadata['databaseName']));

  const readOnlyCard = page.locator('.card-item').filter({ hasText: issuedCard.maskedPan });
  await expect(readOnlyCard).toBeVisible();
  await expect(page.getByRole('button', { name: 'Issue card', exact: true })).toHaveCount(0);
  await expect(readOnlyCard.locator('select')).toHaveCount(0);
  await readOnlyCard.getByRole('button', { name: 'Status history' }).click();
  await expect(readOnlyCard.getByRole('heading', { name: 'Card status history' })).toBeVisible();
  await expect(readOnlyCard.locator('.state-timeline li strong')).toHaveText([
    'Blocked',
    'Active',
  ]);
  await expect(readOnlyCard).toContainText(issueVehicle!.registrationPlate);

  const readOnlyMutations = await Promise.all([
    requestApi<FleetCard>(page, '/api/cards', 'POST', { vehicleId: issueVehicle!.id }),
    requestApi<FleetCard>(page, `/api/cards/${issuedCard.id}/state`, 'PUT', {
      state: 'Blocked',
    }),
    requestApi<FleetCard>(page, `/api/cards/${issuedCard.id}/assignment`, 'PUT', {
      vehicleId: otherVehicle!.id,
    }),
  ]);
  expect(readOnlyMutations.map(response => response.status)).toEqual([403, 403, 404]);
});