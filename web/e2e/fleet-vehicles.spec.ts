import { expect, test, type Page } from '@playwright/test';

test.describe.configure({ mode: 'serial' });

async function signIn(page: Page, userName: string, password: string): Promise<void> {
  await page.goto('/login');
  await page.getByLabel('Username').fill(userName);
  await page.getByLabel('Password').fill(password);
  const loginResponsePromise = page.waitForResponse(response =>
    new URL(response.url()).pathname === '/api/auth/login',
  );
  await page.getByRole('button', { name: 'Sign in' }).click();
  expect((await loginResponsePromise).status()).toBe(200);
  await expect(page).toHaveURL(/\/overview$/);
  await page.getByRole('link', { name: 'Fleet vehicles' }).click();
  await expect(page.getByRole('heading', { level: 2, name: 'Fleet vehicles' })).toBeVisible();
}

async function verifyDatabaseIdentity(page: Page, expectedDatabaseName: string): Promise<void> {
  expect(expectedDatabaseName).toMatch(/^fleetcards_e2e_[0-9a-f]{32}$/);
  const identity = await page.evaluate(async () => {
    const response = await fetch('/api/e2e/database');
    return { status: response.status, body: await response.json() };
  });
  expect(identity).toEqual({ status: 200, body: { databaseName: expectedDatabaseName } });
}

