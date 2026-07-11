import { test, expect } from '@playwright/test';
import { loginAs, Personas } from './helpers';

test('HR persona signs in and sees the calibration navigation', async ({ page }) => {
  await loginAs(page, Personas.hrAdmin);
  await expect(page.getByRole('link', { name: 'HR Calibration' }).first()).toBeVisible();
});

test('employee persona cannot reach HR calibration', async ({ page }) => {
  await loginAs(page, Personas.employee);
  await expect(page.getByRole('link', { name: 'HR Calibration' })).toHaveCount(0);

  await page.goto('/calibration');
  await expect(page.getByRole('heading', { name: 'HR Calibration' })).toHaveCount(0);
  await expect(page).not.toHaveURL(/\/calibration/);
});
