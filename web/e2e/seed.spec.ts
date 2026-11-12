import { expect, test } from '@playwright/test';

// Risk: signed-out-transactions-redirect.
test.use({ storageState: { cookies: [], origins: [] } });

test('signed-out access to transactions redirects to sign in', async ({ page }) => {
  await page.goto('/transactions');

  await expect(page).toHaveURL(/\/login$/);
  await expect(page.getByRole('heading', { name: 'Welcome back' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Sign in' })).toBeVisible();
  // This signed-out flow creates no data to clean up.
});