test('ReadOnly can search in both languages but cannot mutate vehicles', async ({ page }) => {
  await signIn(page, 'piotr.wisniewski', 'Piotr123!');
  await verifyDatabaseIdentity(page, String(test.info().config.metadata['databaseName']));

  const search = page.getByRole('searchbox', { name: 'Search vehicles' });
  await search.fill('pO98765');
  await page.getByRole('button', { name: 'Search', exact: true }).click();
  await expect(page.getByRole('row').filter({ hasText: 'PO 98765' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Add vehicle', exact: true })).toHaveCount(0);
  await expect(page.getByRole('button', { name: 'Edit', exact: true })).toHaveCount(0);
  await expect(page.getByRole('button', { name: 'Archive', exact: true })).toHaveCount(0);

  await page.getByRole('button', { name: /EN/ }).click();
  await expect(page.getByRole('heading', { level: 2, name: 'Pojazdy floty' })).toBeVisible();
  const polishSearch = page.getByRole('searchbox', { name: 'Szukaj pojazdów' });
  await polishSearch.fill('wa12345');
  await page.getByRole('button', { name: 'Szukaj', exact: true }).click();
  await expect(page.getByRole('row').filter({ hasText: 'WA 12345' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Dodaj pojazd', exact: true })).toHaveCount(0);

  const writeStatus = await page.evaluate(async () => {
    const csrfCookie = document.cookie
      .split('; ')
      .find((cookie) => cookie.startsWith('XSRF-TOKEN='))
      ?.slice('XSRF-TOKEN='.length);
    const response = await fetch('/api/cars', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'X-XSRF-TOKEN': decodeURIComponent(csrfCookie ?? ''),
      },
      body: JSON.stringify({
        registrationPlate: 'E2E READONLY BLOCKED',
        fuelType: '95',
        make: null,
        model: null,
      }),
    });
    return response.status;
  });
  expect(writeStatus).toBe(403);
});

test('ReadWrite can manage vehicles and preserve the seeded Volvo history', async ({ page }) => {
  await signIn(page, 'anna.nowak', 'Anna123!');
  await verifyDatabaseIdentity(page, String(test.info().config.metadata['databaseName']));

  const plate = 'E2E P4 ALPHA';
  await page.getByRole('button', { name: 'Add vehicle', exact: true }).click();
  await page.getByLabel('Registration plate').fill(plate);
  await page.getByLabel('Fuel type').selectOption('98');
  await page.getByLabel('Make (optional)').fill('FleetCards');
  await page.getByLabel('Model (optional)').fill('Acceptance');
  await page.getByRole('button', { name: 'Save vehicle' }).click();

  const search = page.getByRole('searchbox', { name: 'Search vehicles' });
  await expect(page.getByRole('row').filter({ hasText: plate })).toBeVisible();
  await search.fill('e2e p4alpha');
  await page.getByRole('button', { name: 'Search', exact: true }).click();
  await expect(page.getByRole('row').filter({ hasText: plate })).toBeVisible();
  await search.fill('');
  await page.getByRole('button', { name: 'Search', exact: true }).click();

  let vehicleRow = page.getByRole('row').filter({ hasText: plate });
  await vehicleRow.getByRole('button', { name: 'Edit', exact: true }).click();
  await page.getByLabel('Registration plate').fill(`${plate} UPDATED`);
  await page.getByLabel('Fuel type').selectOption('Diesel');
  await page.getByLabel('Model (optional)').fill('Acceptance Updated');
  await page.getByRole('button', { name: 'Save vehicle' }).click();

  const updatedPlate = `${plate} UPDATED`;
  vehicleRow = page.getByRole('row').filter({ hasText: updatedPlate });
  await expect(vehicleRow).toContainText('Diesel');
  await vehicleRow.getByRole('button', { name: 'Archive', exact: true }).click();
  await expect(vehicleRow).toHaveCount(0);

  await page.getByRole('button', { name: 'Archived vehicles', exact: true }).click();
  const archivedVehicle = page.getByRole('row').filter({ hasText: updatedPlate });
  await expect(archivedVehicle).toBeVisible();

  await page.getByRole('button', { name: 'Active vehicles', exact: true }).click();
  await page.getByRole('button', { name: 'Add vehicle', exact: true }).click();
  await page.getByLabel('Registration plate').fill('e2e p4alpha updated');
  await page.getByLabel('Fuel type').selectOption('95');
  await page.getByRole('button', { name: 'Save vehicle' }).click();
  const replacementVehicle = page.getByRole('row').filter({ hasText: /e2e p4alpha updated/i });
  await expect(replacementVehicle).toBeVisible();

  await page.getByRole('button', { name: 'Archived vehicles', exact: true }).click();
  await archivedVehicle.getByRole('button', { name: 'Restore', exact: true }).click();
  await expect(page.getByRole('alert')).toHaveText(
    'Could not change the vehicle status. Please try again.',
  );
  await expect(archivedVehicle).toBeVisible();

  await page.getByRole('button', { name: 'Active vehicles', exact: true }).click();
  await replacementVehicle.getByRole('button', { name: 'Archive', exact: true }).click();
  await expect(replacementVehicle).toHaveCount(0);
  await page.getByRole('button', { name: 'Archived vehicles', exact: true }).click();
  await archivedVehicle.getByRole('button', { name: 'Restore', exact: true }).click();
  await page.getByRole('button', { name: 'Active vehicles', exact: true }).click();
  await expect(page.getByRole('row').filter({ hasText: updatedPlate })).toBeVisible();

  const volvo = await page.evaluate(async () => {
    const response = await fetch('/api/cars?search=wa12345&includeArchived=true');
    const vehicles = await response.json();
    return vehicles.find((vehicle: { registrationPlate: string }) =>
      vehicle.registrationPlate === 'WA 12345',
    );
  });
  expect(volvo?.id).toBeTruthy();
  const volvoRow = page.getByRole('row').filter({ hasText: 'WA 12345' });
  await volvoRow.getByRole('button', { name: 'Archive', exact: true }).click();
  await page.getByRole('button', { name: 'Archived vehicles', exact: true }).click();
  const archivedVolvo = page.getByRole('row').filter({ hasText: 'WA 12345' });
  await expect(archivedVolvo).toBeVisible();

  const history = await page.evaluate(async () => {
    const [cardsResponse, reportResponse] = await Promise.all([
      fetch('/api/cards'),
      fetch('/api/transactions/report?year=2026&month=9&timeZone=UTC'),
    ]);
    const report = await reportResponse.json();
    return {
      cards: await cardsResponse.json(),
      transactions: report.transactions,
    };
  });
  expect(history.cards).toEqual(
    expect.arrayContaining([expect.objectContaining({ assignedCarId: volvo.id })]),
  );
  expect(history.transactions).toEqual(
    expect.arrayContaining([
      expect.objectContaining({
        vehicleId: volvo.id,
        vehiclePlate: 'WA 12345',
        fuelType: 'Diesel',
      }),
    ]),
  );

  await archivedVolvo.getByRole('button', { name: 'Restore', exact: true }).click();
  await page.getByRole('button', { name: 'Active vehicles', exact: true }).click();
  await expect(page.getByRole('row').filter({ hasText: 'WA 12345' })).toBeVisible();
  await page.getByRole('button', { name: /EN/ }).click();
  await expect(page.getByRole('heading', { level: 2, name: 'Pojazdy floty' })).toBeVisible();
});