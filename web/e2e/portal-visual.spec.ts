import { expect, test, type Locator, type Page } from '@playwright/test';

const viewports = [
  { name: 'desktop', width: 1280, height: 800 },
  { name: 'mobile', width: 390, height: 844 },
] as const;

const routes = [
  { name: 'overview', path: '/overview', heading: 'Overview' },
  { name: 'cars', path: '/cars', heading: 'Fleet vehicles' },
  { name: 'cards', path: '/cards', heading: 'Fleet cards' },
  { name: 'transactions', path: '/transactions', heading: 'Transactions' },
  { name: 'programs', path: '/programs', heading: 'Programs' },
  { name: 'admin-demo', path: '/admin/demo', heading: 'Demo data' },
] as const;

const maskedPan = '**** **** **** 4242';

const totals = {
  netTotal: 1000,
  vatTotal: 230,
  grossTotal: 1230,
  grossSavingsTotal: 84.5,
  rowCount: 2,
};

const fixtures = {
  client: {
    legalName: 'Northwind Logistics Sp. z o.o.',
    nip: '5252000001',
    jurisdiction: 'Poland',
    address: 'ul. Prosta 1, 00-838 Warszawa',
  },
  vehicles: [
    { id: 'vehicle-1', registrationPlate: 'WA 12345', fuelType: 'Diesel', make: 'Volvo', model: 'FH', isArchived: false },
    { id: 'vehicle-2', registrationPlate: 'PO 98765', fuelType: 'Petrol', make: 'Skoda', model: 'Octavia', isArchived: false },
    { id: 'vehicle-3', registrationPlate: 'GD 55555', fuelType: 'Diesel', make: 'BMW', model: 'i3', isArchived: true },
  ],
  cards: [
    {
      id: 'card-1',
      maskedPan,
      providerToken: 'seeded-provider-token',
      state: 'Active',
      assignedCarId: 'vehicle-1',
      assignedVehicleRegistrationPlate: 'WA 12345',
    },
  ],
  cardHistory: [
    { state: 'Blocked', occurredAt: '2026-09-02T09:00:00Z' },
    { state: 'Active', occurredAt: '2026-09-01T09:00:00Z' },
  ],
  summary: {
    activeVehicleCount: 2,
    activeCardCount: 1,
    periodTotals: totals,
    periodStartUtc: '2026-08-31T22:00:00Z',
    periodEndUtc: '2026-09-30T22:00:00Z',
  },
  report: {
    year: 2026,
    month: 9,
    timeZoneId: 'Europe/Warsaw',
    periodStartUtc: '2026-08-31T22:00:00Z',
    periodEndUtc: '2026-09-30T22:00:00Z',
    totals,
    transactions: [
      reportRow('transaction-1', 'WA 12345', 'Diesel', '2026-09-03T08:15:00Z', 400, 1.9),
      reportRow('transaction-2', 'PO 98765', 'Petrol', '2026-09-12T14:40:00Z', 250, 1.5, 'Refunded'),
    ],
  },
};

function reportRow(
  id: string,
  vehiclePlate: string,
  fuelType: string,
  occurredAtUtc: string,
  netTotal: number,
  unitPrice: number,
  status = 'Completed',
) {
  const vatAmount = netTotal * 0.23;
  return {
    id,
    lineageId: id,
    version: 1,
    correctsTransactionId: null,
    cardId: 'card-1',
    vehicleId: vehiclePlate === 'WA 12345' ? 'vehicle-1' : 'vehicle-2',
    vehiclePlate,
    occurredAtUtc,
    userOccurredAt: occurredAtUtc,
    posOccurredAtLocal: occurredAtUtc,
    posTimeZoneId: 'Europe/Warsaw',
    stationName: 'Station Central',
    fuelType,
    quantity: Math.round((netTotal / unitPrice) * 100) / 100,
    unitPrice,
    discount: 0.1,
    netTotal,
    vatRate: 0.23,
    vatAmount,
    grossTotal: netTotal + vatAmount,
    status,
  };
}

test.use({ locale: 'en-GB', timezoneId: 'Europe/Warsaw', colorScheme: 'light' });

