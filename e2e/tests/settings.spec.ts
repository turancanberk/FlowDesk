import { expect, test } from "@playwright/test";
import { addMember, createWorkspace, register, uniqueName } from "../support/api";
import { signIn } from "../support/session";

/*
  The settings page the sidebar always linked to. Until Phase 17 the link led
  to a 404; the API behind it had existed since Phase 04.
*/
test.describe("Çalışma alanı ayarları", () => {
  test("sahip adı değiştirir; yeni ad her yerde görünür, adres değişmez", async ({ page }) => {
    const owner = await register("Oya Sahip");
    const workspace = await createWorkspace(owner);
    const newName = uniqueName("Yeni Ad");

    await signIn(page, owner);
    await page.goto(`/app/${workspace.slug}/dashboard`);
    await page.getByRole("link", { name: "Ayarlar" }).click();

    await expect(page.getByRole("heading", { name: "Ayarlar", level: 1 })).toBeVisible();

    const nameField = page.getByLabel("Çalışma alanı adı");
    await expect(nameField).toHaveValue(workspace.name);

    const save = page.getByRole("button", { name: "Kaydet" });
    // Nothing to save until something changes.
    await expect(save).toBeDisabled();

    await nameField.fill(newName);
    await save.click();

    await expect(page.getByText("Çalışma alanı adı güncellendi")).toBeVisible();
    await expect(
      page.getByRole("navigation", { name: "Ana gezinme" }).getByRole("button", { name: newName }),
    ).toBeVisible();
    await expect(page).toHaveURL(new RegExp(`/app/${workspace.slug}/settings$`));
  });

  test("yönetici adı değiştirebilir ama silemez; temsilci yalnızca görür", async ({ browser }) => {
    const owner = await register("Oya Sahip");
    const admin = await register("Yasin Yönetici");
    const agent = await register("Ali Temsilci");
    const workspace = await createWorkspace(owner);
    await addMember(owner, workspace, admin, "Admin");
    await addMember(owner, workspace, agent, "Agent");
    const settings = `/app/${workspace.slug}/settings`;

    const adminPage = await (await browser.newContext()).newPage();
    await signIn(adminPage, admin);
    await adminPage.goto(settings);
    await expect(adminPage.getByLabel("Çalışma alanı adı")).toBeEditable();
    await expect(adminPage.getByRole("button", { name: "Çalışma alanını sil" })).toHaveCount(0);

    const agentPage = await (await browser.newContext()).newPage();
    await signIn(agentPage, agent);
    await agentPage.goto(settings);
    await expect(agentPage.getByRole("heading", { name: "Ayarlar", level: 1 })).toBeVisible();
    await expect(agentPage.getByRole("main")).toContainText(workspace.name);
    await expect(agentPage.getByText("Ayarları yöneticiler değiştirebilir.")).toBeVisible();
    await expect(agentPage.getByRole("button", { name: "Kaydet" })).toHaveCount(0);
    await expect(agentPage.getByRole("button", { name: "Çalışma alanını sil" })).toHaveCount(0);
  });

  test("sahip çalışma alanını adını yazarak onaylayıp siler", async ({ page }) => {
    const owner = await register("Oya Sahip");
    const workspace = await createWorkspace(owner);

    await signIn(page, owner);
    await page.goto(`/app/${workspace.slug}/settings`);
    await page.getByRole("button", { name: "Çalışma alanını sil" }).click();

    const dialog = page.getByRole("dialog", { name: "Çalışma alanını sil" });
    const confirm = dialog.getByRole("button", { name: "Kalıcı olarak sil" });

    // A near miss is not a confirmation.
    await dialog.getByLabel("Onaylamak için çalışma alanının adını yazın").fill("yanlış ad");
    await expect(confirm).toBeDisabled();

    await dialog.getByLabel("Onaylamak için çalışma alanının adını yazın").fill(workspace.name);
    await confirm.click();

    await expect(page.getByRole("heading", { name: "Çalışma alanları" })).toBeVisible();
    await expect(page.getByText("Çalışma alanı silindi")).toBeVisible();
    await expect(page.getByRole("link", { name: new RegExp(workspace.name) })).toHaveCount(0);

    // Gone for real, not just from the list.
    await page.goto(`/app/${workspace.slug}/dashboard`);
    await expect(page.getByText("Çalışma alanı bulunamadı")).toBeVisible();
  });
});
