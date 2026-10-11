import { expect, test } from '@playwright/test';

test('page title and favicon are served', async ({ page }) => {
  await page.goto('/login');
  await expect(page).toHaveTitle('Beznynowicz');

  const faviconResponse = await page.request.get('/favicon.ico');
  expect(faviconResponse.ok()).toBeTruthy();
  expect((await faviconResponse.body()).byteLength).toBeGreaterThan(0);
});
