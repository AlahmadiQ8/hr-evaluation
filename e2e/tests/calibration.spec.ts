import { test, expect } from '@playwright/test';
import { loginAs, Personas } from './helpers';

test('HR calibration flags the Investment sector as over quota', async ({ page }) => {
  await loginAs(page, Personas.hrAdmin);
  await page.goto('/calibration');

  const investment = page.locator('.card', { hasText: 'Investment' });
  await expect(investment).toBeVisible();
  await expect(investment.getByText('Over quota')).toBeVisible();

  // The Outstanding band exceeds its cap and is highlighted.
  await expect(investment.locator('tr.table-danger', { hasText: 'Outstanding' })).toBeVisible();
});
