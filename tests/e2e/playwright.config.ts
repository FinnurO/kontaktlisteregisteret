import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
  testDir: './specs',

  // Kjør sekvensielt — delt SQLite-database, parallelisme gir race conditions
  fullyParallel: false,
  workers: 1,

  // CI: forbid test.only, 2 retries. Lokalt: 0 retries
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,

  reporter: [['html', { open: 'never' }], ['list']],

  use: {
    baseURL: 'http://localhost:5100',
    // Behold trace ved første retry for debugging
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
    // Rommelige timeouts — Blazor Server gjør SignalR round-trips
    actionTimeout: 15_000,
    navigationTimeout: 30_000,
  },

  expect: {
    // 5% piksel-toleranse for visuell regresjon
    toHaveScreenshot: {
      maxDiffPixelRatio: 0.05,
      threshold: 0.2,
      animations: 'disabled',
    },
    timeout: 15_000,
  },

  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
  ],

  // Start .NET-appen før tester; gjenbruk eksisterende server lokalt
  webServer: {
    command: 'dotnet run --project ../../src/Kontaktlisteregisteret.Web',
    url: 'http://localhost:5100',
    reuseExistingServer: !process.env.CI,
    timeout: 120_000,
  },
});
