import { defineConfig, devices } from "@playwright/test"

export default defineConfig({
  testDir: "./e2e",
  fullyParallel: false,
  reporter: "list",
  use: {
    baseURL: "http://127.0.0.1:5174",
    trace: "retain-on-failure",
  },
  projects: [
    {
      name: "chromium",
      use: { ...devices["Desktop Chrome"] },
    },
  ],
  webServer: [
    // Never reuse a running server: it may serve other code, or another
    // database than the test one.
    {
      command: "make -C ../.. e2e-api",
      url: "http://127.0.0.1:5051/health/ready",
      reuseExistingServer: false,
      timeout: 300_000,
    },
    {
      command: "pnpm dev --host 127.0.0.1 --port 5174 --strictPort",
      env: { VITE_API_URL: "http://127.0.0.1:5051" },
      url: "http://127.0.0.1:5174",
      reuseExistingServer: false,
      timeout: 120_000,
    },
  ],
})
