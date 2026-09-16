import { defineConfig, devices } from '@playwright/test'

const port = process.env.E2E_PORT ?? '3010'
const baseURL = process.env.E2E_BASE_URL ?? `http://localhost:${port}`

export default defineConfig({
  testDir: './e2e',
  fullyParallel: false,
  workers: 1,
  reporter: 'list',
  use: {
    baseURL,
    trace: 'on-first-retry',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
  webServer: process.env.E2E_BASE_URL
    ? undefined
    : {
        // The production build is the representative target: React's dev
        // Fast Refresh double-invokes mount effects, which DataTables would
        // surface as a duplicate initial draw that never ships.
        command: `npm run build && npx vite preview --port ${port} --strictPort`,
        url: baseURL,
        reuseExistingServer: true,
        timeout: 180_000,
      },
})
