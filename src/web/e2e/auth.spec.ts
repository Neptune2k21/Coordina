import { expect, test } from "@playwright/test"

test("user can register, sign out and sign in", async ({ page }) => {
  const email = `ada-${Date.now()}@coordina.test`
  const workspaceName = `Ada Labs ${Date.now()}`

  await page.goto("/login")
  await page.getByRole("button", { name: "Create an account" }).click()

  await page.getByLabel("Full name").fill("Ada Lovelace")
  await page.getByLabel("Email").fill(email)
  await page.getByLabel("Password").fill("Password123!")
  await page.getByRole("button", { name: /create workspace account/i }).click()

  await expect(
    page.getByRole("heading", {
      name: "Create or join your first workspace.",
    })
  ).toBeVisible()

  await page.getByRole("button", { name: /new workspace/i }).click()
  await page.getByLabel("Workspace name").fill(workspaceName)
  await page
    .getByRole("dialog")
    .getByRole("button", { name: /^create workspace$/i })
    .click()

  await expect(page.getByRole("heading", { name: workspaceName })).toBeVisible()
  await page.getByRole("button", { name: "User actions" }).click()
  await expect(page.getByRole("menuitem", { name: /sign out/i })).toBeVisible()

  await page.getByRole("menuitem", { name: /sign out/i }).click()
  await page.getByRole("button", { name: "Sign in" }).click()
  await page.getByLabel("Email").fill(email)
  await page.getByLabel("Password").fill("Password123!")
  await page
    .locator("form")
    .getByRole("button", { name: /sign in to coordina/i })
    .click()

  await expect(page.getByRole("heading", { name: workspaceName })).toBeVisible()
})
