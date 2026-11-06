import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
  testDir: './specs',
  fullyParallel: true,
  reporter: 'list',
  use: {
    baseURL: process.env['UI_BASE_URL'] ?? 'http://localhost:4200',
    trace: 'retain-on-failure',
    ...devices['Desktop Chrome']
  }
});
