import { test, expect } from '@playwright/test';
import { loginAs, Personas } from './helpers';

test('language toggle switches the app to Arabic with RTL and back', async ({ page }) => {
  await loginAs(page, Personas.hrAdmin);

  await page.getByRole('link', { name: 'العربية' }).click();
  await expect(page.locator('html')).toHaveAttribute('dir', 'rtl');
  await expect(page.locator('html')).toHaveAttribute('lang', 'ar');
  await expect(page.getByRole('link', { name: 'الرئيسية' })).toBeVisible(); // "Home" in Arabic

  await page.getByRole('link', { name: 'English' }).click();
  await expect(page.locator('html')).toHaveAttribute('dir', 'ltr');
  await expect(page.locator('html')).toHaveAttribute('lang', 'en');
});
