import { expect, test as setup } from '@playwright/test';
import { resolve } from 'node:path';

setup('sign in and save the local test session', async ({ browser, page }) => {
  const username = process.env.E2E_USERNAME;
  const password = process.env.E2E_PASSWORD;
  if (!username || !password) {
    throw new Error('Set E2E_USERNAME and E2E_PASSWORD in web/.env.e2e or the environment');
  }

  await page.goto('/login');
  await expect(async () => {
    await page.getByLabel('Username').fill('');
    await page.getByLabel('Username').fill(username);
    await page.getByLabel('Password').fill('');
    await page.getByLabel('Password').fill(password);
    await page.getByRole('button', { name: 'Sign in' }).click();
    await page.waitForURL(/\/overview$/, { timeout: 5_000 });
  }).toPass();
  await expect(page.getByRole('button', { name: 'Log out' })).toBeVisible();
  const authFile = resolve(__dirname, '../playwright/.auth/user.json');
  await page.context().storageState({ path: authFile });

  const restoredContext = await browser.newContext({ storageState: authFile });
  try {
    const restoredPage = await restoredContext.newPage();
    await restoredPage.goto(new URL('/overview', page.url()).toString());
    await expect(restoredPage).toHaveURL(/\/overview$/);
    await expect(restoredPage.getByRole('button', { name: 'Log out' })).toBeVisible();
  } finally {
    await restoredContext.close();
  }
});
