import { expect, test } from "@playwright/test";
import { PASSWORD, createWorkspace, register, uniqueEmail } from "../support/api";
import { signIn } from "../support/session";

test.describe("Oturum", () => {
  test("kayıt, çıkış ve yeniden giriş", async ({ page, context }) => {
    const email = uniqueEmail("kayit");

    await page.goto("/kayit");
    await page.getByLabel("Ad soyad").fill("Selin Kaya");
    await page.getByLabel("E-posta").fill(email);
    await page.getByLabel("Parola").fill(PASSWORD);
    await page.getByRole("button", { name: "Hesap oluştur" }).click();

    await expect(page.getByRole("heading", { name: "Çalışma alanları" })).toBeVisible();
    await expect(page.getByText("Hoş geldiniz, Selin Kaya.")).toBeVisible();

    // ADR-0006: the access token never reaches storage script can read, and
    // the refresh token is a cookie script cannot read.
    const storage = await page.evaluate(() => ({
      local: window.localStorage.length,
      session: window.sessionStorage.length,
    }));
    expect(storage).toEqual({ local: 0, session: 0 });

    const refreshCookie = (await context.cookies()).find(
      (cookie) => cookie.name === "flowdesk_refresh_token",
    );
    expect(refreshCookie?.httpOnly).toBe(true);
    expect(refreshCookie?.sameSite).toBe("Strict");

    await page.getByRole("button", { name: "Çıkış yap" }).click();
    await expect(page).toHaveURL(/\/giris$/);

    // Signed out means signed out: the protected page sends us back.
    await page.goto("/");
    await expect(page).toHaveURL(/\/giris$/);

    await page.getByLabel("E-posta").fill(email);
    await page.getByLabel("Parola").fill(PASSWORD);
    await page.getByRole("button", { name: "Giriş yap" }).click();

    await expect(page.getByRole("heading", { name: "Çalışma alanları" })).toBeVisible();
  });

  test("yanlış parola, hangi bilginin yanlış olduğunu söylemeden reddedilir", async ({ page }) => {
    const person = await register("Burak Er");

    await page.goto("/giris");
    await page.getByLabel("E-posta").fill(person.email);
    await page.getByLabel("Parola").fill("yanlis-parola-2026");
    await page.getByRole("button", { name: "Giriş yap" }).click();

    await expect(page.getByText("E-posta veya parola hatalı.")).toBeVisible();
    await expect(page).toHaveURL(/\/giris$/);
  });

  test("oturum sayfa yenilemesinden sonra sürer", async ({ page }) => {
    const person = await register("Deniz Ak");
    const workspace = await createWorkspace(person);

    await signIn(page, person);
    await page.goto(`/app/${workspace.slug}/dashboard`);
    await expect(page.getByRole("heading", { name: "Dashboard", level: 1 })).toBeVisible();

    // The token was in memory and is gone; the refresh cookie brings it back.
    await page.reload();

    await expect(page.getByRole("heading", { name: "Dashboard", level: 1 })).toBeVisible();
    await expect(page).toHaveURL(new RegExp(`/app/${workspace.slug}/dashboard$`));
  });

  test("süresi dolan access token sessizce yenilenir", async ({ page }) => {
    const person = await register("Ece Yurt");
    const workspace = await createWorkspace(person);

    await page.clock.install();
    await signIn(page, person);
    await page.goto(`/app/${workspace.slug}/dashboard`);
    await expect(page.getByRole("heading", { name: "Dashboard", level: 1 })).toBeVisible();

    // Ten minutes is the token's lifetime; past it, the client must renew
    // before its next request rather than send a token it knows is stale.
    await page.clock.fastForward("11:00");

    const refreshed = page.waitForResponse(
      (response) => response.url().endsWith("/api/auth/refresh") && response.status() === 200,
    );

    await page.getByRole("link", { name: "Talepler" }).click();

    await refreshed;
    await expect(page.getByRole("heading", { name: "Talepler", level: 1 })).toBeVisible();
    await expect(page).not.toHaveURL(/\/giris/);
  });
});
