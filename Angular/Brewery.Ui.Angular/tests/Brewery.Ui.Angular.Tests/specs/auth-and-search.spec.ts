import { expect, test } from '@playwright/test';

test('signs in and searches the SQLite brewery API through the gateway', async ({ page }) => {
  await page.goto('/');
  await page.getByLabel('Username').fill('user1');
  await page.getByLabel('Password').fill('not-used');
  await page.getByRole('button', { name: 'Sign in' }).click();
  await expect(page.getByRole('heading', { name: 'Find breweries' })).toBeVisible();
  await page.getByLabel('City').fill('Denver');
  await page.getByRole('button', { name: 'Search' }).click();
  await expect(page.getByRole('table')).toBeVisible();
});
