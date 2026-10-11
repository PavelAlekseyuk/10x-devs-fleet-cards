import { expect, test, type Page } from '@playwright/test';

function utcDate(daysAgo: number): string {
  return new Date(Date.now() - daysAgo * 24 * 60 * 60 * 1000).toISOString().slice(0, 10);
}

function localDateTime(offsetMs: number): string {
  const date = new Date(Date.now() + offsetMs);
  const pad = (value: number) => String(value).padStart(2, '0');
  return (
    `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}` +
    `T${pad(date.getHours())}:${pad(date.getMinutes())}:${pad(date.getSeconds())}`
  );
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

async function openComparison(page: Page): Promise<string> {
  await page.goto('/programs');
  await expect(page.getByRole('heading', { level: 3, name: 'Assignment history' })).toBeVisible();
  await page.getByRole('button', { name: 'Change program' }).click();
  const dialog = page.getByRole('dialog');
  await dialog.getByRole('combobox').first().selectOption({ label: 'Custom dates' });
  await dialog.getByLabel('From', { exact: true }).fill(utcDate(12));
  await dialog.getByLabel('To', { exact: true }).fill(utcDate(0));
  await dialog.getByRole('button', { name: 'Compare period' }).click();
  const baselineRow = dialog.getByRole('row').filter({ hasText: 'Current actual cost' }).first();
  await expect(baselineRow).toBeVisible();
  return (await baselineRow.getByRole('cell').first().textContent()) ?? '';
}

test('generates demo data, compares programs and schedules an end without a successor', async ({
  page,
}) => {
  await signIn(page);

  const baselineBefore = await openComparison(page);
  await page.getByRole('dialog').getByRole('button', { name: 'Cancel' }).first().click();

  await page.goto('/admin/demo');
  await expect(page.getByRole('heading', { level: 2, name: 'Demo data' })).toBeVisible();
  await page.getByLabel('From date (UTC)').fill(utcDate(10));
  await page.getByLabel('Through date (UTC)').fill(utcDate(1));
  await page.getByLabel('Number of transactions').fill('25');
  await page.getByRole('button', { name: 'Generate transactions' }).click();
  await expect(page.getByRole('status').filter({ hasText: 'Demo transactions generated: 25' })).toBeVisible();

  const baselineAfter = await openComparison(page);
  expect(baselineAfter).not.toBe(baselineBefore);

  const dialog = page.getByRole('dialog');
  await dialog.getByRole('radio', { name: 'End the current program without a successor' }).check();
  await dialog
    .getByLabel('Effective date and time')
    .fill(localDateTime(2 * 60 * 60 * 1000));
  await dialog.getByRole('button', { name: 'Confirm change' }).click();

  await expect(page.getByRole('status').filter({ hasText: 'The program change has been scheduled.' })).toBeVisible();
  const history = page.getByRole('region', { name: 'Assignment history' });
  const gapRow = history.getByRole('row').filter({ hasText: 'No successor scheduled' });
  await expect(gapRow).toBeVisible();
  await expect(gapRow.getByRole('cell', { name: /Unassigned/ })).toBeVisible();
  await expect(gapRow.getByRole('cell', { name: 'Ongoing' })).toBeVisible();
});
