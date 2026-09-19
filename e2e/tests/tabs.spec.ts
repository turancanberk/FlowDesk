import { expect, test } from "@playwright/test";
import { createWorkspace, register } from "../support/api";
import { signIn } from "../support/session";

test.describe("Birden fazla sekme", () => {
  /*
    Two tabs share one refresh cookie. When both need a new access token at
    once — a laptop waking, a browser restoring its tabs — both send the same
    single-use token. Whichever the server handles second is presenting a
    spent token, which is what a stolen one looks like, and the whole session
    is revoked (ADR-0038). The tabs have to take turns.
  */
  test("aynı anda yenilenen iki sekme oturumu kaybetmez", async ({ context }) => {
    const person = await register("Mert Işık");
    const workspace = await createWorkspace(person);
    const dashboard = `/app/${workspace.slug}/dashboard`;

    const first = await context.newPage();
    await signIn(first, person);
    await first.goto(dashboard);

    const second = await context.newPage();
    await second.goto(dashboard);

    for (const tab of [first, second]) {
      await expect(tab.getByRole("heading", { name: "Dashboard", level: 1 })).toBeVisible();
    }

    /*
      Each refresh answer is held back for a moment, so the second tab's
      request leaves while the first tab's answer — and the new cookie it
      carries — is still on its way. Without that, the requests would overlap
      only when the timing happened to line up, and a race that does not
      happen passes.
    */
    await context.route("**/api/auth/refresh", async (route) => {
      const response = await route.fetch();
      await new Promise((resolve) => setTimeout(resolve, 750));
      await route.fulfill({ response });
    });

    await Promise.all([first.reload(), second.reload()]);

    for (const tab of [first, second]) {
      await expect(tab.getByRole("heading", { name: "Dashboard", level: 1 })).toBeVisible();
      await expect(tab).not.toHaveURL(/\/giris/);
    }

    // And both keep working afterwards: the session was not revoked behind
    // one tab's back.
    await context.unroute("**/api/auth/refresh");

    for (const tab of [first, second]) {
      await tab.getByRole("link", { name: "Talepler" }).click();
      await expect(tab.getByRole("heading", { name: "Talepler", level: 1 })).toBeVisible();
    }
  });
});
