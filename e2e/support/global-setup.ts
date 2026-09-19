import { API_URL } from "./environment";

interface ReadinessReport {
  status: string;
  checks: { name: string; status: string }[];
}

/**
 * Stops the run early, with a sentence a person can act on, when the services
 * behind the API are not there.
 *
 * The API reports itself live without a database, and readiness answers 200
 * even when degraded (ADR-0032). Without this check a stopped Compose stack
 * surfaces twenty seconds later as a registration form that never submits.
 */
export default async function globalSetup(): Promise<void> {
  const response = await fetch(`${API_URL}/health/ready`);
  const report = (await response.json()) as ReadinessReport;

  const unhealthy = report.checks.filter((check) => check.status !== "Healthy");

  if (unhealthy.length > 0) {
    const names = unhealthy.map((check) => `${check.name}: ${check.status}`).join(", ");

    throw new Error(
      `Altyapı hazır değil (${names}). Önce: ` +
        "docker compose --env-file .env -f infra/docker-compose.yml up -d",
    );
  }
}
