import { existsSync, readFileSync } from "node:fs";
import path from "node:path";

/*
  Ports of their own, so a suite run never lands on the servers a developer
  already has open. The browser tests need processes they started themselves:
  an API left running from earlier carries rate-limit counters and a build of
  the frontend that may not match the code under test.
*/
export const WEB_PORT = 3100;
export const API_PORT = 5180;

export const WEB_URL = `http://localhost:${WEB_PORT}`;
export const API_URL = `http://localhost:${API_PORT}`;

export const REPOSITORY_ROOT = path.resolve(import.meta.dirname, "..", "..");

/**
 * Reads the repository's `.env`, falling back to `.env.example`.
 *
 * The same file the developer uses for Compose, so the tests reach the same
 * PostgreSQL, RabbitMQ, Mailpit, Azurite and Redis. Values are read as the
 * shell would read them after `set -a; . ./.env`: surrounding quotes are
 * removed, and a quoted value keeps its semicolons (HANDOFF, item 2).
 */
export function readDotEnv(): Record<string, string> {
  const candidates = [".env", ".env.example"].map((name) => path.join(REPOSITORY_ROOT, name));
  const file = candidates.find((candidate) => existsSync(candidate));

  if (file === undefined) {
    throw new Error("Depo kökünde .env ya da .env.example bulunamadı.");
  }

  const values: Record<string, string> = {};

  for (const rawLine of readFileSync(file, "utf8").split(/\r?\n/)) {
    const line = rawLine.trim();

    if (line === "" || line.startsWith("#")) {
      continue;
    }

    const separator = line.indexOf("=");

    if (separator <= 0) {
      continue;
    }

    const key = line.slice(0, separator).trim();
    let value = line.slice(separator + 1).trim();

    if (value.length >= 2 && (value.startsWith('"') || value.startsWith("'"))) {
      const quote = value[0];

      if (value.endsWith(quote ?? "")) {
        value = value.slice(1, -1);
      }
    }

    values[key] = value;
  }

  return values;
}

/**
 * The environment both backend hosts run with during the suite.
 *
 * Everything the developer configured, with only what the suite has to own
 * replaced: the addresses, the environment name and the per-address limits.
 */
export function backendEnvironment(): Record<string, string> {
  return {
    ...inheritedEnvironment(),
    ...readDotEnv(),
    ASPNETCORE_ENVIRONMENT: "Development",
    DOTNET_ENVIRONMENT: "Development",
    ASPNETCORE_URLS: API_URL,
    Cors__AllowedOrigins__0: WEB_URL,
    Email__WebBaseUrl: WEB_URL,
    /*
      Every account in the suite is created from one address. The production
      limits stay the defaults (ADR-0041); the integration tests are where the
      limits themselves are checked.
    */
    RateLimiting__RegistrationPermitLimit: "200",
    RateLimiting__LoginPermitLimit: "200",
    RateLimiting__RefreshPermitLimit: "500",
    RateLimiting__InvitationAcceptancePermitLimit: "200",
    // The shortest interval allowed, so a notification test waits on the
    // chain rather than on the clock.
    Outbox__PollIntervalSeconds: "1",
  };
}

export function webEnvironment(): Record<string, string> {
  return {
    ...inheritedEnvironment(),
    // Inlined into the bundle at build time, which is why the suite builds
    // the frontend itself rather than reusing an existing build.
    NEXT_PUBLIC_API_BASE_URL: API_URL,
    NEXT_TELEMETRY_DISABLED: "1",
  };
}

function inheritedEnvironment(): Record<string, string> {
  return Object.fromEntries(
    Object.entries(process.env).filter(
      (entry): entry is [string, string] => entry[1] !== undefined,
    ),
  );
}
