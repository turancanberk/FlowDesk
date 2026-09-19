import { expect, test } from "@playwright/test";
import { PASSWORD, createWorkspace, register, uniqueEmail } from "../support/api";
import { linkTo, waitForMailTo } from "../support/mailbox";
import { signIn } from "../support/session";

/*
  An invitation followed the way the invited person meets it: an e-mail in an
  inbox, a link, an account they do not have yet. The mail is real — sent by
  the worker through Mailpit — so the link under test is the one a person
  would click, not one the test assembled from the API's response.
*/
test("davet e-postasındaki bağlantıyla hesap açılır ve çalışma alanına katılınır", async ({
  browser,
}) => {
  const owner = await register("Oya Sahip");
  const workspace = await createWorkspace(owner);
  const inviteeEmail = uniqueEmail("davetli");

  const ownerPage = await (await browser.newContext()).newPage();
  await signIn(ownerPage, owner);
  await ownerPage.goto(`/app/${workspace.slug}/team`);

  await ownerPage.getByRole("button", { name: "Ekip üyesi davet et" }).click();
  const dialog = ownerPage.getByRole("dialog", { name: "Ekip üyesi davet et" });
  await dialog.getByLabel("E-posta").fill(inviteeEmail);
  await expect(dialog.getByRole("combobox", { name: "Rol" })).toContainText("Temsilci");
  await dialog.getByRole("button", { name: "Davet oluştur" }).click();

  // The result says what actually happens: the mail is on its way.
  const result = ownerPage.getByRole("dialog", { name: "Davet oluşturuldu" });
  await expect(result).toContainText(`${inviteeEmail} adresine davet e-postası gönderiliyor.`);
  await result.getByRole("button", { name: "Tamam" }).click();

  await expect(ownerPage.getByRole("main").getByText(inviteeEmail)).toBeVisible();

  // The invited person's side starts in their inbox.
  const mail = await waitForMailTo(inviteeEmail);
  expect(mail.subject).toContain(workspace.name);

  const invitationLink = linkTo(mail.html, "/davet?token=");
  expect(invitationLink.startsWith("http://localhost:3100/davet?token=")).toBe(true);

  const invitee = await (await browser.newContext()).newPage();
  await invitee.goto(invitationLink);

  await expect(
    invitee.getByRole("heading", { name: "Daveti kabul etmek için giriş yapın" }),
  ).toBeVisible();
  await invitee.getByRole("button", { name: "Hesap oluştur" }).click();

  await invitee.getByLabel("Ad soyad").fill("Can Yeni");
  await invitee.getByLabel("E-posta").fill(inviteeEmail);
  await invitee.getByLabel("Parola").fill(PASSWORD);
  await invitee.getByRole("button", { name: "Hesap oluştur" }).click();

  // Back to the invitation, which is accepted without another click.
  await expect(
    invitee.getByRole("heading", { name: `${workspace.name} çalışma alanına katıldınız` }),
  ).toBeVisible();
  await invitee.getByRole("button", { name: "Çalışma alanını aç" }).click();

  await expect(invitee.getByRole("heading", { name: "Dashboard", level: 1 })).toBeVisible();
  await expect(invitee.getByRole("navigation", { name: "Ana gezinme" })).toContainText("Temsilci");

  // The link is spent: opening it again does not join a second time.
  await invitee.goto(invitationLink);
  await expect(invitee.getByRole("heading", { name: "Davet kullanılamadı" })).toBeVisible();
});
