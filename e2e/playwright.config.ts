import path from "node:path";
import { defineConfig, devices } from "@playwright/test";
import {
  API_URL,
  REPOSITORY_ROOT,
  WEB_URL,
  backendEnvironment,
  webEnvironment,
} from "./support/environment";

const isCi = process.env["CI"] !== undefined;

/*
  The suite runs the real application: the API, the worker and a production
  build of the frontend, against the services Compose provides. Nothing is
  mocked — a browser test that stubs the API proves the browser talks to the
  stub.
*/
export default defineConfig({
  testDir: "./tests",
  globalSetup: "./support/global-setup.ts",

  // Flaky tests are fixed, not retried into passing.
  retries: 0,
  forbidOnly: isCi,

  /*
    One worker. The journeys share the worker process and its queues, and a
    notification test reading the wrong person's notice because another test
    ran beside it is exactly the kind of failure that looks like a product bug.
  */
  workers: 1,
  fullyParallel: false,

  timeout: 60_000,
  expect: { timeout: 10_000 },

  reporter: isCi ? [["list"], ["html", { open: "never" }]] : [["list"]],

  use: {
    baseURL: WEB_URL,
    locale: "tr-TR",
    timezoneId: "Europe/Istanbul",
    trace: "retain-on-failure",
    screenshot: "only-on-failure",
  },

  projects: [{ name: "chromium", use: { ...devices["Desktop Chrome"] } }],

  webServer: [
    {
      name: "API",
      command: "dotnet run --project backend/src/FlowDesk.Api --no-launch-profile",
      cwd: REPOSITORY_ROOT,
      env: backendEnvironment(),
      url: `${API_URL}/health/live`,
      reuseExistingServer: false,
      timeout: 180_000,
      stdout: "ignore",
      gracefulShutdown: { signal: "SIGTERM", timeout: 5_000 },
    },
    {
      name: "Worker",
      command: "dotnet run --project backend/src/FlowDesk.Worker --no-launch-profile",
      cwd: REPOSITORY_ROOT,
      env: backendEnvironment(),
      // No HTTP port: the worker is ready when its outbox loop has started.
      wait: { stdout: /Outbox işleyici başladı/ },
      reuseExistingServer: false,
      timeout: 180_000,
      gracefulShutdown: { signal: "SIGTERM", timeout: 5_000 },
    },
    {
      name: "Web",
      command: "npm run build && npm run start -- --port 3100",
      cwd: path.join(REPOSITORY_ROOT, "frontend"),
      env: webEnvironment(),
      url: WEB_URL,
      reuseExistingServer: false,
      timeout: 300_000,
      stdout: "ignore",
      gracefulShutdown: { signal: "SIGTERM", timeout: 5_000 },
    },
  ],
});