test.beforeEach(async ({ page }) => {
  await page.clock.setFixedTime(new Date('2026-10-15T10:00:00Z'));
  await page.route(
    url => url.pathname.startsWith('/api/') && url.pathname !== '/api/auth/me',
    async route => {
      const request = route.request();
      const { pathname } = new URL(request.url());
      const body = mockedBody(pathname, request.method());
      if (body === undefined) {
        await route.fallback();
        return;
      }
      await route.fulfill({ json: body });
    },
  );
});

function mockedBody(pathname: string, method: string): unknown {
  if (method !== 'GET') {
    return undefined;
  }
  if (pathname === '/api/client') return fixtures.client;
  if (pathname === '/api/dashboard/summary') return fixtures.summary;
  if (pathname === '/api/cars') return fixtures.vehicles;
  if (pathname === '/api/cards') return fixtures.cards;
  if (/^\/api\/cards\/[^/]+\/state-history$/.test(pathname)) return fixtures.cardHistory;
  if (pathname === '/api/transactions/report') return fixtures.report;
  return undefined;
}

async function signIn(page: Page): Promise<void> {
  await page.goto('/login');
  await page.getByLabel('Username').fill('anna.nowak');
  await page.getByLabel('Password').fill('Anna123!');
  const loginResponse = page.waitForResponse(
    response => new URL(response.url()).pathname === '/api/auth/login',
  );
  await page.getByRole('button', { name: 'Sign in' }).click();
  expect((await loginResponse).status()).toBe(200);
  await expect(page).toHaveURL(/\/overview$/);
}

async function settle(page: Page): Promise<void> {
  await page.evaluate(() => document.fonts.ready);
}

for (const viewport of viewports) {
  test.describe(`${viewport.name} ${viewport.width}x${viewport.height}`, () => {
    test.use({ viewport: { width: viewport.width, height: viewport.height } });

    test('login surface', async ({ page }) => {
      await page.goto('/login');
      await expect(page.getByRole('button', { name: 'Sign in' })).toBeVisible();
      await settle(page);
      await expect(page).toHaveScreenshot(`login-${viewport.name}.png`, {
        animations: 'disabled',
        caret: 'hide',
      });
    });

    for (const route of routes) {
      test(`${route.name} surface`, async ({ page }) => {
        await signIn(page);
        await page.goto(route.path);
        await expect(page).toHaveURL(new RegExp(`${route.path}$`));
        await expect(page.getByRole('heading', { level: 1, name: route.heading })).toBeVisible();
        if (route.name === 'overview') {
          await expect(page.getByText('Loading period summary...')).toHaveCount(0);
          await expect(page.getByText('Gross spend')).toBeVisible();
        }
        if (route.name === 'cars') {
          await expect(page.getByText('WA 12345').first()).toBeVisible();
        }
        if (route.name === 'cards') {
          await expect(page.getByText(maskedPan)).toBeVisible();
        }
        if (route.name === 'programs') {
          await expect(page.getByRole('heading', { level: 3, name: 'Assignment history' })).toBeVisible();
        }
        if (route.name === 'transactions') {
          await expect(page.getByRole('table')).toBeVisible();
          await expect(page.getByText('Refunded')).toBeVisible();
        }
        if (route.name === 'admin-demo') {
          await page.getByLabel('From date (UTC)').fill('2026-09-01');
          await page.getByLabel('Through date (UTC)').fill('2026-09-30');
          await page.getByLabel('Number of transactions').fill('12');
        }
        await settle(page);
        await expect(page).toHaveScreenshot(`${route.name}-${viewport.name}.png`, {
          animations: 'disabled',
          caret: 'hide',
        });
      });
    }

    test('vehicle archive error and destructive feedback', async ({ page }) => {
      await signIn(page);
      await page.goto('/cars');
      const archiveButton = page.getByRole('button', { name: 'Archive', exact: true }).first();
      await expect(archiveButton).toBeVisible();
      await page.route('**/api/cars/vehicle-1/archive', async route => {
        if (route.request().method() === 'POST') {
          await route.fulfill({ status: 503, json: { title: 'Service unavailable' } });
          return;
        }
        await route.fallback();
      });

      await archiveButton.click();
      await expect(page.getByRole('alert')).toContainText(
        'Could not change the vehicle status. Please try again.',
      );
      await settle(page);
      await expect(page).toHaveScreenshot(`cars-archive-error-${viewport.name}.png`, {
        animations: 'disabled',
        caret: 'hide',
      });
    });

    test('archived vehicles use warning status styling', async ({ page }) => {
      await signIn(page);
      await page.goto('/cars');
      await page.getByRole('button', { name: 'Archived vehicles', exact: true }).click();

      const archivedVehicle = page.getByRole('row').filter({ hasText: 'GD 55555' });
      await expect(archivedVehicle).toBeVisible();
      await expect(archivedVehicle.getByText('Archived', { exact: true })).toHaveAttribute(
        'data-tone',
        'warning',
      );
      await archivedVehicle.scrollIntoViewIfNeeded();
      await settle(page);
      await expect(page).toHaveScreenshot(`cars-archived-warning-${viewport.name}.png`, {
        animations: 'disabled',
        caret: 'hide',
      });
    });

    test('keyboard focus-visible treatment on a destructive action', async ({ page }) => {
      await signIn(page);
      await page.goto('/cars');
      const archiveButton = page.getByRole('button', { name: 'Archive', exact: true }).first();
      await expect(archiveButton).toBeVisible();
      await page.keyboard.press('Tab');
      await archiveButton.focus();
      await expect(archiveButton).toBeFocused();
      expect(await archiveButton.evaluate(element => element.matches(':focus-visible'))).toBe(true);
      await settle(page);
      await expect(archiveButton).toHaveScreenshot(
        `cars-archive-focus-visible-${viewport.name}.png`,
        { animations: 'disabled', caret: 'hide' },
      );
    });
  });
}

