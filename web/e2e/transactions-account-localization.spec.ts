import { expect, test, type Page } from '@playwright/test';

// Risk: transactions-account-localization; seed: seed.spec.ts.
test.use({ locale: 'en-US', timezoneId: 'Europe/Warsaw' });

async function expectAccountAtPageTop(page: Page): Promise<void> {
  const account = page.getByRole('complementary');
  const identity = account.getByText('Anna Nowak', { exact: true });
  const logout = account.getByRole('button', { name: 'Log out', exact: true });
  const language = account.getByRole('button', { name: 'EN', exact: true });

  await expect(identity).toBeInViewport();
  await expect(logout).toBeInViewport();
  await expect(language).toBeInViewport();
  const identityBox = await identity.boundingBox();
  const logoutBox = await logout.boundingBox();
  expect(identityBox).not.toBeNull();
  expect(logoutBox).not.toBeNull();
  expect(logoutBox!.y).toBeGreaterThan(identityBox!.y + identityBox!.height);
}

test('long reports keep localized period and account controls accessible at the top', async ({
  page,
}) => {
  // Generate enough real transactions to make the report longer than the viewport.
  await page.goto('/admin/demo');
  await page.getByLabel('From date (UTC)').fill('2026-09-09');
  await page.getByLabel('Through date (UTC)').fill('2026-09-09');
  await page.getByLabel('Number of transactions').fill('40');
  await page.getByRole('button', { name: 'Generate transactions' }).click();
  await expect(page.getByRole('status')).toContainText('Demo transactions generated: 40');

  await page.goto('/transactions');
  const month = page.getByRole('combobox', { name: 'Reporting month' });
  await page.getByRole('textbox', { name: 'Reporting year' }).fill('2026');
  await month.selectOption('09');
  await expect(page.getByRole('heading', { name: 'September 2026' })).toBeVisible();
  await expect.poll(() => page.getByRole('row').count()).toBeGreaterThanOrEqual(41);

  // Check top-of-page placement on desktop, intermediate and mobile widths.
  for (const width of [1280, 800, 390]) {
    await page.setViewportSize({ width, height: 800 });
    await page.evaluate(() => window.scrollTo(0, 0));
    await expectAccountAtPageTop(page);
    expect(await page.evaluate(() => document.documentElement.scrollHeight)).toBeGreaterThan(800);
  }

  // App language, not browser locale, controls month names and logout.
  await page.getByRole('button', { name: 'EN', exact: true }).click();
  const polishMonth = page.getByRole('combobox', { name: 'Miesiąc raportu' });
  await expect(polishMonth.getByRole('option', { name: 'Wrzesień', exact: true })).toBeAttached();
  await expect(polishMonth).toHaveValue('09');
  await expect(page.getByRole('textbox', { name: 'Rok raportu' })).toHaveValue('2026');
  await expect(page.getByRole('heading', { name: 'Wrzesień 2026' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Wyloguj', exact: true })).toBeInViewport();
  await page.getByRole('link', { name: 'Przegląd', exact: true }).click();
  await expect(page.getByText('Wrzesień 2026').first()).toBeVisible();
  await page.getByRole('button', { name: 'PL', exact: true }).click();

  // The chosen period is shared with the overview and logout ends the session.
  await expect(page.getByRole('link', { name: /View transactions for this period/ })).toContainText(
    'September 2026',
  );
  await page.getByRole('link', { name: /View transactions for this period/ }).click();
  await expect(month.getByRole('option', { name: 'September', exact: true })).toBeAttached();
  await expect(month).toHaveValue('09');
  await page.getByRole('button', { name: 'Log out', exact: true }).click();
  await expect(page).toHaveURL(/\/login$/);
  await page.goto('/transactions');
  await expect(page).toHaveURL(/\/login$/);
  // Transactions exist only in this run's disposable SQL container.
});
