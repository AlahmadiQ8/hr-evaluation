import { Page, expect } from '@playwright/test';

/** Signs in through the demo login screen by clicking the persona card with the given name. */
export async function loginAs(page: Page, englishName: string): Promise<void> {
  await page.goto('/login');
  const card = page.locator('.card', { hasText: englishName });
  await card.getByRole('link').click();
  await expect(page.getByText('Signed in as')).toContainText(englishName);
}

/** Personas seeded for the demo (English names, used to identify cards). */
export const Personas = {
  managingDirector: 'Ahmad Al-Rashid',
  hrAdmin: 'Layla Al-Sabah',
  sectorHead: 'Fatima Al-Otaibi',
  departmentManager: 'Yousef Al-Kandari',
  lineManager: 'Noura Al-Mutairi',
  employee: 'Khaled Al-Ajmi',
  midYear: 'Sara Al-Fahad',
  managingDirectorReport: 'Omar Al-Sabah',
} as const;
