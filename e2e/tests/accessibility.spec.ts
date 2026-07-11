import { test, expect } from '@playwright/test';

test('login page is accessible: single H1, page language, and labeled sign-in actions', async ({ page }) => {
  await page.goto('/login');

  await expect(page.locator('html')).toHaveAttribute('lang', 'en');
  await expect(page.getByRole('heading', { level: 1 })).toHaveCount(1);

  // Each seeded persona card exposes a keyboard-focusable sign-in link.
  const signIns = page.getByRole('link', { name: 'Sign in' });
  expect(await signIns.count()).toBeGreaterThan(1);
});
