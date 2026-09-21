import { expect, test } from "@playwright/test";
import { API_URL } from "../support/environment";
import { createCustomer, createTicket, createWorkspace, register } from "../support/api";
import { signIn } from "../support/session";

/*
  The headers a browser actually receives, and the policy actually applied
  (docs/SECURITY.md §14). A Content-Security-Policy is easy to write and easy
  to write wrong: the way to find out is to drive the interface under it and
  watch for what the browser refused to run.
*/
test.describe("Güvenlik başlıkları", () => {
  test("belgeler başlıkları taşır ve CSP arayüzü kırmaz", async ({ page }) => {
    const refused: string[] = [];

    page.on("console", (message) => {
      if (/content security policy|refused to/i.test(message.text())) {
        refused.push(message.text());
      }
    });
    page.on("pageerror", (error) => refused.push(`sayfa hatası: ${error.message}`));

    const owner = await register("Oya Sahip");
    const workspace = await createWorkspace(owner);
    const customer = await createCustomer(owner, workspace);
    const ticket = await createTicket(owner, workspace, customer.id);

    const document = await page.goto("/giris");
    const headers = document?.headers() ?? {};

    expect(headers["x-content-type-options"]).toBe("nosniff");
    expect(headers["x-frame-options"]).toBe("DENY");
    expect(headers["referrer-policy"]).toBe("strict-origin-when-cross-origin");
    expect(headers["permissions-policy"]).toContain("camera=()");

    const policy = headers["content-security-policy"] ?? "";

    // A nonce per document, and no blanket permission for inline script.
    expect(policy).toMatch(/script-src [^;]*'nonce-[a-f0-9]+'/);
    expect(policy).not.toContain("'unsafe-inline' 'nonce");
    expect(policy).toContain("frame-ancestors 'none'");
    expect(policy).toContain("object-src 'none'");

    /*
      The policy has to name the origin the application actually calls. These
      are decided in two different places — the client that sends the request
      and the middleware that writes the policy — and when they disagreed the
      sign-in form loaded and every request it made was refused (Faz 23).
    */
    expect(policy).toContain(`connect-src 'self' ${API_URL}`);

    // Two documents, two nonces: a fixed one would be no better than none.
    const second = await page.goto("/kayit");
    expect(second?.headers()["content-security-policy"]).not.toBe(policy);

    /*
      Now drive the parts a policy usually breaks: hydration, the API call
      from the browser, a dialog that positions itself with inline styles,
      and a font served from the app's own origin.
    */
    await signIn(page, owner);
    await page.goto(`/app/${workspace.slug}/tickets/${ticket.id}`);
    await expect(page.getByRole("heading", { name: ticket.subject, level: 1 })).toBeVisible();

    await page.getByRole("combobox", { name: "Durum" }).click();
    await expect(page.getByRole("option", { name: "Devam Ediyor" })).toBeVisible();
    await page.keyboard.press("Escape");

    await page.goto(`/app/${workspace.slug}/customers`);
    await page.getByRole("button", { name: "Yeni müşteri" }).first().click();
    await expect(page.getByRole("dialog", { name: "Yeni müşteri" })).toBeVisible();

    expect(refused).toEqual([]);
  });

  test("API yanıtları kendi başlıklarını taşır", async ({ request }) => {
    const response = await request.get(`${API_URL}/health/live`);
    const headers = response.headers();

    expect(headers["x-content-type-options"]).toBe("nosniff");
    expect(headers["x-frame-options"]).toBe("DENY");
    expect(headers["referrer-policy"]).toBe("no-referrer");
    expect(headers["content-security-policy"]).toContain("frame-ancestors 'none'");
    expect(headers["cache-control"]).toContain("no-store");
    // The API does not announce what it is running.
    expect(headers["server"]).toBeUndefined();
  });
});
