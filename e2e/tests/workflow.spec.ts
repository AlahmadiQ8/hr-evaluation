import { test, expect } from '@playwright/test';
import { loginAs, Personas } from './helpers';

test('line manager scores an evaluation and it advances to department review', async ({ page }) => {
  await loginAs(page, Personas.lineManager);
  await page.goto('/inbox');

  const row = page.locator('tr', { hasText: Personas.employee });
  await expect(row).toBeVisible();
  await row.getByRole('link', { name: 'Open' }).click();

  // Wait for the editable evaluation form, then rate every item 4 (=> 75% => Exceeds).
  await expect(page.getByRole('button', { name: 'Submit evaluation' })).toBeVisible();
  const selects = page.locator('main select');
  const count = await selects.count();
  expect(count).toBeGreaterThan(0);
  for (let i = 0; i < count; i++) {
    await selects.nth(i).selectOption('4');
  }
  await page.getByLabel('Comment').fill('Strong, consistent delivery.');
  await page.getByRole('button', { name: 'Submit evaluation' }).click();

  await expect(page.getByText('Department review')).toBeVisible();
  await expect(page.getByText('Exceeds')).toBeVisible();
});

test('a mid-year manager change lists two manager evaluators in the chain', async ({ page }) => {
  await loginAs(page, Personas.departmentManager);
  await page.goto('/inbox');

  const row = page.locator('tr', { hasText: Personas.midYear });
  await expect(row).toBeVisible();
  await row.getByRole('link', { name: 'Open' }).click();

  // Two managers evaluated the employee across the year -> two manager-evaluation steps.
  await expect(page.getByText('Manager evaluation')).toHaveCount(2);
});
