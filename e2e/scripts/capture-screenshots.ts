/*
  Regenerates the screenshots in docs/images (Faz 22).

  A script rather than a folder of images somebody once took by hand: an image
  with no way to reproduce it is a claim nobody can check, and the first
  interface change makes it a lie that looks like documentation.

  It captures the demo data (Faz 21), so the screens are full without inventing
  anything. Every address in them is at a reserved domain (RFC 2606).

  Prerequisites — the application running against a database that has been
  seeded:

    dotnet run --project backend/src/FlowDesk.Api -- --seed-demo-data
    dotnet run --project backend/src/FlowDesk.Api
    npm --prefix frontend run build && npm --prefix frontend run start

  Then, from the repository root:

    npm --prefix e2e run screenshots

  The production build is deliberate: `next dev` draws a development indicator
  over the corner of every page.
*/

import { chromium, type Page } from "@playwright/test";
import { mkdir, readdir, rm } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

const REPOSITORY_ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..", "..");
const OUTPUT_DIRECTORY = path.join(REPOSITORY_ROOT, "docs", "images");

const WEB_URL = process.env.SCREENSHOT_WEB_URL ?? "http://localhost:3000";
const EMAIL = process.env.SCREENSHOT_EMAIL ?? "elif.demir@flowdesk.example";
const PASSWORD = process.env.SCREENSHOT_PASSWORD ?? "DemoParola2026";
const WORKSPACE = process.env.SCREENSHOT_WORKSPACE ?? "aydin-yazilim";

/** Wide enough for the sidebar and a table, short enough to read in a README. */
const VIEWPORT = { width: 1440, height: 900 };

async function main(): Promise<void> {
  await rm(OUTPUT_DIRECTORY, { recursive: true, force: true });
  await mkdir(OUTPUT_DIRECTORY, { recursive: true });

  const browser = await chromium.launch();
  const context = await browser.newContext({
    viewport: VIEWPORT,
    deviceScaleFactor: 2,
    locale: "tr-TR",
    timezoneId: "Europe/Istanbul",
    baseURL: WEB_URL,
  });

  const page = await context.newPage();

  try {
    await signIn(page);

    await capture(page, `/app/${WORKSPACE}/dashboard`, "dashboard", "Dashboard");
    await capture(page, `/app/${WORKSPACE}/tickets`, "talepler", "Talepler");
    await captureTicketWithConversation(page);
    await capture(page, `/app/${WORKSPACE}/customers`, "musteriler", "Müşteriler");
    await capture(page, `/app/${WORKSPACE}/team`, "ekip", "Ekip");
    await capture(page, `/app/${WORKSPACE}/activity`, "etkinlik", "Etkinlik");
  } finally {
    await context.close();
    await browser.close();
  }

  const written = (await readdir(OUTPUT_DIRECTORY)).sort();
  console.log(`${written.length} görüntü yazıldı: ${written.join(", ")}`);
}

async function signIn(page: Page): Promise<void> {
  await page.goto("/giris");
  await page.getByLabel("E-posta").fill(EMAIL);
  await page.getByLabel("Parola").fill(PASSWORD);
  await page.getByRole("button", { name: "Giriş yap" }).click();
  await page.getByRole("heading", { name: "Çalışma alanları" }).waitFor();
}

/**
 * A ticket that has a conversation on it.
 *
 * The newest ticket is the one with the least on it — no comments, often
 * nobody assigned — and a screenshot of an empty screen argues against the
 * product rather than for it. This opens tickets until it finds one that has
 * been worked on.
 */
async function captureTicketWithConversation(page: Page): Promise<void> {
  await page.goto(`/app/${WORKSPACE}/tickets`);
  await page.getByRole("heading", { name: "Talepler", exact: true, level: 1 }).waitFor();

  const links = await page
    .getByRole("link", { name: /^TLP-/ })
    .evaluateAll((elements) => elements.map((element) => element.getAttribute("href") ?? ""));

  for (const href of links) {
    await page.goto(href);
    await page.getByRole("heading", { level: 1 }).waitFor();
    await settle(page);

    const empty = await page.getByText("Bu talebe henüz yorum yazılmamış").count();

    if (empty === 0) {
      await write(page, "talep-detayi");

      return;
    }
  }

  throw new Error("Yorumu olan bir demo talebi bulunamadı; demo verisi yazıldı mı?");
}

async function capture(page: Page, route: string, name: string, heading: string): Promise<void> {
  await page.goto(route);

  // Waits for the screen to have arrived rather than for a fixed delay: a
  // screenshot of a loading skeleton documents nothing.
  await page.getByRole("heading", { name: heading, exact: true, level: 1 }).waitFor();
  await settle(page);
  await write(page, name);
}

async function settle(page: Page): Promise<void> {
  await page.waitForLoadState("networkidle");
  await page.evaluate(() => document.fonts.ready);
}

async function write(page: Page, name: string): Promise<void> {
  const file = path.join(OUTPUT_DIRECTORY, `${name}.png`);

  await page.screenshot({ path: file });
  console.log(`  ${name}.png`);
}

await main();
