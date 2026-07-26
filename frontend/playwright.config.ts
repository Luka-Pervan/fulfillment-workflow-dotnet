import { defineConfig, devices } from "@playwright/test";

export default defineConfig({
  testDir: "./e2e",
  fullyParallel: false,
  forbidOnly: Boolean(process.env.CI),
  retries: process.env.CI ? 1 : 0,
  workers: 1,
  reporter: process.env.CI ? "github" : "list",
  use: {
    baseURL: "http://127.0.0.1:5173",
    trace: "retain-on-failure"
  },
  projects: [
    {
      name: "chromium",
      use: { ...devices["Desktop Chrome"] }
    }
  ],
  webServer: [
    {
      command: "npm run e2e:api",
      url: "http://127.0.0.1:5080/health/ready",
      reuseExistingServer: false,
      timeout: 120_000
    },
    {
      command: "npm run dev -- --strictPort",
      url: "http://127.0.0.1:5173",
      reuseExistingServer: false,
      timeout: 30_000
    }
  ]
});
