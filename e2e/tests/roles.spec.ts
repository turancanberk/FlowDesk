import { expect, test } from "@playwright/test";
import {
  addMember,
  createCustomer,
  createTicket,
  createWorkspace,
  register,
  uploadAttachment,
} from "../support/api";
import { signIn } from "../support/session";

/*
  What each role is offered on screen. The interface is not the security
  boundary — the API refuses regardless, and Phase 16 checked every role
  against every endpoint — but a button that always ends in "Bu işlem için
  yetkiniz yok" is a product defect of its own.
*/
test.describe("Rollere göre arayüz", () => {
  test("izleyici okur ama hiçbir şeyi değiştirme olanağı görmez", async ({ page }) => {
    const owner = await register("Oya Sahip");
    const viewer = await register("İlke İzleyici");
    const workspace = await createWorkspace(owner);
    await addMember(owner, workspace, viewer, "Viewer");
    const customer = await createCustomer(owner, workspace);
    const ticket = await createTicket(owner, workspace, customer.id);
    await uploadAttachment(owner, workspace, ticket.id, "sozlesme.txt");

    await signIn(page, viewer);

    for (const [path, heading, createButton] of [
      ["customers", "Müşteriler", "Yeni müşteri"],
      ["tickets", "Talepler", "Yeni talep"],
      ["tasks", "Görevler", "Yeni görev"],
    ] as const) {
      await page.goto(`/app/${workspace.slug}/${path}`);
      await expect(page.getByRole("heading", { name: heading, level: 1 })).toBeVisible();
      await expect(page.getByRole("button", { name: createButton })).toHaveCount(0);
    }

    await page.goto(`/app/${workspace.slug}/tickets/${ticket.id}`);
    await expect(page.getByRole("heading", { name: ticket.subject, level: 1 })).toBeVisible();

    // Reading, and downloading, are what a viewer is for.
    await expect(page.getByText("sozlesme.txt")).toBeVisible();

    const main = page.getByRole("main");
    for (const name of [
      "Düzenle",
      "Sil",
      "Dosya ekle",
      "Yorum ekle",
      "sozlesme.txt dosyasını sil",
    ]) {
      await expect(main.getByRole("button", { name, exact: true })).toHaveCount(0);
    }
    await expect(main.getByRole("combobox", { name: "Durum" })).toHaveCount(0);
    await expect(main.getByRole("combobox", { name: "Atanan" })).toHaveCount(0);

    await page.goto(`/app/${workspace.slug}/team`);
    await expect(page.getByRole("button", { name: "Ekip üyesi davet et" })).toHaveCount(0);
  });

  test("temsilci dosya ekler ama silemez; sahip siler", async ({ browser }) => {
    const owner = await register("Oya Sahip");
    const agent = await register("Ali Temsilci");
    const workspace = await createWorkspace(owner);
    await addMember(owner, workspace, agent, "Agent");
    const customer = await createCustomer(owner, workspace);
    const ticket = await createTicket(owner, workspace, customer.id);
    const ticketPath = `/app/${workspace.slug}/tickets/${ticket.id}`;

    const agentPage = await (await browser.newContext()).newPage();
    await signIn(agentPage, agent);
    await agentPage.goto(ticketPath);

    // Upload through the real form: the hidden input behind "Dosya ekle".
    await agentPage.locator('input[type="file"]').setInputFiles({
      name: "hata-kaydi.txt",
      mimeType: "text/plain",
      buffer: Buffer.from("Hata kaydı"),
    });

    await expect(agentPage.getByText("Dosya eklendi")).toBeVisible();
    await expect(agentPage.getByRole("main").getByText("hata-kaydi.txt")).toBeVisible();

    // Removing a file is an admin's call (ADR-0040), as is deleting the ticket.
    await expect(
      agentPage.getByRole("button", { name: "hata-kaydi.txt dosyasını sil" }),
    ).toHaveCount(0);
    await expect(agentPage.getByRole("button", { name: "Sil", exact: true })).toHaveCount(0);

    const ownerPage = await (await browser.newContext()).newPage();
    await signIn(ownerPage, owner);
    await ownerPage.goto(ticketPath);

    await ownerPage.getByRole("button", { name: "hata-kaydi.txt dosyasını sil" }).click();

    await expect(ownerPage.getByText("Dosya silindi")).toBeVisible();
    await expect(ownerPage.getByText("Bu talebe dosya eklenmemiş.")).toBeVisible();
  });
});
