import { expect, test } from "@playwright/test";
import { addMember, listNotifications, register, uniqueName } from "../support/api";
import { notificationButton, signIn } from "../support/session";

/*
  The product's core loop, end to end: a workspace, a customer, a ticket, a
  comment, an assignment — and the assignee finding out. The last step runs
  through the outbox, the worker, RabbitMQ and a consumer before it reaches
  the browser, which is why a notification is worth a browser test at all:
  Phase 16 found that chain had been silently broken since Phase 15.
*/
test("talep açılır, atanır ve atanan kişi bildirimle haberdar olur", async ({ browser }) => {
  const owner = await register("Oya Sahip");
  const agent = await register("Ali Temsilci");

  const ownerPage = await (await browser.newContext()).newPage();
  await signIn(ownerPage, owner);

  // Workspace
  const workspaceName = uniqueName("Destek Ekibi");
  await ownerPage.getByRole("button", { name: "Yeni çalışma alanı" }).first().click();
  await ownerPage.getByLabel("Çalışma alanı adı").fill(workspaceName);
  await ownerPage.getByRole("button", { name: "Çalışma alanı oluştur" }).click();

  await expect(ownerPage.getByRole("heading", { name: "Dashboard", level: 1 })).toBeVisible();
  const slug = /\/app\/([^/]+)\/dashboard/.exec(ownerPage.url())?.[1] ?? "";
  expect(slug).not.toBe("");

  await addMember(owner, { id: "", slug, name: workspaceName }, agent, "Agent");

  // Customer
  const customerName = uniqueName("Yıldız Lojistik");
  await ownerPage.getByRole("link", { name: "Müşteriler" }).click();
  await ownerPage.getByRole("button", { name: "Yeni müşteri" }).first().click();

  const customerDialog = ownerPage.getByRole("dialog", { name: "Yeni müşteri" });
  await customerDialog.getByLabel("Müşteri adı").fill(customerName);
  // The status is shown in Turkish, not as the code the API speaks.
  const status = customerDialog.getByRole("combobox", { name: "Durum" });
  await expect(status).toContainText("Aktif");
  await expect(status).not.toContainText("Active");
  await customerDialog.getByRole("button", { name: "Kaydet" }).click();

  await expect(ownerPage.getByRole("link", { name: customerName })).toBeVisible();

  // Ticket
  const subject = uniqueName("Fatura PDF'i indirilemiyor");
  await ownerPage.getByRole("link", { name: "Talepler" }).click();
  await ownerPage.getByRole("button", { name: "Yeni talep" }).first().click();

  const ticketDialog = ownerPage.getByRole("dialog", { name: "Yeni talep" });
  await ticketDialog.getByLabel("Konu").fill(subject);
  await ticketDialog.getByRole("combobox", { name: "Müşteri" }).click();
  await ownerPage.getByRole("option", { name: customerName }).click();
  await ticketDialog.getByRole("button", { name: "Talebi aç" }).click();

  await ownerPage.getByRole("link", { name: new RegExp(subject) }).click();
  await expect(ownerPage.getByRole("heading", { name: subject, level: 1 })).toBeVisible();

  // Work on it: a comment, a status change, then the assignment.
  await ownerPage.getByLabel("Yorum").fill("Müşteriyle görüşüldü, fatura yeniden üretilecek.");
  await ownerPage.getByRole("button", { name: "Yorum ekle" }).click();
  await expect(
    ownerPage.getByText("Müşteriyle görüşüldü, fatura yeniden üretilecek."),
  ).toBeVisible();

  await ownerPage.getByRole("combobox", { name: "Durum" }).click();
  await ownerPage.getByRole("option", { name: "Devam Ediyor" }).click();
  await expect(ownerPage.getByRole("combobox", { name: "Durum" })).toContainText("Devam Ediyor");

  await ownerPage.getByRole("combobox", { name: "Atanan" }).click();
  await ownerPage.getByRole("option", { name: "Ali Temsilci" }).click();
  await expect(ownerPage.getByRole("combobox", { name: "Atanan" })).toContainText("Ali Temsilci");

  // The assignee, in their own browser — signed in straight away, before the
  // worker has had time to deliver anything.
  const agentPage = await (await browser.newContext()).newPage();
  await agentPage.clock.install();
  await signIn(agentPage, agent);
  await agentPage.goto(`/app/${slug}/dashboard`);
  await expect(agentPage.getByRole("heading", { name: "Dashboard", level: 1 })).toBeVisible();

  // The worker delivers: outbox, broker, consumer, notice row.
  await expect
    .poll(async () => (await listNotifications(agent, slug)).unreadCount, {
      message: "Worker bildirimi yazmadı",
      timeout: 30_000,
    })
    .toBe(1);

  /*
    The panel asks once a minute; there is no push (docs/ROADMAP.md). Moving
    the page's clock past that minute checks the polling itself, instead of
    waiting for it.
  */
  await agentPage.clock.fastForward("01:01");
  await expect(notificationButton(agentPage)).toHaveAccessibleName("Bildirimler, 1 okunmamış");

  await notificationButton(agentPage).click();
  const panel = agentPage.getByRole("dialog", { name: "Bildirimler" });
  await panel.getByRole("link", { name: new RegExp(`size atandı ${subject}`) }).click();

  await expect(agentPage.getByRole("heading", { name: subject, level: 1 })).toBeVisible();
  await expect(notificationButton(agentPage)).toHaveAccessibleName("Bildirimler");

  // Signing out from inside a workspace ends the session, too.
  await agentPage.getByRole("button", { name: "Çıkış yap" }).click();
  await expect(agentPage).toHaveURL(/\/giris$/);
});