test.describe('card interaction states', () => {
  test.use({ viewport: { width: 1280, height: 800 } });

  async function openCards(page: Page): Promise<Locator> {
    await signIn(page);
    await page.goto('/cards');
    const cardItem = page.getByRole('article').filter({ hasText: maskedPan });
    await expect(cardItem).toBeVisible();
    await settle(page);
    return cardItem;
  }

  test('hover treatment on card action buttons', async ({ page }) => {
    const cardItem = await openCards(page);
    const issueButton = page.getByRole('button', { name: 'Issue card', exact: true });
    const historyButton = cardItem.getByRole('button', { name: 'Status history' });

    await issueButton.hover();
    await expect(issueButton).toBeVisible();
    await expect(issueButton).toHaveScreenshot('card-issue-button-hover.png', {
      animations: 'disabled',
    });

    await historyButton.hover();
    await expect(historyButton).toBeVisible();
    await expect(historyButton).toHaveScreenshot('card-history-button-hover.png', {
      animations: 'disabled',
    });
  });

  test('keyboard focus-visible treatment on card controls', async ({ page }) => {
    const cardItem = await openCards(page);
    const vehicleSelector = page.getByRole('combobox', { name: 'Active vehicle' });
    const issueButton = page.getByRole('button', { name: 'Issue card', exact: true });
    const stateSelector = cardItem.getByRole('combobox', { name: `Status: ${maskedPan}` });
    const historyButton = cardItem.getByRole('button', { name: 'Status history' });

    const controls = [
      { name: 'vehicle-selector', control: vehicleSelector },
      { name: 'issue-action', control: issueButton },
      { name: 'state-selector', control: stateSelector },
      { name: 'history-button', control: historyButton },
    ];

    await vehicleSelector.focus();
    for (const [index, { name, control }] of controls.entries()) {
      if (index > 0) {
        await page.keyboard.press('Tab');
      }
      await expect(control).toBeFocused();
      expect(await control.evaluate(element => element.matches(':focus-visible'))).toBe(true);
      await expect(control).toHaveScreenshot(`card-${name}-focus-visible.png`, {
        animations: 'disabled',
        caret: 'hide',
      });
    }
  });
});
