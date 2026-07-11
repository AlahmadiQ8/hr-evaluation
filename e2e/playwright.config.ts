import { defineConfig, devices } from '@playwright/test';

/**
 * Runs against a live Taqyeem app (start it with `aspire run`, or in CI orchestrate
 * start -> wait -> test). The Blazor Web app has a stable dev URL from its launch profile.
 */
export default defineConfig({
  testDir: './tests',
  timeout: 45_000,
  expect: { timeout: 10_000 },
  // Evaluations mutate shared demo data, so run serially and don't retry (retries would
  // re-run an already-applied mutation). Reset the demo data before a full run.
  fullyParallel: false,
  workers: 1,
  retries: 0,
  reporter: process.env.CI ? [['list'], ['html', { open: 'never' }]] : [['list']],
  use: {
    baseURL: process.env.BASE_URL ?? 'https://localhost:7039',
    ignoreHTTPSErrors: true,
    trace: 'retain-on-failure',
    video: 'retain-on-failure',
    screenshot: 'only-on-failure',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
});
