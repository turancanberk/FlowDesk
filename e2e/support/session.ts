import { expect, type Page } from "@playwright/test";
import { PASSWORD, type Person } from "./api";

/**
 * Signs in through the form, as a person would.
 *
 * The access token lives only in the page's memory (ADR-0006), so there is no
 * storage state to reuse between tests: every signed-in page goes through the
 * real sign-in, which is also the only way the refresh cookie gets set.
 */
export async function signIn(page: Page, person: Person, next?: string): Promise<void> {
  await page.goto(next === undefined ? "/giris" : `/giris?next=${encodeURIComponent(next)}`);

  await page.getByLabel("E-posta").fill(person.email);
  await page.getByLabel("Parola").fill(PASSWORD);
  await page.getByRole("button", { name: "Giriş yap" }).click();

  if (next === undefined) {
    await expect(page.getByRole("heading", { name: "Çalışma alanları" })).toBeVisible();
  }
}

/** The sidebar's notification button, whatever its unread count says. */
export function notificationButton(page: Page) {
  return page.getByRole("navigation", { name: "Ana gezinme" }).getByRole("button", {
    name: /^Bildirimler/,
  });
}
