import { defineConfig, devices } from "@playwright/test"

const webBuild = "node_modules/.cache/e2e-build"

export default defineConfig({
  // A test.only left behind would silently skip every other test.
  forbidOnly: !!process.env.CI,
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
    // The production build, as users get it. The development server also
    // compiles each module on demand, which makes page loads slow and uneven.
    {
      command: `pnpm exec vite build --outDir ${webBuild} && pnpm exec vite preview --outDir ${webBuild} --host 127.0.0.1 --port 5174 --strictPort`,
      env: { VITE_API_URL: "http://127.0.0.1:5051" },
      url: "http://127.0.0.1:5174",
      reuseExistingServer: false,
      timeout: 120_000,
    },
  ],
})
